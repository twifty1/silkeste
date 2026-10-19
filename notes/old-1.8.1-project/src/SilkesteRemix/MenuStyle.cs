using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MadelineInPharloom;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace SilkesteRemix;

internal static class MenuStyle
{
	public static readonly AssistRow Style = new AssistRow
	{
		Id = "menu_style",
		Section = "Controls",
		Label = "Menu Style",
		Values = "Silksong,Classic",
		Default = "Silksong",
		Description = "Silksong: dark glass panel, cream text and gold highlights. Classic: the original green menu."
	};

	private static readonly Color Cream = new Color(0.97f, 0.94f, 0.86f);

	private static readonly Color Muted = new Color(0.72f, 0.69f, 0.62f);

	private static readonly Color Dim = new Color(0.5f, 0.48f, 0.44f);

	private static readonly Color Gold = new Color(1f, 0.82f, 0.4f);

	private static readonly Color GoldFlash = new Color(1f, 0.93f, 0.7f);

	private static readonly Color Ink = new Color(0.035f, 0.04f, 0.065f);

	private static Texture2D vignette;

	private static Texture2D rowFade;

	private static bool On
	{
		get
		{
			try
			{
				return AssistMode.Value(Style) == "Silksong";
			}
			catch (Exception)
			{
				return false;
			}
		}
	}

	public static void Patch(Harmony h)
	{
		h.Patch(AccessTools.Method(typeof(AssistMode), "OnGUI"), null, null, new HarmonyMethod(typeof(MenuStyle), "Swap"));
	}

	private static IEnumerable<CodeInstruction> Swap(IEnumerable<CodeInstruction> code)
	{
		MethodInfo hex = AccessTools.Method(typeof(AssistMode), "Hex", new Type[1] { typeof(string) });
		MethodInfo drawText = AccessTools.Method(typeof(GuiArt), "DrawOutlined");
		MethodInfo drawTex = AccessTools.Method(typeof(GUI), "DrawTexture", new Type[2]
		{
			typeof(Rect),
			typeof(Texture)
		});
		foreach (CodeInstruction item in code)
		{
			MethodInfoOr(item, hex, "MenuHex", out var swapped);
			if (swapped != null)
			{
				yield return swapped;
				continue;
			}
			MethodInfoOr(item, drawText, "MenuText", out var swapped2);
			if (swapped2 != null)
			{
				yield return swapped2;
				continue;
			}
			MethodInfoOr(item, drawTex, "MenuTex", out var swapped3);
			if (swapped3 != null)
			{
				yield return swapped3;
			}
			else
			{
				yield return item;
			}
		}
	}

