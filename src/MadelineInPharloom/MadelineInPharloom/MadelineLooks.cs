using System;
using System.Collections.Generic;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Customizing beyond colours (user 2026-10-19): Headwear, Trail Shape, Companion, Aura and Footprints. The headwear and
/// companion art is shared with the Customization preview (static, 8 pixels per unit like every Celeste sprite).
/// </summary>
public partial class MadelineView
{
	// ---- headwear: tiny pixel art in Celeste's scale ---------------------------------------------------------------
	// drawn without outlines: the outline (Celeste's 1px black) is added round each, except the halo
	private static readonly Dictionary<string, string[]> HatArt = new Dictionary<string, string[]>
	{
		{ "Bow", new string[3] { "RR.RR", "RRDRR", "RR.RR" } },
		{ "Flower Crown", new string[3] { ".P...W.", "PYP.WYW", "GPGGGWG" } },
		{ "Crown", new string[4] { "Y..Y..Y", "YY.Y.YY", "YYYYYYY", "DDRDBDD" } },
		{ "Party Hat", new string[6] { "..Y..", "..B..", ".BWB.", ".WBW.", "BWBWB", "WBWBW" } },
		{ "Top Hat", new string[5] { ".HHHHH.", ".HHHHH.", ".HHHHH.", ".RRRRR.", "HHHHHHH" } },
		{ "Witch Hat", new string[7] { "MM.......", ".MM......", "..MMM....", "...MMM...", "...MMMM..", "...VVVV..", "MMMMMMMMM" } },
		{ "Headphones", new string[6] { "..BBBBBBB..", ".B.......B.", "B.........B", "B.........B", "CC.......CC", "CC.......CC" } },
		{ "Hair Flower", new string[3] { ".P.P.", "PPYPP", ".PPP." } },
		{ "Sprout", new string[4] { "G...G", "GG.GG", "..G..", "..G.." } },
		{ "Halo", new string[3] { ".YYYYY.", "Y.....Y", ".YYYYY." } }
	};

	// where each sits, in Celeste pixels from her bangs' centre (x towards her back, y up); bottoms sit on that point,
	// except the bow and the flower, which are centred there
	private static readonly Dictionary<string, Vector2> HatAt = new Dictionary<string, Vector2>
	{
		{ "Bow", new Vector2(3f, 2.5f) },
		{ "Flower Crown", new Vector2(0.5f, 1f) },
		{ "Crown", new Vector2(0.5f, 1.5f) },
		{ "Party Hat", new Vector2(0.5f, 1.5f) },
		{ "Top Hat", new Vector2(0.5f, 1.5f) },
		{ "Witch Hat", new Vector2(0.5f, 1f) },
		{ "Headphones", new Vector2(0.5f, -3f) },
		{ "Hair Flower", new Vector2(3f, 1.5f) },
		{ "Sprout", new Vector2(0.5f, 2.5f) },
		{ "Halo", new Vector2(0.5f, 7f) }
	};

	// a little darker than looks right on paper: Pharloom's colour grading brightens everything
	private static Color HatColor(char c)
	{
		switch (c)
		{
		case 'K': return new Color(0.04f, 0.03f, 0.05f);
		case 'R': return new Color(0.78f, 0.2f, 0.29f);
		case 'D': return new Color(0.5f, 0.34f, 0.08f);
		case 'P': return new Color(0.9f, 0.45f, 0.6f);
		case 'W': return new Color(0.88f, 0.86f, 0.82f);
		case 'Y': return new Color(0.92f, 0.74f, 0.22f);
		case 'G': return new Color(0.27f, 0.55f, 0.28f);
		case 'B': return new Color(0.28f, 0.5f, 0.78f);
		case 'H': return new Color(0.2f, 0.18f, 0.27f);
		case 'M': return new Color(0.24f, 0.16f, 0.34f);
		case 'V': return new Color(0.55f, 0.35f, 0.75f);
		case 'C': return new Color(0.82f, 0.3f, 0.45f);
		default: return Color.clear;
		}
	}

	private static readonly Dictionary<string, Sprite> hatSprites = new Dictionary<string, Sprite>();

