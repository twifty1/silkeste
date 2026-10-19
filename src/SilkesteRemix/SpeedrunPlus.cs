using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using HarmonyLib;
using InControl;
using MadelineInPharloom;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace SilkesteRemix;

[BepInPlugin("silkeste.remix.speedrun", "Silkeste remix: speedrun", "1.0.0")]
[BepInDependency("melty.madelineinpharloom", BepInDependency.DependencyFlags.HardDependency)]
public class SpeedrunPlus : BaseUnityPlugin
{
	private struct Split
	{
		public string Name;

		public float Time;

		public float Delta;

		public bool HasDelta;

		public bool Gold;
	}

	private struct HudLine
	{
		public string L;

		public string R;

		public Color LC;

		public Color RC;

		public float Scale;
	}

	private const string Sec = "Speedrun Timer";

	public static readonly AssistRow Splits = new AssistRow
	{
		Id = "sr_splits",
		Section = "Speedrun Timer",
		Label = "Splits",
		Values = "Bosses + Areas,Bosses,Areas,Off",
		Default = "Bosses + Areas",
		Description = "Splits the run when you beat a boss or enter a new area, timed on this save's timer."
	};

	public static readonly AssistRow SplitList = new AssistRow
	{
		Id = "sr_split_list",
		Section = "Speedrun Timer",
		Label = "Split List",
		Values = "Last 3,Last 5,Last 8,Last 1,Hidden",
		Default = "Last 3",
		Description = "How many of your latest splits stay on screen under the timer."
	};

	public static readonly AssistRow Compare = new AssistRow
	{
		Id = "sr_compare",
		Section = "Speedrun Timer",
		Label = "Compare Against",
		Values = "Personal Best,Best Segments,Off",
		Default = "Personal Best",
		Description = "Green means ahead, red behind, gold a new best segment. Best Segments compares each split's own length."
	};

	public static readonly AssistRow Precision = new AssistRow
	{
		Id = "sr_precision",
		Section = "Speedrun Timer",
		Label = "Precision",
		Values = "Hundredths,Thousandths,Tenths",
		Default = "Hundredths",
		Description = "How many digits every timer shows, records too."
	};

	public static readonly AssistRow SumOfBest = new AssistRow
	{
		Id = "sr_sum_of_best",
		Section = "Speedrun Timer",
		Label = "Sum of Best",
		Values = "On,Off",
		Default = "On",
		Description = "Shows your best possible time from every best segment, and how much this run could still save."
	};

	public static readonly AssistRow LastRoom = new AssistRow
	{
		Id = "sr_last_room",
		Section = "Speedrun Timer",
		Label = "Last Room Time",
		Values = "On,Off",
		Default = "On",
		Description = "Shows how long the room you just left took, gold when it's your best for that room."
	};

	public static readonly AssistRow Speedo = new AssistRow
	{
		Id = "sr_speed",
		Section = "Speedrun Timer",
		Label = "Speedometer",
		Values = "Off,On",
		Default = "Off",
		Description = "Shows Madeline's speed right now and the top speed in this room."
	};

	public static readonly AssistRow Inputs = new AssistRow
	{
		Id = "sr_inputs",
		Section = "Speedrun Timer",
		Label = "Input Display",
		Values = "Off,On",
		Default = "Off",
		Description = "Shows the directions, jump, dash and attack as you press them, for recording runs."
	};

	public static readonly AssistRow KeyReset = new AssistRow
	{
		Id = "sr_key_reset",
		Section = "Controls",
		Label = "Reset Splits",
		Values = "F7,F8,F9,F10,F11,F12,F1,F2,F3,F4,F5,F6",
		Default = "F5",
		Description = "Clears this run's splits to start a fresh attempt. Personal bests stay. Two actions can't share a key."
	};

	public static readonly AssistRow Attempts = new AssistRow
	{
		Id = "sr_attempts",
		Section = "Speedrun Timer",
		Label = "Attempt Counter",
		Values = "On,Off",
		Default = "On",
		Description = "Counts this save's split resets, and your tries at the boss you're fighting."
	};

