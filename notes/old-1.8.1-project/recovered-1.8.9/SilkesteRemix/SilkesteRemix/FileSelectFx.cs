using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using InControl;
using MadelineInPharloom;
using MadelineInPharloom.Sheets;
using UnityEngine;
using UnityEngine.UI;

namespace SilkesteRemix;

internal static class FileSelectFx
{
	public static readonly AssistRow Portraits = new AssistRow
	{
		Id = "file_icons",
		Section = "Controls",
		Label = "File Icons",
		Values = "Your Pick,By Progress",
		Default = "Your Pick",
		Description = "Your Pick: on the save screen press Tab (Y / Triangle on a controller) to cycle the highlighted file's icon through Celeste's cast. By Progress: Madeline's face follows how far each file has got."
	};

	private static readonly (string path, string name)[] Icons = new(string, string)[32]
	{
		("madeline/normal", "Madeline"),
		("madeline/determined", "Determined"),
		("madeline/surprised", "Surprised"),
		("madeline/peaceful", "Peaceful"),
		("madeline/distracted", "Distracted"),
		("madeline/deadpan", "Deadpan"),
		("madeline/angry", "Angry"),
		("madeline/panic", "Panic"),
		("madeline/sad", "Sad"),
		("madeline/together", "Together"),
		("madelineph/normal", "Phone Madeline"),
		("ghost/normal", "Badeline"),
		("ghost/scoff", "Badeline Scoff"),
		("ghost/yell", "Badeline Yell"),
		("ghost/serious", "Badeline Serious"),
		("ghost/freakA", "Badeline Freak"),
		("theo/normal", "Theo"),
		("theo/excited", "Theo Excited"),
		("theo/yolo", "Theo YOLO"),
		("theo/nailedit", "Theo Nailed It"),
		("theo/wtf", "Theo WTF"),
		("theo/thinking", "Theo Thinking"),
		("granny/normal", "Granny"),
		("granny/laugh", "Granny Laugh"),
		("granny/mock", "Granny Mock"),
		("granny/creepA", "Granny Creepy"),
		("oshiro/normal", "Oshiro"),
		("oshiro/sidehappy", "Oshiro Happy"),
		("oshiro/drama", "Oshiro Drama"),
		("oshiro/lostcontrol", "Oshiro Lost It"),
		("mom/normal", "Mom"),
		("exph/normal", "Ex")
	};

	private static readonly Dictionary<int, int> picks = new Dictionary<int, int>();

	private static bool picksLoaded;

	private static readonly Dictionary<int, float> changedAt = new Dictionary<int, float>();

	private const float EnterDur = 0.5f;

	private const float EnterStagger = 0.07f;

	private const float ExitDur = 0.38f;

	private const float ExitStagger = 0.04f;

	private const float Travel = 900f;

	private const float FadeIn = 0.25f;

	private static readonly (int below, string face)[] Faces = new(int, string)[5]
	{
		(15, "surprised"),
		(40, "normal"),
		(70, "determined"),
		(100, "distracted"),
		(101, "peaceful")
	};

	private static bool wasActive;

	private static float enterAt = -10f;

	private static float exitAt = -10f;

	private static readonly List<CelesteFileSelect.Slot> cache = new List<CelesteFileSelect.Slot>();

	private static readonly Dictionary<string, GuiArt.Img> faces = new Dictionary<string, GuiArt.Img>();

	private static string PicksFile => Path.Combine(AssistMode.DataDir ?? "", "remix_file_icons.cfg");

	private static float Now => Time.unscaledTime;

	public static bool Picking
	{
		get
		{
			try
			{
				return AssistMode.Value(Portraits) == "Your Pick";
			}
			catch (Exception)
			{
				return false;
			}
		}
	}

	public static void Patch(Harmony h)
	{
		Type typeFromHandle = typeof(CelesteFileSelect);
		h.Patch(AccessTools.Method(typeFromHandle, "Tick"), null, new HarmonyMethod(typeof(FileSelectFx), "TickPost"));
		h.Patch(AccessTools.Method(typeFromHandle, "Layout"), null, new HarmonyMethod(typeof(FileSelectFx), "LayoutPost"));
		h.Patch(AccessTools.Method(typeFromHandle, "DrawFile"), null, transpiler: new HarmonyMethod(typeof(FileSelectFx), "PortraitSwap"), postfix: new HarmonyMethod(typeof(FileSelectFx), "NameTag"));
	}

	private static float Smooth(float x)
	{
		x = Mathf.Clamp01(x);
		return x * x * (3f - 2f * x);
	}

