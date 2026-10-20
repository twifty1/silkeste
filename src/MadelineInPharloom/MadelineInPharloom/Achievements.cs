using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

public class Achievements : MonoBehaviour
{
	public enum Tier
	{
		Easy,
		Medium,
		Hard,
		Farewell
	}

	public enum Kind
	{
		Movement,
		Combat,
		Golden,
		Explore,
		Silly,
		Summit
	}

	public class Def
	{
		public string Id;

		public string Name;

		public string How;

		public Tier Tier;

		public Kind Kind;

		public int Target;
	}

	public static readonly Def[] All = new Def[32]
	{
		D("wavedasher", "Wavedasher", "Do 50 wavedashes.", Tier.Easy, Kind.Movement, 50),
		D("climber", "Climber", "Climb 250 m of wall in total.", Tier.Easy, Kind.Movement, 250),
		D("jellyfish", "Jellyfish Jam", "Glide for 60 seconds without landing.", Tier.Hard, Kind.Movement, 60),
		D("hyperactive", "Hyperactive", "Chain a hyper (or wavedash) straight into a super.", Tier.Medium, Kind.Movement, 1),
		D("untouchable", "Untouchable", "In a boss fight, dash through 10 attacks in a row without getting hit.", Tier.Hard, Kind.Combat, 10),
		D("combo", "Combo Breaker", "Hit 2 enemies with one dash.", Tier.Easy, Kind.Combat, 2),
		D("pogo", "Pogo Pro", "Bounce off spikes 25 times in one room.", Tier.Medium, Kind.Combat, 25),
		D("dash_master", "Dash Master", "Beat an Act 2 boss with only dash hits: no silk skills, no tools.", Tier.Farewell, Kind.Combat, 1),
		D("first_golden", "First Golden", "Earn your first golden berry.", Tier.Easy, Kind.Golden, 1),
		D("golden_hoard", "Golden Hoard", "Earn 5 golden berries.", Tier.Hard, Kind.Golden, 5),
		D("flawless", "Flawless", "Beat a boss without taking a single hit.", Tier.Farewell, Kind.Golden, 1),
		D("golden_all", "Golden Everything", "Earn 10 golden berries.", Tier.Farewell, Kind.Golden, 10),
		D("tourist", "Tourist", "Visit 10 areas.", Tier.Easy, Kind.Explore, 10),
		D("fashionista", "Fashionista", "Wear every skin.", Tier.Easy, Kind.Explore, 4),
		D("photographer", "Photographer", "Take 25 photos with Theo's camera ({camera}).", Tier.Medium, Kind.Explore, 25),
		D("night_owl", "Night Owl", "Play 10 hours on one save.", Tier.Medium, Kind.Explore, 10),
		D("achoo", "Achoo", "Stand still until Madeline sneezes.", Tier.Easy, Kind.Silly, 1),
		D("sleepy", "Sleepy", "Rest at benches 30 times.", Tier.Easy, Kind.Silly, 30),
		D("spam_dash", "Spam Dash", "Get dash-locked by hitting enemies 50 times.", Tier.Medium, Kind.Silly, 50),
		D("skill_issue", "Skill Issue", "Die 50 times.", Tier.Easy, Kind.Silly, 50),
		D("speedrunner", "Speedrunner?", "Beat a boss in under 60 seconds.", Tier.Farewell, Kind.Silly, 1),
		// for this update's features (user 2026-10-17)
		D("encore", "Encore", "Win a boss rematch from Records.", Tier.Easy, Kind.Combat, 1),
		D("regular", "Regular", "Win 15 boss rematches.", Tier.Medium, Kind.Combat, 15),
		D("hard_knocks", "Hard Knocks", "Beat 5 bosses in One-Hit Mode.", Tier.Hard, Kind.Combat, 5),
		D("one_shot", "One Shot", "Beat an Act 2 boss in One-Hit Mode.", Tier.Farewell, Kind.Combat, 1),
		D("sky_high", "Sky High", "Ride Silk Soar 20 times.", Tier.Easy, Kind.Movement, 20),
		D("busker", "Busker", "Play the Needolin for 60 seconds in total.", Tier.Easy, Kind.Silly, 60),
		D("chronicler", "Chronicler", "Fill one save's Run page with 25 moments.", Tier.Medium, Kind.Explore, 25),
		D("postman", "Postman", "Collect every postcard.", Tier.Medium, Kind.Explore, 5),
		D("speed_demon", "Speed Demon", "Reach a speed of 400 by chaining dashes, supers and hypers.", Tier.Hard, Kind.Movement, 1),
		D("deathless", "Deathless", "Play for 60 minutes in a row without dying.", Tier.Hard, Kind.Combat, 60),
		D("summit", "The Summit", "Unlock every other achievement.", Tier.Farewell, Kind.Summit, 1)
	};

	public static readonly string[] TierNames = new string[4] { "Easy", "Medium", "Hard", "Farewell" };

	public static readonly string[] KindNames = new string[6] { "Movement", "Combat", "Golden Berries", "Exploring", "Silly", "Summit" };

	public static Achievements Instance;

	private readonly Dictionary<string, int> progress = new Dictionary<string, int>();

	private readonly Dictionary<string, DateTime> unlocked = new Dictionary<string, DateTime>();

	private readonly HashSet<string> skinsWorn = new HashSet<string>();

	private bool loaded;

	private readonly HashSet<int> dodgedThisDash = new HashSet<int>();

	private int dodgeStreak;

	private int dodgeDash = -1;

	private string pogoScene;

	private int pogoCount;

	private static bool otherAttack;

	private int lastWave = -1;

	private int lastHyper = -1;

	private int lastSuper = -1;

	private int lastHp = -1;

	private float hyperAt = -9f;

	private float climbCarry;

	private float glideFor;

	private bool wasBench;

	private bool wasDead;

	private bool wasLocked;

	private float lastY;

	private bool loggedError;

	private bool dirty;

	private float saveIn = 2f;

	public static int Selected;

	public static int SortMode;

	private static readonly string[] SortNames = new string[4] { "Sort: Easiest first", "Sort: Hardest first", "Sort: Category A-Z", "Sort: Category Z-A" };

	private static Rect sortRect;

	public static bool Confirming;

	private static int choice;

	private static float holdAll;

	private static readonly Rect[] choiceRects = new Rect[3];

	private const float HoldAllTime = 1.5f;

	private static readonly string[] Choices = new string[3] { "No", "Reset this one", "Reset ALL" };

	private static readonly Rect[] cellRects = new Rect[All.Length];

	private const int Cols = 8;

	private readonly List<Def> banners = new List<Def>();

	private float bannerT = -1f;

	private bool popSoundDone;

	private int summitCue;

	private static readonly KeyValuePair<float, string>[] SummitCues = new KeyValuePair<float, string>[9]
	{
		new KeyValuePair<float, string>(0f, Sounds.FeatherStateEnd.Id),
		new KeyValuePair<float, string>(0.35f, Sounds.Refill.Id),
		new KeyValuePair<float, string>(0.55f, Sounds.Refill.Id),
		new KeyValuePair<float, string>(0.75f, Sounds.Refill.Id),
		new KeyValuePair<float, string>(1f, Sounds.JumpSuperwall.Id),
		new KeyValuePair<float, string>(1.05f, Sounds.GoldenGet.Id),
		new KeyValuePair<float, string>(1.1f, Sounds.Revive.Id),
		new KeyValuePair<float, string>(1.9f, Sounds.FeatherGet.Id),
		new KeyValuePair<float, string>(2.6f, Sounds.CampfireStand.Id)
	};

	private int testTier;

	private const float BannerLife = 4.5f;

	private const float SummitLife = 8f;

	public static bool StartingArea
	{
		get
		{
			string currentAreaName = BossCard.CurrentAreaName;
			if (currentAreaName != null)
			{
				return currentAreaName.IndexOf("Moss Grotto", StringComparison.OrdinalIgnoreCase) >= 0;
			}
			return false;
		}
	}

	private static string FilePath => Path.Combine(BossCard.DataDir ?? "", "achievements.cfg");

	private static GuiArt Art => SpeedrunTimer.Art;

	private static Def D(string id, string name, string how, Tier t, Kind k, int target)
	{
		return new Def
		{
			Id = id,
			Name = name,
			How = how,
			Tier = t,
			Kind = k,
			Target = target
		};
	}

