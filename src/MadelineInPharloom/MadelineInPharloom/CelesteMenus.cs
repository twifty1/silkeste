using System.Collections;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MadelineInPharloom;

public class CelesteMenus : MonoBehaviour
{
	private class Item
	{
		public Selectable Sel;

		public string Label = "";

		public string Value = "";

		public bool Arrows;

		public Rect Rect;

		public Sprite Icon;
	}

	internal static class HiFont
	{
		private class Hi
		{
			public Texture2D Fill;

			public Texture2D Line;

			public int Pad;
		}

		private const int F = 4;

		private static readonly Dictionary<int, Hi> cache = new Dictionary<int, Hi>();

		public static void Draw(GuiArt art, string text, float x, float top, float scale, Color c, Color outline, float stroke)
		{
			int num = Mathf.Clamp(Mathf.RoundToInt(stroke / scale * 4f), 0, 24);
			foreach (char c2 in text)
			{
				if (!art.Glyphs.TryGetValue(c2, out var value))
				{
					continue;
				}
				Hi hi = Get(c2, value, num);
				if (hi != null)
				{
					float num2 = scale / 4f;
					float num3 = (float)hi.Pad * num2;
					Rect position = new Rect(x + (float)value.XOffset * scale - num3, top + (float)value.YOffset * scale - num3, (float)value.W * scale + 2f * num3, (float)value.H * scale + 2f * num3);
					if (outline.a > 0f && num > 0)
					{
						GUI.color = outline;
						GUI.DrawTexture(position, hi.Line);
					}
					GUI.color = c;
					GUI.DrawTexture(position, hi.Fill);
				}
				x += (float)value.XAdvance * scale;
			}
		}

		private static Hi Get(char ch, GuiArt.Img g, int r)
		{
			int key = ch * 32 + r;
			if (cache.TryGetValue(key, out var value))
			{
				return value;
			}
			return cache[key] = Build(g, r);
		}

