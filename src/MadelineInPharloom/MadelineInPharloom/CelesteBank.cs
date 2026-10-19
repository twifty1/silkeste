using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NVorbis;

namespace MadelineInPharloom;

public class CelesteBank
{
	public class Sample
	{
		public string Name;

		public int Rate;

		public int Channels;

		public int Offset;

		public int Length;

		public uint Crc;
	}

	private readonly byte[] data;

	private readonly string path;

	private readonly long dataStart;

	public readonly Dictionary<string, Sample> Samples = new Dictionary<string, Sample>();

	private static readonly int[] Rates = new int[11]
	{
		4000, 8000, 11000, 11025, 16000, 22050, 24000, 32000, 44100, 48000,
		96000
	};

	private static uint[] crcTable;

	public CelesteBank(string path)
	{
		this.path = path;
		byte[] array;
		using (FileStream fileStream = File.OpenRead(path))
		{
			array = new byte[(int)Math.Min(fileStream.Length, 16777216L)];
			ReadFully(fileStream, array, array.Length);
		}
		int num = IndexOf(array, Encoding.ASCII.GetBytes("FSB5"));
		if (num < 0)
		{
			throw new Exception(Path.GetFileName(path) + " holds no FSB5 sample bank");
		}
		int num2 = num + 60 + BitConverter.ToInt32(array, num + 12) + BitConverter.ToInt32(array, num + 16);
		if (num2 > array.Length)
		{
			using FileStream s = File.OpenRead(path);
			array = new byte[num2];
			ReadFully(s, array, num2);
		}
		data = array;
		int num3 = BitConverter.ToInt32(data, num + 8);
		int num4 = BitConverter.ToInt32(data, num + 12);
		int num5 = BitConverter.ToInt32(data, num + 16);
		int num6 = BitConverter.ToInt32(data, num + 20);
		int num7 = BitConverter.ToInt32(data, num + 24);
		if (num7 != 15)
		{
			throw new Exception(Path.GetFileName(path) + ": codec " + num7 + " is not Vorbis");
		}
		int num8 = num + 60;
		List<Sample> list = new List<Sample>();
		for (int i = 0; i < num3; i++)
		{
			ulong num9 = BitConverter.ToUInt64(data, num8);
			num8 += 8;
			Sample sample = new Sample
			{
				Rate = Rates[Math.Min((int)((num9 >> 1) & 0xF), Rates.Length - 1)],
				Channels = (int)((num9 >> 5) & 1) + 1,
				Offset = (int)(((num9 >> 6) & 0xFFFFFFF) * 16)
			};
			bool flag = (num9 & 1) != 0;
			while (flag)
			{
				uint num10 = BitConverter.ToUInt32(data, num8);
				num8 += 4;
				flag = (num10 & 1) != 0;
				int num11 = (int)((num10 >> 1) & 0xFFFFFF);
				switch ((int)((num10 >> 25) & 0x7F))
				{
				case 1:
					sample.Channels = data[num8];
					break;
				case 2:
					sample.Rate = BitConverter.ToInt32(data, num8);
					break;
				case 11:
					sample.Crc = BitConverter.ToUInt32(data, num8);
					break;
				}
				num8 += num11;
			}
			list.Add(sample);
		}
		int num12 = num + 60 + num4;
		for (int j = 0; j < num3; j++)
		{
			int num13 = num12 + BitConverter.ToInt32(data, num12 + j * 4);
			int k;
			for (k = num13; data[k] != 0; k++)
			{
			}
			list[j].Name = Encoding.UTF8.GetString(data, num13, k - num13);
		}
		dataStart = num12 + num5;
		for (int l = 0; l < num3; l++)
		{
			int num14 = ((l + 1 < num3) ? list[l + 1].Offset : num6);
			list[l].Length = num14 - list[l].Offset;
			Samples[list[l].Name] = list[l];
		}
	}

	public float[] Decode(Sample s, out int channels, out int rate)
	{
		channels = s.Channels;
		rate = s.Rate;
		byte[] array = VorbisSetup.For(s.Crc);
		if (array == null)
		{
			throw new Exception("no Vorbis setup header for crc " + s.Crc);
		}
		using VorbisReader vorbisReader = new VorbisReader(new MemoryStream(Rebuild(s, array)), closeStreamOnDispose: true);
		List<float> list = new List<float>();
		float[] array2 = new float[4096 * Math.Max(1, vorbisReader.Channels)];
		int num;
		while ((num = vorbisReader.ReadSamples(array2, 0, array2.Length)) > 0)
		{
			for (int i = 0; i < num; i++)
			{
				list.Add(array2[i]);
			}
		}
		channels = vorbisReader.Channels;
		rate = vorbisReader.SampleRate;
		return list.ToArray();
	}

