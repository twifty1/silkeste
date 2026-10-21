using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GlobalEnums;
using InControl;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

public class AssistMode : MonoBehaviour
{
	public static GuiArt Art;

	public static string DataDir;

	private static readonly Dictionary<string, int> chosen = new Dictionary<string, int>();

	private static bool loaded;

	public const int MaxHair = 8;

	private static readonly int[] HairLengths = { 3, 4, 6, 8 };

	private static readonly float[] HairSizes = { 0.8f, 1f, 1.3f };

	private TimeManager.TimeControlInstance speedControl;

	private TimeManager.TimeControlInstance menuPause;

	private TimeManager.TimeControlInstance dashFreeze;

	private float tauntHeld;

	private float dashFreezeFor;

	private bool testHold;

	private bool open;

	private bool relinquished;

	private bool dashFreezing;

	public static bool IsOpen;

	private bool cursorWas;

	private CursorLockMode lockWas;

	private Rect arrowLeft;

	private Rect arrowRight;

	private Rect previewBody;

	private int previewPose;

	private static readonly string[] PreviewPoses = new string[5]
	{
		Animations.Idle.CelesteAnim,
		Animations.RunFast.CelesteAnim,
		Animations.Dash.CelesteAnim,
		Animations.JumpSlow.CelesteAnim,
		Animations.ClimbUp.CelesteAnim
	};

	private static readonly string[] PoseNames = new string[5] { "Standing", "Running", "Dashing", "Jumping", "Climbing" };

	private int selected;

	private float openT;

	private float flash;

	private float menuScroll;

	private static readonly string[] Tabs = new string[11] { "Game", "Audio", "Video", "Controls", "Appearance", "Game Modes", "Assist Mode", "Variants", "Challenges", "Speedrun Timer", "About" };

	private static readonly string[] TabNames = new string[11] { "Game", "Audio", "Video", "Controls", "Customization", "Game Modes", "Assist", "Variants", "Challenges", "Speedrun", "About" };

	private int tab;

	private readonly Rect[] tabRects = new Rect[Tabs.Length];


	private readonly Vector2[] previewHairNodes = new Vector2[MaxHair];

	private readonly Dictionary<int, Rect> rowRects = new Dictionary<int, Rect>();

	private readonly Dictionary<int, Rect[]> rowArrows = new Dictionary<int, Rect[]>();

	private float lastClickAt = -10f;

	private Rect resetRect;

	// a tab with Silkeste's own options on it that aren't all at their defaults already
	private bool CanResetTab()
	{
		if (LockedSection(Tabs[tab]))
		{
			return false;
		}
		foreach (AssistRow r in AssistOptions.All)
		{
			if (r.Section == Tabs[tab] && !SilksongOptions.Is(r) && !SilksongOptions.IsGroup(r) && !SilksongOptions.IsButton(r) && Value(r) != r.Default)
			{
				return true;
			}
		}
		return false;
	}

	// ---- per-save game settings (user 2026-10-17) ----------------------------------------------------------------
	// Game mode, difficulty and every Assist, Variant, Challenge and Speedrun option belong to a save file: picking a
	// file (or loading one) swaps them in; a file that has none yet starts on Standard + Normal. Looks stay shared.
	private static readonly string[] PerSave = new string[5] { "Game Modes", "Assist Mode", "Variants", "Challenges", "Speedrun Timer" };

	private static readonly Dictionary<int, Dictionary<string, string>> profiles = new Dictionary<int, Dictionary<string, string>>();

	private static bool profilesLoaded;

	private static bool profilesExisted;

	private static int profileSlot = -1;

	/// <summary>The save file whose game settings are in use (-1: none yet).</summary>
	public static int ProfileSlot => profileSlot;

	/// <summary>The game mode of the file in use (Presets ids).</summary>
	public static string ModeId = "standard";

	/// <summary>The file in use is in Sandbox for good (user 2026-10-19): only deleting it brings the other modes back.</summary>
	public static bool SandboxLocked;

	/// <summary>Locks the file in use into Sandbox.</summary>
	public static void LockSandbox()
	{
		if (!SandboxLocked)
		{
			SandboxLocked = true;
			Plugin.Log.LogInfo("Save settings: file " + profileSlot + " is in Sandbox for good");
			Save();
		}
	}

	private static string ProfilesFile => Path.Combine(DataDir ?? "", "save_settings.cfg");

	private static bool IsPerSave(AssistRow r)
	{
		return Array.IndexOf(PerSave, r.Section) >= 0 && !SilksongOptions.Is(r) && !SilksongOptions.IsGroup(r) && !SilksongOptions.IsButton(r);
	}

	private static Dictionary<string, string> Capture()
	{
		Dictionary<string, string> d = new Dictionary<string, string>();
		foreach (AssistRow r in AssistOptions.All)
		{
			if (IsPerSave(r) && Value(r) != r.Default)
			{
				d[r.Id] = Value(r);
			}
		}
		d["__mode"] = ModeId;
		if (SandboxLocked)
		{
			d["__lock"] = "sandbox";
		}
		return d;
	}

	private static void LoadProfiles()
	{
		if (profilesLoaded)
		{
			return;
		}
		profilesLoaded = true;
		try
		{
			if (!File.Exists(ProfilesFile))
			{
				return;
			}
			profilesExisted = true;
			foreach (string line in File.ReadAllLines(ProfilesFile))
			{
				string[] parts = line.Split('|');
				if (parts.Length == 2 && parts[0] == "active" && int.TryParse(parts[1], out int act))
				{
					profileSlot = act;
					continue;
				}
				if (parts.Length != 2 || !int.TryParse(parts[0], out int slot))
				{
					continue;
				}
				Dictionary<string, string> d = new Dictionary<string, string>();
				foreach (string kv in parts[1].Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries))
				{
					int eq = kv.IndexOf('=');
					if (eq > 0)
					{
						d[kv.Substring(0, eq)] = kv.Substring(eq + 1);
					}
				}
				profiles[slot] = d;
			}
			if (profileSlot >= 0 && profiles.TryGetValue(profileSlot, out var cur) && cur.TryGetValue("__mode", out string m))
			{
				ModeId = m;
				SandboxLocked = cur.TryGetValue("__lock", out string lk) && lk == "sandbox";
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Save settings: " + ex.Message);
		}
	}

	private static void SaveProfiles()
	{
		try
		{
			List<string> lines = new List<string>();
			if (profileSlot >= 0)
			{
				lines.Add("active|" + profileSlot);
			}
			foreach (KeyValuePair<int, Dictionary<string, string>> p in profiles)
			{
				List<string> kvs = new List<string>();
				foreach (KeyValuePair<string, string> kv in p.Value)
				{
					kvs.Add(kv.Key + "=" + kv.Value);
				}
				lines.Add(p.Key + "|" + string.Join(";", kvs.ToArray()));
			}
			File.WriteAllLines(ProfilesFile, lines.ToArray());
		}
		catch (Exception)
		{
		}
	}

	/// <summary>A save file's game mode name and difficulty, for the file cards and the pause card.</summary>
	public static void ProfileOf(int slot, out string mode, out string difficulty)
	{
		Load();
		LoadProfiles();
		if (slot == profileSlot || !profiles.TryGetValue(slot, out Dictionary<string, string> d))
		{
			if (slot == profileSlot)
			{
				mode = Presets.Find(ModeId).Name;
				difficulty = Value(AssistOptions.Difficulty);
				return;
			}
			d = new Dictionary<string, string>();
		}
		mode = Presets.Find(d.TryGetValue("__mode", out string m) ? m : "standard").Name;
		difficulty = d.TryGetValue(AssistOptions.Difficulty.Id, out string v) ? v : AssistOptions.Difficulty.Default;
	}

	/// <summary>A deleted file's game settings go with it: a new game there starts on Standard + Normal.</summary>
	public static void ForgetSlot(int slot)
	{
		StartingAbilities.Forget(slot);
		LoadProfiles();
		if (profiles.Remove(slot))
		{
			if (profileSlot == slot)
			{
				profileSlot = -1;
				ModeId = "standard";
				SandboxLocked = false;
			}
			SaveProfiles();
		}
	}

	/// <summary>Makes the game settings those of this save file (file select, or a loaded save).</summary>
	public static void UseSlot(int slot)
	{
		Load();
		LoadProfiles();
		if (slot < 0 || slot == profileSlot)
		{
			return;
		}
		if (profileSlot >= 0)
		{
			profiles[profileSlot] = Capture();
		}
		else if (!profilesExisted && !profiles.ContainsKey(slot))
		{
			profiles[slot] = Capture();   // first time: the settings you had become this file's
		}
		profiles.TryGetValue(slot, out Dictionary<string, string> d);
		d = d ?? new Dictionary<string, string>();
		foreach (AssistRow r in AssistOptions.All)
		{
			if (!IsPerSave(r))
			{
				continue;
			}
			chosen.Remove(Key(r));
			if (d.TryGetValue(r.Id, out string v))
			{
				int i = Array.IndexOf(Values(r), v);
				if (i >= 0)
				{
					chosen[Key(r)] = i;
				}
			}
		}
		ModeId = d.TryGetValue("__mode", out string mode) ? mode : "standard";
		SandboxLocked = d.TryGetValue("__lock", out string lk) && lk == "sandbox";
		profileSlot = slot;
		Save();
		Plugin.Log.LogInfo("Save settings: file " + slot + " (" + ModeId + ", " + Value(AssistOptions.Difficulty) + ")");
	}

	// ---- presets (Presets.cs) ----------------------------------------------------------------------------------
	private static bool pendingMark;

	/// <summary>Every option of these tabs back to its default, then the preset's values on top.</summary>
	public static void ApplyPreset(Dictionary<string, string> values, string[] sections)
	{
		Load();
		bool assist = false;
		foreach (AssistRow r in AssistOptions.All)
		{
			if (Array.IndexOf(sections, r.Section) < 0 || SilksongOptions.Is(r) || SilksongOptions.IsGroup(r) || SilksongOptions.IsButton(r))
			{
				continue;
			}
			chosen.Remove(Key(r));
			if (values.TryGetValue(r.Id, out string v))
			{
				int i = Array.IndexOf(Values(r), v);
				if (i >= 0)
				{
					chosen[Key(r)] = i;
					if (v != r.Default && (r.Section == "Assist Mode" || r.Section == "Variants"))
					{
						assist = true;
					}
				}
			}
		}
		Save();
		// Assist and Variants options make it an assist save, the same as turning them on by hand
		if (assist && !SaveMarked)
		{
			if (InSave(out _))
			{
				MarkSave();
			}
			else
			{
				pendingMark = true;   // from the file select: marked once the save is loaded
			}
		}
		else if (!assist)
		{
			pendingMark = false;
		}
		Sfx.Play(Sounds.MenuToggleOn.Id);
	}

	private void ResetTab()
	{
		foreach (AssistRow r in AssistOptions.All)
		{
			if (r.Section == Tabs[tab] && !SilksongOptions.Is(r) && !SilksongOptions.IsGroup(r) && !SilksongOptions.IsButton(r))
			{
				chosen.Remove(Key(r));
			}
		}
		Save();
		Sfx.Play(Sounds.MenuToggleOff.Id);
	}

	private float lastRawClick = -10f;

	private float tabChangedAt = -10f;

	private readonly Dictionary<int, Rect> barRects = new Dictionary<int, Rect>();

	private int dragging = -1;

	private int hoverTab = -1;

	// a plain number range with enough steps to be a slider (Silksong's volumes)
	private static bool IsBarRow(AssistRow r)
	{
		string[] v = Values(r);
		if (v.Length < 6)
		{
			return false;
		}
		for (int i = 0; i < v.Length; i++)
		{
			if (!int.TryParse(v[i], out var _))
			{
				return false;
			}
		}
		return true;
	}

	private static bool GroupChanged(AssistRow g)
	{
		foreach (AssistRow r in AssistOptions.All)
		{
			if (SilksongOptions.GroupOf(r) == g.Id && Value(r) != r.Default)
			{
				return true;
			}
		}
		return false;
	}

	// sets a row to value i the same way Left/Right would
	private void SetRow(AssistRow r, int i)
	{
		if (Index(r) == i)
		{
			return;
		}
		if (SilksongOptions.Is(r))
		{
			SilksongOptions.Set(r, i);
		}
		else
		{
			chosen[Key(r)] = i;
		}
		Sfx.Play(Sounds.MenuRolloverDown.Id, 0.6f);
	}

	private Rect arrowL;

	private Rect arrowR;

	private bool padHints;

	public static AssistMode Instance;

	private static bool fromMenu;

	private bool loggedError;

	private static int LookIndex
	{
		get
		{
			Load();
			if (!chosen.TryGetValue(AssistOptions.Look.Id, out var value))
			{
				return 0;
			}
			return value;
		}
	}

	public static bool CatEars => Value(AssistOptions.CatEars) == "On";

	public static int HairLength => HairLengths[Math.Min(HairLengths.Length - 1, Index(AssistOptions.HairLength))];

	public static float HairSize => HairSizes[Math.Min(HairSizes.Length - 1, Index(AssistOptions.HairSize))];

	public static bool AssistOn
	{
		get
		{
			return true;
		}
		set
		{
			Load();
			chosen["assist_on"] = (value ? 1 : 0);
			Save();
		}
	}

	private static bool AnyRaw
	{
		get
		{
			AssistRow[] all = AssistOptions.All;
			for (int i = 0; i < all.Length; i++)
			{
				AssistRow r = all[i];
				if (r.Section == "Assist Mode" && Value(r) != r.Default)
				{
					return true;
				}
			}
			return false;
		}
	}

	public static float GameSpeed
	{
		get
		{
			if (!AssistOn)
			{
				return 1f;
			}
			return float.Parse(Value(AssistOptions.GameSpeed).TrimEnd(new char[1] { '%' }), CultureInfo.InvariantCulture) / 100f;
		}
	}

	public static bool InfiniteStamina
	{
		get
		{
			if (AssistOn)
			{
				return Value(AssistOptions.InfiniteStamina) == "On";
			}
			return false;
		}
	}

	public static int AirDashes
	{
		get
		{
			// Removed from the settings (Dash Count covers it); always the default now.
			return 0;
		}
	}

	public static bool DashAssist
	{
		get
		{
			if (AssistOn)
			{
				return Value(AssistOptions.DashAssist) == "On";
			}
			return false;
		}
	}

	public static bool Invincible
	{
		get
		{
			if (AssistOn)
			{
				return Value(AssistOptions.Invincibility) == "On";
			}
			return false;
		}
	}

