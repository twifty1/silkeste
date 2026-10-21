using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MadelineInPharloom;

/// <summary>
/// Boss rematches from Records (user 2026-10-17), the safe way:
/// - Silkeste learns where each boss's arena is by looking at the scenes you walk through (the arena's scripts check
///   the boss's "defeated" flag), and which door you came in by;
/// - a rematch first copies your save file to a backup and keeps a snapshot of every save value and the rooms' saved
///   states, marks the boss unbeaten for this visit, refills your health and takes you into the arena;
/// - win, die (no cocoon, nothing lost) or walk out, and everything is put back exactly as it was before any save
///   happens; a win then sends you back to your bench.
/// Only a few plain arena bosses are on the list to begin with.
/// </summary>
public class BossRematch : MonoBehaviour
{
	// Act 1 and Act 2 bosses: defeat flag, name, arena scene (found by searching the game's scene files for the flag).
	// Anything the win changes in the world is put back afterwards like every other save value.
	// The 4th column is how the boss's room and world looked before its first fight, as far as the flags its room
	// checks tell (read out of the game's scene files): these are set for the visit and put back afterwards.
	private static readonly string[][] Act1 = new string[34][]
	{
		new string[4] { "defeatedMossMother", "Moss Mother", "Tut_03", "" },
		new string[4] { "defeatedBellBeast", "Bell Beast", "Bone_05", "BellCentipedeLocked=false" },
		new string[4] { "defeatedLace1", "Lace", "Bone_East_12", "laceLeftDocks=false,visitedCitadel=false" },
		new string[4] { "defeatedSongGolem", "Fourth Chorus", "Bone_East_08", "destroyedSongGolemRock=false" },
		new string[4] { "defeatedBoneFlyerGiant", "Savage Beastfly", "Ant_19", "completedMemory_beast=false" },
		new string[4] { "defeatedVampireGnatBoss", "Moorwing", "Greymoor_08", "VampireGnatBossInAltLoc=false,VampireGnatCorpseInWater=false,VampireGnatCorpseOnCaravan=false,VampireGnatDefeatedBeforeCaravanArrived=false,mapperIsFightingAct3=false,greymoor08_plat_destroyed=false" },
		new string[4] { "defeatedSplinterQueen", "Sister Splinter", "Shellwood_18", "splinterQueenSproutCut=false,splinterQueenSproutGrewLarge=false" },
		new string[5] { "spinnerDefeated", "Widow", "Belltown_Shrine", "SpinnerDefeatedTimePassed=false", "top1" },
		new string[4] { "defeatedPhantom", "Phantom", "Organ_01", "" },
		new string[4] { "defeatedCoralDrillers", "Great Conchflies", "Coral_11", "defeatedCoralDrillerSolo=false,coralDrillerSoloReady=false" },
		new string[4] { "defeatedLastJudge", "Last Judge", "Coral_Judge_Arena", "citadelWoken=false,SeenLastJudgeGateOpen=false,visitedCitadel=false,bellShrineBellhart=true,bellShrineBoneForest=true,bellShrineGreymoor=true,bellShrineShellwood=true,bellShrineWilds=true" },
		new string[5] { "defeatedDockForemen", "Forebrothers Signis & Gron", "Dock_09", "", "right1" },
		// Act 2
		new string[4] { "defeatedCogworkDancers", "Cogwork Dancers", "Cog_Dancers", "UnlockedMelodyLift=true" },
		new string[4] { "defeatedTrobbio", "Trobbio", "Library_13", "defeatedTormentedTrobbio=false,tormentedTrobbioLurking=false,trobbioCleanedUp=false" },
		new string[6] { "defeatedRoachkeeperChef", "Disgraced Chef Lugoli", "Dust_Chef", "roachkeeperChefCorpsePrepared=false", "bot1", "start" },
		new string[4] { "defeatedWispPyreEffigy", "Father of the Flame", "Belltown_08", "" },
		new string[4] { "DefeatedSwampShaman", "Groal the Great", "Shadow_18", "" },
		new string[4] { "defeatedCoralDrillerSolo", "Raging Conchfly", "Coral_27", "" },
		new string[4] { "defeatedFirstWeaver", "First Sinner", "Slab_10b", "hasSilkBomb=false" },
		new string[4] { "wardBossDefeated", "The Unravelled", "Ward_02", "wardBossHatchOpened=true,WillLoadWardBoss=true" },
		new string[4] { "defeatedZapCoreEnemy", "Voltvyrm", "Coral_29", "" },
		new string[4] { "defeatedCrowCourt", "Crawfather", "Room_CrowCourt", "" },
		new string[4] { "defeatedAntTrapper", "Gurr the Outcast", "Bone_East_18b", "" },
		new string[4] { "defeatedBoneFlyerGiantGolemScene", "Savage Beastfly (Far Fields)", "Bone_East_08", "" },
		new string[4] { "defeatedBroodMother", "Broodmother", "Slab_16b", "" },
		new string[4] { "defeatedSongChevalierBoss", null, "Hang_17b", "songChevalierEncounterCooldown=false" },
		new string[4] { "defeatedGreyWarrior", null, "Coral_39", "wokeGreyWarrior=false" },
		// Act 3
		new string[4] { "defeatedTormentedTrobbio", "Tormented Trobbio", "Library_13", "defeatedTrobbio=true,tormentedTrobbioLurking=true,trobbioCleanedUp=false" },
		new string[4] { "defeatedFlowerQueen", "Nyleth", "Shellwood_11b", "flowerQueenHeartAppeared=false" },
		new string[4] { "defeatedCoralKing", "Crust King Khann", "Coral_Tower_01", "coralKingHeartAppeared=false" },
		new string[4] { "defeatedCloverDancers", "Clover Dancers", "Clover_01", "cloverDancerRegrowth=false" },
		new string[4] { "defeatedWhiteCloverstag", "Palestag", "Clover_19", "" },
		new string[4] { "defeatedAntQueen", "Skarrsinger Karmelita", "Ant_Queen", "defeatedAntQueenAfterRedMemory=false" },
		new string[4] { "defeatedSeth", "Shrine Guardian Seth", "Shellwood_22", "" }
	};

	// Skull Tyrant (won't wake up again) and Widow (her fight's memory scene swaps Madeline back to Hornet) are left
	// out (user 2026-10-17)

	// every rematch happens in the world before Act 3's black thread changed the rooms
	private const string Always = "blackThreadWorld=false";

	// and with every way of getting around, so nothing on the way from the door to the boss is out of reach
	private const string Moves = "hasDash=true,hasWalljump=true,hasHarpoonDash=true,hasDoubleJump=true,hasSuperJump=true,hasBrolly=true,hasChargeSlash=true";

	// arenas whose closest door to the boss has been worked out
	private static readonly HashSet<string> nearest = new HashSet<string>();

	private static void SetPre(PlayerData pd, string list)
	{
		if (string.IsNullOrEmpty(list))
		{
			return;
		}
		foreach (string pair in list.Split(','))
		{
			string[] kv = pair.Split('=');
			FieldInfo fi = kv.Length == 2 ? AccessTools.Field(typeof(PlayerData), kv[0].Trim()) : null;
			if (fi != null && fi.FieldType == typeof(bool))
			{
				fi.SetValue(pd, kv[1].Trim() == "true");
			}
			else if (fi != null && fi.FieldType == typeof(int) && int.TryParse(kv[1].Trim(), out int n))
			{
				fi.SetValue(pd, n);
			}
		}
	}