	public static Color KindColor(Kind k)
	{
		return k switch
		{
			Kind.Movement => new Color(0.36f, 0.62f, 1f), 
			Kind.Combat => new Color(0.66f, 0.72f, 0.78f), 
			Kind.Golden => new Color(1f, 0.84f, 0.3f), 
			Kind.Explore => new Color(0.65f, 0.5f, 0.38f), 
			Kind.Summit => Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.25f, 1f), 0.45f, 1f), 
			_ => new Color(0.24f, 0.86f, 0.77f), 
		};
	}

	public static Color TierColor(Tier t, float time)
	{
		switch (t)
		{
		case Tier.Easy:
			return new Color(0.36f, 0.9f, 0.42f);
		case Tier.Medium:
			return new Color(1f, 0.6f, 0.15f);
		case Tier.Hard:
			return new Color(1f, 0.25f, 0.25f);
		default:
		{
			float t2 = 0.5f + 0.5f * Mathf.Sin(time * 2.2f);
			return Color.Lerp(new Color(0.62f, 0.3f, 1f), new Color(1f, 0.45f, 0.85f), t2);
		}
		}
	}

	public static string HowText(Def d)
	{
		return d.How.Replace("{camera}", AssistMode.KeyName(AssistOptions.KeyCamera) + " or L3");
	}

	public static int Progress(Def d)
	{
		if (!(Instance == null))
		{
			return Math.Min(d.Target, Instance.Get(d.Id));
		}
		return 0;
	}

	public static bool Unlocked(Def d, out DateTime when)
	{
		when = default(DateTime);
		if (Instance != null)
		{
			return Instance.unlocked.TryGetValue(d.Id, out when);
		}
		return false;
	}

	public static int UnlockedCount(Tier t)
	{
		int num = 0;
		Def[] all = All;
		foreach (Def def in all)
		{
			if (def.Tier == t && Unlocked(def, out var _))
			{
				num++;
			}
		}
		return num;
	}

	public static int TierCount(Tier t)
	{
		int num = 0;
		Def[] all = All;
		for (int i = 0; i < all.Length; i++)
		{
			if (all[i].Tier == t)
			{
				num++;
			}
		}
		return num;
	}

	private void Awake()
	{
		Instance = this;
		try
		{
			MethodInfo methodInfo = AccessTools.Method(typeof(InputHandler), "SetCursorEnabled");
			if (methodInfo != null)
			{
				new Harmony("silkeste.menucursor").Patch(methodInfo, new HarmonyMethod(typeof(Achievements), "CursorPrefix"));
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("menu cursor patch: " + ex.Message);
		}
	}

	private static void CursorPrefix(ref bool isEnabled)
	{
		if (AssistMode.IsOpen || BossCard.RecordsOpen)
		{
			isEnabled = !MenuMouse.PadActive;
		}
	}

	private int Get(string id)
	{
		Load();
		if (!progress.TryGetValue(id, out var value))
		{
			return 0;
		}
		return value;
	}

	public static void Add(string id, int n = 1)
	{
		if (Instance != null)
		{
			Instance.Set(id, Instance.Get(id) + n);
		}
	}

	public static void Best(string id, int v)
	{
		if (Instance != null && v > Instance.Get(id))
		{
			Instance.Set(id, v);
		}
	}

	// Deathless: minutes played since the last death (paused time doesn't count)
	private float aliveFor;

	public static void Done(string id)
	{
		Best(id, 1);
	}

	private void Set(string id, int v)
	{
		Load();
		if (AssistMode.SaveMarked)
		{
			return;
		}
		Def def = Array.Find(All, (Def x) => x.Id == id);
		if (def != null && !unlocked.ContainsKey(id))
		{
			progress[id] = v;
			if (v >= def.Target)
			{
				unlocked[id] = DateTime.Now;
				banners.Add(def);
				Plugin.Log.LogInfo("Achievement unlocked: " + def.Name);
			}
			dirty = true;
		}
	}

	private static bool StartingBoss(string title)
	{
		if (title != null)
		{
			return title.IndexOf("Moss Mother", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return false;
	}

	public static void Dodged(GameObject attacker)
	{
		if (!(Instance == null) && !(attacker == null) && !StartingArea && BossCard.InFight)
		{
			Achievements instance = Instance;
			if (instance.dodgeDash != MoveStats.Dashes)
			{
				instance.dodgeDash = MoveStats.Dashes;
				instance.dodgedThisDash.Clear();
			}
			if (instance.dodgedThisDash.Add(attacker.transform.root.GetInstanceID()))
			{
				instance.dodgeStreak++;
				MoveStats.Dodges = instance.dodgeStreak;
				Best("untouchable", instance.dodgeStreak);
			}
		}
	}

	public static void DashEnemyHits(int n)
	{
		if (n > 0 && !StartingArea)
		{
			Best("combo", n);
		}
	}

	public static void Pogoed()
	{
		if (!(Instance == null) && !StartingArea)
		{
			Achievements instance = Instance;
			string text = ((GameManager.SilentInstance != null) ? GameManager.SilentInstance.sceneName : "");
			if (text != instance.pogoScene)
			{
				instance.pogoScene = text;
				instance.pogoCount = 0;
			}
			Best("pogo", ++instance.pogoCount);
		}
	}

	public static void Sneezed()
	{
		Done("achoo");
	}

	public static void Photo()
	{
		Add("photographer");
	}

	public static void CornerCorrected()
	{
		Add("corner_kid");
	}

	public static void FightStarted()
	{
		otherAttack = false;
	}

	public static void UsedNonDash()
	{
		otherAttack = true;
	}

	public static void BossBeaten(string title, float time, int dashes, int hits, bool golden, bool assist, int goldens, int beaten, int goldenBeaten)
	{
		if (!assist && !StartingBoss(title))
		{
			PlayerData instance = PlayerData.instance;
			if (!otherAttack && instance != null && instance.act2Started)
			{
				Done("dash_master");
			}
			if (hits == 0)
			{
				Done("flawless");
			}
			if (time >= 0f && time < 60f)
			{
				Done("speedrunner");
			}
			if (goldens > 0)
			{
				Done("first_golden");
			}
			Best("golden_hoard", goldens);
			Best("golden_all", goldens);
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
			if (!loggedError)
			{
				loggedError = true;
				Plugin.Log.LogError("Achievements: " + ex);
			}
		}
	}

	private void LateUpdate()
	{
		MenuMouse.PadActive = MenuMouse.PadHints(MenuMouse.PadActive);
		if (AssistMode.IsOpen || BossCard.RecordsOpen)
		{
			Cursor.visible = !MenuMouse.PadActive;
			Cursor.lockState = CursorLockMode.None;
		}
	}

	private void Tick()
	{
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		UpdateBanners(unscaledDeltaTime);
		if (dirty && (saveIn -= unscaledDeltaTime) <= 0f)
		{
			saveIn = 2f;
			Save();
		}
		HeroController silentInstance = HeroController.UnsafeInstance;
		GameManager silentInstance2 = GameManager.SilentInstance;
		MadelineController controller = Plugin.Controller;
		if (!Plugin.Ready || silentInstance == null || silentInstance2 == null || controller == null || silentInstance2.IsMenuScene())
		{
			return;
		}
		Load();
		if (lastWave >= 0 && MoveStats.Wavedashes > lastWave)
		{
			Add("wavedasher", MoveStats.Wavedashes - lastWave);
		}
		if (lastHyper >= 0 && MoveStats.Hypers + MoveStats.Wavedashes > lastHyper)
		{
			hyperAt = Time.unscaledTime;
		}
		if (lastSuper >= 0 && MoveStats.Supers > lastSuper && Time.unscaledTime - hyperAt < 0.9f)
		{
			Done("hyperactive");
		}
		lastWave = MoveStats.Wavedashes;
		lastHyper = MoveStats.Hypers + MoveStats.Wavedashes;
		lastSuper = MoveStats.Supers;
		float y = silentInstance.transform.position.y;
		if (controller.State == 1 && y > lastY && y - lastY < 1f)
		{
			climbCarry += y - lastY;
			if (climbCarry >= 1f)
			{
				int num = (int)climbCarry;
				climbCarry -= num;
				Add("climber", num);
			}
		}
		lastY = y;
		// Speed Demon: Madeline's own speed (the speedometer's number)
		if (controller.Active && controller.Speed.magnitude >= 400f)
		{
			Done("speed_demon");
		}
		if (silentInstance.cState.dead || silentInstance.cState.hazardDeath)
		{
			aliveFor = 0f;
		}
		else if (!silentInstance2.isPaused)
		{
			aliveFor += Time.unscaledDeltaTime;
			Best("deathless", (int)(aliveFor / 60f));
		}
		if (controller.Gliding && !controller.OnGround)
		{
			glideFor += Time.deltaTime;
			Best("jellyfish", (int)glideFor);
		}
		else
		{
			glideFor = 0f;
		}
		bool atBench = silentInstance.playerData.atBench;
		if (atBench && !wasBench)
		{
			Add("sleepy");
		}
		wasBench = atBench;
		bool dead = silentInstance.cState.dead;
		if (dead && !wasDead)
		{
			Add("skill_issue");
		}
		wasDead = dead;
		bool dashLocked = controller.DashLocked;
		if (dashLocked && !wasLocked)
		{
			Add("spam_dash");
		}
		wasLocked = dashLocked;
		int health = silentInstance.playerData.health;
		if (lastHp >= 0 && health < lastHp && !controller.EnemyImmune)
		{
			dodgeStreak = 0;
			MoveStats.Dodges = 0;
		}
		lastHp = health;
		if (!unlocked.ContainsKey("summit"))
		{
			bool flag = true;
			Def[] all = All;
			foreach (Def def in all)
			{
				if (def.Id != "summit" && !unlocked.ContainsKey(def.Id))
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				Done("summit");
			}
		}
		skinsWorn.Add(AssistMode.SkinName);
		Best("fashionista", skinsWorn.Count);
		Best("tourist", BossCard.AreasVisited);
		Best("night_owl", (int)(silentInstance2.PlayTime / 3600f));
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
			if (!File.Exists(FilePath))
			{
				return;
			}
			string[] array = File.ReadAllLines(FilePath);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(new char[1] { '=' });
				if (array2.Length != 2)
				{
					continue;
				}
				if (array2[0] == "skins")
				{
					string[] array3 = array2[1].Split(new char[1] { ',' });
					foreach (string text in array3)
					{
						if (text.Length > 0)
						{
							skinsWorn.Add(text);
						}
					}
				}
				else
				{
					string[] array4 = array2[1].Split(new char[1] { '|' });
					if (int.TryParse(array4[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
					{
						progress[array2[0]] = result;
					}
					if (array4.Length > 1 && long.TryParse(array4[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
					{
						unlocked[array2[0]] = new DateTime(result2);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("achievements.cfg: " + ex.Message);
		}
	}

	private void Save()
	{
		dirty = false;
		try
		{
			List<string> list = new List<string>();
			Def[] all = All;
			foreach (Def def in all)
			{
				progress.TryGetValue(def.Id, out var value);
				list.Add(def.Id + "=" + value.ToString(CultureInfo.InvariantCulture) + (unlocked.TryGetValue(def.Id, out var value2) ? ("|" + value2.Ticks.ToString(CultureInfo.InvariantCulture)) : ""));
			}
			list.Add("skins=" + string.Join(",", new List<string>(skinsWorn).ToArray()));
			File.WriteAllLines(FilePath, list.ToArray());
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("achievements.cfg: " + ex.Message);
		}
	}

	private void OnApplicationQuit()
	{
		if (dirty)
		{
			Save();
		}
	}

	internal static GuiArt.Img IconFor(Kind k)
	{
		if (Art == null)
		{
			return null;
		}
		return k switch
		{
			Kind.Movement => Art.Image("collectables/dashes"), 
			Kind.Combat => Art.Image("collectables/skullGold") ?? Art.Skull, 
			Kind.Golden => BossCard.PixelBerry() ?? Art.Image("collectables/goldberry"), 
			Kind.Explore => Art.Image("collectables/strawberry"), 
			Kind.Summit => Art.Image("collectables/heartgem/2/spin" + ((int)(Time.unscaledTime * 12f) % 11).ToString("00")) ?? Art.Image("collectables/heartgem/2/spin00"), 
			_ => Art.Image("collectables/heartgem/0/spin00"), 
		};
	}

	public static void Glow(Rect r, Color c, float size, float strength)
	{
		for (int num = 10; num >= 1; num--)
		{
			float num2 = (float)num / 10f;
			float num3 = size * num2;
			GUI.color = new Color(c.r, c.g, c.b, strength * (1f - num2) * (1f - num2) * 0.5f);
			GUI.DrawTexture(new Rect(r.x - num3, r.y - num3, r.width + num3 * 2f, r.height + num3 * 2f), Texture2D.whiteTexture);
		}
		GUI.color = Color.white;
	}

	private static void Border(Rect r, float w, Color c)
	{
		GUI.color = c;
		GUI.DrawTexture(new Rect(r.x, r.y, r.width, w), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(r.x, r.yMax - w, r.width, w), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(r.xMax - w, r.y, w, r.height), Texture2D.whiteTexture);
		GUI.color = Color.white;
	}

	private static int[] Order()
	{
		List<int> list = new List<int>();
		for (int i = 0; i < All.Length; i++)
		{
			list.Add(i);
		}
		list.Sort(delegate(int a, int b)
		{
			int num;
			if (SortMode >= 2)
			{
				num = string.Compare(KindNames[(int)All[a].Kind], KindNames[(int)All[b].Kind], StringComparison.Ordinal);
				if (SortMode == 3)
				{
					num = -num;
				}
				if (num == 0)
				{
					num = All[a].Tier.CompareTo(All[b].Tier);
				}
			}
			else
			{
				num = ((SortMode == 1) ? All[b].Tier.CompareTo(All[a].Tier) : All[a].Tier.CompareTo(All[b].Tier));
			}
			return (num == 0) ? a.CompareTo(b) : num;
		});
		return list.ToArray();
	}

	public static void OpenConfirm()
	{
		Confirming = true;
		choice = 0;
		holdAll = 0f;
		Sfx.Play(Sounds.MenuPause.Id);
	}

	public static void CancelConfirm()
	{
		Confirming = false;
		holdAll = 0f;
		Sfx.Play(Sounds.MenuUnpause.Id);
	}

	public static void ConfirmInput(bool left, bool right, bool confirm, bool held)
	{
		int num = choice;
		if (left)
		{
			choice = (choice + 2) % 3;
		}
		if (right)
		{
			choice = (choice + 1) % 3;
		}
		Mouse current = Mouse.current;
		Vector2 point = MenuMouse.Gui();
		bool flag = false;
		if (current != null)
		{
			if (current.delta.ReadValue().sqrMagnitude > 0.5f || current.leftButton.wasPressedThisFrame)
			{
				for (int i = 0; i < 3; i++)
				{
					if (choiceRects[i].Contains(point))
					{
						choice = i;
					}
				}
			}
			if (current.leftButton.wasPressedThisFrame && choiceRects[choice].Contains(point))
			{
				confirm = true;
			}
			flag = current.leftButton.isPressed && choiceRects[2].Contains(point);
		}
		if (choice != num)
		{
			holdAll = 0f;
			Sfx.Play(Sounds.MenuRolloverDown.Id);
		}
		if (choice == 2)
		{
			if (held || flag)
			{
				holdAll += Time.unscaledDeltaTime;
			}
			else
			{
				holdAll = Math.Max(0f, holdAll - Time.unscaledDeltaTime * 2f);
			}
			if (holdAll >= 1.5f)
			{
				Instance?.ResetAll();
				Confirming = false;
				holdAll = 0f;
				Sfx.Play(Sounds.CardOut.Id);
			}
		}
		else if (confirm)
		{
			if (choice == 0)
			{
				CancelConfirm();
				return;
			}
			Instance?.ResetOne(All[Order()[Selected]].Id);
			Confirming = false;
			Sfx.Play(Sounds.CardOut.Id);
		}
	}

	private void ResetOne(string id)
	{
		Load();
		progress.Remove(id);
		unlocked.Remove(id);
		if (id == "fashionista")
		{
			skinsWorn.Clear();
		}
		if (id == "untouchable")
		{
			dodgeStreak = 0;
			MoveStats.Dodges = 0;
		}
		dirty = true;
		Save();
		Plugin.Log.LogInfo("Achievement reset: " + id);
	}

	private void ResetAll()
	{
		Load();
		progress.Clear();
		unlocked.Clear();
		skinsWorn.Clear();
		banners.Clear();
		bannerT = -1f;
		dodgeStreak = 0;
		MoveStats.Dodges = 0;
		dirty = true;
		Save();
		Plugin.Log.LogInfo("Achievements: all reset");
	}

	private static void DrawConfirm(float sw, float alpha)
	{
		Color outline = new Color(0f, 0f, 0f, alpha);
		_ = (float)Screen.height / 1080f;
		GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha);
		GUI.DrawTexture(new Rect(0f, 0f, sw, 1080f), Texture2D.whiteTexture);
		Def def = All[Order()[Selected]];
		Color color = TierColor(def.Tier, Time.unscaledTime);
		color.a = alpha;
		float num = 900f;
		float num2 = 300f;
		float x = sw / 2f - num / 2f;
		float num3 = 540f - num2 / 2f;
		Rect rect = new Rect(x, num3, num, num2);
		Glow(rect, new Color(1f, 0.3f, 0.3f), 24f, 0.4f * alpha);
		GUI.color = new Color(0.07f, 0.06f, 0.09f, 0.97f * alpha);
		GUI.DrawTexture(rect, Texture2D.whiteTexture);
		GUI.color = new Color(1f, 0.3f, 0.3f, alpha);
		GUI.DrawTexture(new Rect(x, num3, num, 5f), Texture2D.whiteTexture);
		GUI.color = Color.white;
		Art.DrawOutlined("Reset achievements?", sw / 2f - Art.Measure("Reset achievements?") * 0.8f / 2f, num3 + 26f, 0.8f, new Color(1f, 1f, 1f, alpha), outline, 3f);
		string line = ((choice == 2) ? "Every achievement goes back to zero. This can't be undone." : ("\"" + def.Name + "\" goes back to zero. This can't be undone."));
		float num4 = 0.5f;
		while (num4 > 0.3f && Art.Measure(line) * num4 > num - 60f)
		{
			num4 -= 0.02f;
		}
		Art.DrawOutlined(line, sw / 2f - Art.Measure(line) * num4 / 2f, num3 + 26f + (float)Art.LineHeight * 0.8f + 14f, num4, new Color(0.8f, 0.8f, 0.8f, alpha), outline, 2f);
		float num5 = 0.55f;
		float num6 = 30f;
		float num7 = 0f - num6;
		for (int i = 0; i < 3; i++)
		{
			num7 += Art.Measure(Choices[i]) * num5 + 50f + num6;
		}
		float num8 = sw / 2f - num7 / 2f;
		float num9 = num3 + num2 - 110f;
		float num10 = (float)Art.LineHeight * num5 + 22f;
		for (int j = 0; j < 3; j++)
		{
			float num11 = Art.Measure(Choices[j]) * num5 + 50f;
			Rect rect2 = new Rect(num8, num9, num11, num10);
			choiceRects[j] = rect2;
			bool flag = j == choice;
			Color color2 = ((j == 0) ? new Color(0.85f, 0.85f, 0.85f, alpha) : new Color(1f, 0.35f, 0.35f, alpha));
			GUI.color = new Color(color2.r, color2.g, color2.b, (flag ? 0.28f : 0.08f) * alpha);
			GUI.DrawTexture(rect2, Texture2D.whiteTexture);
			if (j == 2 && flag && holdAll > 0f)
			{
				GUI.color = new Color(1f, 0.3f, 0.3f, 0.55f * alpha);
				GUI.DrawTexture(new Rect(rect2.x, rect2.y, rect2.width * Mathf.Clamp01(holdAll / 1.5f), rect2.height), Texture2D.whiteTexture);
			}
			GUI.color = Color.white;
			if (flag)
			{
				Border(rect2, 3f, new Color(1f, 1f, 1f, alpha));
			}
			Art.DrawOutlined(Choices[j], rect2.x + 25f, rect2.y + 11f, num5, flag ? new Color(1f, 1f, 1f, alpha) : color2, outline, 2f);
			num8 += num11 + num6;
		}
		string line2 = ((choice == 2) ? "Hold to reset everything" : "Press to choose   Back to cancel");
		Art.DrawOutlined(line2, sw / 2f - Art.Measure(line2) * 0.4f / 2f, num9 + num10 + 16f, 0.4f, new Color(0.6f, 0.6f, 0.6f, alpha), outline, 2f);
	}

	public static void ToggleSort()
	{
		SortMode = (SortMode + 1) % SortNames.Length;
		Selected = 0;
		Sfx.Play(Sounds.MenuButtonSelect.Id);
	}

	public static void Navigate(bool left, bool right, bool up, bool down)
	{
		int num = All.Length;
		int selected = Selected;
		if (left)
		{
			Selected = (Selected + num - 1) % num;
		}
		if (right)
		{
			Selected = (Selected + 1) % num;
		}
		if (up)
		{
			Selected = ((Selected - 8 >= 0) ? (Selected - 8) : Selected);
		}
		if (down)
		{
			Selected = ((Selected + 8 < num) ? (Selected + 8) : Selected);
		}
		Mouse current = Mouse.current;
		if (MenuMouse.Clicked(sortRect))
		{
			ToggleSort();
			return;
		}
		if (current != null && (current.delta.ReadValue().sqrMagnitude > 0.5f || current.leftButton.wasPressedThisFrame))
		{
			Vector2 point = MenuMouse.Gui();
			for (int i = 0; i < num; i++)
			{
				if (cellRects[i].Contains(point))
				{
					Selected = i;
					break;
				}
			}
		}
		if (current != null && current.rightButton.wasPressedThisFrame && cellRects[Selected].Contains(MenuMouse.Gui()))
		{
			OpenConfirm();
		}
		else if (Selected != selected)
		{
			Sfx.Play(Sounds.MenuRolloverDown.Id);
		}
	}

	// The records journal's Achievements page: the same badges, sorting and details as DrawGrid,
	// inked onto Celeste's journal paper to match the other pages.
	// "Almost there": the three locked achievements you're closest to, along the bottom (user 2026-10-18)
	private static void DrawAlmost(Rect r, float alpha)
	{
		List<Def> close = new List<Def>();
		foreach (Def d in All)
		{
			if (d.Target > 1 && !Unlocked(d, out _) && Progress(d) > 0)
			{
				close.Add(d);
			}
		}
		if (close.Count == 0)
		{
			return;
		}
		close.Sort((Def a, Def b) => ((float)Progress(b) / (float)b.Target).CompareTo((float)Progress(a) / (float)a.Target));
		float s = 0.38f;
		float x = r.x;
		Art.DrawLine("Almost there:", x, r.y, s, new Color(0.36f, 0.32f, 0.42f, alpha));
		x += Art.Measure("Almost there:") * s + 24f;
		for (int i = 0; i < Math.Min(3, close.Count); i++)
		{
			Def d = close[i];
			int pct = Mathf.FloorToInt(100f * (float)Progress(d) / (float)d.Target);
			Color c = TierColor(d.Tier, Time.unscaledTime);
			GUI.color = new Color(c.r, c.g, c.b, alpha);
			GUI.DrawTexture(new Rect(x, r.y + 6f, 12f, 12f), Texture2D.whiteTexture);
			string t = d.Name + "  " + pct + "%";
			Art.DrawLine(t, x + 20f, r.y, s, new Color(0.17f, 0.14f, 0.24f, alpha));
			x += 20f + Art.Measure(t) * s + 34f;
		}
	}

	public static void DrawJournal(Rect area, float alpha)
	{
		if (Art == null)
		{
			return;
		}
		Instance?.Load();
		float t = Time.unscaledTime;
		Color ink = new Color(0.17f, 0.14f, 0.24f, alpha);
		Color soft = new Color(0.36f, 0.32f, 0.42f, alpha);
		float x = area.x;
		float y = area.y;
		for (int i = 0; i < 4; i++)
		{
			Color c = TierColor((Tier)i, t);
			string text = TierNames[i];
			string text2 = UnlockedCount((Tier)i) + " / " + TierCount((Tier)i);
			GUI.color = new Color(c.r, c.g, c.b, alpha);
			GUI.DrawTexture(new Rect(x, y + 10f, 18f, 18f), Texture2D.whiteTexture);
			Art.DrawLine(text, x + 28f, y, 0.46f, ink);
			float num = x + 28f + Art.Measure(text) * 0.46f + 12f;
			Art.DrawLine(text2, num, y + 4f, 0.4f, soft);
			x = num + Art.Measure(text2) * 0.4f + 46f;
		}
		string line = SortNames[SortMode];
		float num2 = Art.Measure(line) * 0.38f + 34f;
		float num3 = (float)Art.LineHeight * 0.38f + 12f;
		sortRect = new Rect(area.xMax - num2, y, num2, num3);
		bool flag = sortRect.Contains(MenuMouse.Gui());
		GUI.color = new Color(0.17f, 0.14f, 0.24f, (flag ? 0.95f : 0.8f) * alpha);
		GUI.DrawTexture(sortRect, Texture2D.whiteTexture);
		Art.DrawLine(line, sortRect.x + 17f, y + 6f, 0.38f, new Color(1f, 1f, 1f, alpha));
		y += num3 + 22f;
		float num4 = 90f;
		float num5 = 22f;
		int num6 = (All.Length + 7) / 8;
		float num7 = 8f * num4 + 7f * num5;
		float num8 = area.center.x - num7 / 2f;
		Selected = Mathf.Clamp(Selected, 0, All.Length - 1);
		int[] array = Order();
		for (int j = 0; j < All.Length; j++)
		{
			Def def = All[array[j]];
			Rect rect = new Rect(num8 + (float)(j % 8) * (num4 + num5), y + (float)(j / 8) * (num4 + num5), num4, num4);
			cellRects[j] = rect;
			DateTime when;
			bool flag2 = Unlocked(def, out when);
			bool flag3 = j == Selected;
			Color c2 = TierColor(def.Tier, t + (float)j * 0.3f);
			if (flag3)
			{
				GUI.color = new Color(0.95f, 0.75f, 0.25f, 0.35f * alpha);
				GUI.DrawTexture(new Rect(rect.x - 9f, rect.y - 9f, rect.width + 18f, rect.height + 18f), Texture2D.whiteTexture);
				rect = new Rect(rect.x, rect.y - 4f, rect.width, rect.height);
			}
			GUI.color = new Color(0f, 0f, 0f, 0.12f * alpha);
			GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 5f, rect.width, rect.height), Texture2D.whiteTexture);
			GUI.color = (flag2 ? new Color(Mathf.Lerp(1f, c2.r, 0.3f), Mathf.Lerp(1f, c2.g, 0.3f), Mathf.Lerp(1f, c2.b, 0.3f), alpha) : new Color(0.88f, 0.86f, 0.84f, alpha));
			GUI.DrawTexture(rect, Texture2D.whiteTexture);
			float num9 = (flag3 ? 5f : 3f);
			Border(rect, num9, flag2 ? new Color(c2.r * 0.8f, c2.g * 0.8f, c2.b * 0.8f, alpha) : new Color(0.36f, 0.32f, 0.42f, 0.5f * alpha));
			GuiArt.Img img = IconFor(def.Kind);
			if (img != null)
			{
				float num10 = rect.width * 0.58f;
				float num11 = num10 * (float)img.W / (float)Math.Max(img.W, img.H);
				float num12 = num10 * (float)img.H / (float)Math.Max(img.W, img.H);
				float num13 = (flag2 ? (Mathf.Sin(t * 3f + (float)j) * 2f) : 0f);
				GuiArt.Draw(img, new Rect(rect.center.x - num11 / 2f, rect.center.y - num12 / 2f - 5f + num13, num11, num12), flag2 ? new Color(1f, 1f, 1f, alpha) : new Color(0.17f, 0.14f, 0.24f, 0.25f * alpha));
			}
			if (!flag2 && def.Target > 1)
			{
				float num14 = (float)Progress(def) / (float)def.Target;
				Rect position = new Rect(rect.x + 10f, rect.yMax - 14f, rect.width - 20f, 6f);
				GUI.color = new Color(0.17f, 0.14f, 0.24f, 0.15f * alpha);
				GUI.DrawTexture(position, Texture2D.whiteTexture);
				GUI.color = new Color(c2.r * 0.85f, c2.g * 0.85f, c2.b * 0.85f, alpha);
				GUI.DrawTexture(new Rect(position.x, position.y, position.width * num14, position.height), Texture2D.whiteTexture);
			}
			if (!flag2 && def.Target <= 1)
			{
				Art.DrawLine("?", rect.xMax - 8f - Art.Measure("?") * 0.4f, rect.y + 4f, 0.4f, new Color(0.36f, 0.32f, 0.42f, 0.7f * alpha));
			}
		}
		y += (float)num6 * (num4 + num5) + 8f;
		DrawAlmost(new Rect(area.x, area.yMax - (float)Art.LineHeight * 0.38f - 2f, area.width, (float)Art.LineHeight * 0.38f), alpha);
		Def def2 = All[array[Selected]];
		DateTime when2;
		bool flag4 = Unlocked(def2, out when2);
		Color color = TierColor(def2.Tier, t);
		Rect rect2 = new Rect(area.x, y, area.width, Math.Min(170f, area.yMax - y - (float)Art.LineHeight * 0.38f - 14f));   // room for "Almost there" under it
		if (rect2.height > 60f)
		{
			GUI.color = new Color(color.r, color.g, color.b, 0.14f * alpha);
			GUI.DrawTexture(rect2, Texture2D.whiteTexture);
			GUI.color = new Color(color.r * 0.85f, color.g * 0.85f, color.b * 0.85f, alpha);
			GUI.DrawTexture(new Rect(rect2.x, rect2.y, 6f, rect2.height), Texture2D.whiteTexture);
			float num15 = rect2.x + 30f;
			float num16 = rect2.y + 14f;
			Art.DrawLine(def2.Name, num15, num16, 0.72f, flag4 ? new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, alpha) : ink);
			float num17 = num15 + Art.Measure(def2.Name) * 0.72f + 22f;
			float num18 = num16 + ((float)Art.LineHeight * 0.72f - ((float)Art.LineHeight * 0.36f + 10f)) / 2f;
			string[] array2 = new string[2]
			{
				TierNames[(int)def2.Tier].ToUpperInvariant(),
				KindNames[(int)def2.Kind].ToUpperInvariant()
			};
			Color[] array3 = new Color[2]
			{
				color,
				KindColor(def2.Kind)
			};
			for (int k = 0; k < 2; k++)
			{
				float num19 = Art.Measure(array2[k]) * 0.36f + 24f;
				GUI.color = new Color(array3[k].r * 0.85f, array3[k].g * 0.85f, array3[k].b * 0.85f, alpha);
				GUI.DrawTexture(new Rect(num17, num18, num19, (float)Art.LineHeight * 0.36f + 10f), Texture2D.whiteTexture);
				Art.DrawLine(array2[k], num17 + 12f, num18 + 5f, 0.36f, new Color(1f, 1f, 1f, alpha));
				num17 += num19 + 10f;
			}
			num16 += (float)Art.LineHeight * 0.72f + 6f;
			Art.DrawLine(HowText(def2), num15, num16, 0.46f, soft);
			num16 += (float)Art.LineHeight * 0.46f + 8f;
			if (flag4)
			{
				Art.DrawLine("Unlocked " + when2.ToString("d MMM yyyy", CultureInfo.InvariantCulture), num15, num16, 0.42f, new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, alpha));
			}
			else if (def2.Target > 1)
			{
				int num20 = Progress(def2);
				Rect position2 = new Rect(num15, num16 + 8f, rect2.width - 260f, 10f);
				GUI.color = new Color(0.17f, 0.14f, 0.24f, 0.15f * alpha);
				GUI.DrawTexture(position2, Texture2D.whiteTexture);
				GUI.color = new Color(color.r * 0.85f, color.g * 0.85f, color.b * 0.85f, alpha);
				GUI.DrawTexture(new Rect(position2.x, position2.y, position2.width * (float)num20 / (float)def2.Target, position2.height), Texture2D.whiteTexture);
				Art.DrawLine(num20 + " / " + def2.Target, position2.xMax + 20f, num16, 0.42f, ink);
			}
			else
			{
				Art.DrawLine("Locked", num15, num16, 0.42f, soft);
			}
			if (def2.Kind == Kind.Combat || def2.Kind == Kind.Golden || def2.Id == "speedrunner")
			{
				Art.DrawLine("Moss Grotto and Moss Mother don't count.", num15, num16 + (float)Art.LineHeight * 0.42f + 6f, 0.34f, soft);
			}
		}
		GUI.color = Color.white;
		if (Confirming)
		{
			DrawConfirm(area.center.x * 2f, alpha);
		}
	}

	public static void DrawGrid(float sw, float top, float alpha)
	{
		if (Art == null)
		{
			return;
		}
		Instance?.Load();
		float unscaledTime = Time.unscaledTime;
		Color outline = new Color(0f, 0f, 0f, alpha);
		float num = 0.5f;
		float num2 = 46f;
		float num3 = 0f - num2;
		string[] array = new string[4];
		for (int i = 0; i < 4; i++)
		{
			array[i] = TierNames[i] + "  " + UnlockedCount((Tier)i) + "/" + TierCount((Tier)i);
			num3 += 26f + Art.Measure(array[i]) * num + num2;
		}
		float num4 = sw / 2f - num3 / 2f;
		for (int j = 0; j < 4; j++)
		{
			Color color = TierColor((Tier)j, unscaledTime);
			color.a = alpha;
			GUI.color = color;
			GUI.DrawTexture(new Rect(num4, top + 8f, 16f, 16f), Texture2D.whiteTexture);
			GUI.color = Color.white;
			Art.DrawOutlined(array[j], num4 + 26f, top, num, new Color(0.9f, 0.9f, 0.9f, alpha), outline, 2f);
			num4 += 26f + Art.Measure(array[j]) * num + num2;
		}
		top += (float)Art.LineHeight * num + 14f;
		string line = SortNames[SortMode];
		float num5 = 0.42f;
		float num6 = Art.Measure(line) * num5 + 30f;
		float num7 = (float)Art.LineHeight * num5 + 12f;
		sortRect = new Rect(sw / 2f - num6 / 2f, top, num6, num7);
		bool flag = sortRect.Contains(MenuMouse.Gui());
		MenuStyle.Panel(sortRect, alpha * (flag ? 2f : 1f));
		Art.DrawOutlined(line, sortRect.x + 15f, top + 6f, num5, flag ? new Color(1f, 0.9f, 0.5f, alpha) : new Color(0.85f, 0.85f, 0.85f, alpha), outline, 2f);
		top += num7 + 16f;
		float num8 = 92f;
		float num9 = 20f;
		int num10 = (All.Length + 8 - 1) / 8;
		float num11 = 8f * num8 + 7f * num9;
		float num12 = sw / 2f - num11 / 2f;
		Selected = Mathf.Clamp(Selected, 0, All.Length - 1);
		int[] array2 = Order();
		for (int k = 0; k < All.Length; k++)
		{
			Def def = All[array2[k]];
			int num13 = k % 8;
			int num14 = k / 8;
			Rect rect = new Rect(num12 + (float)num13 * (num8 + num9), top + (float)num14 * (num8 + num9), num8, num8);
			cellRects[k] = rect;
			DateTime when;
			bool flag2 = Unlocked(def, out when);
			bool flag3 = k == Selected;
			Color c = TierColor(def.Tier, unscaledTime + (float)k * 0.3f);
			float num15 = 0.75f + 0.25f * Mathf.Sin(unscaledTime * 2.4f + (float)k * 0.7f);
			if (flag3)
			{
				rect = new Rect(rect.x - 6f, rect.y - 6f, rect.width + 12f, rect.height + 12f);
			}
			Color c2 = KindColor(def.Kind);
			float strength = (flag2 ? (0.9f * num15) : 0.45f) * alpha * (flag3 ? 1.25f : 1f);
			float num16 = rect.width * 0.12f;
			Glow(new Rect(rect.x - num16 * 0.5f, rect.y - num16 * 0.5f, rect.width - num16 * 0.5f, rect.height - num16 * 0.5f), c, flag2 ? 16f : 11f, strength);
			Glow(new Rect(rect.x + num16, rect.y + num16, rect.width - num16 * 0.5f, rect.height - num16 * 0.5f), c2, flag2 ? 16f : 11f, strength);
			GUI.color = new Color(0.07f, 0.06f, 0.09f, 0.95f * alpha);
			GUI.DrawTexture(rect, Texture2D.whiteTexture);
			GUI.color = new Color(c.r, c.g, c.b, (flag2 ? 0.22f : 0.07f) * alpha);
			GUI.DrawTexture(rect, Texture2D.whiteTexture);
			float num17 = (flag3 ? 5f : 4f);
			float a = (flag2 ? 1f : 0.75f) * alpha;
			Color color2 = KindColor(def.Kind);
			GUI.color = new Color(c.r, c.g, c.b, a);
			GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, num17), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(rect.x, rect.y, num17, rect.height), Texture2D.whiteTexture);
			GUI.color = new Color(color2.r, color2.g, color2.b, a);
			GUI.DrawTexture(new Rect(rect.x, rect.yMax - num17, rect.width, num17), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(rect.xMax - num17, rect.y, num17, rect.height), Texture2D.whiteTexture);
			GUI.color = Color.white;
			if (flag3)
			{
				Border(new Rect(rect.x - 6f, rect.y - 6f, rect.width + 12f, rect.height + 12f), 2f, new Color(1f, 1f, 1f, alpha * (0.6f + 0.4f * Mathf.Sin(unscaledTime * 6f))));
			}
			GuiArt.Img img = IconFor(def.Kind);
			if (img != null)
			{
				float num18 = rect.width * 0.56f;
				float num19 = num18 * (float)img.W / (float)Math.Max(img.W, img.H);
				float num20 = num18 * (float)img.H / (float)Math.Max(img.W, img.H);
				float num21 = (flag2 ? (Mathf.Sin(unscaledTime * 3f + (float)k) * 2f) : 0f);
				Rect r = new Rect(rect.center.x - num19 / 2f, rect.center.y - num20 / 2f - 6f + num21, num19, num20);
				GuiArt.Draw(img, r, flag2 ? new Color(1f, 1f, 1f, alpha) : new Color(0.16f, 0.15f, 0.2f, alpha));
			}
			if (!flag2 && def.Target > 1)
			{
				float num22 = (float)Progress(def) / (float)def.Target;
				Rect position = new Rect(rect.x + 10f, rect.yMax - 16f, rect.width - 20f, 6f);
				GUI.color = new Color(1f, 1f, 1f, 0.12f * alpha);
				GUI.DrawTexture(position, Texture2D.whiteTexture);
				GUI.color = new Color(c.r, c.g, c.b, 0.9f * alpha);
				GUI.DrawTexture(new Rect(position.x, position.y, position.width * num22, position.height), Texture2D.whiteTexture);
				GUI.color = Color.white;
			}
			if (!flag2 && def.Target <= 1)
			{
				Art.DrawOutlined("?", rect.xMax - 10f - Art.Measure("?") * 0.42f, rect.y + 6f, 0.42f, new Color(c.r, c.g, c.b, 0.75f * alpha), outline, 2f);
			}
		}
		float num23 = top + (float)num10 * (num8 + num9) + 14f;
		Def def2 = All[Order()[Selected]];
		DateTime when2;
		bool flag4 = Unlocked(def2, out when2);
		Color color3 = TierColor(def2.Tier, unscaledTime);
		color3.a = alpha;
		bool flag5 = def2.Kind == Kind.Combat || def2.Kind == Kind.Golden || def2.Id == "speedrunner";
		float num24 = Math.Min(sw - 160f, 980f);
		float num25 = sw / 2f - num24 / 2f;
		float height = (flag5 ? 222f : 190f);
		MenuStyle.Panel(new Rect(num25, num23, num24, height), alpha);
		GUI.color = color3;
		GUI.DrawTexture(new Rect(num25, num23, 6f, height), Texture2D.whiteTexture);
		GUI.color = Color.white;
		float num26 = num25 + 34f;
		float num27 = num23 + 20f;
		float num28 = 0.85f;
		Art.DrawOutlined(def2.Name, num26, num27, num28, flag4 ? color3 : new Color(0.85f, 0.85f, 0.85f, alpha), outline, 3f);
		string line2 = TierNames[(int)def2.Tier].ToUpperInvariant();
		float num29 = 0.42f;
		float num30 = Art.Measure(line2) * num29 + 28f;
		float num31 = num26 + Art.Measure(def2.Name) * num28 + 24f;
		float num32 = num27 + ((float)Art.LineHeight * num28 - ((float)Art.LineHeight * num29 + 10f)) / 2f;
		GUI.color = new Color(color3.r, color3.g, color3.b, 0.85f * alpha);
		GUI.DrawTexture(new Rect(num31, num32, num30, (float)Art.LineHeight * num29 + 10f), Texture2D.whiteTexture);
		GUI.color = Color.white;
		Art.DrawOutlined(line2, num31 + 14f, num32 + 5f, num29, new Color(1f, 1f, 1f, alpha), outline, 2f);
		string line3 = KindNames[(int)def2.Kind].ToUpperInvariant();
		Color color4 = KindColor(def2.Kind);
		float num33 = num31 + num30 + 12f;
		float width = Art.Measure(line3) * num29 + 28f;
		GUI.color = new Color(color4.r, color4.g, color4.b, 0.85f * alpha);
		GUI.DrawTexture(new Rect(num33, num32, width, (float)Art.LineHeight * num29 + 10f), Texture2D.whiteTexture);
		GUI.color = Color.white;
		Art.DrawOutlined(line3, num33 + 14f, num32 + 5f, num29, new Color(1f, 1f, 1f, alpha), outline, 2f);
		num27 += (float)Art.LineHeight * num28 + 10f;
		Art.DrawOutlined(HowText(def2), num26, num27, 0.55f, new Color(0.88f, 0.88f, 0.88f, alpha), outline, 2f);
		num27 += (float)Art.LineHeight * 0.55f + 14f;
		if (flag4)
		{
			string line4 = "Unlocked " + when2.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
			Art.DrawOutlined(line4, num26, num27, 0.5f, color3, outline, 2f);
		}
		else if (def2.Target > 1)
		{
			int num34 = Progress(def2);
			Rect position2 = new Rect(num26, num27 + 10f, num24 - 260f, 12f);
			GUI.color = new Color(1f, 1f, 1f, 0.12f * alpha);
			GUI.DrawTexture(position2, Texture2D.whiteTexture);
			GUI.color = color3;
			GUI.DrawTexture(new Rect(position2.x, position2.y, position2.width * (float)num34 / (float)def2.Target, position2.height), Texture2D.whiteTexture);
			GUI.color = Color.white;
			string line5 = num34 + " / " + def2.Target;
			Art.DrawOutlined(line5, position2.xMax + 20f, num27, 0.5f, new Color(0.9f, 0.9f, 0.9f, alpha), outline, 2f);
		}
		else
		{
			Art.DrawOutlined("Locked", num26, num27, 0.5f, new Color(0.6f, 0.6f, 0.6f, alpha), outline, 2f);
		}
		if (flag5)
		{
			Art.DrawOutlined("Moss Grotto and Moss Mother don't count.", num26, num27 + (float)Art.LineHeight * 0.5f + 12f, 0.4f, new Color(0.55f, 0.55f, 0.55f, alpha), outline, 2f);
		}
		if (Confirming)
		{
			DrawConfirm(sw, alpha);
		}
	}

	private void UpdateBanners(float dt)
	{
		if (banners.Count == 0)
		{
			return;
		}
		if (bannerT < 0f)
		{
			bannerT = 0f;
			Sfx.Play(Sounds.CardIn.Id, 0.7f);
			if (banners[0].Id == "summit")
			{
				summitCue = 0;
				popSoundDone = true;
			}
			else
			{
				switch (banners[0].Tier)
				{
				case Tier.Easy:
					Sfx.Play(Sounds.Refill.Id);
					break;
				case Tier.Medium:
					Sfx.Play(Sounds.FeatherGet.Id);
					break;
				case Tier.Hard:
					Sfx.Play(Sounds.GoldenGet.Id);
					break;
				default:
					Sfx.Play(Sounds.GoldenGet.Id);
					Sfx.Play(Sounds.Revive.Id, 0.8f);
					Sfx.Play(Sounds.JumpSuperwall.Id, 0.6f);
					break;
				}
			}
			popSoundDone = false;
		}
		float num = bannerT;
		bannerT += dt;
		if (banners[0].Id == "summit")
		{
			while (summitCue < SummitCues.Length && bannerT >= SummitCues[summitCue].Key)
			{
				string value = SummitCues[summitCue].Value;
				float volume = ((value == Sounds.Refill.Id) ? 0.6f : 1f);
				Sfx.Play(value, volume);
				summitCue++;
			}
		}
		if (!popSoundDone && num < 0.32f && bannerT >= 0.32f)
		{
			popSoundDone = true;
			Sfx.Play(Sounds.Refill.Id, (banners[0].Tier >= Tier.Hard) ? 0.8f : 0.45f);
		}
		if (bannerT > LifeOf(banners[0]))
		{
			banners.RemoveAt(0);
			bannerT = -1f;
		}
	}

	private static float LifeOf(Def d)
	{
		if (!(d.Id == "summit"))
		{
			return 4.5f;
		}
		return 8f;
	}

	private void DrawSummit(Def d)
	{
		float num = bannerT;
		float num2 = 8f;
		float now = Time.unscaledTime;
		float num3 = (float)Screen.height / 1080f;
		float num4 = (float)Screen.width / num3;
		Matrix4x4 matrix = GUI.matrix;
		GUI.depth = -5;
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num3, num3, 1f));
		float num5 = ((num > num2 - 0.8f) ? Mathf.Clamp01((num2 - num) / 0.8f) : 1f);
		Vector2 vector = new Vector2(num4 / 2f, 430f);
		Func<float, float, Color> func = delegate(float off, float a4)
		{
			Color result = Color.HSVToRGB(Mathf.Repeat(now * 0.3f + off, 1f), 0.55f, 1f);
			result.a = a4;
			return result;
		};
		GUI.color = new Color(0f, 0f, 0f, 0.6f * Mathf.Clamp01(num / 0.6f) * num5);
		GUI.DrawTexture(new Rect(0f, 0f, num4, 1080f), Texture2D.whiteTexture);
		float num6 = Mathf.Clamp01((num - 0.9f) / 0.6f) * num5;
		if (num6 > 0f)
		{
			Matrix4x4 matrix2 = GUI.matrix;
			for (int num7 = 0; num7 < 18; num7++)
			{
				GUI.matrix = matrix2;
				GUIUtility.RotateAroundPivot((float)num7 * 20f + now * 12f, vector * num3);
				float width = 900f;
				float num8 = 26f + 18f * Mathf.Sin(now * 2f + (float)num7);
				GUI.color = func((float)num7 / 18f, 0.1f * num6);
				GUI.DrawTexture(new Rect(vector.x, vector.y - num8 / 2f, width, num8), Texture2D.whiteTexture);
				GUI.color = func((float)num7 / 18f, 0.06f * num6);
				GUI.DrawTexture(new Rect(vector.x, vector.y - num8, width, num8 * 2f), Texture2D.whiteTexture);
			}
			GUI.matrix = matrix2;
		}
		Glow(new Rect(vector.x - 90f, vector.y - 90f, 180f, 180f), func(0f, 1f), 120f, 0.7f * num6);
		for (int num9 = 0; num9 < 4; num9++)
		{
			float num10 = (num - 1f - (float)num9 * 0.3f) / 1.4f;
			if (!(num10 <= 0f) && !(num10 >= 1f))
			{
				float num11 = 120f + num10 * 900f;
				Border(new Rect(vector.x - num11 / 2f, vector.y - num11 / 2f, num11, num11), 6f * (1f - num10) + 1f, func((float)num9 * 0.25f, (1f - num10) * 0.8f * num5));
			}
		}
		GuiArt.Img img = IconFor(Kind.Summit);
		float num12 = ((num < 1f) ? 0f : BackOut((num - 1f) / 0.5f));
		if (img != null && num12 > 0f)
		{
			float num13 = 230f * num12;
			float num14 = num13 * (float)img.H / (float)Math.Max(1, img.W);
			float num15 = Mathf.Sin(now * 2.2f) * 8f;
			GuiArt.Draw(img, new Rect(vector.x - num13 / 2f + 8f, vector.y - num14 / 2f + 10f + num15, num13, num14), new Color(0f, 0f, 0f, 0.4f * num5));
			GuiArt.Draw(img, new Rect(vector.x - num13 / 2f, vector.y - num14 / 2f + num15, num13, num14), new Color(1f, 1f, 1f, num5));
		}
		float num16 = num - 1f;
		if (num16 > 0f && num16 < 2.5f)
		{
			for (int num17 = 0; num17 < 90; num17++)
			{
				float f = Hash(num17, 11) * MathF.PI * 2f;
				float num18 = 250f + Hash(num17, 12) * 650f;
				float num19 = 1f + Hash(num17, 13) * 1.4f;
				if (!(num16 > num19))
				{
					float num20 = num16 / num19;
					Vector2 vector2 = vector + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num18 * num16 * (1f - 0.5f * num20) + new Vector2(0f, 160f * num16 * num16);
					float num21 = (4f + Hash(num17, 14) * 7f) * (1f - num20 * 0.5f);
					GUI.color = ((num17 % 5 == 0) ? new Color(1f, 1f, 1f, (1f - num20) * num5) : func((float)num17 * 0.07f, (1f - num20) * num5));
					GUI.DrawTexture(new Rect(vector2.x - num21 / 2f, vector2.y - num21 / 2f, num21, num21), Texture2D.whiteTexture);
					if (num17 % 4 == 0)
					{
						GUI.DrawTexture(new Rect(vector2.x - num21 * 1.6f, vector2.y - 1f, num21 * 3.2f, 2f), Texture2D.whiteTexture);
						GUI.DrawTexture(new Rect(vector2.x - 1f, vector2.y - num21 * 1.6f, 2f, num21 * 3.2f), Texture2D.whiteTexture);
					}
				}
			}
		}
		float num22 = num - 1.1f;
		if (num22 > 0f)
		{
			for (int num23 = 0; num23 < 140; num23++)
			{
				float num24 = Hash(num23, 21) * num4;
				float num25 = Hash(num23, 22) * 3f;
				float num26 = 220f + Hash(num23, 23) * 260f;
				float num27 = num22 - num25;
				if (!(num27 <= 0f))
				{
					float num28 = -30f + num27 * num26;
					if (!(num28 > 1110f))
					{
						float x = num24 + Mathf.Sin(num27 * (1.5f + Hash(num23, 24) * 2f) + (float)num23) * 40f;
						float num29 = Mathf.Abs(Mathf.Sin(num27 * (4f + Hash(num23, 25) * 5f) + (float)num23));
						float num30 = 10f + Hash(num23, 26) * 8f;
						GUI.color = func(Hash(num23, 27), 0.9f * num5);
						GUI.DrawTexture(new Rect(x, num28, num30 * num29 + 2f, num30 * 0.6f), Texture2D.whiteTexture);
					}
				}
			}
			GuiArt.Img img2 = BossCard.PixelBerry() ?? ((Art != null) ? Art.Image("collectables/goldberry") : null);
			if (img2 != null)
			{
				for (int num31 = 0; num31 < 14; num31++)
				{
					float num32 = num22 - Hash(num31, 31) * 3.5f;
					if (!(num32 <= 0f))
					{
						float num33 = -80f + num32 * (180f + Hash(num31, 32) * 120f);
						if (!(num33 > 1100f))
						{
							float x2 = Hash(num31, 33) * (num4 - 80f) + Mathf.Sin(num32 * 1.3f + (float)num31) * 30f;
							float num34 = 54f + Hash(num31, 34) * 30f;
							GuiArt.Draw(img2, new Rect(x2, num33, num34, num34), new Color(1f, 1f, 1f, 0.95f * num5));
						}
					}
				}
			}
		}
		float num35 = Mathf.Clamp01(1f - Mathf.Abs(num - 1f) / 0.35f);
		if (num35 > 0f)
		{
			GUI.color = new Color(1f, 1f, 1f, 0.85f * num35);
			GUI.DrawTexture(new Rect(0f, 0f, num4, 1080f), Texture2D.whiteTexture);
		}
		for (int num36 = 0; num36 < 14; num36++)
		{
			float num37 = (float)num36 / 14f;
			float num38 = 6f + (float)num36 * 8f;
			GUI.color = func((float)num36 * 0.03f, 0.08f * (1f - num37) * num5 * Mathf.Clamp01(num / 0.8f));
			GUI.DrawTexture(new Rect(0f, 0f, num4, num38), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(0f, 1080f - num38, num4, num38), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(0f, 0f, num38, 1080f), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(num4 - num38, 0f, num38, 1080f), Texture2D.whiteTexture);
		}
		GUI.color = Color.white;
		if (Art != null)
		{
			new Color(0f, 0f, 0f, num5);
			float a = Mathf.Clamp01((num - 1.3f) / 0.4f) * num5;
			Art.DrawOutlined("ACHIEVEMENT UNLOCKED", num4 / 2f - Art.Measure("ACHIEVEMENT UNLOCKED") * 0.5f / 2f, 640f, 0.5f, new Color(1f, 1f, 1f, a), new Color(0f, 0f, 0f, a), 2f);
			string text = d.Name.ToUpperInvariant();
			float num39 = 1.8f;
			float num40 = num4 / 2f - Art.Measure(text) * num39 / 2f;
			for (int num41 = 0; num41 < text.Length; num41++)
			{
				string line = text.Substring(num41, 1);
				float num42 = Mathf.Clamp01((num - 1.5f - (float)num41 * 0.06f) / 0.25f);
				float num43 = BackOut(num42);
				float num44 = Art.Measure(line) * num39;
				if (num42 > 0f)
				{
					float num45 = Mathf.Sin(now * 3f + (float)num41 * 0.5f) * 6f;
					float top = 690f + (1f - num43) * 60f + num45;
					Color c = func((float)num41 * 0.07f, num42 * num5);
					Art.DrawOutlined(line, num40, top, num39, c, new Color(0f, 0f, 0f, num42 * num5), 4f);
				}
				num40 += num44;
			}
			float a2 = Mathf.Clamp01((num - 2.6f) / 0.5f) * num5;
			Art.DrawOutlined("Every achievement unlocked. You reached the top!", num4 / 2f - Art.Measure("Every achievement unlocked. You reached the top!") * 0.6f / 2f, 690f + (float)Art.LineHeight * num39 + 20f, 0.6f, new Color(1f, 0.95f, 0.8f, a2), new Color(0f, 0f, 0f, a2), 2f);
			for (int num46 = 0; num46 < 4; num46++)
			{
				float a3 = Mathf.Clamp01((num - 3f - (float)num46 * 0.15f) / 0.2f) * num5;
				string line2 = TierNames[num46] + " " + TierCount((Tier)num46) + "/" + TierCount((Tier)num46);
				float num47 = Art.Measure(line2) * 0.45f + 40f;
				float num48 = num4 / 2f - 420f + (float)num46 * 210f + (210f - num47) / 2f;
				float num49 = 690f + (float)Art.LineHeight * num39 + 90f;
				Color color = TierColor((Tier)num46, now);
				color.a = a3;
				GUI.color = color;
				GUI.DrawTexture(new Rect(num48, num49 + 6f, 18f, 18f), Texture2D.whiteTexture);
				GUI.color = Color.white;
				Art.DrawOutlined(line2, num48 + 28f, num49, 0.45f, new Color(1f, 1f, 1f, a3), new Color(0f, 0f, 0f, a3), 2f);
			}
		}
		GUI.matrix = matrix;
		GUI.color = Color.white;
	}

	private static float BackOut(float t)
	{
		t = Mathf.Clamp01(t) - 1f;
		return t * t * (2.9f * t + 1.9f) + 1f;
	}

	private static float Hash(int i, int salt)
	{
		return Mathf.Repeat(Mathf.Sin((float)i * 12.9898f + (float)salt * 78.233f) * 43758.547f, 1f);
	}

	private void OnGUI()
	{
		if (Event.current.type != EventType.Repaint || banners.Count == 0 || bannerT < 0f || Art == null || TheoCamera.Hiding)
		{
			return;
		}
		Def def = banners[0];
		if (def.Id == "summit")
		{
			DrawSummit(def);
			return;
		}
		float num = bannerT;
		float num2 = 4.5f;
		float num3 = BackOut(num / 0.45f);
		float num4 = ((num > num2 - 0.5f) ? Mathf.Clamp01((num - (num2 - 0.5f)) / 0.5f) : 0f);
		float num5 = Mathf.Clamp01(num / 0.2f) * (1f - num4);
		float num6 = (float)Screen.height / 1080f;
		float num7 = (float)Screen.width / num6;
		Matrix4x4 matrix = GUI.matrix;
		GUI.depth = -4;
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num6, num6, 1f));
		float unscaledTime = Time.unscaledTime;
		Color color = TierColor(def.Tier, unscaledTime);
		color.a = num5;
		Color outline = new Color(0f, 0f, 0f, num5);
		int tier = (int)def.Tier;
		float num8 = 600f;
		float num9 = 128f;
		float num10 = num7 - num8 - 30f + (1f - num3) * (num8 + 60f) + num4 * num4 * 300f;
		float num11 = 150f;
		Rect rect = new Rect(num10, num11, num8, num9);
		if (def.Tier == Tier.Farewell)
		{
			float num12 = Mathf.Clamp01(1f - Mathf.Abs(num - 0.6f) / 1.6f) * (1f - num4);
			for (int i = 0; i < 12; i++)
			{
				float num13 = (float)i / 12f;
				float num14 = 6f + (float)i * 7f;
				Color color2 = TierColor(Tier.Farewell, unscaledTime + (float)i * 0.1f);
				color2.a = num12 * 0.07f * (1f - num13);
				GUI.color = color2;
				GUI.DrawTexture(new Rect(0f, 0f, num7, num14), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(0f, 1080f - num14, num7, num14), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(0f, 0f, num14, 1080f), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(num7 - num14, 0f, num14, 1080f), Texture2D.whiteTexture);
			}
			GUI.color = Color.white;
		}
		float num15 = 1f + 0.8f * Mathf.Clamp01(1f - Mathf.Abs(num - 0.35f) / 0.35f);
		Glow(rect, color, 22f * num15 + (float)tier * 6f, (0.55f + 0.1f * (float)tier) * num5);
		GUI.color = new Color(0.07f, 0.06f, 0.09f, 0.96f * num5);
		GUI.DrawTexture(rect, Texture2D.whiteTexture);
		GUI.color = new Color(color.r, color.g, color.b, 0.12f * num5);
		GUI.DrawTexture(rect, Texture2D.whiteTexture);
		GUI.color = color;
		GUI.DrawTexture(new Rect(num10, num11, 8f, num9), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(num10, num11 + num9 - 3f, num8 * Mathf.Clamp01(1f - num / num2), 3f), Texture2D.whiteTexture);
		float num16 = Mathf.Clamp01(1f - Mathf.Abs(num - 0.3f) / 0.15f);
		if (num16 > 0f)
		{
			GUI.color = new Color(1f, 1f, 1f, 0.5f * num16 * num5);
			GUI.DrawTexture(rect, Texture2D.whiteTexture);
		}
		for (int j = 0; j < ((tier < 2) ? 1 : 2); j++)
		{
			float num17 = (num - 0.5f - (float)j * 1.2f) / 0.6f;
			if (num17 <= 0f || num17 >= 1f)
			{
				continue;
			}
			float num18 = num10 - 80f + num17 * (num8 + 160f);
			for (int k = 0; k < 6; k++)
			{
				float num19 = num18 + (float)k * 7f - 21f;
				float num20 = Mathf.Max(num10, num19);
				float num21 = Mathf.Min(num10 + num8, num19 + 14f);
				if (!(num21 <= num20))
				{
					GUI.color = new Color(1f, 1f, 1f, 0.07f * (1f - Mathf.Abs((float)k - 2.5f) / 3f) * num5);
					GUI.DrawTexture(new Rect(num20, num11, num21 - num20, num9), Texture2D.whiteTexture);
				}
			}
		}
		GUI.color = Color.white;
		GuiArt.Img img = IconFor(def.Kind);
		Vector2 vector = new Vector2(num10 + 70f, num11 + num9 / 2f);
		float num22 = (num - 0.3f) / 0.35f;
		float num23 = ((num22 <= 0f) ? 0f : BackOut(num22));
		if (img != null && num23 > 0f)
		{
			float num24 = 82f * num23;
			float num25 = num24 * (float)img.H / (float)Math.Max(1, img.W);
			float num26 = Mathf.Sin(unscaledTime * 3f) * 2f;
			GuiArt.Draw(img, new Rect(vector.x - num24 / 2f + 4f, vector.y - num25 / 2f + 4f + num26, num24, num25), new Color(0f, 0f, 0f, 0.4f * num5));
			GuiArt.Draw(img, new Rect(vector.x - num24 / 2f, vector.y - num25 / 2f + num26, num24, num25), new Color(1f, 1f, 1f, num5));
		}
		if (num22 > 0f && num22 < 2.2f)
		{
			float num27 = num22 / 2.2f;
			float num28 = 40f + num27 * (140f + (float)tier * 40f);
			float num29 = (1f - num27) * num5;
			Border(c: new Color(color.r, color.g, color.b, num29 * 0.9f), r: new Rect(vector.x - num28 / 2f, vector.y - num28 / 2f, num28, num28), w: 3f * (1f - num27) + 1f);
		}
		int num30 = 14 + tier * 10;
		float num31 = num - 0.3f;
		if (num31 > 0f && num31 < 1.6f)
		{
			for (int l = 0; l < num30; l++)
			{
				float f = Hash(l, 1) * MathF.PI * 2f;
				float num32 = 160f + Hash(l, 2) * (220f + (float)tier * 80f);
				float num33 = 0.7f + Hash(l, 3) * 0.8f;
				if (!(num31 > num33))
				{
					float num34 = num31 / num33;
					float num35 = num32 * num31 * (1f - 0.45f * num34);
					Vector2 vector2 = vector + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num35 + new Vector2(0f, 120f * num31 * num31);
					float num36 = (3f + Hash(l, 4) * 5f) * (1f - num34 * 0.6f);
					float num37 = 0.6f + 0.4f * Mathf.Sin(unscaledTime * 25f + (float)l);
					Color color3 = ((l % 4 == 0) ? new Color(1f, 1f, 1f, 1f) : ((def.Tier == Tier.Farewell) ? TierColor(Tier.Farewell, unscaledTime + (float)l * 0.37f) : color));
					color3.a = (1f - num34) * num37 * num5;
					GUI.color = color3;
					GUI.DrawTexture(new Rect(vector2.x - num36 / 2f, vector2.y - num36 / 2f, num36, num36), Texture2D.whiteTexture);
					if (l % 3 == 0)
					{
						GUI.DrawTexture(new Rect(vector2.x - num36 * 1.5f, vector2.y - 1f, num36 * 3f, 2f), Texture2D.whiteTexture);
						GUI.DrawTexture(new Rect(vector2.x - 1f, vector2.y - num36 * 1.5f, 2f, num36 * 3f), Texture2D.whiteTexture);
					}
				}
			}
		}
		GUI.color = Color.white;
		string text = "ACHIEVEMENT UNLOCKED  -  " + TierNames[tier].ToUpperInvariant();
		int length = Mathf.Clamp(Mathf.FloorToInt((num - 0.15f) / 0.018f), 0, text.Length);
		Art.DrawOutlined(text.Substring(0, length), num10 + 132f, num11 + 20f, 0.4f, color, outline, 2f);
		float num38 = 0.75f;
		while (num38 > 0.4f && Art.Measure(def.Name) * num38 > num8 - 160f)
		{
			num38 -= 0.05f;
		}
		float num39 = Mathf.Clamp01((num - 0.4f) / 0.3f);
		float num40 = 1f - (1f - num39) * (1f - num39);
		Art.DrawOutlined(def.Name, num10 + 132f, num11 + 20f + (float)Art.LineHeight * 0.4f + 8f + (1f - num40) * 18f, num38, new Color(1f, 1f, 1f, num5 * num40), new Color(0f, 0f, 0f, num5 * num40), 3f);
		for (int m = 0; m < 4; m++)
		{
			float num41 = Mathf.Clamp01((num - 0.75f - (float)m * 0.08f) / 0.15f);
			GUI.color = ((m <= tier) ? new Color(color.r, color.g, color.b, num5 * num41) : new Color(1f, 1f, 1f, 0.15f * num5 * num41));
			GUI.DrawTexture(new Rect(num10 + 132f + (float)m * 22f, num11 + num9 - 26f, 14f, 6f), Texture2D.whiteTexture);
		}
		GUI.color = Color.white;
		GUI.matrix = matrix;
		GUI.color = Color.white;
	}
}