	public static bool ForgivingJumps => Value(AssistOptions.ForgivingJumps) == "On" || Difficulty == 0;   // Story comes with them

	public static bool MirrorMode => Value(AssistOptions.MirrorMode) == "On";

	public static float Friction => (new float[4] { 1f, 0.4f, 0.12f, 0.04f })[Math.Min(3, Index(AssistOptions.LowFriction))];

	public static bool LowFriction => Friction < 1f;

	public static int JumpCount
	{
		get
		{
			int num = Index(AssistOptions.JumpCount);
			if (num < 3)
			{
				return num + 1;
			}
			return -1;
		}
	}

	public static float DashLength => (new float[4] { 0.7f, 1f, 1.35f, 1.7f })[Math.Min(3, Index(AssistOptions.DashLength))];

	public static float Gravity => (new float[3] { 0.55f, 1f, 1.6f })[Math.Min(2, Index(AssistOptions.Gravity))];

	public static int DashCount
	{
		get
		{
			int num = Index(AssistOptions.DashCount);
			if (num != 0)
			{
				if (num < 5)
				{
					return num - 1;
				}
				return -1;
			}
			// Sandbox files dash forever unless Game > Sandbox: Infinite Dashes is off (user 2026-10-21); Assist's Dash Count,
			// when set, wins
			return ModeId == "sandbox" && Value(AssistOptions.SandboxInfiniteDash) == "On" ? -1 : -2;
		}
	}

	public static bool NoClimbJump => Value(AssistOptions.NoClimbJump) == "On";

	public static bool NoWallJump => Value(AssistOptions.NoWallJump) == "On";

	public static int Wind => (new int[5] { 0, -1, 1, -2, 2 })[Math.Min(4, Index(AssistOptions.Wind))];

	public static float MadelineSpeed => (new float[3] { 1f, 1.25f, 1.5f })[Math.Min(2, Index(AssistOptions.MadelineSpeed))];

	public static bool OneHit => Value(AssistOptions.OneHit) == "On";

	/// <summary>Game Modes.difficulty_level: 0 Story, 1 Easy, 2 Normal, 3 Hard, 4 Hardcore.</summary>
	public static int Difficulty => 2;   // always Normal: difficulties are gone (user 2026-10-20)

	/// <summary>How much of an enemy's damage Madeline takes.</summary>
	public static float DamageTaken => (Difficulty == 0) ? 0.25f : ((Difficulty == 1) ? 0.5f : ((Difficulty == 3) ? 1.5f : 1f));

	/// <summary>How hard Madeline's own hits land (Hard only takes more, user 2026-10-17).</summary>
	public static float DamageDealt => (Difficulty == 0) ? 2f : ((Difficulty == 1) ? 1.5f : ((Difficulty == 4) ? 2f : 1f));

	/// <summary>The file select's difficulty picker: the next difficulty along.</summary>
	public static void NextDifficulty()
	{
		Load();
		chosen[Key(AssistOptions.Difficulty)] = (Difficulty + 1) % Values(AssistOptions.Difficulty).Length;
		Save();
		Sfx.Play(Sounds.MenuToggleOn.Id);
	}

	public static string DifficultyName => Value(AssistOptions.Difficulty);

	public static string TitleSky => Value(AssistOptions.TitleSky);

	public static string TitleArea => Value(AssistOptions.TitleArea);

	public static bool CallHerMadeline => Value(AssistOptions.HerName) == "Madeline";

	public static bool BenchFlagsOn => Value(AssistOptions.BenchFlags) == "On";

	/// <summary>assist.instant_respawn: 0 off, 1 room entrance, 2 bench.</summary>
	public static int QuickRespawn => Index(AssistOptions.Respawn);

	public static bool Bouncy => Value(AssistOptions.Bouncy) == "On";

	public static bool HalfStamina => Value(AssistOptions.HalfStamina) == "On";

	public static bool NoWallSlide => Value(AssistOptions.NoWallSlide) == "On";

	public static bool LongHitLock => Value(AssistOptions.LongHitLock) == "On";

	public static bool DashColourRush => Value(AssistOptions.DashColourRush) == "On";

	public static bool SuperDashing => Value(AssistOptions.SuperDashing) == "On";

	public static bool Hiccups => Value(AssistOptions.Hiccups) == "On";

	public static bool InvisibleMotion => Value(AssistOptions.InvisibleMotion) == "On";

	public static bool NoGrabbing => Value(AssistOptions.NoGrabbing) == "On";

	public static float ShakeMult => 1f;

	public static bool DashTrail => Value(AssistOptions.DashTrail) == "On";

	public static bool LandingEffects => Value(AssistOptions.LandingEffects) == "On";

	public static bool LowDetailMenus => false;

	public static bool AnyOn
	{
		get
		{
			if (!AssistOn)
			{
				return false;
			}
			AssistRow[] all = AssistOptions.All;
			for (int i = 0; i < all.Length; i++)
			{
				AssistRow r = all[i];
				if ((r.Section == "Assist Mode" || r.Section == "Variants") && Value(r) != r.Default)
				{
					return true;
				}
			}
			return false;
		}
	}

	public static int TimerMode => Index(AssistOptions.Timer);

	public static string TimerModeName => Value(AssistOptions.Timer);

	public static int HairVersion => Index(AssistOptions.HairTwo) * 1024 + Index(AssistOptions.HairOne) * 32 + Index(AssistOptions.HairZero);

	public static bool TimerBerries => Value(AssistOptions.TimerBerries) == "On";

	public static bool AutoSkip => Value(AssistOptions.AutoSkip) == "On";

	public static bool UnlimitedBounces => Value(AssistOptions.UnlimitedBounces) == "On";

	public static bool Backpack => Value(AssistOptions.Backpack) == "On";

	public static string SkinName => Value(AssistOptions.Skin);

	public static string OutfitName => Value(AssistOptions.Outfit);

	private static string SaveFile => Path.Combine(DataDir ?? "", "assist.cfg");

	private static string[] Values(AssistRow r)
	{
		return r.Values.Split(new char[1] { ',' });
	}

	private static bool IsLookRow(AssistRow r)
	{
		if (r.Section == "Appearance" && r.Id != AssistOptions.Look.Id && r.Id != AssistOptions.DashColourRush.Id && r.Id != AssistOptions.HerName.Id && r.Id != AssistOptions.BenchFlags.Id)
		{
			return r.Id != AssistOptions.DashTrail.Id && r.Id != AssistOptions.LandingEffects.Id;
		}
		return false;
	}

	private static string KeyFor(AssistRow r, int look)
	{
		if (!IsLookRow(r))
		{
			return r.Id;
		}
		return "look" + (look + 1) + "." + r.Id;
	}

	private static string Key(AssistRow r)
	{
		return KeyFor(r, LookIndex);
	}

	private static int DefaultIndex(AssistRow r, int look)
	{
		if (r.Id == AssistOptions.Skin.Id)
		{
			string[] array = "Madeline,Badeline,Knight,Pink Cat".Split(new char[1] { ',' });
			int num = Array.IndexOf(Values(r), array[Math.Min(look, array.Length - 1)]);
			if (num >= 0)
			{
				return num;
			}
		}
		return Array.IndexOf(Values(r), r.Default);
	}

	private static int IndexFor(AssistRow r, int look)
	{
		Load();
		if (!chosen.TryGetValue(KeyFor(r, look), out var value))
		{
			return DefaultIndex(r, look);
		}
		return value;
	}

	private static int Index(AssistRow r)
	{
		if (SilksongOptions.Is(r) || SilksongOptions.IsGroup(r))
		{
			return SilksongOptions.Index(r);
		}
		if (Locked(r))
		{
			return DefaultIndex(r, 0);
		}
		return IndexFor(r, IsLookRow(r) ? LookIndex : 0);
	}

	// Assist Mode and Variants stay locked on a save until the player agrees to make it an
	// "assist save": from then on it earns no achievements and no golden berries. The mark is
	// kept per save slot with the save's play time when marked, so a new game in the slot is clean.
	private static Dictionary<int, float> marks;

	private static string MarksFile => Path.Combine(DataDir ?? "", "assist_saves.cfg");

