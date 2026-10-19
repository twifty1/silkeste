using UnityEngine;

namespace MadelineInPharloom;

internal static class MenuStyle
{
	private static GuiArt Art => SpeedrunTimer.Art;

	private static Color Hex(string h)
	{
		if (!ColorUtility.TryParseHtmlString("#" + h, out var color))
		{
			return Color.white;
		}
		return color;
	}

	public static float Title(string text, GuiArt.Img icon, float cx, float top, float alpha)
	{
		Color outline = new Color(0f, 0f, 0f, alpha);
		if (icon != null)
		{
			float num = 90f;
			GUI.color = new Color(1f, 1f, 1f, alpha);
			GUI.DrawTexture(new Rect(cx - num / 2f, top, num, num), icon.Tex);
			GUI.color = Color.white;
			top += num + 6f;
		}
		Art.DrawOutlined(text, cx - Art.Measure(text) * 1.2f / 2f, top, 1.2f, new Color(1f, 1f, 1f, alpha), outline, 3f);
		return top + (float)Art.LineHeight * 1.2f + 14f;
	}

	public static float Tabs(string[] names, int current, bool pad, float cx, float top, float alpha, Rect[] rects)
	{
		Color outline = new Color(0f, 0f, 0f, alpha);
		float num = 0.45000002f;
		float num2 = 50f;
		float num3 = 0f;
		for (int i = 0; i < names.Length; i++)
		{
			num3 += Art.Measure(names[i]) * num + ((i > 0) ? num2 : 0f);
		}
		float num4 = cx - num3 / 2f;
		string line = (pad ? "L1" : "Q");
		string line2 = (pad ? "R1" : "E");
		float num5 = num * 0.8f;
		Art.DrawOutlined(line, num4 - 40f - Art.Measure(line) * num5, top + 4f, num5, new Color(0.7f, 0.7f, 0.7f, alpha), outline, 2f);
		Art.DrawOutlined(line2, num4 + num3 + 40f, top + 4f, num5, new Color(0.7f, 0.7f, 0.7f, alpha), outline, 2f);
		Vector2 point = MenuMouse.Gui();
		for (int j = 0; j < names.Length; j++)
		{
			float num6 = Art.Measure(names[j]) * num;
			rects[j] = new Rect(num4 - num2 / 2f, top - 8f, num6 + num2, (float)Art.LineHeight * num + 20f);
			if (j != current && !pad && rects[j].Contains(point))
			{
				GUI.color = new Color(1f, 1f, 1f, 0.06f * alpha);
				GUI.DrawTexture(rects[j], Texture2D.whiteTexture);
				GUI.color = Color.white;
			}
			Color color = ((j == current) ? Hex("84FF54") : new Color(0.55f, 0.55f, 0.55f, 1f));
			color.a = alpha;
			Art.DrawOutlined(names[j], num4, top, num, color, outline, 2f);
			if (j == current)
			{
				GUI.color = color;
				GUI.DrawTexture(new Rect(num4, top + (float)Art.LineHeight * num + 4f, num6, 4f), Texture2D.whiteTexture);
				GUI.color = Color.white;
			}
			num4 += num6 + num2;
		}
		return top + (float)Art.LineHeight * num + 34f;
	}

	public static void Hint(string hint, float cx, float y, float alpha)
	{
		float num = 0.45f;
		Art.DrawOutlined(hint, cx - Art.Measure(hint) * num / 2f, y, num, new Color(1f, 1f, 1f, alpha * 0.8f), new Color(0f, 0f, 0f, alpha), 2f);
	}

	public static void Panel(Rect r, float alpha)
	{
		GUI.color = new Color(1f, 1f, 1f, 0.05f * alpha);
		GUI.DrawTexture(r, Texture2D.whiteTexture);
		GUI.color = new Color(1f, 1f, 1f, 0.12f * alpha);
		GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);
		GUI.color = Color.white;
	}
}
