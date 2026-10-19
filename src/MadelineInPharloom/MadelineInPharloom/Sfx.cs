using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

public static class Sfx
{
	private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

	private static readonly Dictionary<string, float[]> pending = new Dictionary<string, float[]>();

	private static readonly Dictionary<string, int[]> pendingFormat = new Dictionary<string, int[]>();

	private static AudioSource source;

	private static volatile bool decoded;

	public static string Status = "not started";

	private static int loggedCount = -1;

	private static AudioSource menuSource;

	private const int Brick = 8;

	private static AudioSource voiceSource;

	private static AudioSource loopSource;

	private static AudioSource idleSource;

	private static AudioClip lastVoice;

	public static int Surface
	{
		get
		{
			PlayerData instance = PlayerData.instance;
			if (instance == null)
			{
				return 8;
			}
			string text = instance.environmentType.ToString();
			SurfaceRow[] all = Surfaces.All;
			for (int i = 0; i < all.Length; i++)
			{
				SurfaceRow surfaceRow = all[i];
				if (surfaceRow.Environment == text)
				{
					return surfaceRow.CelesteSurface;
				}
			}
			return 8;
		}
	}

	public static void Start(string celesteDir, string cacheDir, string pluginDir)
	{
		Thread thread = new Thread((ThreadStart)delegate
		{
			try
			{
				string text = Path.Combine(cacheDir, "sfx");
				string text2 = Path.Combine(cacheDir, "sfx_banks");
				if (!Complete(text))
				{
					StartHelper(celesteDir, text, pluginDir);
				}
				bool flag = false;
				if (!flag && Complete(text))
				{
					Load(text);
					Plugin.Log.LogInfo("Sounds: Celeste's exact sounds");
				}
				else
				{
					if (flag || !Complete(text2))
					{
						Status = "reading Celeste's sounds";
						try
						{
							File.Delete(Path.Combine(text2, "ready.json"));
						}
						catch
						{
						}
						StringBuilder stringBuilder = new StringBuilder();
						Stopwatch stopwatch = Stopwatch.StartNew();
						int num = CelesteSounds.Render(celesteDir, text2, stringBuilder);
						try
						{
							File.WriteAllText(Path.Combine(text2, "CelesteSounds.log"), stringBuilder.ToString());
						}
						catch
						{
						}
						if (num > 0)
						{
							Plugin.Log.LogWarning("Celeste's sounds: " + num + " could not be read (" + stringBuilder.ToString().Replace("\n", " | ").Trim() + ")");
						}
						else
						{
							Plugin.Log.LogInfo("Celeste's sounds read from its banks in " + stopwatch.ElapsedMilliseconds + " ms");
						}
					}
					Load(text2);
					Status = "ready";
					decoded = true;
					if (!flag)
					{
						TryExact(celesteDir, cacheDir, text, pluginDir);
					}
				}
				Status = "ready";
				decoded = true;
			}
			catch (ThreadAbortException)
			{
				Status = "stopped";
				Thread.ResetAbort();
			}
			catch (Exception ex2)
			{
				// The game closing stops this thread mid-wait; that's not a failure.
				if (ex2 is ThreadAbortException || ex2.InnerException is ThreadAbortException || ex2.Message.IndexOf("aborted", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					Status = "stopped";
					return;
				}
				Status = "failed: " + ex2.Message;
				Plugin.Log.LogError("Sounds: " + ex2.Message);
			}
		});
		thread.IsBackground = true;
		thread.Name = "MadelineSfx";
		thread.Start();
	}

	// CelesteSfx.exe renders Celeste's exact sounds once, through the player's own FMOD. The mod
	// starts it itself, only while those sounds aren't ready yet; if it's missing or blocked (an
	// antivirus can quarantine it), Madeline keeps the raw sounds read from Celeste's banks.
	private static void StartHelper(string celesteDir, string outDir, string pluginDir)
	{
		try
		{
			string text = Path.Combine(pluginDir ?? "", "CelesteSfx.exe");
			if (!File.Exists(text))
			{
				Plugin.Log.LogInfo("Sounds: CelesteSfx.exe isn't here; Celeste's raw recordings are used");
				return;
			}
			if (Process.GetProcessesByName("CelesteSfx").Length != 0)
			{
				return;
			}
			Process.Start(new ProcessStartInfo
			{
				FileName = text,
				Arguments = "--celeste \"" + celesteDir.TrimEnd('\\', '/') + "\" --out \"" + outDir.TrimEnd('\\', '/') + "\"",
				WorkingDirectory = pluginDir,
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			});
			Plugin.Log.LogInfo("Sounds: rendering Celeste's exact sounds in the background");
		}
		catch (Exception ex)
		{
			Plugin.Log.LogInfo("Sounds: CelesteSfx.exe couldn't start (" + ex.Message + "); Celeste's raw recordings are used");
		}
	}

	private static bool Complete(string dir)
	{
		if (!File.Exists(Path.Combine(dir, "ready.json")))
		{
			return false;
		}
		SoundRow[] all = Sounds.All;
		for (int i = 0; i < all.Length; i++)
		{
			SoundRow soundRow = all[i];
			if (!File.Exists(Path.Combine(dir, soundRow.Id + ".wav")))
			{
				return false;
			}
		}
		return true;
	}

	private static void Load(string dir)
	{
		SoundRow[] all = Sounds.All;
		for (int i = 0; i < all.Length; i++)
		{
			SoundRow soundRow = all[i];
			string path = Path.Combine(dir, soundRow.Id + ".wav");
			if (!File.Exists(path))
			{
				Plugin.Log.LogWarning("Sound missing, skipped: " + soundRow.Id);
				continue;
			}
			int channels;
			int rate;
			float[] value = ReadPcm16(path, out channels, out rate);
			lock (pending)
			{
				pending[soundRow.Id] = value;
				pendingFormat[soundRow.Id] = new int[2] { channels, rate };
			}
		}
	}

	private static void TryExact(string celesteDir, string cacheDir, string exactDir, string pluginDir)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.Elapsed.TotalSeconds < 300.0)
		{
			if (Complete(exactDir))
			{
				Load(exactDir);
				Plugin.Log.LogInfo("Sounds: Celeste's exact sounds are ready and replace the raw ones");
				return;
			}
			Thread.Sleep(3000);
		}
		Plugin.Log.LogInfo("Sounds: the exact sounds didn't appear; Celeste's raw recordings stay");
	}

	public static void Pump()
	{
		if (!decoded || pending.Count == 0)
		{
			return;
		}
		lock (pending)
		{
			foreach (KeyValuePair<string, float[]> item in pending)
			{
				int[] array = pendingFormat[item.Key];
				float[] samples = item.Value;
				int read = 0;
				AudioClip value = AudioClip.Create("madeline_" + item.Key, samples.Length / array[0], array[0], array[1], stream: false, delegate(float[] data)
				{
					int num = Math.Min(data.Length, samples.Length - read);
					Array.Copy(samples, read, data, 0, num);
					for (int i = num; i < data.Length; i++)
					{
						data[i] = 0f;
					}
					read += num;
				});
				clips[item.Key] = value;
			}
			pending.Clear();
			if (clips.Count != loggedCount)
			{
				loggedCount = clips.Count;
				Plugin.Log.LogInfo("Sounds: " + clips.Count + " Celeste sounds loaded");
			}
		}
	}

	public static void Play(string id, float volume = 1f)
	{
		if (!clips.TryGetValue(id, out var value))
		{
			return;
		}
		EnsureSource();
		// menus (title, pause, Settings, Records) play on their own source: the gameplay one goes through Silksong's
		// gameplay mixer, which the game turns down while paused and in menus (user 2026-10-18: no menu sounds)
		GameManager gm = GameManager.SilentInstance;
		bool inMenu = gm == null || gm.IsMenuScene() || gm.isPaused || AssistMode.IsOpen || BossCard.RecordsOpen;
		if (source != null && !inMenu)
		{
			source.PlayOneShot(value, volume);
			return;
		}
		if (menuSource == null)
		{
			GameObject gameObject = new GameObject("SilkesteMenuSfx");
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			menuSource = gameObject.AddComponent<AudioSource>();
			menuSource.playOnAwake = false;
			menuSource.spatialBlend = 0f;
		}
		menuSource.PlayOneShot(value, volume * 0.8f);
	}

	public static string PlayVoice(string prefix)
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, AudioClip> clip in clips)
		{
			if (clip.Key.StartsWith(prefix + "_") && clip.Value != lastVoice)
			{
				list.Add(clip.Key);
			}
		}
		if (list.Count == 0)
		{
			foreach (KeyValuePair<string, AudioClip> clip2 in clips)
			{
				if (clip2.Key.StartsWith(prefix + "_"))
				{
					list.Add(clip2.Key);
				}
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		EnsureSource();
		if (voiceSource == null)
		{
			return null;
		}
		string text = list[UnityEngine.Random.Range(0, list.Count)];
		lastVoice = clips[text];
		voiceSource.Stop();
		voiceSource.clip = lastVoice;
		voiceSource.Play();
		return text;
	}

	public static void StopVoice()
	{
		if (voiceSource != null)
		{
			voiceSource.Stop();
		}
	}

	public static void PlaySurface(string sound, float volume = 1f)
	{
		string text = SurfaceId(sound, Surface);
		if (text != null)
		{
			Play(text, volume);
		}
	}

	public static string SurfaceId(string sound, int surface)
	{
		List<string> list = new List<string>();
		int[] array = new int[2] { surface, 8 };
		foreach (int num in array)
		{
			string text = sound + "_" + num;
			foreach (string key in clips.Keys)
			{
				if (key == text || key.StartsWith(text + "_"))
				{
					list.Add(key);
				}
			}
			if (list.Count > 0)
			{
				break;
			}
		}
		if (list.Count != 0)
		{
			return list[UnityEngine.Random.Range(0, list.Count)];
		}
		return null;
	}

	public static void Loop(string id)
	{
		if (id == null || !clips.TryGetValue(id, out var value))
		{
			if (loopSource != null && loopSource.isPlaying)
			{
				loopSource.Stop();
			}
			return;
		}
		EnsureSource();
		if (!(loopSource == null) && (!loopSource.isPlaying || !(loopSource.clip == value)))
		{
			loopSource.clip = value;
			loopSource.loop = true;
			loopSource.Play();
		}
	}

	public static void PlayIdle(string id)
	{
		if (clips.TryGetValue(id, out var value))
		{
			EnsureSource();
			if (!(idleSource == null))
			{
				idleSource.Stop();
				idleSource.clip = value;
				idleSource.Play();
			}
		}
	}

	public static void StopIdle()
	{
		if (idleSource != null && idleSource.isPlaying)
		{
			idleSource.Stop();
		}
	}

	private static void EnsureSource()
	{
		if (source != null)
		{
			return;
		}
		HeroController instance = HeroController.UnsafeInstance;
		if (instance == null)
		{
			return;
		}
		GameObject gameObject = new GameObject("MadelineSfx");
		UnityEngine.Object.DontDestroyOnLoad(gameObject);
		AudioSource audioSource = instance.GetComponent<AudioSource>() ?? instance.GetComponentInChildren<AudioSource>();
		source = gameObject.AddComponent<AudioSource>();
		voiceSource = gameObject.AddComponent<AudioSource>();
		loopSource = gameObject.AddComponent<AudioSource>();
		idleSource = gameObject.AddComponent<AudioSource>();
		AudioSource[] array = new AudioSource[4] { source, voiceSource, loopSource, idleSource };
		foreach (AudioSource audioSource2 in array)
		{
			audioSource2.playOnAwake = false;
			audioSource2.spatialBlend = 0f;
			if (audioSource != null)
			{
				audioSource2.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
			}
		}
	}

	private static float[] ReadPcm16(string path, out int channels, out int rate)
	{
		byte[] array = File.ReadAllBytes(path);
		channels = BitConverter.ToInt16(array, 22);
		rate = BitConverter.ToInt32(array, 24);
		if (Encoding.ASCII.GetString(array, 36, 4) != "data")
		{
			throw new Exception(path + " is not a CelesteSfx WAV");
		}
		float[] array2 = new float[Math.Min(BitConverter.ToInt32(array, 40), array.Length - 44) / 2];
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i] = (float)BitConverter.ToInt16(array, 44 + i * 2) / 32768f;
		}
		return array2;
	}
}