	private static float OutBack(float x)
	{
		x = Mathf.Clamp01(x);
		float num = x - 1f;
		return 1f + 2.4f * num * num * num + 1.4f * num * num;
	}

	private static void TickPost(CelesteFileSelect __instance)
	{
		if (__instance.active)
		{
			if (!wasActive)
			{
				enterAt = Now;
				exitAt = -10f;
			}
			cache.Clear();
			cache.AddRange(__instance.ordered);
			__instance.screenA = Smooth((Now - enterAt) / 0.25f);
		}
		else if (wasActive)
		{
			exitAt = Now;
		}
		wasActive = __instance.active;
		if (__instance.active)
		{
			PickInput(__instance);
		}
		float num = Now - exitAt;
		if (__instance.active || !(num < 0.53999996f))
		{
			return;
		}
		if (__instance.ordered.Count == 0)
		{
			foreach (CelesteFileSelect.Slot item in cache)
			{
				if (item.Button != null)
				{
					__instance.ordered.Add(item);
				}
			}
		}
		__instance.screenA = 1f - Smooth(num / 0.53999996f);
	}

	private static void LoadPicks()
	{
		if (picksLoaded)
		{
			return;
		}
		picksLoaded = true;
		try
		{
			if (!File.Exists(PicksFile))
			{
				return;
			}
			string[] array = File.ReadAllLines(PicksFile);
			foreach (string text in array)
			{
				string[] kv = text.Split(new char[1] { '=' });
				if (kv.Length == 2 && int.TryParse(kv[0], out var result))
				{
					int num = Array.FindIndex(Icons, ((string path, string name) x) => x.path == kv[1].Trim());
					if (num >= 0)
					{
						picks[result] = num;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("file icons: " + ex.Message);
		}
	}

	private static void SavePicks()
	{
		try
		{
			List<string> list = new List<string>();
			foreach (KeyValuePair<int, int> pick in picks)
			{
				list.Add(pick.Key + "=" + Icons[pick.Value].path);
			}
			File.WriteAllLines(PicksFile, list.ToArray());
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("file icons: " + ex.Message);
		}
	}

	private static void PickInput(CelesteFileSelect fs)
	{
		if (!Picking || fs.confirmOpen || fs.eraseOpen || AssistMode.IsOpen || fs.index < 0 || fs.index >= fs.ordered.Count)
		{
			return;
		}
		Keyboard current = Keyboard.current;
		if (current == null || !current.tabKey.wasPressedThisFrame)
		{
			InputDevice activeDevice = InputManager.ActiveDevice;
			if (activeDevice == null || !activeDevice.Action4.WasPressed)
			{
				return;
			}
		}
		int num;
		if (current != null)
		{
			KeyState obj = current["LeftShift"];
			if (obj == null || !obj.isPressed)
			{
				KeyState obj2 = current["RightShift"];
				num = ((obj2 != null && obj2.isPressed) ? 1 : 0);
			}
			else
			{
				num = 1;
			}
		}
		else
		{
			num = 0;
		}
		bool flag = (byte)num != 0;
		SaveSlotButton button = fs.ordered[fs.index].Button;
		if (button == null || button.State == SaveSlotButton.SlotState.EmptySlot)
		{
			return;
		}
		LoadPicks();
		int saveSlotIndex = button.SaveSlotIndex;
		int num2 = (picks.TryGetValue(saveSlotIndex, out var value) ? value : 0);
		num2 = (num2 + ((!flag) ? 1 : (Icons.Length - 1))) % Icons.Length;
		picks[saveSlotIndex] = num2;
		changedAt[saveSlotIndex] = Now;
		SavePicks();
		try
		{
			Sfx.Play(Sounds.MenuToggleOn.Id);
		}
		catch (Exception)
		{
		}
	}

	private static void NameTag(CelesteFileSelect __instance, CelesteFileSelect.Slot sl)
	{
		try
		{
			SaveSlotButton saveSlotButton = sl?.Button;
			if (Picking && !(saveSlotButton == null) && saveSlotButton.State != SaveSlotButton.SlotState.EmptySlot && !(sl.Sel < 0.5f) && __instance.cardRects.TryGetValue(saveSlotButton, out var value))
			{
				GuiArt art = SpeedrunTimer.Art;
				LoadPicks();
				int value2;
				int num = (picks.TryGetValue(saveSlotButton.SaveSlotIndex, out value2) ? value2 : 0);
				float num2 = __instance.screenA * Mathf.Clamp01((sl.Sel - 0.5f) * 2f);
				float value3;
				float num3 = (changedAt.TryGetValue(saveSlotButton.SaveSlotIndex, out value3) ? (1f + 0.25f * Mathf.Clamp01(1f - (Now - value3) / 0.25f)) : 1f);
				string line = Icons[num].name + "   " + (num + 1) + "/" + Icons.Length;
				float num4 = 0.34f * num3;
				float num5 = art.Measure(line) * num4 + 30f;
				float height = (float)art.LineHeight * num4 + 10f;
				float num6 = value.height / 270f;
				float num7 = value.x + 152f * num6 - num5 / 2f;
				float num8 = value.yMax + 4f;
				GUI.color = new Color(0.035f, 0.04f, 0.065f, 0.6f * num2);
				GUI.DrawTexture(new Rect(num7, num8, num5, height), Texture2D.whiteTexture);
				GUI.color = Color.white;
				art.DrawOutlined(line, num7 + 15f, num8 + 5f, num4, new Color(0.97f, 0.94f, 0.86f, num2), new Color(0.035f, 0.04f, 0.065f, num2), 2f);
			}
		}
		catch (Exception)
		{
		}
	}

	private static void LayoutPost(int i, ref Rect __result)
	{
		float num = 0f;
		float num2 = Now - enterAt - (float)i * 0.07f;
		if (num2 < 0.5f)
		{
			num += (1f - OutBack(num2 / 0.5f)) * 900f;
		}
		float num3 = Now - exitAt - (float)i * 0.04f;
		if (exitAt > 0f && num3 > 0f)
		{
			num += Smooth(num3 / 0.38f) * 900f;
		}
		__result.x += num;
	}

	private static IEnumerable<CodeInstruction> PortraitSwap(IEnumerable<CodeInstruction> code)
	{
		List<CodeInstruction> list = new List<CodeInstruction>(code);
		MethodInfo pick = AccessTools.Method(typeof(FileSelectFx), "PortraitFor");
		for (int i = 0; i < list.Count; i++)
		{
			yield return list[i];
			if (i > 0 && list[i - 1].opcode == OpCodes.Ldstr && list[i - 1].operand as string == "portrait/madeline" && (list[i].opcode == OpCodes.Callvirt || list[i].opcode == OpCodes.Call))
			{
				yield return new CodeInstruction(OpCodes.Ldarg_1);
				yield return new CodeInstruction(OpCodes.Call, pick);
			}
		}
	}

	public static GuiArt.Img PortraitFor(GuiArt.Img normal, CelesteFileSelect.Slot sl)
	{
		try
		{
			if (sl?.Button == null || sl.Button.State == SaveSlotButton.SlotState.EmptySlot)
			{
				return normal;
			}
			if (AssistMode.Value(Portraits) == "Your Pick")
			{
				LoadPicks();
				int value;
				return picks.TryGetValue(sl.Button.SaveSlotIndex, out value) ? (Face(Icons[value].path) ?? normal) : normal;
			}
			string text = CelesteFileSelect.Txt(sl.Button.completionText);
			int num = 0;
			string text2 = text;
			foreach (char c in text2)
			{
				if (char.IsDigit(c))
				{
					num = num * 10 + (c - 48);
				}
				else if (num > 0)
				{
					break;
				}
			}
			string face = "madeline/normal";
			(int, string)[] array = Faces;
			for (int i = 0; i < array.Length; i++)
			{
				(int, string) tuple = array[i];
				if (num < tuple.Item1)
				{
					face = "madeline/" + tuple.Item2;
					break;
				}
			}
			return Face(face) ?? normal;
		}
		catch (Exception)
		{
			return normal;
		}
	}

	private static GuiArt.Img Face(string face)
	{
		if (faces.TryGetValue(face, out var value))
		{
			return value;
		}
		faces[face] = null;
		try
		{
			CelesteAssets.Page page = CelesteAssets.DecodeData(Path.Combine(Plugin.FindCeleste(Path.Combine(AssistMode.DataDir ?? "", "settings.cfg")), "Content/Graphics/Atlases/Portraits/" + face + "00.data"));
			Texture2D texture2D = new Texture2D(page.W, page.H, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp
			};
			byte[] array = new byte[page.Rgba.Length];
			int num = page.W * 4;
			for (int i = 0; i < page.H; i++)
			{
				Buffer.BlockCopy(page.Rgba, i * num, array, (page.H - 1 - i) * num, num);
			}
			texture2D.LoadRawTextureData(array);
			texture2D.Apply();
			value = new GuiArt.Img
			{
				W = page.W,
				H = page.H,
				Tex = texture2D
			};
			faces[face] = value;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("file portrait " + face + ": " + ex.Message);
		}
		return value;
	}
}
