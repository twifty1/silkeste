using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// The Tech Tips ghost (user 2026-10-20): a see-through Madeline beside her showing the next Celeste technique. Its path
/// comes from Celeste's own numbers (dash 216, super 260 x 105, hyper 325 x 52.5, wall bounce 170 x 160, gravity 900),
/// worked out once per lesson. It slows down from the dash to just after the jump, leaves Celeste's afterimages, and its
/// hair turns blue when the dash is spent, like hers. The wall bounce starts against a real wall beside her.
/// </summary>
public partial class MadelineView
{
	private struct GhostFrame
	{
		public Vector2 P;   // Celeste pixels: x forward, y up

		public string Anim;

		public int Step;   // the input step reached (-1 before the first)

		public bool Slow;

		public bool Dashing;

		public bool HasDash;

		public float Fade;   // 1, down to 0 over the slide at the end
	}

	private class GhostPath
	{
		public List<GhostFrame> F = new List<GhostFrame>();

		public int ZoneStart;   // frame the jump window opens

		public float Zone;   // its length (seconds)

		public int JumpFrame;   // frame the ghost jumps in it

		public int DashFrame;   // frame the dash starts
	}

	private static readonly Dictionary<string, GhostPath> ghostPaths = new Dictionary<string, GhostPath>();

	private const float GhostDt = 1f / 60f;

