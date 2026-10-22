using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using GlobalEnums;
using UnityEngine;
using UnityEngine.SceneManagement;
using MadelineInPharloom.Sheets;

namespace MadelineInPharloom;

/// <summary>
/// Small touches that make Pharloom Madeline's (user 2026-10-17):
/// - everyone calls her Madeline: "Hornet" in conversations and item text reads "Madeline" (Settings > Her Name);
/// - the map's player marker is a tiny Madeline instead of Hornet's head;
/// - a Celeste checkpoint flag goes up beside every bench she has rested at (Settings > Bench Flags).
/// </summary>
public class MadelineTouches : MonoBehaviour
{
	// ---- her name ---------------------------------------------------------------------------------------------
	private static readonly Regex HornetWord = new Regex("\\bHornet\\b");

	private static readonly Regex HornetCaps = new Regex("\\bHORNET\\b");

	private void Awake()
	{
		try
		{
			Harmony h = new Harmony("silkeste.touches");
			HarmonyMethod post = new HarmonyMethod(typeof(MadelineTouches), "NamePostfix");
			foreach (MethodInfo m in typeof(TeamCherry.Localization.Language).GetMethods(BindingFlags.Static | BindingFlags.Public))
			{
				if (m.Name == "Get" && m.ReturnType == typeof(string))
				{
					h.Patch(m, null, post);
				}
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Her name: " + e.Message);
		}
		try
		{
			new Harmony("silkeste.bind").Patch(AccessTools.Method(typeof(CheckIsCharacterGrounded), "DoAction"), new HarmonyMethod(typeof(MadelineTouches), "BindGroundPrefix"));
			MenuInputGuard.Patch(new Harmony("silkeste.menuinput"));
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Bind ground check: " + e.Message);
		}
	}

	// The bind takes its silk, picks the ground or air bind from Madeline's own ground check, then keeps checking with
	// Hornet's raycasts and ends the bind with no heal when they disagree (an air bind started just above the floor,
	// user 2026-10-22). Madeline is held still while binding, so the bind it picked stands.
	private static bool BindGroundPrefix(CheckIsCharacterGrounded __instance)
	{
		HeroController h = HeroController.UnsafeInstance;
		string state = __instance.State?.Name;
		if (h == null || __instance.Fsm == null || __instance.Fsm.Name != "Bind" || __instance.Fsm.GameObject != h.gameObject || (state != "Bind Ground" && state != "Binding"))
		{
			return true;
		}
		bool ground = state == "Bind Ground";
		if (__instance.StoreResult != null && !__instance.StoreResult.IsNone)
		{
			__instance.StoreResult.Value = ground;
		}
		if (ground && __instance.GroundedEvent != null)
		{
			__instance.Fsm.Event(__instance.GroundedEvent);
		}
		return false;
	}

	private static void NamePostfix(ref string __result)
	{
		if (string.IsNullOrEmpty(__result) || __result.IndexOf("ornet", StringComparison.OrdinalIgnoreCase) < 0 || !AssistMode.CallHerMadeline)
		{
			return;
		}
		__result = HornetCaps.Replace(HornetWord.Replace(__result, "Madeline"), "MADELINE");
	}

	// ---- the map marker -----------------------------------------------------------------------------------------
	private static readonly FieldInfo CompassField = AccessTools.Field(typeof(GameMap), "compassIcon");

	private GameObject markedIcon;

	private SpriteRenderer marker;

	private float checkIn;

	private void Update()
	{
		if ((checkIn -= Time.unscaledDeltaTime) > 0f)
		{
			return;
		}
		checkIn = 0.5f;
		// the map marker and bench flags were taken out again (user 2026-10-17); only her name stays
	}

	private bool loggedMap;

	private bool loggedBench;

	private void MarkMap()
	{
		GameManager gm = GameManager.SilentInstance;
		if (gm == null || gm.gameMap == null || CompassField == null)
		{
			return;
		}
		GameObject icon = CompassField.GetValue(gm.gameMap) as GameObject;
		if (icon == null)
		{
			return;
		}
		if (icon != markedIcon || marker == null)
		{
			// a little Madeline, standing where Hornet's head was
			Sprite s = IdleSprite();
			if (s == null)
			{
				return;
			}
			GameObject go = new GameObject("Madeline Map Marker");
			go.transform.SetParent(icon.transform, false);
			go.layer = icon.layer;
			marker = go.AddComponent<SpriteRenderer>();
			marker.sprite = s;
			markedIcon = icon;
		}
		// Hornet's own picture stays hidden; ours takes its size and drawing order
		float h = 0f;
		foreach (Renderer r in icon.GetComponentsInChildren<Renderer>(true))
		{
			if (r == marker)
			{
				continue;
			}
			if (r.enabled && !r.forceRenderingOff)
			{
				h = Math.Max(h, r.bounds.size.y);
				marker.sortingLayerID = r.sortingLayerID;
				marker.sortingOrder = r.sortingOrder + 1;
			}
			r.forceRenderingOff = true;
		}
		if (h > 0.01f && marker.sprite != null)
		{
			float k = h * 1.4f / Math.Max(0.01f, marker.sprite.bounds.size.y);
			Vector3 parent = icon.transform.lossyScale;
			marker.transform.localScale = new Vector3(k / Math.Max(0.0001f, Math.Abs(parent.x)), k / Math.Max(0.0001f, Math.Abs(parent.y)), 1f);
			marker.transform.localPosition = new Vector3(0f, 0f, -0.01f);
		}
	}

	private static Sprite IdleSprite()
	{
		if (Plugin.PreviewAssets == null)
		{
			return null;
		}
		try
		{
			SkinRow skin = Array.Find(Sheets.Skins.All, (SkinRow k) => k.Name == AssistMode.SkinName);
			CelesteAssets a = Plugin.PreviewAssets(skin.Id != null ? skin : Sheets.Skins.Madeline);
			if (a != null && a.Anims.TryGetValue("idle", out var anim) && anim.Frames.Count > 0)
			{
				return anim.Frames[0].Sprite;
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	// ---- bench flags --------------------------------------------------------------------------------------------
	private static string FlagsFile => Path.Combine(AssistMode.DataDir ?? "", "bench_flags.cfg");

	private HashSet<string> planted;

	private readonly List<SpriteRenderer> flags = new List<SpriteRenderer>();

	private string flagScene;

	private int flagSlot = -1;

	private static Sprite[] flagFrames;

	private void CheckBenches()
	{
		HeroController h = HeroController.UnsafeInstance;
		GameManager gm = GameManager.SilentInstance;
		string scene = SceneManager.GetActiveScene().name;
		if (h == null || gm == null || gm.IsMenuScene() || !AssistMode.BenchFlagsOn)
		{
			ClearFlags();
			flagScene = null;
			return;
		}
		if (planted == null)
		{
			planted = new HashSet<string>();
			try
			{
				if (File.Exists(FlagsFile))
				{
					planted.UnionWith(File.ReadAllLines(FlagsFile));
				}
			}
			catch (Exception)
			{
			}
		}
		// resting now: this bench gets its flag
		if (h.playerData != null && h.playerData.atBench)
		{
			RestBench bench = Nearest(h.transform.position);
			if (bench != null)
			{
				string key = gm.profileID + "|" + scene + "|" + bench.name;
				if (planted.Add(key))
				{
					try
					{
						File.WriteAllLines(FlagsFile, new List<string>(planted).ToArray());
					}
					catch (Exception)
					{
					}
					flagScene = null;   // put it up now
					Sfx.Play(Sheets.Sounds.MenuToggleOn.Id, 0.7f);
				}
			}
		}
		if (scene != flagScene || gm.profileID != flagSlot)
		{
			flagScene = scene;
			flagSlot = gm.profileID;
			ClearFlags();
			foreach (RestBench b in UnityEngine.Object.FindObjectsByType<RestBench>(FindObjectsSortMode.None))
			{
				if (planted.Contains(gm.profileID + "|" + scene + "|" + b.name))
				{
					PlantFlag(b);
				}
			}
		}
	}

	private static RestBench Nearest(Vector3 p)
	{
		RestBench best = null;
		float d = 9f;
		foreach (RestBench b in UnityEngine.Object.FindObjectsByType<RestBench>(FindObjectsSortMode.None))
		{
			float dd = Vector2.Distance(b.transform.position, p);
			if (dd < d)
			{
				d = dd;
				best = b;
			}
		}
		return best;
	}

	private void PlantFlag(RestBench b)
	{
		if (flagFrames == null)
		{
			flagFrames = MakeFlag();
		}
		GameObject go = new GameObject("Celeste Bench Flag");
		go.transform.position = b.transform.position + new Vector3(-2.4f, -0.45f, 0.05f);
		SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
		sr.sprite = flagFrames[0];
		Renderer br = b.GetComponentInChildren<Renderer>();
		if (br != null)
		{
			sr.sortingLayerID = br.sortingLayerID;
			sr.sortingOrder = br.sortingOrder;
		}
		flags.Add(sr);
	}

	private void UpdateFlags()
	{
		if (flagFrames == null || flags.Count == 0)
		{
			return;
		}
		int f = (int)(Time.time / 0.22f) % flagFrames.Length;
		foreach (SpriteRenderer sr in flags)
		{
			if (sr != null)
			{
				sr.sprite = flagFrames[f];
			}
		}
	}

	private void ClearFlags()
	{
		foreach (SpriteRenderer sr in flags)
		{
			if (sr != null)
			{
				UnityEngine.Object.Destroy(sr.gameObject);
			}
		}
		flags.Clear();
	}

	// Celeste's summit flag, in pixels (user 2026-10-17: the old one bobbed about and looked rough): a stone base,
	// a pole with a gold knob, and a red pennant that stays put; only a soft light ripples across the cloth
	private static Sprite[] MakeFlag()
	{
		const int W = 28;
		const int H = 44;
		Sprite[] frames = new Sprite[4];
		Color pole = new Color(0.72f, 0.7f, 0.78f);
		Color poleDark = new Color(0.45f, 0.43f, 0.52f);
		Color knob = new Color(1f, 0.82f, 0.35f);
		Color stone = new Color(0.36f, 0.34f, 0.42f);
		Color stoneTop = new Color(0.52f, 0.5f, 0.6f);
		Color red = new Color(0.85f, 0.2f, 0.29f);
		Color redDark = new Color(0.58f, 0.1f, 0.19f);
		Color redLight = new Color(1f, 0.45f, 0.5f);
		for (int f = 0; f < frames.Length; f++)
		{
			Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
			Color[] px = new Color[W * H];
			// base stone (rows count from the bottom)
			for (int y = 0; y < 4; y++)
			{
				for (int x = 1; x < 11; x++)
				{
					px[y * W + x] = y == 3 ? stoneTop : stone;
				}
			}
			// pole, two pixels wide with a shaded side, and a gold knob on top
			for (int y = 4; y < H - 3; y++)
			{
				px[y * W + 5] = pole;
				px[y * W + 6] = poleDark;
			}
			for (int y = H - 3; y < H; y++)
			{
				for (int x = 4; x < 8; x++)
				{
					px[y * W + x] = knob;
				}
			}
			// the pennant: a triangle off the pole's top, outlined darker, with a light band moving along it
			int top = H - 5;
			for (int x = 7; x < W - 1; x++)
			{
				int span = 14 - (x - 7) * 13 / (W - 8);
				for (int k = 0; k < span; k++)
				{
					int y = top - k - (14 - span) / 2;
					if (y < 4)
					{
						continue;
					}
					bool edge = k == 0 || k == span - 1 || x == W - 2;
					float band = Mathf.Repeat((x - 7) - f * 4f, 16f);
					px[y * W + x] = edge ? redDark : (band < 2f ? redLight : red);
				}
			}
			tex.SetPixels(px);
			tex.Apply(false, true);
			frames[f] = Sprite.Create(tex, new Rect(0f, 0f, W, H), new Vector2(0.2f, 0f), 20f);
		}
		return frames;
	}

	// ---- unlock everything (dev, F10): only when silkeste_dev.txt sits next to the plugin ---------------------------
	private static bool? devOn;

	internal static bool DevOn
	{
		get
		{
			if (!devOn.HasValue)
			{
				try
				{
					devOn = File.Exists(Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? "", "silkeste_dev.txt"));
				}
				catch (Exception)
				{
					devOn = false;
				}
			}
			return devOn.Value;
		}
	}

	private void LateUpdate()
	{
		if (!DevOn)
		{
			return;
		}
		bool f10 = false;
		bool f9 = false;
		try
		{
			f10 = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f10Key.wasPressedThisFrame;
			f9 = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame;
		}
		catch (Exception)
		{
		}
		if (f9 && PlayerData.instance != null)
		{
			// F9 (dev): just the upgraded Needolin, with a full spool to play it (user 2026-10-17)
			PlayerData.instance.hasNeedolin = PlayerData.instance.hasNeedolinMemoryPowerup = true;
			try { HeroController.UnsafeInstance?.RefillSilkToMax(); } catch (Exception) { }
			Sfx.Play(Sheets.Sounds.MenuToggleOn.Id);
			Plugin.Log.LogInfo("F9: upgraded Needolin unlocked, silk full");
		}
		if (!f10 || PlayerData.instance == null)
		{
			return;
		}
		UnlockEverything();
	}

	/// <summary>Every ability, tool, crest and map, money, full silk, every rematch: F10, and the Sandbox mode.</summary>
	public static void UnlockEverything()
	{
		PlayerData pd = PlayerData.instance;
		if (pd == null)
		{
			return;
		}
		int n = 0;
		// every ability, then the whole map: every area's map, the quill, all pins and markers, every room drawn
		pd.hasDash = pd.hasWalljump = pd.hasDoubleJump = pd.hasBrolly = pd.hasHarpoonDash = pd.hasSuperJump = true;
		pd.hasNeedolinMemoryPowerup = pd.hasNeedolin = pd.hasChargeSlash = pd.hasNeedleThrow = pd.hasThreadSphere = pd.hasParry = true;
		pd.hasSilkCharge = pd.hasSilkBomb = pd.hasSilkBossNeedle = pd.hasSilkSpecial = true;
		pd.UnlockedExtraBlueSlot = pd.UnlockedExtraYellowSlot = true;
		pd.silkRegenMax = Math.Max(pd.silkRegenMax, 3);   // all 3 silk hearts (user 2026-10-21)
		pd.HasSeenSilkHearts = true;
		pd.nailUpgrades = Math.Max(pd.nailUpgrades, 4);   // the fully upgraded needle (user 2026-10-21)
		foreach (FieldInfo fi in typeof(PlayerData).GetFields(BindingFlags.Instance | BindingFlags.Public))
		{
			if (fi.FieldType != typeof(bool))
			{
				continue;
			}
			string nm = fi.Name;
			if ((nm.StartsWith("Has", StringComparison.Ordinal) && nm.EndsWith("Map", StringComparison.Ordinal)) || nm == "hasQuill" || nm == "mapAllRooms" || nm.StartsWith("hasPin", StringComparison.Ordinal) || nm.StartsWith("hasMarker", StringComparison.Ordinal))
			{
				fi.SetValue(pd, true);
				n++;
			}
		}
		try { ToolItemManager.UnlockAllTools(); } catch (Exception e) { Plugin.Log.LogWarning("Unlock all: tools: " + e.Message); }
		try { ToolItemManager.UnlockAllCrests(); } catch (Exception e) { Plugin.Log.LogWarning("Unlock all: crests: " + e.Message); }
		try { CurrencyManager.AddCurrency(99999, CurrencyType.Money); CurrencyManager.AddCurrency(9999, CurrencyType.Shard); } catch (Exception e) { Plugin.Log.LogWarning("Unlock all: money: " + e.Message); }
		try { HeroController.UnsafeInstance?.RefillSilkToMax(); } catch (Exception e) { Plugin.Log.LogWarning("Unlock all: silk: " + e.Message); }
		try { BossRematch.UnlockAll(); } catch (Exception e) { Plugin.Log.LogWarning("Unlock all: rematches: " + e.Message); }
		Sfx.Play(Sheets.Sounds.MenuToggleOn.Id);
		Plugin.Log.LogInfo("Unlock all: abilities, tools, crests, money and " + n + " map flags");
	}
}
