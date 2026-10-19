using System;
using System.Collections.Generic;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Settings, Variants and Challenges (user 2026-10-17): a tiny looping Madeline beside the description, acting out
/// what the chosen option does: hiccuping, sliding on ice, floating, blown by wind, steering a dash, vanishing...
/// Drawn from the same Celeste frames as the Customization preview, in the look you have on.
/// </summary>
public static class VariantPreview
{
	// what each option acts out
	private static readonly Dictionary<string, string> Acts = new Dictionary<string, string>
	{
		{ "hiccups", "hiccup" }, { "low_friction", "slide" }, { "gravity", "gravity" }, { "wind", "wind" },
		{ "mirror_mode", "mirror" }, { "super_dashing", "steer" }, { "invisible_motion", "vanish" }, { "bouncy", "bounce" },
		{ "madeline_speed", "zoom" }, { "jump_count", "jumps" }, { "dash_length", "dashlen" }, { "dash_count", "dashes" },
		{ "no_grabbing", "nograb" }, { "no_climb_jump", "nograb" }, { "no_wall_jump", "nograb" }, { "no_wall_slide", "wallfall" },
		{ "half_stamina", "climb" }, { "one_hit", "onehit" }, { "infinite_stamina", "climb" }, { "game_speed", "slowmo" },
		{ "forgiving_jumps", "coyote" }, { "air_dashes", "dashes" }, { "invincibility", "onehit" }
	};

	public static bool Has(string id)
	{
		return id != null && Acts.ContainsKey(id);
	}