	private static GhostPath MakeGhostPath(string id, float drop = 0f)
	{
		string key = id + "@" + drop;
		if (ghostPaths.TryGetValue(key, out GhostPath done))
		{
			return done;
		}
		GhostPath g = new GhostPath();
		List<GhostFrame> f = g.F;
		Vector2 p = Vector2.zero;
		Vector2 v = Vector2.zero;
		int step = -1;
		bool slow = false;
		bool hasDash = true;
		void Add(string anim, bool dashing = false, float fade = 1f)
		{
			f.Add(new GhostFrame { P = p, Anim = anim, Step = step, Slow = slow, Dashing = dashing, HasDash = hasDash, Fade = fade });
		}
		void Hold(float secs, string anim)
		{
			for (float t = 0f; t < secs - 0.001f; t += GhostDt)
			{
				Add(anim);
			}
		}
		void StartDash()
		{
			step++;
			slow = true;
			hasDash = false;
			g.DashFrame = f.Count;
		}
		// a dash for n frames; a down dash that meets the ground turns flat and 1.2x faster (Celeste's ultra)
		int Dash(int frames, Vector2 vel, string anim)
		{
			int landed = -1;
			for (int i = 0; i < frames; i++)
			{
				p += vel * GhostDt;
				if (p.y <= 0f && vel.y < 0f)
				{
					p.y = 0f;
					vel = new Vector2(vel.x * 1.2f, 0f);
					anim = "duck";
					landed = f.Count;
				}
				Add(anim, dashing: true);
			}
			return landed;
		}
		// a jump with Celeste's variable jump (the speed holds while Jump is held), then gravity, to the landing, then
		// a short slide as it fades
		void Jump(float vx, float vy, float varTime, float riseOnly = -1f, float land = 0f)
		{
			step++;
			v = new Vector2(vx, vy);
			float held = 0f;
			float el = 0f;
			int slowLeft = 10;
			for (int i = 0; i < 600; i++)
			{
				el += GhostDt;
				if (held < varTime)
				{
					held += GhostDt;
				}
				else
				{
					v.y = Math.Max(-160f, v.y - 900f * GhostDt * (Math.Abs(v.y) < 40f ? 0.5f : 1f));
				}
				p += v * GhostDt;
				if (slowLeft-- <= 0)
				{
					slow = false;
				}
				if (riseOnly > 0f && el >= riseOnly)
				{
					Add("jumpFast");
					return;   // the wavedash's hop: its dash comes next, mid-air
				}
				if (p.y <= -land && v.y < 0f)
				{
					p.y = -land;
					break;
				}
				Add(v.y > 0f ? "jumpFast" : "fallFast");
			}
			slow = false;
			hasDash = true;
			float slide = 0f;
			while (v.x > 20f && slide < 0.3f)
			{
				slide += GhostDt;
				v.x -= (v.x > 90f ? 400f : 1000f) * GhostDt;
				p.x += v.x * GhostDt;
				Add("runFast", fade: 1f - slide / 0.3f);
			}
		}
		switch (id)
		{
		case "super":
			Hold(0.7f, "idle");
			StartDash();
			g.ZoneStart = f.Count;
			g.Zone = 0.15f;
			Dash(5, new Vector2(216f, 0f), "dash");
			g.JumpFrame = f.Count;
			Jump(260f, 105f, 0.2f);
			break;
		case "hyper":
			Hold(0.7f, "idle");
			StartDash();
			g.ZoneStart = f.Count;
			g.Zone = 0.15f;
			Dash(5, new Vector2(216f * 0.7071f * 1.2f, 0f), "duck");
			g.JumpFrame = f.Count;
			Jump(325f, 52.5f, 0.2f, -1f, drop);
			break;
		case "wavedash":
		{
			Hold(0.6f, "idle");
			Jump(0f, 105f, 0.1f, 0.1f);
			int dashFrames = 9;
			StartDash();
			step = 1;
			int landed = Dash(dashFrames, new Vector2(216f * 0.7071f, -216f * 0.7071f), "dash");
			if (landed < 0)
			{
				landed = f.Count - 1;
			}
			// jump two frames after touching down: cut the dash there
			int cut = Math.Min(f.Count, landed + 3);
			int dashStart = g.DashFrame;
			f.RemoveRange(cut, f.Count - cut);
			p = f[f.Count - 1].P;
			g.ZoneStart = landed;
			g.Zone = Math.Max(0.03f, (dashFrames - (landed - dashStart)) * GhostDt);
			g.JumpFrame = f.Count;
			Jump(325f, 52.5f, 0.2f);
			break;
		}
		case "wallbounce":
			Hold(0.7f, "idle");
			StartDash();
			g.ZoneStart = f.Count;
			g.Zone = 0.15f;
			Dash(5, new Vector2(0f, 216f), "dash");
			g.JumpFrame = f.Count;
			Jump(170f, 160f, 0.25f);
			break;
		}
		Hold(0.5f, "idle");
		for (int i = f.Count - 30; i < f.Count; i++)
		{
			// the idle after the landing is gone (faded): it restarts from her side
			GhostFrame x = f[i];
			x.Fade = 0f;
			f[i] = x;
		}
		ghostPaths[key] = g;
		return g;
	}

	private SpriteRenderer ghostBody;

	private SpriteRenderer ghostBangs;

	private readonly SpriteRenderer[] ghostHair = new SpriteRenderer[3];

	private readonly SpriteRenderer[] ghostTrail = new SpriteRenderer[3];

	private float ghostIdle;

	private float ghostFrame;

	private float ghostFade;

	private float bubbleHold;

	private float bubbleFade;

	private float lastPractice;

	private int ghostLesson = -1;

	private int ghostDir = 1;

	private int ghostLastStep = -1;

	private int ghostLastFrame = -1;

	private float ghostRoom = 30f;

	private Vector3 ghostFrom;

	private bool ghostDevLogged;

	private static readonly Color GhostBody = new Color(0.8f, 0.9f, 1f, 1f);

	private static readonly Color GhostRed = new Color(0.86f, 0.3f, 0.3f, 1f);

	private static readonly Color GhostBlue = new Color(0.27f, 0.72f, 1f, 1f);