	private static void MethodInfoOr(CodeInstruction ci, MethodInfo target, string mine, out CodeInstruction swapped)
	{
		swapped = ((target != null && ci.Calls(target)) ? new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MenuStyle), mine)).MoveLabelsFrom(ci).MoveBlocksFrom(ci) : null);
	}

	public static Color MenuHex(string h)
	{
		if (On)
		{
			if (h == "84FF54")
			{
				return Gold;
			}
			if (h == "FCFF59")
			{
				return GoldFlash;
			}
		}
		return AssistMode.Hex(h);
	}

	private static bool Near(float a, float b)
	{
		return Math.Abs(a - b) < 0.02f;
	}

	public static void MenuText(GuiArt art, string line, float x, float top, float scale, Color c, Color outline, float stroke)
	{
		if (On)
		{
			if (Near(c.r, 1f) && Near(c.g, 1f) && Near(c.b, 1f))
			{
				c = new Color(Cream.r, Cream.g, Cream.b, c.a);
			}
			else if (Near(c.r, c.g) && Near(c.g, c.b) && c.r < 0.8f)
			{
				c = new Color((c.r < 0.6f) ? Dim.r : Muted.r, (c.r < 0.6f) ? Dim.g : Muted.g, (c.r < 0.6f) ? Dim.b : Muted.b, c.a);
			}
			if (outline.r < 0.05f && outline.g < 0.05f && outline.b < 0.05f)
			{
				outline = new Color(Ink.r, Ink.g, Ink.b, outline.a);
			}
			if (line == "SETTINGS")
			{
				Ornament(art, x, top, scale, c.a);
			}
		}
		art.DrawOutlined(line, x, top, scale, c, outline, stroke);
	}

	private static void Ornament(GuiArt art, float x, float top, float scale, float a)
	{
		float num = art.Measure("SETTINGS") * scale;
		float num2 = top + (float)art.LineHeight * scale / 2f;
		Color color = GUI.color;
		GUI.color = new Color(Cream.r, Cream.g, Cream.b, 0.6f * a);
		GUI.DrawTexture(new Rect(x - 260f, num2 - 1f, 220f, 2f), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(x + num + 40f, num2 - 1f, 220f, 2f), Texture2D.whiteTexture);
		GUI.color = new Color(Gold.r, Gold.g, Gold.b, a);
		Diamond(x - 26f, num2, 7f);
		Diamond(x + num + 26f, num2, 7f);
		GUI.color = color;
	}

	private static void Diamond(float cx, float cy, float r)
	{
		Matrix4x4 matrix = GUI.matrix;
		GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy));
		GUI.DrawTexture(new Rect(cx - r / 1.414f, cy - r / 1.414f, r * 1.414f, r * 1.414f), Texture2D.whiteTexture);
		GUI.matrix = matrix;
	}

	public static void MenuTex(Rect r, Texture tex)
	{
		if (!On || tex != Texture2D.whiteTexture)
		{
			GUI.DrawTexture(r, tex);
			return;
		}
		Color color = GUI.color;
		if (r.x == 0f && r.y == 0f && r.height >= 1079f && color.r < 0.05f && color.a > 0f)
		{
			float num = color.a / 0.75f;
			GUI.color = new Color(Ink.r, Ink.g, Ink.b, 0.8f * num);
			GUI.DrawTexture(r, Texture2D.whiteTexture);
			GUI.color = new Color(0f, 0f, 0f, 0.55f * num);
			GUI.DrawTexture(r, Vignette());
			float num2 = r.width / 2f;
			float num3 = Math.Min(1260f, r.width - 40f);
			GUI.color = new Color(1f, 1f, 1f, 0.035f * num);
			GUI.DrawTexture(new Rect(num2 - num3 / 2f, 40f, num3, 1000f), Texture2D.whiteTexture);
			GUI.color = new Color(Cream.r, Cream.g, Cream.b, 0.35f * num);
			GUI.DrawTexture(new Rect(num2 - num3 / 2f, 40f, num3, 2f), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(num2 - num3 / 2f, 1038f, num3, 2f), Texture2D.whiteTexture);
			GUI.color = new Color(Cream.r, Cream.g, Cream.b, 0.18f * num);
			GUI.DrawTexture(new Rect(num2 - num3 / 2f, 40f, 2f, 1000f), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(num2 + num3 / 2f - 2f, 40f, 2f, 1000f), Texture2D.whiteTexture);
			GUI.color = color;
		}
		else if (Near(color.r, 1f) && Near(color.g, 1f) && Near(color.b, 1f) && color.a < 0.2f && r.width > 600f)
		{
			float num4 = color.a / 0.07f;
			GUI.color = new Color(Gold.r, Gold.g, Gold.b, 0.16f * num4);
			GUI.DrawTexture(r, RowFade());
			GUI.color = new Color(Gold.r, Gold.g, Gold.b, 0.9f * num4);
			GUI.DrawTexture(new Rect(r.x, r.y + 4f, 4f, r.height - 8f), Texture2D.whiteTexture);
			GUI.color = color;
		}
		else
		{
			if (color.r < 0.05f && color.g < 0.05f && color.b < 0.05f && r.width < 60f)
			{
				GUI.color = new Color(Ink.r, Ink.g, Ink.b, color.a);
			}
			GUI.DrawTexture(r, tex);
			GUI.color = color;
		}
	}

	private static Texture2D Vignette()
	{
		if (vignette != null)
		{
			return vignette;
		}
		vignette = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear
		};
		for (int i = 0; i < 64; i++)
		{
			for (int j = 0; j < 64; j++)
			{
				float num = ((float)j + 0.5f) / 64f * 2f - 1f;
				float num2 = ((float)i + 0.5f) / 64f * 2f - 1f;
				vignette.SetPixel(j, i, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Sqrt(num * num + num2 * num2) - 0.45f) / 0.75f))));
			}
		}
		vignette.Apply();
		return vignette;
	}

	private static Texture2D RowFade()
	{
		if (rowFade != null)
		{
			return rowFade;
		}
		rowFade = new Texture2D(128, 1, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear
		};
		for (int i = 0; i < 128; i++)
		{
			rowFade.SetPixel(i, 0, new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.15f, (float)i / 127f)));
		}
		rowFade.Apply();
		return rowFade;
	}
}