	private static void LoadMarks()
	{
		if (marks != null)
		{
			return;
		}
		marks = new Dictionary<int, float>();
		try
		{
			if (!File.Exists(MarksFile))
			{
				return;
			}
			string[] array = File.ReadAllLines(MarksFile);
			foreach (string text in array)
			{
				string[] array2 = text.Split('=');
				if (array2.Length == 2 && int.TryParse(array2[0], out var result) && float.TryParse(array2[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
				{
					marks[result] = result2;
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Assist saves: " + ex.Message);
		}
	}

	private static void SaveMarks()
	{
		try
		{
			List<string> list = new List<string>();
			foreach (KeyValuePair<int, float> mark in marks)
			{
				list.Add(mark.Key + "=" + mark.Value.ToString("R", CultureInfo.InvariantCulture));
			}
			Directory.CreateDirectory(DataDir);
			File.WriteAllLines(MarksFile, list.ToArray());
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Assist saves: " + ex.Message);
		}
	}

	private static bool InSave(out GameManager gm)
	{
		gm = GameManager.SilentInstance;
		if (gm != null && !gm.IsMenuScene() && gm.profileID > 0)
		{
			return HeroController.UnsafeInstance != null;
		}
		return false;
	}

	public static bool SaveMarked
	{
		get
		{
			if (!InSave(out var gm))
			{
				return false;
			}
			LoadMarks();
			if (!marks.TryGetValue(gm.profileID, out var value))
			{
				return false;
			}
			if (gm.PlayTime + 60f < value)
			{
				marks.Remove(gm.profileID);
				SaveMarks();
				Plugin.Log.LogInfo("Assist saves: save " + gm.profileID + " was started over, so it can earn rewards again");
				return false;
			}
			return true;
		}
	}

	public static void MarkSaveNow()
	{
		MarkSave();
	}

	private static void MarkSave()
	{
		if (InSave(out var gm))
		{
			LoadMarks();
			marks[gm.profileID] = gm.PlayTime;
			SaveMarks();
			Plugin.Log.LogInfo("Assist saves: save " + gm.profileID + " is now an assist save");
		}
	}

	private static bool LockedSection(string section)
	{
		if (section == "Assist Mode" || section == "Variants")
		{
			return !SaveMarked;
		}
		return false;
	}

	private static bool Locked(AssistRow r)
	{
		return LockedSection(r.Section);
	}

	private int prompt;

	private bool promptYes;

	private float promptT;

	private Rect promptYesRect;

	private Rect promptNoRect;

	private void OpenPrompt()
	{
		prompt = (InSave(out var _) ? 1 : 2);
		promptYes = false;
		promptT = 0f;
		flash = 0f;
		Sfx.Play(Sounds.MenuPause.Id);
	}

	// Runs the unlock prompt; true while it's up, so nothing behind it reacts.
	private bool PromptTick(Keyboard kb, HeroActions a)
	{
		if (prompt == 0)
		{
			return false;
		}
		promptT = Math.Min(1f, promptT + Time.unscaledDeltaTime / 0.2f);
		TrackInputKind(kb);
		bool flag = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || (search == null && kb.dKey.wasPressedThisFrame))) || (a != null && (a.Left.WasPressed || a.Right.WasPressed));
		bool flag2 = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (a?.Jump.WasPressed ?? false);
		bool flag3 = Back(kb, a);
		Mouse current = Mouse.current;
		if (current != null)
		{
			Vector2 point = MenuMouse.Gui();
			if (current.delta.ReadValue().sqrMagnitude > 0.5f || current.leftButton.wasPressedThisFrame)
			{
				if (promptYesRect.Contains(point) && !promptYes)
				{
					promptYes = true;
					flash = 0f;
					Sfx.Play(Sounds.MenuRolloverDown.Id);
				}
				else if (promptNoRect.Contains(point) && promptYes)
				{
					promptYes = false;
					flash = 0f;
					Sfx.Play(Sounds.MenuRolloverDown.Id);
				}
			}
			if (current.leftButton.wasPressedThisFrame && (promptYesRect.Contains(point) || promptNoRect.Contains(point)))
			{
				flag2 = true;
			}
		}
		if (promptT < 0.5f)
		{
			return true;
		}
		if ((prompt == 1 || prompt == 3) && flag)
		{
			promptYes = !promptYes;
			flash = 0f;
			Sfx.Play(Sounds.MenuRolloverDown.Id);
		}
		if (flag2 && prompt == 1 && promptYes)
		{
			MarkSave();
			prompt = 0;
			Sfx.Play(Sounds.MenuToggleOn.Id);
		}
		else if (flag2 && prompt == 3 && promptYes)
		{
			prompt = 0;
			Presets.Apply("sandbox");
			SilksongOptions.Refresh();
		}
		else if (flag2 || flag3)
		{
			prompt = 0;
			Sfx.Play(Sounds.MenuUnpause.Id);
		}
		return true;
	}

	private void DrawLock(float alpha, float cx, float top, float bottom, float half = 520f)
	{
		Color outline = new Color(0f, 0f, 0f, alpha);
		GUI.color = new Color(0f, 0f, 0f, 0.82f * alpha);
		GUI.DrawTexture(new Rect(cx - half, top - 14f, half * 2f, bottom - top + 14f), Texture2D.whiteTexture);
		float num = top + (bottom - top) / 2f - 70f;
		float num2 = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 3f);
		GuiArt.Img img = Art.Image("collectables/goldberry");
		string text = "LOCKED";
		Art.DrawOutlined(text, cx - Art.Measure(text) * num2 / 2f, num, num2, new Color(1f, 0.85f, 0.35f, alpha), outline, 3f);
		num += (float)Art.LineHeight * num2 + 10f;
		string text2 = (InSave(out var _) ? "These change how Silksong plays. Unlocking them makes this an assist save." : "Load a save to unlock these for it.");
		Art.DrawOutlined(text2, cx - Art.Measure(text2) * 0.45f / 2f, num, 0.45f, new Color(0.85f, 0.85f, 0.85f, alpha), outline, 2f);
		num += (float)Art.LineHeight * 0.45f + 14f;
		string text3 = (padHints ? "Press Cross to unlock" : "Press Enter to unlock");
		float a = alpha * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.5f)));
		Art.DrawOutlined(text3, cx - Art.Measure(text3) * 0.5f / 2f, num, 0.5f, new Color(1f, 1f, 1f, a), outline, 2f);
		GUI.color = Color.white;
	}

	private void DrawPrompt(float alpha, float cx)
	{
		float num = 1f - (1f - promptT) * (1f - promptT);
		float num2 = alpha * num;
		Color outline = new Color(0f, 0f, 0f, num2);
		GUI.color = new Color(0f, 0f, 0f, 0.6f * num2);
		GUI.DrawTexture(new Rect(0f, 0f, cx * 2f, 1080f), Texture2D.whiteTexture);
		GuiArt.Img img = Art.Image("textbox/default");
		Rect r = new Rect(cx - 560f, 300f + (1f - num) * 40f, 1120f, 420f);
		if (img != null)
		{
			GUI.color = new Color(1f, 1f, 1f, num2);
			CelesteTextbox.Slices9(img, r, 0.82f);
		}
		else
		{
			GUI.color = new Color(0.1f, 0.1f, 0.14f, 0.95f * num2);
			GUI.DrawTexture(r, Texture2D.whiteTexture);
		}
		Color c = new Color(0.94f, 0.92f, 0.95f, num2);
		float num3 = r.y + 40f;
		string text = ((prompt == 1) ? "Make this an assist save?" : ((prompt == 3) ? "Play this file in Sandbox?" : "No save loaded"));
		Art.DrawOutlined(text, cx - Art.Measure(text) * 0.75f / 2f, num3, 0.75f, new Color(1f, 0.85f, 0.35f, num2), outline, 2f);
		num3 += (float)Art.LineHeight * 0.75f + 18f;
		string s = (prompt == 3) ? "Sandbox unlocks everything on this file for good: every ability, tool, crest and map, plus money and full silk. The file stays in Sandbox until you delete it, and it becomes an assist save that can't earn achievements or golden berries. Other files aren't affected." : ((prompt == 1) ? "Assist Mode and Variants change how the game plays. Once you unlock them, this save can never earn achievements or golden berries again, even if you turn everything back off. Other saves aren't affected." : "Assist Mode and Variants unlock one save at a time. Load the save you want them on, then open the settings there.");
		foreach (string item in Art.Wrap(s, 980f / 0.5f))
		{
			Art.DrawOutlined(item, cx - Art.Measure(item) * 0.5f / 2f, num3, 0.5f, c, outline, 2f);
			num3 += (float)Art.LineHeight * 0.5f;
		}
		float num4 = r.yMax - 80f;
		Color color = ((flash % 0.2f < 0.1f) ? Hex("84FF54") : Hex("FCFF59"));
		color.a = num2;
		promptYesRect = (promptNoRect = default(Rect));
		if (prompt == 1 || prompt == 3)
		{
			string[] array = (prompt == 3) ? new string[2] { "Play Sandbox", "Cancel" } : new string[2] { "Unlock", "Keep locked" };
			for (int i = 0; i < 2; i++)
			{
				bool flag = i == 0 == promptYes;
				float num5 = cx + ((i == 0) ? (-200f) : 200f);
				float num6 = Art.Measure(array[i]) * 0.65f;
				Rect rect = new Rect(num5 - num6 / 2f - 50f, num4 - 10f, num6 + 100f, (float)Art.LineHeight * 0.65f + 20f);
				if (i == 0)
				{
					promptYesRect = rect;
				}
				else
				{
					promptNoRect = rect;
				}
				Color c2 = (flag ? color : new Color(0.75f, 0.75f, 0.75f, num2));
				if (flag)
				{
					Art.DrawOutlined(">", num5 - num6 / 2f - 36f, num4, 0.65f, c2, outline, 2f);
					Art.DrawOutlined("<", num5 + num6 / 2f + 14f, num4, 0.65f, c2, outline, 2f);
				}
				Art.DrawOutlined(array[i], num5 - num6 / 2f, num4, 0.65f, c2, outline, 2f);
			}
		}
		else
		{
			float num7 = Art.Measure("OK") * 0.65f;
			promptNoRect = new Rect(cx - num7 / 2f - 50f, num4 - 10f, num7 + 100f, (float)Art.LineHeight * 0.65f + 20f);
			Art.DrawOutlined("OK", cx - num7 / 2f, num4, 0.65f, color, outline, 2f);
		}
		GUI.color = Color.white;
	}

	private static void ResetLook()
	{
		string value = "look" + (LookIndex + 1) + ".";
		foreach (string item in new List<string>(chosen.Keys))
		{
			if (item.StartsWith(value, StringComparison.Ordinal))
			{
				chosen.Remove(item);
			}
		}
		Sfx.Play(Sounds.MenuToggleOff.Id);
	}

	public static void NextLook()
	{
		chosen[AssistOptions.Look.Id] = (LookIndex + 1) % Values(AssistOptions.Look).Length;
		Save();
	}

	public static Color HairNodeColor(Color c, int i, int n, bool hasDash)
	{
		if (DashColourRush)
		{
			float num = Time.time - MadelineController.LastDashTime;
			if (num < 1f)
			{
				Color a = Color.HSVToRGB(Mathf.Repeat((float)MadelineController.DashNumber * 0.17f + (float)i * 0.08f + num * 1.5f, 1f), 0.75f, 1f);
				a.a = c.a;
				return Color.Lerp(a, c, Mathf.Clamp01((num - 0.6f) / 0.4f));
			}
		}
		switch (Value(AssistOptions.HairEffect))
		{
		case "Rainbow":
		{
			if (!hasDash)
			{
				return c;
			}
			Color result = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.4f + (float)i * 0.09f, 1f), 0.65f, 1f);
			result.a = c.a;
			return result;
		}
		case "Light Tips":
			return Color.Lerp(c, new Color(1f, 1f, 1f, c.a), 0.55f * (float)i / (float)Math.Max(1, n - 1));
		case "Dark Tips":
			return Color.Lerp(c, new Color(0.1f, 0.08f, 0.12f, c.a), 0.55f * (float)i / (float)Math.Max(1, n - 1));
		default:
			return c;
		}
	}

	private static string Value(AssistRow r)
	{
		return Values(r)[Index(r)];
	}

	public static bool KeyPressed(AssistRow row)
	{
		Keyboard current = Keyboard.current;
		if (current == null)
		{
			return false;
		}
		return current[Value(row)]?.wasPressedThisFrame ?? false;
	}

	public static string KeyName(AssistRow row)
	{
		return Value(row);
	}

	public static string HairHex(AssistRow row, string skinHex)
	{
		string text = Value(row);
		string[] array = "Red:AC3232;Purple:9B3FB5;Ghost Blue:9FC0FF;Pale Blue:D8E6FF;Blue:44B7FF;Pink:FF6DEF;PICO Red:FF004D;PICO Blue:29ADFF;PICO Green:00E436;Snow:D3DDF2;Orange:E07A2E;Violet:8E5BFF;Rust:C04A3C;Ivory:D9D1C5;Light Pink:FFB3D4;Gold:F7C948;Blonde:F2D16B;Brown:7A4A2A;Black:2E2E38;White:F4F4F4;Silver:B8C0CC;Green:4CAF50;Mint:7FE0B5;Teal:2BB3B1;Navy:2C3E8C;Lavender:C3A6FF;Crimson:9E1030;Peach:FFB38A".Split(new char[1] { ';' });
		foreach (string text2 in array)
		{
			int num = text2.IndexOf(':');
			if (num > 0 && text2.Substring(0, num) == text)
			{
				return text2.Substring(num + 1);
			}
		}
		return skinHex;
	}

	private static bool IsKeyRow(AssistRow r)
	{
		return r.Section == "Controls";
	}

	private static bool KeyTaken(AssistRow row, int index)
	{
		string text = Values(row)[index];
		AssistRow[] all = AssistOptions.All;
		for (int i = 0; i < all.Length; i++)
		{
			AssistRow r = all[i];
			if (IsKeyRow(r) && r.Id != row.Id && Value(r) == text)
			{
				return true;
			}
		}
		return false;
	}

	public static void SetSkin(string name, bool save)
	{
		int num = Array.IndexOf(Values(AssistOptions.Skin), name);
		if (num >= 0)
		{
			chosen[Key(AssistOptions.Skin)] = num;
			if (save)
			{
				Save();
			}
		}
	}

	public static bool HasSaved(string id)
	{
		Load();
		AssistRow[] all = AssistOptions.All;
		for (int i = 0; i < all.Length; i++)
		{
			AssistRow r = all[i];
			if (r.Id == id)
			{
				return chosen.ContainsKey(Key(r));
			}
		}
		return chosen.ContainsKey(id);
	}

	public static void NextTimerMode()
	{
		chosen[AssistOptions.Timer.Id] = (TimerMode + 1) % Values(AssistOptions.Timer).Length;
		Save();
	}

	private static void Load()
	{
		if (loaded)
		{
			return;
		}
		loaded = true;
		try
		{
			if (DataDir == null || !File.Exists(SaveFile))
			{
				return;
			}
			string[] array = File.ReadAllLines(SaveFile);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(new char[1] { '=' });
				if (array2.Length != 2)
				{
					continue;
				}
				string text = array2[0].Trim();
				string text2 = text;
				if (text == "assist_on")
				{
					chosen[text] = ((array2[1].Trim() == "On") ? 1 : 0);
					continue;
				}
				if (text.StartsWith("look", StringComparison.Ordinal) && text.IndexOf('.') > 0)
				{
					text2 = text.Substring(text.IndexOf('.') + 1);
				}
				AssistRow[] all = AssistOptions.All;
				for (int j = 0; j < all.Length; j++)
				{
					AssistRow r = all[j];
					if (r.Id == text2)
					{
						int num = Array.IndexOf(Values(r), array2[1].Trim());
						if (num >= 0)
						{
							chosen[(IsLookRow(r) && text == text2) ? KeyFor(r, 0) : text] = num;
						}
					}
				}
			}
			MoveKeys();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Assist Mode: " + ex.Message);
		}
	}

	/// <summary>Once (2.1, user 2026-10-19): the Tutorial and Timer keys are gone, Records moves from F5 to F3 and Reset
	/// Splits from F7 to F5, unless someone had already changed them.</summary>
	private static void MoveKeys()
	{
		string flag = Path.Combine(DataDir, "keys_2_1.flag");
		if (File.Exists(flag))
		{
			return;
		}
		File.WriteAllText(flag, "Records F5 to F3, Reset Splits F7 to F5");
		foreach (string[] m in new string[2][]
		{
			new string[3] { "key_records", "F5", "F3" },
			new string[3] { "sr_key_reset", "F7", "F5" }
		})
		{
			foreach (AssistRow r in AssistOptions.All)
			{
				if (r.Id == m[0] && Value(r) == m[1])
				{
					chosen[Key(r)] = Array.IndexOf(Values(r), m[2]);
					Plugin.Log.LogInfo("Keys: " + r.Label + " moved from " + m[1] + " to " + m[2]);
				}
			}
		}
	}

	public static void SaveNow()
	{
		Save();
	}

	/// <summary>A row's value for the save in use.</summary>
	public static string ValueOf(AssistRow r)
	{
		Load();
		return Value(r);
	}

	/// <summary>The next (dir 1) or previous (dir -1) value of a row, saved.</summary>
	public static void Cycle(AssistRow r, int dir)
	{
		Load();
		int n = Values(r).Length;
		chosen[Key(r)] = ((Index(r) + dir) % n + n) % n;
		Save();
	}

	/// <summary>An Off/On row of the save in use (the starting abilities).</summary>
	public static bool IsOn(AssistRow r)
	{
		Load();
		return Value(r) == "On";
	}

	public static void SetOn(AssistRow r, bool on)
	{
		Load();
		int i = Array.IndexOf(Values(r), on ? "On" : "Off");
		if (i >= 0)
		{
			chosen[Key(r)] = i;
			Save();
		}
	}

	private static void Save()
	{
		try
		{
			List<string> list = new List<string>();
			AssistRow[] all = AssistOptions.All;
			for (int i = 0; i < all.Length; i++)
			{
				AssistRow r = all[i];
				if (!IsLookRow(r))
				{
					list.Add(r.Id + "=" + Values(r)[IndexFor(r, 0)]);
					continue;
				}
				for (int j = 0; j < Values(AssistOptions.Look).Length; j++)
				{
					list.Add(KeyFor(r, j) + "=" + Values(r)[IndexFor(r, j)]);
				}
			}
			if (chosen.TryGetValue("assist_on", out var value))
			{
				list.Add("assist_on=" + ((value == 1) ? "On" : "Off"));
			}
			Directory.CreateDirectory(DataDir);
			File.WriteAllLines(SaveFile, list.ToArray());
			if (profileSlot >= 0)
			{
				profiles[profileSlot] = Capture();   // the file in use keeps its own copy as you change things
				SaveProfiles();
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Assist Mode: " + ex.Message);
		}
	}

	// ---- search (user 2026-10-17): Ctrl+F or / and type; every tab's options that match, in one list --------------
	private static string search;

	private bool searchAte;

	private bool SearchKeys(Keyboard kb)
	{
		searchAte = false;
		UnityEngine.InputSystem.Keyboard ik = UnityEngine.InputSystem.Keyboard.current;   // the shim has no letters
		if (FocusTab != null || ik == null)
		{
			return false;
		}
		if (search == null)
		{
			if ((ik.ctrlKey.isPressed && ik.fKey.wasPressedThisFrame) || ik.slashKey.wasPressedThisFrame)
			{
				search = "";
				searchAte = true;
				Sfx.Play(Sounds.MenuButtonSelect.Id);
				return true;
			}
			return false;
		}
		string before = search;
		if (ik.escapeKey.wasPressedThisFrame)
		{
			search = null;
			searchAte = true;
			Sfx.Play(Sounds.MenuToggleOff.Id);
			selected = TabRows().Count > 0 ? TabRows()[0] : selected;
			menuScroll = 0f;
			return true;
		}
		if (ik.backspaceKey.wasPressedThisFrame && search.Length > 0)
		{
			search = search.Substring(0, search.Length - 1);
		}
		for (UnityEngine.InputSystem.Key k = UnityEngine.InputSystem.Key.A; k <= UnityEngine.InputSystem.Key.Z; k++)
		{
			if (ik[k].wasPressedThisFrame && search.Length < 24)
			{
				search += (char)('a' + (k - UnityEngine.InputSystem.Key.A));
			}
		}
		if (ik.spaceKey.wasPressedThisFrame && search.Length > 0 && search.Length < 24)
		{
			search += " ";
		}
		if (search != before)
		{
			searchAte = true;
			List<int> l = TabRows();
			if (l.Count > 0)
			{
				selected = l[0];
			}
			menuScroll = 0f;
			Sfx.Play(Sounds.MenuRolloverDown.Id, 0.5f);
		}
		return true;
	}

	private List<int> SearchRows()
	{
		List<int> list = new List<int>();
		string q = search.Trim().ToLowerInvariant();
		for (int i = 0; i < AssistOptions.All.Length; i++)
		{
			AssistRow r = AssistOptions.All[i];
			if (SilksongOptions.IsGroup(r) || (r.Section == "Game Modes" && FocusTab == null) || Array.IndexOf(Tabs, r.Section) < 0)
			{
				continue;
			}
			if ((r.Label ?? "").ToLowerInvariant().Contains(q) || (r.Description ?? "").ToLowerInvariant().Contains(q) || (r.Section ?? "").ToLowerInvariant().Contains(q))
			{
				list.Add(i);
			}
		}
		return list;
	}

	private List<int> TabRows()
	{
		if (!string.IsNullOrEmpty(search))
		{
			return SearchRows();
		}
		List<int> list = new List<int>();
		for (int i = 0; i < AssistOptions.All.Length; i++)
		{
			if (AssistOptions.All[i].Section == Tabs[tab] && SilksongOptions.Visible(AssistOptions.All[i]))
			{
				list.Add(i);
			}
		}
		// rows with arrows together, then the switches and two-choice buttons (user 2026-10-17); a group header and
		// its own rows stay as one block, and buttons (Open) stay first
		List<int> sorted = new List<int>();
		int s = 0;
		while (s < list.Count)
		{
			string g = RunKey(list[s]);
			int e = s + 1;
			while (e < list.Count && RunKey(list[e]) == g)
			{
				e++;
			}
			List<int> run = list.GetRange(s, e - s);
			int lead = (g != null && SilksongOptions.IsGroup(AssistOptions.All[run[0]])) ? 1 : 0;
			List<int> body = run.GetRange(lead, run.Count - lead);
			List<int> ordered = new List<int>();
			ordered.AddRange(body.FindAll((int k) => SilksongOptions.IsButton(AssistOptions.All[k]) || IsKeyRow(AssistOptions.All[k])));
			ordered.AddRange(body.FindAll((int k) => !SilksongOptions.IsButton(AssistOptions.All[k]) && !IsKeyRow(AssistOptions.All[k]) && Values(AssistOptions.All[k]).Length != 2));
			ordered.AddRange(body.FindAll((int k) => !SilksongOptions.IsButton(AssistOptions.All[k]) && !IsKeyRow(AssistOptions.All[k]) && Values(AssistOptions.All[k]).Length == 2));
			if (lead == 1)
			{
				sorted.Add(run[0]);
			}
			sorted.AddRange(ordered);
			s = e;
		}
		return sorted;
	}

	// which block a row belongs to: its folding group (the header counts as its own group), or none
	private static string RunKey(int i)
	{
		AssistRow r = AssistOptions.All[i];
		return SilksongOptions.IsGroup(r) ? r.Id : SilksongOptions.GroupOf(r);
	}

	// Game Modes lives only behind the file select's Game Mode button (user 2026-10-17)
	private static bool Hidden(int t)
	{
		return Tabs[t] == "Game Modes" && FocusTab == null;
	}

	private void SwitchTab(int dir)
	{
		if (FocusTab != null)
		{
			return;
		}
		tabChangedAt = Time.unscaledTime;
		tab = (tab + dir + Tabs.Length) % Tabs.Length;
		for (int i = 0; i < Tabs.Length && (TabRows().Count == 0 || Hidden(tab)); i++)
		{
			tab = (tab + Math.Sign(dir == 0 ? 1 : dir) + Tabs.Length) % Tabs.Length;   // a page with nothing on it (Silksong's not loaded yet)
		}
		selected = TabRows()[0];
		menuScroll = 0f;
		flash = 0f;
		Sfx.Play(Sounds.MenuRolloverDown.Id);
	}

	private void TrackInputKind(Keyboard kb)
	{
		try
		{
			InControl.InputDevice activeDevice = InputManager.ActiveDevice;
			if (activeDevice != null && activeDevice != InControl.InputDevice.Null && (activeDevice.AnyButtonWasPressed || activeDevice.LeftStick.Vector.sqrMagnitude > 0.25f || activeDevice.DPad.Vector.sqrMagnitude > 0.25f))
			{
				padHints = true;
			}
		}
		catch (Exception)
		{
		}
		if (kb != null && kb.anyKey.wasPressedThisFrame)
		{
			padHints = false;
		}
		Mouse current = Mouse.current;
		if (current != null && current.leftButton.wasPressedThisFrame)
		{
			padHints = false;
		}
	}

	/// <summary>From the pause menu's Options: Settings over the paused game.</summary>
	/// <summary>The tab Settings opens on next time (null: the last one).</summary>
	public static string OpenAtTab;

	/// <summary>Opened for one page only (the file select's Skins and Game Mode): no other tabs (user 2026-10-17).</summary>
	public static string FocusTab;

	/// <summary>The file the focused page is for, shown under its title.</summary>
	public static int FocusSlot = -1;

	private static bool openedFromPause;

	public static void OpenFromPause()
	{
		if (!(Instance == null) && !Instance.open)
		{
			openedFromPause = true;
			Instance.Open(HeroController.UnsafeInstance);
		}
	}

	public static void OpenFromMenu()
	{
		if (!(Instance == null) && !Instance.open)
		{
			fromMenu = true;
			Instance.Open(null);
			if (!Instance.open)
			{
				fromMenu = false;   // refused (the new-file menu): don't leave it half open
			}
		}
	}

	private void Awake()
	{
		Instance = this;
	}

	private void Update()
	{
		if (InSave(out GameManager gmSlot))
		{
			UseSlot(gmSlot.profileID);
			if (ModeId == "sandbox" && !SandboxLocked)
			{
				LockSandbox();   // a Sandbox file from before the lock existed: in Sandbox for good once played
			}
		}
		Presets.Tick();
		if (pendingMark && InSave(out _))
		{
			pendingMark = false;
			if (!SaveMarked)
			{
				MarkSave();
			}
		}
		try
		{
			Tick();
		}
		catch (Exception ex)
		{
			if (!loggedError)
			{
				loggedError = true;
				Plugin.Log.LogError("Assist Mode: " + ex);
			}
		}
	}

	private void Tick()
	{
		if (EmoteMenu.IsOpen || Time.unscaledTime - EmoteMenu.ClosedAt < 0.2f)
		{
			return;   // the Emotes menu has the keys
		}
		HeroController silentInstance = HeroController.UnsafeInstance;
		GameManager silentInstance2 = GameManager.SilentInstance;
		bool num = Plugin.Ready && silentInstance != null && silentInstance2 != null && !silentInstance2.IsMenuScene() && silentInstance2.GameState != GameState.MAIN_MENU;
		if (fromMenu && (silentInstance2 == null || !silentInstance2.IsMenuScene()))
		{
			fromMenu = false;
		}
		if (!num && !fromMenu)
		{
			Close(silentInstance, sound: false);
			EndDashFreeze();
			SetSpeed(1f);
			openT = Math.Max(0f, openT - Time.unscaledDeltaTime / 0.25f);
			return;
		}
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		flash += unscaledDeltaTime;
		if (fromMenu)
		{
			Keyboard current = Keyboard.current;
			InputHandler instance = ManagerSingleton<InputHandler>.Instance;
			HeroActions a = ((instance != null) ? instance.inputActions : null);
			openT = Math.Min(1f, openT + unscaledDeltaTime / 0.25f);
			if (PromptTick(current, a))
			{
				return;
			}
			if (SearchKeys(current))
			{
				if (!searchAte)
				{
					Navigate(current, null);
				}
				return;
			}
			bool flag = false;
			try
			{
				InControl.InputDevice activeDevice = InputManager.ActiveDevice;
				if (activeDevice != null)
				{
					flag = activeDevice.Action2.WasPressed;
				}
			}
			catch (Exception)
			{
			}
			if (KeyPressed(AssistOptions.KeySettings) || Back(current, a) || flag)
			{
				Close(null, sound: true);
				fromMenu = false;
				return;
			}
			TrackInputKind(current);
			bool flag2 = false;
			bool flag3 = false;
			try
			{
				InControl.InputDevice activeDevice2 = InputManager.ActiveDevice;
				if (activeDevice2 != null)
				{
					flag2 = activeDevice2.LeftBumper.WasPressed;
					flag3 = activeDevice2.RightBumper.WasPressed;
				}
			}
			catch (Exception)
			{
			}
			if (flag2 || (current != null && current.qKey.wasPressedThisFrame))
			{
				SwitchTab(-1);
			}
			else if (flag3 || (current != null && current.eKey.wasPressedThisFrame))
			{
				SwitchTab(1);
			}
			else
			{
				Navigate(current, a);
			}
			if (Tabs[tab] == "Appearance")
			{
				PreviewClick();
			}
			return;
		}
		SetSpeed(GameSpeed);
		bool flag4 = silentInstance2.GameState == GameState.PLAYING && !silentInstance2.isPaused && !silentInstance.cState.transitioning && !silentInstance.cState.dead && !silentInstance.cState.hazardDeath;
		Keyboard current2 = Keyboard.current;
		InputHandler instance2 = ManagerSingleton<InputHandler>.Instance;
		HeroActions heroActions = ((instance2 != null) ? instance2.inputActions : null);
		tauntHeld = (((heroActions?.Taunt.IsPressed ?? false) && flag4) ? (tauntHeld + unscaledDeltaTime) : 0f);
		bool flag5 = KeyPressed(AssistOptions.KeySettings) || (tauntHeld >= 1f && !open);
		if (tauntHeld >= 1f)
		{
			tauntHeld = -999f;
		}
		if (open)
		{
			openT = Math.Min(1f, openT + unscaledDeltaTime / 0.25f);
			if (openedFromPause)
			{
				// opened from the pause menu's Settings: stays up while paused, closes if the game resumes
				if (!silentInstance2.isPaused)
				{
					Close(silentInstance, sound: false);
					return;
				}
			}
			else if (silentInstance2.isPaused || (!flag4 && !relinquished))
			{
				Close(silentInstance, sound: false);
				return;
			}
			if (PromptTick(current2, heroActions))
			{
				UpdateDashFreeze(unscaledDeltaTime, heroActions, flag4);
				return;
			}
			if (SearchKeys(current2))
			{
				if (!searchAte)
				{
					Navigate(current2, null);
				}
				UpdateDashFreeze(unscaledDeltaTime, heroActions, flag4);
				return;
			}
			if (flag5 || Back(current2, heroActions))
			{
				Close(silentInstance, sound: true);
				return;
			}
			TrackInputKind(current2);
			bool flag6 = false;
			bool flag7 = false;
			try
			{
				InControl.InputDevice activeDevice3 = InputManager.ActiveDevice;
				if (activeDevice3 != null)
				{
					flag6 = activeDevice3.LeftBumper.WasPressed;
					flag7 = activeDevice3.RightBumper.WasPressed;
				}
			}
			catch (Exception)
			{
			}
			if (flag6 || (current2 != null && current2.qKey.wasPressedThisFrame))
			{
				SwitchTab(-1);
			}
			else if (flag7 || (current2 != null && current2.eKey.wasPressedThisFrame))
			{
				SwitchTab(1);
			}
			else
			{
				Navigate(current2, heroActions);
			}
			if (Tabs[tab] == "Appearance")
			{
				PreviewClick();
			}
			bool flag8 = false;
			bool flag9 = false;
			try
			{
				InControl.InputDevice activeDevice4 = InputManager.ActiveDevice;
				if (activeDevice4 != null)
				{
					flag8 = activeDevice4.Action3.WasPressed;
					flag9 = activeDevice4.Action4.WasPressed;
				}
			}
			catch (Exception)
			{
			}
			if (Tabs[tab] == "Appearance" && ((current2?.tabKey.wasPressedThisFrame ?? false) || flag8))
			{
				previewPose = (previewPose + 1) % PreviewPoses.Length;
				Sfx.Play(Sounds.MenuRolloverDown.Id);
			}
			if (((current2?.rKey.wasPressedThisFrame ?? false) || flag9) && AssistOptions.All[selected].Section == "Appearance")
			{
				ResetLook();
			}
		}
		else
		{
			openT = Math.Max(0f, openT - unscaledDeltaTime / 0.25f);
			if (flag5 && flag4 && !silentInstance.controlReqlinquished)
			{
				Open(silentInstance);
			}
		}
		UpdateDashFreeze(unscaledDeltaTime, heroActions, flag4);
	}

	private void SetSpeed(float speed)
	{
		if (Math.Abs(speed - 1f) < 0.001f)
		{
			if (speedControl != null)
			{
				speedControl.Release();
				speedControl = null;
			}
		}
		else if (speedControl == null)
		{
			speedControl = TimeManager.CreateTimeControl(speed);
		}
		else if (Math.Abs(speedControl.TimeScale - speed) > 0.001f)
		{
			speedControl.TimeScale = speed;
		}
	}

	public void BeginDashFreeze()
	{
		if (DashAssist && !open)
		{
			dashFreezing = true;
			dashFreezeFor = 0f;
			if (dashFreeze == null)
			{
				dashFreeze = TimeManager.CreateTimeControl(0f, TimeManager.TimeControlInstance.Type.MinValue);
			}
		}
	}

	private void UpdateDashFreeze(float dt, HeroActions a, bool playing)
	{
		if (dashFreezing)
		{
			dashFreezeFor += dt;
			if (((a == null || !a.Attack.IsPressed) && !testHold) || !playing || dashFreezeFor >= 5f)
			{
				EndDashFreeze();
			}
		}
	}

	private void EndDashFreeze()
	{
		dashFreezing = false;
		if (dashFreeze != null)
		{
			dashFreeze.Release();
			dashFreeze = null;
		}
	}

	private void Open(HeroController h)
	{
		// user 2026-10-19: Begin in the new-file menu opened Customization while the game loaded. Nothing opens
		// Settings in the moments after it; the log says what tried, so the cause can be found
		if (MadelineTouches.DevOn)
		{
			Plugin.Log.LogInfo("Settings: opening (asked by " + new System.Diagnostics.StackTrace(1, false).ToString().Replace(Environment.NewLine, " | ") + ")");
		}
		if (StartingAbilities.IsOpen || Time.unscaledTime - StartingAbilities.ClosedAt < 0.5f || Time.unscaledTime - StartingAbilities.BeganAt < 3f)
		{
			Plugin.Log.LogInfo("Settings: not opened right after a new file began (asked by " + new System.Diagnostics.StackTrace(1, false).ToString().Replace(Environment.NewLine, " | ") + ")");
			FocusTab = null;
			OpenAtTab = null;
			return;
		}
		open = true;
		IsOpen = true;
		SilksongOptions.Refresh();   // unified settings: Silksong's Game, Audio and Video rows, read fresh
		tab = Mathf.Clamp(tab, 0, Tabs.Length - 1);
		if (OpenAtTab != null)
		{
			tab = Math.Max(0, Array.IndexOf(Tabs, OpenAtTab));
			OpenAtTab = null;
		}
		if (Hidden(tab))
		{
			tab = 0;
		}
		if (TabRows().Count == 0)
		{
			tab = Array.IndexOf(Tabs, "Assist Mode");
		}
		selected = TabRows()[0];
		menuScroll = 0f;
		try
		{
			InControl.InputDevice activeDevice = InputManager.ActiveDevice;
			padHints = activeDevice != null && activeDevice != InControl.InputDevice.Null && h != null && (Keyboard.current == null || !Keyboard.current.anyKey.isPressed);
		}
		catch (Exception)
		{
			padHints = false;
		}
		cursorWas = Cursor.visible;
		lockWas = Cursor.lockState;
		Cursor.visible = true;
		Cursor.lockState = CursorLockMode.None;
		flash = 0f;
		if (menuPause == null)
		{
			menuPause = TimeManager.CreateTimeControl(0f, TimeManager.TimeControlInstance.Type.MinValue);
		}
		if (h != null && !h.controlReqlinquished)
		{
			h.RelinquishControl();
			relinquished = true;
		}
		Sfx.Play(Sounds.MenuPause.Id);
	}

	public static float ClosedAt = -10f;

	private void Close(HeroController h, bool sound)
	{
		prompt = 0;
		FocusTab = null;
		search = null;
		if (open)
		{
			ClosedAt = Time.unscaledTime;
			openedFromPause = false;
			open = false;
			IsOpen = false;
			Cursor.visible = cursorWas;
			Cursor.lockState = lockWas;
			if (menuPause != null)
			{
				menuPause.Release();
				menuPause = null;
			}
			if (relinquished && h != null)
			{
				h.RegainControl();
			}
			relinquished = false;
			Plugin.Controller.ClearInput();
			if (sound)
			{
				Sfx.Play(Sounds.MenuUnpause.Id);
			}
			Save();
		}
	}

	private static bool Back(Keyboard kb, HeroActions a)
	{
		if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
		{
			return true;
		}
		if (a != null)
		{
			if (!a.Dash.WasPressed)
			{
				return a.MenuCancel.WasPressed;
			}
			return true;
		}
		return false;
	}

	private void Navigate(Keyboard kb, HeroActions a)
	{
		// number keys jump to a tab (1-9, 0 for the tenth); Delete resets the tab (user 2026-10-17)
		try
		{
			UnityEngine.InputSystem.Keyboard ik = UnityEngine.InputSystem.Keyboard.current;
			if (ik != null)
			{
				UnityEngine.InputSystem.Controls.KeyControl[] digits = new UnityEngine.InputSystem.Controls.KeyControl[10] { ik.digit1Key, ik.digit2Key, ik.digit3Key, ik.digit4Key, ik.digit5Key, ik.digit6Key, ik.digit7Key, ik.digit8Key, ik.digit9Key, ik.digit0Key };
				List<int> shown = new List<int>();
				for (int t = 0; t < Tabs.Length; t++)
				{
					if (!Hidden(t))
					{
						shown.Add(t);
					}
				}
				for (int d = 0; FocusTab == null && search == null && d < digits.Length && d < shown.Count; d++)
				{
					if (digits[d].wasPressedThisFrame && shown[d] != tab)
					{
						tabChangedAt = Time.unscaledTime;
						tab = shown[d];
						selected = TabRows()[0];
						menuScroll = 0f;
						flash = 0f;
						Sfx.Play(Sounds.MenuRolloverDown.Id);
						return;
					}
				}
			}
		}
		catch (Exception)
		{
		}
		if (kb != null && search == null && kb.deleteKey.wasPressedThisFrame && CanResetTab())
		{
			ResetTab();
			return;
		}
		List<int> list = TabRows();
		int count = list.Count;
		if (count == 0)
		{
			return;   // a search with no results
		}
		int num = Math.Max(0, list.IndexOf(selected));
		bool flag = (kb != null && (kb.upArrowKey.wasPressedThisFrame || (search == null && kb.wKey.wasPressedThisFrame))) || (a?.Up.WasPressed ?? false);
		bool flag2 = (kb != null && (kb.downArrowKey.wasPressedThisFrame || (search == null && kb.sKey.wasPressedThisFrame))) || (a?.Down.WasPressed ?? false);
		bool flag3 = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || (search == null && kb.aKey.wasPressedThisFrame))) || (a?.Left.WasPressed ?? false);
		bool flag4 = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || (search == null && kb.dKey.wasPressedThisFrame))) || (a?.Right.WasPressed ?? false);
		bool flag5 = (kb != null && (kb.enterKey.wasPressedThisFrame || (search == null && kb.spaceKey.wasPressedThisFrame))) || (a?.Jump.WasPressed ?? false);
		int num2 = 0;
		Mouse current = Mouse.current;
		if (current != null)
		{
			Vector2 point = MenuMouse.Gui();
			// one click is one step (user 2026-10-17: a click was read on several frames, so > looked dead on
			// three-choice rows); only the left button changes things
			bool rawClick = current.leftButton.wasPressedThisFrame;
			if (rawClick)
			{
				lastRawClick = Time.unscaledTime;
			}
			if (Time.unscaledTime - lastRawClick < 0.15f)
			{
				// a click is never a Left/Right/Enter press too, even a quick second click that's ignored below,
				// and the stray press can come a frame late (user 2026-10-17: double-clicking stepped left)
				flag3 = (flag4 = (flag5 = false));
			}
			bool wasPressedThisFrame = rawClick && Time.unscaledTime - lastClickAt > 0.2f;
			// volume bars: press on one and drag; the value follows the mouse
			if (!current.leftButton.isPressed)
			{
				dragging = -1;
			}
			else
			{
				if (dragging < 0 && rawClick)
				{
					foreach (KeyValuePair<int, Rect> br in barRects)
					{
						if (br.Value.Contains(point))
						{
							dragging = br.Key;
							selected = br.Key;
							break;
						}
					}
				}
				if (dragging >= 0 && barRects.TryGetValue(dragging, out var bar))
				{
					AssistRow dr = AssistOptions.All[dragging];
					int n = Values(dr).Length;
					float f = Mathf.Clamp01((point.x - (bar.x + 8f)) / Math.Max(1f, bar.width - 16f));
					SetRow(dr, Mathf.RoundToInt(f * (n - 1)));
					lastClickAt = Time.unscaledTime;
					return;
				}
			}
			if (wasPressedThisFrame)
			{
				lastClickAt = Time.unscaledTime;
			}
			bool wasPressedThisFrame2 = false;
			float y = current.scroll.ReadValue().y;
			if (y > 0.1f)
			{
				flag = true;
			}
			else if (y < -0.1f)
			{
				flag2 = true;
			}
			for (int i = 0; i < tabRects.Length; i++)
			{
				if (wasPressedThisFrame && tabRects[i].Contains(point))
				{
					if (i != tab)
					{
						SwitchTab(i - tab);
					}
					return;
				}
			}
			if (current.delta.ReadValue().sqrMagnitude > 0.5f || wasPressedThisFrame || wasPressedThisFrame2)
			{
				foreach (KeyValuePair<int, Rect> rowRect in rowRects)
				{
					if (rowRect.Value.Contains(point) && rowRect.Key != selected)
					{
						selected = rowRect.Key;
						flash = 0f;
						Sfx.Play(Sounds.MenuRolloverDown.Id);
						break;
					}
				}
			}
			// every row's own < > boxes count, not just the chosen row's (user 2026-10-17: the right arrow missed)
			if (wasPressedThisFrame && resetRect.Contains(point))
			{
				ResetTab();
				return;
			}
			bool hitArrow = false;
			if (wasPressedThisFrame)
			{
				foreach (KeyValuePair<int, Rect[]> ra in rowArrows)
				{
					int d = ra.Value[0].Contains(point) ? -1 : (ra.Value[1].Contains(point) ? 1 : 0);
					if (d != 0)
					{
						selected = ra.Key;
						num2 = d;
						hitArrow = true;
						break;
					}
				}
			}
			if (hitArrow)
			{
			}
			else if (wasPressedThisFrame && arrowL.Contains(point))
			{
				num2 = -1;
			}
			else if (wasPressedThisFrame && arrowR.Contains(point))
			{
				num2 = 1;
			}
			else if ((wasPressedThisFrame || wasPressedThisFrame2) && rowRects.ContainsKey(selected) && rowRects[selected].Contains(point))
			{
				num2 = (wasPressedThisFrame2 ? (-1) : 2);
			}
		}
		if (flag)
		{
			selected = list[(num + count - 1) % count];
			flash = 0f;
			Sfx.Play(Sounds.MenuRolloverUp.Id);
		}
		if (flag2)
		{
			selected = list[(num + 1) % count];
			flash = 0f;
			Sfx.Play(Sounds.MenuRolloverDown.Id);
		}
		AssistRow assistRow = AssistOptions.All[selected];
		if (num2 != 0)
		{
			// a mouse click decides alone: the click was also being read as a Left press (the log showed left and
			// right together on every > click, which cancelled out, user 2026-10-17)
			flag3 = (flag4 = (flag5 = false));
		}
		switch (num2)
		{
		case -1:
			flag3 = true;
			break;
		case 1:
			flag4 = true;
			break;
		case 2:
			flag5 = true;
			break;
		}
		if (Locked(assistRow))
		{
			if (flag3 || flag4 || flag5)
			{
				OpenPrompt();
			}
			return;
		}
		if (SilksongOptions.IsGroup(assistRow))
		{
			if (assistRow.Id == "grp_difficulty")
			{
				return;   // a divider, not a fold
			}
			if (flag3 || flag4 || flag5)
			{
				SilksongOptions.Toggle(assistRow);
				Sfx.Play(SilksongOptions.Expanded.Contains(assistRow.Id) ? Sounds.MenuToggleOn.Id : Sounds.MenuToggleOff.Id);
			}
			return;
		}
		if (SilksongOptions.IsButton(assistRow) && assistRow.Id.StartsWith("preset_", StringComparison.Ordinal))
		{
			if (flag3 || flag4 || flag5)
			{
				string pid = assistRow.Id.Substring(7, assistRow.Id.Length - 11);
				if (SandboxLocked && pid != "sandbox")
				{
					Sfx.Play(Sounds.MenuToggleOff.Id);   // in Sandbox for good: delete the file to play it another way
					return;
				}
				if (pid == "sandbox" && Presets.Current != "sandbox")
				{
					// a disclaimer first: Sandbox's unlocks can't be taken back (user 2026-10-17)
					prompt = 3;
					promptYes = false;
					promptT = 0f;
					flash = 0f;
					Sfx.Play(Sounds.MenuPause.Id);
					return;
				}
				Presets.Apply(pid);
				SilksongOptions.Refresh();
			}
			return;
		}
		if (SilksongOptions.IsButton(assistRow))
		{
			if ((flag3 || flag4 || flag5) && assistRow.Id == "techroom_btn")
			{
				// the Tech Tutorial: Settings closes, then the wipe (in a game only)
				if (HeroController.UnsafeInstance != null)
				{
					Sfx.Play(Sounds.MenuButtonSelect.Id);
					Close(HeroController.UnsafeInstance, sound: false);
					TechRoom.Request();
				}
				return;
			}
			if ((flag3 || flag4 || flag5) && assistRow.Id == "emotes_btn")
			{
				EmoteMenu.Open();   // the Emotes menu opens over Settings (user 2026-10-19)
				return;
			}
			if (flag3 || flag4 || flag5)
			{
				Sfx.Play(Sounds.MenuButtonSelect.Id);
				SilksongOptions.OpenBindings(assistRow, delegate
				{
					// gone at once, title screen too, so Silksong's page shows (user 2026-10-17: it stayed up)
					Close(HeroController.UnsafeInstance, sound: false);
					fromMenu = false;
					openT = 0f;
				});
			}
			return;
		}
		int num3 = Values(assistRow).Length;
		int num4 = Index(assistRow);
		int num5 = num4;
		if (IsKeyRow(assistRow))
		{
			int num6 = (flag3 ? (-1) : ((flag4 || flag5) ? 1 : 0));
			if (num6 != 0)
			{
				for (int j = num4 + num6; j >= 0 && j < num3; j += num6)
				{
					if (!KeyTaken(assistRow, j))
					{
						num4 = j;
						break;
					}
				}
			}
		}
		else
		{
			if (num3 == 2 && (flag3 || flag4 || flag5))
			{
				num4 = 1 - num4;   // a two-choice button: any press flips it
			}
			else
			{
			if (flag3)
				{
					num4 = (num4 + num3 - 1) % num3;   // both ways wrap round
				}
				if (flag4 || flag5)
				{
					num4 = (num4 + 1) % num3;
				}
			}
		}
		if (num4 != num5)
		{
			if (SilksongOptions.Is(assistRow))
			{
				SilksongOptions.Set(assistRow, num4);   // Silksong's own option: it saves and applies itself
			}
			else
			{
				chosen[Key(assistRow)] = num4;
			}
			string text = Values(assistRow)[num4];
			Sfx.Play((text == "Off") ? Sounds.MenuToggleOff.Id : ((text == "On") ? Sounds.MenuToggleOn.Id : Sounds.MenuButtonSelect.Id));
		}
	}

	private void PreviewClick()
	{
		Mouse current = Mouse.current;
		if (current == null || !current.leftButton.wasPressedThisFrame)
		{
			return;
		}
		float num = (float)Screen.height / 1080f;
		Vector2 vector = current.position.ReadValue();
		Vector2 point = new Vector2(vector.x / num, ((float)Screen.height - vector.y) / num);
		if (previewBody.Contains(point))
		{
			previewPose = (previewPose + 1) % PreviewPoses.Length;
			Sfx.Play(Sounds.MenuRolloverDown.Id);
			return;
		}
		int num2 = (arrowLeft.Contains(point) ? (-1) : (arrowRight.Contains(point) ? 1 : 0));
		if (num2 != 0)
		{
			AssistRow r = AssistOptions.All[selected];
			if (r.Section != "Appearance")
			{
				selected = Array.IndexOf(AssistOptions.All, AssistOptions.Skin);
				r = AssistOptions.Skin;
			}
			int num3 = Values(r).Length;
			chosen[Key(r)] = (Index(r) + num2 + num3) % num3;
			Sfx.Play(Sounds.MenuButtonSelect.Id);
		}
	}

	private static void DrawSprite(Sprite sp, Vector2 pivotAt, float scale, Color c)
	{
		if (!(sp == null))
		{
			Rect textureRect = sp.textureRect;
			Texture texture = sp.texture;
			Vector2 pivot = sp.pivot;
			Rect position = new Rect(pivotAt.x - pivot.x * scale, pivotAt.y - (textureRect.height - pivot.y) * scale, textureRect.width * scale, textureRect.height * scale);
			GUI.color = c;
			GUI.DrawTextureWithTexCoords(position, texture, new Rect(textureRect.x / (float)texture.width, textureRect.y / (float)texture.height, textureRect.width / (float)texture.width, textureRect.height / (float)texture.height));
		}
	}

	private static Color HexColor(string hex)
	{
		if (!ColorUtility.TryParseHtmlString("#" + hex, out var color))
		{
			return Color.white;
		}
		return color;
	}

	/// <summary>The Emotes menu's Madeline: the preview, playing this animation.</summary>
	public static string PreviewAnimOverride;

	public static void DrawEmotePreview(string anim, float alpha, float x0, float x1)
	{
		if (Instance == null)
		{
			return;
		}
		PreviewAnimOverride = anim;
		try
		{
			Instance.DrawPreview(alpha, x0, x1);
		}
		finally
		{
			PreviewAnimOverride = null;
		}
	}

	private void DrawPreview(float alpha, float x0, float x1)
	{
		if (Plugin.PreviewAssets == null)
		{
			return;
		}
		SkinRow arg = Array.Find(Skins.All, (SkinRow k) => k.Name == SkinName);
		if (arg.Id == null)
		{
			return;
		}
		CelesteAssets celesteAssets;
		try
		{
			celesteAssets = Plugin.PreviewAssets(arg);
		}
		catch
		{
			return;
		}
		if (celesteAssets == null || ((!celesteAssets.Anims.TryGetValue(PreviewAnimOverride ?? PreviewPoses[previewPose], out var value) || value.Frames.Count == 0) && (!celesteAssets.Anims.TryGetValue(Animations.Idle.CelesteAnim, out value) || value.Frames.Count == 0)))
		{
			return;
		}
		bool lowDetail = LowDetailMenus;
		CelesteFrame celesteFrame = value.Frames[lowDetail ? 0 : (int)(Time.unscaledTime / Math.Max(0.05f, value.Delay)) % value.Frames.Count];
		float num = 7f;
		float num2 = (x0 + x1) / 2f;
		Vector2 vector = new Vector2(num2, 540f + 16f * num);
		float num3 = 30f * num;
		float num4 = 40f * num;
		GUI.color = new Color(1f, 1f, 1f, 0.06f * alpha);
		GUI.DrawTexture(new Rect(num2 - num3 / 2f, vector.y - num4 + 4f * num, num3, num4), Texture2D.whiteTexture);
		bool flag = MadelineView.Masked(arg.Style);
		bool flag2 = arg.Style == "cat" || (CatEars && arg.Style == "celeste" && arg.CelesteSprite == "player");
		Color c = HexColor(HairHex(AssistOptions.HairOne, arg.HairNormal));
		c.a = alpha;
		Color color = new Color(0f, 0f, 0f, alpha);
		Color c2 = new Color(1f, 1f, 1f, alpha);
		Color c3 = HexColor(arg.BodyColor);
		c3.a *= alpha;
		int hairLength = HairLength;
		float hairSize = HairSize;
		Vector2[] array = previewHairNodes;
		array[0] = vector + new Vector2(celesteFrame.HairOffset.x, -9f + celesteFrame.HairOffset.y) * num;
		for (int num5 = 1; num5 < hairLength; num5++)
		{
			array[num5] = array[num5 - 1] + new Vector2(-0.5f * ((num5 == 1) ? 2f : 1f) + Mathf.Sin(Time.unscaledTime * 2f + (float)num5) * 0.15f, 2f) * num;
		}
		if (flag2 && !lowDetail)
		{
			Vector2 vector2 = vector + new Vector2(-3f, -3.5f) * num;
			for (int num6 = 5; num6 >= 0; num6--)
			{
				float num7 = (float)num6 / 5f;
				float num8 = Mathf.Sin(Time.unscaledTime * 3f - (float)num6 * 0.7f) * num7 * 2.2f;
				Vector2 pivotAt = vector2 + new Vector2(0f - ((float)num6 * 1.3f + num8 * 0.3f), 0f - ((float)(num6 * num6) * 0.22f + num8 * 0.7f)) * num;
				float scale = num * 0.6f * (1f - num7 * 0.3f);
				DrawSprite(MadelineView.TailOutlineSprite, pivotAt, scale, c2);
				DrawSprite(MadelineView.TailSprite, pivotAt, scale, c);
			}
		}
		if (!lowDetail && celesteFrame.HasHair && !flag && celesteAssets.HairNode != null && celesteAssets.Bangs.Count > 0)
		{
			for (int num9 = 0; num9 < 2; num9++)
			{
				for (int num10 = hairLength - 1; num10 >= 0; num10--)
				{
					Sprite sp = ((num10 == 0) ? celesteAssets.Bangs[Mathf.Clamp(celesteFrame.HairFrame, 0, celesteAssets.Bangs.Count - 1)] : celesteAssets.HairNode);
					float scale2 = num * ((num10 == 0) ? 1f : ((0.25f + (1f - (float)num10 / (float)hairLength) * 0.75f) * hairSize));
					if (num9 == 0)
					{
						DrawSprite(sp, array[num10] + Vector2.left * num, scale2, color);
						DrawSprite(sp, array[num10] + Vector2.right * num, scale2, color);
						DrawSprite(sp, array[num10] + Vector2.up * num, scale2, color);
						DrawSprite(sp, array[num10] + Vector2.down * num, scale2, color);
					}
					else
					{
						DrawSprite(sp, array[num10], scale2, HairNodeColor(c, num10, hairLength, hasDash: true));
					}
				}
			}
		}
		DrawSprite(celesteFrame.Sprite, vector, num, c3);
		previewBody = new Rect(num2 - 8f * num, vector.y - 24f * num, 16f * num, 24f * num);
		if (flag2)
		{
			Vector2 pivotAt2 = array[0] + new Vector2(-0.5f, -2f) * num;
			DrawSprite(MadelineView.EarSprite, pivotAt2, num, c);
			DrawSprite(MadelineView.EarInnerSprite, pivotAt2, num, c2);
		}
		if (flag)
		{
			DrawSprite(MadelineView.MaskFor(arg.Style), array[0] + new Vector2(0.5f, -2f + ((arg.Style == "hornet") ? 8f : 0f)) * num, num, c2);
		}
		// the headwear and the companion in the preview too (user 2026-10-19); she faces right here
		if ((arg.Style == "celeste" || arg.Style == "cat") && celesteFrame.HasHair)
		{
			Sprite hs = MadelineView.Hat(Value(AssistOptions.Headwear), out Vector2 hat);
			if (hs != null)
			{
				DrawSprite(hs, array[0] + new Vector2(0f - hat.x, 0f - hat.y) * num, num, c2);
			}
		}
		string mate = Value(AssistOptions.Companion);
		if (mate == "Bird" && celesteAssets.BirdCrow.Count > 26)
		{
			// the bird stands beside her, pecking now and then
			float pt = Time.unscaledTime % 4f;
			int pf = (pt > 3f) ? Mathf.Min(26, 14 + (int)((pt - 3f) / 0.08f)) : 0;
			DrawSprite(celesteAssets.BirdCrow[pf], vector + new Vector2(-14f, 0f) * num, num * 0.8f, c2);   // it faces right, towards her
		}
		else if (mate != "None")
		{
			Sprite cf = MadelineView.CompanionFrame(mate, Time.unscaledTime);
			if (cf != null)
			{
				float lift = (mate == "Jellyfish") ? 24f : ((mate == "Winged Berry") ? 22f : 20f);
				DrawSprite(cf, vector + new Vector2(-12f, 0f - lift - Mathf.Sin(Time.unscaledTime * 3f) * 1.5f) * num, num * ((mate == "Jellyfish") ? 0.6f : 0.85f), c2);
			}
		}
		if (PreviewAnimOverride != null)
		{
			GUI.color = Color.white;   // the Emotes menu: just her, no arrows or labels
			return;
		}
		float num11 = vector.y - 12f * num;
		float num12 = 70f;
		arrowLeft = new Rect(num2 - num3 / 2f - num12, num11 - num12 / 2f, num12, num12);
		arrowRight = new Rect(num2 + num3 / 2f, num11 - num12 / 2f, num12, num12);
		float num13 = lowDetail ? 1f : 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
		Art.DrawOutlined("<", arrowLeft.center.x - Art.Measure("<") * 1.4f / 2f, arrowLeft.center.y - (float)Art.LineHeight * 1.4f / 2f, 1.4f, new Color(1f, 1f, 1f, alpha * num13), color, 3f);
		Art.DrawOutlined(">", arrowRight.center.x - Art.Measure(">") * 1.4f / 2f, arrowRight.center.y - (float)Art.LineHeight * 1.4f / 2f, 1.4f, new Color(1f, 1f, 1f, alpha * num13), color, 3f);
		AssistRow r = AssistOptions.All[selected];
		if (r.Section != "Appearance")
		{
			r = AssistOptions.Skin;
		}
		string line = r.Label + ": " + Value(r);
		string line2 = "Look " + Value(AssistOptions.Look);
		float num14 = Mathf.Min(0.7f, (x1 - x0 - 20f) / Math.Max(1f, Art.Measure(line)));
		Art.DrawOutlined(line2, num2 - Art.Measure(line2) * 0.9f / 2f, vector.y - num4 + 4f * num - (float)Art.LineHeight * 0.9f - 6f, 0.9f, c2, color, 2f);
		Art.DrawOutlined(line, num2 - Art.Measure(line) * num14 / 2f, vector.y + 4f * num + 10f, num14, new Color(0.85f, 0.85f, 0.85f, alpha), color, 2f);
		string[] obj2 = ((!padHints) ? new string[2]
		{
			PoseNames[previewPose] + " - Tab for a pose",
			"R resets this look"
		} : new string[2]
		{
			PoseNames[previewPose] + " - Square for a pose",
			"Triangle resets this look"
		});
		float num15 = vector.y + 4f * num + 10f + (float)Art.LineHeight * num14 + 4f;
		float num16 = x1 - x0 - 20f;
		string[] array2 = obj2;
		foreach (string line3 in array2)
		{
			float num18 = Mathf.Min(0.42f, num16 / Math.Max(1f, Art.Measure(line3)));
			Art.DrawOutlined(line3, num2 - Art.Measure(line3) * num18 / 2f, num15, num18, new Color(0.7f, 0.7f, 0.7f, alpha), color, 2f);
			num15 += (float)Art.LineHeight * num18 + 2f;
		}
		GUI.color = Color.white;
	}

	private void OnGUI()
	{
		if (TheoCamera.Hiding || Event.current.type != EventType.Repaint || Art == null)
		{
			return;
		}
		float num = (float)Screen.height / 1080f;
		float num2 = (float)Screen.width / num;
		if (dashFreezing)
		{
			DrawDashArrow(num);
		}
		if (openT <= 0f)
		{
			return;
		}
		bool lowDetail = LowDetailMenus;
		float num3 = lowDetail ? 1f : 1f - (1f - openT) * (1f - openT) * (1f - openT);
		float num4 = num3;
		Matrix4x4 matrix = GUI.matrix;
		GUI.depth = -2;
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num, num, 1f));
		GUI.color = lowDetail ? new Color(0.035f, 0.055f, 0.14f, 1f) : new Color(0f, 0f, 0f, ((FocusTab != null) ? 0.9f : 0.75f) * num4);
		GUI.DrawTexture(new Rect(0f, 0f, num2, 1080f), Texture2D.whiteTexture);
		// ---- the settings screen, redesigned (user 2026-10-17: "complete creative freedom") ----------------------
		// a tab sidebar on the left in each tab's own colour, the options on a dark card, a description strip under them
		float slide = (1f - num3) * 40f;
		Color ink = new Color(0f, 0f, 0f, num4);
		Color tabCol = Hex(TabColor(tab));
		float side0 = 70f - slide;
		float sideW = 330f;
		float top = 96f;
		GuiArt.Img img = Art.Image("assistmode");
		if (img != null)
		{
			GUI.color = new Color(1f, 1f, 1f, num4);
			GUI.DrawTexture(new Rect(side0, top - 8f, 64f, 64f), img.Tex);
		}
		float ty = top + (float)Art.LineHeight * 0.85f + 40f;
		float tabH = 74f;
		Vector2 mouse = MenuMouse.Gui();
		if (FocusTab != null)
		{
			// one page only: its name big in the tab's colour, which file it's for, and how to get back
			string ft = FocusTab == "Appearance" ? "Skins" : "Game Mode";
			Art.DrawOutlined(ft, side0 + 76f, top - 4f, 0.95f, new Color(1f, 1f, 1f, num4), ink, 3f);
			GUI.color = new Color(tabCol.r, tabCol.g, tabCol.b, num4);
			GUI.DrawTexture(new Rect(side0, ty - 14f, sideW, 4f), Texture2D.whiteTexture);
			float fy = ty + 6f;
			if (FocusSlot >= 0 && FocusTab != "Appearance")
			{
				Art.DrawOutlined("File " + FocusSlot, side0 + 6f, fy, 0.6f, new Color(1f, 1f, 1f, num4), ink, 2f);
				fy += (float)Art.LineHeight * 0.6f + 10f;
			}
			string[] notes = FocusTab == "Appearance" ? new string[2] { "How Madeline looks.", "Shared by every file." } : new string[3] { "How this file plays.", "Each file keeps", "its own mode." };
			foreach (string note in notes)
			{
				Art.DrawOutlined(note, side0 + 6f, fy, 0.4f, new Color(0.78f, 0.78f, 0.84f, num4), ink, 2f);
				fy += (float)Art.LineHeight * 0.4f + 4f;
			}
			for (int j = 0; j < tabRects.Length; j++)
			{
				tabRects[j] = default(Rect);
			}
		}
		else
		{
		CelesteMenus.HiFont.Draw(Art, "SETTINGS", side0 + 76f, top, 0.85f, new Color(1f, 1f, 1f, num4), ink, 3f);
		for (int j = 0; j < Tabs.Length; j++)
		{
			if (Hidden(j))
			{
				tabRects[j] = default(Rect);
				continue;
			}
			bool on = j == tab;
			Color tc = Hex(TabColor(j));
			Rect r = new Rect(side0, ty, sideW, tabH - 10f);
			tabRects[j] = r;
			bool hover = !on && !padHints && r.Contains(mouse);
			if (hover && hoverTab != j)
			{
				Sfx.Play(Sounds.MenuRolloverDown.Id, 0.5f);   // a soft tick as the mouse comes onto a tab
			}
			if (hover)
			{
				hoverTab = j;
			}
			else if (hoverTab == j)
			{
				hoverTab = -1;
			}
			float push = on ? 18f : (hover ? 8f : 0f);
			GUI.color = new Color(tc.r, tc.g, tc.b, (on ? 0.95f : (hover ? 0.35f : 0.18f)) * num4);
			GUI.DrawTexture(new Rect(r.x + push, r.y, r.width - push, r.height), lowDetail ? Texture2D.whiteTexture : CelesteMenus.MenuFx.FadeOut);
			GUI.color = new Color(tc.r, tc.g, tc.b, num4);
			GUI.DrawTexture(new Rect(r.x + push, r.y, 6f, r.height), Texture2D.whiteTexture);
			float ls = 0.55f;
			Color lc = on ? new Color(1f, 1f, 1f, num4) : new Color(0.78f, 0.78f, 0.82f, num4);
			Art.DrawOutlined(TabNames[j], r.x + push + 26f, r.y + (r.height - (float)Art.LineHeight * ls) / 2f, ls, lc, ink, 2f);
			int count = 0;
			for (int k = 0; k < AssistOptions.All.Length; k++)
			{
				if (AssistOptions.All[k].Section == Tabs[j] && !IsKeyRow(AssistOptions.All[k]) && !SilksongOptions.IsGroup(AssistOptions.All[k]) && Value(AssistOptions.All[k]) != AssistOptions.All[k].Default) count++;
			}
			if (count > 0)
			{
				string cnt = count.ToString();
				float cw = Art.Measure(cnt) * 0.4f + 22f;
				Rect cb = new Rect(r.xMax - cw - 14f, r.y + (r.height - 30f) / 2f, cw, 30f);
				GUI.color = new Color(0f, 0f, 0f, 0.45f * num4);
				GUI.DrawTexture(cb, Texture2D.whiteTexture);
				Art.DrawLine(cnt, cb.x + 11f, cb.y + 2f, 0.4f, new Color(1f, 1f, 1f, num4));
			}
			ty += tabH;
		}
		string pages = padHints ? "L1 / R1  pages" : "Q / E  pages";
		Art.DrawOutlined(pages, side0 + 6f, ty + 10f, 0.38f, new Color(0.65f, 0.65f, 0.7f, num4), ink, 2f);
		}
		// the card
		bool look = Tabs[tab] == "Appearance";
		float p0 = side0 + sideW + 50f;
		float p1 = look ? Math.Max(p0 + 760f, num2 - 560f) : Math.Min(num2 - 90f, p0 + 1180f);
		Rect card = new Rect(p0, top - 10f, p1 - p0, 900f);
		if (selected >= 0 && selected < AssistOptions.All.Length && (AssistOptions.All[selected].Id == AssistOptions.TitleSky.Id || AssistOptions.All[selected].Id == AssistOptions.TitleArea.Id))
		{
			// a live preview of the title's mountain while you pick (user 2026-10-17); beside the card if there's room,
			// otherwise over its top-right corner
			float pw = Math.Max(240f, Math.Min(480f, num2 - p1 - 40f));
			float ph = pw * 9f / 16f;
			Rect pr = (num2 - p1 - 40f >= 240f) ? new Rect(p1 + 20f, card.y + 70f, pw, ph) : new Rect(card.xMax - pw - 30f, card.y + 70f, pw, ph);
			GUI.color = new Color(0f, 0f, 0f, 0.5f * num4);
			GUI.DrawTexture(new Rect(pr.x - 6f, pr.y - 6f, pr.width + 12f, pr.height + 12f), Texture2D.whiteTexture);
			CelesteFileSelect.SkyPreview(pr, num4);
			Art.DrawOutlined("Title preview", pr.x, pr.yMax + 10f, 0.4f, new Color(0.85f, 0.85f, 0.9f, num4), ink, 2f);
		}
		GUI.color = new Color(0.05f, 0.045f, 0.09f, 0.88f * num4);
		GUI.DrawTexture(card, Texture2D.whiteTexture);
		GUI.color = new Color(tabCol.r, tabCol.g, tabCol.b, num4);
		GUI.DrawTexture(new Rect(card.x, card.y, card.width, 6f), Texture2D.whiteTexture);
		GUI.color = new Color(tabCol.r, tabCol.g, tabCol.b, 0.18f * num4);
		GUI.DrawTexture(new Rect(card.x, card.y + 6f, card.width, 90f), lowDetail ? Texture2D.whiteTexture : CelesteMenus.MenuFx.FadeOut);
		List<int> list = TabRows();
		string sub = list.Count + (list.Count == 1 ? " option" : " options");
		if (FocusTab != null)
		{
			string fh = FocusTab == "Appearance" ? "Choose Madeline's look" : "Choose how this file plays";
			Art.DrawOutlined(fh, card.x + 40f, card.y + 30f, 0.62f, new Color(1f, 1f, 1f, num4), ink, 3f);
			sub = (FocusSlot >= 0 && FocusTab != "Appearance") ? ("File " + FocusSlot) : "";   // looks are shared
		}
		else if (search != null)
		{
			// the search box in place of the tab's name
			string sq = "Search:  " + search + ((Time.unscaledTime % 1f < 0.5f) ? "_" : " ");
			GUI.color = new Color(1f, 1f, 1f, 0.08f * num4);
			GUI.DrawTexture(new Rect(card.x + 30f, card.y + 20f, Math.Min(card.width * 0.6f, Art.Measure(sq) * 0.62f + 40f), (float)Art.LineHeight * 0.62f + 16f), Texture2D.whiteTexture);
			Art.DrawOutlined(sq, card.x + 46f, card.y + 28f, 0.62f, new Color(1f, 1f, 1f, num4), ink, 3f);
			sub = (search.Length == 0) ? "type to search" : ((list.Count == 0) ? "nothing matches" : (list.Count + (list.Count == 1 ? " match" : " matches")));
		}
		else
		{
			string headTxt = (Tabs[tab] == "Appearance" ? "Customization" : Tabs[tab]).ToUpperInvariant();
			CelesteMenus.HiFont.Draw(Art, headTxt, card.x + 40f, card.y + 26f, 0.75f, new Color(1f, 1f, 1f, num4), ink, 3f);
		}
		float subX = card.xMax - 40f - Art.Measure(sub) * 0.42f;
		Art.DrawOutlined(sub, subX, card.y + 40f, 0.42f, new Color(0.7f, 0.7f, 0.75f, num4), ink, 2f);
		resetRect = default(Rect);
		if (CanResetTab())
		{
			// reset just this tab (user 2026-10-17)
			string rl = "Reset tab";
			float rw = Art.Measure(rl) * 0.4f + 30f;
			resetRect = new Rect(subX - 24f - rw, card.y + 34f, rw, 36f);
			bool hot = !padHints && resetRect.Contains(mouse);
			GUI.color = hot ? new Color(tabCol.r, tabCol.g, tabCol.b, num4) : new Color(1f, 1f, 1f, 0.1f * num4);
			GUI.DrawTexture(resetRect, Texture2D.whiteTexture);
			Art.DrawOutlined(rl, resetRect.x + 15f, resetRect.y + (resetRect.height - (float)Art.LineHeight * 0.4f) / 2f, 0.4f, new Color(1f, 1f, 1f, num4), ink, 2f);
		}
		float num9 = (float)Art.LineHeight * 0.55f + 22f;
		float rowL = card.x + 40f;
		float rowR = card.xMax - 40f;
		float num18 = card.y + 120f;
		float num19 = card.yMax - 150f - num18;
		Dictionary<int, float> offs = new Dictionary<int, float>();
		float num17 = 0f;
		foreach (int item in list)
		{
			offs[item] = num17;
			num17 += num9;
		}
		float selOff = offs.ContainsKey(selected) ? offs[selected] : 0f;
		float value = Mathf.Clamp(menuScroll, selOff + num9 - num19, selOff);
		menuScroll = Mathf.Clamp(value, 0f, Math.Max(0f, num17 - num19));
		rowRects.Clear();
		rowArrows.Clear();
		arrowL = (arrowR = default(Rect));
		Color hiCol = lowDetail ? Hex("B9D6FF") : Color.Lerp(Color.white, tabCol, 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f));
		barRects.Clear();
		// switching tabs: the rows slide in a little from the right (user 2026-10-17)
		float tabIn = lowDetail ? 1f : Mathf.Clamp01((Time.unscaledTime - tabChangedAt) / 0.2f);
		tabIn = 1f - (1f - tabIn) * (1f - tabIn);
		Matrix4x4 rowsMatrix = GUI.matrix;
		GUI.matrix = rowsMatrix * Matrix4x4.Translate(new Vector3((1f - tabIn) * 36f, 0f, 0f));
		foreach (int item2 in list)
		{
			AssistRow row = AssistOptions.All[item2];
			float y = num18 + offs[item2] - menuScroll;
			if (y < num18 - 1f || y + num9 > num18 + num19 + 1f)
			{
				continue;
			}
			bool sel = item2 == selected;
			Rect rr = new Rect(rowL - 16f, y, rowR - rowL + 32f, num9 - 6f);
			rowRects[item2] = rr;
			// a dot beside anything changed from its default; a group shows one if anything inside it is changed
			bool changedRow = SilksongOptions.IsGroup(row) ? GroupChanged(row) : (!SilksongOptions.IsButton(row) && !SilksongOptions.Is(row) && Values(row).Length > 1 && Value(row) != row.Default);
			if (changedRow)
			{
				GUI.color = new Color(tabCol.r, tabCol.g, tabCol.b, num4);
				GUI.DrawTexture(new Rect(rowL - 34f, y + (rr.height - 10f) / 2f, 10f, 10f), CelesteMenus.MenuFx.Glow);
				GUI.DrawTexture(new Rect(rowL - 32f, y + (rr.height - 6f) / 2f, 6f, 6f), Texture2D.whiteTexture);
			}
			if (row.Id == "grp_difficulty")
			{
				// not a fold: the line between the game modes and the difficulty (user 2026-10-17)
				GUI.color = new Color(tabCol.r, tabCol.g, tabCol.b, 0.7f * num4);
				GUI.DrawTexture(new Rect(rr.x, y + rr.height / 2f, rr.width, 2f), Texture2D.whiteTexture);
				string dn = "difficulty mixes with any mode";
				float dsz = 0.4f;
				GUI.color = new Color(0.07f, 0.08f, 0.14f, num4);
				GUI.DrawTexture(new Rect(rowR - Art.Measure(dn) * dsz - 14f, y + 4f, Art.Measure(dn) * dsz + 14f, rr.height - 8f), Texture2D.whiteTexture);
				Art.DrawOutlined(dn, rowR - Art.Measure(dn) * dsz, y + (rr.height - (float)Art.LineHeight * dsz) / 2f, dsz, new Color(0.8f, 0.8f, 0.88f, num4), ink, 2f);
				continue;
			}
			if (SilksongOptions.IsGroup(row))
			{
				// a folding group: its own look, a tinted band, a +/- box and the count, so it can't be mistaken for an option
				bool openG = SilksongOptions.Expanded.Contains(row.Id);
				GUI.color = new Color(tabCol.r, tabCol.g, tabCol.b, (sel ? 0.42f : 0.2f) * num4);
				GUI.DrawTexture(rr, Texture2D.whiteTexture);
				GUI.color = new Color(tabCol.r, tabCol.g, tabCol.b, num4);
				GUI.DrawTexture(new Rect(rr.x, rr.yMax - 3f, rr.width, 3f), Texture2D.whiteTexture);
				Rect box = new Rect(rowL + 4f, y + (rr.height - 34f) / 2f, 34f, 34f);
				GUI.DrawTexture(box, Texture2D.whiteTexture);
				string pm = openG ? "-" : "+";
				Art.DrawLine(pm, box.center.x - Art.Measure(pm) * 0.6f / 2f, box.center.y - (float)Art.LineHeight * 0.6f / 2f - 2f, 0.6f, new Color(0.05f, 0.05f, 0.08f, num4));
				float gs = 0.55f;
				Art.DrawOutlined(row.Label, box.xMax + 18f, y + (rr.height - (float)Art.LineHeight * gs) / 2f, gs, new Color(1f, 1f, 1f, num4), ink, 2f);
				string cntTxt = row.Values.Split(',')[0].Replace("Show ", "") + " options";
				Art.DrawOutlined(cntTxt, rowR - Art.Measure(cntTxt) * 0.42f, y + (rr.height - (float)Art.LineHeight * 0.42f) / 2f, 0.42f, new Color(0.85f, 0.85f, 0.9f, num4), ink, 2f);
				continue;
			}
			if (sel)
			{
				GUI.color = new Color(tabCol.r, tabCol.g, tabCol.b, 0.22f * num4);
				GUI.DrawTexture(rr, lowDetail ? Texture2D.whiteTexture : CelesteMenus.MenuFx.FadeOut);
				GUI.color = new Color(tabCol.r, tabCol.g, tabCol.b, num4);
				GUI.DrawTexture(new Rect(rr.x, rr.y, 5f, rr.height), Texture2D.whiteTexture);
			}
			else if (!padHints && rr.Contains(mouse))
			{
				GUI.color = new Color(1f, 1f, 1f, 0.05f * num4);
				GUI.DrawTexture(rr, Texture2D.whiteTexture);
			}
			float ts = 0.55f;
			float textY = y + (rr.height - (float)Art.LineHeight * ts) / 2f;
			Color lab = sel ? hiCol : Color.white;
			lab.a = num4;
			float indent = SilksongOptions.GroupOf(row) != null ? 34f : 0f;   // rows inside an open group sit in a little
			if (indent > 0f)
			{
				GUI.color = new Color(tabCol.r, tabCol.g, tabCol.b, 0.5f * num4);
				GUI.DrawTexture(new Rect(rowL + 12f, rr.y + 4f, 3f, rr.height - 8f), Texture2D.whiteTexture);
			}
			Art.DrawOutlined(row.Label, rowL + 10f + indent, textY, Mathf.Min(ts, (rowR - rowL) * 0.5f / Math.Max(1f, Art.Measure(row.Label))), lab, ink, 2f);
			// the value as a chip: tinted in the tab's colour when it isn't the default
			string val = Value(row);
			bool changed = val != row.Default && !SilksongOptions.IsGroup(row);
			float vs = 0.48f;
			float vw = Art.Measure(val) * vs + 36f;
			bool two = Values(row).Length <= 2;   // two choices: a button that flips, no arrows (user 2026-10-17)
			float vx = rowR - vw - ((sel && !two) ? 34f : 0f);
			Rect chip = new Rect(vx, y + (rr.height - 40f) / 2f, vw, 40f);
			bool onOffRow = (row.Values == "On,Off" || row.Values == "Off,On");
			if (onOffRow)
			{
				// an On/Off switch: a track that fills with the tab's colour, a white knob that slides across
				bool isOn = val == "On";
				Rect track = new Rect(rowR - 84f, y + (rr.height - 36f) / 2f, 84f, 36f);
				GUI.color = isOn ? new Color(tabCol.r, tabCol.g, tabCol.b, num4) : new Color(1f, 1f, 1f, 0.14f * num4);
				GUI.DrawTexture(track, Texture2D.whiteTexture);
				Rect knob = new Rect(isOn ? (track.xMax - 32f) : (track.x + 4f), track.y + 4f, 28f, 28f);
				GUI.color = new Color(1f, 1f, 1f, num4);
				GUI.DrawTexture(knob, Texture2D.whiteTexture);
				string st = isOn ? "On" : "Off";
				float ss = 0.42f;
				Art.DrawOutlined(st, track.x - 16f - Art.Measure(st) * ss, track.y + (track.height - (float)Art.LineHeight * ss) / 2f, ss, isOn ? new Color(1f, 1f, 1f, num4) : new Color(0.7f, 0.7f, 0.75f, num4), ink, 2f);
				continue;
			}
			if (IsBarRow(row))
			{
				// a number range (volumes): a bar to click or drag anywhere along (user 2026-10-17)
				int bi = Index(row);
				int bn = Values(row).Length;
				Rect track = new Rect(rowR - 320f, y + (rr.height - 14f) / 2f, 320f, 14f);
				barRects[item2] = new Rect(track.x - 8f, y, track.width + 16f, rr.height);
				GUI.color = new Color(1f, 1f, 1f, 0.12f * num4);
				GUI.DrawTexture(track, Texture2D.whiteTexture);
				float fill = bn > 1 ? (float)bi / (float)(bn - 1) : 0f;
				GUI.color = new Color(tabCol.r, tabCol.g, tabCol.b, num4);
				GUI.DrawTexture(new Rect(track.x, track.y, track.width * fill, track.height), Texture2D.whiteTexture);
				Rect knobB = new Rect(track.x + track.width * fill - 9f, track.center.y - 15f, 18f, 30f);
				GUI.color = new Color(1f, 1f, 1f, num4);
				GUI.DrawTexture(knobB, Texture2D.whiteTexture);
				Art.DrawOutlined(val, track.x - 22f - Art.Measure(val) * vs, y + (rr.height - (float)Art.LineHeight * vs) / 2f, vs, new Color(1f, 1f, 1f, num4), ink, 2f);
				continue;
			}
			if (!two)
			{
				// a selector (user 2026-10-17): [<] value [>] in a fixed column so every row lines up, arrows always
				// shown (bright on the chosen row), little pips under the value for where it is in the list
				int idx = Index(row);
				int n = Values(row).Length;
				float boxW = 40f;
				float valW = Mathf.Max(190f, vw);
				Rect rBox = new Rect(rowR - boxW, chip.y, boxW, chip.height);
				Rect vBox = new Rect(rBox.x - 6f - valW, chip.y, valW, chip.height);
				Rect lBox = new Rect(vBox.x - 6f - boxW, chip.y, boxW, chip.height);
				float on = sel ? 1f : 0.45f;
				for (int side = 0; side < 2; side++)
				{
					Rect bx = side == 0 ? lBox : rBox;
					bool hot = sel && !padHints && bx.Contains(mouse);
					GUI.color = hot ? new Color(tabCol.r, tabCol.g, tabCol.b, num4) : new Color(1f, 1f, 1f, (sel ? 0.16f : 0.07f) * num4);
					GUI.DrawTexture(bx, Texture2D.whiteTexture);
					string ar = side == 0 ? "<" : ">";
					Art.DrawOutlined(ar, bx.center.x - Art.Measure(ar) * vs / 2f, bx.y + (bx.height - (float)Art.LineHeight * vs) / 2f, vs, new Color(1f, 1f, 1f, on * num4), ink, 2f);
				}
				GUI.color = changed ? new Color(tabCol.r, tabCol.g, tabCol.b, 0.85f * num4) : new Color(1f, 1f, 1f, 0.08f * num4);
				GUI.DrawTexture(vBox, Texture2D.whiteTexture);
				float fs = Mathf.Min(vs, (valW - 20f) / Math.Max(1f, Art.Measure(val)));
				Art.DrawOutlined(val, vBox.center.x - Art.Measure(val) * fs / 2f, vBox.y + (vBox.height - (float)Art.LineHeight * fs) / 2f - (n <= 12 ? 3f : 0f), fs, new Color(1f, 1f, 1f, num4), ink, 2f);
				if (n <= 12)
				{
					float pw = 6f;
					float gap = 4f;
					float px = vBox.center.x - (n * pw + (n - 1) * gap) / 2f;
					for (int i = 0; i < n; i++)
					{
						GUI.color = new Color(1f, 1f, 1f, (i == idx ? 0.95f : 0.25f) * num4);
						GUI.DrawTexture(new Rect(px + i * (pw + gap), vBox.yMax - 7f, pw, 3f), Texture2D.whiteTexture);
					}
				}
				string hair = row.Id.StartsWith("hair_", StringComparison.Ordinal) ? HairHex(row, null) : null;
				if (hair != null && ColorUtility.TryParseHtmlString("#" + hair, out var hc))
				{
					hc.a = num4;
					GUI.color = new Color(0f, 0f, 0f, num4);
					GUI.DrawTexture(new Rect(lBox.x - 44f, lBox.y + 4f, 32f, 32f), Texture2D.whiteTexture);
					GUI.color = hc;
					GUI.DrawTexture(new Rect(lBox.x - 42f, lBox.y + 6f, 28f, 28f), Texture2D.whiteTexture);
				}
				rowArrows[item2] = new Rect[2] { lBox, rBox };
				if (sel)
				{
					arrowL = lBox;
					arrowR = rBox;
				}
				continue;
			}
			if (SilksongOptions.IsButton(row))
			{
				// something that opens: a solid button in the tab's colour with an arrow, raised a little
				string bl = val + "  >";
				float bw = Art.Measure(bl) * vs + 44f;
				Rect btn = new Rect(rowR - bw, chip.y, bw, chip.height);
				bool hot = !padHints && btn.Contains(mouse);
				GUI.color = new Color(0f, 0f, 0f, 0.35f * num4);
				GUI.DrawTexture(new Rect(btn.x + 3f, btn.y + 4f, btn.width, btn.height), Texture2D.whiteTexture);
				GUI.color = new Color(tabCol.r, tabCol.g, tabCol.b, (hot || sel ? 1f : 0.8f) * num4);
				GUI.DrawTexture(btn, Texture2D.whiteTexture);
				GUI.color = new Color(1f, 1f, 1f, 0.25f * num4);
				GUI.DrawTexture(new Rect(btn.x, btn.y, btn.width, 3f), Texture2D.whiteTexture);
				Art.DrawOutlined(bl, btn.x + 22f, btn.y + (btn.height - (float)Art.LineHeight * vs) / 2f, vs, new Color(1f, 1f, 1f, num4), ink, 2f);
				continue;
			}
			if (Values(row).Length == 1)
			{
				// just information: plain text, no box, so it doesn't look clickable
				Art.DrawOutlined(val, rowR - Art.Measure(val) * vs, y + (rr.height - (float)Art.LineHeight * vs) / 2f, vs, new Color(0.75f, 0.75f, 0.8f, num4), ink, 2f);
				continue;
			}
			// two choices: one button showing the choice
			GUI.color = changed ? new Color(tabCol.r, tabCol.g, tabCol.b, 0.85f * num4) : new Color(1f, 1f, 1f, 0.08f * num4);
			GUI.DrawTexture(chip, Texture2D.whiteTexture);
			Art.DrawOutlined(val, chip.x + 18f, chip.y + (chip.height - (float)Art.LineHeight * vs) / 2f, vs, new Color(1f, 1f, 1f, num4), ink, 2f);
		}
		GUI.matrix = rowsMatrix;
		if (menuScroll > 1f)
		{
			Art.DrawOutlined("^", card.center.x - 8f, num18 - 30f, 0.5f, new Color(1f, 1f, 1f, num4 * 0.7f), ink, 2f);
		}
		if (menuScroll < num17 - num19 - 1f)
		{
			Art.DrawOutlined("v", card.center.x - 8f, num18 + num19 + 2f, 0.5f, new Color(1f, 1f, 1f, num4 * 0.7f), ink, 2f);
		}
		// the description strip at the card's foot
		Rect desc = new Rect(card.x + 30f, card.yMax - 120f, card.width - 60f, 96f);
		GUI.color = new Color(1f, 1f, 1f, 0.05f * num4);
		GUI.DrawTexture(desc, Texture2D.whiteTexture);
		GUI.color = new Color(tabCol.r, tabCol.g, tabCol.b, num4);
		GUI.DrawTexture(new Rect(desc.x, desc.y, 4f, desc.height), Texture2D.whiteTexture);
		float dsc = 0.42f;
		float dy = desc.y + 14f;
		AssistRow dRow = AssistOptions.All[selected];
		bool preview = VariantPreview.Has(dRow.Id) && !LockedSection(dRow.Section);
		if (preview)
		{
			// a tiny Madeline acting the option out (user 2026-10-17)
			VariantPreview.Draw(dRow.Id, Value(dRow), new Rect(desc.xMax - 226f, desc.y + 4f, 220f, desc.height - 8f), num4, tabCol);
		}
		foreach (string dl in Art.Wrap(dRow.Description, (desc.width - 50f - (preview ? 240f : 0f)) / dsc))
		{
			Art.DrawOutlined(dl, desc.x + 24f, dy, dsc, new Color(0.82f, 0.82f, 0.86f, num4), ink, 2f);
			dy += (float)Art.LineHeight * dsc;
		}
		if (LockedSection(Tabs[tab]))
		{
			DrawLock(num4, card.center.x, card.y + 104f, card.yMax, card.width / 2f);   // the whole card, rows and description
		}
		if (SaveMarked && (Tabs[tab] == "Assist Mode" || Tabs[tab] == "Variants"))
		{
			string text4 = "Assist save: no achievements or golden berries";
			Art.DrawOutlined(text4, card.x + 40f, card.yMax + 14f, 0.4f, new Color(1f, 0.85f, 0.35f, num4 * 0.9f), ink, 2f);
		}
		string line3 = (search != null) ? "Type to search   Up/Down choose   Left/Right change   Backspace delete   Esc stop searching" : (FocusTab != null) ? (padHints ? "Up/Down choose   Left/Right change   Circle back to the file" : "Up/Down choose   Left/Right change   Esc back to the file") : (padHints ? "Up/Down choose   Left/Right change   L1/R1 pages   Circle to close" : ("Up/Down choose   Left/Right change   Q/E or 1-9 pages   Ctrl+F search   Del reset tab   " + KeyName(AssistOptions.KeySettings) + " or Esc to close"));
		Art.DrawOutlined(line3, num2 / 2f - Art.Measure(line3) * 0.42f / 2f, 1030f, 0.42f, new Color(1f, 1f, 1f, num4 * 0.75f), ink, 2f);
		if (look)
		{
			DrawPreview(num4, card.xMax + 20f, num2 - 20f);
		}
		if (prompt != 0)
		{
			DrawPrompt(num4, (num2 / 2f));
		}
		GUI.matrix = matrix;
		GUI.color = Color.white;
	}

	private void DrawDashArrow(float s)
	{
		HeroController silentInstance = HeroController.UnsafeInstance;
		Camera main = Camera.main;
		GuiArt.Img img = Art.Image("dotarrow");
		GuiArt.Img img2 = Art.Image("dotarrow_outline");
		if (!(silentInstance == null) && !(main == null) && img != null)
		{
			Vector2 aimPreview = Plugin.Controller.AimPreview;
			Vector3 vector = main.WorldToScreenPoint(silentInstance.transform.position);
			Vector2 vector2 = new Vector2(vector.x, (float)Screen.height - vector.y) / s;
			float z = Mathf.Atan2(aimPreview.y, aimPreview.x) * 57.29578f;
			Vector2 vector3 = vector2 + aimPreview.normalized * 110f;
			Matrix4x4 matrix = GUI.matrix;
			GUI.matrix = Matrix4x4.TRS(new Vector3(vector3.x * s, vector3.y * s, 0f), Quaternion.Euler(0f, 0f, z), new Vector3(s, s, 1f));
			float num = 72f;
			if (img2 != null)
			{
				GUI.color = Color.black;
				GUI.DrawTexture(new Rect((0f - num) / 2f - 4f, (0f - num) / 2f - 4f, num + 8f, num + 8f), img2.Tex);
			}
			GUI.color = Color.white;
			GUI.DrawTexture(new Rect((0f - num) / 2f, (0f - num) / 2f, num, num), img.Tex);
			GUI.matrix = matrix;
		}
	}

	// each settings tab's own colour
	private static string TabColor(int i)
	{
		string[] c = new string[10] { "9AA3B5", "4FC1C9", "D98B4A", "6CC48A", "E886B8", "5BA4E6", "B07CE8", "E8624F", "E8C35A", "C9C2D9" };
		return c[Mathf.Clamp(i, 0, c.Length - 1)];
	}

	private static Color Hex(string hex)
	{
		ColorUtility.TryParseHtmlString("#" + hex, out var color);
		return color;
	}
}
