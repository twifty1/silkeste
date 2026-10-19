using System;
using HarmonyLib;
using MadelineInPharloom;
using UnityEngine;

namespace SilkesteRemix;

internal static class MenuHints
{
	private static readonly Color Cream = new Color(0.97f, 0.94f, 0.86f);

	private static readonly Color Muted = new Color(0.78f, 0.75f, 0.68f);

	private static readonly Color Ink = new Color(0.035f, 0.04f, 0.065f);

	public static void Patch(Harmony h)
	{
		h.Patch(AccessTools.Method(typeof(CelesteMenus), "DrawHints"), new HarmonyMethod(typeof(MenuHints), "DrawMenu"));
		h.Patch(AccessTools.Method(typeof(CelesteFileSelect), "Hints"), new HarmonyMethod(typeof(MenuHints), "DrawFiles"));
	}

	private static bool DrawMenu(CelesteMenus __instance, float sw)
	{
		bool padActive = MenuMouse.PadActive;
		return !Row((!(__instance.kind == "title")) ? new(string, string)[2]
		{
			("Confirm", padActive ? "Cross" : "Enter"),
			("Back", padActive ? "Circle" : "Esc")
		} : new(string, string)[1] { ("Confirm", padActive ? "Cross" : "Enter") }, sw, 1f);
	}

	private static bool DrawFiles(CelesteFileSelect __instance, float sw, float a)
	{
		bool padActive = MenuMouse.PadActive;
		return !Row((!__instance.confirmOpen) ? ((!FileSelectFx.Picking) ? new(string, string)[3]
		{
			("Confirm", padActive ? "Cross" : "Enter"),
			("Erase", padActive ? "Square" : "Del"),
			("Back", padActive ? "Circle" : "Esc")
		} : new(string, string)[4]
		{
			("Confirm", padActive ? "Cross" : "Enter"),
			("Icon", padActive ? "Triangle" : "Tab"),
			("Erase", padActive ? "Square" : "Del"),
			("Back", padActive ? "Circle" : "Esc")
		}) : new(string, string)[2]
		{
			("Confirm", padActive ? "Cross" : "Enter"),
			("Back", padActive ? "Circle" : "Esc")
		}, sw, a);
	}

	private static Color A(Color c, float a)
	{
		return new Color(c.r, c.g, c.b, c.a * a);
	}

	private static bool Row((string label, string key)[] hints, float sw, float a)
	{
		try
		{
			GuiArt art = SpeedrunTimer.Art;
			if (art == null)
			{
				return false;
			}
			float num = sw - 54f;
			Color color = GUI.color;
			for (int num2 = hints.Length - 1; num2 >= 0; num2--)
			{
				(string label, string key) tuple = hints[num2];
				string item = tuple.label;
				string item2 = tuple.key;
				float num3 = Math.Max(40f, art.Measure(item2) * 0.36f + 28f);
				num -= num3;
				GUI.color = A(Ink, 0.6f * a);
				GUI.DrawTexture(new Rect(num, 1004f, num3, 40f), Texture2D.whiteTexture);
				GUI.color = A(Cream, 0.7f * a);
				GUI.DrawTexture(new Rect(num, 1004f, num3, 2f), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(num, 1042f, num3, 2f), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(num, 1004f, 2f, 40f), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(num + num3 - 2f, 1004f, 2f, 40f), Texture2D.whiteTexture);
				GUI.color = A(Cream, 0.25f * a);
				GUI.DrawTexture(new Rect(num + 2f, 1006f, num3 - 4f, 3f), Texture2D.whiteTexture);
				GUI.color = color;
				art.DrawOutlined(item2, num + (num3 - art.Measure(item2) * 0.36f) / 2f, 1004f + (40f - (float)art.LineHeight * 0.36f) / 2f, 0.36f, A(Cream, a), A(Ink, 0.8f * a), 2f);
				num -= 12f + art.Measure(item) * 0.4f;
				art.DrawOutlined(item, num, 1004f + (40f - (float)art.LineHeight * 0.4f) / 2f, 0.4f, A(Muted, a), A(Ink, 0.7f * a), 2f);
				num -= 34f;
			}
			GUI.color = color;
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}
}