	private void UpdateTechGhost(HeroController h, Vector3 feet, float vs, float dt)
	{
		if (ghostBody == null)
		{
			ghostBody = MakeRenderer("TechGhost", 0);
			ghostBangs = MakeRenderer("TechGhostBangs", 0);
			for (int i = 0; i < ghostHair.Length; i++)
			{
				ghostHair[i] = MakeRenderer("TechGhostHair" + i, 0);
			}
			for (int i = 0; i < ghostTrail.Length; i++)
			{
				ghostTrail[i] = MakeRenderer("TechGhostTrail" + i, 0);
			}
		}
		float px = vs / 8f;
		TechTips.Head = feet + new Vector3(0f, 22f * px, 0f);
		TechTips.Pinned = TechRoom.Active;   // in the Tech Tutorial the bubble stays at the top of the screen
		TechTips.NameOverride = null;
		TechTips.HowOverride = null;
		TechTips.PipsNeed = 0;
		int section = TechRoom.SectionLesson(feet.x);   // the tutorial's super and hyper gaps teach their own move
		int lesson = !TechTips.On ? -1 : section >= 0 ? section : TechRoom.Active ? -1 : TechTips.Current;   // in the tutorial only the gaps use this ghost
		bool standing = root.activeSelf && !dying && ctrl.Active && ctrl.OnGround && ctrl.Speed.sqrMagnitude < 1f && ctrl.State == 0 && !ctrl.Ducking && emote < 0 && !h.playerData.atBench && !BossRematch.Active && PostcardView.TutorialDone && !h.controlReqlinquished;
		ghostIdle = standing ? ghostIdle + dt : 0f;
		if (lesson < 0 || lesson != ghostLesson && ghostLesson >= 0)
		{
			// learned (or switched off): put it away
			ghostLesson = -1;
			bubbleHold = 0f;
		}
		if (section >= 0 && ghostLesson == section && (ghostFrom - TechRoom.SectionGhostAt(section)).sqrMagnitude > 0.01f)
		{
			ghostLesson = -1;   // moved on to the next gap
		}
		if (ghostLesson < 0 && lesson >= 0 && (ghostIdle > 1.2f || section >= 0) && (Time.unscaledTime - TechTips.DoneAt > 2.2f || section >= 0))
		{
			if (section >= 0)
			{
				// at a gap the ghost stands before its edge and does the move across it, again and again
				ghostLesson = section;
				ghostFrom = TechRoom.SectionGhostAt(section);
				ghostDir = 1;
				ghostRoom = 999f;
				ghostFrame = 0f;
				ghostLastStep = -1;
				ghostLastFrame = -1;
				bubbleHold = 8f;
				TechTips.Introduce(section);
			}
			else if (StartGhost(lesson, feet))
			{
				ghostDevLogged = false;
			}
			else if (MadelineTouches.DevOn && !ghostDevLogged)
			{
				ghostDevLogged = true;
				Plugin.Log.LogInfo("Tech ghost: " + TechTips.Lessons[lesson].Id + " waits for a wall beside her");
			}
		}
		else if (MadelineTouches.DevOn && ghostLesson < 0 && ghostIdle > 3f && !ghostDevLogged)
		{
			ghostDevLogged = true;
			Plugin.Log.LogInfo("Tech ghost: not shown (lesson " + lesson + ", tips " + (TechTips.On ? "on" : "off") + ", file " + TechTips.Slot + ")");
		}
		if (!standing)
		{
			ghostDevLogged = false;
		}
		bool show = ghostLesson >= 0;
		if (show && section >= 0)
		{
			bubbleHold = Math.Max(bubbleHold, 8f);   // at a gap the bubble stays up
			// the training room's station card: its step, what to do, and the step's pips
			TechRoom.StationCard(feet.x, out string title, out string how, out int done, out int need);
			TechTips.NameOverride = title;
			TechTips.HowOverride = how;
			TechTips.PipsDone = done;
			TechTips.PipsNeed = need;
		}
		if (show)
		{
			// the ghost plays while she stands still; once she moves it fades, but the bubble stays up while she practises
			bool keep = standing || section >= 0;   // at a gap it stays visible while she moves
			ghostFade = Mathf.MoveTowards(ghostFade, keep ? 1f : 0f, dt / (keep ? 0.3f : 0.2f));
			if (TechTips.PracticeAt != lastPractice)
			{
				lastPractice = TechTips.PracticeAt;
				bubbleHold = Math.Max(bubbleHold, 8f);
			}
			bubbleHold = standing ? Math.Max(bubbleHold, 8f) : bubbleHold - dt;
			if (bubbleHold <= 0f)
			{
				ghostLesson = -1;
				show = false;
			}
		}
		else
		{
			ghostFade = Mathf.MoveTowards(ghostFade, 0f, dt / 0.2f);
		}
		bubbleFade = Mathf.MoveTowards(bubbleFade, show ? Mathf.Clamp01(bubbleHold) : 0f, dt / 0.2f);
		TechTips.Showing = bubbleFade > 0.01f && ghostLesson >= 0 ? ghostLesson : -1;
		TechTips.BubbleAlpha = bubbleFade;
		TechTips.Facing = ghostDir;
		// the bubble sits over the ghost's start, then follows her while she practises
		Vector3 over = ghostFrom + new Vector3(ghostDir * 1.6f, 3.4f, 0f);
		Vector3 follow = feet + new Vector3(0f, 3.4f, 0f);
		Vector3 want = Vector3.Lerp(follow, over, ghostFade);
		TechTips.BubbleAt = (TechTips.BubbleAt - want).sqrMagnitude > 400f ? want : Vector3.Lerp(TechTips.BubbleAt, want, 1f - Mathf.Exp(-dt * 10f));
		bool drawGhost = show && ghostFade > 0.01f;
		SetGhostVisible(drawGhost);
		if (!show)
		{
			TechTips.DemoCursor = float.NaN;
			TechTips.DemoJump = float.NaN;
			TechTips.ActiveStep = -1;
			return;
		}
		GhostPath path = MakeGhostPath(TechTips.Lessons[ghostLesson].Id, section == 1 ? TechRoom.HyperDropPx : 0f);
		int n = path.F.Count;
		int i0 = Mathf.Clamp((int)ghostFrame, 0, n - 1);
		ghostFrame += dt * 60f * (path.F[i0].Slow ? 0.35f : 1f);
		if (ghostFrame >= n)
		{
			ghostFrame -= n;
		}
		i0 = Mathf.Clamp((int)ghostFrame, 0, n - 1);
		GhostFrame g = path.F[i0];
		TechTips.ActiveStep = g.Step;
		TechTips.DemoZone = path.Zone;
		bool inAction = i0 >= path.DashFrame && g.Fade > 0f;
		TechTips.DemoCursor = inAction && i0 <= path.JumpFrame + 12 ? (i0 - path.ZoneStart) / 60f : float.NaN;
		TechTips.DemoJump = inAction && i0 >= path.JumpFrame ? (path.JumpFrame - path.ZoneStart) / 60f : float.NaN;
		Vector3 at = ghostFrom + new Vector3(g.P.x * ghostDir / 8f * vs, g.P.y / 8f * vs, 0.0015f);
		TechTips.GhostHead = at + new Vector3(0f, 24f * px, 0f);
		if (i0 < ghostLastFrame)
		{
			ghostLastStep = -1;   // looped
		}
		ghostLastFrame = i0;
		if (g.Step != ghostLastStep)
		{
			if (g.Step >= 0 && drawGhost)
			{
				TechTips.GhostPressAt = Time.unscaledTime;
				TechTips.GhostPressStep = g.Step;
				Particles.DustTinted(at, -MathF.PI / 2f, 3, new Color(0.75f, 0.88f, 1f, 0.6f * ghostFade));
			}
			ghostLastStep = g.Step;
		}
		if (!drawGhost)
		{
			return;
		}
		// it fades where a wall stops it, and over its slide at the end
		float room = Mathf.Clamp01((ghostRoom - 0.6f - Math.Abs(g.P.x / 8f * vs)) / 1.5f);
		float alpha = ghostFade * g.Fade * room;
		if (!DrawGhostSprite(ghostBody, g, i0, path, at, vs, new Color(GhostBody.r, GhostBody.g, GhostBody.b, (TechRoom.Active ? 0.8f : 0.62f) * alpha)))
		{
			SetGhostVisible(false);
			return;
		}
		// afterimages along the dash, in the spent-dash blue
		for (int j = 0; j < ghostTrail.Length; j++)
		{
			int back = i0 - 3 * (j + 1);
			bool on = back >= 0 && path.F[back].Dashing && i0 - back < 14;
			ghostTrail[j].enabled = on && alpha > 0.01f;
			if (ghostTrail[j].enabled)
			{
				GhostFrame b = path.F[back];
				Vector3 bat = ghostFrom + new Vector3(b.P.x * ghostDir / 8f * vs, b.P.y / 8f * vs, 0.002f + 0.0002f * j);
				DrawGhostSprite(ghostTrail[j], b, back, path, bat, vs, new Color(GhostBlue.r, GhostBlue.g, GhostBlue.b, 0.4f * (1f - j / 3f) * alpha));
			}
		}
		CelesteFrame fr = GhostCelesteFrame(g, i0, path);
		bool hair = fr != null && fr.HasHair && assets.Bangs.Count > 0 && assets.HairNode != null && alpha > 0.01f;
		ghostBangs.enabled = hair;
		foreach (SpriteRenderer r in ghostHair)
		{
			r.enabled = hair;
		}
		if (hair)
		{
			Vector3 n0 = at + new Vector3(fr.HairOffset.x * ghostDir * px, (9f - fr.HairOffset.y) * px, -0.0001f);
			Color hc = g.HasDash ? GhostRed : GhostBlue;
			Color ht = new Color(hc.r, hc.g, hc.b, 0.75f * alpha);
			ghostBangs.sprite = assets.Bangs[Mathf.Clamp(fr.HairFrame, 0, assets.Bangs.Count - 1)];
			ghostBangs.color = ht;
			ghostBangs.transform.position = n0;
			ghostBangs.transform.localScale = new Vector3(ghostDir * vs, vs, 1f);
			// the hair trails behind the way it moves
			Vector2 vel = i0 > 0 ? (g.P - path.F[i0 - 1].P) * 60f : Vector2.zero;
			Vector2 lean = new Vector2(-ghostDir * (1.2f + Mathf.Clamp(Math.Abs(vel.x) / 200f, 0f, 1.2f)), -1.5f + Mathf.Clamp(-vel.y / 200f, -1f, 1f));
			for (int i = 0; i < ghostHair.Length; i++)
			{
				float s = vs * (1f - (i + 1) * 0.2f);
				ghostHair[i].sprite = assets.HairNode;
				ghostHair[i].color = ht;
				ghostHair[i].transform.position = n0 + new Vector3(lean.x * (i + 1) * px, lean.y * (i + 1) * px, 0.0001f * (i + 1));
				ghostHair[i].transform.localScale = new Vector3(s, s, 1f);
			}
		}
	}

