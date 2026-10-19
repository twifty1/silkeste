using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using GlobalEnums;
using HarmonyLib;
using InControl;
using MadelineInPharloom.Sheets;
using TeamCherry.Localization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MadelineInPharloom;

public class BossCard : MonoBehaviour
{
	private class Fight
	{
		public string Id;

		public string Title;

		public string Sub;

		public string Scene;

		public float Time;

		public float LeftSceneAt = -1f;

		public int Hits;

		public int Heals;

		public int Dashes;

		public bool Assist;

		public bool Invincible;

		public bool HadGolden;

		public bool FirstTry;
	}

	private class Card
	{
		public string Title;

		public string Sub;

		public float Time;

		public int Deaths;

		public int Dashes;

		public int Hits;

		public int Heals;

		public int Goldens;

		public bool Golden;

		public bool HadGolden;

		public bool Assist;

		public bool Invincible;

		public bool FirstTry;

		public bool NewBest;

		public bool Viewing;

		public bool AreaMode;

		public string Note = "";

		public float PrevBest = -1f;
	}

	private class Record
	{
		public float Time;

		public int Deaths;

		public int Dashes;

		public int Hits;

		public int Heals;
	}

	private enum Phase
	{
		None,
		In,
		Shown,
		Out
	}

	public static GuiArt Art;

	public static string DataDir;

	public static BossCard Instance;

	private float hudAlpha;

	private float hudLostT = -1f;

	private readonly Dictionary<string, Record> best = new Dictionary<string, Record>();

	private readonly Dictionary<string, string> titles = new Dictionary<string, string>();

	private readonly Dictionary<string, string> journal = new Dictionary<string, string>();

	private Fight fight;

	private readonly Dictionary<string, int> deaths = new Dictionary<string, int>();

	private readonly HashSet<string> goldens = new HashSet<string>();

	private bool loaded;

	private bool wasDead;

	private int lastHealth = -1;

	private int baselineSlot = -1;

	private readonly Dictionary<string, bool> flags = new Dictionary<string, bool>();

	private static List<FieldInfo> defeatFields;

	private Phase phase;

	private Card card;

	private readonly List<Card> queue = new List<Card>();

	private float t;

	private float shownFor;

	private float controlFor;

	private bool relinquished;

	public static List<Sprite> GoldFrames;

	private static readonly Dictionary<Sprite, GuiArt.Img> goldImgs = new Dictionary<Sprite, GuiArt.Img>();

	private bool viewing;

	// Rematch (BossRematch): Records closes itself when one starts
	public static bool CloseRequested;

	private string rematchFlag;

	// Bosses tab: All / Act 1 / Act 2 / Act 3 (user 2026-10-17)
	private int actFilter;

	private readonly Rect[] actRects = new Rect[4];

	private static readonly string[] ActNames = new string[4] { "All", "Act 1", "Act 2", "Act 3" };

	private Rect rematchRect;

	/// <summary>The defeat flag of a boss card id, if Silkeste knows it.</summary>
	public static string FlagOfBoss(string id)
	{
		if (Instance == null || id == null)
		{
			return null;
		}
		foreach (KeyValuePair<string, string> kv in Instance.flagBoss)
		{
			if (kv.Value == id)
			{
				return kv.Key;
			}
		}
		return null;
	}

	private bool viewAll;

	private bool viewRelinquished;

	private bool photoTaken;

	private static bool fromMenu;

	private int viewTab;

	private bool viewPad;

	private bool cursorWas;

	private CursorLockMode lockWas;

	private readonly Rect[] vTabRects = new Rect[7];

	private readonly Rect[] scopeRects = new Rect[2];

	private Rect pickL;

	private Rect pickR;

	private int viewIndex;

	private float viewT;

	private TimeManager.TimeControlInstance viewPause;

	private readonly Dictionary<string, string> flagBoss = new Dictionary<string, string>();

	private readonly HashSet<int> importedSlots = new HashSet<int>();

	private readonly Dictionary<string, float> areaTime = new Dictionary<string, float>();

	private readonly Dictionary<string, int> areaDeaths = new Dictionary<string, int>();

	private readonly Dictionary<string, int> areaKills = new Dictionary<string, int>();

	private readonly Dictionary<string, string> areaNames = new Dictionary<string, string>();

	private string curZone;

	private float bannerT = -1f;

	private float areaSaveIn = 10f;

	private string bannerZone;

	private bool bannerNew;

	private bool wasHazard;

	private static readonly string[] TabNames = new string[7] { "Bosses", "Areas", "Stats", "Achievements", "Postcards", "Photos", "Run" };

	private bool loggedError;

	public static string ResetFlag;

	private float statsTop;

	private static GuiArt.Img clock;

	private static Material meshMat;

	public static bool GoldenRun
	{
		get
		{
			Fight fight = ((Instance != null) ? Instance.fight : null);
			if (fight != null && !fight.HadGolden && !fight.Assist && (fight.FirstTry || fight.Heals == 0))
			{
				return fight.LeftSceneAt < 0f;
			}
			return false;
		}
	}

	public static bool InFight
	{
		get
		{
			Fight fight = ((Instance != null) ? Instance.fight : null);
			if (fight != null)
			{
				return fight.LeftSceneAt < 0f;
			}
			return false;
		}
	}

	public static float FightTime => ((Instance != null) ? Instance.fight : null)?.Time ?? 0f;

	public static bool GoldenInHud
	{
		get
		{
			if (GoldenRun)
			{
				return FightTime >= 2.5f;
			}
			return false;
		}
	}

	public static bool CardShowing
	{
		get
		{
			if (Instance != null)
			{
				return Instance.phase != Phase.None;
			}
			return false;
		}
	}

	public static bool RecordsOpen
	{
		get
		{
			if (Instance != null)
			{
				return Instance.viewing;
			}
			return false;
		}
	}

	public static string CurrentAreaName
	{
		get
		{
			if (!(Instance != null) || Instance.curZone == null)
			{
				return null;
			}
			return Instance.AreaName(Instance.curZone);
		}
	}

	public static int AreasVisited
	{
		get
		{
			if (Instance == null)
			{
				return 0;
			}
			HashSet<string> hashSet = new HashSet<string>();
			foreach (string key in Instance.areaTime.Keys)
			{
				hashSet.Add(key.Substring(key.IndexOf('|') + 1));
			}
			return hashSet.Count;
		}
	}

	private string SaveFile => Path.Combine(DataDir, "bosses.cfg");

	public static event Action GoldenLost;

	private void Awake()
	{
		Instance = this;
		try
		{
			MethodInfo methodInfo = AccessTools.Method(typeof(HealthManager), "Die", new Type[8]
			{
				typeof(float?),
				typeof(AttackTypes),
				typeof(NailElements),
				typeof(GameObject),
				typeof(bool),
				typeof(float),
				typeof(bool),
				typeof(bool)
			});
			new Harmony("silkeste.kills").Patch(methodInfo, new HarmonyMethod(typeof(BossCard), "DiePre"), new HarmonyMethod(typeof(BossCard), "DiePost"));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Kill tracker: " + ex.Message);
		}
	}

	private static void DiePre(HealthManager __instance, out bool __state)
	{
		__state = __instance.isDead;
	}

	// An enemy that wasn't dead before Die and is now: one kill in the current area.
	private static void DiePost(HealthManager __instance, bool __state)
	{
		try
		{
			GameManager silentInstance = GameManager.SilentInstance;
			if (!__state && __instance.isDead && Instance != null && Instance.curZone != null && silentInstance != null && !silentInstance.IsMenuScene())
			{
				string key = Key(silentInstance.profileID, Instance.curZone);
				Instance.areaKills.TryGetValue(key, out var value);
				Instance.areaKills[key] = value + 1;
			}
		}
		catch (Exception)
		{
		}
	}

	internal static GuiArt.Img PixelBerry()
	{
		if (GoldFrames == null || GoldFrames.Count == 0)
		{
			return null;
		}
		int num = (int)(Time.unscaledTime / 0.08f) % 32;
		Sprite sprite = GoldFrames[Math.Min((num < 24) ? (num % 6) : (num - 24), GoldFrames.Count - 1)];
		if (!goldImgs.TryGetValue(sprite, out var value))
		{
			Dictionary<Sprite, GuiArt.Img> dictionary = goldImgs;
			GuiArt.Img obj = new GuiArt.Img
			{
				W = sprite.texture.width,
				H = sprite.texture.height,
				Tex = sprite.texture
			};
			value = obj;
			dictionary[sprite] = obj;
		}
		return value;
	}

	public static void OnBossTitle(string id)
	{
		if (Instance == null || string.IsNullOrEmpty(id))
		{
			return;
		}
		Names(id, out var title, out var sub);
		Achievements.FightStarted();
		MadelineView.Startle();
		Instance.fight = new Fight
		{
			Id = id,
			Title = title,
			Sub = sub,
			Scene = SceneManager.GetActiveScene().name,
			Assist = (AssistMode.AnyOn || AssistMode.SaveMarked),
			Invincible = AssistMode.Invincible
		};
		GameManager instance = GameManager.instance;
		Instance.Load();
		Instance.fight.HadGolden = Instance.HasGolden(id);
		Instance.fight.FirstTry = instance == null || !Instance.deaths.TryGetValue(Key(instance.profileID, id), out var value) || value == 0;
		Instance.lastHealth = ((PlayerData.instance != null) ? PlayerData.instance.health : (-1));
		Instance.titles[id] = title;
		if (!Instance.journal.ContainsKey(id))
		{
			string text = JournalFor(title);
			if (text != null)
			{
				Instance.journal[id] = text;
			}
		}
		Plugin.Log.LogInfo("Boss fight: " + id + " (" + title + ((sub.Length > 0) ? (", " + sub) : "") + ")");
	}

	public static void OnDash()
	{
		if (Instance != null && Instance.fight != null)
		{
			Instance.fight.Dashes++;
		}
	}

	private static void Names(string id, out string title, out string sub)
	{
		string text = Clean(Lang(id + "_MAIN") ?? Lang(id) ?? Pretty(id));
		string text2 = Clean(Lang(id + "_SUPER") ?? "");
		title = ((text2.Length > 0) ? (text2 + " " + text) : text);
		sub = "";
	}

