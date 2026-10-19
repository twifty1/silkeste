using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GlobalEnums;
using MadelineInPharloom.Sheets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MadelineInPharloom;

public class SpeedrunTimer : MonoBehaviour
{
	public static GuiArt Art;

	public static string DataDir;

	private float modeShown;

	private readonly Dictionary<int, float> otherSaves = new Dictionary<int, float>();

	private string room;

	private float roomTime;

	private readonly Dictionary<int, float> times = new Dictionary<int, float>();

	private bool timesLoaded;

	private int slot = -1;

	private float fileTime;

	private float saveIn = 5f;

	private bool loggedError;

	private float vis = 1f;

	private static int mode => AssistMode.TimerMode;

	private bool on => mode != 0;

	private string SaveFile => Path.Combine(DataDir, "timer.cfg");

	private string TimesFile => Path.Combine(DataDir, "timer_times.cfg");

	private float AllSaves()
	{
		float num = fileTime;
		foreach (KeyValuePair<int, float> otherSafe in otherSaves)
		{
			if (otherSafe.Key != slot)
			{
				num += otherSafe.Value;
			}
		}
		return num;
	}

	private void ReadOtherSaves(GameManager gm)
	{
		otherSaves.Clear();
		for (int i = 1; (float)i <= 4f; i++)
		{
			if (i == gm.profileID)
			{
				continue;
			}
			int sl = i;
			try
			{
				gm.GetSaveStatsForSlot(sl, delegate(SaveStats stats, string error)
				{
					if (stats != null)
					{
						otherSaves[sl] = stats.PlayTime;
					}
				});
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Timer: save slot " + sl + ": " + ex.Message);
			}
		}
	}