	/// <summary>Starts the ghost for a lesson; false when it can't be shown here (the wall bounce needs a wall).</summary>
	private bool StartGhost(int lesson, Vector3 feet)
	{
		Vector2 o = (Vector2)feet + new Vector2(0f, 0.6f);
		if (TechTips.Lessons[lesson].NeedsWall)
		{
			int side = 0;
			float best = 2.5f;
			RaycastHit2D wall = default(RaycastHit2D);
			foreach (int d in new int[2] { 1, -1 })
			{
				RaycastHit2D hit = Physics2D.Raycast(o, new Vector2(d, 0f), 2.5f, 33562880);
				// tall enough to bounce off: solid a little higher too
				if (hit.collider != null && hit.distance < best && Physics2D.Raycast(o + new Vector2(0f, 1.5f), new Vector2(d, 0f), hit.distance + 0.3f, 33562880).collider != null)
				{
					best = hit.distance;
					side = d;
					wall = hit;
				}
			}
			if (side == 0)
			{
				return false;
			}
			ghostFrom = new Vector3(wall.point.x - side * 0.5f, feet.y, feet.z);
			ghostDir = -side;
			ghostRoom = Room(new Vector2(ghostFrom.x, o.y), ghostDir);
		}
		else
		{
			float right = Room(o, 1);
			float left = Room(o, -1);
			ghostDir = TechRoom.Active ? 1 : Math.Abs(right - left) < 2f ? (ctrl.Facing >= 0 ? 1 : -1) : right >= left ? 1 : -1;   // the tutorial goes right
			ghostRoom = ghostDir > 0 ? right : left;
			// it stands a step ahead of her, not on top of her
			float ahead = Mathf.Clamp(ghostRoom - 4f, 0f, 1.5f);
			ghostFrom = feet + new Vector3(ghostDir * ahead, 0f, 0f);
			ghostRoom -= ahead;
		}
		ghostLesson = lesson;
		ghostFrame = 0f;
		ghostLastStep = -1;
		ghostLastFrame = -1;
		bubbleHold = 8f;
		TechTips.Introduce(lesson);
		Plugin.Log.LogInfo("Tech ghost: showing " + TechTips.Lessons[lesson].Id + " (file " + TechTips.Slot + ", " + TechTips.Count(lesson) + "/" + TechTips.Reps + ")");
		return true;
	}

