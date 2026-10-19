using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MadelineInPharloom
{
    /// <summary>
    /// Reads Madeline's sounds straight out of the player's Celeste sound banks (FMOD Studio .bank files hold an FSB5
    /// sample bank). Celeste's samples are Vorbis with FMOD's shared setup header stripped; each one is rebuilt as an
    /// Ogg Vorbis stream in memory (the setup header comes from python-fsb5's table, MIT, Simon Pinfold) and decoded
    /// with NVorbis (MIT). No helper program: nothing from outside the game process runs (user 2026-10-13).
    /// </summary>
    public class CelesteBank
    {
        public class Sample { public string Name; public int Rate, Channels, Offset, Length; public uint Crc; }

        readonly byte[] data;   // the bank up to the end of its sample names (the audio is read on demand)
        readonly string path;
        readonly long dataStart;
        public readonly Dictionary<string, Sample> Samples = new Dictionary<string, Sample>();

        static readonly int[] Rates = { 4000, 8000, 11000, 11025, 16000, 22050, 24000, 32000, 44100, 48000, 96000 };

        public CelesteBank(string path)
        {
            this.path = path;
            // the FSB5 header sits after the Studio metadata, within the first few MB
            byte[] head;
            using (var fs = File.OpenRead(path)) { head = new byte[(int)Math.Min(fs.Length, 16 << 20)]; ReadFully(fs, head, head.Length); }
            int i = IndexOf(head, Encoding.ASCII.GetBytes("FSB5"));
            if (i < 0) throw new Exception(Path.GetFileName(path) + " holds no FSB5 sample bank");
            int hdrEnd = i + 60 + BitConverter.ToInt32(head, i + 12) + BitConverter.ToInt32(head, i + 16);
            if (hdrEnd > head.Length) using (var fs = File.OpenRead(path)) { head = new byte[hdrEnd]; ReadFully(fs, head, hdrEnd); }
            data = head;
            int num = BitConverter.ToInt32(data, i + 8), headers = BitConverter.ToInt32(data, i + 12), names = BitConverter.ToInt32(data, i + 16);
            int dataSize = BitConverter.ToInt32(data, i + 20), codec = BitConverter.ToInt32(data, i + 24);
            if (codec != 15) throw new Exception(Path.GetFileName(path) + ": codec " + codec + " is not Vorbis");
            int p = i + 60;
            var list = new List<Sample>();
            for (int s = 0; s < num; s++)
            {
                ulong m = BitConverter.ToUInt64(data, p); p += 8;
                var smp = new Sample
                {
                    Rate = Rates[Math.Min((int)((m >> 1) & 0xF), Rates.Length - 1)],
                    Channels = (int)((m >> 5) & 1) + 1,
                    Offset = (int)(((m >> 6) & 0x0FFFFFFF) * 16),
                };
                bool next = (m & 1) != 0;
                while (next)
                {
                    uint c = BitConverter.ToUInt32(data, p); p += 4;
                    next = (c & 1) != 0;
                    int size = (int)((c >> 1) & 0xFFFFFF), type = (int)((c >> 25) & 0x7F);
                    if (type == 1) smp.Channels = data[p];
                    else if (type == 2) smp.Rate = BitConverter.ToInt32(data, p);
                    else if (type == 11) smp.Crc = BitConverter.ToUInt32(data, p);
                    p += size;
                }
                list.Add(smp);
            }
            int nameBase = i + 60 + headers;
            for (int s = 0; s < num; s++)
            {
                int st = nameBase + BitConverter.ToInt32(data, nameBase + s * 4), e = st;
                while (data[e] != 0) e++;
                list[s].Name = Encoding.UTF8.GetString(data, st, e - st);
            }
            dataStart = nameBase + names;
            for (int s = 0; s < num; s++)
            {
                int end = s + 1 < num ? list[s + 1].Offset : dataSize;
                list[s].Length = end - list[s].Offset;
                Samples[list[s].Name] = list[s];
            }
        }

        /// <summary>The sample decoded to interleaved floats.</summary>
        public float[] Decode(Sample s, out int channels, out int rate)
        {
            channels = s.Channels; rate = s.Rate;
            byte[] setup = VorbisSetup.For(s.Crc);
            if (setup == null) throw new Exception("no Vorbis setup header for crc " + s.Crc);
            byte[] ogg = Rebuild(s, setup);
            using (var reader = new NVorbis.VorbisReader(new MemoryStream(ogg), true))
            {
                var all = new List<float>();
                var buf = new float[4096 * Math.Max(1, reader.Channels)];
                int n;
                while ((n = reader.ReadSamples(buf, 0, buf.Length)) > 0)
                    for (int k = 0; k < n; k++) all.Add(buf[k]);
                channels = reader.Channels; rate = reader.SampleRate;
                return all.ToArray();
            }
        }

        // python-fsb5's rebuild: identification header (blocksizes 256 / 2048), an empty comment header, the setup
        // header, then FMOD's [u16 size][packet] audio packets, one packet per Ogg page.
        byte[] Rebuild(Sample s, byte[] setup)
        {
            var ogg = new MemoryStream();
            int seq = 0;
            var id = new MemoryStream();
            id.WriteByte(1); id.Write(Encoding.ASCII.GetBytes("vorbis"), 0, 6);
            id.Write(BitConverter.GetBytes(0), 0, 4);
            id.WriteByte((byte)s.Channels);
            id.Write(BitConverter.GetBytes(s.Rate), 0, 4);
            id.Write(BitConverter.GetBytes(0), 0, 4); id.Write(BitConverter.GetBytes(0), 0, 4); id.Write(BitConverter.GetBytes(0), 0, 4);
            id.WriteByte(0xB8);   // blocksize_0 = 2^8, blocksize_1 = 2^11
            id.WriteByte(1);      // framing
            Page(ogg, id.ToArray(), 0, 0x02, ref seq);
            var comment = new MemoryStream();
            comment.WriteByte(3); comment.Write(Encoding.ASCII.GetBytes("vorbis"), 0, 6);
            byte[] vendor = Encoding.ASCII.GetBytes("Silkeste");
            comment.Write(BitConverter.GetBytes(vendor.Length), 0, 4); comment.Write(vendor, 0, vendor.Length);
            comment.Write(BitConverter.GetBytes(0), 0, 4);
            comment.WriteByte(1);
            Page(ogg, comment.ToArray(), 0, 0, ref seq);
            Page(ogg, setup, 0, 0, ref seq);
            byte[] raw = new byte[s.Length];
            using (var fs = File.OpenRead(path)) { fs.Seek(dataStart + s.Offset, SeekOrigin.Begin); ReadFully(fs, raw, raw.Length); }
            int p = 0, end = raw.Length;
            long granule = 0;
            var packets = new List<byte[]>();
            while (p + 2 <= end)
            {
                int size = BitConverter.ToUInt16(raw, p); p += 2;
                if (size == 0 || p + size > end) break;
                var pk = new byte[size]; Buffer.BlockCopy(raw, p, pk, 0, size); p += size;
                packets.Add(pk);
            }
            for (int k = 0; k < packets.Count; k++)
            {
                granule += 1024;   // an upper bound per packet: the stream ends where the packets end
                Page(ogg, packets[k], granule, (byte)(k == packets.Count - 1 ? 0x04 : 0), ref seq);
            }
            return ogg.ToArray();
        }

        static void Page(Stream o, byte[] packet, long granule, byte type, ref int seq)
        {
            int segs = packet.Length / 255 + 1;
            if (segs > 255) throw new Exception("Vorbis packet too large for one Ogg page");
            var h = new byte[27 + segs];
            Encoding.ASCII.GetBytes("OggS").CopyTo(h, 0);
            h[4] = 0; h[5] = type;
            BitConverter.GetBytes(granule).CopyTo(h, 6);
            BitConverter.GetBytes(0x53494C4B).CopyTo(h, 14);   // serial
            BitConverter.GetBytes(seq++).CopyTo(h, 18);
            h[26] = (byte)segs;
            int left = packet.Length;
            for (int k = 0; k < segs; k++) { int v = Math.Min(255, left); h[27 + k] = (byte)v; left -= v; }
            uint crc = 0;
            crc = Crc(crc, h, 0, h.Length);
            crc = Crc(crc, packet, 0, packet.Length);
            BitConverter.GetBytes(crc).CopyTo(h, 22);
            o.Write(h, 0, h.Length);
            o.Write(packet, 0, packet.Length);
        }

        static uint[] crcTable;
        static uint Crc(uint crc, byte[] b, int off, int len)
        {
            if (crcTable == null)
            {
                crcTable = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint r = i << 24;
                    for (int k = 0; k < 8; k++) r = (r & 0x80000000) != 0 ? (r << 1) ^ 0x04C11DB7 : r << 1;
                    crcTable[i] = r;
                }
            }
            for (int i = off; i < off + len; i++) crc = (crc << 8) ^ crcTable[((crc >> 24) ^ b[i]) & 0xFF];
            return crc;
        }

        static void ReadFully(Stream s, byte[] buf, int count)
        {
            int got = 0, n;
            while (got < count && (n = s.Read(buf, got, count - got)) > 0) got += n;
        }

        /// <summary>A sample by its name, or its first numbered variation (name_01, name_02...).</summary>
        public Sample Find(string name)
        {
            Sample s;
            if (Samples.TryGetValue(name, out s) || Samples.TryGetValue(name + "_01", out s) || Samples.TryGetValue(name + "_1", out s)) return s;
            foreach (var kv in Samples) if (kv.Key.StartsWith(name + "_", StringComparison.Ordinal)) return kv.Value;
            return null;
        }

        static int IndexOf(byte[] hay, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= hay.Length; i++)
            {
                int k = 0;
                while (k < needle.Length && hay[i + k] == needle[k]) k++;
                if (k == needle.Length) return i;
            }
            return -1;
        }
    }
}