	private void LoadTimes(int profile, float playTime)
	{
		if (!timesLoaded)
		{
			timesLoaded = true;
			try
			{
				if (File.Exists(TimesFile))
				{
					string[] array = File.ReadAllLines(TimesFile);
					for (int i = 0; i < array.Length; i++)
					{
						string[] array2 = array[i].Split(new char[1] { '=' });
						if (array2.Length == 2 && int.TryParse(array2[0], out var result) && float.TryParse(array2[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
						{
							times[result] = result2;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Timer: " + ex.Message);
			}
		}
		if (profile != slot)
		{
			if (slot >= 0)
			{
				times[slot] = fileTime;
				SaveTimes();
			}
			slot = profile;
			GameManager silentInstance = GameManager.SilentInstance;
			if (silentInstance != null)
			{
				ReadOtherSaves(silentInstance);
			}
			fileTime = ((times.TryGetValue(slot, out var value) && value <= playTime + 1f) ? value : playTime);
		}
	}

	private void OnApplicationQuit()
	{
		SaveTimes();
	}

	private void SaveTimes()
	{
		if (slot < 0)
		{
			return;
		}
		times[slot] = fileTime;
		try
		{
			List<string> list = new List<string>();
			foreach (KeyValuePair<int, float> time in times)
			{
				list.Add(time.Key + "=" + time.Value.ToString("R", CultureInfo.InvariantCulture));
			}
			Directory.CreateDirectory(DataDir);
			File.WriteAllLines(TimesFile, list.ToArray());
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Timer: " + ex.Message);
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
				Plugin.Log.LogError("SpeedrunTimer: " + ex);
			}
		}
	}

	private float skipWait;

	// Auto-Skip Cutscenes: once Silksong offers a skip, take it, as a skip press would.
	private void AutoSkip()
	{
		if ((skipWait -= Time.unscaledDeltaTime) > 0f || !AssistMode.AutoSkip)
		{
			return;
		}
		GameManager silentInstance = GameManager.SilentInstance;
		InputHandler instance = ManagerSingleton<InputHandler>.Instance;
		if (silentInstance == null || instance == null || silentInstance.GameState != GameState.CUTSCENE || instance.skippingCutscene || silentInstance.isPaused)
		{
			return;
		}
		if (instance.SkipMode == SkipPromptMode.SKIP_INSTANT || instance.SkipMode == SkipPromptMode.SKIP_PROMPT)
		{
			instance.skippingCutscene = true;
			silentInstance.SkipCutscene();
			skipWait = 0.5f;
		}
	}

	private void Tick()
	{
		try
		{
			AutoSkip();
		}
		catch (Exception ex)
		{
			if (!loggedError)
			{
				loggedError = true;
				Plugin.Log.LogError("Auto-skip: " + ex);
			}
		}
		GameManager silentInstance = GameManager.SilentInstance;
		HeroController silentInstance2 = HeroController.UnsafeInstance;
		if (!Plugin.Ready || silentInstance == null || silentInstance2 == null || silentInstance.IsMenuScene())
		{
			if (slot >= 0)
			{
				SaveTimes();
				slot = -1;
			}
			return;
		}
		_ = Keyboard.current;
		string text = SceneManager.GetActiveScene().name;
		if (text != room)
		{
			room = text;
			roomTime = 0f;
		}
		LoadTimes(silentInstance.profileID, silentInstance.PlayTime);
		if (modeShown > 0f)
		{
			modeShown -= Time.unscaledDeltaTime;
		}
		if (!text.StartsWith("Cinematic_") && !text.StartsWith("Opening_") && !IsLoading(silentInstance, silentInstance2))
		{
			roomTime += Time.unscaledDeltaTime;
			fileTime += Time.unscaledDeltaTime;
			if ((saveIn -= Time.unscaledDeltaTime) <= 0f)
			{
				saveIn = 5f;
				SaveTimes();
			}
		}
	}

	private static readonly System.Reflection.FieldInfo SceneLoadF = HarmonyLib.AccessTools.Field(typeof(GameManager), "sceneLoad");

	// Load-removed time, as Silksong's speedrun.com boards time runs: the community autosplitter's
	// load removal (AlexKnauth/silksong-autosplit-wasm). Cutscenes, the inventory and Madeline's
	// own animations all count; loads, room transitions and the pause menu don't.
	private static bool IsLoading(GameManager gm, HeroController hero)
	{
		GameState gameState = gm.GameState;
		UIManager instance = UIManager.instance;
		UIState uIState = ((instance != null) ? instance.uiState : UIState.PLAYING);
		InputHandler instance2 = ManagerSingleton<InputHandler>.Instance;
		bool flag = instance2 == null || instance2.acceptingInput;
		bool flag2 = PlayerData.instance != null && PlayerData.instance.isInventoryOpen;
		string text = gm.sceneName ?? "";
		string text2 = gm.nextSceneName ?? "";
		SceneLoad sceneLoad = ((SceneLoadF != null) ? (SceneLoadF.GetValue(gm) as SceneLoad) : null);
		bool flag3 = text == "Quit_To_Menu" || (text != "Menu_Title" && (text2.Length == 0 || text2 == "Menu_Title"));
		if ((gameState == GameState.PLAYING || gameState == GameState.ENTERING_LEVEL) && uIState != UIState.PLAYING)
		{
			return true;
		}
		if (gameState != GameState.PLAYING && gameState != GameState.CUTSCENE && uIState != UIState.CUTSCENE && !flag)
		{
			return true;
		}
		if ((gameState == GameState.EXITING_LEVEL && uIState != UIState.CUTSCENE && (sceneLoad == null || sceneLoad.IsActivationAllowed) && !flag2) || gameState == GameState.LOADING)
		{
			return true;
		}
		if (hero.transitionState == HeroTransitionState.WAITING_TO_ENTER_LEVEL && !flag2)
		{
			return true;
		}
		if (uIState != UIState.PLAYING && (flag3 || (uIState != UIState.PAUSED && uIState != UIState.CUTSCENE && text2.Length != 0)) && text2 != text)
		{
			return true;
		}
		return false;
	}

	private void OnGUI()
	{
		if (TheoCamera.Hiding || TheoCamera.HidingHud || (!on && modeShown <= 0f) || Art == null || Event.current.type != EventType.Repaint)
		{
			return;
		}
		bool flag = true;
		try
		{
			flag = GameCameras.instance == null || GameCameras.instance.IsHudVisible;
		}
		catch (Exception)
		{
		}
		bool flag2 = false;
		try
		{
			HeroController silentInstance = HeroController.UnsafeInstance;
			GameManager silentInstance2 = GameManager.SilentInstance;
			flag2 = (silentInstance != null && silentInstance.cState.transitioning) || (silentInstance2 != null && silentInstance2.IsInSceneTransition);
		}
		catch (Exception)
		{
		}
		float num = ((flag && !CelesteTextbox.Showing && !flag2) ? 1f : 0f);
		vis = Mathf.MoveTowards(vis, num, Time.unscaledDeltaTime / ((num > vis) ? 0.35f : 0.2f));
		if (vis <= 0f && modeShown <= 0f)
		{
			return;
		}
		GameManager silentInstance3 = GameManager.SilentInstance;
		if (!Plugin.Ready || silentInstance3 == null || HeroController.UnsafeInstance == null || silentInstance3.IsMenuScene() || !PostcardView.TutorialDone || silentInstance3.isPaused || AssistMode.IsOpen || BossCard.RecordsOpen || (PlayerData.instance != null && PlayerData.instance.isInventoryOpen))
		{
			return;
		}
		float num2 = (float)Screen.height / 1080f;
		Matrix4x4 matrix = GUI.matrix;
		GUI.depth = 2;
		float num3 = 1f - (1f - vis) * (1f - vis) * (1f - vis);
		GUI.matrix = Matrix4x4.TRS(new Vector3((0f - (1f - num3)) * 340f * num2, 0f, 0f), Quaternion.identity, new Vector3(num2, num2, 1f));
		float num4 = 225f;
		Color c = new Color(1f, 1f, 1f, num3);
		Color color = new Color(0f, 0f, 0f, num3);
		float num5 = 0.36f;
		if (modeShown > 0f)
		{
			float a = Mathf.Clamp01(modeShown);
			Art.DrawOutlined("Timer: " + AssistMode.TimerModeName, 32f, num4 - 8f - (float)Art.LineHeight * num5, num5, new Color(1f, 1f, 1f, a), new Color(0f, 0f, 0f, a), 2f);
		}
		if (!on)
		{
			GUI.matrix = matrix;
			GUI.color = Color.white;
			return;
		}
		bool num6 = mode == 3 || mode == 5;
		bool flag3 = mode >= 4;
		string line = ((mode == 2) ? BossCard.FormatTime(roomTime) : BossCard.FormatTime(flag3 ? AllSaves() : fileTime));
		string text = (num6 ? BossCard.FormatTime(roomTime) : null);
		float num7 = 0.6f;
		float num8 = num7 * 0.6f;
		GuiArt.Img img = Art.Image("strawberryCountBG");
		float num9 = Math.Max(300f, Art.Measure(line) * num7 + 60f);
		if (img != null)
		{
			GuiArt.Draw(img, new Rect(num9 - (float)img.W, num4, img.W, img.H), c);
			GUI.color = color;
			if (num9 > (float)img.W)
			{
				GUI.DrawTexture(new Rect(0f, num4, num9 - (float)img.W + 1f, img.H), Texture2D.whiteTexture);
			}
			if (text != null)
			{
				GuiArt.Draw(img, new Rect(num9 - (float)img.W, num4 + (float)img.H, (float)img.W * 0.75f, (float)img.H * 0.85f), c);
			}
		}
		Art.DrawOutlined(line, 32f, num4 + 19f - (float)Art.LineHeight * num7 / 2f, num7, c, color, 2f);
		if (text != null)
		{
			Art.DrawOutlined(text, 32f, num4 + 38f + 16f - (float)Art.LineHeight * num8 / 2f, num8, new Color(0.8f, 0.8f, 0.8f, num3), color, 2f);
		}
		int num10 = BossCard.GoldenCount(silentInstance3.profileID);
		GuiArt.Img img2 = BossCard.PixelBerry() ?? Art.Image("collectables/goldberry");
		if (num10 > 0 && img2 != null && AssistMode.TimerBerries)
		{
			float num11 = 22f;
			float num12 = 36f;
			float num13 = num4 - num12 - 2f;
			GuiArt.Draw(img2, new Rect(num11, num13, num12, num12), c);
			string line2 = "x" + num10.ToString(CultureInfo.InvariantCulture);
			Art.DrawOutlined(line2, num11 + num12 + 1f, num13 + num12 / 2f - (float)Art.LineHeight * num8 * 0.8f / 2f, num8 * 0.8f, c, color, 2f);
		}
		GUI.matrix = matrix;
		GUI.color = Color.white;
	}
}