	private static float Room(Vector2 o, int dir)
	{
		RaycastHit2D hit = Physics2D.Raycast(o, new Vector2(dir, 0f), 30f, 33562880);
		return hit.collider != null ? hit.distance : 30f;
	}

	private CelesteFrame GhostCelesteFrame(GhostFrame g, int i0, GhostPath path)
	{
		if (!assets.Anims.TryGetValue(g.Anim, out CelesteAnim an) || an.Frames.Count == 0)
		{
			assets.Anims.TryGetValue("idle", out an);
		}
		if (an == null || an.Frames.Count == 0)
		{
			return null;
		}
		// how long this animation has been going in the loop, for its frame
		int start = i0;
		while (start > 0 && path.F[start - 1].Anim == g.Anim)
		{
			start--;
		}
		float inAnim = (i0 - start) / 60f;
		int fi = 0;
		if (an.Delay > 0f)
		{
			fi = an.Loop ? (int)(inAnim / an.Delay) % an.Frames.Count : Math.Min(an.Frames.Count - 1, (int)(inAnim / an.Delay));
		}
		return an.Frames[fi];
	}

	private bool DrawGhostSprite(SpriteRenderer r, GhostFrame g, int i0, GhostPath path, Vector3 at, float vs, Color tint)
	{
		CelesteFrame fr = GhostCelesteFrame(g, i0, path);
		if (fr == null)
		{
			r.enabled = false;
			return false;
		}
		r.enabled = tint.a > 0.01f;
		r.sprite = fr.Sprite;
		r.color = tint;
		r.transform.position = at;
		r.transform.localScale = new Vector3(ghostDir * vs, vs, 1f);
		return true;
	}