		private static Hi Build(GuiArt.Img g, int r)
		{
			if (g.Rgba == null || g.W <= 0 || g.H <= 0)
			{
				return null;
			}
			int num = r + 2;
			int num2 = g.W * 4 + num * 2;
			int num3 = g.H * 4 + num * 2;
			float[] array = new float[num2 * num3];
			for (int i = 0; i < num3; i++)
			{
				for (int j = 0; j < num2; j++)
				{
					float x = ((float)(j - num) + 0.5f) / 4f - 0.5f;
					float y = ((float)(i - num) + 0.5f) / 4f - 0.5f;
					float num4 = Sample(g, x, y);
					array[i * num2 + j] = Mathf.Clamp01((num4 - 0.5f) * 3f + 0.5f);
				}
			}
			float[] array2 = new float[num2 * num3];
			for (int k = 0; k < array2.Length; k++)
			{
				array2[k] = ((array[k] >= 0.5f) ? 0f : 1000000f);
			}
			for (int l = 0; l < num3; l++)
			{
				for (int m = 0; m < num2; m++)
				{
					int num5 = l * num2 + m;
					float num6 = array2[num5];
					if (m > 0)
					{
						num6 = Math.Min(num6, array2[num5 - 1] + 1f);
					}
					if (l > 0)
					{
						num6 = Math.Min(num6, array2[num5 - num2] + 1f);
						if (m > 0)
						{
							num6 = Math.Min(num6, array2[num5 - num2 - 1] + 1.4142f);
						}
						if (m < num2 - 1)
						{
							num6 = Math.Min(num6, array2[num5 - num2 + 1] + 1.4142f);
						}
					}
					array2[num5] = num6;
				}
			}
			for (int num7 = num3 - 1; num7 >= 0; num7--)
			{
				for (int num8 = num2 - 1; num8 >= 0; num8--)
				{
					int num9 = num7 * num2 + num8;
					float num10 = array2[num9];
					if (num8 < num2 - 1)
					{
						num10 = Math.Min(num10, array2[num9 + 1] + 1f);
					}
					if (num7 < num3 - 1)
					{
						num10 = Math.Min(num10, array2[num9 + num2] + 1f);
						if (num8 < num2 - 1)
						{
							num10 = Math.Min(num10, array2[num9 + num2 + 1] + 1.4142f);
						}
						if (num8 > 0)
						{
							num10 = Math.Min(num10, array2[num9 + num2 - 1] + 1.4142f);
						}
					}
					array2[num9] = num10;
				}
			}
			Color32[] array3 = new Color32[num2 * num3];
			Color32[] array4 = new Color32[num2 * num3];
			for (int n = 0; n < num3; n++)
			{
				for (int num11 = 0; num11 < num2; num11++)
				{
					int num12 = n * num2 + num11;
					int num13 = (num3 - 1 - n) * num2 + num11;
					float num14 = Mathf.Max(array[num12], Mathf.Clamp01((float)r + 0.5f - array2[num12]));
					array3[num13] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(array[num12] * 255f));
					array4[num13] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(num14 * 255f));
				}
			}
			return new Hi
			{
				Fill = Tex(num2, num3, array3),
				Line = Tex(num2, num3, array4),
				Pad = num
			};
		}

		private static float Alpha(GuiArt.Img g, int x, int y)
		{
			if (x < 0 || y < 0 || x >= g.W || y >= g.H)
			{
				return 0f;
			}
			return (float)(int)g.Rgba[(y * g.W + x) * 4 + 3] / 255f;
		}

		private static float Sample(GuiArt.Img g, float x, float y)
		{
			int num = Mathf.FloorToInt(x);
			int num2 = Mathf.FloorToInt(y);
			float t = x - (float)num;
			float t2 = y - (float)num2;
			float a = Mathf.Lerp(Alpha(g, num, num2), Alpha(g, num + 1, num2), t);
			float b = Mathf.Lerp(Alpha(g, num, num2 + 1), Alpha(g, num + 1, num2 + 1), t);
			return Mathf.Lerp(a, b, t2);
		}

		private static Texture2D Tex(int w, int h, Color32[] px)
		{
			Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: true);
			texture2D.filterMode = FilterMode.Trilinear;
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.anisoLevel = 2;
			texture2D.SetPixels32(px);
			texture2D.Apply(updateMipmaps: true, makeNoLongerReadable: true);
			return texture2D;
		}
	}

	internal static class MenuFx
	{
		private static Texture2D glow;

		private static Texture2D fadeBoth;

		private static Texture2D fadeOut;

		private static Texture2D fadeIn;

		private static Texture2D streak;

		public static Texture2D Glow => glow ?? (glow = Make(64, 64, delegate(float x, float y)
		{
			float num = Mathf.Sqrt(x * x + y * y);
			float num2 = Mathf.Clamp01(1f - num);
			return num2 * num2 * (3f - 2f * num2);
		}));

		public static Texture2D FadeBoth => fadeBoth ?? (fadeBoth = Make(128, 1, (float x, float y) => Mathf.Clamp01((1f - Mathf.Abs(x)) * 2.2f)));

		public static Texture2D FadeOut => fadeOut ?? (fadeOut = Make(128, 1, delegate(float x, float y)
		{
			float num = 1f - (x + 1f) / 2f;
			return num * num;
		}));

		public static Texture2D Streak => streak ?? (streak = Make(128, 1, delegate(float x, float y)
		{
			float num = (x + 1f) / 2f;
			float num2 = Mathf.Clamp01(num / 0.12f) * (1f - num);
			return num2 * num2;
		}));

		public static Texture2D FadeIn => fadeIn ?? (fadeIn = Make(128, 1, delegate(float x, float y)
		{
			float num = (x + 1f) / 2f;
			return num * num;
		}));

		private static Texture2D Make(int w, int h, Func<float, float, float> f)
		{
			Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp
			};
			Color32[] array = new Color32[w * h];
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					float arg = ((w > 1) ? ((float)j / ((float)w - 1f) * 2f - 1f) : 0f);
					float arg2 = ((h > 1) ? ((float)i / ((float)h - 1f) * 2f - 1f) : 0f);
					array[i * w + j] = new Color(1f, 1f, 1f, f(arg, arg2));
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			return texture2D;
		}
	}

	public static bool Showing;

	private CanvasGroup screen;

	private string kind = "";

	private string heading = "";

	private readonly List<Item> items = new List<Item>();

	private float refreshIn;

	private float alpha;

	private float flash;

	private GameObject lastSel;

	private bool loggedError;

	private static CelesteMenus Instance;

	private float restoreAt;

	private float diagUntil;

	private float diagIn;

	private GameObject beforeOverlay;

	private List<Transform> allScreens;

	private UIManager allScreensFor;

	private readonly Dictionary<Transform, Vector3> shrunk = new Dictionary<Transform, Vector3>();

	private static bool skyOnly;

	private static bool titleSeen;

	private bool wasOverlay;

	private float sinceFiles = 99f;

	private readonly HashSet<string> loggedScreens = new HashSet<string>();

	private float holdFor;

	private bool holding;

	private bool autoPicked;

	private GameObject shownSel;

	private Vector3 hiddenScale = Vector3.one;

	private static readonly Color Ink = new Color(0f, 0f, 0f, 0.75f);

	private float smoothScroll;

	private float barY = -1000f;

	private float barX = -1000f;

	private float barW;

	private float screenShownAt;

	private readonly Dictionary<Selectable, float> onAmt = new Dictionary<Selectable, float>();

	private float leaveFade = 1f;

	private static Texture2D dot;

	private static GuiArt Art => SpeedrunTimer.Art;

	private static bool OverlayOpen
	{
		get
		{
			if (!BossCard.RecordsOpen && !StartingAbilities.IsOpen)
			{
				return AssistMode.IsOpen;
			}
			return true;
		}
	}

	private void Awake()
	{
		Instance = this;
		try
		{
			Harmony harmony = new Harmony("silkeste.menusound");
			Type typeFromHandle = typeof(MenuAudioController);
			harmony.Patch(AccessTools.Method(typeFromHandle, "PlaySelect"), new HarmonyMethod(typeof(CelesteMenus), "Quiet"));
			harmony.Patch(AccessTools.Method(typeFromHandle, "PlaySubmit"), new HarmonyMethod(typeof(CelesteMenus), "Submit"));
			harmony.Patch(AccessTools.Method(typeFromHandle, "PlayCancel"), new HarmonyMethod(typeof(CelesteMenus), "Cancel"));
			harmony.Patch(AccessTools.Method(typeof(UIManager), "UIGoToAchievementsMenu"), new HarmonyMethod(typeof(CelesteMenus), "ToAchievements"));
			harmony.Patch(AccessTools.Method(typeof(UIManager), "UIGoToExtrasMenu"), new HarmonyMethod(typeof(CelesteMenus), "ToExtras"));
			harmony.Patch(AccessTools.Method(typeof(UIManager), "UIGoToOptionsMenu"), new HarmonyMethod(typeof(CelesteMenus), "ToOptions"));
			harmony.Patch(AccessTools.Method(typeof(GameManager), "PauseGameToggle"), new HarmonyMethod(typeof(CelesteMenus), "HoldPause"));
			harmony.Patch(AccessTools.Method(typeof(UIManager), "GoToOptionsMenu"), new HarmonyMethod(typeof(CelesteMenus), "OptionsRoutine"));
			harmony.Patch(AccessTools.Method(typeof(UIManager), "QuitGame"), new HarmonyMethod(typeof(CelesteMenus), "QuitGuard"));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("menu sound patch: " + ex.Message);
		}
	}

	private static bool Up()
	{
		if (!Showing && !CelesteFileSelect.Showing)
		{
			return skyOnly;
		}
		return true;
	}

	// a press that lands while the quit prompt is still opening (a double press on Quit Game, or the press that
	// opened it) never closes the game; a real Yes a moment later still does
	private static bool QuitGuard()
	{
		if (Instance != null && Instance.QuitPrompt && Time.unscaledTime - Instance.screenShownAt < 0.6f)
		{
			Plugin.Log.LogInfo("Quit prompt: a Yes right as it opened was ignored");
			return false;
		}
		Plugin.Log.LogInfo("Quit prompt: Yes, closing the game");
		return true;
	}

	private static bool Quiet()
	{
		return !Up();
	}

	private static bool Submit()
	{
		if (!Up())
		{
			return true;
		}
		Sfx.Play(Sounds.MenuButtonSelect.Id);
		return false;
	}

	private static bool ToAchievements()
	{
		if (Instance == null || Art == null)
		{
			return true;
		}
		Instance.Overlay();
		BossCard.OpenAchievementsFromMenu();
		return false;
	}

	// the pause menu's Options opens Silkeste's Settings too (user 2026-10-17); Silksong's pages live inside it
	// every way back from the binding pages (Back, Apply, Esc) runs this routine: from Settings, go to Settings
	private static bool OptionsRoutine(UIManager __instance)
	{
		if (!SilksongOptions.FromBindings)
		{
			return true;
		}
		// let Silksong step back to its Options normally (skipping that left the keyboard page stuck, user
		// 2026-10-17), then straight on out of Options and into Settings on the Controls tab
		SilksongOptions.FromBindings = false;
		__instance.StartCoroutine(BackToSettings(__instance));
		return true;
	}

	private static IEnumerator BackToSettings(UIManager ui)
	{
		float t = 0f;
		while (t < 0.5f)
		{
			t += Time.unscaledDeltaTime;
			yield return null;
		}
		GameManager gm = GameManager.SilentInstance;
		bool paused = gm != null && !gm.IsMenuScene() && gm.isPaused;
		try
		{
			ui.UILeaveOptionsMenu();
		}
		catch (Exception)
		{
		}
		if (Instance != null)
		{
			Instance.Overlay();
		}
		AssistMode.OpenAtTab = "Controls";
		if (paused)
		{
			AssistMode.OpenFromPause();
		}
		else
		{
			AssistMode.OpenFromMenu();
		}
	}

	private static IEnumerator Nothing()
	{
		yield break;
	}

	/// <summary>Set by Settings' "Pause Menu" button: the next pause goes through to Silksong's pause menu.</summary>
	public static bool AllowPause;

	// Esc in the game opens Settings instead of the pause menu (user 2026-10-17); Esc again closes it. A controller's
	// Start still pauses, and Settings has a Pause Menu button for Resume / Quit.
	private static bool EscInGame(GameManager __instance, ref IEnumerator __result)
	{
		if (AllowPause)
		{
			AllowPause = false;
			return true;
		}
		if (Instance == null || Art == null || __instance == null || __instance.IsMenuScene())
		{
			return true;
		}
		if (AssistMode.IsOpen || Time.unscaledTime - AssistMode.ClosedAt < 0.3f || StartingAbilities.IsOpen || Time.unscaledTime - StartingAbilities.ClosedAt < 0.3f)
		{
			__result = Nothing();   // that Esc was for Settings (or the starting abilities)
			return false;
		}
		bool esc = false;
		try
		{
			Keyboard kb = Keyboard.current;
			esc = kb != null && kb.escapeKey.wasPressedThisFrame;
		}
		catch (Exception)
		{
		}
		if (!esc || __instance.isPaused)
		{
			return true;
		}
		AssistMode.OpenFromPause();
		__result = Nothing();
		return false;
	}

	public static bool BypassOptions;

	// while Settings is open over the pause menu, the Esc that closes it must not also resume the game
	private static bool HoldPause(ref IEnumerator __result)
	{
		if (AssistMode.IsOpen || Time.unscaledTime - AssistMode.ClosedAt < 0.3f)
		{
			GameManager gm = GameManager.SilentInstance;
			if (gm != null && gm.isPaused)
			{
				__result = Nothing();
				return false;
			}
		}
		return true;
	}

	private static bool ToOptions()
	{
		if (BypassOptions)
		{
			BypassOptions = false;
			return true;
		}
		GameManager gm = GameManager.SilentInstance;
		if (SilksongOptions.FromBindings)
		{
			return true;   // coming back from the bindings: OptionsRoutine takes it from here
		}
		if (false)
		{
			// backing out of the keyboard or controller page: past Silksong's old Options, straight to Settings
			SilksongOptions.FromBindings = false;
			bool paused = gm != null && !gm.IsMenuScene() && gm.isPaused;
			try { UIManager.instance.UILeaveOptionsMenu(); } catch (Exception) { }
			Instance.Overlay();
			AssistMode.OpenAtTab = "Controls";   // back where they left from
			if (paused)
			{
				AssistMode.OpenFromPause();
			}
			else
			{
				AssistMode.OpenFromMenu();
			}
			return false;
		}
		if (Instance == null || Art == null || gm == null || gm.IsMenuScene() || !gm.isPaused)
		{
			return true;
		}
		Instance.Overlay();
		AssistMode.OpenFromPause();
		return false;
	}

	private static bool ToExtras()
	{
		if (Instance == null || Art == null)
		{
			return true;
		}
		Instance.Overlay();
		AssistMode.OpenFromMenu();
		return false;
	}

	private void Overlay()
	{
		EventSystem current = EventSystem.current;
		beforeOverlay = ((current != null && current.currentSelectedGameObject != null) ? current.currentSelectedGameObject : shownSel);
		if (current != null)
		{
			current.SetSelectedGameObject(null);
		}
	}

	private static bool Cancel()
	{
		if (!Up())
		{
			return true;
		}
		Sfx.Play(Sounds.MenuUnpause.Id);
		return false;
	}

	private void LateUpdate()
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
				Plugin.Log.LogError("CelesteMenus: " + ex);
			}
			Showing = false;
			screen = null;
		}
	}

	private CanvasGroup FindScreen(out string k)
	{
		k = "";
		UIManager instance = UIManager.instance;
		if (instance == null || CelesteFileSelect.Showing)
		{
			return null;
		}
		MenuScreen[] array = new MenuScreen[3] { instance.quitGamePrompt, instance.returnMainMenuPrompt, instance.resolutionPrompt };
		foreach (MenuScreen menuScreen in array)
		{
			if (Up(menuScreen))
			{
				k = "prompt";
				return menuScreen.ScreenCanvasGroup;
			}
		}
		array = new MenuScreen[15]
		{
			instance.remapGamepadMenuScreen, instance.keyboardMenuScreen, instance.advancedGamepadMenuScreen, instance.gamepadMenuScreen, instance.advancedVideoMenuScreen, instance.videoMenuScreen, instance.audioMenuScreen, instance.gameOptionsMenuScreen, instance.overscanMenuScreen, instance.brightnessMenuScreen,
			instance.achievementsMenuScreen, instance.extrasContentMenuScreen, instance.extrasMenuScreen, instance.playModeMenuScreen, instance.optionsMenuScreen
		};
		foreach (MenuScreen menuScreen2 in array)
		{
			if (Up(menuScreen2))
			{
				k = "page";
				return menuScreen2.ScreenCanvasGroup;
			}
		}
		GameManager silentInstance = GameManager.SilentInstance;
		if (instance.pauseMenuScreen != null && instance.pauseMenuScreen.gameObject.activeInHierarchy && silentInstance != null && silentInstance.isPaused && instance.pauseMenuScreen.ScreenCanvasGroup != null && instance.pauseMenuScreen.ScreenCanvasGroup.interactable)
		{
			k = "pause";
			return instance.pauseMenuScreen.ScreenCanvasGroup;
		}
		if (instance.mainMenuScreen != null && instance.mainMenuScreen.gameObject.activeInHierarchy && instance.mainMenuScreen.alpha > 0.01f)
		{
			k = "title";
			return instance.mainMenuScreen;
		}
		return null;
	}

	private static bool Up(MenuScreen m)
	{
		if (m == null || !m.gameObject.activeInHierarchy)
		{
			return false;
		}
		CanvasGroup screenCanvasGroup = m.ScreenCanvasGroup;
		if (screenCanvasGroup != null)
		{
			return screenCanvasGroup.alpha > 0.5f;
		}
		return false;
	}

	private void Tick()
	{
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		flash += unscaledDeltaTime;
		string k = "";
		CanvasGroup canvasGroup = ((Art != null) ? FindScreen(out k) : null);
		if (CelesteFileSelect.Showing)
		{
			sinceFiles = 0f;
		}
		else
		{
			sinceFiles += unscaledDeltaTime;
		}
		GameManager silentInstance = GameManager.SilentInstance;
		skyOnly = canvasGroup == null && !CelesteFileSelect.Showing && sinceFiles < 1.5f && (silentInstance == null || silentInstance.IsMenuScene()) && UIManager.instance != null;
		if (k == "title")
		{
			titleSeen = true;
		}
		// on the title scene with no menu up (also on the way back from a game, user 2026-10-20: Silksong's title
		// showed for a moment after Quit to Menu), the Celeste sky covers it
		if (canvasGroup == null && UIManager.instance != null && SceneManager.GetActiveScene().name == "Menu_Title")
		{
			skyOnly = true;
		}
		if (Art == null)
		{
			if (Time.realtimeSinceStartup > 60f)
			{
				skyOnly = false;
			}
			HideAll(skyOnly);
			return;
		}
		GameManager silentInstance2 = GameManager.SilentInstance;
		// Keep Silksong's own pause menu hidden until its fade-out after unpausing is over too,
		// or it shows for a moment once ours is gone.
		UIManager instance0 = UIManager.instance;
		bool pauseFading = instance0 != null && instance0.pauseMenuScreen != null && instance0.pauseMenuScreen.gameObject.activeInHierarchy && instance0.pauseMenuScreen.ScreenCanvasGroup != null && instance0.pauseMenuScreen.ScreenCanvasGroup.alpha > 0.01f && silentInstance2 != null && !silentInstance2.IsMenuScene();
		HideAll(skyOnly || canvasGroup != null || screen != null || CelesteFileSelect.Showing || sinceFiles < 1.5f || pauseFading || (silentInstance2 != null && silentInstance2.isPaused && !silentInstance2.IsMenuScene()));
		if (canvasGroup == null)
		{
			k = "";
		}
		GameManager silentInstance3 = GameManager.SilentInstance;
		holding = canvasGroup == null && screen != null && (holdFor += unscaledDeltaTime) < 1.5f && UIManager.instance != null && (silentInstance3 == null || silentInstance3.IsMenuScene() || silentInstance3.isPaused);
		if (holding)
		{
			return;
		}
		holdFor = 0f;
		if (canvasGroup != screen)
		{
			RestoreRaycasts();
			screen = canvasGroup;
			kind = k;
			refreshIn = 0f;
			autoPicked = false;
			shownSel = null;
			screenShownAt = Time.unscaledTime;
			barY = -1000f;
			barX = -1000f;
			barW = 0f;
			smoothScroll = 0f;
		}
		Showing = screen != null;
		alpha = ((screen != null) ? 1f : Mathf.MoveTowards(alpha, 0f, unscaledDeltaTime / 0.15f));
		if (screen == null)
		{
			return;
		}
		screen.blocksRaycasts = false;
		if ((refreshIn -= unscaledDeltaTime) <= 0f)
		{
			refreshIn = 0.6f;
			ShowRemap();
			Read();
		}
		if (items.Count == 0)
		{
			Showing = false;
			return;
		}
		EventSystem current = EventSystem.current;
		if (OverlayOpen)
		{
			if (current != null && current.currentSelectedGameObject != null)
			{
				current.SetSelectedGameObject(null);
			}
			wasOverlay = true;
			return;
		}
		if (wasOverlay)
		{
			wasOverlay = false;
			restoreAt = Time.unscaledTime + 0.3f;
		}
		if (restoreAt > 0f)
		{
			if (Time.unscaledTime < restoreAt)
			{
				if (current != null && current.currentSelectedGameObject != null)
				{
					current.SetSelectedGameObject(null);
				}
				return;
			}
			restoreAt = 0f;
			GameObject gameObject = ((beforeOverlay != null && beforeOverlay.activeInHierarchy) ? beforeOverlay : DefaultPick());
			if (current != null && gameObject != null)
			{
				current.SetSelectedGameObject(gameObject);
			}
			if (ManagerSingleton<InputHandler>.Instance != null)
			{
				ManagerSingleton<InputHandler>.Instance.StartUIInput();
			}
		}
		GameObject sel = ((current != null) ? current.currentSelectedGameObject : null);
		if (!autoPicked && current != null && (sel == null || items.Find((Item i) => i.Sel != null && i.Sel.gameObject == sel) == null))
		{
			GameObject pick = DefaultPick();
			if (pick != null)
			{
				current.SetSelectedGameObject(pick);
				sel = pick;
			}
		}
		// the quit and exit prompts stay on No while they open (2.0.3: players' games closed ~20 s after starting,
		// right after this prompt): whatever Silksong or a stray input picks in that time goes back to No
		if (QuitPrompt && current != null && Time.unscaledTime - screenShownAt < 0.6f)
		{
			GameObject no = DefaultPick();
			if (no != null && sel != no)
			{
				current.SetSelectedGameObject(no);
				sel = no;
			}
		}
		autoPicked = true;
		if (sel != null && items.Find((Item i) => i.Sel != null && i.Sel.gameObject == sel) != null)
		{
			shownSel = sel;
		}
		if (sel != lastSel && sel != null && lastSel != null)
		{
			Sfx.Play(Sounds.MenuRolloverDown.Id, 0.8f);
		}
		if (sel != lastSel)
		{
			flash = 0f;
		}
		lastSel = sel;
		Mouse current2 = Mouse.current;
		if (current2 != null && current != null)
		{
			Vector2 point = MenuMouse.Gui();
			foreach (Item item in items)
			{
				if (!item.Rect.Contains(point) || item.Sel == null)
				{
					continue;
				}
				// on the quit prompts the mouse resting near the middle of the screen sat on Yes, so the smallest nudge
				// picked it and the next confirm press closed the game: there only a click chooses
				if (!QuitPrompt && current2.delta.ReadValue().sqrMagnitude > 0.5f && sel != item.Sel.gameObject)
				{
					current.SetSelectedGameObject(item.Sel.gameObject);
				}
				if (current2.leftButton.wasPressedThisFrame && (!QuitPrompt || Time.unscaledTime - screenShownAt > 0.6f))
				{
					current.SetSelectedGameObject(item.Sel.gameObject);
					if (item.Arrows)
					{
						AxisEventData eventData = new AxisEventData(current)
						{
							moveDir = ((!(point.x < item.Rect.center.x)) ? MoveDirection.Right : MoveDirection.Left)
						};
						ExecuteEvents.Execute(item.Sel.gameObject, eventData, ExecuteEvents.moveHandler);
					}
					else
					{
						ExecuteEvents.Execute(item.Sel.gameObject, new BaseEventData(current), ExecuteEvents.submitHandler);
					}
				}
			}
		}
		foreach (Item item2 in items)
		{
			item2.Value = ValueOf(item2.Sel);
			item2.Icon = IconOf(item2.Sel);
		}
	}

	// The button Silksong itself would highlight first: No on the quit and exit prompts (so a
	// stray confirm press never quits), else the screen's own default, else the first button.
	private GameObject DefaultPick()
	{
		if (items.Count == 0)
		{
			return null;
		}
		if (kind == "prompt")
		{
			Item no = items.Find((Item i) => i.Sel != null && i.Sel.name.StartsWith("No", StringComparison.OrdinalIgnoreCase));
			if (no != null)
			{
				return no.Sel.gameObject;
			}
		}
		MenuScreen menuScreen = ((screen != null) ? screen.GetComponent<MenuScreen>() : null);
		if (menuScreen != null && menuScreen.defaultHighlight != null)
		{
			Selectable first = menuScreen.defaultHighlight.GetFirstInteractable();
			if (first != null && items.Find((Item i) => i.Sel == first) != null)
			{
				return first.gameObject;
			}
		}
		if (items[0].Sel == null)
		{
			return null;
		}
		return items[0].Sel.gameObject;
	}

	private void HideAll(bool on)
	{
		UIManager instance = UIManager.instance;
		if (instance == null || (!on && !CelesteFileSelect.Showing))
		{
			if (shrunk.Count <= 0)
			{
				return;
			}
			foreach (KeyValuePair<Transform, Vector3> item in shrunk)
			{
				if (item.Key != null)
				{
					item.Key.localScale = item.Value;
				}
			}
			shrunk.Clear();
			return;
		}
		if (allScreens == null || allScreensFor != instance)
		{
			allScreensFor = instance;
			allScreens = new List<Transform>();
			MenuScreen[] array = new MenuScreen[19]
			{
				instance.quitGamePrompt, instance.returnMainMenuPrompt, instance.resolutionPrompt, instance.remapGamepadMenuScreen, instance.keyboardMenuScreen, instance.advancedGamepadMenuScreen, instance.gamepadMenuScreen, instance.advancedVideoMenuScreen, instance.videoMenuScreen, instance.audioMenuScreen,
				instance.gameOptionsMenuScreen, instance.overscanMenuScreen, instance.brightnessMenuScreen, instance.achievementsMenuScreen, instance.extrasContentMenuScreen, instance.extrasMenuScreen, instance.playModeMenuScreen, instance.optionsMenuScreen, instance.pauseMenuScreen
			};
			foreach (MenuScreen menuScreen in array)
			{
				if (menuScreen != null)
				{
					allScreens.Add(menuScreen.transform);
				}
			}
			if (instance.mainMenuScreen != null)
			{
				allScreens.Add(instance.mainMenuScreen.transform);
			}
		}
		List<Transform> list = allScreens;
		if (list == null)
		{
			MenuScreen[] array = new MenuScreen[19]
			{
				instance.quitGamePrompt, instance.returnMainMenuPrompt, instance.resolutionPrompt, instance.remapGamepadMenuScreen, instance.keyboardMenuScreen, instance.advancedGamepadMenuScreen, instance.gamepadMenuScreen, instance.advancedVideoMenuScreen, instance.videoMenuScreen, instance.audioMenuScreen,
				instance.gameOptionsMenuScreen, instance.overscanMenuScreen, instance.brightnessMenuScreen, instance.achievementsMenuScreen, instance.extrasContentMenuScreen, instance.extrasMenuScreen, instance.playModeMenuScreen, instance.optionsMenuScreen, instance.pauseMenuScreen
			};
			foreach (MenuScreen menuScreen2 in array)
			{
				if (menuScreen2 != null)
				{
					list.Add(menuScreen2.transform);
				}
			}
			if (instance.mainMenuScreen != null)
			{
				list.Add(instance.mainMenuScreen.transform);
			}
		}
		foreach (Transform item2 in list)
		{
			if (!(item2 == null) && !(item2.localScale == Vector3.zero))
			{
				if (!shrunk.ContainsKey(item2))
				{
					shrunk[item2] = item2.localScale;
				}
				item2.localScale = Vector3.zero;
			}
		}
	}

	private void RestoreRaycasts()
	{
		if (screen != null)
		{
			screen.blocksRaycasts = true;
		}
	}

	private void Read()
	{
		items.Clear();
		heading = "";
		float num = 0f;
		Text[] componentsInChildren = screen.GetComponentsInChildren<Text>(includeInactive: false);
		foreach (Text text in componentsInChildren)
		{
			if (!(text.GetComponentInParent<Selectable>() != null) && !string.IsNullOrEmpty(text.text) && text.isActiveAndEnabled && (float)text.fontSize > num && text.text.Trim().Length > 1)
			{
				num = text.fontSize;
				heading = Clean(text.text);
			}
		}
		List<Selectable> list = new List<Selectable>();
		Selectable[] componentsInChildren2;
		if (!loggedScreens.Contains(screen.name))
		{
			loggedScreens.Add(screen.name);
			StringBuilder stringBuilder = new StringBuilder("Celeste menus: " + screen.name + ":");
			componentsInChildren2 = screen.GetComponentsInChildren<Selectable>(includeInactive: true);
			foreach (Selectable selectable in componentsInChildren2)
			{
				stringBuilder.Append(" [" + selectable.name + " " + selectable.GetType().Name + (selectable.gameObject.activeInHierarchy ? "" : " inactive") + (selectable.interactable ? "" : " noninteract") + ((selectable.gameObject.activeInHierarchy && Invisible(selectable)) ? " invisible" : "") + "]");
			}
			Plugin.Log.LogInfo(stringBuilder.ToString());
		}
		if (kind == "pause")
		{
			EnsureDifficulty();
		}
		componentsInChildren2 = screen.GetComponentsInChildren<Selectable>(includeInactive: false);
		foreach (Selectable selectable2 in componentsInChildren2)
		{
			if (selectable2.isActiveAndEnabled && (selectable2.interactable || selectable2 is MappableKey || selectable2 is MappableControllerButton) && selectable2.gameObject.activeInHierarchy && !(selectable2 is Scrollbar) && !Invisible(selectable2))
			{
				list.Add(selectable2);
			}
		}
		foreach (Selectable item2 in list)
		{
			string text2 = LabelOf(item2);
			if (text2.Equals("Sprint", StringComparison.OrdinalIgnoreCase))
			{
				text2 = "Climb";
			}
			if (kind == "title" && text2.Equals("Options", StringComparison.OrdinalIgnoreCase))
			{
				// Silksong's Options lives inside Settings now (user 2026-10-17): the button is switched off and the
				// title's list rebuilt, so up/down never lands on it
				if (item2.gameObject.activeSelf)
				{
					item2.gameObject.SetActive(false);
					MenuButtonList mbl = screen.GetComponentInChildren<MenuButtonList>(true);
					if (mbl != null)
					{
						try { mbl.SetupActive(); } catch (Exception) { }
					}
				}
				continue;
			}
			if (kind == "title" && text2.Equals("Extras", StringComparison.OrdinalIgnoreCase))
			{
				text2 = "Settings";
			}
			if (kind == "pause")
			{
				// the pause menu as Continue / Settings / Quit (user 2026-10-17)
				if (text2.Equals("Resume", StringComparison.OrdinalIgnoreCase) || text2.Equals("Continue", StringComparison.OrdinalIgnoreCase))
				{
					text2 = "Continue";
				}
				else if (text2.Equals("Options", StringComparison.OrdinalIgnoreCase))
				{
					text2 = "Settings";
				}
				else if (text2.IndexOf("quit", StringComparison.OrdinalIgnoreCase) >= 0 || text2.IndexOf("menu", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					text2 = BossRematch.Active ? "Give Up" : "Quit";
				}
			}
			Item item = new Item
			{
				Sel = item2,
				Label = text2,
				Arrows = (item2 is MenuOptionHorizontal || item2 is Slider || item2 is DifficultyOption || item2.GetComponentInChildren<Slider>() != null)
			};
			item.Value = ValueOf(item2);
			if (item.Label.Length == 0 && item.Value.Length == 0)
			{
				item.Label = Pretty(item2.gameObject.name);
			}
			items.Add(item);
		}
		if (kind == "title")
		{
			// Settings sits above Achievements on the title (user 2026-10-17); up/down follows the drawn order
			int s = items.FindIndex((Item i) => i.Label == "Settings");
			int ach = items.FindIndex((Item i) => i.Label.IndexOf("chievement", StringComparison.OrdinalIgnoreCase) >= 0);
			if (s > ach && ach >= 0)
			{
				Item moved = items[s];
				items.RemoveAt(s);
				items.Insert(ach, moved);
			}
			for (int n = 0; n < items.Count; n++)
			{
				if (items[n].Sel == null)
				{
					continue;
				}
				Navigation nav = items[n].Sel.navigation;
				nav.mode = Navigation.Mode.Explicit;
				nav.selectOnUp = items[(n + items.Count - 1) % items.Count].Sel;
				nav.selectOnDown = items[(n + 1) % items.Count].Sel;
				items[n].Sel.navigation = nav;
			}
		}
	}

	// ---- the pause menu's Difficulty (user 2026-10-19): a copy of the Options button between Settings and Quit -----

	private void EnsureDifficulty()
	{
		// the pause menu's Leave Tutorial (user 2026-10-20), in the place the Difficulty row had: only in the tutorial
		try
		{
			if (difficultyRow != null && (!TechRoom.Active || !difficultyRow.transform.IsChildOf(screen.transform)))
			{
				UnityEngine.Object.Destroy(difficultyRow.gameObject);
				difficultyRow = null;
			}
			if (!TechRoom.Active || difficultyRow != null)
			{
				return;
			}
			Selectable options = null;
			Selectable quit = null;
			foreach (Selectable s in screen.GetComponentsInChildren<Selectable>(includeInactive: false))
			{
				if (s is LeaveTutorialOption)
				{
					continue;
				}
				string l = LabelOf(s);
				if (l.Equals("Options", StringComparison.OrdinalIgnoreCase))
				{
					options = s;
				}
				else if (l.IndexOf("quit", StringComparison.OrdinalIgnoreCase) >= 0 || l.IndexOf("menu", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					quit = s;
				}
			}
			if (options == null)
			{
				return;
			}
			GameObject go = UnityEngine.Object.Instantiate(options.gameObject, options.transform.parent);
			go.name = "SilkesteLeaveTutorial";
			go.transform.SetSiblingIndex(options.transform.GetSiblingIndex() + 1);
			foreach (Component c in go.GetComponentsInChildren<Component>(true))
			{
				if (c != null && (c is Selectable || c.GetType().Name.Contains("Locali") || c.GetType().Name == "Animator" || c is EventTrigger))
				{
					UnityEngine.Object.DestroyImmediate(c);
				}
			}
			foreach (Text tx in go.GetComponentsInChildren<Text>(true))
			{
				tx.text = "Leave Tutorial";
			}
			difficultyRow = go.AddComponent<LeaveTutorialOption>();
			difficultyRow.transition = Selectable.Transition.None;
			Navigation n = difficultyRow.navigation;
			n.mode = Navigation.Mode.Explicit;
			n.selectOnUp = options;
			n.selectOnDown = quit != null ? quit : options;
			difficultyRow.navigation = n;
			Navigation no = options.navigation;
			no.mode = Navigation.Mode.Explicit;
			no.selectOnDown = difficultyRow;
			options.navigation = no;
			if (quit != null)
			{
				Navigation nq = quit.navigation;
				nq.mode = Navigation.Mode.Explicit;
				nq.selectOnUp = difficultyRow;
				quit.navigation = nq;
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Pause Leave Tutorial: " + e.Message);
		}
	}

	private Selectable difficultyRow;

	// ---- the title's Continue (user 2026-10-17): a copy of Start Game that loads the save played last ----------
	private Button continueButton;

	private void MakeContinue(Selectable start)
	{
		try
		{
			GameObject go = UnityEngine.Object.Instantiate(start.gameObject, start.transform.parent);
			go.name = "SilkesteContinue";
			go.transform.SetSiblingIndex(start.transform.GetSiblingIndex());
			foreach (Component c in go.GetComponentsInChildren<Component>(true))
			{
				if (c != null && (c is Selectable || c.GetType().Name.Contains("Locali") || c.GetType().Name == "Animator" || c is EventTrigger))
				{
					UnityEngine.Object.DestroyImmediate(c);
				}
			}
			foreach (Text tx in go.GetComponentsInChildren<Text>(true))
			{
				tx.text = "Continue";
			}
			continueButton = go.AddComponent<Button>();
			continueButton.transition = Selectable.Transition.None;
			continueButton.onClick.AddListener(delegate
			{
				int slot = CelesteFileSelect.LastSlot;
				if (slot < 0 || UIManager.instance == null)
				{
					return;
				}
				Sfx.Play(Sounds.MenuButtonSelect.Id);
				CelesteFileSelect.Continue(slot);
				UIManager.instance.UIGoToProfileMenu();
			});
			Plugin.Log.LogInfo("Title: Continue added (save " + CelesteFileSelect.LastSlot + ")");
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Title Continue: " + e.Message);
		}
	}

	private bool Invisible(Selectable s)
	{
		Transform parent = s.transform;
		while (parent != null && parent != screen.transform)
		{
			CanvasGroup component = parent.GetComponent<CanvasGroup>();
			if (component != null && component.alpha < 0.1f)
			{
				return true;
			}
			parent = parent.parent;
		}
		return false;
	}

	private static string LabelOf(Selectable s)
	{
		Text text = null;
		MenuOptionHorizontal menuOptionHorizontal = s as MenuOptionHorizontal;
		Text[] componentsInChildren = s.GetComponentsInChildren<Text>(includeInactive: false);
		foreach (Text text2 in componentsInChildren)
		{
			if (text2.isActiveAndEnabled && !string.IsNullOrEmpty(text2.text) && (!(menuOptionHorizontal != null) || !(text2 == menuOptionHorizontal.optionText)) && (text == null || (text2.text.Length <= 32 && (text.text.Length > 32 || text2.transform.position.x < text.transform.position.x))))
			{
				text = text2;
			}
		}
		if (text != null)
		{
			return Clean(text.text);
		}
		Component[] componentsInChildren2 = s.GetComponentsInChildren<Component>(includeInactive: false);
		foreach (Component component in componentsInChildren2)
		{
			PropertyInfo propertyInfo = ((component != null) ? component.GetType().GetProperty("text") : null);
			if (propertyInfo != null && component.GetType().Name.StartsWith("TextMeshPro"))
			{
				string text3 = propertyInfo.GetValue(component, null) as string;
				if (!string.IsNullOrEmpty(text3))
				{
					return Clean(text3);
				}
			}
		}
		return "";
	}

	private static Sprite IconOf(Selectable s)
	{
		Image image = ((s is MappableControllerButton) ? ((MappableControllerButton)s).buttonmapSprite : ((s is MappableKey) ? ((MappableKey)s).keymapSprite : null));
		if (!(image != null) || !image.isActiveAndEnabled || !(image.sprite != null))
		{
			return null;
		}
		return image.sprite;
	}

	private static string ValueOf(Selectable s)
	{
		if (s == null)
		{
			return "";
		}
		if (s is DifficultyOption)
		{
			return AssistMode.ValueOf(AssistOptions.Difficulty);
		}
		if (s is MappableControllerButton)
		{
			Text buttonmapText = ((MappableControllerButton)s).buttonmapText;
			if (!(buttonmapText != null))
			{
				return "";
			}
			return Clean(buttonmapText.text);
		}
		if (s is MappableKey)
		{
			Text keymapText = ((MappableKey)s).keymapText;
			if (!(keymapText != null))
			{
				return "";
			}
			return Clean(keymapText.text);
		}
		MenuOptionHorizontal menuOptionHorizontal = s as MenuOptionHorizontal;
		if (menuOptionHorizontal != null && menuOptionHorizontal.optionText != null)
		{
			return Clean(menuOptionHorizontal.optionText.text);
		}
		Slider slider = (s as Slider) ?? s.GetComponentInChildren<Slider>();
		if (slider != null)
		{
			return Mathf.RoundToInt(slider.value).ToString();
		}
		Text text = null;
		Text text2 = null;
		Text[] componentsInChildren = s.GetComponentsInChildren<Text>(includeInactive: false);
		foreach (Text text3 in componentsInChildren)
		{
			if (text3.isActiveAndEnabled && !string.IsNullOrEmpty(text3.text))
			{
				if (text == null || text3.transform.position.x < text.transform.position.x)
				{
					text2 = text;
					text = text3;
				}
				else if (text2 == null || text3.transform.position.x > text2.transform.position.x)
				{
					text2 = text3;
				}
			}
		}
		if (!(text2 != null) || !(text2 != text))
		{
			return "";
		}
		return Clean(text2.text);
	}

	private static string Clean(string s)
	{
		s = Regex.Replace(s ?? "", "<[^>]*>", "").Replace('\n', ' ').Trim();
		s = s.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"')
			.Replace('”', '"')
			.Replace('—', '-')
			.Replace('–', '-');
		return s;
	}

	private static string Pretty(string n)
	{
		return Regex.Replace(n.Replace("Button", "").Replace("_", " "), "([a-z])([A-Z])", "$1 $2").Trim();
	}

	private void OnGUI()
	{
		if (Event.current.type == EventType.Repaint && screen == null && skyOnly)
		{
			float num = (float)Screen.height / 1080f;
			Matrix4x4 matrix = GUI.matrix;
			GUI.depth = -6;
			GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num, num, 1f));
			CelesteFileSelect.Background((float)Screen.width / num, 1f);
			GUI.matrix = matrix;
			GUI.color = Color.white;
		}
		else
		{
			if (Event.current.type != EventType.Repaint || Art == null || screen == null)
			{
				return;
			}
			leaveFade = (holding ? Mathf.Clamp01(1f - holdFor / 0.14f) : 1f);
			bool num2 = (!holding || leaveFade > 0f) && items.Count > 0 && !OverlayOpen;
			if (OverlayOpen)
			{
				GUI.depth = 3;
			}
			float num3 = (float)Screen.height / 1080f;
			float num4 = (float)Screen.width / num3;
			Matrix4x4 matrix2 = GUI.matrix;
			GUI.depth = (OverlayOpen ? 3 : (-6));
			GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num3, num3, 1f));
			if (kind == "pause")
			{
				GUI.color = new Color(0f, 0f, 0f, 0.55f);
				GUI.DrawTexture(new Rect(0f, 0f, num4, 1080f), Texture2D.whiteTexture);
				float num5 = 1.7f;
				HiFont.Draw(Art, "PAUSED", num4 / 2f - Art.Measure("PAUSED") * num5 / 2f, 250f, num5, new Color(0.72f, 0.72f, 0.74f), new Color(0.16f, 0.12f, 0.22f, 1f), 6f);
				DrawPauseInfo(num4);
			}
			else
			{
				CelesteFileSelect.Background(num4, 1f);
				if (kind != "title")
				{
					GUI.color = new Color(0.03f, 0.02f, 0.08f, 0.3f);
					GUI.DrawTexture(new Rect(0f, 0f, num4, 1080f), Texture2D.whiteTexture);
					float num6 = Math.Min(num4, 1700f);
					GUI.color = new Color(0.02f, 0.01f, 0.07f, 0.55f);
					GUI.DrawTexture(new Rect(num4 / 2f - num6 / 2f, 0f, num6, 1080f), MenuFx.FadeBoth);
				}
				else
				{
					GUI.color = new Color(0.02f, 0.01f, 0.07f, 0.55f);
					GUI.DrawTexture(new Rect(0f, 0f, 1100f, 1080f), MenuFx.FadeOut);
				}
			}
			GameObject sel = shownSel;
			if (num2)
			{
				if (kind == "title")
				{
					DrawTitle(num4, sel);
				}
				else
				{
					DrawList(num4, sel);
				}
				DrawHints(num4);
				// the title left alone: the mountain drifts and the menu fades out until a key or the mouse moves
				// (user 2026-10-17)
				float idle = TitleIdle();
				if (kind == "title" && idle > 0.001f)
				{
					CelesteFileSelect.Background(num4, idle);
				}
				if (kind == "title")
				{
					DrawTip(num4, 1f - idle);
				}
			}
			GUI.matrix = matrix2;
			GUI.color = Color.white;
		}
	}

	private Color Hi()
	{
		if (AssistMode.LowDetailMenus)
		{
			return new Color(0.72f, 0.86f, 1f);
		}
		if (!(flash % 0.2f < 0.1f))
		{
			return Hex("FCFF59");
		}
		return Hex("84FF54");
	}

	private static Color Hex(string h)
	{
		if (!ColorUtility.TryParseHtmlString("#" + h, out var color))
		{
			return Color.white;
		}
		return color;
	}

	private float lastActive;

	private Vector2 lastMouse;

	private GameObject lastIdleSel;

	private float idleAmount;

	// ---- title tips (user 2026-10-17): one line at a time along the bottom, fading from one to the next -----------
	private static readonly string[] Tips = new string[13]
	{
		"Dashing through an enemy hits it, and nothing can hit you mid-dash.",
		"Your dash comes back when you touch the ground.",
		"Hold Dash against a wall to grab it, once you have Cling Grip.",
		"Jump at the end of a ground dash for a super: long and fast.",
		"Dash down-diagonal into the ground, then jump, for a wavedash.",
		"Records keeps every boss you've beaten. Pick one to fight it again.",
		"Win a boss fight first try, or without healing, for its golden berry.",
		"Each save keeps its own game mode.",
		"One-Hit Mode: any enemy hit is fatal, like in Celeste.",
		"Theo's camera takes a photo without the HUD. Find it in Records.",
		"Madeline's hair turns pink while she still has two dashes.",
		"Postcards explain each new ability. They stay in Records.",
		"Feeling stuck? Assist Mode can slow the game down or give endless stamina."
	};

	private void DrawTip(float sw, float a)
	{
		if (Art == null || a <= 0.01f)
		{
			return;
		}
		float t = Time.unscaledTime / 7f;
		int i = (int)t % Tips.Length;
		float k = t - Mathf.Floor(t);
		float fade = Mathf.Clamp01(Mathf.Min(k, 1f - k) * 8f);
		string tip = Tips[i];
		float s = 0.42f;
		Art.DrawOutlined(tip, sw / 2f - Art.Measure(tip) * s / 2f, 1012f, s, new Color(0.9f, 0.9f, 0.95f, 0.85f * a * fade), new Color(0f, 0f, 0f, 0.7f * a * fade), 2f);
	}

	private float TitleIdle()
	{
		float now = Time.unscaledTime;
		bool act = false;
		try
		{
			Vector2 m = MenuMouse.Gui();
			if ((m - lastMouse).sqrMagnitude > 4f)
			{
				act = true;
			}
			lastMouse = m;
			Keyboard kb = Keyboard.current;
			if (kb != null && kb.anyKey.isPressed)
			{
				act = true;
			}
			InControl.InputDevice dev = InControl.InputManager.ActiveDevice;
			if (dev != null && (dev.AnyButtonIsPressed || dev.LeftStick.Vector.sqrMagnitude > 0.09f || dev.DPad.Vector.sqrMagnitude > 0.09f))
			{
				act = true;
			}
			GameObject s = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
			if (s != lastIdleSel)
			{
				act = true;
			}
			lastIdleSel = s;
		}
		catch (Exception)
		{
		}
		if (act || kind != "title")
		{
			lastActive = now;
		}
		float target = (now - lastActive > 25f) ? 1f : 0f;
		idleAmount = Mathf.MoveTowards(idleAmount, target, Time.unscaledDeltaTime / (target > idleAmount ? 1.5f : 0.3f));
		CelesteFileSelect.ExtraPan = idleAmount * Mathf.Sin(now * 0.08f) * 60f;
		return idleAmount;
	}

	private void DrawTitle(float sw, GameObject sel)
	{
		bool lowDetail = AssistMode.LowDetailMenus;
		float unscaledTime = lowDetail ? 0f : Time.unscaledTime;
		string text = "SILKESTE";
		float num = 2.2f;
		float num2 = 140f;
		float num3 = 120f;
		float num4 = Art.Measure(text) * num;
		float num5 = (float)Art.LineHeight * num;
		if (lowDetail)
		{
			HiFont.Draw(Art, text, num2, num3, num, new Color(0.78f, 0.85f, 1f), new Color(0.12f, 0.08f, 0.2f, 1f), 3f);
		}
		else
		{
			float num6 = 0.8f + Mathf.Sin(unscaledTime * 0.9f) * 0.2f;
			GUI.color = new Color(1f, 0.45f, 0.6f, 0.4f * num6);
			GUI.DrawTexture(new Rect(num2 - 120f, num3 - 90f, num4 * 0.65f + 120f, num5 + 180f), MenuFx.Glow);
			GUI.color = new Color(0.5f, 0.7f, 1f, 0.4f * num6);
			GUI.DrawTexture(new Rect(num2 + num4 * 0.35f, num3 - 90f, num4 * 0.65f + 120f, num5 + 180f), MenuFx.Glow);
			HiFont.Draw(Art, text, num2 + 6f, num3 + 8f, num, new Color(0f, 0f, 0f, 0.35f), new Color(0f, 0f, 0f, 0f), 0f);
			float num7 = num2;
			for (int i = 0; i < text.Length; i++)
			{
				string text2 = text.Substring(i, 1);
				Color c = Color.Lerp(new Color(1f, 0.45f, 0.5f), new Color(0.7f, 0.85f, 1f), (float)i / (float)(text.Length - 1));
				HiFont.Draw(Art, text2, num7, num3 + Mathf.Sin(unscaledTime * 1.6f + (float)i * 0.5f) * 4f, num, c, new Color(0.12f, 0.08f, 0.2f, 1f), 5f);
				num7 += Art.Measure(text2) * num;
			}
		}
		float num8 = num3 + (float)Art.LineHeight * num + 4f;
		Art.DrawOutlined("Madeline in Pharloom", num2 + 6f, num8, 0.6f, new Color(0.85f, 0.85f, 0.95f, 1f), Ink, 2f);
		GUI.color = lowDetail ? new Color(0.75f, 0.57f, 0.8f, 0.8f) : new Color(1f, 1f, 1f, 0.45f);
		GUI.DrawTexture(new Rect(num2 + 6f, num8 + (float)Art.LineHeight * 0.6f + 10f, 520f, 2f), lowDetail ? Texture2D.whiteTexture : MenuFx.FadeOut);
		float num9 = 470f;
		int num10 = 0;
		foreach (Item item in items)
		{
			float num11 = lowDetail ? 1f : Appear(num10++);
			bool flag = item.Sel != null && item.Sel.gameObject == sel;
			float num12 = lowDetail ? (flag ? 1f : 0f) : OnAmount(item.Sel, flag, Time.unscaledDeltaTime);
			GuiArt.Img img = Art.Image(IconFor(item.Label));
			float num13 = lowDetail ? 0f : num12 * 30f;
			float num14 = lowDetail ? 60f : 60f + num12 * 16f;
			float num15 = 82f;
			float num16 = 150f + num13;
			float num17 = num9 + num15 / 2f;
			if (flag && lowDetail)
			{
				GUI.color = new Color(0.55f, 0.72f, 1f, 0.16f);
				GUI.DrawTexture(new Rect(120f, num9 + 6f, 660f, num15 - 12f), Texture2D.whiteTexture);
			}
			else if (!lowDetail && num12 > 0.01f)
			{
				GUI.color = new Color(1f, 1f, 1f, 0.09f * num12 * num11);
				GUI.DrawTexture(new Rect(num16 - 70f, num9 + 6f, 660f, num15 - 12f), MenuFx.Streak);
				GUI.color = new Color(1f, 0.85f, 0.6f, 0.35f * num12 * num11);
				GUI.DrawTexture(new Rect(num16 - num14, num17 - num14, num14 * 2f, num14 * 2f), MenuFx.Glow);
			}
			if (img != null)
			{
				GUI.color = new Color(1f, 1f, 1f, (flag ? 1f : 0.85f) * num11);
				GUI.DrawTexture(new Rect(num16 - num14 / 2f, num17 - num14 / 2f, num14, num14 * (float)img.H / (float)Math.Max(1, img.W)), img.Tex);
			}
			float num18 = lowDetail ? 0.8f : 0.8f + num12 * 0.15f;
			Color c2 = Color.Lerp(Color.white, Hi(), num12);
			c2.a = num11;
			Art.DrawOutlined(item.Label, num16 + 60f + (1f - num11) * 20f, num17 - (float)Art.LineHeight * num18 / 2f, num18, c2, new Color(0f, 0f, 0f, 0.75f * num11), 3f);
			item.Rect = new Rect(num16 - 50f, num9, 120f + Art.Measure(item.Label) * num18, num15);
			num9 += num15;
		}
		GUI.color = Color.white;
	}

	private void ShowRemap()
	{
		ControllerDetect componentInChildren = screen.GetComponentInChildren<ControllerDetect>(includeInactive: true);
		InputHandler instance = ManagerSingleton<InputHandler>.Instance;
		if (!(componentInChildren == null) && !(componentInChildren.remapButton == null) && !(instance == null) && !(instance.gamepadState.ToString() == "DETACHED") && !componentInChildren.remapButton.gameObject.activeSelf)
		{
			componentInChildren.remapButton.gameObject.SetActive(value: true);
			MenuButtonList menuButtonList = AccessTools.Field(typeof(ControllerDetect), "menuButtonList")?.GetValue(componentInChildren) as MenuButtonList;
			if (menuButtonList != null)
			{
				menuButtonList.SetupActive();
			}
		}
	}

	private static bool IsMap(Selectable s)
	{
		if (!(s is MappableControllerButton))
		{
			return s is MappableKey;
		}
		return true;
	}

	private float LocalX(Transform t)
	{
		float num = 0f;
		while (t != null && t != screen.transform)
		{
			num += t.localPosition.x;
			t = t.parent;
		}
		return num;
	}

	private float Appear(int i)
	{
		if (AssistMode.LowDetailMenus)
		{
			return 1f;
		}
		float num = Mathf.Clamp01((Time.unscaledTime - screenShownAt - (float)i * 0.03f) / 0.18f);
		return Mathf.Min(1f - (1f - num) * (1f - num), leaveFade);
	}

	private float OnAmount(Selectable s, bool on, float dt)
	{
		if (AssistMode.LowDetailMenus)
		{
			return on ? 1f : 0f;
		}
		if (s == null)
		{
			return 0f;
		}
		onAmt.TryGetValue(s, out var value);
		value = Mathf.MoveTowards(value, on ? 1f : 0f, dt / 0.12f);
		onAmt[s] = value;
		return value * value * (3f - 2f * value);
	}

	private static string IconFor(string label)
	{
		string text = label.ToLowerInvariant();
		if (text.Contains("option") || text.Contains("setting"))
		{
			return "menu/options";
		}
		if (text.Contains("quit") || text.Contains("exit"))
		{
			return "menu/exit";
		}
		if (text.Contains("achiev"))
		{
			return "menu/journal";
		}
		if (text.Contains("extra") || text.Contains("credit"))
		{
			return "menu/credits";
		}
		if (text.Contains("continue") || text.Contains("resume"))
		{
			return "menu/play";
		}
		return "menu/start";
	}

	// the pause menu's side card (user 2026-10-17): where you are, and this save's deaths, time and golden berries
	private void DrawPauseInfo(float sw)
	{
		GameManager gm = GameManager.SilentInstance;
		if (gm == null || OverlayOpen)
		{
			return;
		}
		Rect card = new Rect(60f, 1080f - 440f - 110f, 440f, 440f);   // bottom left, clear of the menu
		GUI.color = new Color(0.05f, 0.045f, 0.09f, 0.85f);
		GUI.DrawTexture(card, Texture2D.whiteTexture);
		GUI.color = new Color(0.36f, 0.64f, 0.9f, 1f);
		GUI.DrawTexture(new Rect(card.x, card.y, card.width, 5f), Texture2D.whiteTexture);
		string area = BossCard.CurrentAreaName ?? "Pharloom";
		float s = 0.7f;
		while (s > 0.35f && Art.Measure(area) * s > card.width - 60f)
		{
			s -= 0.03f;
		}
		HiFont.Draw(Art, area, card.x + 30f, card.y + 26f, s, Color.white, Color.black, 3f);
		float y = card.y + 40f + (float)Art.LineHeight * s;
		Art.DrawOutlined("This save", card.x + 30f, y, 0.4f, new Color(0.65f, 0.65f, 0.72f), Color.black, 2f);
		y += 46f;
		// how this file plays, and deaths in this room too (user 2026-10-17)
		AssistMode.ProfileOf(gm.profileID, out string modeName, out string diffName);
		string[] labels = new string[5] { "Mode", "Deaths", "This room", "Time", "Golden berries" };
		string[] values = new string[5]
		{
			modeName,
			CelesteFileSelect.Deaths(gm.profileID).ToString(),
			DeathCounter.RoomDeaths.ToString(),
			BossCard.FormatTime(gm.PlayTime),
			BossCard.GoldensOnSlot(gm.profileID).ToString()
		};
		string[] icons = new string[5] { "journal/clear", "collectables/skullBlue", "journal/skullBlue", "journal/time", "collectables/goldberry" };
		for (int i = 0; i < 5; i++)
		{
			GuiArt.Img ic = Art.Image(icons[i]) ?? (i == 0 ? Art.Skull : null);
			if (ic != null)
			{
				GuiArt.Draw(ic, new Rect(card.x + 30f, y - 4f, 40f, 40f), Color.white);
			}
			Art.DrawOutlined(labels[i], card.x + 84f, y, 0.5f, new Color(0.85f, 0.85f, 0.9f), Color.black, 2f);
			Art.DrawOutlined(values[i], card.xMax - 30f - Art.Measure(values[i]) * 0.5f, y, 0.5f, Color.white, Color.black, 2f);
			y += 56f;
		}
		// rematch the last boss straight from here (user 2026-10-18): click it, or press R
		pauseRematchRect = default(Rect);
		string lf = BossRematch.LastFlag;
		if (lf != null && !BossRematch.Active)
		{
			string label = "Rematch " + (BossRematch.TitleOf(lf) ?? "last boss") + "   R";
			float ls = 0.45f;
			Rect rb = new Rect(card.x, card.yMax + 14f, Math.Max(card.width, Art.Measure(label) * ls + 50f), (float)Art.LineHeight * ls + 22f);
			bool hover = rb.Contains(MenuMouse.Gui());
			GUI.color = hover ? new Color(0.85f, 0.25f, 0.35f, 1f) : new Color(0.6f, 0.16f, 0.26f, 0.95f);
			GUI.DrawTexture(rb, Texture2D.whiteTexture);
			Art.DrawOutlined(label, rb.x + 25f, rb.y + 11f, ls, Color.white, Color.black, 2f);
			pauseRematchRect = rb;
		}
		GUI.color = Color.white;
	}

	public static Rect pauseRematchRect;

	private bool QuitPrompt
	{
		get
		{
			UIManager ui = UIManager.instance;
			return kind == "prompt" && ui != null && ((ui.quitGamePrompt != null && screen == ui.quitGamePrompt.ScreenCanvasGroup) || (ui.returnMainMenuPrompt != null && screen == ui.returnMainMenuPrompt.ScreenCanvasGroup));
		}
	}

	private void DrawList(float sw, GameObject sel)
	{
		UIManager instance = UIManager.instance;
		bool flag = instance != null && ((instance.keyboardMenuScreen != null && screen == instance.keyboardMenuScreen.ScreenCanvasGroup) || (instance.remapGamepadMenuScreen != null && screen == instance.remapGamepadMenuScreen.ScreenCanvasGroup) || (instance.gamepadMenuScreen != null && screen == instance.gamepadMenuScreen.ScreenCanvasGroup));
		float num = sw / 2f;
		float num2 = (flag ? 640f : (kind == "pause" ? 480f : Math.Min(1100f, sw - 200f)));   // pause: a narrow bar that stays clear of the info card
		float num3 = ((kind == "prompt") ? 330f : ((kind == "pause") ? 380f : 110f));
		if (kind == "pause")
		{
			heading = "";
		}
		if (flag && heading.Length > 0 && !AssistMode.LowDetailMenus)
		{
			// the binding pages in the Settings screen's look (user 2026-10-17): a dark card with Controls' green stripe,
			// the page name in its corner, rows below
			Color green = new Color(0.42f, 0.77f, 0.54f);
			float cw = Math.Min(1560f, sw - 120f);
			Rect card = new Rect(sw / 2f - cw / 2f, 60f, cw, 900f);
			GUI.color = new Color(0.05f, 0.045f, 0.09f, 0.92f);
			GUI.DrawTexture(card, Texture2D.whiteTexture);
			GUI.color = green;
			GUI.DrawTexture(new Rect(card.x, card.y, card.width, 6f), Texture2D.whiteTexture);
			GUI.color = new Color(green.r, green.g, green.b, 0.18f);
			GUI.DrawTexture(new Rect(card.x, card.y + 6f, card.width, 90f), MenuFx.FadeOut);
			HiFont.Draw(Art, heading.ToUpperInvariant(), card.x + 40f, card.y + 26f, 0.75f, Color.white, Color.black, 3f);
			string how = "Pick an action, press Confirm, then press the new " + (heading.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0 ? "key." : "button.") + "   Back returns to Settings.";
			Art.DrawOutlined(how, card.xMax - 40f - Art.Measure(how) * 0.4f, card.y + 42f, 0.4f, new Color(0.75f, 0.75f, 0.8f, 1f), Color.black, 2f);
			num3 = card.y + 130f;
		}
		else if (heading.Length > 0)
		{
			float num4 = 1.15f;
			while (num4 > 0.5f && Art.Measure(heading) * num4 > sw - 200f)
			{
				num4 -= 0.05f;
			}
			string text = heading;
			float num5 = num - Art.Measure(text) * num4 / 2f;
			if (AssistMode.LowDetailMenus)
			{
				Art.DrawOutlined(text, num5, num3, num4, new Color(0.78f, 0.86f, 1f), new Color(0.08f, 0.06f, 0.16f, 0.85f), 2f);
			}
			else
			{
				for (int i = 0; i < text.Length; i++)
				{
					string text2 = text.Substring(i, 1);
					Color c = Color.Lerp(new Color(0.78f, 0.66f, 0.85f), new Color(0.55f, 0.66f, 0.92f), (text.Length > 1) ? ((float)i / (float)(text.Length - 1)) : 0f);
					HiFont.Draw(Art, text2, num5, num3, num4, c, new Color(0.08f, 0.06f, 0.16f, 0.85f), 4f);
					num5 += Art.Measure(text2) * num4;
				}
			}
			GUI.color = AssistMode.LowDetailMenus ? new Color(0.73f, 0.62f, 0.84f, 0.85f) : new Color(0.8f, 0.78f, 1f, 0.5f);
			GUI.DrawTexture(new Rect(num - 340f, num3 + (float)Art.LineHeight * num4 + 10f, 680f, 2f), AssistMode.LowDetailMenus ? Texture2D.whiteTexture : MenuFx.FadeBoth);
			num3 += (float)Art.LineHeight * num4 + 36f;
		}
		float num6 = ((kind == "pause") ? 0.95f : 0.8f);
		float num7 = (float)Art.LineHeight * num6 + 22f;
		float num8 = 930f - num3;
		if ((float)items.Count * num7 < num8)
		{
			num3 += (num8 - (float)items.Count * num7) * 0.35f;
		}
		num8 = 930f - num3;
		if ((float)items.Count * num7 > num8)
		{
			num6 = Math.Max(0.45f, num6 * num8 / ((float)items.Count * num7));
			num7 = (float)Art.LineHeight * num6 + 12f;
		}
		int count = items.Count;
		int[] array = new int[count];
		int[] colOf = new int[count];
		bool num9 = flag && items.Exists((Item item2) => IsMap(item2.Sel));
		int num10 = 0;
		if (num9)
		{
			List<float> list = new List<float>();
			for (int num11 = 0; num11 < count; num11++)
			{
				if (IsMap(items[num11].Sel))
				{
					list.Add(LocalX(items[num11].Sel.transform));
				}
			}
			list.Sort();
			float num12 = ((list.Count > 0) ? ((list[0] + list[list.Count - 1]) / 2f) : 0f);
			int val = 0;
			int val2 = 0;
			for (int num13 = 0; num13 < count; num13++)
			{
				if (IsMap(items[num13].Sel))
				{
					bool flag2 = LocalX(items[num13].Sel.transform) > num12;
					colOf[num13] = (flag2 ? 1 : 0);
					array[num13] = (flag2 ? val2++ : val++);
				}
			}
			num10 = Math.Max(val, val2);
			for (int num14 = 0; num14 < count; num14++)
			{
				if (!IsMap(items[num14].Sel))
				{
					colOf[num14] = -1;
					array[num14] = num10++;
				}
			}
			num2 = Math.Min(1400f, sw - 160f);
		}
		else
		{
			for (int num15 = 0; num15 < count; num15++)
			{
				array[num15] = num15;
				colOf[num15] = -1;
			}
			num10 = count;
		}
		if ((float)num10 * num7 > num8)
		{
			num6 = Math.Max(0.45f, num6 * num8 / ((float)num10 * num7));
			num7 = (float)Art.LineHeight * num6 + 12f;
		}
		// binding pages (user 2026-10-17: "looks off"): roomier rows that use the card's height, and the page's
		// buttons (Reset Defaults, Back) side by side underneath instead of full-width bars
		int mapRows = 0;
		List<int> pageButtons = new List<int>();
		bool bindLook = flag && !AssistMode.LowDetailMenus;
		if (bindLook)
		{
			for (int k = 0; k < count; k++)
			{
				if (colOf[k] >= 0)
				{
					mapRows = Math.Max(mapRows, array[k] + 1);
				}
				else
				{
					pageButtons.Add(k);
				}
			}
			num6 = 0.56f;
			num7 = Mathf.Clamp((num8 - 90f) / Math.Max(1, mapRows), 44f, 66f);
			num10 = mapRows;
		}
		int num16 = items.FindIndex((Item item2) => item2.Sel != null && item2.Sel.gameObject == sel);
		float num17 = (float)num10 * num7;
		float b = 0f;
		if (num17 > num8 && num16 >= 0)
		{
			b = Mathf.Clamp((float)array[num16] * num7 - num8 / 2f, 0f, num17 - num8);
		}
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		float t = AssistMode.LowDetailMenus ? 1f : 1f - Mathf.Exp((0f - unscaledDeltaTime) * 16f);
		smoothScroll = Mathf.Lerp(smoothScroll, b, t);
		b = smoothScroll;
		float baseCx = num;
		float baseW = num2;
		Func<int, float> func = (int num33) => (colOf[num33] >= 0) ? (baseCx + ((colOf[num33] == 0) ? (-1f) : 1f) * baseW * 0.26f) : baseCx;
		Func<int, float> func2 = (int num33) => (colOf[num33] >= 0) ? (baseW * 0.46f) : baseW;
		if (num16 >= 0)
		{
			float num18 = num3 + (float)array[num16] * num7 - b;
			float num19 = func(num16) - func2(num16) / 2f - 20f;
			float num20 = func2(num16) + 40f;
			barY = ((barY < -500f) ? num18 : Mathf.Lerp(barY, num18, t));
			barX = ((barX < -500f) ? num19 : Mathf.Lerp(barX, num19, t));
			barW = ((barW < 1f) ? num20 : Mathf.Lerp(barW, num20, t));
			float num21 = AssistMode.LowDetailMenus ? 1f : Appear(num16);
			if (flag && !AssistMode.LowDetailMenus)
			{
				// the chosen binding's tile shows it (drawn with the rows)
			}
			else
			{
			GUI.color = AssistMode.LowDetailMenus ? new Color(0.3f, 0.48f, 0.78f, 0.24f) : new Color(0.55f, 0.5f, 0.95f, 0.22f * num21);
			GUI.DrawTexture(new Rect(barX - (AssistMode.LowDetailMenus ? 0f : 60f), barY - 6f, barW + (AssistMode.LowDetailMenus ? 0f : 120f), num7), AssistMode.LowDetailMenus ? Texture2D.whiteTexture : MenuFx.FadeBoth);
			}
			if (!AssistMode.LowDetailMenus && !flag)
			{
				GUI.color = new Color(1f, 1f, 1f, 0.35f * num21);
				GUI.DrawTexture(new Rect(barX, barY - 6f, barW, 1.5f), MenuFx.FadeBoth);
				GUI.DrawTexture(new Rect(barX, barY - 6f + num7 - 1.5f, barW, 1.5f), MenuFx.FadeBoth);
			}
			GUI.color = Color.white;
		}
		for (int num22 = 0; num22 < items.Count; num22++)
		{
			Item item = items[num22];
			float num23 = AssistMode.LowDetailMenus ? 1f : Appear(num22);
			float num24 = num3 + (float)array[num22] * num7 - b;
			num = func(num22);
			num2 = func2(num22);
			item.Rect = new Rect(num - num2 / 2f - 20f, num24 - 6f, num2 + 40f, num7);
			if (bindLook && colOf[num22] < 0)
			{
				// one of the page's buttons: in a centred row under the bindings
				int k2 = pageButtons.IndexOf(num22);
				float bw = 320f;
				float tw = pageButtons.Count * bw + (pageButtons.Count - 1) * 30f;
				Rect pb = new Rect(sw / 2f - tw / 2f + k2 * (bw + 30f), num3 + mapRows * num7 + 24f - b, bw, 54f);
				item.Rect = pb;
				bool on = item.Sel != null && item.Sel.gameObject == sel;
				GUI.color = new Color(0f, 0f, 0f, 0.35f * num23);
				GUI.DrawTexture(new Rect(pb.x + 3f, pb.y + 4f, pb.width, pb.height), Texture2D.whiteTexture);
				GUI.color = on ? new Color(0.42f, 0.77f, 0.54f, num23) : new Color(1f, 1f, 1f, 0.12f * num23);
				GUI.DrawTexture(pb, Texture2D.whiteTexture);
				float ls2 = 0.5f;
				Art.DrawOutlined(item.Label, pb.center.x - Art.Measure(item.Label) * ls2 / 2f, pb.y + (pb.height - (float)Art.LineHeight * ls2) / 2f, ls2, new Color(1f, 1f, 1f, num23), new Color(0f, 0f, 0f, 0.75f * num23), 2f);
				GUI.color = Color.white;
				continue;
			}
			if (num24 < num3 - 4f || num24 + num7 > 940f)
			{
				item.Rect = default(Rect);
				continue;
			}
			bool flag3 = item.Sel != null && item.Sel.gameObject == sel;
			float num25 = OnAmount(item.Sel, flag3, unscaledDeltaTime);
			if (flag && !AssistMode.LowDetailMenus)
			{
				// every binding on its own tile; the chosen one glows green; one waiting for a key pulses
				bool waiting = item.Value.IndexOf("press", StringComparison.OrdinalIgnoreCase) >= 0 || item.Value == "...";
				Rect tile = new Rect(item.Rect.x, item.Rect.y + 2f, item.Rect.width, item.Rect.height - 6f);
				float pulse = waiting ? (0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 8f)) : 1f;
				GUI.color = flag3 ? new Color(0.42f, 0.77f, 0.54f, 0.16f * num23 * pulse) : new Color(1f, 1f, 1f, 0.045f * num23);
				GUI.DrawTexture(tile, Texture2D.whiteTexture);
				Color edge = flag3 ? new Color(0.42f, 0.77f, 0.54f, num23 * pulse) : new Color(1f, 1f, 1f, 0.08f * num23);
				GUI.color = edge;
				GUI.DrawTexture(new Rect(tile.x, tile.y, tile.width, 2f), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(tile.x, tile.yMax - 2f, tile.width, 2f), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(tile.x, tile.y, 2f, tile.height), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(tile.xMax - 2f, tile.y, 2f, tile.height), Texture2D.whiteTexture);
				GUI.color = Color.white;
			}
			num24 += AssistMode.LowDetailMenus ? 0f : (1f - num23) * 24f;
			float num26 = AssistMode.LowDetailMenus ? 0f : (0f - num25) * (2f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 2f);
			Color color = AssistMode.LowDetailMenus ? (flag3 ? new Color(0.78f, 0.86f, 1f) : Color.white) : Color.Lerp(Color.white, Hi(), num25);
			color.a = num23;
			Color outline = new Color(0f, 0f, 0f, 0.75f * num23);
			bool flag4 = item.Value.Length == 0 && item.Icon != null && item.Icon.texture != null;
			if (item.Value.Length == 0 && !flag4)
			{
				Art.DrawOutlined(item.Label, num - Art.Measure(item.Label) * num6 / 2f, num24 + num26, num6, color, outline, 2.5f);
				continue;
			}
			if (flag4 && bindLook && item.Sel is MappableKey)
			{
				// a mouse button: Silksong shows a tiny mouse picture; say which button instead, in a value box
				string sn = item.Icon.name.ToLowerInvariant();
				string mv = sn.Contains("right") ? "Right Click" : (sn.Contains("middle") ? "Middle Click" : (sn.Contains("left") ? "Left Click" : "Mouse"));
				item.Value = mv;
				flag4 = false;
			}
			if (flag4)
			{
				Art.DrawOutlined(item.Label, num - num2 / 2f, num24 + num26, num6, color, outline, 2.5f);
				Rect textureRect = item.Icon.textureRect;
				Texture2D texture = item.Icon.texture;
				float num27 = num7 - 8f;
				float num28 = num27 * textureRect.width / Math.Max(1f, textureRect.height);
				GUI.color = new Color(1f, 1f, 1f, num23);
				GUI.DrawTextureWithTexCoords(new Rect(num + num2 / 2f - num28, num24 + num26 - 2f, num28, num27), texture, new Rect(textureRect.x / (float)texture.width, textureRect.y / (float)texture.height, textureRect.width / (float)texture.width, textureRect.height / (float)texture.height));
				GUI.color = Color.white;
				continue;
			}
			string value = item.Value;
			float num29 = Art.Measure(value) * num6;
			float num30 = num + num2 / 2f - num29;
			float num31 = num6;
			float num32 = num2 - num29 - 90f;
			while (num31 > 0.35f && Art.Measure(item.Label) * num31 > num32)
			{
				num31 -= 0.02f;
			}
			Art.DrawOutlined(item.Label, num - num2 / 2f, num24 + num26 + (num6 - num31) * (float)Art.LineHeight / 2f + (bindLook ? (num7 - 12f - (float)Art.LineHeight * num6) / 2f : 0f), num31, color, outline, 2.5f);
			Color c2 = (flag3 ? color : new Color(0.8f, 0.8f, 0.85f, num23));
			if (flag && !AssistMode.LowDetailMenus)
			{
				// a key as a value box, like the Settings screen's
				float ks = Math.Min(0.48f, num6 * 0.8f);
				float kw = Art.Measure(value) * ks + 36f;
				Rect chip = new Rect(num + num2 / 2f - kw, num24 + num26 + (num7 - 12f - 40f) / 2f, kw, 40f);
				GUI.color = flag3 ? new Color(0.42f, 0.77f, 0.54f, 0.85f * num23) : new Color(1f, 1f, 1f, 0.1f * num23);
				GUI.DrawTexture(chip, Texture2D.whiteTexture);
				Art.DrawOutlined(value, chip.x + 18f, chip.y + (chip.height - (float)Art.LineHeight * ks) / 2f, ks, new Color(1f, 1f, 1f, num23), outline, 2f);
				GUI.color = Color.white;
			}
			else
			{
				Art.DrawOutlined(value, num30, num24 + num26, num6, c2, outline, 2.5f);
			}
			if (item.Arrows && flag3)
			{
				Art.DrawOutlined("<", num30 - Art.Measure("<") * num6 - 18f, num24 + num26, num6, color, outline, 2.5f);
				Art.DrawOutlined(">", num + num2 / 2f + 14f, num24 + num26, num6, color, outline, 2.5f);
			}
		}
		if (b > 1f)
		{
			Art.DrawOutlined("^", num - 8f, num3 - 30f, 0.5f, Color.white, Ink, 2f);
		}
		if (b < num17 - num8 - 1f && num17 > num8)
		{
			Art.DrawOutlined("v", num - 8f, 940f, 0.5f, Color.white, Ink, 2f);
		}
	}

	private static void Pill(string key, float right, float top, float h)
	{
		if (dot == null)
		{
			dot = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Bilinear
			};
			Color32[] array = new Color32[4096];
			for (int i = 0; i < 64; i++)
			{
				for (int j = 0; j < 64; j++)
				{
					float num = Mathf.Sqrt(((float)j - 31.5f) * ((float)j - 31.5f) + ((float)i - 31.5f) * ((float)i - 31.5f));
					array[i * 64 + j] = new Color(1f, 1f, 1f, Mathf.Clamp01(31.5f - num));
				}
			}
			dot.SetPixels32(array);
			dot.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		}
		string text = key.ToUpperInvariant();
		float num2 = Mathf.Min(0.5f, (h - 10f) / (float)Art.LineHeight * 1.1f);
		if (text.Length > 6)
		{
			num2 *= 0.75f;
		}
		float num3 = Art.Measure(text) * num2;
		float num4 = Math.Max(h, num3 + h * 0.6f);
		float num5 = right - num4;
		GUI.color = Color.white;
		GUI.DrawTexture(new Rect(num5, top, h, h), dot);
		GUI.DrawTexture(new Rect(num5 + num4 - h, top, h, h), dot);
		if (num4 > h)
		{
			GUI.DrawTexture(new Rect(num5 + h / 2f, top, num4 - h, h), Texture2D.whiteTexture);
		}
		Art.DrawLine(text, num5 + (num4 - num3) / 2f, top + (h - (float)Art.LineHeight * num2) / 2f, num2, new Color(0.08f, 0.07f, 0.12f));
	}

	private void DrawHints(float sw)
	{
		bool padActive = MenuMouse.PadActive;
		string[] array = ((!(kind == "title")) ? ((!padActive) ? new string[2] { "Confirm  Enter", "Back  Esc" } : new string[2] { "Confirm  Cross", "Back  Circle" }) : ((!padActive) ? new string[1] { "Confirm  Enter" } : new string[1] { "Confirm  Cross" }));
		float num = 0.5f;
		float num2 = sw - 50f;
		for (int num3 = array.Length - 1; num3 >= 0; num3--)
		{
			num2 -= Art.Measure(array[num3]) * num;
			Art.DrawOutlined(array[num3], num2, 1010f, num, Color.white, new Color(0f, 0f, 0f, 0.6f), 2f);
			num2 -= 50f;
		}
	}
}