	public static readonly AssistRow Pace = new AssistRow
	{
		Id = "sr_pace",
		Section = "Speedrun Timer",
		Label = "Pace Colours",
		Values = "On,Off",
		Default = "On",
		Description = "Tints the big timer green when your last split was ahead, red when behind, gold on a best segment."
	};

	public static readonly AssistRow Position = new AssistRow
	{
		Id = "sr_pos",
		Section = "Speedrun Timer",
		Label = "Position",
		Values = "Top Left,Middle Left,Bottom Left",
		Default = "Top Left",
		Description = "Where the timer panel sits. At the top left, the rosary and shard counters move down below it."
	};

	private static readonly AssistRow[] TimerRows = new AssistRow[12]
	{
		DialogueSkip.Row,
		Position,
		Splits,
		SplitList,
		Compare,
		Precision,
		SumOfBest,
		LastRoom,
		Attempts,
		Pace,
		Speedo,
		Inputs
	};

	private static object lastFight;

	private static string fightTitle;

	private static readonly List<Split> run = new List<Split>();

	private static readonly Dictionary<string, float> records = new Dictionary<string, float>();

	private static readonly HashSet<string> areasSplit = new HashSet<string>();

	private static SpeedrunTimer timer;

	private static int runSlot = -1;

	private static string lastArea;

	private static string lastRoomName;

	private static float lastRoomTime = -1f;

	private static bool lastRoomGold;

	private static float prevRoomTime;

	private static string trackedRoom;

	private static float topSpeed;

	private static float resetShown;

	private static bool recordsLoaded;

	private static AssistRow[] allRows;

	private static readonly FieldInfo AllField = AccessTools.Field(typeof(AssistOptions), "All");

	private static readonly Color Ahead = new Color(0.35f, 0.9f, 0.45f);

	private static readonly Color Behind = new Color(1f, 0.38f, 0.35f);

	private static readonly Color Gold = new Color(1f, 0.82f, 0.25f);

	private static readonly Color Grey = new Color(0.8f, 0.8f, 0.8f);

	private static readonly Color Cream = new Color(0.97f, 0.94f, 0.86f);

	private static readonly Color Muted = new Color(0.78f, 0.75f, 0.68f);

	private static readonly Color Ink = new Color(0.04f, 0.05f, 0.08f);

	private static Texture2D fade;

	private static bool hudError;

	private static string RecordsFile => Path.Combine(AssistMode.DataDir ?? "", "remix_speedrun.cfg");

	private static string V(AssistRow r)
	{
		return AssistMode.Value(r);
	}

	private static string RunFile(int slot)
	{
		return Path.Combine(AssistMode.DataDir ?? "", "remix_run_" + slot + ".cfg");
	}

	private void Awake()
	{
		AddRows();
		base.gameObject.AddComponent<HudShift>();
		Harmony harmony = new Harmony("silkeste.remix.speedrun");
		PatchAllReaders(harmony);
		HairPreview.Patch(harmony);
		// MenuStyle.Patch(harmony);   // retired 2026-10-17: the main plugin has its own redesigned settings screen now
		RecordsDark.Patch(harmony);
		LoadingRunner.Patch(harmony);
		MenuHints.Patch(harmony);
		DialogueSkip.Patch(harmony);
		FileSelectFx.Patch(harmony);
		base.gameObject.AddComponent<LoadingRunner>();
		base.gameObject.AddComponent<SaveNotice>();
		harmony.Patch(AccessTools.Method(typeof(BossCard), "FormatTime"), new HarmonyMethod(typeof(SpeedrunPlus), "FormatPre"));
		harmony.Patch(AccessTools.Method(typeof(Achievements), "BossBeaten"), null, new HarmonyMethod(typeof(SpeedrunPlus), "BossPost"));
		harmony.Patch(AccessTools.Method(typeof(SpeedrunTimer), "OnGUI"), new HarmonyMethod(typeof(SpeedrunPlus), "Hud"));
		base.Logger.LogInfo("Silkeste remix speedrun: " + AllRows().Length + " settings rows");
		PogoRemix.ReportTest((Array.IndexOf(AllRows(), Splits) >= 0) ? "pass speedrun-settings" : "fail speedrun-settings: rows not added");
	}