	private void SetGhostVisible(bool on)
	{
		if (ghostBody == null)
		{
			return;
		}
		if (!on)
		{
			ghostBody.enabled = false;
			ghostBangs.enabled = false;
			foreach (SpriteRenderer r in ghostHair)
			{
				r.enabled = false;
			}
			foreach (SpriteRenderer r in ghostTrail)
			{
				r.enabled = false;
			}
		}
	}

	private void HideTechGhost()
	{
		SetGhostVisible(false);
		foreach (SpriteRenderer[] rs in roomGhosts)
		{
			foreach (SpriteRenderer r in rs)
			{
				r.enabled = false;
			}
		}
		TechTips.Showing = -1;
	}

	// ---- the Tech Tutorial's ghost runs: Celeste's own recordings, played where Celeste plays them ----------------

	private readonly List<SpriteRenderer[]> roomGhosts = new List<SpriteRenderer[]>();

	private float roomBubble;

	private int lastRoomShown = -1;

	private void UpdateRoomGhosts(Vector3 feet, float vs, float dt)
	{
		List<TechRoom.Playback> pbs = TechRoom.Playbacks;
		while (roomGhosts.Count < pbs.Count)
		{
			SpriteRenderer[] made = new SpriteRenderer[5];
			for (int i = 0; i < made.Length; i++)
			{
				made[i] = MakeRenderer("TechRoomGhost" + roomGhosts.Count + "_" + i, 0);
			}
			roomGhosts.Add(made);
		}
		float px = vs / 8f;
		int shown = -1;
		for (int gi = 0; gi < roomGhosts.Count; gi++)
		{
			SpriteRenderer[] rs = roomGhosts[gi];
			foreach (SpriteRenderer r in rs)
			{
				r.enabled = false;
			}
			if (gi >= pbs.Count)
			{
				continue;
			}
			TechRoom.Playback pb = pbs[gi];
			if (!TechRoom.Active || feet.x < pb.MinX - 1f || feet.x > pb.MaxX + 1f)
			{
				pb.T = 0f;
				continue;
			}
			if (shown < 0)
			{
				shown = gi;
			}
			List<TechRoomData.PlaybackFrame> f = pb.Frames;
			float dur = f[f.Count - 1].T;
			pb.T += dt;
			float t = pb.T % (dur + 1f);
			if (t > dur)
			{
				continue;   // a breath between runs, like Celeste's
			}
			int i0 = Mathf.Clamp((int)(t * 60f), 0, f.Count - 1);
			while (i0 + 1 < f.Count && f[i0 + 1].T <= t)
			{
				i0++;
			}
			while (i0 > 0 && f[i0].T > t)
			{
				i0--;
			}
			TechRoomData.PlaybackFrame fr = f[i0];
			int start = i0;
			while (start > 0 && f[start - 1].Anim == fr.Anim)
			{
				start--;
			}
			string animName = string.IsNullOrEmpty(fr.Anim) ? "idle" : fr.Anim;
			if (!assets.Anims.TryGetValue(animName, out CelesteAnim an) || an.Frames.Count == 0)
			{
				assets.Anims.TryGetValue(animName.StartsWith("dream") ? "dash" : "idle", out an);
			}
			if (an == null || an.Frames.Count == 0)
			{
				continue;
			}
			float inAnim = t - f[start].T;
			int fi = 0;
			if (an.Delay > 0f)
			{
				fi = an.Loop ? (int)(inAnim / an.Delay) % an.Frames.Count : Math.Min(an.Frames.Count - 1, (int)(inAnim / an.Delay));
			}
			CelesteFrame cf = an.Frames[fi];
			Vector2 d = fr.P - f[0].P;
			Vector3 at = pb.Origin + new Vector3(d.x / 8f, -d.y / 8f, 0.0015f);
			int face = fr.Facing < 0 ? -1 : 1;
			rs[0].enabled = true;
			rs[0].sprite = cf.Sprite;
			rs[0].color = new Color(GhostBody.r, GhostBody.g, GhostBody.b, 0.6f);
			rs[0].transform.position = at;
			rs[0].transform.localScale = new Vector3(face * vs, vs, 1f);
			if (cf.HasHair && assets.Bangs.Count > 0 && assets.HairNode != null)
			{
				Color ht = new Color(fr.Hair.r / 255f, fr.Hair.g / 255f, fr.Hair.b / 255f, 0.75f);
				Vector3 n0 = at + new Vector3(cf.HairOffset.x * face * px, (9f - cf.HairOffset.y) * px, -0.0001f);
				rs[1].enabled = true;
				rs[1].sprite = assets.Bangs[Mathf.Clamp(cf.HairFrame, 0, assets.Bangs.Count - 1)];
				rs[1].color = ht;
				rs[1].transform.position = n0;
				rs[1].transform.localScale = new Vector3(face * vs, vs, 1f);
				Vector2 vel = i0 > 0 ? (fr.P - f[i0 - 1].P) * 60f : Vector2.zero;
				Vector2 lean = new Vector2(-face * (1.2f + Mathf.Clamp(Math.Abs(vel.x) / 200f, 0f, 1.2f)), -1.5f + Mathf.Clamp(vel.y / 200f, -1f, 1f));
				for (int i = 0; i < 3; i++)
				{
					float s = vs * (1f - (i + 1) * 0.2f);
					rs[2 + i].enabled = true;
					rs[2 + i].sprite = assets.HairNode;
					rs[2 + i].color = ht;
					rs[2 + i].transform.position = n0 + new Vector3(lean.x * (i + 1) * px, lean.y * (i + 1) * px, 0.0001f * (i + 1));
					rs[2 + i].transform.localScale = new Vector3(s, s, 1f);
				}
			}
		}
		// the card for where she is (user 2026-10-20: every part shows its keys): the start, the wavedash run, the
		// practice room; the super and hyper gaps have theirs from the ghost above
		if (!TechRoom.Active || !TechTips.On || TechRoom.SectionLesson(feet.x) >= 0)
		{
			roomBubble = 0f;
			return;
		}
		roomBubble = Mathf.MoveTowards(roomBubble, 1f, dt / 0.2f);
		TechTips.BubbleAlpha = roomBubble;
		TechTips.Facing = 1;
		TechTips.ActiveStep = -1;
		TechTips.DemoCursor = float.NaN;
		TechTips.DemoJump = float.NaN;
		TechTips.CardRows.Clear();
		TechTips.Step dash = new TechTips.Step { Dir = 0, Dash = true };
		TechTips.Step jump = new TechTips.Step { Dir = -1, Jump = true };
		if (TechRoom.EnemyHint)
		{
			string done = TechRoom.PracticedList;
			TechTips.ExtraName = "Practice";
			TechTips.CardRows.Add(new TechTips.CardRow { Label = "Super", Steps = TechTips.Lessons[0].Steps });
			TechTips.CardRows.Add(new TechTips.CardRow { Label = "Hyper", Steps = TechTips.Lessons[1].Steps });
			TechTips.CardRows.Add(new TechTips.CardRow { Label = "Wavedash", Steps = TechTips.Lessons[2].Steps });
			TechTips.ExtraHow = "For the whole of any dash, and every super, hyper and wavedash, nothing can hurt her: use that to go right through enemies. Mix the moves together for the most fun, as long as you like. They never die.\nGo all the way right to leave the tutorial." + (done.Length > 0 ? "\nDone on them: " + done : "");
			TechTips.Showing = -3;
		}
		else if (shown >= 0 && pbs[shown].Lesson >= 0 || TechRoom.PastStart(feet.x))
		{
			TechTips.Introduce(2);
			TechTips.Showing = 2;   // the wavedash, with Celeste's ghost run
		}
		else
		{
			TechTips.ExtraName = "Tech Tutorial";
			TechTips.CardRows.Add(new TechTips.CardRow { Label = "Jump", Steps = new TechTips.Step[1] { jump } });
			TechTips.CardRows.Add(new TechTips.CardRow { Label = "Dash", Steps = new TechTips.Step[1] { dash } });
			TechTips.ExtraHow = "Hold a direction while you press Dash to aim it, any of 8 ways. Touch the ground to get the dash back.\nHead right: each part teaches one move, with a ghost to copy.";
			TechTips.Showing = -3;
		}
	}
}