	private static Sprite Pixels(string[] art, Func<char, Color> color, Vector2 pivot)
	{
		int w = art[0].Length;
		int h = art.Length;
		Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Point,
			wrapMode = TextureWrapMode.Clamp
		};
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				t.SetPixel(x, h - 1 - y, color(art[y][x]));
			}
		}
		t.Apply();
		return Sprite.Create(t, new Rect(0f, 0f, w, h), pivot, 8f);
	}

	/// <summary>The art with a 1px black outline round it (each empty pixel next to a filled one).</summary>
	private static string[] Outlined(string[] art)
	{
		int w = art[0].Length + 2;
		int h = art.Length + 2;
		char[][] g = new char[h][];
		for (int y = 0; y < h; y++)
		{
			g[y] = new string('.', w).ToCharArray();
		}
		for (int y = 0; y < art.Length; y++)
		{
			for (int x = 0; x < art[y].Length; x++)
			{
				g[y + 1][x + 1] = art[y][x];
			}
		}
		string[] r = new string[h];
		for (int y = 0; y < h; y++)
		{
			char[] o = (char[])g[y].Clone();
			for (int x = 0; x < w; x++)
			{
				if (g[y][x] == '.' && ((x > 0 && g[y][x - 1] != '.') || (x < w - 1 && g[y][x + 1] != '.') || (y > 0 && g[y - 1][x] != '.') || (y < h - 1 && g[y + 1][x] != '.')))
				{
					o[x] = 'K';
				}
			}
			r[y] = new string(o);
		}
		return r;
	}

	/// <summary>A headwear's sprite and where it sits (Celeste pixels from her bangs: x towards her back, y up).</summary>
	public static Sprite Hat(string name, out Vector2 at)
	{
		at = HatAt.TryGetValue(name ?? "", out Vector2 v) ? v : Vector2.zero;
		if (name == null || !HatArt.TryGetValue(name, out string[] art))
		{
			return null;
		}
		if (!hatSprites.TryGetValue(name, out Sprite sp))
		{
			bool centred = name == "Bow" || name == "Hair Flower";
			sp = Pixels(name == "Halo" ? art : Outlined(art), HatColor, centred ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f));
			hatSprites[name] = sp;
		}
		if (name == "Halo")
		{
			at.y += Mathf.Sin(Time.unscaledTime * 2.5f);
		}
		return sp;
	}

	private SpriteRenderer headwear;

	private void UpdateHeadwear(CelesteFrame frame, Vector3 feet, int facing, float sx, float vs)
	{
		if (headwear == null)
		{
			headwear = MakeRenderer("Headwear", 0);
		}
		string hat = AssistMode.ValueOf(AssistOptions.Headwear);
		Sprite sp = Hat(hat, out Vector2 at);
		bool show = sp != null && (skin.Style == "cat" || skin.Style == "celeste") && frame.HasHair && root.activeSelf && !binding;
		headwear.enabled = show;
		if (!show)
		{
			return;
		}
		float px = vs / 8f;
		headwear.sprite = sp;
		headwear.color = Color.white;
		headwear.transform.position = new Vector3(nodes[0].x - facing * at.x * px, nodes[0].y + at.y * px, feet.z - 0.0005f);
		headwear.transform.localScale = new Vector3(facing * Math.Abs(sx) * vs, vs, 1f);
	}

	// ---- trail shapes ------------------------------------------------------------------------------------------------
	private static Sprite heartSprite;

	private static Sprite starSprite;

	private static Sprite HeartSprite => heartSprite ?? (heartSprite = Pixels(new string[6] { ".HH.HH.", "HHHHHHH", "HHHHHHH", ".HHHHH.", "..HHH..", "...H..." }, (char c) => c == 'H' ? Color.white : Color.clear, new Vector2(0.5f, 0.5f)));

	private static Sprite StarSprite => starSprite ?? (starSprite = Pixels(new string[6] { "...H...", "..HHH..", "HHHHHHH", ".HHHHH.", ".HH.HH.", "H.....H" }, (char c) => c == 'H' ? Color.white : Color.clear, new Vector2(0.5f, 0.5f)));

	/// <summary>The shape a dash trail piece takes, or null for Celeste's afterimage.</summary>
	private Sprite TrailShapeSprite()
	{
		switch (AssistMode.ValueOf(AssistOptions.TrailShape))
		{
		case "Hearts":
			return HeartSprite;
		case "Stars":
			return StarSprite;
		case "Feathers":
			return assets.Feather;
		default:
			return null;
		}
	}

	// ---- the companion: Celeste's bird, or something floating along behind her ---------------------------------------
	/// <summary>How each floating companion animates: frame order and seconds per frame.</summary>
	private static int[] CompanionOrder(string name, int count, out float delay)
	{
		List<int> order = new List<int>();
		delay = 0.1f;
		switch (name)
		{
		case "Golden Berry":
		case "Winged Berry":
			delay = 0.08f;
			break;
		case "Crystal Heart":
			for (int i = 0; i < 10; i++)
			{
				order.Add(0);   // Celeste's heart: still a moment, then a spin
			}
			break;
		case "Cassette":
			delay = 0.07f;
			for (int i = 0; i < count; i++)
			{
				order.Add(i);
			}
			for (int i = 0; i < 30; i++)
			{
				order.Add(0);
			}
			return order.ToArray();
		case "Feather":
			delay = 0.06f;
			break;
		}
		for (int i = (name == "Crystal Heart") ? 1 : 0; i < count; i++)
		{
			order.Add(i);
		}
		return order.ToArray();
	}

	/// <summary>A companion's frame right now (the preview and the game share it); null when it has none.</summary>
	public static Sprite CompanionFrame(string name, float time)
	{
		if (CelesteAssets.Companions == null || name == null || !CelesteAssets.Companions.TryGetValue(name, out List<Sprite> frames) || frames.Count == 0)
		{
			return null;
		}
		int[] order = CompanionOrder(name, frames.Count, out float delay);
		return frames[order[(int)(time / delay) % order.Length]];
	}

	private SpriteRenderer companion;

	private Vector3 compPos;

	private bool compPlaced;

	private float compStill;

	private float compAnim;

	private float peckIn = 2f;

	private float peckT = -1f;

	private void UpdateCompanion(HeroController h, Vector3 feet, int facing, float vs, float dt)
	{
		if (companion == null)
		{
			companion = MakeRenderer("Companion", 0);
		}
		string kind = AssistMode.ValueOf(AssistOptions.Companion);
		bool bird = kind == "Bird";
		compAnim += dt;
		Sprite floating = bird ? null : CompanionFrame(kind, compAnim);
		bool show = kind != "None" && root.activeSelf && (bird ? (assets.BirdFly.Count >= 4 && assets.BirdCrow.Count > 26) : floating != null);
		companion.enabled = show;
		if (!show)
		{
			compPlaced = false;
			return;
		}
		float px = vs / 8f;
		bool still = h.cState.onGround && (ctrl.Active ? (ctrl.OnGround && ctrl.Speed.sqrMagnitude < 4f) : Rb(h).linearVelocity.sqrMagnitude < 0.01f);
		compStill = still ? compStill + dt : 0f;
		bool land = bird && compStill > 0.8f;
		float lift = kind == "Jellyfish" ? 24f : (kind == "Winged Berry" ? 22f : 20f);
		Vector3 target = land ? (feet + new Vector3(-facing * 14f * px, 0f, 0f)) : (feet + new Vector3(-facing * 12f * px, (lift + Mathf.Sin(Time.time * 3f) * 1.5f) * px, 0f));
		target.z = feet.z + 0.003f;   // behind her
		if (!compPlaced || (compPos - target).magnitude > 90f * px)
		{
			compPos = target;   // a new room, a respawn: it's just there
			compPlaced = true;
		}
		Vector3 was = compPos;
		compPos = Vector3.Lerp(compPos, target, 1f - Mathf.Exp(-dt * (land ? 7f : 4.5f)));
		float vx = (compPos.x - was.x) / Math.Max(dt, 0.0001f);
		Sprite frame;
		int face = 1;
		float scale = 0.85f * vs;
		if (!bird)
		{
			frame = floating;
			if (kind == "Jellyfish")
			{
				scale = 0.6f * vs;
			}
		}
		else if (land && (compPos - target).magnitude < 1.5f * px)
		{
			// standing beside her: still, with a peck every few seconds
			scale = 0.8f * vs;
			face = feet.x >= compPos.x ? 1 : -1;
			if (peckT < 0f && (peckIn -= dt) <= 0f)
			{
				peckT = 0f;
			}
			int f = 0;
			if (peckT >= 0f)
			{
				peckT += dt;
				f = 14 + (int)(peckT / 0.08f);
				if (f > 26)
				{
					peckT = -1f;
					peckIn = 2f + UnityEngine.Random.value * 3f;
					f = 0;
				}
			}
			frame = assets.BirdCrow[f];
		}
		else
		{
			scale = 0.8f * vs;
			peckT = -1f;
			int[] order = new int[4] { 3, 0, 1, 2 };
			frame = assets.BirdFly[order[(int)(compAnim / 0.1f) % 4]];
			face = Math.Abs(vx) > 2f * px ? Math.Sign(vx) : facing;
		}
		companion.sprite = frame;
		companion.color = Color.white;
		companion.transform.position = compPos;
		companion.transform.localScale = new Vector3(face * scale, scale, 1f);
	}

	// ---- aura and footprints: little things around her and behind her ----------------------------------------------
	private struct Mote
	{
		public SpriteRenderer R;

		public Vector3 Pos;

		public Vector3 Vel;

		public float Age;

		public float Life;

		public float Phase;
	}

	private readonly List<Mote> motes = new List<Mote>();

	private readonly Stack<SpriteRenderer> motePool = new Stack<SpriteRenderer>();

	private float auraIn;

	private float stepIn;

	private static Sprite dotSprite;

	private static Sprite flakeSprite;

	private static Sprite petalSprite;

	private static Sprite sparkleSprite;

	private static Sprite flowerSprite;

	private static Sprite Dot => dotSprite ?? (dotSprite = Pixels(new string[2] { "WW", "WW" }, (char c) => Color.white, new Vector2(0.5f, 0.5f)));

	private static Sprite Flake => flakeSprite ?? (flakeSprite = Pixels(new string[3] { ".W.", "WWW", ".W." }, (char c) => c == 'W' ? Color.white : Color.clear, new Vector2(0.5f, 0.5f)));

	private static Sprite Petal => petalSprite ?? (petalSprite = Pixels(new string[2] { "PP.", ".PP" }, (char c) => c == 'P' ? new Color(0.95f, 0.6f, 0.72f) : Color.clear, new Vector2(0.5f, 0.5f)));

	private static Sprite Sparkle => sparkleSprite ?? (sparkleSprite = Pixels(new string[5] { "..W..", "..W..", "WWWWW", "..W..", "..W.." }, (char c) => c == 'W' ? Color.white : Color.clear, new Vector2(0.5f, 0.5f)));

	private static Sprite Flower => flowerSprite ?? (flowerSprite = Pixels(new string[4] { ".P.", "PYP", ".P.", ".G." }, (char c) => c switch
	{
		'P' => new Color(0.95f, 0.62f, 0.75f),
		'Y' => new Color(0.95f, 0.85f, 0.35f),
		'G' => new Color(0.3f, 0.6f, 0.3f),
		_ => Color.clear
	}, new Vector2(0.5f, 0f)));

	private void SpawnMote(Sprite sp, Vector3 pos, Vector3 vel, float life, Color c, float scale)
	{
		SpriteRenderer r;
		if (motePool.Count > 0)
		{
			r = motePool.Pop();
			r.gameObject.SetActive(true);
		}
		else
		{
			r = MakeRenderer("Mote", 0);
		}
		r.sprite = sp;
		r.color = c;
		r.transform.position = pos;
		r.transform.localScale = new Vector3(scale, scale, 1f);
		motes.Add(new Mote { R = r, Pos = pos, Vel = vel, Age = 0f, Life = life, Phase = UnityEngine.Random.value * 6.28f });
	}

	private void UpdateAuraAndSteps(HeroController h, Vector3 feet, int facing, float vs, float dt)
	{
		float px = vs / 8f;
		bool here = root.activeSelf && !dying;
		string aura = AssistMode.ValueOf(AssistOptions.Aura);
		if (here && aura != "None" && (auraIn -= dt) <= 0f && motes.Count < 40)
		{
			Vector3 c = feet + new Vector3(0f, 8f * px, 0.002f);
			switch (aura)
			{
			case "Fireflies":
				auraIn = 0.35f;
				SpawnMote(Dot, c + new Vector3((UnityEngine.Random.value * 2f - 1f) * 14f * px, (UnityEngine.Random.value * 2f - 1f) * 10f * px, 0f), new Vector3((UnityEngine.Random.value - 0.5f) * 3f * px, (UnityEngine.Random.value - 0.3f) * 3f * px, 0f), 2.2f, new Color(0.85f, 1f, 0.45f), vs * 0.5f);
				break;
			case "Snowfall":
				auraIn = 0.12f;
				SpawnMote(Flake, c + new Vector3((UnityEngine.Random.value * 2f - 1f) * 18f * px, 18f * px, 0f), new Vector3(0f, -10f * px, 0f), 2f, new Color(0.92f, 0.95f, 1f), vs * 0.5f);
				break;
			case "Sparkles":
				auraIn = 0.18f;
				SpawnMote(Sparkle, c + new Vector3((UnityEngine.Random.value * 2f - 1f) * 12f * px, (UnityEngine.Random.value * 2f - 1f) * 12f * px, 0f), Vector3.zero, 0.6f, Color.Lerp(hairColor, Color.white, 0.5f), vs * 0.5f);
				break;
			case "Petals":
				auraIn = 0.2f;
				SpawnMote(Petal, c + new Vector3((UnityEngine.Random.value * 2f - 1f) * 18f * px, 16f * px, 0f), new Vector3(facing * -4f * px, -8f * px, 0f), 2.4f, Color.white, vs * 0.6f);
				break;
			}
		}
		string steps = AssistMode.ValueOf(AssistOptions.Footprints);
		bool running = here && ctrl.Active && ctrl.OnGround && Math.Abs(ctrl.Speed.x) > 40f;
		if (running && steps != "None" && (stepIn -= dt) <= 0f && motes.Count < 60)
		{
			stepIn = 0.14f;
			Vector3 at = feet + new Vector3(-facing * 2f * px, 0f, 0.002f);
			switch (steps)
			{
			case "Flowers":
				SpawnMote(Flower, at, Vector3.zero, 1.6f, Color.white, vs * 0.8f);
				break;
			case "Sparkles":
				SpawnMote(Sparkle, at + new Vector3(0f, 2f * px, 0f), new Vector3(0f, 6f * px, 0f), 0.5f, Color.Lerp(hairColor, Color.white, 0.5f), vs * 0.45f);
				break;
			case "Hearts":
				SpawnMote(HeartSprite, at + new Vector3(0f, 2f * px, 0f), new Vector3(0f, 8f * px, 0f), 0.7f, hairColor, vs * 0.45f);
				break;
			case "Snow":
				SpawnMote(Flake, at + new Vector3(0f, 1f * px, 0f), new Vector3(-facing * 6f * px, 10f * px, 0f), 0.5f, new Color(0.92f, 0.95f, 1f), vs * 0.5f);
				break;
			}
		}
		for (int i = motes.Count - 1; i >= 0; i--)
		{
			Mote m = motes[i];
			m.Age += dt;
			if (m.R == null || m.Age >= m.Life || !here)
			{
				if (m.R != null)
				{
					m.R.gameObject.SetActive(false);
					motePool.Push(m.R);
				}
				motes.RemoveAt(i);
				continue;
			}
			m.Pos += m.Vel * dt;
			float sway = (m.R.sprite == Flake || m.R.sprite == Petal) ? Mathf.Sin(m.Age * 2.5f + m.Phase) * 3f * px : 0f;
			m.R.transform.position = m.Pos + new Vector3(sway, 0f, 0f);
			float t = m.Age / m.Life;
			Color c = m.R.color;
			float flicker = m.R.sprite == Dot ? 0.5f + 0.5f * Mathf.Sin(m.Age * 6f + m.Phase) : 1f;
			c.a = Mathf.Clamp01(Math.Min(t * 5f, (1f - t) * 3f)) * flicker;
			m.R.color = c;
			motes[i] = m;
		}
	}

	// ---- emotes (user 2026-10-19): each has a key (1-8 at first) and a right-stick direction (EmoteBinds); one plays
	// for a few seconds while she stands still, holding the key is the same as one press --------------------------------
	private int emote = -1;

	private float emoteUntil;

	private bool emoteStarted;

	/// <summary>Called while she's on the ground under control: starts an emote when asked for, keeps it playing, and
	/// ends it on any move or when its time is up. True when an emote is showing.</summary>
	private bool EmotePlaying(Vector2 speed)
	{
		InputMove(out int mx, out int my);
		bool standing = Math.Abs(speed.x) < 1f && ctrl.State == 0 && !ctrl.Ducking && mx == 0 && my == 0;
		int asked = EmoteBinds.Pressed();
		if (asked >= 0 && asked != emote && standing && assets.Anims.ContainsKey(EmoteBinds.Anims[asked][0]))
		{
			emote = asked;
			emoteStarted = false;
			emoteUntil = Time.time + EmoteBinds.Seconds[asked];
			Plugin.Log.LogInfo("Emote: " + EmoteBinds.Names[asked]);
		}
		if (emote < 0)
		{
			return false;
		}
		if (!standing || Time.time >= emoteUntil)
		{
			emote = -1;   // moved, or its time is up
			return false;
		}
		string[] e = EmoteBinds.Anims[emote];
		if (emoteStarted && anim != null && Array.IndexOf(e, anim.Id) < 0)
		{
			emote = -1;   // a one-shot one (the stretch) has played through
			return false;
		}
		if (!emoteStarted)
		{
			Play(e[0], restart: true);
			emoteStarted = true;
		}
		return true;
	}

	/// <summary>Headwear off while she's gone (dying, hidden); the companion waits where it is.</summary>
	private void HideLooks()
	{
		if (headwear != null)
		{
			headwear.enabled = false;
		}
	}
}
