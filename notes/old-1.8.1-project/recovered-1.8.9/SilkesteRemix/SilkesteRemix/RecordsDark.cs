using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MadelineInPharloom;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace SilkesteRemix;

internal static class RecordsDark
{
	public static readonly AssistRow Style = new AssistRow
	{
		Id = "records_style",
		Section = "Controls",
		Label = "Records Style",
		Values = "Dark,Light",
		Default = "Dark",
		Description = "Dark: the records journal on a dark page with light ink. Light: the original paper page."
	};

	private static readonly Color Cream = new Color(0.95f, 0.92f, 0.86f);

	private static readonly Color Muted = new Color(0.7f, 0.67f, 0.64f);

	private static readonly Color PageTint = new Color(0.2f, 0.19f, 0.25f);

	private static readonly Color Card = new Color(0.12f, 0.11f, 0.16f);

	private static readonly MethodInfo DrawLine = AccessTools.Method(typeof(GuiArt), "DrawLine");

	private static readonly MethodInfo DrawImg = AccessTools.Method(typeof(GuiArt), "Draw", new Type[3]
	{
		typeof(GuiArt.Img),
		typeof(Rect),
		typeof(Color)
	});

	private static readonly MethodInfo DrawTex = AccessTools.Method(typeof(GUI), "DrawTexture", new Type[2]
	{
		typeof(Rect),
		typeof(Texture)
	});

	private static bool On
	{
		get
		{
			if (!BossCard.RecordsOpen)
			{
				return false;
			}
			try
			{
				return AssistMode.Value(Style) == "Dark";
			}
			catch (Exception)
			{
				return false;
			}
		}
	}

	public static void Patch(Harmony h)
	{
		HarmonyMethod transpiler = new HarmonyMethod(typeof(RecordsDark), "Swap");
		Type[] array = new Type[2]
		{
			typeof(BossCard),
			typeof(Achievements)
		};
		for (int i = 0; i < array.Length; i++)
		{
			MethodInfo[] methods = array[i].GetMethods(AccessTools.allDeclared);
			foreach (MethodInfo methodInfo in methods)
			{
				if (methodInfo.IsAbstract || methodInfo.ContainsGenericParameters)
				{
					continue;
				}
				try
				{
					bool flag = false;
					foreach (CodeInstruction originalInstruction in PatchProcessor.GetOriginalInstructions(methodInfo))
					{
						if (originalInstruction.operand is MethodInfo methodInfo2 && (methodInfo2 == DrawLine || methodInfo2 == DrawImg || methodInfo2 == DrawTex))
						{
							flag = true;
							break;
						}
					}
					if (flag)
					{
						h.Patch(methodInfo, null, null, transpiler);
					}
				}
				catch (Exception)
				{
				}
			}
		}
	}

	private static IEnumerable<CodeInstruction> Swap(IEnumerable<CodeInstruction> code)
	{
		foreach (CodeInstruction item in code)
		{
			string text = (item.Calls(DrawLine) ? "RecLine" : (item.Calls(DrawImg) ? "RecImg" : (item.Calls(DrawTex) ? "RecTex" : null)));
			yield return (text == null) ? item : new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RecordsDark), text)).MoveLabelsFrom(item).MoveBlocksFrom(item);
		}
	}

	private static float Lum(Color c)
	{
		return 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
	}

	private static float Sat(Color c)
	{
		return Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));
	}

	private static Color Lighten(Color c)
	{
		float a = c.a;
		Color result = ((!(Sat(c) < 0.15f)) ? ((Lum(c) < 0.5f) ? Color.Lerp(c, Color.white, 0.45f) : c) : ((Lum(c) < 0.25f) ? Cream : ((Lum(c) < 0.55f) ? Muted : c)));
		result.a = a;
		return result;
	}

	public static void RecLine(GuiArt art, string line, float x, float top, float scale, Color c)
	{
		art.DrawLine(line, x, top, scale, On ? Lighten(c) : c);
	}

	public static void RecImg(GuiArt.Img img, Rect r, Color c)
	{
		if (On && img != null && BossCard.Art != null && img == BossCard.Art.Image("journal/page"))
		{
			c = new Color(c.r * PageTint.r, c.g * PageTint.g, c.b * PageTint.b, c.a);
		}
		GuiArt.Draw(img, r, c);
	}

	public static void RecTex(Rect r, Texture tex)
	{
		if (!On || tex != Texture2D.whiteTexture)
		{
			GUI.DrawTexture(r, tex);
			return;
		}
		Color color = GUI.color;
		bool flag = r.height <= 4f || r.width <= 6f;
		if (Sat(color) < 0.4f && Lum(color) > 0.7f && !flag)
		{
			GUI.color = new Color(color.r * 0.22f + Card.r * 0.3f, color.g * 0.22f + Card.g * 0.3f, color.b * 0.22f + Card.b * 0.3f, color.a);
		}
		else if (flag && Sat(color) < 0.15f && Lum(color) < 0.55f && color.a > 0f)
		{
			GUI.color = new Color(Muted.r, Muted.g, Muted.b, color.a);
		}
		GUI.DrawTexture(r, tex);
		GUI.color = color;
	}
}