	// which act each boss belongs to, for Records' Act filter (rematch bosses and the rest the game flags)
	private static readonly string[] Act1Flags = new string[] { "defeatedMossMother", "defeatedBellBeast", "defeatedLace1", "defeatedSongGolem", "defeatedBoneFlyerGiant", "defeatedVampireGnatBoss", "defeatedSplinterQueen", "spinnerDefeated", "skullKingDefeated", "defeatedPhantom", "defeatedCoralDrillers", "defeatedLastJudge", "defeatedDockForemen" };

	private static readonly string[] Act2Flags = new string[] { "defeatedCogworkDancers", "defeatedTrobbio", "defeatedRoachkeeperChef", "defeatedWispPyreEffigy", "DefeatedSwampShaman", "defeatedCoralDrillerSolo", "defeatedFirstWeaver", "wardBossDefeated", "defeatedZapCoreEnemy", "defeatedCrowCourt", "defeatedLaceTower", "defeatedBroodMother", "defeatedGreyWarrior", "defeatedAntTrapper", "defeatedBoneFlyerGiantGolemScene", "defeatedSongChevalierBoss" };

	private static readonly string[] Act3Flags = new string[] { "defeatedTormentedTrobbio", "defeatedFlowerQueen", "defeatedCoralKing", "defeatedCloverDancers", "defeatedWhiteCloverstag", "defeatedAntQueen", "defeatedSeth" };

	/// <summary>Bosses Records counts: every boss with an act, Skull Tyrant left out.</summary>
	public static int KnownBossCount => Act1Flags.Length - 1 + Act2Flags.Length + Act3Flags.Length;

	/// <summary>1, 2 or 3; 0 for anything not in an act list (Records doesn't show those, user 2026-10-17).</summary>
	public static int ActOf(string flag)
	{
		if (string.IsNullOrEmpty(flag))
		{
			return 0;
		}
		if (Array.IndexOf(Act1Flags, flag) >= 0)
		{
			return 1;
		}
		if (Array.IndexOf(Act2Flags, flag) >= 0)
		{
			return 2;
		}
		return (Array.IndexOf(Act3Flags, flag) >= 0) ? 3 : 0;
	}

	/// <summary>F10 (dev): every Act 1 and Act 2 boss on the list can be rematched, beaten or not.</summary>
	private static bool allThisSession;

	/// <summary>Every boss on the list can be rematched: F10 for this session, and always on a Sandbox file (user
	/// 2026-10-21: the Sandbox unlock only lasted until the game was restarted).</summary>
	public static bool All
	{
		get
		{
			return allThisSession || AssistMode.ModeId == "sandbox";
		}
		set
		{
			allThisSession = value;
		}
	}

	private static string[] Row(string flag)
	{
		foreach (string[] r in Act1)
		{
			if (r[0] == flag)
			{
				return r;
			}
		}
		return null;
	}

	public static string TitleOf(string flag)
	{
		return Row(flag)?[1];
	}

	/// <summary>F10: unlock every rematch and put those bosses into Records.</summary>
	public static void UnlockAll()
	{
		All = true;
		BossCard.Instance?.ImportRematches();
		Plugin.Log.LogInfo("Rematch: every boss on the list unlocked (F10)");
	}

