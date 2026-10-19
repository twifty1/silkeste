// CelesteSfx: renders Madeline's sound events from the player's own Celeste copy to WAV files.
// It loads Celeste's fmod/fmodstudio DLLs (FMOD Studio 1.10, 32-bit in every Celeste build) from the
// Celeste folder and plays each event through FMOD's non-realtime WAV writer. Nothing from Celeste ships.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using MadelineInPharloom.Sheets;

static class Program
{
    const int OUTPUT_WAVWRITER_NRT = 5;
    const int STUDIO_INIT_SYNCHRONOUS_UPDATE = 0x2;
    const int INIT_STREAM_FROM_UPDATE = 0x1, INIT_MIX_FROM_UPDATE = 0x2;
    const int PLAYBACK_STOPPED = 2;
    const int LOADING_LOADED = 3, LOADING_ERROR = 4;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Create(out IntPtr studio, uint headerVersion);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetLowLevel(IntPtr studio, out IntPtr system);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetOutput(IntPtr system, int output);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Initialize(IntPtr studio, int maxChannels, int studioFlags, int flags, IntPtr extra);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int LoadBank(IntPtr studio, byte[] file, int flags, out IntPtr bank);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetEvent(IntPtr studio, byte[] path, out IntPtr desc);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Handle(IntPtr h);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateInstance(IntPtr desc, out IntPtr inst);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetState(IntPtr h, out int state);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetParam(IntPtr inst, byte[] name, float value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int StopFn(IntPtr inst, int mode);
    const int STOP_IMMEDIATE = 1;
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetBufferSize(IntPtr system, out uint length, out int numBuffers);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetFormat(IntPtr system, out int rate, out int speakerMode, out int rawSpeakers);

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)] static extern IntPtr LoadLibraryW(string path);
    [DllImport("kernel32", CharSet = CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool SetDllDirectoryW(string path);

    static IntPtr studioDll;
    static T Fn<T>(string name) where T : class
    {
        IntPtr p = GetProcAddress(studioDll, name);
        if (p == IntPtr.Zero) throw new Exception("fmodstudio has no " + name);
        return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
    }
    static byte[] Z(string s) { return Encoding.UTF8.GetBytes(s + "\0"); }
    static void Check(int r, string what) { if (r != 0) throw new Exception(what + " failed (FMOD result " + r + ")"); }

    static int Main(string[] args)
    {
        string celeste = null, outDir = null;
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "--celeste") celeste = args[++i];
            else if (args[i] == "--out") outDir = args[++i];
        }
        if (celeste == null || outDir == null) { Console.Error.WriteLine("usage: CelesteSfx --celeste <Celeste folder> --out <folder>"); return 2; }
        Directory.CreateDirectory(outDir);
        // Melty starts this beside the game on every Play (1.7.1): sounds already made means nothing to do
        if (File.Exists(Path.Combine(outDir, "ready.json")))
        {
            bool all = true;
            foreach (var s in Sounds.All) if (!File.Exists(Path.Combine(outDir, s.Id + ".wav"))) { all = false; break; }
            if (all) { Console.WriteLine("already made"); return 0; }
        }
        var log = new StringBuilder();
        Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var report = new StringBuilder("{");
        int ok = 0;
        try
        {
            string libDir = Environment.Is64BitProcess ? Path.Combine(celeste, "lib64-win-x64") : celeste;
            SetDllDirectoryW(libDir);
            coreDll = LoadLibraryW(Path.Combine(libDir, Environment.Is64BitProcess ? "fmod64.dll" : "fmod.dll"));
            studioDll = LoadLibraryW(Path.Combine(libDir, "fmodstudio.dll"));
            if (coreDll == IntPtr.Zero || studioDll == IntPtr.Zero) throw new Exception("could not load Celeste's FMOD from " + libDir + " (error " + Marshal.GetLastWin32Error() + ")");
            string banks = Path.Combine(celeste, "Content", "FMOD", "Desktop");
            foreach (SoundRow row in Sounds.All)
            {
                string wav = Path.Combine(outDir, row.Id + ".wav");
                string err = null;
                // Some Celeste events are randomised and now and then render nothing (talk babble, some footsteps): render again.
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    err = Render(banks, row, wav + ".raw.wav");
                    if (err == null) err = TrimToPcm16(wav + ".raw.wav", wav);
                    if (err != "rendered silence") break;
                }
                try { File.Delete(wav + ".raw.wav"); } catch { }
                say(row.Id + " (" + row.Event + "): " + (err ?? "ok"));
                if (report.Length > 1) report.Append(",");
                report.Append("\"" + row.Id + "\":\"" + (err == null ? "ok" : err.Replace("\"", "'")) + "\"");
                if (err == null) ok++;
            }
        }
        catch (Exception e) { say("FAILED: " + e.Message); }
        report.Append("}");
        File.WriteAllText(Path.Combine(outDir, "report.json"), report.ToString());
        File.WriteAllText(Path.Combine(outDir, "CelesteSfx.log"), log.ToString());
        if (ok == Sounds.All.Length) File.WriteAllText(Path.Combine(outDir, "ready.json"), "{\"sounds\":" + ok + "}");
        return ok == Sounds.All.Length ? 0 : 1;
    }

    static string Render(string banks, SoundRow row, string wavPath)
    {
        IntPtr studio, system, desc, inst, bank;
        Check(Fn<Create>("FMOD_Studio_System_Create")(out studio, 0x00011014), "System_Create");
        IntPtr name = Marshal.StringToHGlobalAnsi(wavPath);
        try
        {
            Check(Fn<GetLowLevel>("FMOD_Studio_System_GetLowLevelSystem")(studio, out system), "GetLowLevelSystem");
            SetOutput setOutput = Marshal.GetDelegateForFunctionPointer(CoreProc("FMOD_System_SetOutput"), typeof(SetOutput)) as SetOutput;
            Check(setOutput(system, OUTPUT_WAVWRITER_NRT), "SetOutput");
            Check(Fn<Initialize>("FMOD_Studio_System_Initialize")(studio, 64, STUDIO_INIT_SYNCHRONOUS_UPDATE, INIT_STREAM_FROM_UPDATE | INIT_MIX_FROM_UPDATE, name), "Initialize");
            var load = Fn<LoadBank>("FMOD_Studio_System_LoadBankFile");
            foreach (string b in new[] { "Master Bank.bank", "Master Bank.strings.bank", "sfx.bank", "ui.bank", "dlc_sfx.bank" })
                Check(load(studio, Z(Path.Combine(banks, b)), 0, out bank), "LoadBank " + b);
            if (Fn<GetEvent>("FMOD_Studio_System_GetEvent")(studio, Z(row.Event), out desc) != 0) return "event not found";
            var update = Fn<Handle>("FMOD_Studio_System_Update");
            Fn<Handle>("FMOD_Studio_EventDescription_LoadSampleData")(desc);
            var loading = Fn<GetState>("FMOD_Studio_EventDescription_GetSampleLoadingState");
            for (int i = 0; i < 500; i++)
            {
                int st; loading(desc, out st);
                if (st == LOADING_LOADED) break;
                if (st == LOADING_ERROR) return "sample data failed to load";
                update(studio);
            }
            Check(Fn<CreateInstance>("FMOD_Studio_EventDescription_CreateInstance")(desc, out inst), "CreateInstance");
            var setParam = Fn<SetParam>("FMOD_Studio_EventInstance_SetParameterValue");
            if (row.SurfaceIndex >= 0) setParam(inst, Z("surface_index"), row.SurfaceIndex);
            SetParams(setParam, inst, row.Params);
            Check(Fn<Handle>("FMOD_Studio_EventInstance_Start")(inst), "Start");
            var playback = Fn<GetState>("FMOD_Studio_EventInstance_GetPlaybackState");
            // Looping events (talk babble, wall slide) play hold_seconds, then end: "stop" cuts the loop,
            // name=value sets parameters, as Celeste's Textbox.StopTalker does for the babble.
            int holdBlocks = -1;
            if (row.HoldSeconds > 0)
            {
                uint blockLen; int numBuffers, rate, mode, raw;
                var bufSize = Marshal.GetDelegateForFunctionPointer(CoreProc("FMOD_System_GetDSPBufferSize"), typeof(GetBufferSize)) as GetBufferSize;
                var format = Marshal.GetDelegateForFunctionPointer(CoreProc("FMOD_System_GetSoftwareFormat"), typeof(GetFormat)) as GetFormat;
                Check(bufSize(system, out blockLen, out numBuffers), "GetDSPBufferSize");
                Check(format(system, out rate, out mode, out raw), "GetSoftwareFormat");
                holdBlocks = (int)Math.Ceiling(row.HoldSeconds * rate / blockLen);
            }
            int blocks = 0;
            for (; blocks < 1000; blocks++)  // ~20 s cap
            {
                if (blocks == holdBlocks)
                {
                    if (row.End == "stop") Fn<StopFn>("FMOD_Studio_EventInstance_Stop")(inst, STOP_IMMEDIATE);
                    else SetParams(setParam, inst, row.End);
                }
                update(studio);
                int st; playback(inst, out st);
                if (st == PLAYBACK_STOPPED) break;
            }
            for (int i = 0; i < 20; i++) update(studio);  // let reverb tails out
            Fn<Handle>("FMOD_Studio_EventInstance_Release")(inst);
            return null;
        }
        finally
        {
            Fn<Handle>("FMOD_Studio_System_Release")(studio);
            Marshal.FreeHGlobal(name);
        }
    }

    static void SetParams(SetParam setParam, IntPtr inst, string spec)
    {
        if (spec == "none") return;
        foreach (string kv in spec.Split(';'))
        {
            string[] p = kv.Split('=');
            Check(setParam(inst, Z(p[0]), float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)), "SetParameter " + p[0]);
        }
    }

    static IntPtr coreDll;
    static IntPtr CoreProc(string name)
    {
        IntPtr p = GetProcAddress(coreDll, name);
        if (p == IntPtr.Zero) throw new Exception("fmod has no " + name);
        return p;
    }

    // Reads FMOD's WAV (16-bit PCM or 32-bit float), trims silence at both ends, writes 16-bit PCM.
    static string TrimToPcm16(string inPath, string outPath)
    {
        byte[] b = File.ReadAllBytes(inPath);
        int pos = 12, fmt = 0, ch = 0, rate = 0, bits = 0, dataOff = -1, dataLen = 0;
        while (pos + 8 <= b.Length)
        {
            string id = Encoding.ASCII.GetString(b, pos, 4);
            int len = BitConverter.ToInt32(b, pos + 4);
            if (id == "fmt ") { fmt = BitConverter.ToInt16(b, pos + 8); ch = BitConverter.ToInt16(b, pos + 10); rate = BitConverter.ToInt32(b, pos + 12); bits = BitConverter.ToInt16(b, pos + 22); }
            if (id == "data") { dataOff = pos + 8; dataLen = Math.Min(len <= 0 ? b.Length - dataOff : len, b.Length - dataOff); break; }
            pos += 8 + len + (len & 1);
        }
        if (dataOff < 0 || ch == 0) return "no audio data written";
        int bytesPer = bits / 8, frames = dataLen / (bytesPer * ch);
        var samples = new float[frames * ch];
        for (int i = 0; i < samples.Length; i++)
        {
            int o = dataOff + i * bytesPer;
            samples[i] = (fmt == 3 || fmt == -2) && bits == 32 ? BitConverter.ToSingle(b, o) : bits == 16 ? BitConverter.ToInt16(b, o) / 32768f : 0f;
        }
        const float threshold = 0.0005f;
        int first = 0, last = frames - 1;
        while (first < frames && Peak(samples, first, ch) < threshold) first++;
        while (last > first && Peak(samples, last, ch) < threshold) last--;
        if (first >= frames) return "rendered silence";
        int n = last - first + 1;
        using (var w = new BinaryWriter(File.Create(outPath)))
        {
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * ch * 2); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)ch); w.Write(rate); w.Write(rate * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * ch * 2);
            for (int f = first; f <= last; f++)
                for (int c = 0; c < ch; c++)
                    w.Write((short)Math.Max(-32768, Math.Min(32767, (int)(samples[f * ch + c] * 32767f))));
        }
        return null;
    }

    static float Peak(float[] s, int frame, int ch)
    {
        float m = 0;
        for (int c = 0; c < ch; c++) m = Math.Max(m, Math.Abs(s[frame * ch + c]));
        return m;
    }
}
