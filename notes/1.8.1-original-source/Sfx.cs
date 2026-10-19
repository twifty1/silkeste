using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Madeline's sounds, rendered once from the player's Celeste copy by CelesteSfx.exe into the cache folder,
    /// then played through the hero's own audio mixer group so Silksong's volume settings apply.
    /// </summary>
    public static class Sfx
    {
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, float[]> pending = new Dictionary<string, float[]>();
        static readonly Dictionary<string, int[]> pendingFormat = new Dictionary<string, int[]>();
        static AudioSource source;
        static volatile bool decoded;
        public static string Status = "not started";
        static int loggedCount = -1;

        /// <summary>Renders (first run only) and decodes the WAVs on a worker thread.</summary>
        public static void Start(string celesteDir, string cacheDir, string pluginDir)
        {
            new Thread(() =>
            {
                try
                {
                    // Two sources (user 2026-10-13: "both"):
                    //   sfx/       the exact sounds, Celeste's own events rendered by CelesteSfx.exe through its FMOD
                    //   sfx_banks/ Celeste's raw recordings, read here in the game (CelesteBank / CelesteSounds)
                    // The exact ones are used when they are there. Otherwise the raw ones play right away, and the helper
                    // gets one low-priority try per version in the background; if Windows blocks it, nothing is lost.
                    string exactDir = Path.Combine(cacheDir, "sfx"), banksDir = Path.Combine(cacheDir, "sfx_banks");
                    bool forceBanks = false;
#if TESTHARNESS
                    forceBanks = File.Exists(Path.Combine(pluginDir, "sfx_from_banks.flag"));   // test: the raw ones only
                    if (File.Exists(Path.Combine(pluginDir, "sfx_fresh.flag")))   // test: a new player's empty cache
                    {
                        cacheDir = Path.Combine(cacheDir, "fresh" + DateTime.Now.Ticks);
                        exactDir = Path.Combine(cacheDir, "sfx"); banksDir = Path.Combine(cacheDir, "sfx_banks");
                        Plugin.Log.LogInfo("[TEST] fresh sound cache " + cacheDir);
                    }
#endif
                    if (!forceBanks && Complete(exactDir)) { Load(exactDir); Plugin.Log.LogInfo("Sounds: Celeste's exact sounds"); }
                    else
                    {
                        if (forceBanks || !Complete(banksDir))
                        {
                            Status = "reading Celeste's sounds";
                            try { File.Delete(Path.Combine(banksDir, "ready.json")); } catch { }
                            var log = new System.Text.StringBuilder();
                            var watch = Stopwatch.StartNew();
                            int failed = CelesteSounds.Render(celesteDir, banksDir, log);
                            try { File.WriteAllText(Path.Combine(banksDir, "CelesteSounds.log"), log.ToString()); } catch { }
                            if (failed > 0) Plugin.Log.LogWarning("Celeste's sounds: " + failed + " could not be read (" + log.ToString().Replace("\n", " | ").Trim() + ")");
                            else Plugin.Log.LogInfo("Celeste's sounds read from its banks in " + watch.ElapsedMilliseconds + " ms");
                        }
                        Load(banksDir);
                        Status = "ready";
                        decoded = true;
                        if (!forceBanks) TryExact(celesteDir, cacheDir, exactDir, pluginDir);
                    }                    Status = "ready";
                    decoded = true;
                }
                catch (System.Threading.ThreadAbortException) { Status = "stopped"; System.Threading.Thread.ResetAbort(); }   // game quitting: not an error
                catch (Exception e) { Status = "failed: " + e.Message; Plugin.Log.LogError("Sounds: " + e.Message); }
            }) { IsBackground = true, Name = "MadelineSfx" }.Start();
        }

        static bool Complete(string dir)
        {
            if (!File.Exists(Path.Combine(dir, "ready.json"))) return false;
            foreach (SoundRow row in Sounds.All) if (!File.Exists(Path.Combine(dir, row.Id + ".wav"))) return false;
            return true;
        }

        // the WAVs of one folder into the clips (Pump makes them AudioClips on the main thread, replacing older ones)
        static void Load(string dir)
        {
            foreach (SoundRow row in Sounds.All)
            {
                int channels, rate;
                string wav = Path.Combine(dir, row.Id + ".wav");
                if (!File.Exists(wav)) { Plugin.Log.LogWarning("Sound missing, skipped: " + row.Id); continue; }
                float[] data = ReadPcm16(wav, out channels, out rate);
                lock (pending) { pending[row.Id] = data; pendingFormat[row.Id] = new[] { channels, rate }; }
            }
        }

        // One try per version at the exact sounds (sfx_attempt.txt is written first, so a helper that takes the game
        // down with it on some PC is not tried again until the next version). On success they replace the raw ones now.
        static void TryExact(string celesteDir, string cacheDir, string exactDir, string pluginDir)
        {
            // 1.7.1: the game no longer starts CelesteSfx.exe itself. Players saw the game close ~30 s in on the first
            // start of a new version, when it launched the helper; Windows security that blocks an unsigned helper can
            // take the program that started it down too. Melty now starts the helper beside the game (recipe
            // "together"), so here the game only waits for its sounds and swaps them in when they appear.
            var waited = Stopwatch.StartNew();
            while (waited.Elapsed.TotalSeconds < 300)
            {
                if (Complete(exactDir)) { Load(exactDir); Plugin.Log.LogInfo("Sounds: Celeste's exact sounds are ready and replace the raw ones"); return; }
                Thread.Sleep(3000);
            }
            Plugin.Log.LogInfo("Sounds: the exact sounds didn't appear; Celeste's raw recordings stay");
        }

        /// <summary>Main thread: turns decoded samples into AudioClips once they are ready.</summary>
        public static void Pump()
        {
            if (!decoded || pending.Count == 0) return;
            lock (pending)
            {
                foreach (var kv in pending)
                {
                    int[] f = pendingFormat[kv.Key];
                    // Filled through the reader callback: Unity 6's SetData overloads drag in Span types this build can't see.
                    float[] samples = kv.Value;
                    int read = 0;
                    var clip = AudioClip.Create("madeline_" + kv.Key, samples.Length / f[0], f[0], f[1], false, data =>
                    {
                        int n = Math.Min(data.Length, samples.Length - read);
                        Array.Copy(samples, read, data, 0, n);
                        for (int i = n; i < data.Length; i++) data[i] = 0f;
                        read += n;
                    });
                    clips[kv.Key] = clip;
                }
                pending.Clear();
                if (clips.Count != loggedCount) { loggedCount = clips.Count; Plugin.Log.LogInfo("Sounds: " + clips.Count + " Celeste sounds loaded"); }   // once, not per batch
            }
        }

        public static void Play(string id, float volume = 1f)
        {
            AudioClip clip;
            if (!clips.TryGetValue(id, out clip)) return;
            EnsureSource();
            if (source != null) { source.PlayOneShot(clip, volume); return; }
            // no Madeline yet (the main menu, the save files): a plain source of its own for menu sounds (user 2026-10-14)
            if (menuSource == null)
            {
                var go = new GameObject("SilkesteMenuSfx");
                UnityEngine.Object.DontDestroyOnLoad(go);
                menuSource = go.AddComponent<AudioSource>();
                menuSource.playOnAwake = false; menuSource.spatialBlend = 0f;
            }
            menuSource.PlayOneShot(clip, volume * 0.8f);
        }
        static AudioSource menuSource;

        /// <summary>One of the skin's talk babbles (sounds named prefix_*), never the same one twice in a row.
        /// Returns the sound id it played, or null.</summary>
        public static string PlayVoice(string prefix)
        {
            var choices = new List<string>();
            foreach (var kv in clips) if (kv.Key.StartsWith(prefix + "_") && kv.Value != lastVoice) choices.Add(kv.Key);
            if (choices.Count == 0) foreach (var kv in clips) if (kv.Key.StartsWith(prefix + "_")) choices.Add(kv.Key);
            if (choices.Count == 0) return null;
            EnsureSource();
            if (voiceSource == null) return null;
            string id = choices[UnityEngine.Random.Range(0, choices.Count)];
            lastVoice = clips[id];
            voiceSource.Stop();
            voiceSource.clip = lastVoice;
            voiceSource.Play();
            return id;
        }

        public static void StopVoice()
        {
            if (voiceSource != null) voiceSource.Stop();
        }

        const int Brick = 8;   // every surface sound has a brick take (preflight checks it)

        /// <summary>Celeste surface for the area Madeline is in (hooks.floor_surface, sheets/surfaces.json).</summary>
        public static int Surface
        {
            get
            {
                PlayerData pd = PlayerData.instance;
                if (pd == null) return Brick;
                string env = pd.environmentType.ToString();
                foreach (SurfaceRow r in Surfaces.All) if (r.Environment == env) return r.CelesteSurface;
                return Brick;
            }
        }

        /// <summary>A surface sound (footstep, landing, grab, handhold): one of sound_surface[_take], else the brick one.</summary>
        public static void PlaySurface(string sound, float volume = 1f)
        {
            string id = SurfaceId(sound, Surface);
            if (id != null) Play(id, volume);
        }

        public static string SurfaceId(string sound, int surface)
        {
            var takes = new List<string>();
            foreach (int s in new[] { surface, Brick })
            {
                string exact = sound + "_" + s;
                foreach (string key in clips.Keys) if (key == exact || key.StartsWith(exact + "_")) takes.Add(key);
                if (takes.Count > 0) break;
            }
            return takes.Count == 0 ? null : takes[UnityEngine.Random.Range(0, takes.Count)];
        }

        /// <summary>The one looping sound she can make (the wall slide for its surface, or the glider), or null
        /// for none. Called once a frame; keeps playing while the id stays the same.</summary>
        public static void Loop(string id)
        {
            AudioClip clip;
            if (id == null || !clips.TryGetValue(id, out clip)) { if (loopSource != null && loopSource.isPlaying) loopSource.Stop(); return; }
            EnsureSource();
            if (loopSource == null) return;
            if (loopSource.isPlaying && loopSource.clip == clip) return;
            loopSource.clip = clip;
            loopSource.loop = true;
            loopSource.Play();
        }

        /// <summary>An idle-move sound (Celeste keeps it as idleSfx so it can be cut off when she moves).</summary>
        public static void PlayIdle(string id)
        {
            AudioClip clip;
            if (!clips.TryGetValue(id, out clip)) return;
            EnsureSource();
            if (idleSource == null) return;
            idleSource.Stop();
            idleSource.clip = clip;
            idleSource.Play();
        }

        public static void StopIdle()
        {
            if (idleSource != null && idleSource.isPlaying) idleSource.Stop();
        }

        static AudioSource voiceSource, loopSource, idleSource;
        static AudioClip lastVoice;

        static void EnsureSource()
        {
            if (source != null) return;
            HeroController hero = HeroController.instance;
            if (hero == null) return;
            var go = new GameObject("MadelineSfx");
            UnityEngine.Object.DontDestroyOnLoad(go);
            AudioSource heroSource = hero.GetComponent<AudioSource>() ?? hero.GetComponentInChildren<AudioSource>();
            source = go.AddComponent<AudioSource>();
            voiceSource = go.AddComponent<AudioSource>();   // its own source so the next line can cut it off
            loopSource = go.AddComponent<AudioSource>();    // wall slide or glider
            idleSource = go.AddComponent<AudioSource>();    // idle moves
            foreach (AudioSource s in new[] { source, voiceSource, loopSource, idleSource })
            {
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                if (heroSource != null) s.outputAudioMixerGroup = heroSource.outputAudioMixerGroup;
            }
        }

        static float[] ReadPcm16(string path, out int channels, out int rate)
        {
            byte[] b = File.ReadAllBytes(path);
            channels = BitConverter.ToInt16(b, 22);
            rate = BitConverter.ToInt32(b, 24);
            if (Encoding.ASCII.GetString(b, 36, 4) != "data") throw new Exception(path + " is not a CelesteSfx WAV");
            int len = Math.Min(BitConverter.ToInt32(b, 40), b.Length - 44);
            var s = new float[len / 2];
            for (int i = 0; i < s.Length; i++) s[i] = BitConverter.ToInt16(b, 44 + i * 2) / 32768f;
            return s;
        }
    }
}
