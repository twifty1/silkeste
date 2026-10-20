using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using InControl;
using MadelineInPharloom.Sheets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MadelineInPharloom;

public class CelesteFileSelect : MonoBehaviour
{
	private class Slot
	{
		public SaveSlotButton Button;

		public float Sel;

		public float ShineT = -1f;

		public Rect Gui;

		public bool Ours;

		public Rect Ticket;
	}

	private static class FileIcons
	{
		private static Texture2D spool;

		private static Texture2D pin;

		private static Texture2D newFile;

		public static Texture2D Spool => spool ?? (spool = Make(delegate(float x, float y)
		{
			float num = Mathf.Sqrt(x * x + y * y);
			if (num > 0.9f)
			{
				return Color.clear;
			}
			Color result = ((num > 0.72f) ? new Color(0.6f, 0.5f, 0.62f) : ((Mathf.Repeat((x + y) * 6f, 1f) < 0.5f) ? new Color(0.92f, 0.86f, 0.96f) : new Color(0.8f, 0.72f, 0.88f)));
			if (num < 0.18f)
			{
				result = new Color(0.6f, 0.5f, 0.62f);
			}
			result.a = Mathf.Clamp01((0.9f - num) * 25f);
			return result;
		}));

		public static Texture2D Pin => pin ?? (pin = Make(delegate(float x, float y)
		{
			float num = Mathf.Sqrt(x * x + (y + 0.3f) * (y + 0.3f));
			float a = ((num < 0.55f && num > 0.22f) ? 1f : 0f);
			if (y > 0.1f && Mathf.Abs(x) < (0.95f - y) * 0.55f)
			{
				a = 1f;
			}
			if (num <= 0.22f)
			{
				a = 0f;
			}
			return new Color(1f, 1f, 1f, a);
		}));

		public static Texture2D NewFile => newFile ?? (newFile = Make(delegate(float x, float y)
		{
			float a = ((Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.85f) < 0.07f) ? 1f : 0f);
			if ((Mathf.Abs(x) < 0.08f && Mathf.Abs(y) < 0.45f) || (Mathf.Abs(y) < 0.08f && Mathf.Abs(x) < 0.45f))
			{
				a = 1f;
			}
			return new Color(1f, 1f, 1f, a);
		}));