	/// <summary>The little scene for option id with value val, in box (GUI units).</summary>
	public static void Draw(string id, string val, Rect box, float alpha, Color tint)
	{
		if (!Acts.TryGetValue(id, out var act) || Plugin.PreviewAssets == null)
		{
			return;
		}
		SkinRow skin = Array.Find(Skins.All, (SkinRow k) => k.Name == AssistMode.SkinName);
		if (skin.Id == null)
		{
			skin = Skins.Madeline;
		}
		CelesteAssets assets;
		try
		{
			assets = Plugin.PreviewAssets(skin);
		}
		catch
		{
			return;
		}
		if (assets == null)
		{
			return;
		}
		// the stage: a dark box, a floor, and for wall acts a wall on the right
		GUI.color = new Color(0f, 0f, 0f, 0.35f * alpha);
		GUI.DrawTexture(box, Texture2D.whiteTexture);
		float floor = box.yMax - 10f;
		GUI.color = new Color(tint.r, tint.g, tint.b, 0.6f * alpha);
		GUI.DrawTexture(new Rect(box.x + 6f, floor, box.width - 12f, 2f), Texture2D.whiteTexture);
		float s = 2.2f;
		float t = Time.unscaledTime;
		float x = box.center.x;
		float y = floor;
		string pose = "idle";
		bool flip = false;
		float a = 1f;
		float p = Mathf.Repeat(t, 2f) / 2f;   // 0..1 every two seconds
		float w = box.width / 2f - 26f;
		switch (act)
		{
		case "hiccup":
		{
			float h = Mathf.Repeat(t, 1.3f);
			float k = Mathf.Clamp01(h / 0.45f);
			y -= Mathf.Sin(k * Mathf.PI) * 34f;
			x += (Mathf.Floor(t / 1.3f) % 2f == 0f ? 1f : -1f) * k * 18f;
			pose = h < 0.45f ? "jumpFast" : "idle";
			break;
		}
		case "slide":
			x = box.x + 26f + Mathf.SmoothStep(0f, 1f, Mathf.PingPong(t * 0.6f, 1f)) * w * 2f;
			pose = "duck";
			break;
		case "gravity":
		{
			float g = val == "Low" ? 0.55f : (val == "High" ? 1.6f : 1f);
			float dur = 1.2f / Mathf.Sqrt(g);
			float k = Mathf.Repeat(t, dur + 0.4f) / dur;
			if (k <= 1f)
			{
				y -= 4f * k * (1f - k) * 44f / g;
				pose = k < 0.5f ? "jumpFast" : "fallFast";
			}
			break;
		}
		case "wind":
		{
			float dir = val.Contains("Left") ? -1f : 1f;
			float strong = val.Contains("Strong") ? 1.8f : 1f;
			x = box.center.x + Mathf.Sin(t * 1.4f) * 10f + dir * 8f * strong;
			pose = "runFast";
			flip = dir > 0f;
			for (int i = 0; i < 4; i++)
			{
				float lx = Mathf.Repeat(t * 90f * strong * dir + i * 47f, box.width - 30f);
				if (dir < 0f)
				{
					lx = box.width - 30f - lx;
				}
				GUI.color = new Color(1f, 1f, 1f, 0.35f * alpha);
				GUI.DrawTexture(new Rect(box.x + 15f + lx, box.y + 14f + i * 16f, 20f, 2f), Texture2D.whiteTexture);
			}
			break;
		}
		case "mirror":
			flip = Mathf.Repeat(t, 2f) < 1f;
			x = box.center.x + (flip ? -1f : 1f) * Mathf.PingPong(t * 40f, 30f);
			pose = "runFast";
			GUI.color = new Color(1f, 1f, 1f, 0.25f * alpha);
			GUI.DrawTexture(new Rect(box.center.x - 1f, box.y + 6f, 2f, box.height - 16f), Texture2D.whiteTexture);
			break;
		case "steer":
		{
			float k = Mathf.Repeat(t, 1.6f) / 1.6f;
			x = box.x + 26f + k * w * 2f;
			y = floor - 20f - Mathf.Sin(k * Mathf.PI) * 30f;
			pose = "dash";
			Trail(box, k, w, floor, alpha, tint, true);
			break;
		}
		case "vanish":
		{
			bool moving = Mathf.Repeat(t, 2f) < 1.2f;
			x = box.x + 26f + Mathf.PingPong(t * 60f, w * 2f);
			pose = moving ? "runFast" : "idle";
			a = moving ? 0.12f : 1f;
			break;
		}
		case "bounce":
		{
			float k = Mathf.Repeat(t, 0.9f) / 0.9f;
			y -= Mathf.Sin(k * Mathf.PI) * 40f;
			pose = k < 0.5f ? "jumpFast" : "fallFast";
			break;
		}
		case "zoom":
		{
			float sp = val == "150%" ? 2f : (val == "125%" ? 1.5f : 1f);
			x = box.x + 26f + Mathf.Repeat(t * 70f * sp, w * 2f);
			pose = "runFast";
			flip = true;
			break;
		}
		case "jumps":
		{
			int n = val == "Infinite" ? 4 : Mathf.Clamp(int.TryParse(val, out var j) ? j : 1, 1, 4);
			float k = Mathf.Repeat(t, 0.5f * n + 0.5f) / 0.5f;
			int step = Mathf.Min(n - 1, (int)k);
			float f = Mathf.Clamp01(k - step);
			y -= (step * 14f + Mathf.Sin(f * Mathf.PI) * 24f) * (k < n ? 1f : 0f);
			pose = k < n ? "jumpFast" : "idle";
			break;
		}
		case "dashlen":
		{
			float len = val == "Short" ? 0.5f : (val == "Long" ? 0.8f : (val == "Very Long" ? 1f : 0.65f));
			float k = Mathf.Clamp01(Mathf.Repeat(t, 1.4f) / 0.35f);
			x = box.x + 26f + k * w * 2f * len;
			pose = k < 1f ? "dash" : "idle";
			flip = true;
			Trail(box, k * len, w, floor - 10f, alpha, tint, false);
			break;
		}
		case "dashes":
		{
			float k = Mathf.Repeat(t, 1.5f);
			x = box.center.x + (k < 0.3f ? (k / 0.3f - 0.5f) * 40f : 20f);
			y -= k < 0.6f ? 24f : 0f;
			pose = k < 0.3f ? "dash" : "fallFast";
			break;
		}
		case "nograb":
		case "wallfall":
		case "climb":
		{
			// a wall on the right; she slides off it (no grab / no wall slide) or climbs it
			Rect wall = new Rect(box.xMax - 34f, box.y + 6f, 12f, box.height - 16f);
			GUI.color = new Color(tint.r, tint.g, tint.b, 0.45f * alpha);
			GUI.DrawTexture(wall, Texture2D.whiteTexture);
			x = wall.x - 6f;
			flip = true;
			float k = Mathf.Repeat(t, 1.6f) / 1.6f;
			if (act == "climb")
			{
				y = floor - k * (box.height - 30f);
				pose = "climbUp";
			}
			else
			{
				y = floor - (1f - k * k) * (box.height - 30f);
				pose = act == "wallfall" ? "fallFast" : "wallslide";
			}
			break;
		}
		case "onehit":
		{
			GuiArt.Img heart = SpeedrunTimer.Art?.Image("collectables/heartgem/0/spin00");
			if (heart != null)
			{
				float hb = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(t * 3f));
				GUI.color = new Color(1f, 1f, 1f, alpha * hb);
				GUI.DrawTexture(new Rect(box.x + 12f, box.y + 10f, 28f, 28f), heart.Tex);
			}
			pose = "idle";
			break;
		}
		case "slowmo":
		{
			float sp = 0.5f;
			if (val.EndsWith("%") && float.TryParse(val.TrimEnd('%'), out var pc))
			{
				sp = pc / 100f;
			}
			x = box.x + 26f + Mathf.PingPong(t * 70f * sp, w * 2f);
			pose = "runFast";
			break;
		}
		case "coyote":
		{
			float k = Mathf.Repeat(t, 1.6f) / 1.6f;
			// a ledge that ends mid-box; she runs off and still jumps
			GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha);
			GUI.DrawTexture(new Rect(box.center.x, floor, box.width / 2f - 6f, 3f), Texture2D.whiteTexture);
			x = box.x + 26f + k * w * 2f;
			float off = Mathf.Clamp01((k - 0.45f) / 0.55f);
			y -= Mathf.Sin(off * Mathf.PI) * 30f;
			pose = off > 0f ? "jumpFast" : "runFast";
			flip = true;
			break;
		}
		}
		Frame(assets, pose, new Vector2(x, y), s, flip, alpha * a);
		GUI.color = Color.white;
	}

	/// <summary>Madeline in the look you have on, posing (any Celeste animation), feet at a GUI point.</summary>
	public static void Pose(string pose, Vector2 feet, float scale, bool flip, float alpha)
	{
		if (Plugin.PreviewAssets == null)
		{
			return;
		}
		SkinRow skin = Array.Find(Skins.All, (SkinRow k) => k.Name == AssistMode.SkinName);
		if (skin.Id == null)
		{
			skin = Skins.Madeline;
		}
		try
		{
			CelesteAssets assets = Plugin.PreviewAssets(skin);
			if (assets != null)
			{
				Frame(assets, pose, feet, scale, flip, alpha);
			}
		}
		catch
		{
		}
		GUI.color = Color.white;
	}

	private static void Trail(Rect box, float k, float w, float y, float alpha, Color tint, bool arc)
	{
		for (int i = 1; i <= 4; i++)
		{
			float kk = k - i * 0.06f;
			if (kk < 0f)
			{
				continue;
			}
			float tx = box.x + 26f + kk * w * 2f;
			float ty = arc ? (y - 20f - Mathf.Sin(kk * Mathf.PI) * 30f) : y;
			GUI.color = new Color(tint.r, tint.g, tint.b, alpha * (0.5f - i * 0.1f));
			GUI.DrawTexture(new Rect(tx - 6f, ty - 26f, 12f, 22f), Texture2D.whiteTexture);
		}
	}

	private static void Frame(CelesteAssets assets, string pose, Vector2 feet, float s, bool flip, float alpha)
	{
		if ((!assets.Anims.TryGetValue(pose, out var anim) || anim.Frames.Count == 0) && (!assets.Anims.TryGetValue("idle", out anim) || anim.Frames.Count == 0))
		{
			return;
		}
		CelesteFrame f = anim.Frames[(int)(Time.unscaledTime / Math.Max(0.05f, anim.Delay)) % anim.Frames.Count];
		Matrix4x4 m0 = GUI.matrix;
		if (flip)
		{
			GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), feet);
		}
		// hair: the bangs and three nodes trailing behind, in the 1-dash colour
		Color hair = Color.red;
		ColorUtility.TryParseHtmlString("#" + Array.Find(Skins.All, (SkinRow k) => k.Name == AssistMode.SkinName).HairNormal, out hair);
		hair.a = alpha;
		if (f.HasHair && assets.HairNode != null && assets.Bangs.Count > 0)
		{
			Vector2 n0 = feet + new Vector2(f.HairOffset.x, -9f + f.HairOffset.y) * s;
			for (int i = 3; i >= 0; i--)
			{
				Sprite sp = i == 0 ? assets.Bangs[Mathf.Clamp(f.HairFrame, 0, assets.Bangs.Count - 1)] : assets.HairNode;
				Vector2 at = n0 + new Vector2(-1.2f * i, 1.5f * i) * s;
				Sprite(sp, at, s * (i == 0 ? 1f : 1f - i * 0.2f), hair);
			}
		}
		Sprite(f.Sprite, feet, s, new Color(1f, 1f, 1f, alpha));
		GUI.matrix = m0;
	}

	private static void Sprite(Sprite sp, Vector2 pivotAt, float scale, Color c)
	{
		if (sp == null)
		{
			return;
		}
		Rect r = sp.textureRect;
		Texture tex = sp.texture;
		Vector2 pivot = sp.pivot;
		GUI.color = c;
		GUI.DrawTextureWithTexCoords(new Rect(pivotAt.x - pivot.x * scale, pivotAt.y - (r.height - pivot.y) * scale, r.width * scale, r.height * scale), tex, new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height));
	}
}