	internal static string Lang(string key)
	{
		try
		{
			if (Language.Has(key, "Titles"))
			{
				string text = Language.Get(key, "Titles");
				if (!string.IsNullOrEmpty(text))
				{
					return text;
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	internal static string Clean(string s)
	{
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = false;
		foreach (char c in s)
		{
			switch (c)
			{
			case '<':
				flag = true;
				continue;
			case '>':
				flag = false;
				continue;
			}
			if (flag)
			{
				continue;
			}
			int value;
			if (c >= ' ')
			{
				switch (c)
				{
				default:
					value = c;
					break;
				case '–':
				case '—':
					value = 45;
					break;
				case '“':
				case '”':
					value = 34;
					break;
				case '‘':
				case '’':
					value = 39;
					break;
				}
			}
			else
			{
				value = 32;
			}
			stringBuilder.Append((char)value);
		}
		return stringBuilder.ToString().Trim();
	}

	private static string Pretty(string id)
	{
		string[] array = id.ToLowerInvariant().Split(new char[2] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = char.ToUpperInvariant(array[i][0]) + array[i].Substring(1);
		}
		return string.Join(" ", array);
	}

	private static string Key(int slot, string id)
	{
		return slot + "|" + id;
	}

	private static string JournalFor(string title)
	{
		try
		{
			string text = Norm(title);
			EnemyJournalRecord enemyJournalRecord = null;
			foreach (EnemyJournalRecord allEnemy in EnemyJournalManager.GetAllEnemies())
			{
				if (allEnemy == null)
				{
					continue;
				}
				string text2 = Norm(allEnemy.DisplayName.ToString());
				if (text2.Length != 0)
				{
					if (text2 == text)
					{
						return allEnemy.name;
					}
					if (enemyJournalRecord == null && (text.Contains(text2) || text2.Contains(text)))
					{
						enemyJournalRecord = allEnemy;
					}
				}
			}
			return (enemyJournalRecord != null) ? enemyJournalRecord.name : null;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Boss journal: " + ex.Message);
			return null;
		}
	}

	private static string Norm(string s)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string text = (s ?? "").ToLowerInvariant();
		foreach (char c in text)
		{
			if (char.IsLetter(c))
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	public static void OpenAchievementsFromMenu()
	{
		if (!(Instance == null) && !Instance.viewing)
		{
			fromMenu = true;
			Instance.viewing = true;
			Instance.viewTab = 3;
			Instance.viewIndex = 0;
			Instance.viewPad = false;
			Instance.cursorWas = Cursor.visible;
			Instance.lockWas = Cursor.lockState;
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
			Sfx.Play(Sounds.CardIn.Id);
		}
	}

	private List<KeyValuePair<string, Record>> ViewList(int slot)
	{
		Dictionary<string, Record> dictionary = new Dictionary<string, Record>();
		foreach (KeyValuePair<string, Record> item in best)
		{
			int num = item.Key.IndexOf('|');
			string key = item.Key.Substring(num + 1);
			if ((viewAll || !(item.Key.Substring(0, num) != slot.ToString(CultureInfo.InvariantCulture))) && (!dictionary.TryGetValue(key, out var value) || (item.Value.Time >= 0f && (value.Time < 0f || item.Value.Time < value.Time))))
			{
				dictionary[key] = item.Value;
			}
		}
		List<KeyValuePair<string, Record>> list = new List<KeyValuePair<string, Record>>(dictionary);
		list.RemoveAll((KeyValuePair<string, Record> r) => Hidden(r.Key));
		list.Sort((KeyValuePair<string, Record> a, KeyValuePair<string, Record> b) => string.Compare(TitleOf(a.Key), TitleOf(b.Key), StringComparison.OrdinalIgnoreCase));
		return list;
	}

	// Skull Tyrant is a minor boss: left out of Records altogether (user 2026-10-17)
	private bool Hidden(string key)
	{
		string t = Norm(key) + " " + Norm(TitleOf(key));
		return t.Contains("skullking") || t.Contains("skulltyrant");
	}

	private string TitleOf(string boss)
	{
		if (boss.StartsWith("unseen:", StringComparison.Ordinal))
		{
			return "???";
		}
		if (boss.StartsWith("flag:", StringComparison.Ordinal))
		{
			// rows made from a save flag carry the code's name (Vampire Gnat Boss): use the name players know
			if (!shownNames.TryGetValue(boss, out var shown))
			{
				shown = BossRematch.TitleOf(boss.Substring(5));
				if (shown == null && journal.TryGetValue(boss, out var page))
				{
					try
					{
						EnemyJournalRecord record = EnemyJournalManager.GetRecord(page);
						string d = (record != null) ? Clean(record.DisplayName.ToString()) : null;
						if (!string.IsNullOrEmpty(d) && !d.StartsWith("!!", StringComparison.Ordinal))
						{
							shown = d;
						}
					}
					catch (Exception)
					{
					}
				}
				shownNames[boss] = shown;
			}
			if (shown != null)
			{
				return shown;
			}
		}
		if (!titles.TryGetValue(boss, out var value))
		{
			return Pretty(boss);
		}
		return value;
	}

	private readonly Dictionary<string, string> shownNames = new Dictionary<string, string>();

	/// <summary>F10's "rematch every boss": puts the rematch bosses into Records now.</summary>
	public void ImportRematches()
	{
		GameManager gm = GameManager.SilentInstance;
		if (gm != null && PlayerData.instance != null)
		{
			WatchDefeats(gm, PlayerData.instance);
			ImportBeaten(gm.profileID, PlayerData.instance, force: true);
			cacheAt = -10f;
		}
	}

	private void ImportBeaten(int slot, PlayerData pd, bool force = false)
	{
		if (defeatFields == null || (!importedSlots.Add(slot) && !force))
		{
			return;
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		HashSet<string> hashSet = new HashSet<string>(titles.Keys);
		foreach (string key2 in deaths.Keys)
		{
			hashSet.Add(key2.Substring(key2.IndexOf('|') + 1));
		}
		foreach (string golden in goldens)
		{
			hashSet.Add(golden.Substring(golden.IndexOf('|') + 1));
		}
		foreach (string item in hashSet)
		{
			if (item.StartsWith("flag:", StringComparison.Ordinal))
			{
				continue;
			}
			if (!journal.TryGetValue(item, out var value))
			{
				Names(item, out var title, out var _);
				if (!titles.ContainsKey(item))
				{
					titles[item] = title;
				}
				value = JournalFor(title);
				if (value != null)
				{
					journal[item] = value;
				}
			}
			if (value != null && !dictionary.ContainsKey(value))
			{
				dictionary[value] = item;
			}
		}
		int num = 0;
		foreach (FieldInfo defeatField in defeatFields)
		{
			bool rematch = BossRematch.All && BossRematch.Allows(defeatField.Name);
			if (!(bool)defeatField.GetValue(pd) && !rematch && best.TryGetValue(Key(slot, "flag:" + defeatField.Name), out Record unbeaten) && unbeaten.Time == -1f)
			{
				// a row F10's rematches added for a boss this save never beat, whose rematch is gone again
				best.Remove(Key(slot, "flag:" + defeatField.Name));
			}
			if ((!defeatField.Name.StartsWith("defeated", StringComparison.OrdinalIgnoreCase) && !BossRematch.Allows(defeatField.Name)) || (!(bool)defeatField.GetValue(pd) && !rematch))
			{
				continue;
			}
			if (!flagBoss.TryGetValue(defeatField.Name, out var value2))
			{
				string text = BossRematch.TitleOf(defeatField.Name) ?? FlagTitle(defeatField.Name);
				string text2 = JournalFor(text);
				if (text2 == null && !BossRematch.Allows(defeatField.Name))
				{
					continue;
				}
				if (text2 == null || !dictionary.TryGetValue(text2, out value2))
				{
					value2 = "flag:" + defeatField.Name;
					titles[value2] = text;
					if (text2 != null)
					{
						journal[value2] = text2;
					}
				}
				flagBoss[defeatField.Name] = value2;
			}
			string key = Key(slot, value2);
			if (!best.ContainsKey(key))
			{
				best[key] = new Record
				{
					Time = -1f,
					Deaths = -1,
					Dashes = -1,
					Hits = -1,
					Heals = -1
				};
				num++;
			}
		}
		if (num > 0)
		{
			Save();
			Plugin.Log.LogInfo("Boss records: imported " + num + " bosses beaten before on save " + slot);
		}
	}

	private static string FlagTitle(string flag)
	{
		string text = flag.Substring("defeated".Length).TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			if (c == '_')
			{
				stringBuilder.Append(' ');
				continue;
			}
			if (i > 0 && char.IsUpper(c) && !char.IsUpper(text[i - 1]))
			{
				stringBuilder.Append(' ');
			}
			stringBuilder.Append(c);
		}
		return stringBuilder.ToString().Trim();
	}

	private void UpdateViewer(HeroController h, GameManager gm)
	{
		Keyboard current = Keyboard.current;
		InputHandler instance = ManagerSingleton<InputHandler>.Instance;
		HeroActions heroActions = ((instance != null) ? instance.inputActions : null);
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		if (!viewing)
		{
			viewT = Math.Max(0f, viewT - unscaledDeltaTime / 0.15f);
			bool flag = h != null && gm.GameState == GameState.PLAYING && !gm.isPaused && !h.cState.dead && !h.cState.transitioning;
			if ((AssistMode.KeyPressed(AssistOptions.KeyRecords) || MenuMouse.SharePressed()) && flag && phase == Phase.None && !AssistMode.IsOpen)
			{
				viewing = true;
				viewIndex = 0;
				viewPad = false;
				cursorWas = Cursor.visible;
				lockWas = Cursor.lockState;
				Cursor.visible = true;
				Cursor.lockState = CursorLockMode.None;
				if (viewPause == null)
				{
					viewPause = TimeManager.CreateTimeControl(0f, TimeManager.TimeControlInstance.Type.MinValue);
				}
				if (!h.controlReqlinquished)
				{
					h.RelinquishControl();
					viewRelinquished = true;
				}
				Sfx.Play(Sounds.CardIn.Id);
			}
			return;
		}
		viewT = Math.Min(1f, viewT + unscaledDeltaTime / 0.15f);
		bool flag2 = CloseRequested || AssistMode.KeyPressed(AssistOptions.KeyRecords) || MenuMouse.SharePressed() || (current != null && (current.escapeKey.wasPressedThisFrame || current.backspaceKey.wasPressedThisFrame)) || (heroActions != null && (heroActions.Dash.WasPressed || heroActions.MenuCancel.WasPressed));
		try
		{
			InControl.InputDevice activeDevice = InputManager.ActiveDevice;
			if (activeDevice != null && activeDevice.Action2.WasPressed)
			{
				flag2 = true;
			}
		}
		catch (Exception)
		{
		}
		if (flag2 && Achievements.Confirming)
		{
			Achievements.CancelConfirm();
			return;
		}
		if (flag2)
		{
			CloseRequested = false;
			viewing = false;
			Cursor.visible = cursorWas;
			Cursor.lockState = lockWas;
			if (viewPause != null)
			{
				viewPause.Release();
				viewPause = null;
			}
			if (viewRelinquished)
			{
				h.RegainControl();
			}
			viewRelinquished = false;
			Plugin.Controller.ClearInput();
			Sfx.Play(Sounds.CardOut.Id);
			return;
		}
		bool flag3 = (current != null && (current.leftArrowKey.wasPressedThisFrame || current.aKey.wasPressedThisFrame)) || (heroActions?.Left.WasPressed ?? false);
		bool flag4 = (current != null && (current.rightArrowKey.wasPressedThisFrame || current.dKey.wasPressedThisFrame)) || (heroActions?.Right.WasPressed ?? false);
		bool flag5 = (current != null && (current.upArrowKey.wasPressedThisFrame || current.downArrowKey.wasPressedThisFrame || current.wKey.wasPressedThisFrame || current.sKey.wasPressedThisFrame)) || (heroActions != null && (heroActions.Up.WasPressed || heroActions.Down.WasPressed));
		viewPad = MenuMouse.PadHints(viewPad);
		bool flag6 = false;
		bool flag7 = false;
		try
		{
			InControl.InputDevice activeDevice2 = InputManager.ActiveDevice;
			if (activeDevice2 != null)
			{
				flag6 = activeDevice2.LeftBumper.WasPressed;
				flag7 = activeDevice2.RightBumper.WasPressed;
			}
		}
		catch (Exception)
		{
		}
		bool flag8 = (current != null && (current.tabKey.wasPressedThisFrame || current.eKey.wasPressedThisFrame)) || flag7;
		bool flag9 = (current?.qKey.wasPressedThisFrame ?? false) || flag6;
		for (int i = 0; i < TabNames.Length; i++)
		{
			if (i != viewTab && MenuMouse.Clicked(vTabRects[i]))
			{
				viewTab = i;
				viewIndex = 0;
				Sfx.Play(Sounds.MenuRolloverDown.Id);
				return;
			}
		}
		for (int i = 0; i < actRects.Length; i++)
		{
			if (viewTab == 0 && i != actFilter && MenuMouse.Clicked(actRects[i]))
			{
				actFilter = i;
				viewIndex = 0;
				Sfx.Play(Sounds.MenuRolloverDown.Id);
				return;
			}
		}
		if (viewTab == 0 && rematchFlag != null && !fromMenu && (MenuMouse.Clicked(rematchRect) || (current != null && (current.enterKey.wasPressedThisFrame)) || (heroActions?.Jump.WasPressed ?? false)))
		{
			Sfx.Play(Sounds.MenuRolloverDown.Id);
			BossRematch.Begin(rematchFlag);
			return;
		}
		if (viewTab == 3)
		{
			bool up = (current != null && (current.upArrowKey.wasPressedThisFrame || current.wKey.wasPressedThisFrame)) || (heroActions?.Up.WasPressed ?? false);
			bool down = (current != null && (current.downArrowKey.wasPressedThisFrame || current.sKey.wasPressedThisFrame)) || (heroActions?.Down.WasPressed ?? false);
			bool flag10 = false;
			try
			{
				InControl.InputDevice activeDevice3 = InputManager.ActiveDevice;
				if (activeDevice3 != null)
				{
					flag10 = activeDevice3.Action4.WasPressed;
				}
			}
			catch (Exception)
			{
			}
			bool flag11 = false;
			try
			{
				InControl.InputDevice activeDevice4 = InputManager.ActiveDevice;
				if (activeDevice4 != null)
				{
					flag11 = activeDevice4.Action3.WasPressed;
				}
			}
			catch (Exception)
			{
			}
			bool confirm = (current != null && (current.enterKey.wasPressedThisFrame || current.spaceKey.wasPressedThisFrame)) || (heroActions?.Jump.WasPressed ?? false);
			bool held = (current != null && (current.enterKey.isPressed || current.spaceKey.isPressed)) || (heroActions?.Jump.IsPressed ?? false);
			if (Achievements.Confirming)
			{
				Achievements.ConfirmInput(flag3, flag4, confirm, held);
				flag8 = (flag9 = false);
			}
			else if (flag11 || (current != null && current.rKey.wasPressedThisFrame))
			{
				Achievements.OpenConfirm();
			}
			else if (flag10 || (current != null && current.tabKey.wasPressedThisFrame))
			{
				Achievements.ToggleSort();
				flag8 = false;
			}
			else if (!flag8 && !flag9)
			{
				Achievements.Navigate(flag3, flag4, up, down);
			}
			flag3 = (flag4 = (flag5 = false));
		}
		else
		{
			bool flag12 = (current != null && (current.upArrowKey.wasPressedThisFrame || current.wKey.wasPressedThisFrame)) || (heroActions?.Up.WasPressed ?? false);
			bool num = (current != null && (current.downArrowKey.wasPressedThisFrame || current.sKey.wasPressedThisFrame)) || (heroActions?.Down.WasPressed ?? false);
			flag5 = flag3 || flag4;
			flag3 = flag12;
			flag4 = num;
		}
		if (fromMenu)
		{
			flag5 = false;
			viewAll = true;
		}
		if (MenuMouse.Clicked(scopeRects[0]) && viewAll)
		{
			flag5 = true;
		}
		if (MenuMouse.Clicked(scopeRects[1]) && !viewAll)
		{
			flag5 = true;
		}
		if (MenuMouse.Clicked(pickL))
		{
			flag3 = true;
		}
		if (MenuMouse.Clicked(pickR))
		{
			flag4 = true;
		}
		float num2 = MenuMouse.Wheel();
		if (num2 > 0.1f)
		{
			flag3 = true;
		}
		else if (num2 < -0.1f)
		{
			flag4 = true;
		}
		if (flag8 || flag9)
		{
			viewTab = (viewTab + (flag8 ? 1 : (TabNames.Length - 1))) % TabNames.Length;
			viewIndex = 0;
			Sfx.Play(Sounds.MenuRolloverDown.Id);
		}
		int num3 = ViewCount();
		if (viewTab <= 2 || viewTab >= 4)
		{
			foreach (KeyValuePair<Rect, int> rowHit in rowHits)
			{
				if (rowHit.Value != viewIndex && MenuMouse.Clicked(rowHit.Key))
				{
					viewIndex = rowHit.Value;
					Sfx.Play(Sounds.MenuRolloverDown.Id);
					break;
				}
			}
		}
		if (flag3 && num3 > 0)
		{
			viewIndex = (viewIndex + num3 - 1) % num3;
			Sfx.Play(Sounds.MenuRolloverUp.Id);
		}
		if (flag4 && num3 > 0)
		{
			viewIndex = (viewIndex + 1) % num3;
			Sfx.Play(Sounds.MenuRolloverDown.Id);
		}
		if (flag5)
		{
			viewAll = !viewAll;
			viewIndex = 0;
			Sfx.Play(Sounds.MenuToggleOn.Id);
		}
	}

	private void TrackArea(HeroController h, GameManager gm, bool playing, float dt)
	{
		string text = null;
		MapZone mapZone = MapZone.NONE;
		try
		{
			mapZone = gm.GetCurrentMapZoneEnum();
			text = mapZone.ToString();
		}
		catch (Exception)
		{
		}
		bool dead = h.cState.dead;
		bool hazardDeath = h.cState.hazardDeath;
		if (curZone != null && ((dead && !wasDead) || (hazardDeath && !wasHazard)))
		{
			string key = Key(gm.profileID, curZone);
			areaDeaths.TryGetValue(key, out var value);
			areaDeaths[key] = value + 1;
		}
		wasHazard = hazardDeath;
		if (text == null || text == "NONE")
		{
			return;
		}
		if (!areaNames.ContainsKey(text))
		{
			try
			{
				string text2 = Clean(GameManager.GetFormattedMapZoneStringV2(mapZone) ?? "");
				areaNames[text] = ((text2.Length > 0) ? text2 : Pretty(text));
			}
			catch (Exception)
			{
				areaNames[text] = Pretty(text);
			}
		}
		string key2 = Key(gm.profileID, text);
		if (text != curZone)
		{
			bool num = !areaTime.ContainsKey(key2);
			if (num)
			{
				areaTime[key2] = 0f;
			}
			if (num)
			{
				bannerZone = text;
				bannerNew = true;
				bannerT = 0f;
			}
			curZone = text;
			Save();
		}
		if (playing && !h.controlReqlinquished && !dead && Time.timeScale > 0f)
		{
			areaTime[key2] += dt;
		}
		if ((areaSaveIn -= dt) <= 0f)
		{
			areaSaveIn = 10f;
			Save();
		}
		if (bannerT >= 0f && phase == Phase.None && !viewing && !AssistMode.IsOpen && !h.controlReqlinquished && PostcardView.TutorialDone && (bannerT += dt) > 4f)
		{
			bannerT = -1f;
		}
	}

	private string AreaName(string zone)
	{
		if (!areaNames.TryGetValue(zone, out var value))
		{
			value = Pretty(zone);
		}
		// a name the game has no text for comes through as its raw key ("#!#CORAL_CAVERNS#!#"): tidy it up
		if (value != null && value.Contains("#!#"))
		{
			value = Pretty(value.Replace("#!#", "").Trim());
		}
		return value;
	}

	private List<KeyValuePair<string, KeyValuePair<float, int>>> AreaList(int slot)
	{
		Dictionary<string, KeyValuePair<float, int>> dictionary = new Dictionary<string, KeyValuePair<float, int>>();
		foreach (KeyValuePair<string, float> item in areaTime)
		{
			int num = item.Key.IndexOf('|');
			if (viewAll || !(item.Key.Substring(0, num) != slot.ToString(CultureInfo.InvariantCulture)))
			{
				string key = item.Key.Substring(num + 1);
				areaDeaths.TryGetValue(item.Key, out var value);
				dictionary.TryGetValue(key, out var value2);
				dictionary[key] = new KeyValuePair<float, int>(value2.Key + item.Value, value2.Value + value);
			}
		}
		List<KeyValuePair<string, KeyValuePair<float, int>>> list = new List<KeyValuePair<string, KeyValuePair<float, int>>>(dictionary);
		list.Sort((KeyValuePair<string, KeyValuePair<float, int>> a, KeyValuePair<string, KeyValuePair<float, int>> b) => string.Compare(AreaName(a.Key), AreaName(b.Key), StringComparison.OrdinalIgnoreCase));
		return list;
	}

	private void DrawAreaBanner()
	{
		HeroController silentInstance = HeroController.UnsafeInstance;
		if (bannerT < 0f || bannerZone == null || phase != Phase.None || viewing || AssistMode.IsOpen || silentInstance == null || silentInstance.controlReqlinquished || !PostcardView.TutorialDone)
		{
			return;
		}
		GameManager silentInstance2 = GameManager.SilentInstance;
		if (!(silentInstance2 == null))
		{
			float num = 4f;
			float value = ((bannerT < 0.4f) ? (bannerT / 0.4f) : ((bannerT > num - 0.5f) ? ((num - bannerT) / 0.5f) : 1f));
			value = Mathf.Clamp01(value);
			float num2 = 1f - (1f - value) * (1f - value) * (1f - value);
			float num3 = (float)Screen.height / 1080f;
			float num4 = (float)Screen.width / num3;
			Matrix4x4 matrix = GUI.matrix;
			GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num3, num3, 1f));
			GuiArt.Img img = Art.Image("areaselect/title");
			GuiArt.Img img2 = Art.Image("areaselect/accent");
			string line = AreaName(bannerZone);
			float num5 = 1.1f;
			while (num5 > 0.5f && Art.Measure(line) * num5 > 760f)
			{
				num5 -= 0.05f;
			}
			float num6 = Math.Max(640f, Art.Measure(line) * num5 + 200f);
			float num7 = num6 * 173f / 916f;
			float x = num4 / 2f - num6 / 2f;
			float num8 = 40f - (1f - num2) * 220f;
			Color c = Hex("8C1C2E");
			c.a = num2;
			Color c2 = Hex("C8465A");
			c2.a = num2;
			if (img != null)
			{
				GuiArt.Draw(img, new Rect(x, num8, num6, num7), c);
			}
			if (img2 != null)
			{
				GuiArt.Draw(img2, new Rect(x, num8, num6, num7), c2);
			}
			Color color = new Color(1f, 1f, 1f, num2);
			Art.DrawLine(line, num4 / 2f - Art.Measure(line) * num5 / 2f + 3f, num8 + num7 / 2f - (float)Art.LineHeight * num5 / 2f + 3f, num5, new Color(0f, 0f, 0f, 0.35f * num2));
			Art.DrawLine(line, num4 / 2f - Art.Measure(line) * num5 / 2f, num8 + num7 / 2f - (float)Art.LineHeight * num5 / 2f, num5, color);
			string key = Key(silentInstance2.profileID, bannerZone);
			areaTime.TryGetValue(key, out var value2);
			areaDeaths.TryGetValue(key, out var value3);
			string line2 = (bannerNew ? "New Area" : ("Deaths " + value3.ToString(CultureInfo.InvariantCulture) + "     Time " + FormatTime(value2)));
			float num9 = 0.6f;
			float num10 = num8 + num7 + 6f;
			GuiArt.Img img3 = Art.Image("collectables/skullGold") ?? Art.Skull;
			float num11 = Art.Measure(line2) * num9;
			float num12 = num4 / 2f - num11 / 2f;
			if (!bannerNew && img3 != null)
			{
				GuiArt.Draw(img3, new Rect(num12 - 50f, num10, 40f, 40f), color);
			}
			Art.DrawOutlined(line2, num12, num10, num9, bannerNew ? new Color(1f, 0.9f, 0.55f, num2) : color, new Color(0f, 0f, 0f, num2), 2f);
			GUI.matrix = matrix;
			GUI.color = Color.white;
		}
	}

	// ---- Records: drawn as Celeste's journal ----
	// Lists are rebuilt at most once a second (or when the save/scope changes), the boss portrait
	// is looked up once per boss, and only the rows on the page are drawn, so the journal costs
	// about what the old screen did.

	private List<KeyValuePair<string, Record>> bossCache;

	private List<KeyValuePair<string, KeyValuePair<float, int>>> areaCache;

	private float cacheAt = -10f;

	private int cacheSlot = -1;

	private bool cacheAll;

	private int cacheGoldens;

	private int cacheDeaths;

	private int cacheAchieved;

	private readonly Dictionary<string, Sprite> portraits = new Dictionary<string, Sprite>();

	private readonly List<KeyValuePair<Rect, int>> rowHits = new List<KeyValuePair<Rect, int>>();

	private float jScroll;

	private float jSelY = -1f;

	private int jTabShown = -1;

	private float jTabAt;

	private float jOpenAt;

	private bool jWasOpen;

	private static readonly string[] TabColors = new string[7] { "C2414B", "3F7FBF", "D9A21B", "3E9B5A", "B5653A", "7A4FA8", "2A9D9A" };

	private static readonly Color Ink = new Color(0.17f, 0.14f, 0.24f, 1f);

	private static readonly Color InkSoft = new Color(0.36f, 0.32f, 0.42f, 1f);

	private void RefreshLists(int slot, bool force)
	{
		if (!force && bossCache != null && slot == cacheSlot && viewAll == cacheAll && Time.unscaledTime - cacheAt < 1f)
		{
			return;
		}
		cacheAt = Time.unscaledTime;
		cacheSlot = slot;
		cacheAll = viewAll;
		bossCache = ViewList(slot);
		areaCache = AreaList(slot);
		cacheBeaten = bossCache.FindAll((KeyValuePair<string, Record> r) => ActOfKey(r.Key) > 0).Count;
		cacheGoldens = 0;
		HashSet<string> hashSet = new HashSet<string>();
		string toughest = null;
		int toughestDeaths = 0;
		string fastest = null;
		float fastestTime = float.MaxValue;
		foreach (KeyValuePair<string, Record> item in bossCache)
		{
			if (viewAll ? HasGolden(item.Key) : goldens.Contains(Key(slot, item.Key)))
			{
				cacheGoldens++;
			}
			if (journal.TryGetValue(item.Key, out var value))
			{
				hashSet.Add(value);
			}
			if (item.Value.Deaths > toughestDeaths)
			{
				toughestDeaths = item.Value.Deaths;
				toughest = TitleOf(item.Key);
			}
			if (item.Value.Time > 0f && item.Value.Time < fastestTime)
			{
				fastestTime = item.Value.Time;
				fastest = TitleOf(item.Key);
			}
		}
		EnsureAllBosses();
		if (allBossJournals != null)
		{
			foreach (string allBossJournal in allBossJournals)
			{
				if (!hashSet.Contains(allBossJournal))
				{
					bossCache.Add(new KeyValuePair<string, Record>("unseen:" + allBossJournal, new Record
					{
						Time = -2f,
						Deaths = -1,
						Dashes = -1,
						Hits = -1,
						Heals = -1
					}));
				}
			}
		}
		// Silksong has 43 distinct bosses (the community wiki's count, rematches not counted
		// twice), so 43 bosses to beat and 43 golden berries to earn. Bosses the mod can't name
		// from the game's own flags still get a mystery row each.
		int n = 1;
		while (bossCache.Count < TotalBosses)
		{
			bossCache.Add(new KeyValuePair<string, Record>("unseen:#" + n++, new Record
			{
				Time = -2f,
				Deaths = -1,
				Dashes = -1,
				Hits = -1,
				Heals = -1
			}));
		}
		cacheTotalBosses = Math.Max(TotalBosses, cacheBeaten);
		cacheDeaths = 0;
		cacheKills = 0;
		float num = 0f;
		string deadliest = null;
		int deadliestN = 0;
		string killiest = null;
		int killiestN = 0;
		foreach (KeyValuePair<string, KeyValuePair<float, int>> item2 in areaCache)
		{
			int num2 = AreaKills(slot, item2.Key);
			cacheDeaths += item2.Value.Value;
			cacheKills += num2;
			num += item2.Value.Key;
			if (item2.Value.Value > deadliestN)
			{
				deadliestN = item2.Value.Value;
				deadliest = AreaName(item2.Key);
			}
			if (num2 > killiestN)
			{
				killiestN = num2;
				killiest = AreaName(item2.Key);
			}
		}
		statDeadliest = ((deadliest != null) ? (deadliest + " (" + deadliestN + ")") : "-");
		statKilliest = ((killiest != null) ? (killiest + " (" + killiestN + ")") : "-");
		statToughest = ((toughest != null) ? (toughest + " (" + toughestDeaths + ")") : "-");
		statFastest = ((fastest != null) ? (fastest + " (" + FormatTime(fastestTime) + ")") : "-");
		statExplored = FormatTime(num);
		cacheAchieved = 0;
		Achievements.Def[] all = Achievements.All;
		for (int i = 0; i < all.Length; i++)
		{
			if (Achievements.Unlocked(all[i], out var _))
			{
				cacheAchieved++;
			}
		}
	}

	// the bosses Records counts: those with an act, Skull Tyrant left out (user 2026-10-17)
	private static int TotalBosses => BossRematch.KnownBossCount;

	private int cacheBeaten;

	private int cacheTotalBosses;

	private int cacheKills;

	private string statDeadliest = "-";

	private string statKilliest = "-";

	private string statToughest = "-";

	private string statFastest = "-";

	private string statExplored = "-";

	private List<string> allBossJournals;

	// Every boss Silksong tracks a "defeated" flag for that has a Hunter's Journal entry:
	// the bosses there are to beat, and so the golden berries there are to earn.
	private void EnsureAllBosses()
	{
		if (allBossJournals != null || defeatFields == null || defeatFields.Count == 0)
		{
			return;
		}
		HashSet<string> hashSet = new HashSet<string>();
		List<string> list = new List<string>();
		foreach (FieldInfo defeatField in defeatFields)
		{
			if (defeatField.Name.StartsWith("defeated", StringComparison.OrdinalIgnoreCase))
			{
				string text = JournalFor(FlagTitle(defeatField.Name));
				if (text != null && !Hidden(text) && hashSet.Add(text))
				{
					list.Add(text);
				}
			}
		}
		allBossJournals = list;
		Plugin.Log.LogInfo("Records: " + list.Count + " bosses in the game");
	}

	private int AreaKills(int slot, string zone)
	{
		int num = 0;
		foreach (KeyValuePair<string, int> areaKill in areaKills)
		{
			int num2 = areaKill.Key.IndexOf('|');
			if (num2 >= 0 && areaKill.Key.Substring(num2 + 1) == zone && (viewAll || areaKill.Key.Substring(0, num2) == slot.ToString(CultureInfo.InvariantCulture)))
			{
				num += areaKill.Value;
			}
		}
		return num;
	}

	private static string Kdr(int kills, int deaths)
	{
		if (deaths != 0)
		{
			return ((float)kills / (float)deaths).ToString("0.0", CultureInfo.InvariantCulture);
		}
		if (kills <= 0)
		{
			return "-";
		}
		return kills + ".0";
	}

	private int ViewCount()
	{
		GameManager silentInstance = GameManager.SilentInstance;
		if (silentInstance == null)
		{
			return 0;
		}
		RefreshLists(silentInstance.profileID, force: false);
		if (viewTab == 4)
		{
			return PostcardView.Album().Count;
		}
		if (viewTab == 5)
		{
			Album.Refresh();
			return Album.Photos.Count;
		}
		if (viewTab == 6)
		{
			return RunLog.Of(silentInstance.profileID).Count;
		}
		if (viewTab != 1)
		{
			if (viewTab == 2)
			{
				return 12;
			}
			if (viewTab != 0)
			{
				return 0;
			}
			return BossList().Count;
		}
		return areaCache.Count;
	}

	private Sprite PortraitOf(string key)
	{
		if (portraits.TryGetValue(key, out var value))
		{
			return value;
		}
		value = null;
		try
		{
			string value2 = null;
			if (key.StartsWith("unseen:#", StringComparison.Ordinal))
			{
				portraits[key] = null;
				return null;
			}
			if (key.StartsWith("unseen:", StringComparison.Ordinal))
			{
				value2 = key.Substring(7);
			}
			if (value2 != null || journal.TryGetValue(key, out value2))
			{
				EnemyJournalRecord record = EnemyJournalManager.GetRecord(value2);
				if (record != null)
				{
					value = record.EnemySprite ?? record.IconSprite;
				}
			}
			if (value == null && !key.StartsWith("unseen:", StringComparison.Ordinal))
			{
				// no journal match by exact name (Great Conchflies vs the journal's "Great Conchfly"): take the journal
				// page whose name shares the longest start with the boss's, if it's nearly all of it
				string t = Norm(TitleOf(key));
				int bestLen = 0;
				foreach (EnemyJournalRecord r in EnemyJournalManager.GetAllEnemies())
				{
					if (r == null || (r.EnemySprite == null && r.IconSprite == null))
					{
						continue;
					}
					string d = Norm(r.DisplayName.ToString());
					int n = 0;
					while (n < d.Length && n < t.Length && d[n] == t[n])
					{
						n++;
					}
					if (n > bestLen && n >= d.Length - 2 && n >= t.Length - 3 && n >= 5)
					{
						bestLen = n;
						value = r.EnemySprite ?? r.IconSprite;
						Plugin.Log.LogInfo("Records: " + key + " (" + t + ") looks like journal page " + r.name);
					}
				}
				if (value != null)
				{
					Plugin.Log.LogInfo("Records: portrait for " + key + " found by name");
				}
			}
		}
		catch (Exception)
		{
		}
		portraits[key] = value;
		return value;
	}

	private static float Ease(float t)
	{
		t = Mathf.Clamp01(t);
		return 1f - (1f - t) * (1f - t) * (1f - t);
	}

	private static Color A(Color c, float a)
	{
		c.a *= a;
		return c;
	}

	private void Ink2(string s, float x, float y, float scale, Color c)
	{
		Art.DrawLine(s, x, y, scale, c);
	}

	private float Fit(string s, float scale, float width)
	{
		float num = Art.Measure(s) * scale;
		if (!(num > width) || !(num > 0f))
		{
			return scale;
		}
		return scale * width / num;
	}

	private void DrawViewer()
	{
		GameManager silentInstance = GameManager.SilentInstance;
		if (silentInstance == null)
		{
			return;
		}
		if (viewing && !jWasOpen)
		{
			jOpenAt = Time.unscaledTime;
			actFilter = 0;   // every time Records opens, the Bosses list starts on All
			jScroll = 0f;
			jSelY = -1f;
			RefreshLists(silentInstance.profileID, force: true);
		}
		jWasOpen = viewing;
		RefreshLists(silentInstance.profileID, force: false);
		if (jTabShown != viewTab)
		{
			jTabShown = viewTab;
			jTabAt = Time.unscaledTime;
			jScroll = 0f;
			jSelY = -1f;
		}
		float num = (float)Screen.height / 1080f;
		float num2 = (float)Screen.width / num;
		float num3 = viewT;
		float num4 = Ease(num3);
		Matrix4x4 matrix = GUI.matrix;
		GUI.depth = -1;
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num, num, 1f));
		// the mountain sky from the file select behind the journal, dimmed, with a soft vignette (2026-10-17)
		CelesteFileSelect.Background(num2, num3);
		GUI.color = new Color(0.03f, 0.02f, 0.06f, 0.55f * num3);
		GUI.DrawTexture(new Rect(0f, 0f, num2, 1080f), Texture2D.whiteTexture);
		GUI.color = new Color(0f, 0f, 0f, 0.5f * num3);
		GUI.DrawTexture(new Rect(0f, 0f, num2, 140f), CelesteMenus.MenuFx.FadeIn);
		float num5 = Math.Min(num2 - 120f, 1640f);
		float num6 = Math.Min(num5 * 1010f / 1800f, 860f);
		float num7 = num2 / 2f - num5 / 2f;
		float num8 = 120f + (1f - num4) * 90f;
		Rect rect = new Rect(num7, num8, num5, num6);
		GUIUtility.RotateAroundPivot(-0.5f * num4, rect.center * num);
		DrawTabs(rect, num3);
		GUI.color = new Color(0f, 0f, 0f, 0.35f * num3);
		GUI.DrawTexture(new Rect(rect.x + 14f, rect.y + 18f, rect.width, rect.height), Texture2D.whiteTexture);
		GuiArt.Img img = Art.Image("journal/page");
		if (img != null)
		{
			GuiArt.Draw(img, rect, new Color(1f, 1f, 1f, num3));
		}
		else
		{
			GUI.color = new Color(0.95f, 0.92f, 0.85f, num3);
			GUI.DrawTexture(rect, Texture2D.whiteTexture);
		}
		// Celeste's page art leaves its right ~11% blank; keep everything on the paper.
		float paperRight = rect.x + rect.width * 0.885f;
		Tape(new Vector2(rect.x + 30f, rect.y + 22f), -38f, num3);
		Tape(new Vector2(paperRight - 10f, rect.y + rect.height - 26f), -32f, num3);
		// Madeline herself, in your look, sitting on the top edge of the journal with her legs over the side
		VariantPreview.Pose("edge", new Vector2(paperRight - 90f, rect.y + 34f), 5.5f, flip: true, num3 * Mathf.Clamp01((Time.unscaledTime - jOpenAt - 0.25f) / 0.4f));
		float num9 = rect.x + 70f;
		float num10 = paperRight - 60f;
		float num11 = rect.y + 40f;
		Color ink = A(Ink, num3);
		Color inkSoft = A(InkSoft, num3);
		Ink2("RECORDS", num9, num11, 0.95f, ink);
		string line = TabNames[viewTab];
		Color tabTint = Hex(TabColors[viewTab]);
		float lx = num9 + Art.Measure("RECORDS") * 0.95f + 26f;
		GUI.color = new Color(tabTint.r, tabTint.g, tabTint.b, 0.9f * num3);
		GUI.DrawTexture(new Rect(lx - 6f, num11 + 12f, Art.Measure(line) * 0.55f + 24f, (float)Art.LineHeight * 0.55f + 8f), Texture2D.whiteTexture);
		Ink2(line, lx + 6f, num11 + 14f, 0.55f, new Color(1f, 1f, 1f, num3));
		float flourish = Art.Measure("RECORDS") * 0.95f * Ease((Time.unscaledTime - jOpenAt) / 0.6f);
		GUI.color = new Color(0.85f, 0.6f, 0.12f, num3);
		GUI.DrawTexture(new Rect(num9, num11 + (float)Art.LineHeight * 0.95f - 6f, flourish, 4f), Texture2D.whiteTexture);
		if (!fromMenu)
		{
			DrawScope(num10, num11 + 6f, num3);
		}
		else
		{
			scopeRects[0] = (scopeRects[1] = default(Rect));
		}
		if (AssistMode.SaveMarked && !viewAll && !fromMenu)
		{
			string text = "ASSIST SAVE";
			float num12 = Art.Measure(text) * 0.4f + 30f;
			Rect r = new Rect(num10 - num12, num11 + 62f, num12, 30f);
			GUI.color = new Color(0.85f, 0.65f, 0.15f, 0.9f * num3);
			GUI.DrawTexture(r, Texture2D.whiteTexture);
			Ink2(text, r.x + 15f, r.y + 2f, 0.4f, new Color(1f, 1f, 1f, num3));
		}
		float num13 = num11 + (float)Art.LineHeight * 0.95f + 10f;
		GUI.color = A(InkSoft, 0.5f * num3);
		GUI.DrawTexture(new Rect(num9, num13, num10 - num9, 3f), Texture2D.whiteTexture);
		num13 += 18f;
		if (viewTab <= 2)
		{
			num13 = DrawSummary(num9, num10, num13, num3);
		}
		float num14 = Ease((Time.unscaledTime - jTabAt) / 0.25f);
		Rect area = new Rect(num9 + (1f - num14) * 40f, num13 + 10f, num10 - num9, rect.yMax - 50f - (num13 + 10f));
		float a = num3 * num14;
		rowHits.Clear();
		pickL = (pickR = default(Rect));
		switch (viewTab)
		{
		case 0:
			DrawBossPage(area, silentInstance.profileID, a);
			break;
		case 1:
			DrawAreaPage(area, silentInstance.profileID, a);
			break;
		case 2:
			DrawStatsPage(area, a);
			break;
		case 4:
			DrawPostcardPage(area, a);
			break;
		case 5:
			DrawPhotoPage(area, a);
			break;
		case 6:
			DrawRunPage(area, silentInstance.profileID, a);
			break;
		default:
			Achievements.DrawJournal(new Rect(area.x, area.y, area.width, rect.yMax - 40f - area.y), a);
			break;
		}
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num, num, 1f));
		string text2 = ((viewTab == 0) ? "boss" : "area");
		string hint = (viewPad ? (((viewTab == 2) ? "Up/Down read a stat   " : ("Up/Down " + text2 + "   ")) + "Left/Right this save or all saves   L1/R1 pages   Circle to close") : (((viewTab == 2) ? "Up/Down or point at a stat   " : ("Up/Down, wheel or click " + text2 + "   ")) + "Left/Right this save or all saves   Q/E pages   " + AssistMode.KeyName(AssistOptions.KeyRecords) + " or Esc to close"));
		if (viewTab >= 4)
		{
			string what = viewTab == 4 ? "postcard" : (viewTab == 5 ? "photo" : "scroll");
			hint = viewPad ? ("Up/Down " + what + "   L1/R1 pages   Circle to close") : ("Up/Down, wheel or click " + what + "   Q/E pages   " + AssistMode.KeyName(AssistOptions.KeyRecords) + " or Esc to close");
		}
		if (viewTab == 3)
		{
			hint = (viewPad ? "D-pad choose   Square reset   Triangle sort   L1/R1 pages   Circle to close" : ("Arrows or mouse choose   R or right-click reset   Tab sort   Q/E pages   " + AssistMode.KeyName(AssistOptions.KeyRecords) + " or Esc to close"));
		}
		if (fromMenu)
		{
			hint = hint.Replace("Left/Right this save or all saves   ", "");
		}
		MenuStyle.Hint(hint, num2 / 2f, 1040f, num3);
		GUI.matrix = matrix;
		GUI.color = Color.white;
	}

	// ---- Run page (user 2026-10-17): this save's story as a timeline, newest first ----------------------------
	private void DrawRunPage(Rect area, int slot, float alpha)
	{
		List<RunLog.Entry> all = RunLog.Of(slot);
		if (all.Count == 0)
		{
			string t1 = "Your run's story starts here.";
			string t2 = "Beat a boss, earn a golden berry or find a new ability, and it's written down with the time.";
			Ink2(t1, area.center.x - Art.Measure(t1) * 0.7f / 2f, area.y + 120f, 0.7f, A(Ink, alpha));
			float s2 = Fit(t2, 0.42f, area.width - 40f);
			Ink2(t2, area.center.x - Art.Measure(t2) * s2 / 2f, area.y + 120f + (float)Art.LineHeight * 0.7f + 10f, s2, A(InkSoft, alpha));
			return;
		}
		List<RunLog.Entry> list = new List<RunLog.Entry>(all);
		list.Reverse();
		int bosses = list.FindAll((RunLog.Entry e) => e.Kind == "boss").Count;
		int berries = list.FindAll((RunLog.Entry e) => e.Kind == "golden").Count;
		int skills = list.FindAll((RunLog.Entry e) => e.Kind == "ability").Count;
		string head = bosses + " bosses   " + berries + " golden berries   " + skills + " abilities";
		Ink2(head, area.x, area.y, 0.42f, A(InkSoft, alpha));
		float rowH = 64f;
		float top = area.y + 50f;
		int fit = Math.Max(1, (int)((area.yMax - top) / rowH));
		viewIndex = Mathf.Clamp(viewIndex, 0, Math.Max(0, list.Count - fit));
		float lineX = area.x + 190f;
		Color teal = Hex(TabColors[6]);
		GUI.color = new Color(teal.r, teal.g, teal.b, 0.5f * alpha);
		GUI.DrawTexture(new Rect(lineX - 2f, top, 4f, Math.Min(list.Count - viewIndex, fit) * rowH - 20f), Texture2D.whiteTexture);
		GuiArt.Img berry = PixelBerry() ?? Art.Image("collectables/goldberry");
		for (int i = 0; i < fit && viewIndex + i < list.Count; i++)
		{
			RunLog.Entry e = list[viewIndex + i];
			float y = top + i * rowH;
			float a = alpha * Mathf.Clamp01((Time.unscaledTime - jTabAt) * 6f - i * 0.25f);   // rows appear one after another
			string when = FormatTime(e.Time);
			Ink2(when, lineX - 30f - Art.Measure(when) * 0.45f, y + 4f, 0.45f, A(InkSoft, a));
			Color dot = e.Kind == "golden" ? new Color(0.92f, 0.7f, 0.15f) : (e.Kind == "boss" ? Hex(TabColors[0]) : teal);
			GUI.color = new Color(dot.r, dot.g, dot.b, a);
			GUI.DrawTexture(new Rect(lineX - 11f, y + 8f, 22f, 22f), CelesteMenus.MenuFx.Glow);
			GUI.DrawTexture(new Rect(lineX - 7f, y + 12f, 14f, 14f), Texture2D.whiteTexture);
			string what = e.Kind == "boss" ? ("Beat " + e.Text) : (e.Kind == "golden" ? ("Golden berry: " + e.Text) : ("Found " + e.Text));
			float tx = lineX + 34f;
			if (e.Kind == "golden" && berry != null)
			{
				GuiArt.Draw(berry, new Rect(tx, y + 2f, 36f, 36f), new Color(1f, 1f, 1f, a));
				tx += 46f;
			}
			Ink2(what, tx, y + 2f, Fit(what, 0.55f, area.xMax - tx - 20f), A(Ink, a));
		}
		if (viewIndex > 0)
		{
			Ink2("^", lineX - 6f, top - 34f, 0.4f, A(InkSoft, alpha));
		}
		if (viewIndex + fit < list.Count)
		{
			Ink2("v", lineX - 6f, area.yMax - 10f, 0.4f, A(InkSoft, alpha));
		}
	}

	// ---- Postcards page (user 2026-10-17): every card as a journal list, the chosen one pinned up on the right ----
	private void DrawPostcardPage(Rect area, float alpha)
	{
		PostcardRow[] all = Postcards.All;
		if (all.Length == 0)
		{
			return;
		}
		List<PostcardRow> found = PostcardView.Album();
		viewIndex = Mathf.Clamp(viewIndex, 0, all.Length - 1);
		float listW = area.width * 0.36f;
		float rowH = 58f;
		float y = area.y + 6f;
		string head = found.Count + " of " + all.Length + " found";
		Ink2(head, area.x, y, 0.45f, A(InkSoft, alpha));
		y += (float)Art.LineHeight * 0.45f + 14f;
		Vector2 mouse = MenuMouse.Gui();
		for (int i = 0; i < all.Length; i++)
		{
			bool has = found.Exists((PostcardRow r) => r.Id == all[i].Id);
			bool on = i == viewIndex;
			Rect row = new Rect(area.x, y, listW, rowH - 8f);
			rowHits.Add(new KeyValuePair<Rect, int>(row, i));
			if (on)
			{
				GUI.color = A(Ink, 0.88f * alpha);
				GUI.DrawTexture(row, Texture2D.whiteTexture);
			}
			else if (!viewPad && row.Contains(mouse))
			{
				GUI.color = A(InkSoft, 0.12f * alpha);
				GUI.DrawTexture(row, Texture2D.whiteTexture);
			}
			// a little stamp in the card's own colour, with a perforated white edge
			Color stamp = Color.HSVToRGB(Mathf.Repeat(0.03f + i * 0.13f, 1f), has ? 0.55f : 0.05f, has ? 0.85f : 0.6f);
			Rect st = new Rect(row.x + 12f, row.y + 7f, 36f, 36f);
			GUI.color = new Color(1f, 1f, 1f, alpha);
			GUI.DrawTexture(st, Texture2D.whiteTexture);
			GUI.color = A(stamp, alpha);
			GUI.DrawTexture(new Rect(st.x + 4f, st.y + 4f, st.width - 8f, st.height - 8f), Texture2D.whiteTexture);
			if (!has)
			{
				Ink2("?", st.center.x - Art.Measure("?") * 0.45f / 2f, st.y + 2f, 0.45f, new Color(1f, 1f, 1f, alpha));
			}
			string name = has ? all[i].Title : "???";
			float sc = Fit(name, 0.5f, listW - 80f);
			Color nc = on ? new Color(1f, 1f, 1f, alpha) : (has ? A(Ink, alpha) : A(InkSoft, 0.6f * alpha));
			Ink2(name, row.x + 62f, row.y + (row.height - (float)Art.LineHeight * sc) / 2f, sc, nc);
			y += rowH;
		}
		// the chosen card, pinned to the page with a slight tilt and a shadow
		PostcardRow card = all[viewIndex];
		bool seen = found.Exists((PostcardRow r) => r.Id == card.Id);
		Rect right = new Rect(area.x + listW + 50f, area.y, area.xMax - area.x - listW - 50f, area.height);
		if (Art.Postcard == null || Art.Postcard.Tex == null)
		{
			return;
		}
		float k = Mathf.Min((right.width - 30f) / (float)Art.Postcard.W, (right.height - 110f) / (float)Art.Postcard.H);
		Vector2 c = new Vector2(right.center.x, right.y + 20f + (float)Art.Postcard.H * k / 2f);
		float bob = Mathf.Sin(Time.unscaledTime * 1.3f) * 2f;
		c.y += bob;
		float pw = (float)Art.Postcard.W * k;
		float ph = (float)Art.Postcard.H * k;
		Matrix4x4 m0 = GUI.matrix;
		GUIUtility.RotateAroundPivot(-1.5f + (viewIndex % 3 - 1) * 1.2f, c);
		GUI.color = new Color(0f, 0f, 0f, 0.25f * alpha);
		GUI.DrawTexture(new Rect(c.x - pw / 2f + 10f, c.y - ph / 2f + 14f, pw, ph), Texture2D.whiteTexture);
		if (seen)
		{
			PostcardView.DrawAlbumCard(card, c, k, alpha);
		}
		else
		{
			// the card's back: blank paper, a faded stamp box and a question mark
			GUI.color = new Color(0.93f, 0.9f, 0.84f, alpha);
			GUI.DrawTexture(new Rect(c.x - pw / 2f, c.y - ph / 2f, pw, ph), Texture2D.whiteTexture);
			GUI.color = A(InkSoft, 0.35f * alpha);
			GUI.DrawTexture(new Rect(c.x + pw / 2f - 110f * k * 2f, c.y - ph / 2f + 40f * k * 2f, 80f * k * 2f, 90f * k * 2f), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(c.x + 10f, c.y - ph * 0.3f, 3f, ph * 0.6f), Texture2D.whiteTexture);
			Ink2("?", c.x - pw / 4f - Art.Measure("?") * 2f / 2f, c.y - (float)Art.LineHeight, 2f, A(InkSoft, 0.6f * alpha));
		}
		GUI.matrix = m0;
		// a red pushpin holding it up
		Vector2 pin = new Vector2(c.x, c.y - ph / 2f + 6f);
		GUI.color = new Color(0f, 0f, 0f, 0.3f * alpha);
		GUI.DrawTexture(new Rect(pin.x - 9f, pin.y - 5f, 22f, 22f), CelesteMenus.MenuFx.Glow);
		GUI.color = new Color(0.85f, 0.18f, 0.22f, alpha);
		GUI.DrawTexture(new Rect(pin.x - 13f, pin.y - 13f, 26f, 26f), CelesteMenus.MenuFx.Glow);
		GUI.DrawTexture(new Rect(pin.x - 8f, pin.y - 8f, 16f, 16f), CelesteMenus.MenuFx.Glow);
		GUI.color = new Color(1f, 0.85f, 0.85f, 0.9f * alpha);
		GUI.DrawTexture(new Rect(pin.x - 5f, pin.y - 7f, 6f, 6f), CelesteMenus.MenuFx.Glow);
		// what the card is for, underneath
		string note = !seen ? (card.Unlock == "none" ? "Not found yet" : "Get " + UnlockName(card.Unlock) + " to find this card") : (card.Unlock == "none" ? "From the start of your journey" : "Found with " + UnlockName(card.Unlock));
		float ns = Fit(note, 0.5f, right.width - 20f);
		Ink2(note, right.center.x - Art.Measure(note) * ns / 2f, c.y + ph / 2f + 30f, ns, A(InkSoft, alpha));
		GUI.color = Color.white;
	}

	private static string UnlockName(string id)
	{
		UnlockRow[] all = Unlocks.All;
		for (int i = 0; i < all.Length; i++)
		{
			if (all[i].Id == id)
			{
				return all[i].SilksongUnlock;
			}
		}
		return id;
	}

	// ---- Photos page (user 2026-10-17): the chosen photo as a big taped-in polaroid, a filmstrip of the rest ----
	private void DrawPhotoPage(Rect area, float alpha)
	{
		Album.Refresh();
		int n = Album.Photos.Count;
		if (n == 0)
		{
			string a1 = "No photos yet.";
			string a2 = "Press " + AssistMode.KeyName(AssistOptions.KeyCamera) + " in the game to take one with Theo's camera.";
			Ink2(a1, area.center.x - Art.Measure(a1) * 0.8f / 2f, area.y + 140f, 0.8f, A(Ink, alpha));
			float s2 = Fit(a2, 0.5f, area.width - 40f);
			Ink2(a2, area.center.x - Art.Measure(a2) * s2 / 2f, area.y + 140f + (float)Art.LineHeight * 0.8f + 16f, s2, A(InkSoft, alpha));
			return;
		}
		viewIndex = Mathf.Clamp(viewIndex, 0, n - 1);
		float stripW = Mathf.Min(260f, area.width * 0.24f);
		Rect main = new Rect(area.x, area.y, area.width - stripW - 40f, area.height);
		// the big photo, a little tilted, white polaroid frame, two strips of tape
		Texture2D pic = Album.Get(viewIndex);
		float tilt = (viewIndex % 2 == 0) ? -1.6f : 1.3f;
		if (pic != null)
		{
			float bottom = 70f;
			float k = Mathf.Min((main.width - 80f) / (float)pic.width, (main.height - bottom - 60f) / (float)pic.height);
			float w = (float)pic.width * k;
			float h = (float)pic.height * k;
			Vector2 c = new Vector2(main.center.x, main.y + 30f + (h + bottom) / 2f);
			Matrix4x4 m0 = GUI.matrix;
			GUIUtility.RotateAroundPivot(tilt, c);
			Rect frame = new Rect(c.x - w / 2f - 18f, c.y - (h + bottom) / 2f - 18f, w + 36f, h + bottom + 36f);
			GUI.color = new Color(0f, 0f, 0f, 0.28f * alpha);
			GUI.DrawTexture(new Rect(frame.x + 12f, frame.y + 16f, frame.width, frame.height), Texture2D.whiteTexture);
			// each photo gets its own frame colour, like a pack of coloured polaroids
			Color[] frames = new Color[5] { new Color(0.98f, 0.97f, 0.94f), new Color(1f, 0.88f, 0.9f), new Color(0.86f, 0.93f, 1f), new Color(0.88f, 0.98f, 0.9f), new Color(1f, 0.95f, 0.8f) };
			Color fc = frames[Mathf.Abs(Album.Photos[viewIndex].GetHashCode()) % frames.Length];
			GUI.color = new Color(fc.r, fc.g, fc.b, alpha);
			GUI.DrawTexture(frame, Texture2D.whiteTexture);
			GUI.color = new Color(1f, 1f, 1f, alpha);
			GUI.DrawTexture(new Rect(frame.x + 18f, frame.y + 18f, w, h), pic);
			// handwritten caption on the frame's wide bottom edge: where (or which boss), then when
			Album.Tag(viewIndex, out string tagBoss, out string tagArea);
			string cap = (tagBoss ?? tagArea) != null ? ((tagBoss ?? tagArea) + "  -  " + Album.Name(viewIndex)) : Album.Name(viewIndex);
			float cs = Fit(cap, 0.55f, w - 20f);
			Ink2(cap, c.x - Art.Measure(cap) * cs / 2f, frame.yMax - bottom / 2f - (float)Art.LineHeight * cs / 2f - 12f, cs, A(Ink, 0.85f * alpha));
			// tape on the top corners
			Tape(new Vector2(frame.x + 30f, frame.y + 4f), -32f, alpha);
			Tape(new Vector2(frame.xMax - 30f, frame.y + 4f), 30f, alpha);
			// a sticker in the bottom corner: a golden berry for boss fights, else a strawberry or a crystal heart
			int pick = Mathf.Abs(Album.Photos[viewIndex].GetHashCode() / 7) % 2;
			GuiArt.Img sticker = (tagBoss != null) ? (PixelBerry() ?? Art.Image("collectables/goldberry")) : Art.Image(pick == 0 ? "collectables/strawberry" : "collectables/heartgem/0/spin00");
			if (sticker != null)
			{
				float sz = 64f;
				Matrix4x4 m1 = GUI.matrix;
				Vector2 sc = new Vector2(frame.xMax - 34f, frame.yMax - 30f);
				GUIUtility.RotateAroundPivot(14f, sc);
				GuiArt.Draw(sticker, new Rect(sc.x - sz / 2f, sc.y - sz / 2f, sz, sz), new Color(1f, 1f, 1f, alpha));
				GUI.matrix = m1;
			}
			if (tagBoss != null)
			{
				Stamp("BOSS FIGHT", new Vector2(frame.x + 110f, frame.y + 60f), new Color(0.75f, 0.12f, 0.16f), alpha);
			}
			GUI.matrix = m0;
		}
		string count = (viewIndex + 1) + " / " + n;
		Ink2(count, main.center.x - Art.Measure(count) * 0.45f / 2f, main.yMax - (float)Art.LineHeight * 0.45f, 0.45f, A(InkSoft, alpha));
		// the filmstrip: neighbours above and below the chosen one
		Rect strip = new Rect(area.xMax - stripW, area.y, stripW, area.height);
		GUI.color = new Color(0.12f, 0.1f, 0.14f, 0.92f * alpha);
		GUI.DrawTexture(strip, Texture2D.whiteTexture);
		for (float hy = strip.y + 10f; hy < strip.yMax - 10f; hy += 26f)
		{
			GUI.color = new Color(0.95f, 0.93f, 0.88f, 0.75f * alpha);
			GUI.DrawTexture(new Rect(strip.x + 8f, hy, 12f, 9f), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(strip.xMax - 20f, hy, 12f, 9f), Texture2D.whiteTexture);
		}
		float cellH = (strip.height - 20f) / 5f;
		for (int d = -2; d <= 2; d++)
		{
			int i = viewIndex + d;
			if (i < 0 || i >= n)
			{
				continue;
			}
			Rect cell = new Rect(strip.x + 30f, strip.y + 10f + (d + 2) * cellH + 6f, strip.width - 60f, cellH - 12f);
			rowHits.Add(new KeyValuePair<Rect, int>(cell, i));
			Texture2D t = Album.Get(i);
			if (t != null)
			{
				float k2 = Mathf.Min(cell.width / (float)t.width, cell.height / (float)t.height);
				Rect img = new Rect(cell.center.x - (float)t.width * k2 / 2f, cell.center.y - (float)t.height * k2 / 2f, (float)t.width * k2, (float)t.height * k2);
				GUI.color = new Color(1f, 1f, 1f, (d == 0 ? 1f : 0.45f) * alpha);
				GUI.DrawTexture(img, t);
				if (d == 0)
				{
					Color hi = Hex(TabColors[5]);
					hi.a = alpha;
					GUI.color = hi;
					GUI.DrawTexture(new Rect(img.x - 4f, img.y - 4f, img.width + 8f, 4f), Texture2D.whiteTexture);
					GUI.DrawTexture(new Rect(img.x - 4f, img.yMax, img.width + 8f, 4f), Texture2D.whiteTexture);
					GUI.DrawTexture(new Rect(img.x - 4f, img.y, 4f, img.height), Texture2D.whiteTexture);
					GUI.DrawTexture(new Rect(img.xMax, img.y, 4f, img.height), Texture2D.whiteTexture);
				}
			}
		}
		GUI.color = Color.white;
	}

	// a strip of semi-clear sticky tape, centred at p, turned by deg
	// an ink stamp, slightly crooked, thumped onto the page
	private void Stamp(string word, Vector2 c, Color ink, float alpha)
	{
		float s = 0.5f;
		float w = Art.Measure(word) * s + 36f;
		float h = (float)Art.LineHeight * s + 18f;
		Matrix4x4 m0 = GUI.matrix;
		GUIUtility.RotateAroundPivot(-12f, c);
		Color a = new Color(ink.r, ink.g, ink.b, 0.85f * alpha);
		GUI.color = a;
		GUI.DrawTexture(new Rect(c.x - w / 2f, c.y - h / 2f, w, 4f), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(c.x - w / 2f, c.y + h / 2f - 4f, w, 4f), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(c.x - w / 2f, c.y - h / 2f, 4f, h), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(c.x + w / 2f - 4f, c.y - h / 2f, 4f, h), Texture2D.whiteTexture);
		Ink2(word, c.x - Art.Measure(word) * s / 2f, c.y - (float)Art.LineHeight * s / 2f, s, a);
		GUI.matrix = m0;
	}

	private void Tape(Vector2 p, float deg, float alpha)
	{
		Matrix4x4 m0 = GUI.matrix;
		GUIUtility.RotateAroundPivot(deg, p);
		GUI.color = new Color(0.94f, 0.9f, 0.76f, 0.7f * alpha);
		GUI.DrawTexture(new Rect(p.x - 46f, p.y - 15f, 92f, 30f), Texture2D.whiteTexture);
		GUI.color = new Color(1f, 1f, 1f, 0.25f * alpha);
		GUI.DrawTexture(new Rect(p.x - 46f, p.y - 15f, 92f, 6f), Texture2D.whiteTexture);
		GUI.matrix = m0;
	}

	private void DrawTabs(Rect page, float alpha)
	{
		float num = 0.45f;
		float num2 = page.x + 90f;
		Vector2 point = MenuMouse.Gui();
		for (int i = 0; i < TabNames.Length; i++)
		{
			float num3 = Art.Measure(TabNames[i]) * num + 56f;
			bool flag = i == viewTab;
			bool flag2 = !flag && !viewPad && vTabRects[i].Contains(point);
			float num4 = (flag ? 66f : (flag2 ? 56f : 50f));
			Rect rect = new Rect(num2, page.y - num4 + 12f, num3, num4);
			vTabRects[i] = new Rect(num2, page.y - 66f + 12f, num3, 66f);
			Color c = Hex(TabColors[i]);
			if (flag)
			{
				rect.y -= 2f + 2f * Mathf.Sin(Time.unscaledTime * 2.2f);   // the open tab floats a little
				GUI.color = new Color(c.r, c.g, c.b, 0.45f * alpha);
				GUI.DrawTexture(new Rect(rect.x - 20f, rect.y - 20f, rect.width + 40f, rect.height + 30f), CelesteMenus.MenuFx.Glow);
			}
			float num5 = (flag ? 1f : 0.6f);
			GUI.color = new Color(c.r * num5, c.g * num5, c.b * num5, alpha);
			GUI.DrawTexture(rect, Texture2D.whiteTexture);
			GUI.color = new Color(0f, 0f, 0f, 0.25f * alpha);
			GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 5f), Texture2D.whiteTexture);
			GUI.color = new Color(1f, 1f, 1f, 0.18f * alpha);
			GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 7f, rect.width - 8f, 3f), Texture2D.whiteTexture);
			Art.DrawOutlined(TabNames[i], rect.x + 28f, rect.y + 12f, num, new Color(1f, 1f, 1f, alpha * (flag ? 1f : 0.8f)), new Color(0f, 0f, 0f, 0.6f * alpha), 2f);
			num2 += num3 + 10f;
		}
		string line = (viewPad ? "L1" : "Q");
		string line2 = (viewPad ? "R1" : "E");
		Art.DrawOutlined(line, page.x + 40f, page.y - 44f, 0.4f, new Color(0.8f, 0.8f, 0.8f, alpha), new Color(0f, 0f, 0f, alpha), 2f);
		Art.DrawOutlined(line2, num2 + 6f, page.y - 44f, 0.4f, new Color(0.8f, 0.8f, 0.8f, alpha), new Color(0f, 0f, 0f, alpha), 2f);
	}

	private void DrawScope(float right, float y, float alpha)
	{
		string text = "This Save";
		string text2 = "All Saves";
		float num = 0.5f;
		float num2 = Art.Measure(text) * num + 40f;
		float num3 = Art.Measure(text2) * num + 40f;
		float num4 = (float)Art.LineHeight * num + 14f;
		float num5 = right - num2 - num3;
		scopeRects[0] = new Rect(num5, y, num2, num4);
		scopeRects[1] = new Rect(num5 + num2, y, num3, num4);
		GUI.color = A(InkSoft, 0.15f * alpha);
		GUI.DrawTexture(new Rect(num5, y, num2 + num3, num4), Texture2D.whiteTexture);
		Rect rect = (viewAll ? scopeRects[1] : scopeRects[0]);
		GUI.color = A(Ink, 0.9f * alpha);
		GUI.DrawTexture(rect, Texture2D.whiteTexture);
		Ink2(text, num5 + 20f, y + 7f, num, viewAll ? A(InkSoft, alpha) : new Color(1f, 1f, 1f, alpha));
		Ink2(text2, num5 + num2 + 20f, y + 7f, num, viewAll ? new Color(1f, 1f, 1f, alpha) : A(InkSoft, alpha));
	}

	private float DrawSummary(float x0, float x1, float y, float alpha)
	{
		float num = Ease((Time.unscaledTime - jOpenAt - 0.1f) / 0.7f);
		int count = cacheTotalBosses;
		int num2 = Achievements.All.Length;
		Func<int, int> up = (int v) => Mathf.RoundToInt((float)v * num);   // counts up from 0 as the journal opens
		string[] array = new string[4]
		{
			up(cacheBeaten) + " / " + count,
			up(cacheGoldens) + " / " + count,
			up(cacheKills) + " / " + up(cacheDeaths),
			up(cacheAchieved) + " / " + num2
		};
		string[] array2 = new string[4]
		{
			"Bosses beaten",
			"Golden berries",
			"Kills / deaths   K/D " + Kdr(cacheKills, cacheDeaths),
			"Achievements"
		};
		GuiArt.Img[] array3 = new GuiArt.Img[4]
		{
			Art.Image("journal/clear"),
			PixelBerry() ?? Art.Image("collectables/goldberry"),
			Art.Image("journal/skullBlue"),
			Art.Image("journal/heartgem0")
		};
		float[] array4 = new float[4]
		{
			(count > 0) ? ((float)cacheBeaten / (float)count) : 0f,
			(count > 0) ? ((float)cacheGoldens / (float)count) : 0f,
			-1f,
			(num2 > 0) ? ((float)cacheAchieved / (float)num2) : 0f
		};
		float num3 = (x1 - x0) / 4f;
		for (int i = 0; i < 4; i++)
		{
			float num4 = x0 + num3 * (float)i;
			float num5 = 52f;
			if (array3[i] != null)
			{
				float num6 = num5 * Math.Min(1f, (float)array3[i].W / (float)Math.Max(1, array3[i].H));
				GuiArt.Draw(array3[i], new Rect(num4, y + 2f, num6, num5), new Color(1f, 1f, 1f, alpha));
			}
			float x = num4 + num5 + 16f;
			Ink2(array[i], x, y - 4f, Fit(array[i], 0.62f, num3 - num5 - 40f), A(Ink, alpha));
			Ink2(array2[i], x, y + (float)Art.LineHeight * 0.62f - 6f, Fit(array2[i], 0.36f, num3 - num5 - 40f), A(InkSoft, alpha));
			if (array4[i] >= 0f)
			{
				float num7 = num3 - num5 - 50f;
				Rect rect = new Rect(x, y + (float)Art.LineHeight * 0.62f + (float)Art.LineHeight * 0.36f - 2f, num7, 6f);
				GUI.color = A(InkSoft, 0.25f * alpha);
				GUI.DrawTexture(rect, Texture2D.whiteTexture);
				GUI.color = new Color(0.85f, 0.62f, 0.12f, alpha);
				GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * array4[i] * num, rect.height), Texture2D.whiteTexture);
			}
		}
		y += 92f;
		GUI.color = A(InkSoft, 0.3f * alpha);
		GUI.DrawTexture(new Rect(x0, y, x1 - x0, 2f), Texture2D.whiteTexture);
		return y + 6f;
	}

	// A table on the left half of the page with the selected entry's details on the right.
	private float DrawTable(Rect area, int count, string[] heads, GuiArt.Img[] icons, float[] cols, Func<int, string[]> cells, Func<int, bool> golden, float alpha)
	{
		float num = 52f;
		float num2 = area.y + 46f;
		Color c = A(InkSoft, alpha);
		for (int i = 0; i < heads.Length; i++)
		{
			float num3 = area.x + cols[i];
			if (icons[i] != null)
			{
				GuiArt.Draw(icons[i], new Rect(num3, area.y + 2f, 34f, 34f), new Color(1f, 1f, 1f, alpha));
			}
			else
			{
				Ink2(heads[i], num3, area.y + 8f, Fit(heads[i], 0.34f, ((i + 1 < cols.Length) ? (cols[i + 1] - cols[i]) : 60f) - 8f), c);
			}
		}
		float num4 = area.yMax - num2;
		int num5 = Math.Max(1, (int)(num4 / num));
		float num6 = Math.Max(0f, (float)count * num - num4);
		float b = Mathf.Clamp((float)viewIndex * num - num4 / 2f + num / 2f, 0f, num6);
		jScroll = Mathf.Lerp(jScroll, b, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
		float num7 = num2 + (float)viewIndex * num - jScroll;
		jSelY = ((jSelY < 0f) ? num7 : Mathf.Lerp(jSelY, num7, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime)));
		float width = cols[cols.Length - 1] + 60f;
		GUI.color = new Color(0.95f, 0.75f, 0.25f, 0.28f * alpha);
		GUI.DrawTexture(new Rect(area.x - 14f, jSelY, width, num - 4f), Texture2D.whiteTexture);
		GUI.color = new Color(0.85f, 0.55f, 0.1f, alpha);
		GUI.DrawTexture(new Rect(area.x - 14f, jSelY, 5f, num - 4f), Texture2D.whiteTexture);
		float shine = Mathf.Repeat(Time.unscaledTime * 0.5f, 1.6f) / 1.6f;   // a light sweeping across the chosen row
		GUI.color = new Color(1f, 0.95f, 0.75f, 0.35f * alpha * Mathf.Sin(shine * MathF.PI));
		GUI.DrawTexture(new Rect(area.x - 14f + (width - 120f) * shine, jSelY, 120f, num - 4f), CelesteMenus.MenuFx.Glow);
		int num8 = Math.Max(0, (int)(jScroll / num));
		int num9 = Math.Min(count, num8 + num5 + 1);
		GuiArt.Img img = PixelBerry() ?? Art.Image("collectables/goldberry");
		for (int j = num8; j < num9; j++)
		{
			float num10 = num2 + (float)j * num - jScroll;
			if (num10 < num2 - 2f || num10 + num > area.yMax + 2f)
			{
				continue;
			}
			float num11 = alpha * Mathf.Clamp01((area.yMax - num10) / num) * Mathf.Clamp01((num10 - num2 + num) / num);
			rowHits.Add(new KeyValuePair<Rect, int>(new Rect(area.x - 14f, num10, width, num - 4f), j));
			if (j % 2 == 1)
			{
				GUI.color = A(InkSoft, 0.06f * num11);
				GUI.DrawTexture(new Rect(area.x - 14f, num10, width, num - 4f), Texture2D.whiteTexture);
			}
			if (rowAct != null)
			{
				// the act's colour down the row's edge, and its number where each act starts
				int act = rowAct(j);
				Color ac = Hex(ActColors[Mathf.Clamp(act, 0, 3)]);
				GUI.color = new Color(ac.r, ac.g, ac.b, num11);
				GUI.DrawTexture(new Rect(area.x - 26f, num10 + 4f, 6f, num - 12f), Texture2D.whiteTexture);
				if (actFilter == 0 && (j == 0 || rowAct(j - 1) != act))
				{
					string an = (act == 1) ? "I" : ((act == 2) ? "II" : "III");
					Ink2(an, area.x - 34f - Art.Measure(an) * 0.42f, num10 + 6f, 0.42f, new Color(ac.r, ac.g, ac.b, num11));
				}
			}
			string[] array = cells(j);
			Color c2 = A(Ink, num11);
			for (int k = 0; k < array.Length; k++)
			{
				float num12 = ((k == 0) ? Fit(array[k], 0.5f, cols[1] - 30f) : Fit(array[k], 0.46f, ((k + 1 < cols.Length) ? (cols[k + 1] - cols[k]) : 80f) - 10f));
				Ink2(array[k], area.x + cols[k], num10 + 8f + (0.5f - num12) * (float)Art.LineHeight / 2f, num12, (array[k] == "-" || array[0] == "???") ? A(InkSoft, 0.7f * num11) : c2);
			}
			if (golden != null && img != null)
			{
				bool flag = golden(j);
				float num13 = (flag ? (3f * Mathf.Sin(Time.unscaledTime * 3f + (float)j)) : 0f);
				GuiArt.Draw(img, new Rect(area.x + cols[cols.Length - 1], num10 + 4f + num13, 40f, 40f), flag ? new Color(1f, 1f, 1f, num11) : new Color(0.2f, 0.17f, 0.28f, 0.18f * num11));
			}
		}
		if (jScroll > 2f)
		{
			Ink2("^", area.x + width / 2f, num2 - 14f, 0.32f, A(InkSoft, alpha));   // between the header and the first row, clear of the header's icons
		}
		if (jScroll < num6 - 2f)
		{
			Ink2("v", area.x + width / 2f, area.yMax - 8f, 0.4f, A(InkSoft, alpha));
		}
		return area.x + width;
	}

	private void DrawActChips(Rect area, float alpha)
	{
		float s = 0.42f;
		float h = (float)Art.LineHeight * s + 12f;
		float x = area.x;
		Vector2 m = MenuMouse.Gui();
		for (int i = 0; i < ActNames.Length; i++)
		{
			float w = Art.Measure(ActNames[i]) * s + 34f;
			actRects[i] = new Rect(x, area.y, w, h);
			bool on = i == actFilter;
			GUI.color = on ? A(Ink, 0.9f * alpha) : A(InkSoft, (actRects[i].Contains(m) ? 0.3f : 0.15f) * alpha);
			GUI.DrawTexture(actRects[i], Texture2D.whiteTexture);
			Ink2(ActNames[i], x + 17f, area.y + 6f, s, on ? new Color(1f, 1f, 1f, alpha) : A(InkSoft, alpha));
			x += w + 6f;
		}
	}

	private static int ActOfKey(string key)
	{
		if (key.StartsWith("unseen:", StringComparison.Ordinal))
		{
			return 0;
		}
		return BossRematch.ActOf(FlagOfBoss(key) ?? (key.StartsWith("flag:", StringComparison.Ordinal) ? key.Substring(5) : null));
	}

	// a boss is only listed when it belongs to an act, under All too (user 2026-10-17)
	private bool InAct(string key)
	{
		int act = ActOfKey(key);
		return act > 0 && (actFilter == 0 || act == actFilter);
	}

	// the boss list as shown: only bosses with an act; under All they go Act 1, 2, 3 like a timeline (user 2026-10-17)
	private List<KeyValuePair<string, Record>> BossList()
	{
		List<KeyValuePair<string, Record>> list = bossCache.FindAll((KeyValuePair<string, Record> r) => InAct(r.Key));
		if (actFilter == 0)
		{
			list.Sort((KeyValuePair<string, Record> a, KeyValuePair<string, Record> b) => (ActOfKey(a.Key) != ActOfKey(b.Key)) ? ActOfKey(a.Key).CompareTo(ActOfKey(b.Key)) : string.Compare(TitleOf(a.Key), TitleOf(b.Key), StringComparison.OrdinalIgnoreCase));
		}
		return list;
	}

	// set by a page before DrawTable: each row's act, for the act marks down the left (null: none)
	private Func<int, int> rowAct;

	private static readonly string[] ActColors = new string[4] { "888888", "4CAF50", "3F7FBF", "8E5BFF" };

	private void DrawBossPage(Rect area, int slot, float alpha)
	{
		DrawActChips(area, alpha);
		float chipsH = (float)Art.LineHeight * 0.42f + 24f;
		area = new Rect(area.x, area.y + chipsH, area.width, area.height - chipsH);
		List<KeyValuePair<string, Record>> list = BossList();
		rowAct = (int i) => ActOfKey(list[i].Key);
		if (list.Count == 0)
		{
			string text = (actFilter > 0) ? ("No Act " + actFilter + " bosses beaten yet.") : (viewAll ? "No bosses beaten yet." : "No bosses beaten on this save yet.");
			Ink2(text, area.center.x - Art.Measure(text) * 0.7f / 2f, area.y + 120f, 0.7f, A(InkSoft, alpha));
			DrawGoldenHowTo(new Rect(area.center.x - 380f, area.yMax - 170f, 760f, 170f), alpha);
			return;
		}
		viewIndex = Mathf.Clamp(viewIndex, 0, list.Count - 1);
		float num = area.width * 0.64f;
		string[] heads = new string[7] { "Boss", "", "", "Hits", "Heals", "Dashes", "" };
		GuiArt.Img[] icons = new GuiArt.Img[7]
		{
			null,
			Art.Image("journal/time"),
			Art.Image("journal/skullBlue"),
			null,
			null,
			null,
			null
		};
		float[] cols = new float[7]
		{
			0f,
			num * 0.37f,
			num * 0.55f,
			num * 0.66f,
			num * 0.76f,
			num * 0.87f,
			num * 0.98f
		};
		float num2 = DrawTable(area, list.Count, heads, icons, cols, delegate(int i)
		{
			Record value2 = list[i].Value;
			return new string[6]
			{
				TitleOf(list[i].Key),
				(value2.Time >= 0f) ? FormatTime(value2.Time) : "-",
				N(value2.Deaths),
				N(value2.Hits),
				N(value2.Heals),
				N(value2.Dashes)
			};
		}, (int i) => viewAll ? HasGolden(list[i].Key) : goldens.Contains(Key(slot, list[i].Key)), alpha);
		string key = list[viewIndex].Key;
		Record value = list[viewIndex].Value;
		Rect rect = new Rect(num2 + 50f, area.y, area.xMax - num2 - 50f, area.height);
		float num3 = Math.Min(rect.width, rect.height - 230f);
		Rect rect2 = new Rect(rect.center.x - num3 / 2f, rect.y, num3, num3);
		float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 1.7f);
		Color spot = Hex(TabColors[0]);
		GUI.color = new Color(spot.r, spot.g, spot.b, 0.35f * alpha * pulse);
		GUI.DrawTexture(new Rect(rect2.center.x - num3 * 0.75f, rect2.center.y - num3 * 0.75f, num3 * 1.5f, num3 * 1.5f), CelesteMenus.MenuFx.Glow);
		GUI.color = A(InkSoft, 0.1f * alpha);
		GUI.DrawTexture(rect2, Texture2D.whiteTexture);
		GUI.color = A(InkSoft, 0.45f * alpha);
		GUI.DrawTexture(new Rect(rect2.x, rect2.y, rect2.width, 2f), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(rect2.x, rect2.yMax - 2f, rect2.width, 2f), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(rect2.x, rect2.y, 2f, rect2.height), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(rect2.xMax - 2f, rect2.y, 2f, rect2.height), Texture2D.whiteTexture);
		Sprite sprite = PortraitOf(key);
		if (sprite != null && sprite.texture != null)
		{
			Vector2 vector = sprite.bounds.size;
			float num4 = (num3 - 30f) / Math.Max(vector.x, vector.y);
			float num5 = 1f + 0.015f * Mathf.Sin(Time.unscaledTime * 1.6f);
			float num6 = vector.x * num4 * num5;
			float num7 = vector.y * num4 * num5;
			bool unseen = key.StartsWith("unseen:", StringComparison.Ordinal);
			DrawSpriteMesh(sprite, new Rect(rect2.center.x - num6 / 2f, rect2.center.y - num7 / 2f, num6, num7), (float)Screen.height / 1080f, unseen ? new Color(0.1f, 0.08f, 0.16f, 0.9f * alpha) : new Color(1f, 1f, 1f, alpha));
		}
		else if (key.StartsWith("unseen:", StringComparison.Ordinal))
		{
			float qs = num3 / (float)Art.LineHeight * 0.7f;
			float qb = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 2f);
			Ink2("?", rect2.center.x - Art.Measure("?") * qs * qb / 2f, rect2.center.y - (float)Art.LineHeight * qs * qb / 2f, qs * qb, A(Ink, 0.35f * alpha));
		}
		bool flag = (viewAll ? HasGolden(key) : goldens.Contains(Key(slot, key)));
		GuiArt.Img img = PixelBerry() ?? Art.Image("collectables/goldberry");
		if (flag && img != null)
		{
			float num8 = 6f * Mathf.Sin(Time.unscaledTime * 2.5f);
			GuiArt.Draw(img, new Rect(rect2.xMax - 70f, rect2.y + 10f + num8, 60f, 60f), new Color(1f, 1f, 1f, alpha));
		}
		if (!key.StartsWith("unseen:", StringComparison.Ordinal) && value.Time > -1.5f)
		{
			// top-left of the portrait: the bottom is where the Rematch button sits (user 2026-10-18)
			Stamp(flag ? "GOLDEN" : "DEFEATED", new Vector2(rect2.x + num3 * 0.3f, rect2.y + num3 * 0.14f), flag ? new Color(0.85f, 0.6f, 0.08f) : new Color(0.75f, 0.12f, 0.16f), alpha);
		}
		string text2 = TitleOf(key);
		float num9 = Fit(text2, 0.75f, rect.width);
		float num10 = rect2.yMax + 14f;
		Ink2(text2, rect.center.x - Art.Measure(text2) * num9 / 2f, num10, num9, A(Ink, alpha));
		num10 += (float)Art.LineHeight * num9 + 2f;
		string text3 = ((value.Time >= 0f) ? ("Best " + FormatTime(value.Time)) : ((value.Time < -1.5f) ? "Not yet encountered..." : "Beaten before Silkeste"));
		Ink2(text3, rect.center.x - Art.Measure(text3) * 0.45f / 2f, num10, 0.45f, A(InkSoft, alpha));
		num10 += (float)Art.LineHeight * 0.45f + 14f;
		DrawBerryCard(new Rect(rect.x, num10, rect.width, area.yMax - num10), key, slot, flag, alpha);
		DrawRematch(rect2, key, alpha);
	}

	// a Rematch button over the bottom of the portrait, for the bosses that can be fought again
	private void DrawRematch(Rect box, string key, float alpha)
	{
		rematchFlag = null;
		string f = FlagOfBoss(key);
		if (fromMenu || !BossRematch.Allows(f) || PlayerData.instance == null)
		{
			return;
		}
		FieldInfo fi = typeof(PlayerData).GetField(f);
		if (fi == null || (!(bool)fi.GetValue(PlayerData.instance) && !BossRematch.All))
		{
			return;
		}
		if (!BossRematch.Ready(f))
		{
			string hint = "Walk through its arena once to unlock a rematch";
			float hs = Fit(hint, 0.35f, box.width - 20f);
			Ink2(hint, box.center.x - Art.Measure(hint) * hs / 2f, box.yMax - (float)Art.LineHeight * hs - 10f, hs, A(InkSoft, 0.8f * alpha));
			return;
		}
		rematchFlag = f;
		string text = "Rematch  >";
		float s = 0.5f;
		float w = Art.Measure(text) * s + 40f;
		float hh = (float)Art.LineHeight * s + 16f;
		rematchRect = new Rect(box.center.x - w / 2f, box.yMax - hh - 12f, w, hh);
		bool hover = rematchRect.Contains(MenuMouse.Gui());
		GUI.color = hover ? new Color(0.85f, 0.25f, 0.35f, alpha) : new Color(0.6f, 0.16f, 0.26f, 0.92f * alpha);
		GUI.DrawTexture(rematchRect, Texture2D.whiteTexture);
		GUI.color = new Color(0f, 0f, 0f, 0.25f * alpha);
		GUI.DrawTexture(new Rect(rematchRect.x, rematchRect.yMax - 4f, rematchRect.width, 4f), Texture2D.whiteTexture);
		Ink2(text, rematchRect.x + 20f, rematchRect.y + 7f, s, new Color(1f, 1f, 1f, alpha));
	}

	// The selected boss's golden berry: whether it's earned, and if not, which way is still open.
	private void DrawBerryCard(Rect r, string key, int slot, bool have, float alpha)
	{
		if (r.height < 70f)
		{
			return;
		}
		GUI.color = new Color(0.98f, 0.86f, 0.45f, 0.22f * alpha);
		GUI.DrawTexture(r, Texture2D.whiteTexture);
		GUI.color = new Color(0.85f, 0.6f, 0.1f, alpha);
		GUI.DrawTexture(new Rect(r.x, r.y, 5f, r.height), Texture2D.whiteTexture);
		GuiArt.Img img = PixelBerry() ?? Art.Image("collectables/goldberry");
		float num = r.x + 18f;
		float num2 = r.y + 10f;
		if (img != null)
		{
			GuiArt.Draw(img, new Rect(num, num2, 34f, 34f), have ? new Color(1f, 1f, 1f, alpha) : new Color(0.2f, 0.17f, 0.28f, 0.35f * alpha));
		}
		string text = (have ? "Golden berry earned!" : "Golden berry");
		Ink2(text, num + 44f, num2 + 2f, Fit(text, 0.45f, r.width - 80f), have ? new Color(0.62f, 0.42f, 0.04f, alpha) : A(Ink, alpha));
		num2 += 46f;
		string text2;
		if (key.StartsWith("unseen:", StringComparison.Ordinal))
		{
			text2 = "Something waits out there. Find and beat it to learn its name, and maybe earn its berry.";
		}
		else if (have)
		{
			text2 = "Already yours. One per boss, across all saves.";
		}
		else if (AssistMode.SaveMarked && !viewAll)
		{
			text2 = "This is an assist save, so it can't earn one here.";
		}
		else
		{
			deaths.TryGetValue(Key(slot, key), out var value);
			text2 = ((value > 0 && !viewAll) ? ("You've died to it " + value + ((value == 1) ? " time" : " times") + ". Win without healing to earn it.") : "Beat it without dying to it (healing is fine), or win without healing at all.");
		}
		foreach (string item in Art.Wrap(text2, (r.width - 40f) / 0.36f))
		{
			if (num2 + (float)Art.LineHeight * 0.36f > r.yMax - 4f)
			{
				break;
			}
			Ink2(item, num, num2, 0.36f, A(InkSoft, alpha));
			num2 += (float)Art.LineHeight * 0.36f;
		}
	}

	private void DrawGoldenHowTo(Rect r, float alpha)
	{
		if (r.height < 60f)
		{
			return;
		}
		string[] array = new string[4] { "Golden berry:", "First try - win without dying to it.", "Or no heals - win without healing.", "One per boss. Assist saves earn none." };
		float num = r.y;
		for (int i = 0; i < array.Length; i++)
		{
			float num2 = Fit(array[i], (i == 0) ? 0.42f : 0.36f, r.width);
			if (num + (float)Art.LineHeight * num2 > r.yMax)
			{
				break;
			}
			Ink2(array[i], r.center.x - Art.Measure(array[i]) * num2 / 2f, num, num2, (i == 0) ? new Color(0.7f, 0.48f, 0.05f, alpha) : A(InkSoft, alpha));
			num += (float)Art.LineHeight * num2 + 2f;
		}
	}

	private void DrawAreaPage(Rect area, int slot, float alpha)
	{
		List<KeyValuePair<string, KeyValuePair<float, int>>> list = areaCache;
		if (list.Count == 0)
		{
			string text = "No areas visited yet.";
			Ink2(text, area.center.x - Art.Measure(text) * 0.7f / 2f, area.y + 120f, 0.7f, A(InkSoft, alpha));
			return;
		}
		viewIndex = Mathf.Clamp(viewIndex, 0, list.Count - 1);
		float num = area.width * 0.64f;
		rowAct = null;
		float num2 = DrawTable(area, list.Count, new string[5] { "Area", "", "Kills", "", "K/D" }, new GuiArt.Img[5]
		{
			null,
			Art.Image("journal/time"),
			null,
			Art.Image("journal/skullBlue"),
			null
		}, new float[5]
		{
			0f,
			num * 0.5f,
			num * 0.68f,
			num * 0.8f,
			num * 0.92f
		}, delegate(int i)
		{
			int kills = AreaKills(slot, list[i].Key);
			return new string[5]
			{
				AreaName(list[i].Key),
				FormatTime(list[i].Value.Key),
				kills.ToString(CultureInfo.InvariantCulture),
				list[i].Value.Value.ToString(CultureInfo.InvariantCulture),
				Kdr(kills, list[i].Value.Value)
			};
		}, null, alpha);
		KeyValuePair<string, KeyValuePair<float, int>> keyValuePair = list[viewIndex];
		int num3 = AreaKills(slot, keyValuePair.Key);
		Rect rect = new Rect(num2 + 50f, area.y + 10f, area.xMax - num2 - 50f, area.height - 10f);
		GuiArt.Img img = Art.Image("areaselect/title");
		float num4 = rect.width * 173f / 916f;
		Color areaCol = AreaColor(keyValuePair.Key);
		if (img != null)
		{
			Color c = areaCol;
			c.a = alpha;
			GuiArt.Draw(img, new Rect(rect.x, rect.y, rect.width, num4), c);
		}
		string text2 = AreaName(keyValuePair.Key);
		float num5 = Fit(text2, 0.7f, rect.width - 60f);
		Art.DrawOutlined(text2, rect.center.x - Art.Measure(text2) * num5 / 2f, rect.y + num4 / 2f - (float)Art.LineHeight * num5 / 2f, num5, new Color(1f, 1f, 1f, alpha), new Color(0f, 0f, 0f, alpha), 2f);
		float num6 = rect.y + num4 + 24f;
		string[] array = new string[4]
		{
			FormatTime(keyValuePair.Value.Key),
			num3.ToString(CultureInfo.InvariantCulture),
			keyValuePair.Value.Value.ToString(CultureInfo.InvariantCulture),
			Kdr(num3, keyValuePair.Value.Value)
		};
		string[] array2 = new string[4]
		{
			"time spent here",
			(num3 == 1) ? "enemy killed" : "enemies killed",
			(keyValuePair.Value.Value == 1) ? "death" : "deaths",
			"kills per death"
		};
		GuiArt.Img[] array3 = new GuiArt.Img[4]
		{
			Art.Image("journal/time"),
			Art.Image("journal/skullGold"),
			Art.Image("journal/skullBlue"),
			Art.Image("journal/dot")
		};
		float num7 = Math.Min(92f, (rect.yMax - 40f - num6) / 4f);
		float num8 = Math.Min(56f, num7 - 10f);
		for (int j = 0; j < 4; j++)
		{
			if (array3[j] != null)
			{
				GuiArt.Draw(array3[j], new Rect(rect.x + 30f, num6, num8, num8), new Color(1f, 1f, 1f, alpha));
			}
			float num9 = num8 / 56f;
			Ink2(array[j], rect.x + 50f + num8, num6 - 6f * num9, 0.66f * num9, A(Ink, alpha));
			Ink2(array2[j], rect.x + 50f + num8, num6 + (float)Art.LineHeight * 0.66f * num9 - 10f * num9, 0.36f * num9, A(InkSoft, alpha));
			num6 += num7;
		}
		// a ring: this area's share of all the time you've spent in Pharloom, filling as the page opens
		float total = 0f;
		foreach (KeyValuePair<string, KeyValuePair<float, int>> ar in list)
		{
			total += ar.Value.Key;
		}
		float share = (total > 0f) ? (keyValuePair.Value.Key / total) : 0f;
		float fill = share * Ease((Time.unscaledTime - jTabAt) / 0.8f);
		float ringR = Math.Min(70f, rect.width * 0.17f);
		Vector2 rc = new Vector2(rect.xMax - ringR - 24f, rect.y + num4 + 24f + ringR + 10f);
		for (int d = 0; d < 48; d++)
		{
			float ang = (float)d / 48f * MathF.PI * 2f - MathF.PI / 2f;
			bool lit = (float)d / 48f < fill;
			GUI.color = lit ? new Color(areaCol.r, areaCol.g, areaCol.b, alpha) : A(InkSoft, 0.18f * alpha);
			GUI.DrawTexture(new Rect(rc.x + Mathf.Cos(ang) * ringR - 5f, rc.y + Mathf.Sin(ang) * ringR - 5f, 10f, 10f), Texture2D.whiteTexture);
		}
		string pct = Mathf.RoundToInt(share * 100f) + "%";
		Ink2(pct, rc.x - Art.Measure(pct) * 0.6f / 2f, rc.y - (float)Art.LineHeight * 0.6f / 2f - 8f, 0.6f, A(Ink, alpha));
		Ink2("of your time", rc.x - Art.Measure("of your time") * 0.3f / 2f, rc.y + 14f, 0.3f, A(InkSoft, alpha));
		string text3 = (viewAll ? "All saves together" : "This save");
		Ink2(text3, rect.center.x - Art.Measure(text3) * 0.36f / 2f, rect.yMax - 34f, 0.36f, A(InkSoft, alpha));
	}

	// each area its own colour, picked from a palette by its name (the same every time)
	private static Color AreaColor(string key)
	{
		string[] palette = new string[10] { "8C1C2E", "2E6B8C", "3E8C4A", "8C6A1C", "6A2E8C", "1C7A8C", "8C3E6A", "5A6B2E", "2E3E8C", "8C4A2E" };
		int h = 0;
		foreach (char ch in key ?? "")
		{
			h = h * 31 + ch;
		}
		return Hex(palette[Math.Abs(h) % palette.Length]);
	}

	// The Stats page: a wall of tiles summing up the whole journey.
	private void DrawStatsPage(Rect area, float alpha)
	{
		int num = Achievements.All.Length;
		GuiArt.Img img = PixelBerry() ?? Art.Image("collectables/goldberry");
		string[] array = new string[12]
		{
			cacheKills.ToString(CultureInfo.InvariantCulture),
			cacheDeaths.ToString(CultureInfo.InvariantCulture),
			Kdr(cacheKills, cacheDeaths),
			cacheBeaten + " / " + cacheTotalBosses,
			cacheGoldens + " / " + cacheTotalBosses,
			cacheAchieved + " / " + num,
			areaCache.Count.ToString(CultureInfo.InvariantCulture),
			statExplored,
			statDeadliest,
			statKilliest,
			statToughest,
			statFastest
		};
		string[] array2 = new string[12] { "Enemies killed", "Deaths", "Kill / death ratio", "Bosses beaten", "Golden berries", "Achievements", "Areas discovered", "Time exploring", "Deadliest area", "Hunting ground", "Toughest boss", "Fastest boss kill" };
		GuiArt.Img[] array3 = new GuiArt.Img[12]
		{
			Art.Image("journal/skullGold"),
			Art.Image("journal/skullBlue"),
			Art.Image("journal/dot"),
			Art.Image("journal/clear"),
			img,
			Art.Image("journal/heartgem0"),
			Art.Image("journal/strawberry"),
			Art.Image("journal/time"),
			Art.Image("journal/skullBlue"),
			Art.Image("journal/skullGold"),
			Art.Image("journal/fullclear"),
			Art.Image("journal/time")
		};
		string[] array4 = new string[12] { "C2414B", "6A5ACD", "3F7FBF", "3E9B5A", "D9A21B", "D45A9B", "3F7FBF", "6A5ACD", "C2414B", "3E9B5A", "D9A21B", "D45A9B" };
		int num2 = 4;
		int num3 = 3;
		float num4 = 18f;
		float num5 = (area.width - num4 * (float)(num2 - 1)) / (float)num2;
		string[] desc = new string[12] { "Every enemy you've defeated, counted in the area it fell.", "Every time you've died, spikes and pits included.", "Enemies killed for each death. Above 1 means you're winning.", "Bosses beaten, out of every boss Silksong has.", "Golden berries earned: beat a boss first try, or without healing. One per boss.", "Silkeste's own achievements unlocked.", "Areas of Pharloom you've set foot in.", "Time spent exploring, counted while you're in control.", "The area that has claimed you the most times.", "The area where you've defeated the most enemies.", "The boss you've died to most often.", "Your quickest win against any boss." };
		area = new Rect(area.x, area.y, area.width, area.height - 70f);
		float num6 = Math.Min(170f, (area.height - num4 * (float)(num3 - 1)) / (float)num3);
		viewIndex = Mathf.Clamp(viewIndex, 0, 11);
		Vector2 mouse = MenuMouse.Gui();
		float num7 = area.y + (area.height - (num6 * (float)num3 + num4 * (float)(num3 - 1))) / 2f;
		for (int i = 0; i < 12; i++)
		{
			float num8 = Ease((Time.unscaledTime - jTabAt - (float)i * 0.035f) / 0.3f);
			Rect r = new Rect(area.x + (float)(i % num2) * (num5 + num4), num7 + (float)(i / num2) * (num6 + num4) + (1f - num8) * 20f, num5, num6);
			float num9 = alpha * num8;
			Color color = Hex(array4[i]);
			rowHits.Add(new KeyValuePair<Rect, int>(r, i));
			if (r.Contains(mouse) && !viewPad && viewIndex != i && Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.5f)
			{
				viewIndex = i;
			}
			if (i == viewIndex)
			{
				float lift = 4f + 2f * Mathf.Sin(Time.unscaledTime * 4f);
				GUI.color = new Color(0.95f, 0.75f, 0.25f, 0.45f * num9);
				GUI.DrawTexture(new Rect(r.x - 7f, r.y - 7f, r.width + 14f, r.height + 14f), Texture2D.whiteTexture);
				r = new Rect(r.x, r.y - lift, r.width, r.height);
			}
			GUI.color = new Color(0f, 0f, 0f, 0.1f * num9);
			GUI.DrawTexture(new Rect(r.x + 4f, r.y + 5f, r.width, r.height), Texture2D.whiteTexture);
			GUI.color = new Color(Mathf.Lerp(1f, color.r, 0.16f), Mathf.Lerp(1f, color.g, 0.16f), Mathf.Lerp(1f, color.b, 0.16f), 0.85f * num9);
			GUI.DrawTexture(r, Texture2D.whiteTexture);
			GUI.color = new Color(color.r * 0.85f, color.g * 0.85f, color.b * 0.85f, num9);
			GUI.DrawTexture(new Rect(r.x, r.y, 6f, r.height), Texture2D.whiteTexture);
			float num10 = Math.Min(48f, r.height * 0.32f);
			if (array3[i] != null)
			{
				float num11 = num10 * Math.Min(1f, (float)array3[i].W / (float)Math.Max(1, array3[i].H));
				GuiArt.Draw(array3[i], new Rect(r.x + 22f, r.y + 16f, num11, num10), new Color(1f, 1f, 1f, num9));
			}
			Ink2(array2[i], r.x + 22f + num10 + 12f, r.y + 16f + num10 / 2f - (float)Art.LineHeight * 0.36f / 2f, Fit(array2[i], 0.36f, r.width - num10 - 50f), A(InkSoft, num9));
			float num12 = Fit(array[i], (i >= 8) ? 0.5f : 0.85f, r.width - 44f);
			Ink2(array[i], r.x + 22f, r.yMax - 18f - (float)Art.LineHeight * num12, num12, A(Ink, num9));
		}
		Rect d = new Rect(area.x, area.yMax + 12f, area.width, 56f);
		Color dc = Hex(array4[viewIndex]);
		GUI.color = new Color(dc.r, dc.g, dc.b, 0.14f * alpha);
		GUI.DrawTexture(d, Texture2D.whiteTexture);
		GUI.color = new Color(dc.r * 0.85f, dc.g * 0.85f, dc.b * 0.85f, alpha);
		GUI.DrawTexture(new Rect(d.x, d.y, 6f, d.height), Texture2D.whiteTexture);
		string head = array2[viewIndex] + ':';
		Ink2(head, d.x + 24f, d.y + 12f, 0.42f, A(Ink, alpha));
		float hw = Art.Measure(head) * 0.42f + 16f;
		Ink2(desc[viewIndex], d.x + 24f + hw, d.y + 14f, Fit(desc[viewIndex], 0.38f, d.width - hw - 50f), A(InkSoft, alpha));
	}

	private float DrawPicker(string name, int index, int count, float lx, float leftW, float ny, float alpha)
	{
		Color c = new Color(1f, 1f, 1f, alpha);
		Color outline = new Color(0f, 0f, 0f, alpha);
		float num = 1f;
		while (num > 0.5f && Art.Measure(name) * num > leftW - 220f)
		{
			num -= 0.05f;
		}
		string line = index + 1 + " / " + count;
		float num2 = lx - (leftW - 120f) / 2f;
		float num3 = lx + (leftW - 120f) / 2f - Art.Measure(">") * 1.2f;
		float num4 = Art.Measure(">") * 1.2f;
		float num5 = (float)Art.LineHeight * 1.2f;
		pickL = new Rect(num2 - 24f, ny - 16f, num4 + 48f, num5 + 32f);
		pickR = new Rect(num3 - 24f, ny - 16f, num4 + 48f, num5 + 32f);
		Color color = Hex("84FF54");
		color.a = alpha;
		float num6 = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
		Vector2 point = MenuMouse.Gui();
		Art.DrawOutlined("<", num2, ny, 1.2f, pickL.Contains(point) ? color : new Color(1f, 1f, 1f, alpha * num6), outline, 3f);
		Art.DrawOutlined(">", num3, ny, 1.2f, pickR.Contains(point) ? color : new Color(1f, 1f, 1f, alpha * num6), outline, 3f);
		Art.DrawOutlined(name, lx - Art.Measure(name) * num / 2f, ny, num, c, outline, 3f);
		Art.DrawOutlined(line, lx - Art.Measure(line) * 0.6f / 2f, ny + (float)Art.LineHeight * num + 8f, 0.6f, new Color(0.8f, 0.8f, 0.8f, alpha), outline, 2f);
		return ny + (float)Art.LineHeight * num + 8f + (float)Art.LineHeight * 0.6f;
	}

	private void Update()
	{
		try
		{
			Tick();
		}
		catch (Exception ex)
		{
			if (!loggedError)
			{
				loggedError = true;
				Plugin.Log.LogError("BossCard: " + ex);
			}
		}
	}

	private void Tick()
	{
		if (!Plugin.Ready)
		{
			return;
		}
		HeroController silentInstance = HeroController.UnsafeInstance;
		GameManager silentInstance2 = GameManager.SilentInstance;
		PlayerData instance = PlayerData.instance;
		if (silentInstance2 != null && silentInstance2.IsMenuScene() && (fromMenu || viewing || viewT > 0f))
		{
			fromMenu = true;
			Load();
			UpdateViewer(null, silentInstance2);
			if (!viewing && viewT <= 0f)
			{
				fromMenu = false;
			}
			return;
		}
		fromMenu = false;
		if (silentInstance == null || silentInstance2 == null || instance == null || silentInstance2.IsMenuScene())
		{
			Hide(silentInstance);
			fight = null;
			baselineSlot = -1;
			curZone = null;
			bannerT = -1f;
			return;
		}
		Load();
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		bool flag = silentInstance2.GameState == GameState.PLAYING && !silentInstance2.isPaused;
		string text = SceneManager.GetActiveScene().name;
		if (fight != null)
		{
			if (text == fight.Scene)
			{
				if (flag && !silentInstance.cState.dead)
				{
					fight.Time += unscaledDeltaTime;
				}
				fight.LeftSceneAt = -1f;
			}
			else if (fight.LeftSceneAt < 0f)
			{
				fight.LeftSceneAt = Time.unscaledTime;
			}
			else if (Time.unscaledTime - fight.LeftSceneAt > 120f)
			{
				fight = null;
			}
		}
		if (fight != null)
		{
			if ((AssistMode.AnyOn || AssistMode.SaveMarked))
			{
				fight.Assist = true;
			}
			if (AssistMode.Invincible)
			{
				fight.Invincible = true;
			}
			if (lastHealth >= 0 && instance.health < lastHealth && !silentInstance.cState.dead)
			{
				fight.Hits++;
			}
			if (lastHealth >= 0 && instance.health > lastHealth && !silentInstance.cState.dead)
			{
				bool goldenRun = GoldenRun;
				fight.Heals++;
				int num;
				if (goldenRun)
				{
					num = ((!GoldenRun) ? 1 : 0);
					if (num != 0 && hudAlpha > 0.5f)
					{
						hudLostT = 0f;
						Sfx.Play(Sounds.GoldenLaugh.Id);
					}
				}
				else
				{
					num = 0;
				}
				if (num != 0 && BossCard.GoldenLost != null)
				{
					BossCard.GoldenLost();
				}
			}
		}
		lastHealth = instance.health;
		bool dead = silentInstance.cState.dead;
		if (dead && !wasDead && fight != null)
		{
			string key = Key(silentInstance2.profileID, fight.Id);
			deaths.TryGetValue(key, out var value);
			deaths[key] = value + 1;
			Save();
			fight = null;
		}
		wasDead = dead;
		hudAlpha = Mathf.MoveTowards(hudAlpha, GoldenInHud ? 1f : 0f, unscaledDeltaTime / 0.3f);
		if (hudLostT >= 0f && (hudLostT += unscaledDeltaTime) > 1.3f)
		{
			hudLostT = -1f;
		}
		WatchDefeats(silentInstance2, instance);
		EnsureAllBosses();
		ImportBeaten(silentInstance2.profileID, instance);
		TrackArea(silentInstance, silentInstance2, flag && !silentInstance.cState.transitioning, unscaledDeltaTime);
		UpdateCard(silentInstance, silentInstance2, unscaledDeltaTime);
		UpdateViewer(silentInstance, silentInstance2);
	}

	private void WatchDefeats(GameManager gm, PlayerData pd)
	{
		if (defeatFields == null)
		{
			defeatFields = new List<FieldInfo>();
			FieldInfo[] fields = typeof(PlayerData).GetFields(BindingFlags.Instance | BindingFlags.Public);
			foreach (FieldInfo fieldInfo in fields)
			{
				if (fieldInfo.FieldType == typeof(bool) && (fieldInfo.Name.StartsWith("defeated", StringComparison.OrdinalIgnoreCase) || fieldInfo.Name.StartsWith("killed", StringComparison.OrdinalIgnoreCase) || BossRematch.Allows(fieldInfo.Name)))
				{
					defeatFields.Add(fieldInfo);
				}
			}
		}
		bool flag = gm.profileID != baselineSlot;
		baselineSlot = gm.profileID;
		foreach (FieldInfo defeatField in defeatFields)
		{
			bool flag2 = (bool)defeatField.GetValue(pd);
			if (!flag && flags.TryGetValue(defeatField.Name, out var value) && !value && flag2)
			{
				Defeated(gm, defeatField.Name);
				MadelineView.Cheer();
				if (BossRematch.ActOf(defeatField.Name) > 0)
				{
					if (AssistMode.Difficulty >= 3)
					{
						Achievements.Add("hard_knocks");
					}
					if (AssistMode.Difficulty == 4 && BossRematch.ActOf(defeatField.Name) == 2)
					{
						Achievements.Done("one_shot");
					}
				}
				string runName = (fight != null) ? TitleOf(fight.Id) : (BossRematch.TitleOf(defeatField.Name) ?? (flagBoss.TryGetValue(defeatField.Name, out string fb) ? TitleOf(fb) : null));
				if (runName != null && !Hidden(runName) && BossRematch.ActOf(defeatField.Name) > 0)
				{
					RunLog.Add("boss", runName);
				}
			}
			flags[defeatField.Name] = flag2;
		}
	}

	private void Defeated(GameManager gm, string flag)
	{
		Plugin.Log.LogInfo("Boss defeated: " + flag + ((fight != null) ? (" (fight " + fight.Id + ", " + fight.Hits + " hits)") : " (no fight seen)"));
		if (fight == null)
		{
			return;
		}
		flagBoss[flag] = fight.Id;
		string text = Key(gm.profileID, fight.Id);
		deaths.TryGetValue(text, out var value);
		bool flag2 = HasGolden(fight.Id);
		bool flag3 = (fight.FirstTry || fight.Heals == 0) && !fight.Assist;
		if (flag3 && !flag2)
		{
			goldens.Add(text);
			RunLog.Add("golden", TitleOf(fight.Id));
		}
		best.TryGetValue(text, out var value2);
		if (value2 != null && value2.Time < 0f)
		{
			value2 = null;
		}
		bool flag4 = value2 != null && fight.Time < value2.Time;
		if (value2 == null || flag4)
		{
			best[text] = new Record
			{
				Time = fight.Time,
				Deaths = value,
				Dashes = fight.Dashes,
				Hits = fight.Hits,
				Heals = fight.Heals
			};
		}
		Save();
		int num = 0;
		num = GoldenTotal();
		int num2 = 0;
		int num3 = 0;
		foreach (KeyValuePair<string, Record> item in ViewList(gm.profileID))
		{
			num2++;
			if (goldens.Contains(Key(gm.profileID, item.Key)))
			{
				num3++;
			}
		}
		int num4 = 0;
		foreach (string golden in goldens)
		{
			if (TitleOf(golden.Substring(golden.IndexOf('|') + 1)).IndexOf("Moss Mother", StringComparison.OrdinalIgnoreCase) < 0)
			{
				num4++;
			}
		}
		Achievements.BossBeaten(fight.Title, fight.Time, fight.Dashes, fight.Hits, flag3, fight.Assist, num4, num2, num3);
		queue.Add(new Card
		{
			Title = fight.Title,
			Sub = fight.Sub,
			Time = fight.Time,
			Deaths = value,
			Dashes = fight.Dashes,
			Hits = fight.Hits,
			Heals = fight.Heals,
			Golden = flag3,
			HadGolden = flag2,
			Goldens = num,
			Assist = fight.Assist,
			Invincible = fight.Invincible,
			FirstTry = fight.FirstTry,
			NewBest = flag4,
			PrevBest = (value2?.Time ?? (-1f))
		});
		fight = null;
	}

	private bool HasGolden(string id)
	{
		Load();
		foreach (string golden in goldens)
		{
			if (golden.EndsWith("|" + id, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	private int GoldenTotal()
	{
		HashSet<string> hashSet = new HashSet<string>();
		foreach (string golden in goldens)
		{
			hashSet.Add(golden.Substring(golden.IndexOf('|') + 1));
		}
		return hashSet.Count;
	}

	public static int GoldenCount(int slot)
	{
		if (Instance == null)
		{
			return 0;
		}
		Instance.Load();
		return Instance.GoldenTotal();
	}

	/// <summary>Bosses beaten on a save (those Records lists), and how many there are, for the file select.</summary>
	public static int BossesOnSlot(int slot, out int total)
	{
		total = TotalBosses;
		if (Instance == null)
		{
			return 0;
		}
		Instance.Load();
		string prefix = slot.ToString(CultureInfo.InvariantCulture) + "|";
		HashSet<string> seen = new HashSet<string>();
		foreach (string key in Instance.best.Keys)
		{
			if (key.StartsWith(prefix, StringComparison.Ordinal))
			{
				string id = key.Substring(prefix.Length);
				if (ActOfKey(id) > 0)
				{
					seen.Add(id);
				}
			}
		}
		return seen.Count;
	}

	public static int GoldensOnSlot(int slot)
	{
		if (Instance == null)
		{
			return 0;
		}
		Instance.Load();
		string value = slot.ToString(CultureInfo.InvariantCulture) + "|";
		int num = 0;
		foreach (string golden in Instance.goldens)
		{
			if (golden.StartsWith(value, StringComparison.Ordinal))
			{
				num++;
			}
		}
		return num;
	}

	private void Load()
	{
		if (loaded)
		{
			return;
		}
		loaded = true;
		try
		{
			if (!File.Exists(SaveFile))
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
				if (array2[0].StartsWith("golden:", StringComparison.Ordinal))
				{
					goldens.Add(array2[0].Substring(7));
				}
				else if (array2[0].StartsWith("deaths:", StringComparison.Ordinal))
				{
					if (int.TryParse(array2[1], out var result))
					{
						deaths[array2[0].Substring(7)] = result;
					}
				}
				else if (array2[0].StartsWith("title:", StringComparison.Ordinal))
				{
					string text = array2[0].Substring(6);
					titles[text] = (text.StartsWith("flag:", StringComparison.Ordinal) ? FlagTitle(text.Substring(5)) : array2[1]);
				}
				else if (array2[0].StartsWith("flagboss:", StringComparison.Ordinal))
				{
					flagBoss[array2[0].Substring(9)] = array2[1];
				}
				else if (array2[0].StartsWith("atime:", StringComparison.Ordinal))
				{
					if (float.TryParse(array2[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
					{
						areaTime[array2[0].Substring(6)] = result2;
					}
				}
				else if (array2[0].StartsWith("adeaths:", StringComparison.Ordinal))
				{
					if (int.TryParse(array2[1], out var result3))
					{
						areaDeaths[array2[0].Substring(8)] = result3;
					}
				}
				else if (array2[0].StartsWith("akills:", StringComparison.Ordinal))
				{
					if (int.TryParse(array2[1], out var resultK))
					{
						areaKills[array2[0].Substring(7)] = resultK;
					}
				}
				else if (array2[0].StartsWith("aname:", StringComparison.Ordinal))
				{
					areaNames[array2[0].Substring(6)] = array2[1];
				}
				else if (array2[0].StartsWith("journal:", StringComparison.Ordinal))
				{
					journal[array2[0].Substring(8)] = array2[1];
				}
				else if (array2[0].StartsWith("best:", StringComparison.Ordinal))
				{
					string[] array3 = array2[1].Split(new char[1] { ';' });
					if (array3.Length == 5 && float.TryParse(array3[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result4) && int.TryParse(array3[1], out var result5) && int.TryParse(array3[2], out var result6) && int.TryParse(array3[3], out var result7) && int.TryParse(array3[4], out var result8))
					{
						best[array2[0].Substring(5)] = new Record
						{
							Time = result4,
							Deaths = result5,
							Dashes = result6,
							Hits = result7,
							Heals = result8
						};
					}
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Boss records: " + ex.Message);
		}
		try
		{
			if (ResetFlag != null && File.Exists(ResetFlag))
			{
				best.Clear();
				goldens.Clear();
				deaths.Clear();
				flagBoss.Clear();
				File.Delete(ResetFlag);
				Save();
				Plugin.Log.LogInfo("Boss records: reset (boss times, deaths and golden berries)");
			}
		}
		catch (Exception ex2)
		{
			Plugin.Log.LogWarning("Boss records reset: " + ex2.Message);
		}
	}

	private void Save()
	{
		try
		{
			List<string> list = new List<string>();
			foreach (string golden in goldens)
			{
				list.Add("golden:" + golden + "=1");
			}
			foreach (KeyValuePair<string, int> death in deaths)
			{
				list.Add("deaths:" + death.Key + "=" + death.Value);
			}
			foreach (KeyValuePair<string, string> title in titles)
			{
				list.Add("title:" + title.Key + "=" + title.Value.Replace("=", " "));
			}
			foreach (KeyValuePair<string, string> item in journal)
			{
				list.Add("journal:" + item.Key + "=" + item.Value);
			}
			foreach (KeyValuePair<string, string> item2 in flagBoss)
			{
				list.Add("flagboss:" + item2.Key + "=" + item2.Value);
			}
			foreach (KeyValuePair<string, float> item3 in areaTime)
			{
				list.Add("atime:" + item3.Key + "=" + item3.Value.ToString("R", CultureInfo.InvariantCulture));
			}
			foreach (KeyValuePair<string, int> areaDeath in areaDeaths)
			{
				list.Add("adeaths:" + areaDeath.Key + "=" + areaDeath.Value);
			}
			foreach (KeyValuePair<string, int> areaKill in areaKills)
			{
				list.Add("akills:" + areaKill.Key + "=" + areaKill.Value);
			}
			foreach (KeyValuePair<string, string> areaName in areaNames)
			{
				list.Add("aname:" + areaName.Key + "=" + areaName.Value.Replace("=", " "));
			}
			foreach (KeyValuePair<string, Record> item4 in best)
			{
				list.Add("best:" + item4.Key + "=" + item4.Value.Time.ToString("R", CultureInfo.InvariantCulture) + ";" + item4.Value.Deaths + ";" + item4.Value.Dashes + ";" + item4.Value.Hits + ";" + item4.Value.Heals);
			}
			Directory.CreateDirectory(DataDir);
			File.WriteAllLines(SaveFile, list.ToArray());
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Boss records: " + ex.Message);
		}
	}

	private void UpdateCard(HeroController h, GameManager gm, float dt)
	{
		bool flag = gm.GameState == GameState.PLAYING && !gm.isPaused && !h.cState.transitioning && !h.cState.dead && !h.controlReqlinquished;
		controlFor = (flag ? (controlFor + dt) : 0f);
		if (phase == Phase.None && queue.Count > 0 && flag && controlFor >= 1f)
		{
			card = queue[0];
			queue.RemoveAt(0);
			phase = Phase.In;
			t = 0f;
			Sfx.Play(Sounds.CardIn.Id);
			if (card.Golden && !card.HadGolden)
			{
				Sfx.Play(Sounds.GoldenGet.Id);
			}
			if (!h.controlReqlinquished)
			{
				h.RelinquishControl();
				relinquished = true;
			}
		}
		switch (phase)
		{
		case Phase.In:
			t += dt / 0.6f;
			if (t >= 1f)
			{
				phase = Phase.Shown;
				shownFor = 0f;
				photoTaken = card.Viewing;
			}
			break;
		case Phase.Shown:
			shownFor += dt;
			if (!photoTaken && shownFor >= 0.8f)
			{
				photoTaken = true;
				TheoCamera.BossPhoto(card.Title);
			}
			if (shownFor >= 0.75f && Confirm())
			{
				phase = Phase.Out;
				t = 0f;
				Sfx.Play(Sounds.CardOut.Id);
			}
			break;
		case Phase.Out:
			t += dt / 0.4f;
			if (t >= 1f)
			{
				phase = Phase.None;
				card = null;
				Release(h);
			}
			break;
		}
	}

	private static bool Confirm()
	{
		InputHandler instance = ManagerSingleton<InputHandler>.Instance;
		Keyboard current = Keyboard.current;
		if (instance != null && instance.inputActions != null && instance.inputActions.Jump.WasPressed)
		{
			return true;
		}
		if (current != null)
		{
			if (!current.enterKey.wasPressedThisFrame)
			{
				return current.spaceKey.wasPressedThisFrame;
			}
			return true;
		}
		return false;
	}

	private void Release(HeroController h)
	{
		if (relinquished && h != null)
		{
			h.RegainControl();
		}
		relinquished = false;
		Plugin.Controller.ClearInput();
	}

	private void Hide(HeroController h)
	{
		if (phase != Phase.None)
		{
			phase = Phase.None;
			card = null;
			Release(h);
		}
	}

	public static string FormatTime(float seconds)
	{
		int num = (int)(seconds * 100f);
		int num2 = num / 6000;
		int num3 = num / 100 % 60;
		int num4 = num % 100;
		int num5 = num2 / 60;
		if (num5 > 0)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}.{3:00}", num5, num2 % 60, num3, num4);
		}
		return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}.{2:00}", num2, num3, num4);
	}

	private void DrawHudBerry()
	{
		GuiArt.Img img = PixelBerry() ?? Art.Image("collectables/goldberry");
		if (img != null && (!(hudAlpha <= 0f) || !(hudLostT < 0f)))
		{
			float num = (float)Screen.height / 1080f;
			float num2 = (float)Screen.width / num;
			float num3 = 64f;
			float num4 = num2 - 48f - num3;
			float num5 = 150f + Mathf.Sin(Time.unscaledTime * 3f) * 4f;
			float a = hudAlpha;
			if (hudLostT >= 0f)
			{
				float num6 = Mathf.Max(0f, hudLostT - 0.35f);
				num5 -= num6 * num6 * 600f;
				a = Mathf.Clamp01(1f - num6 / 0.8f);
				num4 += Mathf.Sin(hudLostT * 40f) * ((hudLostT < 0.35f) ? 4f : 0f);
			}
			Matrix4x4 matrix = GUI.matrix;
			GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num, num, 1f));
			GuiArt.Draw(img, new Rect(num4, num5, num3, num3), new Color(1f, 1f, 1f, a));
			GUI.matrix = matrix;
			GUI.color = Color.white;
		}
	}

	private void OnGUI()
	{
		if (TheoCamera.Hiding)
		{
			return;
		}
		if (Event.current.type == EventType.Repaint && Art != null && phase == Phase.None)
		{
			DrawHudBerry();
		}
		if (Event.current.type == EventType.Repaint && Art != null && viewT > 0f)
		{
			DrawViewer();
			return;
		}
		if (Event.current.type == EventType.Repaint && Art != null)
		{
			DrawAreaBanner();
		}
		if (phase != Phase.None && card != null && Art != null && Event.current.type == EventType.Repaint)
		{
			float num = (float)Screen.height / 1080f;
			float width = (float)Screen.width / num;
			float num2 = Mathf.Clamp01(t);
			float num3 = ((phase == Phase.In) ? (1f - (1f - num2) * (1f - num2) * (1f - num2)) : ((phase != Phase.Out) ? 1f : (1f - num2 * num2 * num2)));
			float num4 = num3;
			Matrix4x4 matrix = GUI.matrix;
			GUI.depth = -1;
			GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num, num, 1f));
			GUI.color = new Color(0f, 0f, 0f, 0.45f * num4);
			GUI.DrawTexture(new Rect(0f, 0f, width, 1080f), Texture2D.whiteTexture);
			DrawCardBody(card, num4, num3);
			GUI.matrix = matrix;
			GUI.color = Color.white;
		}
	}

	private void DrawCardBody(Card card, float alpha, float ease)
	{
		float num = (float)Screen.height / 1080f;
		float num2 = (float)Screen.width / num;
		bool flag = card.Golden || card.HadGolden;
		float num3 = 0.72f;
		float num4 = (1f - ease) * 900f;
		GuiArt.Img img = Art.Image(flag ? "areaselect/cardtop_golden" : "areaselect/cardtop");
		GuiArt.Img img2 = Art.Image(flag ? "areaselect/card_golden" : "areaselect/card");
		float num5 = 855f * num3;
		float num6 = num2 - num5 + num4;
		float num7 = 260f;
		Color c = new Color(1f, 1f, 1f, alpha);
		if (img != null)
		{
			GuiArt.Draw(img, new Rect(num6, num7, num5, (float)img.H * num3), c);
			num7 += (float)img.H * num3;
		}
		float num8 = num7;
		if (img2 != null)
		{
			GuiArt.Draw(img2, new Rect(num6, num7, num5, (float)img2.H * num3), c);
		}
		float num9 = ((img2 != null) ? ((float)img2.H) : 900f) * num3;
		GuiArt.Img img3 = Art.Image("areaselect/title");
		float num10 = 173f * num3;
		float num11 = 916f * num3;
		float num12 = num2 - num11 + num4 * 1.2f;
		float num13 = 260f - num10 * 0.55f;
		Color c2 = Hex(flag ? "D8A520" : "8C1C2E");
		c2.a = alpha;
		if (img3 != null)
		{
			GuiArt.Draw(img3, new Rect(num12, num13, num11, num10), c2);
		}
		GuiArt.Img img4 = Art.Image("areaselect/accent");
		Color c3 = Hex(flag ? "F7D769" : "C8465A");
		c3.a = alpha;
		if (img4 != null)
		{
			GuiArt.Draw(img4, new Rect(num12, num13, num11, num10), c3);
		}
		float num14 = 1.1f;
		string title = card.Title;
		while (num14 > 0.5f && Art.Measure(title) * num14 > num11 - 140f * num3)
		{
			num14 -= 0.05f;
		}
		float num15 = num12 + 90f * num3;
		float num16 = num13 + num10 / 2f;
		Color c4 = new Color(0f, 0f, 0f, 0.35f * alpha);
		if (card.Sub.Length > 0)
		{
			float num17 = num14 * 0.55f;
			Art.DrawLine(card.Sub, num15, num16 - (float)Art.LineHeight * (num14 + num17) / 2f, num17, new Color(1f, 1f, 1f, 0.85f * alpha));
			Art.DrawLine(title, num15 + 3f, num16 - (float)Art.LineHeight * (num14 - num17) / 2f + 3f, num14, c4);
			Art.DrawLine(title, num15, num16 - (float)Art.LineHeight * (num14 - num17) / 2f, num14, c);
		}
		else
		{
			Art.DrawLine(title, num15 + 3f, num16 - (float)Art.LineHeight * num14 / 2f + 3f, num14, c4);
			Art.DrawLine(title, num15, num16 - (float)Art.LineHeight * num14 / 2f, num14, c);
		}
		Color color = new Color(0.2f, 0.16f, 0.14f, alpha);
		float num18 = 0.8f;
		float num19 = 90f * num3;
		float num20 = 120f * num3 + 330f * num3 + Art.Measure("00:00.000") * num18;
		float y = num8 + 70f * num3;
		float num21 = num6 + (num5 - num20) / 2f;
		float num22 = num21 + 120f * num3;
		statsTop = y;
		float k = (card.Viewing ? 1f : ((phase == Phase.Shown) ? Mathf.Clamp01(shownFor / 0.3f) : ((phase == Phase.Out) ? 1f : 0f)));
		k = 1f - (1f - k) * (1f - k);
		Func<int, string> func = (int v) => (v >= 0) ? N(Mathf.RoundToInt((float)v * k)) : N(v);
		Stat(Art.Image("collectables/skullGold") ?? Art.Skull, "Deaths", func(card.Deaths), num21, num22, ref y, num19, num18, color, alpha, num3);
		float num23 = y;
		Stat(ClockIcon(), "Time", (card.Time < 0f) ? "-" : FormatTime(card.Time * k), num21, num22, ref y, num19, num18, color, alpha, num3);
		if (card.NewBest || card.PrevBest >= 0f)
		{
			string line = (card.NewBest ? "New best!" : ("Best " + FormatTime(card.PrevBest)));
			float x = num22 + 330f * num3 + Art.Measure(FormatTime(card.Time)) * num18 + 24f * num3;
			float num24 = num18 * 0.6f;
			Art.DrawLine(line, x, num23 + num19 / 2f - (float)Art.LineHeight * num24 / 2f, num24, card.NewBest ? new Color(0.75f, 0.5f, 0.02f, alpha) : new Color(color.r, color.g, color.b, 0.6f * alpha));
		}
		if (!card.AreaMode)
		{
			Stat(Art.Image("collectables/dashes"), "Dashes", func(card.Dashes), num21, num22, ref y, num19, num18, color, alpha, num3);
			Stat(Art.Image("collectables/heartgem/1/spin00"), "Hits taken", func(card.Hits), num21, num22, ref y, num19, num18, color, alpha, num3);
			Stat(Art.Image("collectables/heartgem/0/spin00"), "Heals", func(card.Heals), num21, num22, ref y, num19, num18, color, alpha, num3);
		}
		GuiArt.Img img5 = PixelBerry() ?? Art.Image("collectables/goldberry");
		float num25 = num8 + num9 * 0.8f;
		float num26 = 130f * num3 * 1.4f;
		if (!card.AreaMode && card.Time >= 0f)
		{
			List<string> list = new List<string>();
			if (card.Viewing ? (card.Deaths == 0) : card.FirstTry)
			{
				list.Add("First Try");
			}
			if (card.Hits == 0)
			{
				list.Add("No Hit");
			}
			if (card.Heals == 0)
			{
				list.Add("No Heals");
			}
			float num27 = num18 * 0.55f;
			float num28 = (float)Art.LineHeight * num27 + 14f * num3;
			float num29 = num21;
			float num30 = num25 - num28 - 22f * num3;
			float num31 = num6 + num5 - 70f * num3 - ((card.Goldens > 0) ? (64f * num3 + 120f * num3) : 0f);
			if (list.Count > 0 && num30 > y - 16f * num3)
			{
				for (int num32 = 0; num32 < list.Count; num32++)
				{
					float num33 = (card.Viewing ? 1f : Mathf.Clamp01((shownFor - 0.3f - (float)num32 * 0.12f) / 0.15f));
					if (phase == Phase.Out)
					{
						num33 = 1f;
					}
					if (!(num33 <= 0f))
					{
						float num34 = Art.Measure(list[num32]) * num27 + 28f * num3;
						if (num29 + num34 > num31)
						{
							break;
						}
						Color color2 = ((list[num32] == "No Hit") ? new Color(0.75f, 0.5f, 0.05f, 0.9f * alpha * num33) : new Color(0.2f, 0.16f, 0.14f, 0.85f * alpha * num33));
						float num35 = 0.85f + 0.15f * num33;
						Rect position = new Rect(num29 + num34 * (1f - num35) / 2f, num30 + num28 * (1f - num35) / 2f, num34 * num35, num28 * num35);
						GUI.color = color2;
						GUI.DrawTexture(position, Texture2D.whiteTexture);
						GUI.color = Color.white;
						Art.DrawLine(list[num32], num29 + 14f * num3, num30 + 7f * num3, num27, new Color(1f, 0.97f, 0.9f, alpha * num33));
						num29 += num34 + 12f * num3;
					}
				}
			}
		}
		float num36 = ((card.Viewing || phase == Phase.Shown) ? (Mathf.Sin((card.Viewing ? Time.unscaledTime : shownFor) * 3f) * 6f) : 0f);
		string line2;
		string line3;
		if (card.AreaMode)
		{
			line2 = card.Note;
			line3 = "Deaths and time here.";
		}
		else if (card.Viewing)
		{
			line2 = (card.Golden ? "Golden Berry" : "No Golden Berry");
			line3 = (card.Golden ? "Collected." : ((card.Time < 0f) ? "From before records." : "Beat it without healing."));
		}
		else if (card.Golden && !card.HadGolden)
		{
			line2 = "Golden Berry!";
			line3 = ((card.FirstTry && card.Heals > 0) ? "Beaten on the first try." : (card.FirstTry ? "First try, no heals!" : "Beaten without healing."));
		}
		else if (card.Golden || card.HadGolden)
		{
			line2 = "Golden Berry";
			line3 = (card.Golden ? "No heals again!" : "Already collected.");
		}
		else if (card.Assist)
		{
			line2 = "No Golden Berry";
			line3 = "Assist Mode was on.";
		}
		else
		{
			line2 = "No Golden Berry";
			line3 = "Now: beat it without healing.";
		}
		if (img5 != null && !card.AreaMode)
		{
			Rect r = new Rect(num21 - num26 * 0.15f, num25 + (num9 * 0.2f - num26) / 2f + num36, num26, num26);
			GuiArt.Draw(img5, r, new Color(1f, 1f, 1f, (flag ? 1f : 0.3f) * alpha));
			if (flag)
			{
				float unscaledTime = Time.unscaledTime;
				for (int num37 = 0; num37 < 5; num37++)
				{
					float num38 = Mathf.Sin(Mathf.Repeat(unscaledTime * 0.9f + (float)num37 * 0.37f, 1f) * MathF.PI);
					float f = (float)num37 * 2.4f + Mathf.Floor(unscaledTime * 0.9f + (float)num37 * 0.37f) * 1.7f;
					float num39 = num26 * (0.45f + 0.12f * Mathf.Sin((float)num37 * 3.1f));
					Vector2 vector = r.center + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num39;
					float num40 = (5f + 5f * num38) * num3 * 1.6f;
					GUI.color = new Color(1f, 1f, 0.85f, num38 * alpha);
					GUI.DrawTexture(new Rect(vector.x - num40 / 2f, vector.y - 1.5f, num40, 3f), Texture2D.whiteTexture);
					GUI.DrawTexture(new Rect(vector.x - 1.5f, vector.y - num40 / 2f, 3f, num40), Texture2D.whiteTexture);
				}
				GUI.color = Color.white;
			}
		}
		float num41 = num18 * 1.05f;
		float num42 = num18 * 0.7f;
		float num43 = num25 + num9 * 0.1f - (float)Art.LineHeight * (num41 + num42) / 2f;
		Art.DrawLine(line2, num22 + 40f * num3, num43, num41, flag ? new Color(0.55f, 0.38f, 0.02f, alpha) : color);
		Art.DrawLine(line3, num22 + 40f * num3, num43 + (float)Art.LineHeight * num41, num42, new Color(color.r, color.g, color.b, 0.75f * alpha));
		if (img5 != null && card.Goldens > 0 && !card.AreaMode)
		{
			string line4 = "x" + card.Goldens.ToString(CultureInfo.InvariantCulture);
			float num44 = 64f * num3;
			float num45 = num18 * 0.75f;
			float num46 = num44 + 8f + Art.Measure(line4) * num45;
			float num47 = num6 + num5 - 70f * num3 - num46;
			float num48 = num25 - num44 - 24f * num3;
			GuiArt.Draw(img5, new Rect(num47, num48, num44, num44), c);
			Art.DrawLine(line4, num47 + num44 + 8f, num48 + num44 / 2f - (float)Art.LineHeight * num45 / 2f, num45, color);
		}
		GuiArt.Img img6 = Art.Image("assistmodestamp");
		if (card.Assist && img6 != null)
		{
			Color c5 = Hex("B8323C");
			c5.a = 0.85f * alpha;
			GuiArt.Draw(img6, new Rect(num6 + num5 - 260f * num3, num8 + num9 * 0.5f, 157f * num3 * 1.3f, 131f * num3 * 1.3f), c5);
		}
		if (!card.Viewing && phase == Phase.Shown && shownFor > 0.75f)
		{
			float a = Mathf.Clamp01((shownFor - 0.75f) * 2f);
			float num49 = 0.55f;
			float top = Math.Min(1072f - (float)Art.LineHeight * num49, num8 + num9 + 6f);
			Art.DrawOutlined("Jump to continue", num2 - 60f - Art.Measure("Jump to continue") * num49, top, num49, new Color(1f, 1f, 1f, a), new Color(0f, 0f, 0f, a), 2f);
		}
		GUI.color = Color.white;
	}

	private void Stat(GuiArt.Img icon, string label, string value, float iconX, float textLeft, ref float y, float isz, float rs, Color ink, float alpha, float sc)
	{
		if (icon != null)
		{
			float num = isz * (float)icon.W / (float)Math.Max(icon.W, icon.H);
			float num2 = isz * (float)icon.H / (float)Math.Max(icon.W, icon.H);
			Rect r = new Rect(iconX + (isz - num) / 2f, y + (isz - num2) / 2f, num, num2);
			float num3 = 6f * sc;
			GuiArt.Draw(icon, new Rect(r.x + num3, r.y + num3, r.width, r.height), new Color(0.2f, 0.12f, 0.08f, 0.28f * alpha));
			GuiArt.Draw(icon, new Rect(r.x + num3 * 0.5f, r.y + num3 * 0.5f, r.width, r.height), new Color(0.2f, 0.12f, 0.08f, 0.18f * alpha));
			GuiArt.Draw(icon, r, new Color(1f, 1f, 1f, alpha));
		}
		float num4 = y + isz / 2f - (float)Art.LineHeight * rs / 2f;
		Art.DrawLine(label, textLeft, num4, rs * 0.8f, new Color(ink.r, ink.g, ink.b, 0.7f * alpha));
		Art.DrawLine(value, textLeft + 330f * sc, num4 - (float)Art.LineHeight * rs * 0.1f, rs, ink);
		if (y > statsTop + 1f)
		{
			GUI.color = new Color(ink.r, ink.g, ink.b, 0.18f * alpha);
			float y2 = y - 14f * sc;
			for (float num5 = textLeft; num5 < textLeft + 560f * sc; num5 += 14f * sc)
			{
				GUI.DrawTexture(new Rect(num5, y2, 7f * sc, 2f), Texture2D.whiteTexture);
			}
			GUI.color = Color.white;
		}
		y += isz + 28f * sc;
	}

	private static GuiArt.Img ClockIcon()
	{
		if (clock != null)
		{
			return clock;
		}
		Texture2D texture2D = new Texture2D(32, 32, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Point,
			wrapMode = TextureWrapMode.Clamp
		};
		Color32[] px = new Color32[1024];
		Color32 color = new Color32(43, 30, 24, byte.MaxValue);
		Color32 color2 = new Color32(0, 0, 0, 0);
		Color color3 = new Color(1f, 0.89f, 0.5f);
		Color color4 = new Color(0.9f, 0.66f, 0.18f);
		Color color5 = new Color(0.62f, 0.4f, 0.08f);
		Color b = new Color(1f, 0.98f, 0.92f);
		Color color6 = new Color(0.86f, 0.8f, 0.68f);
		float num = 15.5f;
		float num2 = 13.5f;
		float R = 12.5f;
		Func<float, float, float> func = (float dx, float dy) => Mathf.Clamp01(0.5f + (0f - dx + dy) / (2f * R));
		for (int num3 = 0; num3 < 32; num3++)
		{
			for (int num4 = 0; num4 < 32; num4++)
			{
				float num5 = (float)num4 - num;
				float num6 = (float)num3 - num2;
				float num7 = Mathf.Sqrt(num5 * num5 + num6 * num6);
				Color color7;
				if (num7 <= R - 4.2f)
				{
					float num8 = func(num5, num6);
					color7 = Color.Lerp(color6, b, Mathf.Clamp01(num8 * 1.3f));
					if (num7 > R - 5.6f && num8 < 0.55f)
					{
						color7 = Color.Lerp(color7, color6 * 0.85f, 0.5f);
					}
				}
				else if (num7 <= R - 1.2f)
				{
					float num9 = func(num5, num6);
					color7 = ((num9 > 0.62f) ? color3 : ((num9 > 0.32f) ? color4 : color5));
				}
				else
				{
					if (!(num7 <= R + 0.3f))
					{
						px[num3 * 32 + num4] = color2;
						continue;
					}
					color7 = color;
				}
				color7.a = 1f;
				px[num3 * 32 + num4] = color7;
			}
		}
		Action<int, int, Color32> action = delegate(int xx, int yy, Color32 col)
		{
			if (xx >= 0 && yy >= 0 && xx < 32 && yy < 32)
			{
				px[yy * 32 + xx] = col;
			}
		};
		for (int num10 = 13; num10 <= 18; num10++)
		{
			for (int num11 = 28; num11 <= 30; num11++)
			{
				action(num10, num11, (num10 == 13 || num10 == 18 || num11 == 30 || num11 == 28) ? color : ((Color32)((num10 < 16) ? color3 : color4)));
			}
		}
		for (int num12 = 26; num12 <= 27; num12++)
		{
			action(14, num12, color);
			action(15, num12, color4);
			action(16, num12, color5);
			action(17, num12, color);
		}
		Color32 arg = new Color32(120, 92, 60, byte.MaxValue);
		action(15, 21, arg);
		action(16, 21, arg);
		action(15, 6, arg);
		action(16, 6, arg);
		action(8, 13, arg);
		action(8, 14, arg);
		action(23, 13, arg);
		action(23, 14, arg);
		for (int num13 = 1; num13 <= 6; num13++)
		{
			action(15, 13 + num13, color);
			action(16, 13 + num13, color);
		}
		for (int num14 = 1; num14 <= 4; num14++)
		{
			action(16 + num14, 13, color);
			action(16 + num14, 14, color);
		}
		action(15, 13, color4);
		action(16, 13, color3);
		action(15, 14, color3);
		action(16, 14, color4);
		action(7, 21, Color.white);
		action(8, 22, Color.white);
		action(6, 20, color3);
		texture2D.SetPixels32(px);
		texture2D.Apply();
		clock = new GuiArt.Img
		{
			W = 32,
			H = 32,
			Tex = texture2D
		};
		return clock;
	}

	private static Color Hex(string hex)
	{
		ColorUtility.TryParseHtmlString("#" + hex, out var color);
		return color;
	}

	private static void DrawSpriteMesh(Sprite sp, Rect dst, float guiScale, float alpha)
	{
		DrawSpriteMesh(sp, dst, guiScale, new Color(1f, 1f, 1f, alpha));
	}

	private static void DrawSpriteMesh(Sprite sp, Rect dst, float guiScale, Color tint)
	{
		if (meshMat == null)
		{
			meshMat = new Material(Shader.Find("Sprites/Default"));
		}
		meshMat.mainTexture = sp.texture;
		Bounds bounds = sp.bounds;
		Vector2[] vertices = sp.vertices;
		Vector2[] uv = sp.uv;
		ushort[] triangles = sp.triangles;
		GL.PushMatrix();
		GL.LoadPixelMatrix();
		meshMat.SetPass(0);
		GL.Begin(4);
		GL.Color(tint);
		ushort[] array = triangles;
		foreach (ushort num in array)
		{
			float num2 = dst.x + (vertices[num].x - bounds.min.x) / bounds.size.x * dst.width;
			float num3 = dst.y + (bounds.max.y - vertices[num].y) / bounds.size.y * dst.height;
			GL.TexCoord2(uv[num].x, uv[num].y);
			GL.Vertex3(num2 * guiScale, (float)Screen.height - num3 * guiScale, 0f);
		}
		GL.End();
		GL.PopMatrix();
	}

	private static string N(int v)
	{
		if (v >= 0)
		{
			return v.ToString(CultureInfo.InvariantCulture);
		}
		return "-";
	}
}