	private byte[] Rebuild(Sample s, byte[] setup)
	{
		MemoryStream memoryStream = new MemoryStream();
		int seq = 0;
		MemoryStream memoryStream2 = new MemoryStream();
		memoryStream2.WriteByte(1);
		memoryStream2.Write(Encoding.ASCII.GetBytes("vorbis"), 0, 6);
		memoryStream2.Write(BitConverter.GetBytes(0), 0, 4);
		memoryStream2.WriteByte((byte)s.Channels);
		memoryStream2.Write(BitConverter.GetBytes(s.Rate), 0, 4);
		memoryStream2.Write(BitConverter.GetBytes(0), 0, 4);
		memoryStream2.Write(BitConverter.GetBytes(0), 0, 4);
		memoryStream2.Write(BitConverter.GetBytes(0), 0, 4);
		memoryStream2.WriteByte(184);
		memoryStream2.WriteByte(1);
		Page(memoryStream, memoryStream2.ToArray(), 0L, 2, ref seq);
		MemoryStream memoryStream3 = new MemoryStream();
		memoryStream3.WriteByte(3);
		memoryStream3.Write(Encoding.ASCII.GetBytes("vorbis"), 0, 6);
		byte[] bytes = Encoding.ASCII.GetBytes("Silkeste");
		memoryStream3.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
		memoryStream3.Write(bytes, 0, bytes.Length);
		memoryStream3.Write(BitConverter.GetBytes(0), 0, 4);
		memoryStream3.WriteByte(1);
		Page(memoryStream, memoryStream3.ToArray(), 0L, 0, ref seq);
		Page(memoryStream, setup, 0L, 0, ref seq);
		byte[] array = new byte[s.Length];
		using (FileStream fileStream = File.OpenRead(path))
		{
			fileStream.Seek(dataStart + s.Offset, SeekOrigin.Begin);
			ReadFully(fileStream, array, array.Length);
		}
		int num = 0;
		int num2 = array.Length;
		long num3 = 0L;
		List<byte[]> list = new List<byte[]>();
		while (num + 2 <= num2)
		{
			int num4 = BitConverter.ToUInt16(array, num);
			num += 2;
			if (num4 == 0 || num + num4 > num2)
			{
				break;
			}
			byte[] array2 = new byte[num4];
			Buffer.BlockCopy(array, num, array2, 0, num4);
			num += num4;
			list.Add(array2);
		}
		for (int i = 0; i < list.Count; i++)
		{
			num3 += 1024;
			Page(memoryStream, list[i], num3, (byte)((i == list.Count - 1) ? 4u : 0u), ref seq);
		}
		return memoryStream.ToArray();
	}

	private static void Page(Stream o, byte[] packet, long granule, byte type, ref int seq)
	{
		int num = packet.Length / 255 + 1;
		if (num > 255)
		{
			throw new Exception("Vorbis packet too large for one Ogg page");
		}
		byte[] array = new byte[27 + num];
		Encoding.ASCII.GetBytes("OggS").CopyTo(array, 0);
		array[4] = 0;
		array[5] = type;
		BitConverter.GetBytes(granule).CopyTo(array, 6);
		BitConverter.GetBytes(1397312587).CopyTo(array, 14);
		BitConverter.GetBytes(seq++).CopyTo(array, 18);
		array[26] = (byte)num;
		int num2 = packet.Length;
		for (int i = 0; i < num; i++)
		{
			int num3 = Math.Min(255, num2);
			array[27 + i] = (byte)num3;
			num2 -= num3;
		}
		BitConverter.GetBytes(Crc(Crc(0u, array, 0, array.Length), packet, 0, packet.Length)).CopyTo(array, 22);
		o.Write(array, 0, array.Length);
		o.Write(packet, 0, packet.Length);
	}

	private static uint Crc(uint crc, byte[] b, int off, int len)
	{
		if (crcTable == null)
		{
			crcTable = new uint[256];
			for (uint num = 0u; num < 256; num++)
			{
				uint num2 = num << 24;
				for (int i = 0; i < 8; i++)
				{
					num2 = (((num2 & 0x80000000u) != 0) ? ((num2 << 1) ^ 0x4C11DB7) : (num2 << 1));
				}
				crcTable[num] = num2;
			}
		}
		for (int j = off; j < off + len; j++)
		{
			crc = (crc << 8) ^ crcTable[((crc >> 24) ^ b[j]) & 0xFF];
		}
		return crc;
	}

	private static void ReadFully(Stream s, byte[] buf, int count)
	{
		int num;
		for (int i = 0; i < count; i += num)
		{
			if ((num = s.Read(buf, i, count - i)) <= 0)
			{
				break;
			}
		}
	}

	public Sample Find(string name)
	{
		if (Samples.TryGetValue(name, out var value) || Samples.TryGetValue(name + "_01", out value) || Samples.TryGetValue(name + "_1", out value))
		{
			return value;
		}
		foreach (KeyValuePair<string, Sample> sample in Samples)
		{
			if (sample.Key.StartsWith(name + "_", StringComparison.Ordinal))
			{
				return sample.Value;
			}
		}
		return null;
	}

	private static int IndexOf(byte[] hay, byte[] needle)
	{
		for (int i = 0; i + needle.Length <= hay.Length; i++)
		{
			int j;
			for (j = 0; j < needle.Length && hay[i + j] == needle[j]; j++)
			{
			}
			if (j == needle.Length)
			{
				return i;
			}
		}
		return -1;
	}
}