		private static Texture2D Make(Func<float, float, Color> f)
		{
			Texture2D texture2D = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp
			};
			Color[] array = new Color[4096];
			for (int i = 0; i < 64; i++)
			{
				for (int j = 0; j < 64; j++)
				{
					Color clear = Color.clear;
					for (int k = 0; k < 4; k++)
					{
						float arg = ((float)j + 0.25f + (float)(k & 1) * 0.5f) / 64f * 2f - 1f;
						float arg2 = ((float)i + 0.25f + (float)(k >> 1) * 0.5f) / 64f * 2f - 1f;
						Color color = f(arg, arg2);
						clear += new Color(color.r * color.a, color.g * color.a, color.b * color.a, color.a);
					}
					clear /= 4f;
					array[(63 - i) * 64 + j] = ((clear.a > 0f) ? new Color(clear.r / clear.a, clear.g / clear.a, clear.b / clear.a, clear.a) : Color.clear);
				}
			}
			texture2D.SetPixels(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			return texture2D;
		}
	}

	private readonly Dictionary<SaveSlotButton, Slot> slots = new Dictionary<SaveSlotButton, Slot>();

	private bool loggedError;

	private bool logged;

	private static readonly SaveSlotButton.SlotState[] Drawn = new SaveSlotButton.SlotState[2]
	{
		SaveSlotButton.SlotState.SavePresent,
		SaveSlotButton.SlotState.EmptySlot
	};

	private float screenA;

	private float findIn;

	/// <summary>The title's Continue asked for this save slot (-1: none).</summary>
	public static int AutoBegin = -1;

	private static float autoBeginAt;

	private static string LastSlotFile => System.IO.Path.Combine(AssistMode.DataDir ?? "", "last_slot.cfg");

	/// <summary>The save slot played last, or -1.</summary>
	public static int LastSlot
	{
		get
		{
			try
			{
				if (System.IO.File.Exists(LastSlotFile) && int.TryParse(System.IO.File.ReadAllText(LastSlotFile).Trim(), out var n))
				{
					return n;
				}
			}
			catch (Exception)
			{
			}
			return -1;
		}
	}

	public static void RememberSlot(int slot)
	{
		try
		{
			System.IO.File.WriteAllText(LastSlotFile, slot.ToString());
		}
		catch (Exception)
		{
		}
	}

	public static void Continue(int slot)
	{
		AutoBegin = slot;
		autoBeginAt = Time.unscaledTime;
	}

	public static bool Showing;

	private bool active;

	private int index;

	private readonly List<Slot> ordered = new List<Slot>();

	private readonly Dictionary<SaveSlotButton, Rect> cardRects = new Dictionary<SaveSlotButton, Rect>();

	private static readonly SaveSlotButton.SlotState[] HandBack = new SaveSlotButton.SlotState[4]
	{
		SaveSlotButton.SlotState.ClearPrompt,
		SaveSlotButton.SlotState.ClearConfirm,
		SaveSlotButton.SlotState.RestoreSave,
		SaveSlotButton.SlotState.BlackThreadInfected
	};

	private bool confirmOpen;

	private int confirmIndex;

	private float confirmT;

	private bool begun;

	private int lastSlot = -1;

	private readonly Rect[] optionRects = new Rect[3];

	private bool eraseOpen;

	private int eraseIndex;

	private float eraseT;

	private float assistShutAt = -10f;

	private readonly Rect[] eraseRects = new Rect[2];

	private static Texture2D sky;

	private static Texture2D flake;

	private static volatile Color[] skyPixels;

	private static bool skyStarted;

	private static volatile bool painting;

	private const int SkyW = 1280;

	private const int SkyH = 720;

	private float cardAlpha = 1f;

	private static Dictionary<int, int> deaths = new Dictionary<int, int>();

	private static float deathsReadAt = -10f;

	private static GuiArt Art => SpeedrunTimer.Art;

	/// <summary>True when this slot holds a started file (not an empty slot).</summary>
	public static bool HasSave(int slot)
	{
		if (slot < 0)
		{
			return false;
		}
		foreach (SaveSlotButton b in UnityEngine.Object.FindObjectsByType<SaveSlotButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
		{
			if (b != null && b.SaveSlotIndex == slot)
			{
				return b.saveFileState != SaveSlotButton.SaveFileStates.Empty;
			}
		}
		return PlayerData.instance != null && GameManager.SilentInstance != null && GameManager.SilentInstance.profileID == slot && !GameManager.SilentInstance.IsMenuScene();
	}

	// any delete (Silkeste's file select or Silksong's own prompt) resets that file: Standard, Normal, no head start
	private static void ClearedPostfix(SaveSlotButton __instance)
	{
		try
		{
			AssistMode.ForgetSlot(__instance.SaveSlotIndex);
			RunLog.Forget(__instance.SaveSlotIndex);
			TechTips.ForgetSlot(__instance.SaveSlotIndex);
			Plugin.Log.LogInfo("File " + __instance.SaveSlotIndex + " deleted: back to Standard");
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("File delete: " + e.Message);
		}
	}

	private void Awake()
	{
		try
		{
			new Harmony("melty.madelineinpharloom.filedelete").Patch(AccessTools.Method(typeof(SaveSlotButton), "ClearSaveFile"), null, new HarmonyMethod(typeof(CelesteFileSelect), "ClearedPostfix"));
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("File delete hook: " + e.Message);
		}
		StartSky();
		try
		{
			MethodInfo methodInfo = AccessTools.Method(typeof(MenuAudioController), "PlaySelect");
			if (methodInfo != null)
			{
				new Harmony("silkeste.fileselectsound").Patch(methodInfo, new HarmonyMethod(typeof(CelesteFileSelect), "QuietSelect"));
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("file select sound patch: " + ex.Message);
		}
	}

	private static bool QuietSelect()
	{
		return !Showing;
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
				Plugin.Log.LogError("CelesteFileSelect: " + ex);
			}
		}
	}

	private void Tick()
	{
		if (Art == null || Art.Image("fileselect/card") == null)
		{
			return;
		}
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		EventSystem current = EventSystem.current;
		if ((findIn -= unscaledDeltaTime) <= 0f)
		{
			findIn = 0.5f;
			SaveSlotButton[] array = UnityEngine.Object.FindObjectsByType<SaveSlotButton>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			foreach (SaveSlotButton saveSlotButton in array)
			{
				if (!slots.ContainsKey(saveSlotButton))
				{
					slots[saveSlotButton] = new Slot
					{
						Button = saveSlotButton
					};
				}
			}
		}
		List<SaveSlotButton> list = new List<SaveSlotButton>();
		ordered.Clear();
		bool flag = false;
		bool flag2 = false;
		foreach (KeyValuePair<SaveSlotButton, Slot> slot2 in slots)
		{
			Slot value = slot2.Value;
			SaveSlotButton key = slot2.Key;
			if (key == null)
			{
				list.Add(slot2.Key);
				continue;
			}
			value.Ours = false;
			if (key.isActiveAndEnabled)
			{
				if (Array.IndexOf(HandBack, key.State) >= 0)
				{
					flag = true;
				}
				else if (key.State != SaveSlotButton.SlotState.Hidden)
				{
					flag2 |= key.myCanvasGroup == null || key.myCanvasGroup.alpha > 0.05f || active;
					value.Ours = true;
					ordered.Add(value);
				}
			}
		}
		foreach (SaveSlotButton item in list)
		{
			slots.Remove(item);
		}
		ordered.Sort((Slot x, Slot y) => x.Button.SaveSlotIndex.CompareTo(y.Button.SaveSlotIndex));
		bool flag3 = ordered.Count > 0 && flag2 && !flag && !begun;
		if (flag3 && !active)
		{
			screenA = 1f;
			confirmOpen = false;
			confirmT = 0f;
		}
		active = flag3;
		Showing = active;
		if (!active)
		{
			screenA = Mathf.MoveTowards(screenA, 0f, unscaledDeltaTime / 0.2f);
		}
		if (ordered.Count == 0)
		{
			begun = false;
		}
		foreach (KeyValuePair<SaveSlotButton, Slot> slot3 in slots)
		{
			if (slot3.Key != null && slot3.Key.myCanvasGroup != null)
			{
				slot3.Key.myCanvasGroup.blocksRaycasts = !active;
			}
		}
		if (!active)
		{
			return;
		}
		if (!logged)
		{
			logged = true;
			Plugin.Log.LogInfo("Celeste file select: the save files as Celeste's");
		}
		if (current != null && current.currentSelectedGameObject != null && current.currentSelectedGameObject.GetComponentInParent<SaveSlotButton>() != null)
		{
			current.SetSelectedGameObject(null);
		}
		if (lastSlot >= 0)
		{
			for (int num = 0; num < ordered.Count; num++)
			{
				if (ordered[num].Button.SaveSlotIndex == lastSlot)
				{
					index = num;
					lastSlot = -1;
				}
			}
		}
		index = Mathf.Clamp(index, 0, ordered.Count - 1);
		// the title's Continue: straight into that save, as if Begin was pressed on it (user 2026-10-17)
		if (AutoBegin >= 0 && !begun)
		{
			Slot auto = ordered.Find((Slot o) => o.Button != null && o.Button.SaveSlotIndex == AutoBegin);
			if (auto != null && auto.Button.State == SaveSlotButton.SlotState.SavePresent && current != null)
			{
				AutoBegin = -1;
				begun = true;
				Sfx.Play(Sounds.MenuButtonSelect.Id);
				current.SetSelectedGameObject(auto.Button.gameObject);
				ExecuteEvents.Execute(auto.Button.gameObject, new BaseEventData(current), ExecuteEvents.submitHandler);
				return;
			}
			if (Time.unscaledTime - autoBeginAt > 6f)
			{
				AutoBegin = -1;   // that save isn't there any more: just show the files
			}
		}
		Keyboard current5 = Keyboard.current;
		InputHandler instance = ManagerSingleton<InputHandler>.Instance;
		HeroActions heroActions = ((instance != null) ? instance.inputActions : null);
		InControl.InputDevice inputDevice = null;
		try
		{
			inputDevice = InputManager.ActiveDevice;
		}
		catch (Exception)
		{
		}
		bool flag4 = (current5 != null && (current5.upArrowKey.wasPressedThisFrame || current5.wKey.wasPressedThisFrame)) || (heroActions?.Up.WasPressed ?? false);
		bool flag5 = (current5 != null && (current5.downArrowKey.wasPressedThisFrame || current5.sKey.wasPressedThisFrame)) || (heroActions?.Down.WasPressed ?? false);
		bool flag6 = (current5 != null && (current5.leftArrowKey.wasPressedThisFrame || current5.aKey.wasPressedThisFrame)) || (heroActions?.Left.WasPressed ?? false);
		bool flag7 = (current5 != null && (current5.rightArrowKey.wasPressedThisFrame || current5.dKey.wasPressedThisFrame)) || (heroActions?.Right.WasPressed ?? false);
		bool flag8 = (current5 != null && (current5.enterKey.wasPressedThisFrame || current5.spaceKey.wasPressedThisFrame)) || (inputDevice != null && inputDevice.Action1.WasPressed) || (heroActions?.Jump.WasPressed ?? false);
		bool flag9 = (current5 != null && (current5.escapeKey.wasPressedThisFrame || current5.backspaceKey.wasPressedThisFrame)) || (inputDevice?.Action2.WasPressed ?? false);
		bool flag10 = (current5 != null && current5.deleteKey.wasPressedThisFrame) || (inputDevice?.Action3.WasPressed ?? false);
		Mouse current6 = Mouse.current;
		Vector2 point = MenuMouse.Gui();
		bool flag11 = current6?.leftButton.wasPressedThisFrame ?? false;
		bool flag12 = current6 != null && current6.delta.ReadValue().sqrMagnitude > 0.5f;
		SaveSlotButton button = ordered[index].Button;
		confirmT = Mathf.MoveTowards(confirmT, confirmOpen ? 1f : 0f, unscaledDeltaTime / 0.25f);
		if (AssistMode.IsOpen)
		{
			assistShutAt = Time.unscaledTime;
		}
		else
		{
			if (Time.unscaledTime - assistShutAt < 0.25f)
			{
				return;
			}
			bool flag13 = current6?.rightButton.wasPressedThisFrame ?? false;
			eraseT = Mathf.MoveTowards(eraseT, eraseOpen ? 1f : 0f, unscaledDeltaTime / 0.18f);
			if (eraseOpen)
			{
				int num2 = eraseIndex;
				if (flag6 || flag4)
				{
					eraseIndex = 0;
				}
				if (flag7 || flag5)
				{
					eraseIndex = 1;
				}
				for (int num3 = 0; num3 < 2; num3++)
				{
					if (eraseRects[num3].Contains(point) && (flag12 || flag11))
					{
						eraseIndex = num3;
					}
				}
				if (eraseIndex != num2)
				{
					Sfx.Play((eraseIndex == 0) ? Sounds.MenuRolloverUp.Id : Sounds.MenuRolloverDown.Id);
				}
				bool flag14 = flag8 || (flag11 && eraseRects[eraseIndex].Contains(point));
				if (flag9 || flag13 || (flag14 && eraseIndex == 0))
				{
					eraseOpen = false;
					Sfx.Play(Sounds.MenuUnpause.Id);
				}
				else if (flag14 && eraseIndex == 1)
				{
					eraseOpen = false;
					Sfx.Play(Sounds.MenuButtonSelect.Id);
					try
					{
						AccessTools.Method(typeof(SaveSlotButton), "ClearSaveFile").Invoke(button, null);
						Plugin.Log.LogInfo("File " + button.SaveSlotIndex + " deleted from the file select");
						AssistMode.ForgetSlot(button.SaveSlotIndex);
						RunLog.Forget(button.SaveSlotIndex);
					}
					catch (Exception ex2)
					{
						Plugin.Log.LogWarning("Couldn't delete the file: " + ex2.Message);
					}
				}
			}
			else if (confirmOpen)
			{
				int num4 = confirmIndex;
				int num5 = 3;   // Begin, Settings and Skins (user 2026-10-19: mode and difficulty moved into the new-file menu)
				if (flag4 || flag6)
				{
					confirmIndex = Math.Max(0, confirmIndex - 1);
				}
				if (flag5 || flag7)
				{
					confirmIndex = Math.Min(num5 - 1, confirmIndex + 1);
				}
				for (int num6 = 0; num6 < num5; num6++)
				{
					if (optionRects[num6].Contains(point) && (flag12 || flag11))
					{
						confirmIndex = num6;
					}
				}
				confirmIndex = Mathf.Clamp(confirmIndex, 0, num5 - 1);
				if (confirmIndex != num4)
				{
					Sfx.Play(flag4 ? Sounds.MenuRolloverUp.Id : Sounds.MenuRolloverDown.Id);
				}
				bool flag15 = flag8 || (flag11 && optionRects[confirmIndex].Contains(point));
				if (confirmIndex == 1 && flag15)
				{
					Sfx.Play(Sounds.MenuButtonSelect.Id);
					AssistMode.UseSlot(button.SaveSlotIndex);
					AssistMode.OpenAtTab = "Game";   // Settings always starts on Game here, not the last tab (user 2026-10-17)
					AssistMode.OpenFromMenu();
				}
				else if (confirmIndex == 2 && flag15)
				{
					// Skins: Settings opened straight on the Customization tab
					Sfx.Play(Sounds.MenuButtonSelect.Id);
					AssistMode.OpenAtTab = "Appearance";
					AssistMode.FocusTab = "Appearance";
					AssistMode.FocusSlot = button.SaveSlotIndex;
					AssistMode.OpenFromMenu();
				}
				else if (confirmIndex == 0 && flag15)
				{
					AssistMode.UseSlot(button.SaveSlotIndex);
					SaveSlotButton b = button;
					EventSystem es = current;
					Action start = delegate
					{
						begun = true;
						Sfx.Play(Sounds.MenuButtonSelect.Id);
						RememberSlot(b.SaveSlotIndex);
						if (es != null && b != null)
						{
							es.SetSelectedGameObject(b.gameObject);
							ExecuteEvents.Execute(b.gameObject, new BaseEventData(es), ExecuteEvents.submitHandler);
						}
					};
					// a new file in Standard or Speedrunner first picks its starting abilities (user 2026-10-19)
					// Silksong starts a new game when the slot's file is empty (after a delete it shows Restore, not Empty)
					bool fresh = b.saveFileState == SaveSlotButton.SaveFileStates.Empty;
					Plugin.Log.LogInfo("Begin on file " + b.SaveSlotIndex + ": " + b.saveFileState + ", " + b.State + ", mode " + AssistMode.ModeId);
					if (fresh)
					{
						TechTips.ForgetSlot(b.SaveSlotIndex);   // a new file: the Tech Tutorial question and lessons start over
						StartingAbilities.OpenMenu(start, b.SaveSlotIndex);
					}
					else
					{
						start();
					}
				}
				else if (flag9)
				{
					confirmOpen = false;
					Sfx.Play(Sounds.MenuUnpause.Id);
				}
			}
			else
			{
				int num7 = index;
				if (flag4)
				{
					index = (index + ordered.Count - 1) % ordered.Count;
				}
				if (flag5)
				{
					index = (index + 1) % ordered.Count;
				}
				for (int num8 = 0; num8 < ordered.Count; num8++)
				{
					if (cardRects.TryGetValue(ordered[num8].Button, out var value2) && value2.Contains(point) && (flag12 || flag11 || flag13))
					{
						index = num8;
					}
				}
				if (flag13 && cardRects.TryGetValue(ordered[index].Button, out var value3) && value3.Contains(point))
				{
					flag10 = true;
				}
				if (index != num7)
				{
					Sfx.Play(flag4 ? Sounds.MenuRolloverUp.Id : Sounds.MenuRolloverDown.Id);
				}
				button = ordered[index].Button;
				Rect value4;
				bool flag16 = cardRects.TryGetValue(button, out value4) && value4.Contains(point);
				if (flag8 || (flag11 && flag16))
				{
					confirmOpen = true;
					confirmIndex = 0;
					Sfx.Play(Sounds.MenuPause.Id);
				}
				else if (flag9 && button.backButton != null && current != null)
				{
					current.SetSelectedGameObject(button.backButton.gameObject);
					ExecuteEvents.Execute(button.backButton.gameObject, new BaseEventData(current), ExecuteEvents.submitHandler);
				}
				else if (flag10 && button.State == SaveSlotButton.SlotState.SavePresent)
				{
					eraseOpen = true;
					eraseIndex = 0;
					Sfx.Play(Sounds.MenuPause.Id);
				}
			}
			for (int num9 = 0; num9 < ordered.Count; num9++)
			{
				Slot slot = ordered[num9];
				bool flag17 = num9 == index;
				slot.Sel = Mathf.MoveTowards(slot.Sel, (flag17 && !confirmOpen) ? 1f : 0f, unscaledDeltaTime / 0.18f);
				if (slot.ShineT >= 0f && (slot.ShineT += unscaledDeltaTime) > 0.6f)
				{
					slot.ShineT = -1f;
				}
			}
		}
	}

	private void EraseBox(float sw, SaveSlotButton b)
	{
		float num = eraseT * eraseT * (3f - 2f * eraseT) * screenA;
		if (num <= 0.01f)
		{
			return;
		}
		GUI.color = new Color(0.02f, 0.01f, 0.06f, 0.7f * num);
		GUI.DrawTexture(new Rect(0f, 0f, sw, 1080f), Texture2D.whiteTexture);
		GUI.color = new Color(0.02f, 0.01f, 0.07f, 0.6f * num);
		GUI.DrawTexture(new Rect(sw / 2f - 700f, 330f, 1400f, 420f), CelesteMenus.MenuFx.FadeBoth);
		float num2 = (1f - num) * 30f;
		float num3 = sw / 2f;
		string text = "Delete File " + b.SaveSlotIndex + "?";
		float num4 = 1.1f;
		CelesteMenus.HiFont.Draw(Art, text, num3 - Art.Measure(text) * num4 / 2f, 380f + num2, num4, new Color(1f, 1f, 1f, num), new Color(0.08f, 0.06f, 0.16f, 0.85f * num), 4f);
		string line = "This can't be undone.";
		Art.DrawOutlined(line, num3 - Art.Measure(line) * 0.55f / 2f, 380f + num2 + (float)Art.LineHeight * num4 + 6f, 0.55f, new Color(0.85f, 0.8f, 0.95f, num), new Color(0f, 0f, 0f, 0.6f * num), 2f);
		GUI.color = new Color(0.8f, 0.78f, 1f, 0.5f * num);
		GUI.DrawTexture(new Rect(num3 - 300f, 380f + num2 + (float)Art.LineHeight * num4 + 60f, 600f, 2f), CelesteMenus.MenuFx.FadeBoth);
		string[] array = new string[2] { "Cancel", "Delete" };
		float num5 = 0.85f;
		float num6 = 590f + num2;
		for (int i = 0; i < 2; i++)
		{
			bool num7 = i == eraseIndex;
			float num8 = num3 + ((i == 0) ? (-200f) : 200f);
			float num9 = Art.Measure(array[i]) * num5;
			eraseRects[i] = new Rect(num8 - num9 / 2f - 40f, num6 - 10f, num9 + 80f, (float)Art.LineHeight * num5 + 20f);
			if (num7)
			{
				GUI.color = ((i == 1) ? new Color(1f, 0.35f, 0.4f, 0.28f * num) : new Color(0.55f, 0.5f, 0.95f, 0.25f * num));
				GUI.DrawTexture(eraseRects[i], CelesteMenus.MenuFx.FadeBoth);
			}
			float num10 = (num7 ? (-3f - Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 2f) : 0f);
			Color c = ((!num7) ? new Color(0.8f, 0.8f, 0.85f, num) : ((i == 1) ? new Color(1f, 0.45f, 0.45f, num) : new Color(0.6f, 1f, 0.6f, num)));
			Art.DrawOutlined(array[i], num8 - num9 / 2f, num6 + num10, num5, c, new Color(0f, 0f, 0f, 0.75f * num), 2.5f);
		}
		GUI.color = Color.white;
	}

	private void TickOld()
	{
		if (Art == null || Art.Image("fileselect/card") == null)
		{
			return;
		}
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		EventSystem current = EventSystem.current;
		GameObject gameObject = ((current != null) ? current.currentSelectedGameObject : null);
		if ((findIn -= unscaledDeltaTime) <= 0f)
		{
			findIn = 0.5f;
			SaveSlotButton[] array = UnityEngine.Object.FindObjectsByType<SaveSlotButton>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			foreach (SaveSlotButton saveSlotButton in array)
			{
				if (!slots.ContainsKey(saveSlotButton))
				{
					slots[saveSlotButton] = new Slot
					{
						Button = saveSlotButton
					};
				}
			}
		}
		List<SaveSlotButton> list = new List<SaveSlotButton>();
		ordered.Clear();
		bool flag = false;
		foreach (KeyValuePair<SaveSlotButton, Slot> slot2 in slots)
		{
			Slot value = slot2.Value;
			SaveSlotButton key = slot2.Key;
			if (key == null)
			{
				list.Add(slot2.Key);
				continue;
			}
			value.Ours = false;
			if (key.isActiveAndEnabled && !(((key.myCanvasGroup != null) ? key.myCanvasGroup.alpha : 1f) < 0.3f))
			{
				if (Array.IndexOf(Drawn, key.State) >= 0)
				{
					value.Ours = true;
					ordered.Add(value);
				}
				else if (key.State != SaveSlotButton.SlotState.Hidden && key.State != SaveSlotButton.SlotState.OperationInProgress)
				{
					flag = true;
				}
			}
		}
		foreach (SaveSlotButton item in list)
		{
			slots.Remove(item);
		}
		ordered.Sort((Slot x, Slot y) => x.Button.SaveSlotIndex.CompareTo(y.Button.SaveSlotIndex));
		active = ordered.Count > 0 && !flag;
		Showing = active;
		screenA = Mathf.MoveTowards(screenA, active ? 1f : 0f, unscaledDeltaTime / 0.25f);
		foreach (KeyValuePair<SaveSlotButton, Slot> slot3 in slots)
		{
			if (slot3.Key != null && slot3.Key.myCanvasGroup != null)
			{
				slot3.Key.myCanvasGroup.blocksRaycasts = !active;
			}
		}
		if (!active)
		{
			return;
		}
		if (!logged)
		{
			logged = true;
			Plugin.Log.LogInfo("Celeste file select: the save files as Celeste's");
		}
		for (int num = 0; num < ordered.Count; num++)
		{
			if (gameObject == ordered[num].Button.gameObject)
			{
				index = num;
			}
		}
		index = Mathf.Clamp(index, 0, ordered.Count - 1);
		Keyboard current5 = Keyboard.current;
		InputHandler instance = ManagerSingleton<InputHandler>.Instance;
		HeroActions heroActions = ((instance != null) ? instance.inputActions : null);
		bool flag2 = (current5 != null && (current5.upArrowKey.wasPressedThisFrame || current5.wKey.wasPressedThisFrame)) || (heroActions?.Up.WasPressed ?? false);
		bool num2 = (current5 != null && (current5.downArrowKey.wasPressedThisFrame || current5.sKey.wasPressedThisFrame)) || (heroActions?.Down.WasPressed ?? false);
		int num3 = index;
		if (flag2)
		{
			index = (index + ordered.Count - 1) % ordered.Count;
		}
		if (num2)
		{
			index = (index + 1) % ordered.Count;
		}
		Mouse current6 = Mouse.current;
		bool flag3 = false;
		if (current6 != null)
		{
			Vector2 point = MenuMouse.Gui();
			for (int num4 = 0; num4 < ordered.Count; num4++)
			{
				if (cardRects.TryGetValue(ordered[num4].Button, out var value2) && value2.Contains(point))
				{
					if (current6.delta.ReadValue().sqrMagnitude > 0.5f)
					{
						index = num4;
					}
					if (current6.leftButton.wasPressedThisFrame)
					{
						index = num4;
						flag3 = true;
					}
				}
			}
		}
		SaveSlotButton button = ordered[index].Button;
		if (index != num3)
		{
			Sfx.Play(flag2 ? Sounds.MenuRolloverUp.Id : Sounds.MenuRolloverDown.Id);
		}
		if (current != null && gameObject != button.gameObject)
		{
			current.SetSelectedGameObject(button.gameObject);
		}
		if (flag3 && current != null)
		{
			ExecuteEvents.Execute(button.gameObject, new BaseEventData(current), ExecuteEvents.submitHandler);
		}
		bool flag4 = current5?.deleteKey.wasPressedThisFrame ?? false;
		try
		{
			InControl.InputDevice activeDevice = InputManager.ActiveDevice;
			if (activeDevice != null && activeDevice.Action3.WasPressed)
			{
				flag4 = true;
			}
		}
		catch (Exception)
		{
		}
		if (flag4 && button.State == SaveSlotButton.SlotState.SavePresent && button.clearSaveButton != null && current != null)
		{
			Selectable componentInChildren = button.clearSaveButton.GetComponentInChildren<Selectable>(includeInactive: true);
			if (componentInChildren != null)
			{
				current.SetSelectedGameObject(componentInChildren.gameObject);
				ExecuteEvents.Execute(componentInChildren.gameObject, new BaseEventData(current), ExecuteEvents.submitHandler);
			}
		}
		for (int num5 = 0; num5 < ordered.Count; num5++)
		{
			Slot slot = ordered[num5];
			bool flag5 = num5 == index;
			if (flag5 && slot.Sel < 0.5f)
			{
				slot.ShineT = 0f;
			}
			slot.Sel = Mathf.MoveTowards(slot.Sel, flag5 ? 1f : 0f, unscaledDeltaTime / 0.18f);
			if (slot.ShineT >= 0f && (slot.ShineT += unscaledDeltaTime) > 0.6f)
			{
				slot.ShineT = -1f;
			}
		}
	}

	private static void Hide(CanvasGroup cg)
	{
		if (cg != null)
		{
			cg.alpha = 0f;
		}
	}

	private static Rect ScreenRect(RectTransform rt)
	{
		if (rt == null)
		{
			return default(Rect);
		}
		Vector3[] array = new Vector3[4];
		rt.GetWorldCorners(array);
		Canvas componentInParent = rt.GetComponentInParent<Canvas>();
		Camera cam = ((componentInParent != null && componentInParent.renderMode != RenderMode.ScreenSpaceOverlay) ? componentInParent.worldCamera : null);
		float num = (float)Screen.height / 1080f;
		Vector2 vector = RectTransformUtility.WorldToScreenPoint(cam, array[0]);
		Vector2 vector2 = RectTransformUtility.WorldToScreenPoint(cam, array[2]);
		return new Rect(vector.x / num, ((float)Screen.height - vector2.y) / num, (vector2.x - vector.x) / num, (vector2.y - vector.y) / num);
	}

	private Rect Layout(int i, int n, float sw)
	{
		float num = ((n <= 3) ? 250f : 205f);
		float num2 = ((n <= 3) ? 40f : 26f);
		float num3 = (float)n * num + (float)(n - 1) * num2;
		float num4 = (1080f - num3) / 2f - 20f;
		return new Rect(sw / 2f - 10f, num4 + (float)i * (num + num2), 20f, num / 1.1f);
	}

	private void OnGUI()
	{
		if (Event.current.type != EventType.Repaint || Art == null || screenA <= 0f)
		{
			return;
		}
		GuiArt.Img img = Art.Image("fileselect/card");
		GuiArt.Img ticket = Art.Image("fileselect/ticket");
		if (img == null)
		{
			return;
		}
		float num = (float)Screen.height / 1080f;
		float num2 = (float)Screen.width / num;
		Matrix4x4 matrix = GUI.matrix;
		GUI.depth = ((AssistMode.IsOpen || StartingAbilities.IsOpen) ? 3 : (-6));
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num, num, 1f));
		Background(num2, screenA);
		float num3 = confirmT * confirmT * (3f - 2f * confirmT);
		Slot pickedSlot = ((index < ordered.Count) ? ordered[index] : null);
		for (int i = 0; i < ordered.Count; i++)
		{
			Rect gui = Layout(i, ordered.Count, num2);
			// the files rise into place one after another as the screen fades in (2026-10-17)
			float rise = Mathf.Clamp01(screenA * 1.7f - (float)i * 0.22f);
			gui.y += (1f - rise * rise * (3f - 2f * rise)) * 140f;
			if (ordered[i] == pickedSlot && num3 > 0f)
			{
				Rect rect = new Rect(num2 / 2f - 10f, 170f, 20f, 227.27272f);
				gui = new Rect(Mathf.Lerp(gui.x, rect.x, num3), Mathf.Lerp(gui.y, rect.y, num3), gui.width, Mathf.Lerp(gui.height, rect.height, num3));
			}
			ordered[i].Gui = gui;
		}
		List<Slot> list = new List<Slot>(ordered);
		list.Sort((Slot x, Slot y) => ((x == pickedSlot) ? 2f : x.Sel).CompareTo((y == pickedSlot) ? 2f : y.Sel));
		cardRects.Clear();
		foreach (Slot item in list)
		{
			if (!(item.Button == null))
			{
				cardAlpha = ((item == pickedSlot) ? 1f : (1f - num3));
				if (cardAlpha > 0.01f)
				{
					// the chosen file sways gently, as if hanging in the wind (2026-10-17)
					Matrix4x4 m0 = GUI.matrix;
					if (item.Sel > 0.01f)
					{
						GUIUtility.RotateAroundPivot(Mathf.Sin(Time.unscaledTime * 1.3f) * 1.2f * item.Sel, item.Gui.center);
					}
					DrawFile(item, img, ticket);
					GUI.matrix = m0;
					if (item == pickedSlot && item.Sel > 0.5f && cardRects.TryGetValue(item.Button, out Rect cr))
					{
						DrawClimber(item, cr);
					}
				}
			}
		}
		cardAlpha = 1f;
		if (num3 > 0.01f)
		{
			Options(num2, num3 * screenA);
		}
		Hints(num2, screenA);
		if (pickedSlot != null && pickedSlot.Button != null)
		{
			EraseBox(num2, pickedSlot.Button);
		}
		GUI.matrix = matrix;
		GUI.color = Color.white;
	}

	/// <summary>Extra sideways drift for the title's idle camera.</summary>
	public static float ExtraPan;

	/// <summary>The title's mountain, small, with its drifting particles: Settings' preview of Title Sky / Mountain.</summary>
	internal static void SkyPreview(Rect r, float a)
	{
		GUI.color = new Color(1f, 1f, 1f, a);
		GUI.DrawTexture(r, Sky());
		Texture2D image = Flake();
		float t = Time.unscaledTime;
		for (int k = 0; k < 14; k++)
		{
			float h = Hash(k, 8);
			float sz = 2f + h * 4f;
			float fall = TitleScene.Rising ? -0.6f : 1f;
			float y = r.y + Mathf.Repeat(Hash(k, 6) * r.height + t * (10f + h * 25f) * fall, r.height);
			float x = r.x + Mathf.Repeat(Hash(k, 7) * r.width - t * (4f + h * 8f), r.width);
			GUI.color = new Color(TitleScene.Particle.r, TitleScene.Particle.g, TitleScene.Particle.b, a * (0.3f + h * 0.6f));
			GUI.DrawTexture(new Rect(x, y, sz, sz), image);
		}
		GUI.color = Color.white;
	}

	internal static void Background(float sw, float a)
	{
		float unscaledTime = Time.unscaledTime;
		float num = 1.04f + Mathf.Sin(unscaledTime * 0.05f) * 0.01f;
		float num2 = Mathf.Sin(unscaledTime * 0.031f) * sw * 0.012f + ExtraPan;
		float num3 = Mathf.Sin(unscaledTime * 0.043f) * 6f;
		GUI.color = new Color(1f, 1f, 1f, a);
		GUI.DrawTexture(new Rect((sw - sw * num) / 2f + num2, (1080f - 1080f * num) / 2f + num3, sw * num, 1080f * num), Sky());
		float num4 = Mathf.Repeat(unscaledTime, 11f);
		if (num4 < 0.9f)
		{
			int i = Mathf.FloorToInt(unscaledTime / 11f);
			float num5 = num4 / 0.9f;
			float num6 = (0.15f + Hash(i, 31) * 0.6f) * sw;
			float num7 = 60f + Hash(i, 32) * 220f;
			Vector2 pivotPoint = new Vector2(num6 + num5 * 520f, num7 + num5 * 190f);
			Matrix4x4 matrix = GUI.matrix;
			GUIUtility.RotateAroundPivot(20f, pivotPoint);
			GUI.color = new Color(1f, 0.95f, 1f, a * Mathf.Sin(num5 * MathF.PI) * 0.9f);
			GUI.DrawTexture(new Rect(pivotPoint.x - 170f, pivotPoint.y - 1.5f, 170f, 3f), CelesteMenus.MenuFx.FadeIn);
			GUI.matrix = matrix;
		}
		for (int j = 0; j < 24; j++)
		{
			float x = Hash(j, 1) * sw;
			float y = Hash(j, 2) * 560f;
			float num8 = 0.5f + 0.5f * Mathf.Sin(unscaledTime * (1f + Hash(j, 3) * 2f) + (float)j);
			float num9 = 2f + Hash(j, 4) * 2.5f;
			GUI.color = new Color(0.92f, 0.94f, 1f, a * (0.2f + 0.7f * num8) * TitleScene.Stars);
			GUI.DrawTexture(new Rect(x, y, num9, num9), Texture2D.whiteTexture);
		}
		Texture2D image = Flake();
		for (int k = 0; k < 40; k++)
		{
			float num10 = Hash(k, 8);
			float num11 = 3f + num10 * num10 * 11f;
			float num12 = 22f + num10 * 75f;
			// snow, or the area's own drift: spores, embers, bubbles rise instead (TitleScene)
			float fall = TitleScene.Rising ? -0.6f : 1f;
			float y2 = Mathf.Repeat(Hash(k, 6) * 1080f + unscaledTime * num12 * fall, 1110f) - 15f;
			float x2 = Mathf.Repeat(Hash(k, 7) * sw + Mathf.Sin(unscaledTime * 0.6f + (float)k) * (15f + num10 * 30f) - unscaledTime * (8f + num10 * 22f), sw);
			GUI.color = new Color(TitleScene.Particle.r, TitleScene.Particle.g, TitleScene.Particle.b, a * (0.18f + num10 * 0.55f));
			GUI.DrawTexture(new Rect(x2, y2, num11, num11), image);
		}
		GUI.color = Color.white;
	}

	private static void StartSky()
	{
		if (skyStarted)
		{
			return;
		}
		skyStarted = true;
		TitleScene.Pick();
		Thread thread = new Thread((ThreadStart)delegate
		{
			try
			{
				painting = true;
				skyPixels = PaintSky();
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("file select sky: " + ex.Message);
			}
			finally
			{
				painting = false;
			}
		});
		thread.IsBackground = true;
		thread.Start();
	}

	private static Texture2D Sky()
	{
		if (TitleScene.Stale && skyPixels == null && !painting)
		{
			// Title Sky / Title Mountain changed in Settings: one painted before swaps in at once; a new one is
			// painted in the background while the old one stays up (user 2026-10-17)
			if (painted.TryGetValue(TitleScene.Wanted, out Texture2D done) && done != null)
			{
				TitleScene.Pick();
				sky = done;
				return sky;
			}
			skyStarted = false;
			paintingFor = TitleScene.Wanted;
			StartSky();
		}
		Color[] array = skyPixels;
		if (array != null)
		{
			Texture2D fresh = new Texture2D(1280, 720, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp
			};
			fresh.SetPixels(array);
			fresh.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			skyPixels = null;
			painted[paintingFor ?? TitleScene.Wanted] = fresh;   // kept, so switching back is instant
			sky = fresh;
		}
		if (sky != null)
		{
			return sky;
		}
		StartSky();
		return Texture2D.blackTexture;
	}

	private static float HashT(int i, int salt)
	{
		double num = Math.Sin((double)i * 12.9898 + (double)salt * 78.233) * 43758.5453;
		return (float)(num - Math.Floor(num));
	}

	// the 14 ice crystals' places, worked out once instead of for every pixel
	private static readonly float[] crystalX = new float[14];

	private static readonly float[] crystalY = new float[14];

	private static readonly float[] crystalS = new float[14];

	// skies already painted this session, by Title Sky + Title Mountain: switching back is instant
	private static readonly Dictionary<string, Texture2D> painted = new Dictionary<string, Texture2D>();

	private static string paintingFor;

	private static Color[] PaintSky()
	{
		for (int m = 0; m < 14; m++)
		{
			crystalX[m] = 0.3f + Hash(m, 21) * 0.4f;
			crystalY[m] = 0.76f + Hash(m, 22) * 0.22f;
			crystalS[m] = 0.7f + Hash(m, 23) * 0.6f;
		}
		Color[] array = new Color[921600];
		Color a = TitleScene.Top;
		Color color = TitleScene.Mid;
		Color b = TitleScene.Bottom;
		System.Random random = new System.Random(7);
		List<Vector2> list = new List<Vector2>();
		List<float> list2 = new List<float>();
		for (int i = 0; i < 70; i++)
		{
			float num = (float)random.NextDouble();
			float num2 = 0.5f + ((float)random.NextDouble() - 0.5f) * (0.08f + num * 0.75f);
			list.Add(new Vector2(num2 * 1280f, (0.08f + num * 0.95f) * 720f));
			list2.Add((float)random.NextDouble());
		}
		for (int j = 0; j < 720; j++)
		{
			float num3 = (float)j / 719f;
			Color color2 = ((num3 < 0.45f) ? Color.Lerp(a, color, num3 / 0.45f) : Color.Lerp(color, b, (num3 - 0.45f) / 0.55f));
			for (int k = 0; k < 1280; k++)
			{
				float num4 = (float)k / 1279f;
				Color a2 = color2;
				float num5 = 0.2f + Mathf.Sin(num4 * 5.5f + 0.8f) * 0.06f + Mathf.Sin(num4 * 13f) * 0.015f;
				float num6 = Mathf.Exp((0f - (num3 - num5)) * (num3 - num5) * 160f) * (0.35f + Mathf.PerlinNoise(num4 * 9f, 0.5f) * 0.8f);
				num6 += Mathf.Exp((0f - (num3 - num5 + 0.05f)) * (num3 - num5 + 0.05f) * 400f) * Mathf.PerlinNoise(num4 * 22f, 4.2f) * 0.5f;
				a2 += Color.Lerp(new Color(0.15f, 0.55f, 0.5f), new Color(0.55f, 0.25f, 0.55f), num4) * num6 * 0.7f;
				float num7 = (num4 - 0.82f) * 1280f / 720f;
				float num8 = num3 - 0.16f;
				float num9 = Mathf.Sqrt(num7 * num7 + num8 * num8);
				a2 += new Color(0.5f, 0.5f, 0.75f) * Mathf.Exp((0f - num9) * 9f) * 0.35f;
				if (num9 < 0.045f)
				{
					a2 = Color.Lerp(a2, TitleScene.Moon - new Color(0.06f, 0.06f, 0.03f) * Mathf.PerlinNoise(num4 * 90f, num3 * 90f), Mathf.Clamp01((0.045f - num9) * 720f * 0.9f));
				}
				float num10 = 0.72f - Mathf.PerlinNoise(num4 * 4.2f, 1.7f) * 0.22f - Mathf.Max(0f, 0.25f - Mathf.Abs(num4 - 0.12f)) * 0.5f - Mathf.Max(0f, 0.2f - Mathf.Abs(num4 - 0.88f)) * 0.35f;
				if (num3 > num10)
				{
					a2 = Color.Lerp(a2, Color.Lerp(new Color(0.17f, 0.2f, 0.4f), new Color(0.12f, 0.14f, 0.3f), (num3 - num10) * 3f), Mathf.Clamp01((num3 - num10) * 720f * 0.5f));
				}
				float num11 = Mathf.PerlinNoise(num4 * 7f, num3 * 9f + 3f) * Mathf.Clamp01(1f - Mathf.Abs(num3 - 0.68f) * 7f);
				if (num11 > 0.45f)
				{
					a2 = Color.Lerp(a2, new Color(0.62f, 0.6f, 0.82f), Mathf.Clamp01((num11 - 0.45f) * 4f) * 0.55f);
				}
				float num12 = 0.015f + (num3 - 0.09f) * 0.55f - Mathf.Abs(num4 - 0.5f + Mathf.Sin(num3 * 9f) * 0.012f);
				if (num3 > 0.09f && num12 > 0f)
				{
					int num13 = 0;
					float num14 = float.MaxValue;
					for (int l = 0; l < list.Count; l++)
					{
						float sqrMagnitude = (list[l] - new Vector2(k, j)).sqrMagnitude;
						if (sqrMagnitude < num14)
						{
							num14 = sqrMagnitude;
							num13 = l;
						}
					}
					Vector2 vector = list[num13];
					float num15 = Mathf.Clamp01(0.55f + (0.5f - vector.x / 1280f) * 1.8f + (list2[num13] - 0.5f) * 0.35f);
					Color a3 = Color.Lerp(new Color(0.24f, 0.45f, 0.78f), new Color(0.62f, 0.85f, 0.98f), num15);
					a3 = Color.Lerp(a3, new Color(0.3f, 0.5f, 0.85f), Mathf.Clamp01((num3 - 0.6f) * 1.4f));
					float num16 = 0.2f + (list2[num13] - 0.5f) * 0.05f;
					if (vector.y / 720f < num16)
					{
						a3 = Color.Lerp(new Color(0.78f, 0.8f, 0.97f), Color.white, num15 * 0.9f);
					}
					if (num4 > 0.5f)
					{
						a3 += new Color(0.2f, 0.2f, 0.3f) * Mathf.Clamp01(1f - num12 * 1280f * 0.08f) * 0.6f;
					}
					a3 = Color.Lerp(a3, TitleScene.Tint * new Color(1f, 1f, 1f, 1f) * (0.6f + 0.6f * num15), TitleScene.TintAmount);   // the area's colour in the ice
					a2 = Color.Lerp(a2, a3, Mathf.Clamp01(num12 * 1280f * 0.8f));
				}
				for (int m = 0; m < 14; m++)
				{
					float num17 = crystalX[m];
					float num18 = crystalY[m];
					float num19 = crystalS[m];
					float num21 = (num3 - num18) * 13f / num19;
					if (num21 * num21 > 30f)
					{
						continue;   // far from this crystal: nothing to add (keeps repainting quick)
					}
					float num20 = (num4 - num17) * 34f / num19;
					if (num20 * num20 * 0.25f > 30f)
					{
						continue;
					}
					float num22 = 1f - (Mathf.Abs(num20) * 2.2f + Mathf.Abs(num21));
					if (num22 > 0f)
					{
						a2 = Color.Lerp(a2, (num20 < 0f) ? new Color(0.7f, 1f, 0.98f) : new Color(0.35f, 0.85f, 0.9f), Mathf.Clamp01(num22 * 6f) * 0.95f);
					}
					float num23 = Mathf.Exp((0f - (num20 * num20 * 0.25f + num21 * num21)) * 0.5f);
					a2 += new Color(0.1f, 0.35f, 0.35f) * num23 * 0.3f;
				}
				if (num3 < 0.6f && Hash(k * 131 + j * 7, 3) > 0.9985f && Hash(k * 17 + j * 3, 9) < TitleScene.Stars)
				{
					a2 = Color.Lerp(a2, Color.white, 0.35f + Hash(k, j) * 0.4f);
				}
				float num24 = Mathf.Clamp01((num3 - 0.8f) * 5f) * (0.5f + Mathf.PerlinNoise(num4 * 5f, num3 * 3f) * 0.6f);
				a2 = Color.Lerp(a2, new Color(0.5f, 0.42f, 0.7f), num24 * 0.45f);
				float num25 = num4 - 0.5f;
				float num26 = num3 - 0.5f;
				a2 *= 1f - (num25 * num25 * 0.9f + num26 * num26 * 1.1f) * 0.75f;
				a2 += new Color(1f, 1f, 1f) * ((HashT(k * 7 + j * 1031, 5) - 0.5f) / 160f);
				a2.a = 1f;
				array[(719 - j) * 1280 + k] = a2;
			}
		}
		return array;
	}

	private static Texture2D SkyOld()
	{
		if (sky != null)
		{
			return sky;
		}
		Color32[] array = new Color32[518400];
		Color a = new Color(0.04f, 0.07f, 0.19f);
		Color color = new Color(0.15f, 0.16f, 0.4f);
		Color b = new Color(0.42f, 0.29f, 0.55f);
		float[] array2 = new float[3] { 0.6f, 0.71f, 0.82f };
		float[] array3 = new float[3] { 0.17f, 0.2f, 0.17f };
		float[] array4 = new float[3] { 0.0062f, 0.0045f, 0.0072f };
		Color[] array5 = new Color[3]
		{
			new Color(0.3f, 0.27f, 0.53f),
			new Color(0.21f, 0.17f, 0.4f),
			new Color(0.12f, 0.09f, 0.25f)
		};
		float[,] array6 = new float[3, 960];
		for (int i = 0; i < 3; i++)
		{
			for (int j = 0; j < 960; j++)
			{
				array6[i, j] = array2[i] - (Mathf.PerlinNoise((float)j * array4[i] * 2f, (float)i * 9.1f) * 0.72f + Mathf.PerlinNoise((float)j * array4[i] * 6.3f, (float)i * 4.3f) * 0.28f) * array3[i];
			}
		}
		for (int k = 0; k < 540; k++)
		{
			float num = 1f - (float)k / 539f;
			Color color2 = ((num < 0.6f) ? Color.Lerp(a, color, num / 0.6f) : Color.Lerp(color, b, (num - 0.6f) / 0.4f));
			for (int l = 0; l < 960; l++)
			{
				Color a2 = color2;
				float num2 = ((float)l - 595.2f) / 480f;
				float num3 = (num - 0.62f) / 0.25f;
				a2 = Color.Lerp(a2, new Color(0.62f, 0.45f, 0.72f), Mathf.Clamp01(0.35f - (num2 * num2 + num3 * num3) * 0.35f));
				for (int m = 0; m < 3; m++)
				{
					float num4 = array6[m, l];
					if (num >= num4)
					{
						float num5 = (num - num4) * 540f;
						Color color3 = array5[m];
						a2 = Color.Lerp(b: (!(num5 < 3f)) ? Color.Lerp(color3 * 1.15f, color3, Mathf.Clamp01(num5 / 40f)) : Color.Lerp(new Color(0.8f, 0.78f, 0.95f), color3, num5 / 3f), a: a2, t: Mathf.Clamp01((num - num4) * 540f * 0.9f));
					}
					else if (num4 - num < 0.04f)
					{
						a2 = Color.Lerp(a2, array5[m] * 1.3f, (1f - (num4 - num) / 0.04f) * 0.25f);
					}
				}
				if (num < 0.55f && Hash(l * 131 + k * 7, 3) > 0.9985f)
				{
					a2 = Color.Lerp(a2, Color.white, 0.6f);
				}
				a2.a = 1f;
				array[k * 960 + l] = a2;
			}
		}
		sky = new Texture2D(960, 540, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp
		};
		sky.SetPixels32(array);
		sky.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		return sky;
	}

	private static Texture2D Flake()
	{
		if (flake != null)
		{
			return flake;
		}
		Color32[] array = new Color32[256];
		for (int i = 0; i < 16; i++)
		{
			for (int j = 0; j < 16; j++)
			{
				float num = Mathf.Sqrt(((float)j - 7.5f) * ((float)j - 7.5f) + ((float)i - 7.5f) * ((float)i - 7.5f)) / 7.5f;
				array[i * 16 + j] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - num * num));
			}
		}
		flake = new Texture2D(16, 16, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Bilinear
		};
		flake.SetPixels32(array);
		flake.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		return flake;
	}

	private static float Hash(int i, int salt)
	{
		return Mathf.Repeat(Mathf.Sin((float)i * 12.9898f + (float)salt * 78.233f) * 43758.547f, 1f);
	}

	private void Options(float sw, float a)
	{
		string[] array = new string[3] { "Begin", "Settings", "Skins" };
		float[] array2 = new float[3] { 1f, 0.75f, 0.75f };
		float num = 520f;
		float unscaledTime = Time.unscaledTime;
		Color color = ((unscaledTime % 0.2f < 0.1f) ? Hex("84FF54") : Hex("FCFF59"));
		for (int i = 0; i < array.Length; i++)
		{
			float num2 = array2[i];
			float num3 = Art.Measure(array[i]) * num2;
			float num4 = sw / 2f - num3 / 2f;
			bool num5 = i == confirmIndex;
			float num6 = (num5 ? ((0f - Mathf.Abs(Mathf.Sin(unscaledTime * 5f))) * 3f) : 0f);
			Color c = (num5 ? color : Color.white);
			c.a = a;
			optionRects[i] = new Rect(num4 - 30f, num - 6f, num3 + 60f, (float)Art.LineHeight * num2 + 12f);
			Art.DrawOutlined(array[i], num4, num + num6, num2, c, new Color(0f, 0f, 0f, a * 0.7f), 3f);
			num += (float)Art.LineHeight * num2 + 26f;
		}
	}

	private static Color Hex(string h)
	{
		if (!ColorUtility.TryParseHtmlString("#" + h, out var color))
		{
			return Color.white;
		}
		return color;
	}

	private void Hints(float sw, float a)
	{
		bool padActive = MenuMouse.PadActive;
		string[] array = ((!confirmOpen) ? ((!padActive) ? new string[3] { "Confirm  Enter", "Erase  Delete / Right-click", "Back  Esc" } : new string[3] { "Confirm  Cross", "Erase  Square", "Back  Circle" }) : ((!padActive) ? new string[2] { "Confirm  Enter", "Back  Esc" } : new string[2] { "Confirm  Cross", "Back  Circle" }));
		float num = 0.5f;
		float num2 = sw - 50f;
		for (int num3 = array.Length - 1; num3 >= 0; num3--)
		{
			float num4 = Art.Measure(array[num3]) * num;
			num2 -= num4;
			Art.DrawOutlined(array[num3], num2, 1010f, num, new Color(1f, 1f, 1f, a), new Color(0f, 0f, 0f, a * 0.6f), 2f);
			num2 -= 50f;
		}
	}

	private void DrawFile(Slot sl, GuiArt.Img card, GuiArt.Img ticket)
	{
		SaveSlotButton button = sl.Button;
		Rect gui = sl.Gui;
		if (gui.height < 10f)
		{
			return;
		}
		float num = screenA * cardAlpha;
		float num2 = 1f - (1f - sl.Sel) * (1f - sl.Sel);
		float num3 = gui.height * 1.1f;
		float num4 = num3 * (float)card.W / (float)card.H;
		float num5 = num3 / 270f;
		float num6 = gui.center.x - num4 / 2f - num2 * num4 * 0.32f;
		float num7 = gui.center.y - num3 / 2f;
		bool flag = button.State == SaveSlotButton.SlotState.EmptySlot;
		Color c = new Color(0.27f, 0.23f, 0.29f, num);
		Color c2 = new Color(0.47f, 0.4f, 0.52f, num);
		if (ticket != null && num2 > 0.01f)
		{
			float num8 = num3 * 0.9f;
			float num9 = num8 * (float)ticket.W / (float)ticket.H * 0.95f;
			float num10 = num6 + num4 - 30f * num5 - num9 * (1f - num2);
			float num11 = num7 + (num3 - num8) / 2f;
			GUI.color = new Color(1f, 1f, 1f, num);
			GUI.DrawTexture(new Rect(num10, num11, num9, num8), ticket.Tex);
			sl.Ticket = new Rect(num10, num11, num9, num8);
			float num12 = num * Mathf.Clamp01((num2 - 0.6f) / 0.4f);
			if (!flag && num12 > 0f)
			{
				float num13 = num10 + 70f * num5;
				float num14 = 0.5f * num5;
				string text = Txt(button.rosaryText);
				string text2 = Txt(button.shardText);
				string text3 = Txt(button.completionText);
				string line = ((text.Length > 0) ? (text + " Rosaries") : "") + ((text2.Length > 0) ? ("    " + text2 + " Shards") : "");
				Art.DrawLine(line, num13, num11 + 40f * num5, num14, new Color(0.55f, 0.5f, 0.58f, num12));
				if (text3.Length > 0)
				{
					float num15 = num11 + 40f * num5 + (float)Art.LineHeight * num14;
					float num16 = (float)Art.LineHeight * num14;
					GUI.color = new Color(1f, 1f, 1f, num12);
					GUI.DrawTexture(new Rect(num13, num15, num16, num16), FileIcons.Spool);
					Art.DrawLine(text3, num13 + num16 + 8f * num5, num15, num14, new Color(0.55f, 0.5f, 0.58f, num12));
				}
				GuiArt.Img skull = Art.Skull;
				float num17 = 62f * num5;
				float num18 = num11 + num8 - 40f * num5 - num17;
				if (skull != null)
				{
					GUI.color = new Color(1f, 1f, 1f, num12);
					GUI.DrawTexture(new Rect(num13, num18, num17, num17 * (float)skull.H / (float)Math.Max(1, skull.W)), skull.Tex);
				}
				Outlined("x" + Deaths(button.SaveSlotIndex), num13 + num17 + 12f * num5, num18 + num17 / 2f, 0.8f * num5, num12);
				// bosses beaten on this file, and how far the completion has come as a silk thread (2026-10-17)
				int bossesTotal;
				int bosses = BossCard.BossesOnSlot(button.SaveSlotIndex, out bossesTotal);
				float yb = num11 + 40f * num5 + (float)Art.LineHeight * num14 * 2.2f;
				Art.DrawLine("Bosses  " + bosses + " / " + bossesTotal, num13, yb, num14, new Color(0.55f, 0.5f, 0.58f, num12));
				float pct = 0f;
				string pctText = text3.TrimEnd('%').Trim();
				float.TryParse(pctText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out pct);
				float barW = num9 - 140f * num5;
				float barY = yb + (float)Art.LineHeight * num14 + 10f * num5;
				GUI.color = new Color(0.55f, 0.5f, 0.58f, 0.25f * num12);
				GUI.DrawTexture(new Rect(num13, barY, barW, 6f * num5), Texture2D.whiteTexture);
				GUI.color = new Color(0.86f, 0.33f, 0.4f, num12);
				GUI.DrawTexture(new Rect(num13, barY, barW * Mathf.Clamp01(pct / 100f), 6f * num5), Texture2D.whiteTexture);
				string line2 = Txt(button.playTimeText);
				float num19 = 0.6f * num5;
				Art.DrawLine(line2, num10 + num9 - 50f * num5 - Art.Measure(line2) * num19, num18 + num17 / 2f - (float)Art.LineHeight * num19 / 2f, num19, new Color(0.55f, 0.5f, 0.58f, num12));
			}
		}
		if (num2 > 0.01f)
		{
			num7 += Mathf.Sin(Time.unscaledTime * 1.8f) * 4f * num2;
			GUI.color = new Color(1f, 0.85f, 0.6f, 0.28f * num2 * num);
			GUI.DrawTexture(new Rect(num6 - num4 * 0.12f, num7 - num3 * 0.35f, num4 * 1.24f, num3 * 1.7f), CelesteMenus.MenuFx.Glow);
		}
		GUI.color = new Color(0f, 0f, 0f, 0.3f * num);
		GUI.DrawTexture(new Rect(num6 + 8f, num7 + 10f, num4, num3), card.Tex);
		GUI.color = new Color(1f, 1f, 1f, num);
		GUI.DrawTexture(new Rect(num6, num7, num4, num3), card.Tex);
		cardRects[button] = new Rect(num6, num7, num4, num3);
		if (!flag)
		{
			// how the file plays, as badges on the card's top edge (only what isn't Standard / Normal)
			AssistMode.ProfileOf(button.SaveSlotIndex, out string bm, out string bd);
			float bx = num6 + num4 - 34f * num5;
			if (false && bd != "Normal")
			{
				bx = Badge(bd, bd == "Hardcore" ? "C2213A" : (bd == "Hard" ? "D9772B" : (bd == "Story" ? "5BB3E8" : "4CAF50")), bx, num7 - 6f * num5, num5, num);
			}
			if (bm != "Standard")
			{
				Badge(bm, bm == "Sandbox" ? "C9A227" : "7A4FA8", bx, num7 - 6f * num5, num5, num);
			}
		}
		if (!flag)
		{
			int num20 = BossCard.GoldensOnSlot(button.SaveSlotIndex);
			GuiArt.Img img = Art.Image("fileselect/goldberry");
			GuiArt.Img img2 = Art.Image("fileselect/flag");
			if (num20 > 0 && img != null)
			{
				GUI.color = new Color(1f, 1f, 1f, num * 0.9f);
				GUI.DrawTexture(new Rect(num6, num7, num4, num3), img.Tex);
			}
			if (Txt(button.completionText).StartsWith("100") && img2 != null)
			{
				GUI.color = new Color(1f, 1f, 1f, num * 0.9f);
				GUI.DrawTexture(new Rect(num6, num7, num4, num3), img2.Tex);
			}
		}
		if (sl.ShineT >= 0f)
		{
			int num21 = Mathf.Clamp((int)(sl.ShineT / 0.1f), 0, 5);
			GuiArt.Img img3 = Art.Image("fileselect/cardShine00" + num21);
			if (img3 != null)
			{
				GUI.color = new Color(1f, 1f, 1f, num);
				GUI.DrawTexture(new Rect(num6, num7, num4, num3), img3.Tex);
			}
		}
		GuiArt.Img img4 = Art.Image("portrait/madeline");
		GuiArt.Img img5 = Art.Image("fileselect/portraitOverlay");
		float num22 = 200f * num5;
		float num23 = num6 + 52f * num5;
		float num24 = num7 + num3 / 2f - num22 / 2f;
		if (img4 != null)
		{
			GUI.color = new Color(1f, 1f, 1f, num * (flag ? 0.4f : 1f));
			GUI.DrawTexture(new Rect(num23, num24, num22, num22), img4.Tex);
		}
		if (img5 != null)
		{
			float num25 = num22 * 1.36f;
			GUI.color = new Color(1f, 1f, 1f, num);
			GUI.DrawTexture(new Rect(num23 + num22 / 2f - num25 / 2f, num24 + num22 / 2f - num25 / 2f, num25, num25), img5.Tex);
		}
		float num26 = (num23 + num22 + num6 + num4 - 40f * num5) / 2f;
		float num27 = 0.9f * num5;
		float num28 = 0.62f * num5;
		if (flag)
		{
			float num29 = 46f * num5;
			GUI.color = new Color(c.r, c.g, c.b, num * 0.55f);
			GUI.DrawTexture(new Rect(num26 - num29 / 2f, num7 + num3 / 2f - (float)Art.LineHeight * num27 / 2f - num29 - 8f * num5, num29, num29), FileIcons.NewFile);
			Centered("New Game", num26, num7 + num3 / 2f - (float)Art.LineHeight * num27 / 2f, num27, c);
			Centered("FILE " + button.SaveSlotIndex, num26, num7 + num3 / 2f + (float)Art.LineHeight * num27 * 0.55f, num28 * 0.8f, c2);
			if (num2 > 0.3f)
			{
				Color c3 = new Color(c2.r, c2.g, c2.b, num * Mathf.Clamp01((num2 - 0.3f) * 2f) * (0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 2.5f)));
				Centered("A new climb begins", num26, num7 + num3 / 2f + (float)Art.LineHeight * num27 * 1.25f, num28 * 0.6f, c3);
			}
		}
		else
		{
			float num30 = num7 + num3 * 0.16f;
			Centered("Madeline", num26, num30, num27, c);
			string line3 = Txt(button.locationText);
			float num31 = num28;
			while (num31 > 0.25f * num5 && Art.Measure(line3) * num31 > num4 * 0.55f)
			{
				num31 -= 0.02f * num5;
			}
			float num32 = (float)Art.LineHeight * num31 * 0.95f;
			float num33 = num26 - (Art.Measure(line3) * num31 + num32 + 6f * num5) / 2f;
			GUI.color = new Color(c2.r, c2.g, c2.b, num);
			GUI.DrawTexture(new Rect(num33, num30 + (float)Art.LineHeight * num27, num32, num32), FileIcons.Pin);
			Art.DrawLine(line3, num33 + num32 + 6f * num5, num30 + (float)Art.LineHeight * num27, num31, c2);
			int num34 = BossCard.GoldensOnSlot(button.SaveSlotIndex);
			GuiArt.Img img6 = BossCard.PixelBerry() ?? Art.Image("collectables/goldberry");
			string text4 = "x" + num34;
			float num35 = 70f * num5;
			float num36 = 0.85f * num5;
			float num37 = num35 + 8f * num5 + Art.Measure(text4) * num36;
			float num38 = num26 - num37 / 2f;
			float num39 = num30 + (float)Art.LineHeight * (num27 + num31) + 6f * num5;
			if (img6 != null)
			{
				GUI.color = new Color(1f, 1f, 1f, num * ((num34 > 0) ? 1f : 0.35f));
				GUI.DrawTexture(new Rect(num38, num39, num35, num35), img6.Tex);
			}
			Outlined(text4, num38 + num35 + 8f * num5, num39 + num35 / 2f, num36, num);
		}
		GUI.color = Color.white;
	}

	// Madeline herself, in the look you have on, sitting on top of the chosen file: she idles and looks about, and
	// on a new file she waits, bouncing on her toes (2026-10-17)
	private float climberIdleAt;

	private string climberPose = "idle";

	private void DrawClimber(Slot sl, Rect card)
	{
		float a = screenA * cardAlpha * Mathf.Clamp01((sl.Sel - 0.5f) * 2f);
		if (a <= 0.01f)
		{
			return;
		}
		bool empty = sl.Button.State == SaveSlotButton.SlotState.EmptySlot;
		float t = Time.unscaledTime;
		if (t > climberIdleAt)
		{
			string[] idles = empty ? new string[] { "idle", "lookUp", "idle" } : new string[] { "idle", "idleA", "idleB", "idleC", "idle" };
			climberPose = idles[(int)(Hash((int)t, 17) * idles.Length) % idles.Length];
			climberIdleAt = t + 2.5f + Hash((int)t, 18) * 2.5f;
		}
		float scale = card.height / 270f * 3.2f;
		Vector2 feet = new Vector2(card.xMax - card.width * 0.16f, card.y + 4f * scale);
		if (empty)
		{
			feet.y -= Mathf.Abs(Mathf.Sin(t * 4f)) * 6f;
		}
		VariantPreview.Pose(climberPose, feet, scale, flip: true, a);
	}

	// a small rounded label hanging off the card's top edge, right to left; returns where the next one goes
	private static float Badge(string text, string hex, float right, float y, float k, float a)
	{
		float s = 0.4f * k;
		float w = Art.Measure(text) * s + 26f * k;
		float h = (float)Art.LineHeight * s + 10f * k;
		Rect r = new Rect(right - w, y - h / 2f, w, h);
		GUI.color = new Color(0f, 0f, 0f, 0.35f * a);
		GUI.DrawTexture(new Rect(r.x + 3f, r.y + 4f, r.width, r.height), Texture2D.whiteTexture);
		Color c = Hex(hex);
		GUI.color = new Color(c.r, c.g, c.b, a);
		GUI.DrawTexture(r, Texture2D.whiteTexture);
		Art.DrawLine(text, r.x + 13f * k, r.y + 4f * k, s, new Color(1f, 1f, 1f, a));
		return r.x - 8f * k;
	}

	private static void Centered(string s, float mid, float y, float scale, Color c)
	{
		Art.DrawLine(s, mid - Art.Measure(s) * scale / 2f, y, scale, c);
	}

	private static void Outlined(string s, float x, float y, float scale, float a)
	{
		Art.DrawOutlined(s, x, y - (float)Art.LineHeight * scale / 2f, scale, new Color(1f, 1f, 1f, a), new Color(0.2f, 0.16f, 0.2f, a), 3f * scale / 0.8f);
	}

	internal static int Deaths(int slot)
	{
		if (Time.unscaledTime - deathsReadAt > 1f)
		{
			deathsReadAt = Time.unscaledTime;
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			try
			{
				string path = Path.Combine(DeathCounter.DataDir ?? "", "deaths.cfg");
				if (File.Exists(path))
				{
					string[] array = File.ReadAllLines(path);
					for (int i = 0; i < array.Length; i++)
					{
						string[] array2 = array[i].Split(new char[1] { '=' });
						if (array2.Length == 2 && int.TryParse(array2[0], out var result) && int.TryParse(array2[1], out var result2))
						{
							dictionary[result] = result2;
						}
					}
				}
			}
			catch (Exception)
			{
			}
			deaths = dictionary;
		}
		if (!deaths.TryGetValue(slot, out var value))
		{
			return 0;
		}
		return value;
	}

	private static string Txt(Text t)
	{
		if (!(t != null) || !t.gameObject.activeInHierarchy)
		{
			return "";
		}
		return (t.text ?? "").Trim();
	}
}