	// the arena's scene and a door into it: the one learned by walking in, else one from the game's own room map
	private static string[] Arena(string flag)
	{
		Load();
		string[] r = Row(flag);
		// a door learned by walking in is best, as long as it's the room the table says (some flags show up in rooms
		// with a later fight of the same boss)
		if (r != null && r.Length > 4)
		{
			return new string[2] { r[2], r[4] };   // a set door (Widow: the side door locks, so from the top)
		}
		if (arenas.TryGetValue(flag, out string[] a) && (r == null || string.Equals(a[0], r[2], StringComparison.OrdinalIgnoreCase)))
		{
			return a;
		}
		if (r == null)
		{
			return null;
		}
		try
		{
			foreach (KeyValuePair<string, SceneTeleportMap.SceneInfo> kv in SceneTeleportMap.GetTeleportMap())
			{
				if (string.Equals(kv.Key, r[2], StringComparison.OrdinalIgnoreCase) && kv.Value != null && kv.Value.TransitionGates != null && kv.Value.TransitionGates.Count > 0)
				{
					string gate = kv.Value.TransitionGates[0];
					foreach (string g in kv.Value.TransitionGates)
					{
						if (g.StartsWith("left", StringComparison.Ordinal) || g.StartsWith("right", StringComparison.Ordinal))
						{
							gate = g;
							break;
						}
					}
					return new string[2] { kv.Key, gate };
				}
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Rematch room map: " + e.Message);
		}
		return null;
	}

	private static string ArenasFile => Path.Combine(AssistMode.DataDir ?? "", "rematch_arenas.cfg");

	private static readonly Dictionary<string, string[]> arenas = new Dictionary<string, string[]>();   // flag -> scene, gate

	private static bool loaded;

	public static BossRematch Instance;

	/// <summary>A rematch is running.</summary>
	public static bool Active { get; private set; }

	private static string activeFlag;

	private static string arenaScene;

	private static string pdSnapshot;

	private static string sdSnapshot;

	private static bool won;

	private static float wonAt;

	private static bool leftArena;

	private static bool arrived;

	private string scene;

	private string gateIn;

	private float scanAt = -1f;

	private float hornetLogAt;

	private string lastHornet;

	private void Awake()
	{
		Instance = this;
		try
		{
			Harmony h = new Harmony("silkeste.rematch");
			Difficulty.Patch(h);
			h.Patch(AccessTools.Method(typeof(HeroController), "Die"), new HarmonyMethod(typeof(BossRematch), "DiePrefix"));
			foreach (MethodInfo m in typeof(GameManager).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (m.Name == "SaveGame")
				{
					h.Patch(m, new HarmonyMethod(typeof(BossRematch), "SavePrefix"));
				}
			}
			h.Patch(AccessTools.Method(typeof(HeroController), "CanPlayNeedolin"), null, new HarmonyMethod(typeof(BossRematch), "NeedolinPostfix"));
			h.Patch(AccessTools.Method(typeof(UIManager), "UIShowReturnMenuPrompt"), new HarmonyMethod(typeof(BossRematch), "QuitPrefix"));
			h.Patch(AccessTools.Method(typeof(UIManager), "UIReturnToMainMenu"), new HarmonyMethod(typeof(BossRematch), "QuitPrefix"));
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Rematch hooks: " + e.Message);
		}
	}

	// Madeline moves herself, so Hornet's own "standing on the ground" flag stays off and the Needolin never starts:
	// let it play whenever Madeline is standing still on the ground (user 2026-10-17)
	private static float needolinLogAt;

	private static void NeedolinPostfix(HeroController __instance, ref bool __result)
	{
		if (MadelineTouches.DevOn && __instance != null && Time.unscaledTime > needolinLogAt)
		{
			needolinLogAt = Time.unscaledTime + 0.5f;
			HeroController x = __instance;
			Plugin.Log.LogInfo("CanPlayNeedolin: game says " + __result + "; silk " + x.playerData.silk + ", madeline active " + Plugin.Controller.Active + " ground " + Plugin.Controller.OnGround + " state " + Plugin.Controller.State + ", hasNeedolin " + x.HasNeedolin() + ", relinquished " + x.controlReqlinquished + ", hero_state " + x.hero_state + ", recoiling " + x.cState.recoiling);
		}
		if (__result || __instance == null)
		{
			return;
		}
		MadelineController c = Plugin.Controller;
		HeroController h = __instance;
		if (c != null && c.Active && c.OnGround && c.State != 2 && h.HasNeedolin() && !h.playerData.isInventoryOpen && !h.controlReqlinquished && h.hero_state != GlobalEnums.ActorStates.no_input && !h.cState.transitioning && !h.cState.hazardDeath && !h.cState.hazardRespawning && !h.cState.recoiling && GameManager.SilentInstance != null && !GameManager.SilentInstance.isPaused)
		{
			__result = true;
		}
	}

	// ---- giving up (pause menu's Quit turns into Give Up during a rematch) ---------------------------------------
	private static bool QuitPrefix()
	{
		if (!Active || Instance == null)
		{
			return true;
		}
		Instance.StartCoroutine(Instance.GiveUp());
		return false;
	}

	private IEnumerator GiveUp()
	{
		GameManager gm = GameManager.SilentInstance;
		if (gm != null && gm.isPaused)
		{
			yield return gm.StartCoroutine(gm.PauseGameToggleByMenu());
		}
		float t = 0f;
		while (gm != null && gm.isPaused && t < 2f)
		{
			t += Time.unscaledDeltaTime;
			yield return null;
		}
		HeroController h = HeroController.UnsafeInstance;
		if (Active && h != null && !h.cState.dead)
		{
			Plugin.Log.LogInfo("Rematch: given up");
			h.StartCoroutine((IEnumerator)AccessTools.Method(typeof(HeroController), "Die").Invoke(h, new object[2] { true, false }));
		}
	}

	// the first rematch in an arena: if another door is closer to the boss, go round to it (once) and remember it
	private IEnumerator Nearest(string flag, string gateIn)
	{
		string[] r = Row(flag);
		if (r == null || r.Length > 4 || nearest.Contains(flag))
		{
			yield break;
		}
		float waited = 0f;
		while (waited < 15f && (!string.Equals(SceneManager.GetActiveScene().name, arenaScene, StringComparison.OrdinalIgnoreCase) || HeroController.UnsafeInstance == null || HeroController.UnsafeInstance.cState.transitioning))
		{
			waited += Time.unscaledDeltaTime;
			yield return null;
		}
		yield return new WaitForSecondsRealtime(1f);
		GameManager gm = GameManager.SilentInstance;
		HeroController h = HeroController.UnsafeInstance;
		if (!Active || activeFlag != flag || gm == null || h == null || h.cState.dead || !string.Equals(SceneManager.GetActiveScene().name, arenaScene, StringComparison.OrdinalIgnoreCase))
		{
			yield break;
		}
		HealthManager boss = null;
		foreach (HealthManager hm in UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
		{
			if (hm != null && hm.hp >= 100 && hm.hp < 100000 && hm.gameObject.scene.name != null && (boss == null || hm.hp > boss.hp))
			{
				boss = hm;
			}
		}
		Vector3 b;
		string what;
		if (boss != null)
		{
			b = boss.transform.position;
			what = boss.gameObject.name;
		}
		else
		{
			// the boss lives in a scene of its own that loads later (The Unravelled): aim for the room's boss area
			GameObject area = null;
			foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				if (t != null && t.gameObject.scene == SceneManager.GetActiveScene() && (t.name == "Boss Scene" || t.name.StartsWith("Boss Scene", StringComparison.Ordinal)))
				{
					area = t.gameObject;
					break;
				}
			}
			if (area == null)
			{
				yield break;
			}
			b = area.transform.position;
			what = area.name;
		}
		string best = null;
		float bestD = float.MaxValue;
		float inD = float.MaxValue;
		foreach (TransitionPoint tp in UnityEngine.Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
		{
			if (tp == null || tp.isInactive || string.IsNullOrEmpty(tp.targetScene))
			{
				continue;
			}
			float d = Vector2.Distance(tp.transform.position, b);
			if (tp.gameObject.name == gateIn)
			{
				inD = d;
			}
			if (d < bestD)
			{
				bestD = d;
				best = tp.gameObject.name;
			}
		}
		nearest.Add(flag);
		arenas[flag] = new string[2] { arenaScene, best ?? gateIn };
		Save();
		Plugin.Log.LogInfo("Rematch: closest door to " + what + " in " + arenaScene + " is " + best + " (" + bestD.ToString("0") + " away, came in by " + gateIn + " " + inD.ToString("0") + ")");
		if (best != null && best != gateIn && bestD + 8f < inD)
		{
			gm.BeginSceneTransition(new GameManager.SceneLoadInfo
			{
				SceneName = arenaScene,
				EntryGateName = best,
				EntryDelay = 0f,
				Visualization = GameManager.SceneLoadVisualizations.Default,
				WaitForSceneTransitionCameraFade = true,
				AlwaysUnloadUnusedAssets = true
			});
		}
	}

	// what's in the arena a few seconds in: tells which bosses didn't show up and why
	private IEnumerator ReportNow(string flag)
	{
		yield return null;
		yield return Report(flag, 0f);
	}

	private IEnumerator Report(string flag, float delay = 7f)
	{
		yield return new WaitForSecondsRealtime(delay);
		if (!Active || activeFlag != flag)
		{
			yield break;
		}
		try
		{
			List<string> big = new List<string>();
			foreach (HealthManager hm in UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				if (hm != null && hm.hp >= 100)
				{
					big.Add(hm.gameObject.name + "@" + (Vector2)hm.transform.position + "(" + hm.hp + (hm.gameObject.activeInHierarchy ? "" : ",off") + ")");
				}
			}
			HeroController hh = HeroController.UnsafeInstance;
			List<string> doors = new List<string>();
			foreach (TransitionPoint tp in UnityEngine.Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
			{
				if (tp != null)
				{
					doors.Add(tp.gameObject.name + "@" + (Vector2)tp.transform.position + (tp.isInactive ? "(off)" : ""));
				}
			}
			Plugin.Log.LogInfo("Rematch report doors: " + string.Join(" ", doors.ToArray()) + " hero@" + (hh != null ? ((Vector2)hh.transform.position).ToString() : "?"));
			Plugin.Log.LogInfo("Rematch report " + flag + ": scene " + SceneManager.GetActiveScene().name + ", big enemies: " + string.Join(" ", big.ToArray()));
			Activators();
			SaveChecks();
			foreach (PlayMakerFSM fsm in UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				if (fsm == null || fsm.Fsm == null || (!Mentions(fsm) && fsm.gameObject.name.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) < 0 && fsm.gameObject.name.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) < 0 && fsm.gameObject.name.IndexOf("Gate", StringComparison.OrdinalIgnoreCase) < 0 && fsm.gameObject.name.IndexOf("Door", StringComparison.OrdinalIgnoreCase) < 0))
				{
					continue;
				}
				if (!Mentions(fsm) && (fsm.ActiveStateName == "Opened" || fsm.ActiveStateName == "Idle" || !fsm.gameObject.activeInHierarchy))
				{
					continue;
				}
				Plugin.Log.LogInfo("  fsm " + fsm.gameObject.name + "/" + fsm.FsmName + " state=" + fsm.ActiveStateName + (fsm.gameObject.activeInHierarchy ? "" : " (off)"));
				Dump(fsm);
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Rematch report: " + e.Message);
		}
	}

	// the boss script's current step: what it does and where it can go next, to see what it's waiting for
	private static void Dump(PlayMakerFSM fsm)
	{
		foreach (HutongGames.PlayMaker.FsmState st in fsm.FsmStates)
		{
			if (st.Name != fsm.ActiveStateName)
			{
				continue;
			}
			foreach (HutongGames.PlayMaker.FsmStateAction act in st.Actions)
			{
				if (act == null)
				{
					continue;
				}
				List<string> parts = new List<string>();
				foreach (FieldInfo fi in act.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
				{
					object v = fi.GetValue(act);
					if (v is HutongGames.PlayMaker.NamedVariable nv)
					{
						parts.Add(fi.Name + "=" + (string.IsNullOrEmpty(nv.Name) ? "" : nv.Name + ":") + nv.ToString());
					}
					else if (v is HutongGames.PlayMaker.FsmEvent ev)
					{
						parts.Add(fi.Name + "->" + ev.Name);
					}
					else if (v is HutongGames.PlayMaker.FsmOwnerDefault od)
					{
						parts.Add(fi.Name + "=" + (od.GameObject?.Value != null ? od.GameObject.Value.name : "owner"));
					}
				}
				Plugin.Log.LogInfo("    " + act.GetType().Name + (act.Enabled ? "" : " (off)") + " " + string.Join(" ", parts.ToArray()));
			}
			foreach (HutongGames.PlayMaker.FsmTransition tr in st.Transitions)
			{
				Plugin.Log.LogInfo("    on " + tr.EventName + " -> " + tr.ToState);
			}
		}
	}

	// a boss that's switched off: which scripts switch it (or a parent) on, and where those scripts are now
	private static void Activators()
	{
		HashSet<GameObject> off = new HashSet<GameObject>();
		foreach (HealthManager hm in UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
		{
			if (hm != null && hm.hp >= 300 && hm.hp < 100000 && !hm.gameObject.activeInHierarchy)
			{
				for (Transform t = hm.transform; t != null; t = t.parent)
				{
					off.Add(t.gameObject);
				}
			}
		}
		if (off.Count == 0)
		{
			return;
		}
		foreach (PlayMakerFSM fsm in UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
		{
			if (fsm == null || fsm.Fsm == null)
			{
				continue;
			}
			foreach (HutongGames.PlayMaker.FsmState st in fsm.FsmStates)
			{
				foreach (HutongGames.PlayMaker.FsmStateAction act in st.Actions)
				{
					if (act == null)
					{
						continue;
					}
					foreach (FieldInfo fi in act.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
					{
						object v = fi.GetValue(act);
						GameObject g = (v as HutongGames.PlayMaker.FsmOwnerDefault)?.GameObject?.Value ?? (v as HutongGames.PlayMaker.FsmGameObject)?.Value;
						if (g != null && off.Contains(g))
						{
							Plugin.Log.LogInfo("  activator: " + fsm.gameObject.name + "/" + fsm.FsmName + " step '" + st.Name + "' " + act.GetType().Name + " -> " + g.name + "; script now at '" + fsm.ActiveStateName + "'" + (fsm.gameObject.activeInHierarchy ? "" : " (off)"));
							Dump(fsm);
						}
					}
				}
			}
		}
	}

	// every non-script component in the room that checks a save value, with what it checks and the value now
	private static void SaveChecks()
	{
		HashSet<string> seen = new HashSet<string>();
		foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
		{
			if (mb == null || mb is PlayMakerFSM || mb.gameObject.scene.name != SceneManager.GetActiveScene().name)
			{
				continue;
			}
			List<string> found = new List<string>();
			Collect(mb, 0, found);
			foreach (string f in found)
			{
				string line = mb.GetType().Name + " on " + mb.gameObject.name + (mb.gameObject.activeInHierarchy ? "" : " (off)") + ": " + f;
				if (seen.Add(line))
				{
					Plugin.Log.LogInfo("  check " + line);
				}
			}
		}
	}

	private static void Collect(object o, int depth, List<string> found)
	{
		if (o == null || depth > 3)
		{
			return;
		}
		foreach (FieldInfo fi in o.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
		{
			Type t = fi.FieldType;
			object v;
			try
			{
				v = fi.GetValue(o);
			}
			catch (Exception)
			{
				continue;
			}
			if (v == null)
			{
				continue;
			}
			if (v is string str)
			{
				FieldInfo pf = str.Length > 3 ? AccessTools.Field(typeof(PlayerData), str) : null;
				if (pf != null)
				{
					found.Add(fi.Name + "=" + str + " (now " + pf.GetValue(PlayerData.instance) + ")");
				}
			}
			else if (v is UnityEngine.Object)
			{
			}
			else if (v is System.Collections.IEnumerable en && !(v is string))
			{
				int n = 0;
				foreach (object e in en)
				{
					if (++n > 20)
					{
						break;
					}
					if (e != null && !(e is UnityEngine.Object) && !e.GetType().IsPrimitive)
					{
						Collect(e, depth + 1, found);
					}
				}
			}
			else if (!t.IsPrimitive && !t.IsEnum && (t.IsValueType || t.IsClass) && t.Namespace != "UnityEngine" && t.Namespace != "System")
			{
				Collect(v, depth + 1, found);
			}
		}
	}

	private static bool Mentions(PlayMakerFSM fsm)
	{
		string[] r = Row(activeFlag);
		foreach (HutongGames.PlayMaker.FsmState st in fsm.FsmStates)
		{
			foreach (HutongGames.PlayMaker.FsmStateAction act in st.Actions)
			{
				if (act == null)
				{
					continue;
				}
				foreach (FieldInfo fi in act.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
				{
					if (fi.FieldType != typeof(HutongGames.PlayMaker.FsmString))
					{
						continue;
					}
					string v = (fi.GetValue(act) as HutongGames.PlayMaker.FsmString)?.Value;
					if (v == activeFlag || (r != null && v != null && v.Length > 4 && r[3].Contains(v + "=")))
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	// some fights are started by something the first visit does (Lugoli: the kitchen gong): start its battle directly
	private static bool StartBattleFor(string flag)
	{
		foreach (BattleScene bs in UnityEngine.Object.FindObjectsByType<BattleScene>(FindObjectsSortMode.None))
		{
			if (bs != null && bs.setPDBoolOnEnd == flag && !(bool)AccessTools.Field(typeof(BattleScene), "completed").GetValue(bs) && (int)AccessTools.Field(typeof(BattleScene), "currentWave").GetValue(bs) == 0)
			{
				Plugin.Log.LogInfo("Rematch: starting " + bs.gameObject.name + " for " + flag);
				bs.StartBattle();
				return true;
			}
		}
		return false;
	}

	// Lugoli: on the first visit the kitchen gong brings her down; in a rematch the fight starts a moment after you arrive
	private IEnumerator AutoStart(string flag)
	{
		float waited = 0f;
		while (waited < 15f && (!string.Equals(SceneManager.GetActiveScene().name, arenaScene, StringComparison.OrdinalIgnoreCase) || HeroController.UnsafeInstance == null || HeroController.UnsafeInstance.cState.transitioning))
		{
			waited += Time.unscaledDeltaTime;
			yield return null;
		}
		yield return new WaitForSecondsRealtime(1.5f);
		if (!Active || activeFlag != flag)
		{
			yield break;
		}
		StartBattleFor(flag);
		yield return new WaitForSecondsRealtime(1f);
		foreach (HealthManager hm in UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
		{
			if (hm == null || hm.hp < 100 || hm.hp >= 100000)
			{
				continue;
			}
			foreach (PlayMakerFSM fsm in hm.GetComponents<PlayMakerFSM>())
			{
				if (fsm != null && fsm.ActiveStateName == "Dormant")
				{
					fsm.SendEvent("BATTLE START");
					Plugin.Log.LogInfo("Rematch: woke " + hm.gameObject.name);
				}
			}
		}
	}

	// fights that carry on in another scene (First Sinner's is a memory reached from her cage)
	private static bool FightGoesOn(string flag, string sceneName)
	{
		string memory = flag == "defeatedFirstWeaver" ? "Memory_First_Sinner" : flag == "defeatedFlowerQueen" ? "Shellwood_11b_Memory" : flag == "defeatedCoralKing" ? "Memory_Coral_Tower" : flag == "defeatedAntQueen" ? "Memory_Ant_Queen" : null;
		return memory != null && string.Equals(sceneName, memory, StringComparison.OrdinalIgnoreCase);
	}

	// ---- what Records asks -------------------------------------------------------------------------------------
	public static bool Allows(string flag)
	{
		return flag != null && Row(flag) != null;
	}

	/// <summary>The arena is known (you've walked through it since this update).</summary>
	public static bool Ready(string flag)
	{
		Load();
		return Allows(flag) && (All ? Arena(flag) != null : arenas.ContainsKey(flag));
	}

	// ---- "Rematch last boss" from the pause menu (user 2026-10-18) ----------------------------------------------
	private static string lastFlag;

	private static string LastFile => Path.Combine(AssistMode.DataDir ?? "", "rematch_last.cfg");

	/// <summary>The boss of the last rematch started, if it can be fought again right now.</summary>
	public static string LastFlag
	{
		get
		{
			if (lastFlag == null)
			{
				try
				{
					lastFlag = File.Exists(LastFile) ? File.ReadAllText(LastFile).Trim() : "";
				}
				catch (Exception)
				{
					lastFlag = "";
				}
			}
			return (lastFlag.Length > 0 && Ready(lastFlag)) ? lastFlag : null;
		}
	}

	/// <summary>Pause menu: unpause, then straight into the last boss's rematch.</summary>
	public static void RematchLast()
	{
		string f = LastFlag;
		if (Instance == null || Active || f == null)
		{
			return;
		}
		Instance.StartCoroutine(Instance.FromPause(f));
	}

	private IEnumerator FromPause(string f)
	{
		GameManager gm = GameManager.SilentInstance;
		if (gm != null && gm.isPaused)
		{
			yield return gm.StartCoroutine(gm.PauseGameToggleByMenu());
		}
		float t = 0f;
		while (gm != null && gm.isPaused && t < 2f)
		{
			t += Time.unscaledDeltaTime;
			yield return null;
		}
		Begin(f);
	}

	/// <summary>Records' Rematch: closes Records and, a moment later, goes into the arena.</summary>
	public static void Begin(string flag)
	{
		if (Instance == null || Active || !Ready(flag))
		{
			return;
		}
		BossCard.CloseRequested = true;
		Instance.StartCoroutine(Instance.Go(flag));
	}

	private IEnumerator Go(string flag)
	{
		float t = 0f;
		while (BossCard.RecordsOpen && t < 2f)
		{
			t += Time.unscaledDeltaTime;
			yield return null;
		}
		yield return new WaitForSecondsRealtime(0.25f);
		GameManager gm = GameManager.SilentInstance;
		HeroController h = HeroController.UnsafeInstance;
		PlayerData pd = PlayerData.instance;
		if (gm == null || h == null || pd == null || gm.IsMenuScene())
		{
			yield break;
		}
		FieldInfo f = AccessTools.Field(typeof(PlayerData), flag);
		if (f == null)
		{
			yield break;
		}
		BackupSave(gm.profileID);
		pdSnapshot = JsonUtility.ToJson(pd);
		try
		{
			sdSnapshot = SceneData.instance != null ? JsonUtility.ToJson(SceneData.instance) : null;
		}
		catch (Exception)
		{
			sdSnapshot = null;
		}
		string[] where = Arena(flag);
		if (where == null)
		{
			pdSnapshot = null;
			sdSnapshot = null;
			yield break;
		}
		f.SetValue(pd, false);
		if (ActOf(flag) < 3)
		{
			SetPre(pd, Always);   // Act 3 bosses keep the black-thread world they belong to
		}
		if (All)
		{
			SetPre(pd, Moves);   // test builds only (F10): players rematch with their own setup (user 2026-10-17)
		}
		SetPre(pd, Row(flag)?[3]);
		activeFlag = flag;
		arenaScene = where[0];
		lastFlag = flag;
		try
		{
			File.WriteAllText(LastFile, flag);
		}
		catch (Exception)
		{
		}
		// the arena also remembers its own fight ("battle done", locked gates): forget that room for this visit
		int forgot = ForgetRoom(SceneData.instance?.PersistentBools, arenaScene) + ForgetRoom(SceneData.instance?.PersistentInts, arenaScene);
		Plugin.Log.LogInfo("Rematch: cleared " + forgot + " saved things in " + arenaScene);
		won = false;
		leftArena = false;
		arrived = false;
		Active = true;
		try
		{
			AccessTools.Method(typeof(HeroController), "MaxHealth")?.Invoke(h, null);
		}
		catch (Exception)
		{
		}
		if (MadelineTouches.DevOn)
		{
			StartCoroutine(Report(flag));   // the arena report only on the dev install: it's slow and log-heavy
		}
		StartCoroutine(Nearest(flag, where[1]));
		if (Row(flag).Length > 5 && Row(flag)[5] == "start")
		{
			StartCoroutine(AutoStart(flag));
		}
		Plugin.Log.LogInfo("Rematch: " + flag + " in " + arenaScene + " (gate " + where[1] + ")");
		gm.BeginSceneTransition(new GameManager.SceneLoadInfo
		{
			SceneName = arenaScene,
			EntryGateName = where[1],
			EntryDelay = 0f,
			Visualization = GameManager.SceneLoadVisualizations.Default,
			WaitForSceneTransitionCameraFade = true,
			AlwaysUnloadUnusedAssets = true
		});
	}

	// ---- during the rematch ------------------------------------------------------------------------------------
	// ---- dev commands (only with silkeste_dev.txt): a line in dev_cmd.txt, so rematches can be tried without keys
	private float cmdAt;

	private void DevCommands()
	{
		if (Time.unscaledTime < cmdAt || !MadelineTouches.DevOn)
		{
			return;
		}
		cmdAt = Time.unscaledTime + 1f;
		string path = Path.Combine(AssistMode.DataDir ?? "", "dev_cmd.txt");
		if (!File.Exists(path))
		{
			return;
		}
		string cmd;
		try
		{
			cmd = File.ReadAllText(path).Trim();
			File.Delete(path);
		}
		catch (Exception)
		{
			return;
		}
		Plugin.Log.LogInfo("Dev command: " + cmd);
		string[] p = cmd.Split(' ');
		try
		{
			if (p[0] == "techroom")
			{
				TechRoom.Request();   // into (or out of) the Tech Tutorial
			}
			else if (p[0] == "techask")
			{
				TechRoom.OfferAfterIntro();   // the "do you know Celeste's tech?" prompt
			}
			else if (p[0] == "emotemenu")
			{
				EmoteMenu.Open();   // a look at the Emotes menu
			}
			else if (p[0] == "startmenu")
			{
				StartingAbilities.OpenMenu(null);   // a look at the starting abilities prompt; Begin just closes it
			}
			else if (p[0] == "load" && p.Length > 1 && UIManager.instance != null)
			{
				CelesteFileSelect.Continue(int.Parse(p[1]));
				UIManager.instance.UIGoToProfileMenu();
			}
			else if (p[0] == "rematch" && p.Length > 1)
			{
				All = true;
				Begin(p[1]);
			}
			else if (p[0] == "journal")
			{
				foreach (EnemyJournalRecord r in EnemyJournalManager.GetAllEnemies())
				{
					if (r != null)
					{
						Plugin.Log.LogInfo("  journal " + r.name + " | " + r.DisplayName + " | sprite " + (r.EnemySprite != null) + " icon " + (r.IconSprite != null));
					}
				}
			}
			else if (p[0] == "gates" && p.Length > 1)
			{
				foreach (KeyValuePair<string, SceneTeleportMap.SceneInfo> kv in SceneTeleportMap.GetTeleportMap())
				{
					if (string.Equals(kv.Key, p[1], StringComparison.OrdinalIgnoreCase))
					{
						Plugin.Log.LogInfo("  gates " + kv.Key + ": " + string.Join(" ", kv.Value.TransitionGates.ToArray()));
					}
				}
			}
			else if (p[0] == "event" && p.Length > 2)
			{
				foreach (PlayMakerFSM fsm in UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
				{
					if (fsm != null && (fsm.gameObject.name + "/" + fsm.FsmName).Replace(" ", "_") == p[1])
					{
						fsm.SendEvent(p[2]);
						Plugin.Log.LogInfo("  sent " + p[2] + " to " + p[1]);
					}
				}
			}
			else if (p[0] == "kill")
			{
				foreach (HealthManager hm in UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
				{
					if (hm != null && hm.hp >= 100 && hm.hp < 100000)
					{
						Plugin.Log.LogInfo("  killing " + hm.gameObject.name);
						hm.ApplyExtraDamage(hm.hp);
					}
				}
			}
			else if (p[0] == "shot")
			{
				ScreenCapture.CaptureScreenshot(Path.Combine(AssistMode.DataDir ?? "", "dev_shot.png"));
			}
			else if (p[0] == "tpboss")
			{
				HealthManager boss = null;
				foreach (HealthManager hm in UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
				{
					if (hm != null && hm.hp >= 100 && hm.hp < 100000 && (boss == null || hm.hp > boss.hp))
					{
						boss = hm;
					}
				}
				HeroController h = HeroController.UnsafeInstance;
				if (boss != null && h != null)
				{
					float dx = p.Length > 1 ? float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture) : -6f;
					float dy = p.Length > 2 ? float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture) : 0f;
					h.transform.position = boss.transform.position + new Vector3(dx, dy, 0f);
					Plugin.Log.LogInfo("  hero moved next to " + boss.gameObject.name);
				}
			}
			else if (p[0] == "fsm" && p.Length > 1)
			{
				string want = cmd.Substring(4).Trim();
				foreach (PlayMakerFSM fsm in UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
				{
					if (fsm != null && fsm.gameObject.name.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						Plugin.Log.LogInfo("  fsm " + fsm.gameObject.name + "/" + fsm.FsmName + " state=" + fsm.ActiveStateName + (fsm.gameObject.activeInHierarchy ? "" : " (off)"));
						Dump(fsm);
					}
				}
			}
			else if (p[0] == "who" && p.Length > 1)
			{
				// which scripts send an event, and from which step (to find what starts a fight)
				string ev = cmd.Substring(4).Trim();
				foreach (PlayMakerFSM fsm in UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
				{
					if (fsm == null || fsm.Fsm == null)
					{
						continue;
					}
					foreach (HutongGames.PlayMaker.FsmState st in fsm.FsmStates)
					{
						foreach (HutongGames.PlayMaker.FsmStateAction act in st.Actions)
						{
							if (act == null)
							{
								continue;
							}
							foreach (FieldInfo fi in act.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
							{
								object v = fi.GetValue(act);
								string name = (v as HutongGames.PlayMaker.FsmEvent)?.Name ?? (v as HutongGames.PlayMaker.FsmString)?.Value ?? (v as HutongGames.PlayMaker.FsmEventTarget)?.ToString();
								if (name == ev)
								{
									Plugin.Log.LogInfo("  " + ev + " from " + fsm.gameObject.name + "/" + fsm.FsmName + " step " + st.Name + " (" + act.GetType().Name + ") now=" + fsm.ActiveStateName + (fsm.gameObject.activeInHierarchy ? "" : " (off)"));
								}
							}
						}
					}
				}
			}
			else if (p[0] == "battle")
			{
				foreach (BattleScene bs in UnityEngine.Object.FindObjectsByType<BattleScene>(FindObjectsInactive.Include, FindObjectsSortMode.None))
				{
					Collider2D c = bs.GetComponent<Collider2D>();
					Plugin.Log.LogInfo("  battle " + bs.gameObject.name + (bs.gameObject.activeInHierarchy ? "" : " (off)") + " completed=" + AccessTools.Field(typeof(BattleScene), "completed").GetValue(bs) + " wave=" + AccessTools.Field(typeof(BattleScene), "currentWave").GetValue(bs) + " trigger=" + (c != null ? c.bounds.ToString() + (c.enabled ? "" : " (off)") : "none") + " pdOnEnd=" + bs.setPDBoolOnEnd);
				}
			}
			else if (p[0] == "startbattle")
			{
				StartBattleFor(activeFlag);
			}
			else if (p[0] == "tpto" && p.Length > 2)
			{
				HeroController h = HeroController.UnsafeInstance;
				if (h != null)
				{
					h.transform.position = new Vector3(float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture), h.transform.position.z);
				}
			}
			else if (p[0] == "report" && Active)
			{
				StartCoroutine(ReportNow(activeFlag));
			}
			else if (p[0] == "giveup" && Active)
			{
				StartCoroutine(GiveUp());
			}
			else if (p[0] == "hero")
			{
				HeroController h = HeroController.UnsafeInstance;
				Plugin.Log.LogInfo("  hero " + (h != null ? h.transform.position.ToString() + " dead " + h.cState.dead + " relinquished " + h.controlReqlinquished : "none") + " scene " + SceneManager.GetActiveScene().name);
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Dev command: " + e.Message);
		}
	}

	private bool needolinDumped;

	// dev: write out the hero's Needolin script (and the sub-script it runs) once, then follow which steps it takes
	private PlayMakerFSM specials;

	private string specialsWas;

	private void DumpNeedolin()
	{
		HeroController h = HeroController.UnsafeInstance;
		if (!MadelineTouches.DevOn || h == null || GameManager.SilentInstance == null || GameManager.SilentInstance.IsMenuScene())
		{
			return;
		}
		if (specials == null)
		{
			foreach (PlayMakerFSM fsm in h.GetComponents<PlayMakerFSM>())
			{
				if (fsm.FsmName == "Silk Specials")
				{
					specials = fsm;
				}
			}
		}
		if (specials == null)
		{
			return;
		}
		if (!needolinDumped)
		{
			needolinDumped = true;
			if (h.superJumpFSM != null)
			{
				Plugin.Log.LogInfo("Silk Soar script: " + h.superJumpFSM.FsmName);
				DumpStates(h.superJumpFSM.FsmStates);
			}
			foreach (HutongGames.PlayMaker.FsmState st in specials.FsmStates)
			{
				if (st.Name != "Needolin Sub")
				{
					continue;
				}
				foreach (HutongGames.PlayMaker.FsmStateAction act in st.Actions)
				{
					HutongGames.PlayMaker.Fsm sub = act is HutongGames.PlayMaker.Actions.RunFSMAction ? RunFsmField.GetValue(act) as HutongGames.PlayMaker.Fsm : null;
					if (sub != null)
					{
						Plugin.Log.LogInfo("Needolin sub-script: " + sub.Name);
						DumpStates(sub.States);
					}
				}
			}
		}
		string now = specials.ActiveStateName + SubState(specials);
		if (now != specialsWas)
		{
			specialsWas = now;
			Plugin.Log.LogInfo("Silk Specials: " + now + " (madeline ground " + Plugin.Controller.OnGround + ", cState ground " + h.cState.onGround + ", input blocked " + h.IsInputBlocked() + ")");
		}
	}

	private static readonly FieldInfo RunFsmField = AccessTools.Field(typeof(HutongGames.PlayMaker.Actions.RunFSMAction), "runFsm");

	private static string SubState(PlayMakerFSM fsm)
	{
		foreach (HutongGames.PlayMaker.FsmState st in fsm.FsmStates)
		{
			if (st.Name != fsm.ActiveStateName)
			{
				continue;
			}
			foreach (HutongGames.PlayMaker.FsmStateAction act in st.Actions)
			{
				HutongGames.PlayMaker.Fsm sub = act is HutongGames.PlayMaker.Actions.RunFSMAction ? RunFsmField.GetValue(act) as HutongGames.PlayMaker.Fsm : null;
				if (sub != null)
				{
					return " > " + sub.ActiveStateName;
				}
			}
		}
		return "";
	}

	private static void DumpStates(HutongGames.PlayMaker.FsmState[] states)
	{
		foreach (HutongGames.PlayMaker.FsmState st in states)
		{
			Plugin.Log.LogInfo("  [" + st.Name + "]");
			foreach (HutongGames.PlayMaker.FsmStateAction act in st.Actions)
			{
				if (act == null)
				{
					continue;
				}
				List<string> parts = new List<string>();
				foreach (FieldInfo fi in act.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
				{
					object v = fi.GetValue(act);
					if (v is HutongGames.PlayMaker.NamedVariable nv)
					{
						parts.Add(fi.Name + "=" + (string.IsNullOrEmpty(nv.Name) ? "" : nv.Name + ":") + nv.ToString());
					}
					else if (v is HutongGames.PlayMaker.FsmEvent ev)
					{
						parts.Add(fi.Name + "->" + ev.Name);
					}
					else if (v is string sv)
					{
						parts.Add(fi.Name + "=" + sv);
					}
				}
				Plugin.Log.LogInfo("    " + act.GetType().Name + " " + string.Join(" ", parts.ToArray()));
			}
			foreach (HutongGames.PlayMaker.FsmTransition tr in st.Transitions)
			{
				Plugin.Log.LogInfo("    on " + tr.EventName + " -> " + tr.ToState);
			}
		}
	}

	// Hornet's scripts (Needolin, taunt) ask the game whether she's on the ground; Madeline moves herself, so tell
	// the game what Madeline is doing (user 2026-10-17)
	private static void SyncGround()
	{
		HeroController h = HeroController.UnsafeInstance;
		MadelineController c = Plugin.Controller;
		if (h != null && c != null && c.Active)
		{
			h.cState.onGround = c.OnGround && c.State != 2;
		}
	}

	private void LateUpdate()
	{
		SyncGround();
	}

	private void Update()
	{
		SyncGround();
		GameManager gp = GameManager.SilentInstance;
		if (gp != null && gp.isPaused && !Active && !AssistMode.IsOpen && CelesteMenus.pauseRematchRect.width > 0f)
		{
			UnityEngine.InputSystem.Keyboard ik = UnityEngine.InputSystem.Keyboard.current;
			if (MenuMouse.Clicked(CelesteMenus.pauseRematchRect) || (ik != null && ik.rKey.wasPressedThisFrame))
			{
				CelesteMenus.pauseRematchRect = default(Rect);
				Sfx.Play(Sheets.Sounds.MenuButtonSelect.Id);
				RematchLast();
			}
		}
		if (MadelineTouches.DevOn)
		{
			// dev: why Silk Soar does or doesn't start when Clawline is pressed
			HeroController hh = HeroController.UnsafeInstance;
			HeroActions acts = ManagerSingleton<InputHandler>.Instance?.inputActions;
			if (hh != null && acts != null && acts.SuperDash.WasPressed)
			{
				Plugin.Log.LogInfo("Clawline pressed: stick " + acts.MoveVector.Vector + ", down " + acts.Down.IsPressed + ", only down " + AccessTools.Method(typeof(HeroController), "IsPressingOnlyDown")?.Invoke(hh, null) + ", can soar " + hh.CanSuperJump() + ", ground " + hh.cState.onGround + ", hero_state " + hh.hero_state + ", device " + InControl.InputManager.ActiveDevice?.Name);
			}
		}
		DevCommands();
		DumpNeedolin();
		GameManager gm = GameManager.SilentInstance;
		HeroController h = HeroController.UnsafeInstance;
		string now = SceneManager.GetActiveScene().name;
		if (now != scene)
		{
			// a new room: remember the door, and look for a boss's defeated check in it a moment later
			if (Active)
			{
				Plugin.Log.LogInfo("Rematch: now in " + now + " (from " + scene + ")");
			}
			scene = now;
			gateIn = gm != null ? gm.entryGateName : null;
			scanAt = Time.unscaledTime + 1.5f;
		}
		if (scanAt > 0f && Time.unscaledTime > scanAt)
		{
			scanAt = -1f;
			if (!Active)
			{
				Scan(now, gateIn);
			}
		}
		if (!Active)
		{
			return;
		}
		if (MadelineTouches.DevOn && h != null && Time.unscaledTime > hornetLogAt)
		{
			// First Sinner diagnostics: what shows Hornet in her memory fight (logged when it changes)
			hornetLogAt = Time.unscaledTime + 1f;
			string c = Plugin.HornetCheck(h);
			if (c != lastHornet)
			{
				lastHornet = c;
				Plugin.Log.LogInfo("Rematch look (" + now + "): " + c);
			}
		}
		if (gm == null || gm.IsMenuScene() || PlayerData.instance == null)
		{
			Finish("menu");
			return;
		}
		FieldInfo f = AccessTools.Field(typeof(PlayerData), activeFlag);
		if (!won && f != null && (bool)f.GetValue(PlayerData.instance))
		{
			won = true;
			wonAt = Time.unscaledTime;
		}
		if (won && Time.unscaledTime - wonAt > 6f && h != null && !h.cState.dead)
		{
			// the boss card has had its moment: everything back, and home to the bench
			Finish("won");
			try
			{
				h.StartCoroutine((IEnumerator)AccessTools.Method(typeof(HeroController), "Die").Invoke(h, new object[2] { true, false }));
			}
			catch (Exception)
			{
			}
			return;
		}
		// walked out: only once settled in a room that isn't the fight's (a reload of the arena passes through
		// loading scenes, which isn't leaving)
		bool inFight = string.Equals(now, arenaScene, StringComparison.OrdinalIgnoreCase) || FightGoesOn(activeFlag, now);
		if (inFight)
		{
			arrived = true;
		}
		else if (arrived && h != null && !h.cState.transitioning && !h.cState.dead && gm.GameState == GlobalEnums.GameState.PLAYING)
		{
			leftArena = true;
		}
		if (leftArena && h != null && !h.cState.dead && !h.cState.transitioning)
		{
			Finish(won ? "won" : "left");
		}
	}

	// dying in a rematch is a non-lethal death: back to the bench, no cocoon, nothing lost
	private static void DiePrefix(ref bool nonLethal)
	{
		if (Active)
		{
			Plugin.Log.LogInfo("Rematch: Die called (nonLethal was " + nonLethal + ")");
			nonLethal = true;
			leftArena = true;
		}
	}

	// nothing from a rematch may ever be saved: put everything back first
	private static void SavePrefix()
	{
		if (Active)
		{
			Finish("save");
		}
	}

	private static void Finish(string why)
	{
		if (!Active)
		{
			return;
		}
		Plugin.Log.LogInfo("Rematch finishing (" + why + ") in " + SceneManager.GetActiveScene().name);
		Active = false;
		if (why == "won")
		{
			Achievements.Done("encore");
			Achievements.Add("regular");
		}
		try
		{
			if (pdSnapshot != null && PlayerData.instance != null)
			{
				JsonUtility.FromJsonOverwrite(pdSnapshot, PlayerData.instance);
			}
			if (sdSnapshot != null && SceneData.instance != null)
			{
				// empty the rooms first, so anything the rematch added is gone too, then read the snapshot back
				ClearRooms(SceneData.instance.PersistentBools);
				ClearRooms(SceneData.instance.PersistentInts);
				JsonUtility.FromJsonOverwrite(sdSnapshot, SceneData.instance);
			}
			Plugin.Log.LogInfo("Rematch over (" + why + "): save values put back");
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Rematch restore: " + e.Message + " (your save backup is in " + BackupDir + ")");
		}
		pdSnapshot = null;
		sdSnapshot = null;
		activeFlag = null;
	}

	private static IDictionary Rooms(object collection)
	{
		return collection == null ? null : AccessTools.Field(collection.GetType(), "scenes")?.GetValue(collection) as IDictionary;
	}

	private static int ForgetRoom(object collection, string sceneName)
	{
		IDictionary rooms = Rooms(collection);
		if (rooms == null || !rooms.Contains(sceneName))
		{
			return 0;
		}
		int n = (rooms[sceneName] as ICollection)?.Count ?? 0;
		rooms.Remove(sceneName);
		return n;
	}

	private static void ClearRooms(object collection)
	{
		Rooms(collection)?.Clear();
	}

	// ---- learning the arenas -----------------------------------------------------------------------------------
	private static void Scan(string sceneName, string gate)
	{
		if (string.IsNullOrEmpty(sceneName) || string.IsNullOrEmpty(gate))
		{
			return;
		}
		Load();
		try
		{
			foreach (PlayMakerFSM fsm in UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
			{
				if (fsm == null || fsm.Fsm == null)
				{
					continue;
				}
				foreach (HutongGames.PlayMaker.FsmState st in fsm.FsmStates)
				{
					foreach (HutongGames.PlayMaker.FsmStateAction act in st.Actions)
					{
						if (act == null)
						{
							continue;
						}
						foreach (FieldInfo fi in act.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
						{
							if (fi.FieldType != typeof(HutongGames.PlayMaker.FsmString))
							{
								continue;
							}
							string v = (fi.GetValue(act) as HutongGames.PlayMaker.FsmString)?.Value;
							if (Allows(v) && !arenas.ContainsKey(v) && string.Equals(sceneName, Row(v)[2], StringComparison.OrdinalIgnoreCase))
							{
								arenas[v] = new string[2] { sceneName, gate };
								Save();
								Plugin.Log.LogInfo("Rematch: learned " + v + " -> " + sceneName + " via " + gate);
							}
						}
					}
				}
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Rematch scan: " + e.Message);
		}
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
			if (File.Exists(ArenasFile))
			{
				foreach (string l in File.ReadAllLines(ArenasFile))
				{
					string[] p = l.Split('|');
					if (p.Length >= 3 && Row(p[0]) != null && string.Equals(p[1], Row(p[0])[2], StringComparison.OrdinalIgnoreCase))
					{
						arenas[p[0]] = new string[2] { p[1], p[2] };
						if (p.Length > 3 && p[3] == "near")
						{
							nearest.Add(p[0]);
						}
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void Save()
	{
		try
		{
			List<string> lines = new List<string>();
			foreach (KeyValuePair<string, string[]> kv in arenas)
			{
				lines.Add(kv.Key + "|" + kv.Value[0] + "|" + kv.Value[1] + (nearest.Contains(kv.Key) ? "|near" : ""));
			}
			File.WriteAllLines(ArenasFile, lines.ToArray());
		}
		catch (Exception)
		{
		}
	}

	// ---- the save backup ---------------------------------------------------------------------------------------
	private static string BackupDir => Path.Combine(AssistMode.DataDir ?? "", "rematch_backup");

	private static void BackupSave(int slot)
	{
		try
		{
			Directory.CreateDirectory(BackupDir);
			foreach (string f in Directory.GetFiles(Application.persistentDataPath, "user" + slot + "*.dat", SearchOption.AllDirectories))
			{
				File.Copy(f, Path.Combine(BackupDir, Path.GetFileName(f)), overwrite: true);
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Rematch backup: " + e.Message);
		}
	}
}