	private static void AddRows()
	{
		List<AssistRow> list = new List<AssistRow>(AssistOptions.All);
		int num = list.FindIndex((AssistRow r) => r.Id == AssistOptions.AutoSkip.Id) + 1;
		if (num <= 0)
		{
			num = list.Count;
		}
		list.InsertRange(num, TimerRows);
		int num2 = list.FindIndex((AssistRow r) => r.Id == AssistOptions.KeyRecords.Id) + 1;
		list.Insert((num2 <= 0) ? list.Count : num2, KeyReset);
		// list.Add(MenuStyle.Style);   // retired with it
		list.Add(RecordsDark.Style);
		list.Add(LoadingRunner.Loading);
		list.Add(LoadingRunner.TitleNap);
		list.Add(SaveNotice.Style);
		list.Add(FileSelectFx.Portraits);
		allRows = list.ToArray();
		AllField.SetValue(null, allRows);   // one shared list, so the main plugin can add Silksong's own rows to it (2026-10-17)
		AssistMode.loaded = false;
	}

	public static AssistRow[] AllRows()
	{
		return (AssistRow[])AllField.GetValue(null);
	}

	private static IEnumerable<CodeInstruction> SwapAll(IEnumerable<CodeInstruction> code)
	{
		foreach (CodeInstruction item in code)
		{
			if (item.opcode == OpCodes.Ldsfld && object.Equals(item.operand, AllField))
			{
				yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SpeedrunPlus), "AllRows")).MoveLabelsFrom(item).MoveBlocksFrom(item);
			}
			else
			{
				yield return item;
			}
		}
	}

	private static void PatchAllReaders(Harmony h)
	{
		HarmonyMethod transpiler = new HarmonyMethod(typeof(SpeedrunPlus), "SwapAll");
		int num = 0;
		Type[] types = typeof(AssistMode).Assembly.GetTypes();
		foreach (Type type in types)
		{
			Type type2 = type;
			while (type2.DeclaringType != null)
			{
				type2 = type2.DeclaringType;
			}
			if (type2 != typeof(AssistMode) && type2 != typeof(PostcardView))
			{
				continue;
			}
			MethodInfo[] methods = type.GetMethods(AccessTools.allDeclared);
			foreach (MethodInfo methodInfo in methods)
			{
				if (methodInfo.IsAbstract || methodInfo.ContainsGenericParameters)
				{
					continue;
				}
				try
				{
					bool flag = false;
					foreach (CodeInstruction originalInstruction in PatchProcessor.GetOriginalInstructions(methodInfo))
					{
						if (originalInstruction.opcode == OpCodes.Ldsfld && object.Equals(originalInstruction.operand, AllField))
						{
							flag = true;
							break;
						}
					}
					if (flag)
					{
						h.Patch(methodInfo, null, null, transpiler);
						num++;
					}
				}
				catch (Exception)
				{
				}
			}
		}
		Plugin.Log.LogInfo("Silkeste remix speedrun: settings list swapped in " + num + " places");
	}

	private static bool FormatPre(float seconds, ref string __result)
	{
		string text;
		try
		{
			text = V(Precision);
		}
		catch (Exception)
		{
			return true;
		}
		if (text == "Hundredths")
		{
			return true;
		}
		int num = ((!(text == "Thousandths")) ? 1 : 3);
		int num2 = ((num == 3) ? 1000 : 10);
		long num3 = (long)(Math.Max(0f, seconds) * (float)num2);
		long num4 = num3 / num2;
		long num5 = num3 % num2;
		long num6 = num4 / 3600;
		long num7 = num4 / 60 % 60;
		long num8 = num4 % 60;
		string text2 = num5.ToString((num == 3) ? "000" : "0", CultureInfo.InvariantCulture);
		__result = ((num6 > 0) ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}.{3}", num6, num7, num8, text2) : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}.{2}", num7, num8, text2));
		return false;
	}

	private static void BossPost(string title, bool assist)
	{
		string text = V(Splits);
		if (text == "Bosses + Areas" || text == "Bosses")
		{
			AddSplit(string.IsNullOrEmpty(title) ? "Boss" : title);
		}
	}

	private void Update()
	{
		try
		{
			Tick();
		}
		catch (Exception ex)
		{
			if (resetShown > -100f)
			{
				base.Logger.LogWarning("speedrun: " + ex.Message);
				resetShown = -1000f;
			}
		}
	}

	private static void Tick()
	{
		if (resetShown > 0f)
		{
			resetShown -= Time.unscaledDeltaTime;
		}
		GameManager silentInstance = GameManager.SilentInstance;
		if (!Plugin.Ready || silentInstance == null || HeroController.UnsafeInstance == null || silentInstance.IsMenuScene())
		{
			return;
		}
		if (timer == null)
		{
			timer = UnityEngine.Object.FindObjectOfType<SpeedrunTimer>();
		}
		if (timer == null || timer.slot < 0)
		{
			return;
		}
		LoadRecords();
		if (timer.slot != runSlot)
		{
			LoadRun(timer.slot);
		}
		BossCard.Fight fight = ((BossCard.Instance != null) ? BossCard.Instance.fight : null);
		if (fight != null && fight != lastFight)
		{
			fightTitle = (string.IsNullOrEmpty(fight.Title) ? fight.Id : fight.Title);
			Bump("att|" + fightTitle);
		}
		lastFight = fight;
		if (run.Count > 0 && timer.fileTime + 1f < run[run.Count - 1].Time)
		{
			ResetRun();
		}
		if (AssistMode.KeyPressed(KeyReset))
		{
			ResetRun();
			resetShown = 1.5f;
			Sfx.Play(Sounds.MenuToggleOff.Id);
		}
		string currentAreaName = BossCard.CurrentAreaName;
		if (!string.IsNullOrEmpty(currentAreaName) && currentAreaName != lastArea)
		{
			lastArea = currentAreaName;
			string text = V(Splits);
			if ((text == "Bosses + Areas" || text == "Areas") && areasSplit.Add(currentAreaName))
			{
				AddSplit(currentAreaName);
			}
		}
		if (timer.room != trackedRoom)
		{
			if (trackedRoom != null && prevRoomTime > 0.5f)
			{
				lastRoomName = trackedRoom;
				lastRoomTime = prevRoomTime;
				string key = "room|" + trackedRoom;
				lastRoomGold = !records.TryGetValue(key, out var value) || prevRoomTime < value;
				if (lastRoomGold)
				{
					records[key] = prevRoomTime;
					SaveRecords();
				}
			}
			trackedRoom = timer.room;
			topSpeed = 0f;
		}
		prevRoomTime = timer.roomTime;
		if (Plugin.Controller.Active)
		{
			topSpeed = Math.Max(topSpeed, Plugin.Controller.Speed.magnitude);
		}
	}

	private static void AddSplit(string name)
	{
		if (timer == null || timer.slot < 0)
		{
			return;
		}
		float fileTime = timer.fileTime;
		float num = ((run.Count > 0) ? run[run.Count - 1].Time : 0f);
		float num2 = fileTime - num;
		Split item = new Split
		{
			Name = name,
			Time = fileTime
		};
		string key = "pb|" + name;
		string key2 = "seg|" + name;
		float value;
		bool flag = records.TryGetValue(key2, out value);
		item.Gold = !flag || num2 < value;
		float value2;
		if (V(Compare) == "Best Segments")
		{
			if (flag)
			{
				item.Delta = num2 - value;
				item.HasDelta = true;
			}
		}
		else if (records.TryGetValue(key, out value2))
		{
			item.Delta = fileTime - value2;
			item.HasDelta = true;
		}
		if (item.Gold)
		{
			records[key2] = num2;
		}
		if (!records.TryGetValue(key, out var value3) || fileTime < value3)
		{
			records[key] = fileTime;
		}
		run.Add(item);
		SaveRecords();
		SaveRun();
	}

	private static void Bump(string key)
	{
		records.TryGetValue(key, out var value);
		records[key] = value + 1f;
		SaveRecords();
	}

	private static int Count(string key)
	{
		if (!records.TryGetValue(key, out var value))
		{
			return 0;
		}
		return (int)value;
	}

	private static void ResetRun()
	{
		if (runSlot >= 0 && run.Count > 0)
		{
			Bump("resets|" + runSlot);
		}
		run.Clear();
		areasSplit.Clear();
		lastArea = BossCard.CurrentAreaName;
		if (lastArea != null)
		{
			areasSplit.Add(lastArea);
		}
		SaveRun();
	}

	private static void LoadRecords()
	{
		if (recordsLoaded)
		{
			return;
		}
		recordsLoaded = true;
		try
		{
			if (!File.Exists(RecordsFile))
			{
				return;
			}
			string[] array = File.ReadAllLines(RecordsFile);
			foreach (string text in array)
			{
				int num = text.LastIndexOf('=');
				if (num > 0 && float.TryParse(text.Substring(num + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
				{
					records[text.Substring(0, num)] = result;
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("speedrun records: " + ex.Message);
		}
	}

	private static void SaveRecords()
	{
		try
		{
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, float> record in records)
			{
				list.Add(record.Key + "=" + record.Value.ToString("R", CultureInfo.InvariantCulture));
			}
			File.WriteAllLines(RecordsFile, list.ToArray());
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("speedrun records: " + ex.Message);
		}
	}

	private static void LoadRun(int slot)
	{
		runSlot = slot;
		run.Clear();
		areasSplit.Clear();
		lastArea = null;
		try
		{
			if (!File.Exists(RunFile(slot)))
			{
				return;
			}
			string[] array = File.ReadAllLines(RunFile(slot));
			foreach (string text in array)
			{
				int num = text.LastIndexOf('=');
				if (num > 0 && float.TryParse(text.Substring(num + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
				{
					string item = text.Substring(0, num);
					run.Add(new Split
					{
						Name = item,
						Time = result
					});
					areasSplit.Add(item);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("speedrun run: " + ex.Message);
		}
		lastArea = BossCard.CurrentAreaName;
	}

	private static void SaveRun()
	{
		if (runSlot < 0)
		{
			return;
		}
		try
		{
			List<string> list = new List<string>();
			foreach (Split item in run)
			{
				string text = item.Name;
				float time = item.Time;
				list.Add(text + "=" + time.ToString("R", CultureInfo.InvariantCulture));
			}
			File.WriteAllLines(RunFile(runSlot), list.ToArray());
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("speedrun run: " + ex.Message);
		}
	}

	private static string Signed(float d)
	{
		return ((d < 0f) ? "-" : "+") + BossCard.FormatTime(Math.Abs(d));
	}

	private static Texture2D Fade()
	{
		if (fade != null)
		{
			return fade;
		}
		fade = new Texture2D(256, 1, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear
		};
		for (int i = 0; i < 256; i++)
		{
			float num = (float)i / 255f;
			fade.SetPixel(i, 0, new Color(1f, 1f, 1f, (num < 0.55f) ? 1f : Mathf.SmoothStep(1f, 0f, (num - 0.55f) / 0.45f)));
		}
		fade.Apply();
		return fade;
	}

	private static bool Hud(SpeedrunTimer __instance)
	{
		try
		{
			DrawHud(__instance);
		}
		catch (Exception ex)
		{
			if (!hudError)
			{
				hudError = true;
				Plugin.Log.LogError("speedrun hud: " + ex);
			}
			return true;
		}
		return false;
	}

	private static void DrawHud(SpeedrunTimer t)
	{
		GuiArt art = SpeedrunTimer.Art;
		int timerMode = AssistMode.TimerMode;
		bool flag = timerMode != 0;
		bool flag2 = V(Speedo) == "On" || V(Inputs) == "On";
		if (TheoCamera.Hiding || TheoCamera.HidingHud || (!flag && !flag2 && t.modeShown <= 0f) || art == null || Event.current.type != EventType.Repaint)
		{
			return;
		}
		bool flag3 = true;
		bool flag4 = false;
		try
		{
			flag3 = GameCameras.instance == null || GameCameras.instance.IsHudVisible;
		}
		catch (Exception)
		{
		}
		try
		{
			HeroController silentInstance = HeroController.UnsafeInstance;
			GameManager silentInstance2 = GameManager.SilentInstance;
			flag4 = (silentInstance != null && silentInstance.cState.transitioning) || (silentInstance2 != null && silentInstance2.IsInSceneTransition);
		}
		catch (Exception)
		{
		}
		float num = ((flag3 && !CelesteTextbox.Showing && !flag4) ? 1f : 0f);
		t.vis = Mathf.MoveTowards(t.vis, num, Time.unscaledDeltaTime / ((num > t.vis) ? 0.35f : 0.2f));
		if (t.vis <= 0f && t.modeShown <= 0f)
		{
			return;
		}
		GameManager silentInstance3 = GameManager.SilentInstance;
		if (!Plugin.Ready || silentInstance3 == null || HeroController.UnsafeInstance == null || silentInstance3.IsMenuScene() || !PostcardView.TutorialDone || silentInstance3.isPaused || AssistMode.IsOpen || BossCard.RecordsOpen || (PlayerData.instance != null && PlayerData.instance.isInventoryOpen))
		{
			return;
		}
		float a = 1f - (1f - t.vis) * (1f - t.vis) * (1f - t.vis);
		float num2 = (float)Screen.height / 1080f;
		Matrix4x4 matrix = GUI.matrix;
		GUI.depth = 2;
		GUI.matrix = Matrix4x4.TRS(new Vector3((0f - (1f - a)) * 340f * num2, 0f, 0f), Quaternion.identity, new Vector3(num2, num2, 1f));
		Color outline = A(Ink, 0.9f);
		List<HudLine> list = new List<HudLine>();
		if (resetShown > 0f)
		{
			list.Add(new HudLine
			{
				L = "Splits reset",
				LC = Gold,
				Scale = 0.36f
			});
		}
		if (flag)
		{
			bool num3 = timerMode == 3 || timerMode == 5;
			bool flag5 = timerMode >= 4;
			Color lC = Cream;
			if (V(Pace) == "On" && V(Compare) != "Off" && run.Count > 0)
			{
				Split split = run[run.Count - 1];
				if (split.Gold)
				{
					lC = Color.Lerp(Cream, Gold, 0.7f);
				}
				else if (split.HasDelta)
				{
					lC = Color.Lerp(Cream, (split.Delta <= 0f) ? Ahead : Behind, 0.7f);
				}
			}
			list.Add(new HudLine
			{
				L = BossCard.FormatTime((timerMode == 2) ? t.roomTime : (flag5 ? t.AllSaves() : t.fileTime)),
				LC = lC,
				Scale = 0.6f
			});
			if (num3)
			{
				list.Add(new HudLine
				{
					L = BossCard.FormatTime(t.roomTime),
					LC = Muted,
					Scale = 0.36f
				});
			}
			int num4 = V(SplitList) switch
			{
				"Last 1" => 1, 
				"Last 5" => 5, 
				"Last 8" => 8, 
				"Hidden" => 0, 
				_ => 3, 
			};
			bool flag6 = V(Compare) != "Off";
			for (int i = Math.Max(0, run.Count - num4); i < run.Count; i++)
			{
				Split split2 = run[i];
				string l = ((split2.Name.Length > 20) ? (split2.Name.Substring(0, 19) + ".") : split2.Name);
				string text = BossCard.FormatTime(split2.Time);
				Color rC = Cream;
				if (flag6 && split2.HasDelta)
				{
					text = Signed(split2.Delta) + "  " + text;
					rC = ((split2.Delta <= 0f) ? Ahead : Behind);
				}
				if (flag6 && split2.Gold)
				{
					rC = Gold;
				}
				list.Add(new HudLine
				{
					L = l,
					LC = Muted,
					R = text,
					RC = rC,
					Scale = 0.36f
				});
			}
			if (V(SumOfBest) == "On" && run.Count > 0)
			{
				float num5 = 0f;
				foreach (Split item in run)
				{
					if (records.TryGetValue("seg|" + item.Name, out var value))
					{
						num5 += value;
					}
				}
				float num6 = Math.Max(0f, run[run.Count - 1].Time - num5);
				list.Add(new HudLine
				{
					L = "Best possible",
					LC = Muted,
					R = BossCard.FormatTime(t.fileTime - num6),
					RC = Cream,
					Scale = 0.36f
				});
				if (num6 > 0.005f)
				{
					list.Add(new HudLine
					{
						L = "Time to save",
						LC = Muted,
						R = "-" + BossCard.FormatTime(num6),
						RC = Gold,
						Scale = 0.36f
					});
				}
			}
			if (V(Attempts) == "On")
			{
				if (BossCard.InFight && fightTitle != null)
				{
					list.Add(new HudLine
					{
						L = ((fightTitle.Length > 16) ? (fightTitle.Substring(0, 15) + ".") : fightTitle),
						LC = Muted,
						R = "try " + Count("att|" + fightTitle),
						RC = Cream,
						Scale = 0.36f
					});
				}
				int num7 = Count("resets|" + runSlot);
				if (num7 > 0)
				{
					list.Add(new HudLine
					{
						L = "Resets",
						LC = Muted,
						R = num7.ToString(CultureInfo.InvariantCulture),
						RC = Cream,
						Scale = 0.36f
					});
				}
			}
			if (V(LastRoom) == "On" && lastRoomTime >= 0f)
			{
				list.Add(new HudLine
				{
					L = "Last room",
					LC = Muted,
					R = BossCard.FormatTime(lastRoomTime),
					RC = (lastRoomGold ? Gold : Cream),
					Scale = 0.36f
				});
			}
		}
		if (V(Speedo) == "On" && Plugin.Controller.Active)
		{
			list.Add(new HudLine
			{
				L = "Speed",
				LC = Muted,
				R = Mathf.RoundToInt(Plugin.Controller.Speed.magnitude) + "  top " + Mathf.RoundToInt(topSpeed),
				RC = Cream,
				Scale = 0.36f
			});
		}
		float num8 = 300f;
		float num9 = 14f;
		foreach (HudLine item2 in list)
		{
			float num10 = art.Measure(item2.L) * item2.Scale;
			if (item2.R != null)
			{
				num10 = Math.Max(num10, 200f + art.Measure(item2.R) * item2.Scale);
			}
			num8 = Math.Max(num8, num10 + 60f);
			num9 += (float)art.LineHeight * item2.Scale + 6f;
		}
		bool flag7 = V(Inputs) == "On";
		float num11 = (flag7 ? 84f : 0f);
		string text2 = V(Position);
		float num12 = ((text2 == "Top Left") ? 225f : ((!(text2 == "Middle Left")) ? (1040f - num11 - num9) : 400f));
		float num13 = num12;
		if (num13 < 300f && list.Count + (flag7 ? 1 : 0) > 0)
		{
			HudShift.PanelPixels = (num9 + num11 + 10f) * a;
		}
		if (t.modeShown > 0f)
		{
			float a2 = Mathf.Clamp01(t.modeShown);
			art.DrawOutlined("Timer: " + AssistMode.TimerModeName, 32f, num13 - 8f - (float)art.LineHeight * 0.36f, 0.36f, new Color(Cream.r, Cream.g, Cream.b, a2), new Color(0f, 0f, 0f, a2), 2f);
		}
		if (list.Count > 0)
		{
			GUI.color = A(Ink, 0.42f);
			GUI.DrawTexture(new Rect(0f, num13, num8 + 120f, num9), Fade());
			GUI.color = A(Cream, 0.55f);
			GUI.DrawTexture(new Rect(0f, num13, num8 + 60f, 2f), Fade());
			GUI.DrawTexture(new Rect(0f, num13 + num9 - 2f, num8 + 60f, 2f), Fade());
		}
		float num14 = num13 + 8f;
		foreach (HudLine item3 in list)
		{
			art.DrawOutlined(item3.L, 32f, num14, item3.Scale, A(item3.LC), outline, 2f);
			if (item3.R != null)
			{
				art.DrawOutlined(item3.R, 232f, num14, item3.Scale, A(item3.RC), outline, 2f);
			}
			num14 += (float)art.LineHeight * item3.Scale + 6f;
		}
		int num15 = BossCard.GoldenCount(silentInstance3.profileID);
		GuiArt.Img img = BossCard.PixelBerry() ?? art.Image("collectables/goldberry");
		if (flag && num15 > 0 && img != null && AssistMode.TimerBerries)
		{
			float num16 = 36f;
			float num17 = num13 - num16 - 2f;
			GuiArt.Draw(img, new Rect(22f, num17, num16, num16), A(Color.white));
			art.DrawOutlined("x" + num15.ToString(CultureInfo.InvariantCulture), 22f + num16 + 1f, num17 + num16 / 2f - (float)art.LineHeight * 0.36f * 0.8f / 2f, 0.28800002f, A(Cream), outline, 2f);
		}
		if (flag7)
		{
			DrawInputs(art, num13 + num9 + 12f, a, outline);
		}
		GUI.matrix = matrix;
		GUI.color = Color.white;
		Color A(Color c, float m = 1f)
		{
			c.a = a * m;
			return c;
		}
	}

	private static void DrawInputs(GuiArt art, float y, float a, Color outline)
	{
		HeroActions heroActions = ManagerSingleton<InputHandler>.Instance?.inputActions;
		if (heroActions != null)
		{
			Key(70f, y, 34f, "^", P(heroActions.Up));
			Key(32f, y + 34f + 4f, 34f, "<", P(heroActions.Left));
			Key(70f, y + 34f + 4f, 34f, "v", P(heroActions.Down));
			Key(108f, y + 34f + 4f, 34f, ">", P(heroActions.Right));
			Key(156f, y + 34f + 4f, 72f, "Jump", P(heroActions.Jump));
			Key(156f + 72f + 4f, y + 34f + 4f, 72f, "Dash", P(heroActions.Attack));
			Key(156f + 152f, y + 34f + 4f, 72f, "Grab", P(heroActions.Dash) && !AssistMode.NoGrabbing);
			GUI.color = Color.white;
		}
		void Key(float x, float yy, float w, string label, bool down)
		{
			GUI.color = (down ? new Color(Cream.r, Cream.g, Cream.b, 0.9f * a) : new Color(Ink.r, Ink.g, Ink.b, 0.42f * a));
			GUI.DrawTexture(new Rect(x, yy, w, 34f), Texture2D.whiteTexture);
			Color c = (down ? new Color(Ink.r, Ink.g, Ink.b, a) : new Color(Cream.r, Cream.g, Cream.b, a));
			float num = 0.3f;
			art.DrawOutlined(label, x + (w - art.Measure(label) * num) / 2f, yy + (34f - (float)art.LineHeight * num) / 2f, num, c, down ? new Color(1f, 1f, 1f, 0f) : outline, down ? 0f : 1f);
		}
		static bool P(PlayerAction p)
		{
			return p?.IsPressed ?? false;
		}
	}
}
