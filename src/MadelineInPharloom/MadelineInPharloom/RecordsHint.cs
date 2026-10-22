using System;
using System.Collections.Generic;
using System.Reflection;
using GlobalEnums;
using HarmonyLib;
using InControl;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Once per file, after the first Moss Mother (user 2026-10-22): a card like the Tech Tutorial's with the Records
/// button on it, so players know they can rematch bosses and see their stats. Opening Records closes it.
/// </summary>
public class RecordsHint : MonoBehaviour
{
	private const string FlagId = "records_hint";

	private const float ShowFor = 10f;

	private static bool pending;

	private static float shownAt = -100f;

	private float calmFor;

	private bool padHints;

	/// <summary>Moss Mother was just beaten (not in a rematch): show the card once things calm down.</summary>
	public static void Queue()
	{
		if (!TechTips.Flag(FlagId))
		{
			pending = true;
		}
	}

	/// <summary>Dev: show the card now (a look at it, no flag set).</summary>
	public static void ShowNow()
	{
		shownAt = Time.unscaledTime;
	}

	private static float Age => Time.unscaledTime - shownAt;

	private void Update()
	{
		padHints = TechTipsGui.PadNow;
		GameManager gm = GameManager.SilentInstance;
		HeroController h = HeroController.UnsafeInstance;
		if (gm == null || h == null || gm.IsMenuScene())
		{
			pending = false;
			shownAt = -100f;
			return;
		}
		bool calm = gm.GameState == GameState.PLAYING && !gm.isPaused && !h.controlReqlinquished && !h.cState.transitioning && !h.cState.dead && !BossCard.InFight && !BossCard.RecordsOpen && !AssistMode.IsOpen;
		calmFor = calm ? calmFor + Time.unscaledDeltaTime : 0f;
		if (pending && calmFor > 1.5f)
		{
			pending = false;
			shownAt = Time.unscaledTime;
			TechTips.SetFlag(FlagId);
			Sfx.Play(Sounds.MenuPause.Id, 0.6f);
		}
		if (BossCard.RecordsOpen && Age < ShowFor)
		{
			shownAt = Time.unscaledTime - ShowFor;   // they found it
		}
	}

	private static readonly MethodInfo SkinForControl = AccessTools.Method(typeof(UIButtonSkins), "GetButtonSkinFor", new Type[1] { typeof(InputControlType) });

	private static Sprite glyph;

	private static float glyphAt = -10f;

	// the controller's Share button (Records' button on a pad), as the game draws it
	private static Sprite PadGlyph()
	{
		if (Time.unscaledTime - glyphAt < 1f)
		{
			return glyph;
		}
		glyphAt = Time.unscaledTime;
		glyph = null;
		try
		{
			UIButtonSkins skins = UIManager.instance != null ? UIManager.instance.uiButtonSkins : null;
			if (skins != null && SkinForControl != null)
			{
				glyph = (SkinForControl.Invoke(skins, new object[1] { InputControlType.Share }) as ButtonSkin)?.sprite;
			}
		}
		catch (Exception)
		{
		}
		return glyph;
	}

	private static readonly Color Panel = new Color(0.06f, 0.06f, 0.12f, 1f);

	private static readonly Color Gold = new Color(1f, 0.85f, 0.35f, 1f);

	private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

	private static void Box(Rect r, Color c, float radius)
	{
		GUI.color = Color.white;
		GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, alphaBlend: true, 0f, c, 0f, Math.Min(radius, Math.Min(r.width, r.height) / 2f));
	}

	private void OnGUI()
	{
		GuiArt art = AssistMode.Art;
		float age = Age;
		if (art == null || age >= ShowFor || Event.current.type != EventType.Repaint || TheoCamera.Hiding || AssistMode.IsOpen || BossCard.RecordsOpen)
		{
			return;
		}
		float a = Mathf.Clamp01(age / 0.3f) * Mathf.Clamp01((ShowFor - age) / 0.6f);
		float k = Screen.height / 1080f;
		float sw = Screen.width / k;
		Matrix4x4 old = GUI.matrix;
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(k, k, 1f));
		const float nameScale = 0.55f;
		const float howScale = 0.42f;
		const float pillH = 60f;
		const float inner = 560f;
		string title = "Records";
		string label = "Open Records";
		Sprite g = padHints ? PadGlyph() : null;
		string key = AssistMode.KeyName(AssistOptions.KeyRecords);
		float keyW = g != null ? 52f : art.Measure(key) * 0.5f + 4f;
		float labelW = art.Measure(label) * 0.45f;
		List<string> how = new List<string>();
		foreach (string para in "Rematch any boss you've beaten, and see your times, deaths, golden berries and achievements.".Split('\n'))
		{
			how.AddRange(art.Wrap(para, inner / howScale));
		}
		float headH = art.LineHeight * nameScale + 6f;
		float howH = how.Count * art.LineHeight * howScale;
		float h = 16f + headH + 10f + pillH + 12f + howH + 16f;
		float w0 = inner + 36f;
		Rect card = new Rect(sw / 2f - w0 / 2f, 40f - (1f - Mathf.Clamp01(age / 0.3f)) * 20f, w0, h);
		Box(new Rect(card.x - 3f, card.y - 3f, card.width + 6f, card.height + 6f), new Color(1f, 1f, 1f, 0.18f * a), 16f);
		Box(card, A(Panel, 0.86f * a), 14f);
		float hy = card.y + 14f;
		art.DrawOutlined(title, card.x + 18f, hy, nameScale, A(Color.white, a), A(Color.black, a), 2f);
		// the button, then what it does
		float y = hy + headH + 10f;
		float pillW = 20f + keyW + 20f;
		float rowW = pillW + 18f + labelW;
		float x = card.x + 18f + (inner - rowW) / 2f;
		Rect pill = new Rect(x, y, pillW, pillH);
		Box(pill, A(Gold, a), 12f);
		if (g != null)
		{
			TechTipsGui.DrawSpriteAt(g, new Rect(pill.x + 20f, pill.center.y - 24f, 48f, 48f), A(Color.white, a));
		}
		else
		{
			art.DrawOutlined(key, pill.x + 20f, pill.y + (pillH - art.LineHeight * 0.5f) / 2f, 0.5f, A(new Color(0.1f, 0.08f, 0.12f, 1f), a), new Color(0f, 0f, 0f, 0f), 2f);
		}
		art.DrawOutlined(label, pill.xMax + 18f, y + (pillH - art.LineHeight * 0.45f) / 2f, 0.45f, A(Gold, a), A(Color.black, a), 2f);
		float ty = y + pillH + 12f;
		foreach (string line in how)
		{
			art.DrawOutlined(line, card.x + 18f, ty, howScale, new Color(0.92f, 0.92f, 0.98f, a), A(Color.black, a), 1.5f);
			ty += art.LineHeight * howScale;
		}
		GUI.matrix = old;
		GUI.color = Color.white;
	}
}
