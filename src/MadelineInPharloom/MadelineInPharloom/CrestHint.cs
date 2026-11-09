using System;
using System.Collections.Generic;
using GlobalEnums;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Players didn't know the two crests dash differently, or that they can swap (user 2026-11-09). The first time she
/// rests at a bench on each file, a card like the Records one says what each does; and while Madeline is on, the
/// game's crest menu describes her dash on each crest instead of Hornet's needle.
/// </summary>
public class CrestHint : MonoBehaviour
{
	private const string FlagId = "crest_hint";

	private const float ShowFor = 14f;

	private static float shownAt = -100f;

	private float benchFor;

	public const string BounceText = "Madeline bounces off what she hits. In the air, a hit gives her dash back.";

	public const string RefillText = "Madeline dashes through what she hits and can dash again right away. The second dash is weaker, then she waits 1 second. Killing an enemy on the ground skips the wait.";

	private static readonly string[] Rows = new string[4]
	{
		"Hunter",
		"Dash through enemies and dash again right away. The second dash is weaker, then you wait 1 second. Kill an enemy on the ground to skip the wait.",
		"Wanderer",
		"Bounce off enemies. In the air, a hit gives your dash back."
	};

	public static void Patch(Harmony h)
	{
		h.Patch(AccessTools.PropertyGetter(typeof(InventoryToolCrest), "Description"), null, new HarmonyMethod(typeof(CrestHint), "DescriptionPostfix"));
	}

	private static void DescriptionPostfix(InventoryToolCrest __instance, ref string __result)
	{
		if (!Plugin.Ready || __instance == null || __instance.CrestData == null)
		{
			return;
		}
		__result = __instance.CrestData.name == "Wanderer" ? BounceText : RefillText;
	}

	/// <summary>Dev: show the card now (a look at it, no flag set).</summary>
	public static void ShowNow()
	{
		shownAt = Time.unscaledTime;
	}

	private static float Age => Time.unscaledTime - shownAt;

	private void Update()
	{
		GameManager gm = GameManager.SilentInstance;
		HeroController h = HeroController.UnsafeInstance;
		if (gm == null || h == null || gm.IsMenuScene() || !Plugin.Ready)
		{
			shownAt = -100f;
			benchFor = 0f;
			return;
		}
		PlayerData pd = h.playerData;
		bool resting = pd != null && pd.atBench && !pd.isInventoryOpen && gm.GameState == GameState.PLAYING && !gm.isPaused && !TechRoom.Active && !BossRematch.Active && !AssistMode.IsOpen && !BossCard.RecordsOpen;
		benchFor = resting ? benchFor + Time.unscaledDeltaTime : 0f;
		if (benchFor > 1f && Age >= ShowFor && TechTips.Slot >= 0 && !TechTips.Flag(FlagId))
		{
			shownAt = Time.unscaledTime;
			TechTips.SetFlag(FlagId);
			Sfx.Play(Sounds.MenuPause.Id, 0.6f);
		}
		// up from the bench, or into the crest menu: they've got it
		if (Age < ShowFor && Age > 0.5f && (pd == null || !pd.atBench || pd.isInventoryOpen))
		{
			shownAt = Time.unscaledTime - ShowFor + 0.6f;
		}
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
		const float headScale = 0.45f;
		const float textScale = 0.42f;
		const float inner = 620f;
		// each crest: its name in gold, then what it does
		List<KeyValuePair<string, bool>> lines = new List<KeyValuePair<string, bool>>();
		for (int i = 0; i < Rows.Length; i += 2)
		{
			lines.Add(new KeyValuePair<string, bool>(Rows[i], true));
			foreach (string l in art.Wrap(Rows[i + 1], inner / textScale))
			{
				lines.Add(new KeyValuePair<string, bool>(l, false));
			}
		}
		List<string> foot = art.Wrap("Change crests at any bench.", inner / textScale);
		float headH = art.LineHeight * nameScale + 6f;
		float bodyH = 0f;
		foreach (KeyValuePair<string, bool> l in lines)
		{
			bodyH += art.LineHeight * (l.Value ? headScale : textScale) + (l.Value ? 6f : 0f);
		}
		float footH = foot.Count * art.LineHeight * textScale;
		float h = 16f + headH + 8f + bodyH + 12f + footH + 16f;
		float w0 = inner + 36f;
		Rect card = new Rect(sw / 2f - w0 / 2f, 40f - (1f - Mathf.Clamp01(age / 0.3f)) * 20f, w0, h);
		Box(new Rect(card.x - 3f, card.y - 3f, card.width + 6f, card.height + 6f), new Color(1f, 1f, 1f, 0.18f * a), 16f);
		Box(card, A(Panel, 0.86f * a), 14f);
		float y = card.y + 14f;
		art.DrawOutlined("Two ways to dash", card.x + 18f, y, nameScale, A(Color.white, a), A(Color.black, a), 2f);
		y += headH + 8f;
		foreach (KeyValuePair<string, bool> l in lines)
		{
			if (l.Value)
			{
				y += 6f;
				art.DrawOutlined(l.Key, card.x + 18f, y, headScale, A(Gold, a), A(Color.black, a), 2f);
				y += art.LineHeight * headScale;
			}
			else
			{
				art.DrawOutlined(l.Key, card.x + 18f, y, textScale, new Color(0.92f, 0.92f, 0.98f, a), A(Color.black, a), 1.5f);
				y += art.LineHeight * textScale;
			}
		}
		y += 12f;
		foreach (string l in foot)
		{
			art.DrawOutlined(l, card.x + 18f, y, textScale, new Color(0.75f, 0.78f, 0.9f, a), A(Color.black, a), 1.5f);
			y += art.LineHeight * textScale;
		}
		GUI.matrix = old;
		GUI.color = Color.white;
	}
}
