using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using InControl;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Tech Tips (user 2026-10-20: players don't know what a super or a hyper is, and not everyone reads English): when
/// Madeline stands still, a see-through ghost Madeline shows the next Celeste technique beside her, slowed down at the
/// moment that matters, with the player's own buttons above it. Each move is learned by doing it 3 times. Missed tries
/// are coached: a timing bar shows where the jump landed against the window (early or late), and the step that went
/// wrong shakes. Progress is per save file; deleting the file brings the lessons back. Super, hyper, wavedash, then
/// the wall bounce once Cling Grip is found. Settings → Game → Tech Tips.
/// </summary>
public static class TechTips
{
	public struct Step
	{
		public int Dir;   // -1 none, else 0-7 (counter-clockwise from right, like the stick) relative to facing right

		public bool Dash;

		public bool Jump;
	}

	public struct Lesson
	{
		public string Id;

		public string Name;

		public Step[] Steps;

		public int TimedStep;   // the jump that must land inside the dash window

		public bool NeedsWall;

		public string How;   // the words under the buttons (user 2026-10-20: say how, and say when it's diagonal)
	}

	public const int Reps = 3;

	// down-diagonal is direction 7 (down-right) when facing right; up is 2
	public static readonly Lesson[] Lessons = new Lesson[4]
	{
		new Lesson { Id = "super", Name = "Super", TimedStep = 1, How = "On the ground, dash {fwd}, then press Jump while the dash is still going. You keep its speed in a long, fast jump.", Steps = new Step[2] { new Step { Dir = 0, Dash = true }, new Step { Dir = -1, Jump = true } } },
		new Lesson { Id = "hyper", Name = "Hyper", TimedStep = 1, How = "On the ground, hold DOWN and {fwd} together (diagonal) and dash, then press Jump while still dashing. Lower and faster than a super.", Steps = new Step[2] { new Step { Dir = 7, Dash = true }, new Step { Dir = -1, Jump = true } } },
		new Lesson { Id = "wavedash", Name = "Wavedash", TimedStep = 2, How = "Jump, then hold DOWN and {fwd} together (diagonal) and dash into the ground. Press Jump as you land, still dashing. Your dash comes back!", Steps = new Step[3] { new Step { Dir = -1, Jump = true }, new Step { Dir = 7, Dash = true }, new Step { Dir = -1, Jump = true } } },
		new Lesson { Id = "wallbounce", Name = "Wall Bounce", TimedStep = 1, NeedsWall = true, How = "Right next to a wall, hold UP and dash straight up, then press Jump while still dashing. She bounces off the wall, high and away.", Steps = new Step[2] { new Step { Dir = 2, Dash = true }, new Step { Dir = -1, Jump = true } } }
	};

	public static bool On => true;   // the tips only show in the Tech Tutorial now (user 2026-10-21), so no setting

	// ---- progress, per save file --------------------------------------------------------------------------------

	private static Dictionary<int, Dictionary<string, int>> saves;

	private static string FilePath => Path.Combine(AssistMode.DataDir ?? "", "tech_tips_saves.cfg");

	public static int Slot
	{
		get
		{
			GameManager gm = GameManager.SilentInstance;
			return gm != null ? gm.profileID : -1;
		}
	}

	private static Dictionary<string, int> Progress(int slot)
	{
		if (saves == null)
		{
			saves = new Dictionary<int, Dictionary<string, int>>();
			try
			{
				if (File.Exists(FilePath))
				{
					foreach (string l in File.ReadAllLines(FilePath))
					{
						// 2=super:3,hyper:1
						string[] kv = l.Split('=');
						if (kv.Length != 2 || !int.TryParse(kv[0], out int s))
						{
							continue;
						}
						Dictionary<string, int> d = new Dictionary<string, int>();
						foreach (string part in kv[1].Split(','))
						{
							string[] p = part.Split(':');
							if (p.Length == 2 && int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
							{
								d[p[0].Trim()] = n;
							}
						}
						saves[s] = d;
					}
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning("Tech tips: " + e.Message);
			}
		}
		if (!saves.TryGetValue(slot, out Dictionary<string, int> got))
		{
			got = saves[slot] = new Dictionary<string, int>();
		}
		return got;
	}

	private static void Save()
	{
		try
		{
			List<string> lines = new List<string>();
			foreach (KeyValuePair<int, Dictionary<string, int>> s in saves)
			{
				List<string> parts = new List<string>();
				foreach (KeyValuePair<string, int> p in s.Value)
				{
					parts.Add(p.Key + ":" + p.Value.ToString(CultureInfo.InvariantCulture));
				}
				if (parts.Count > 0)
				{
					lines.Add(s.Key + "=" + string.Join(",", parts.ToArray()));
				}
			}
			Directory.CreateDirectory(AssistMode.DataDir);
			File.WriteAllLines(FilePath, lines.ToArray());
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Tech tips: " + e.Message);
		}
	}

	public static int Count(int lesson)
	{
		int slot = Slot;
		if (slot < 0 || lesson < 0)
		{
			return 0;
		}
		Progress(slot).TryGetValue(Lessons[lesson].Id, out int n);
		return n;
	}

	public static bool Done(int lesson) => Count(lesson) >= Reps;

	private static bool Available(int lesson)
	{
		return !Lessons[lesson].NeedsWall || (PlayerData.instance != null && MadelineController.Unlocked(Unlocks.GrabClimb) && !AssistMode.NoWallJump);
	}

	/// <summary>The lesson being taught on this file: the first one not learned yet, or -1.</summary>
	public static int Current
	{
		get
		{
			if (Slot < 0)
			{
				return -1;
			}
			for (int i = 0; i < Lessons.Length; i++)
			{
				if (!Done(i))
				{
					return Available(i) ? i : -1;
				}
			}
			return -1;
		}
	}

	/// <summary>A deleted save file starts the lessons over (CelesteFileSelect's delete hook).</summary>
	public static void ForgetSlot(int slot)
	{
		Progress(slot);
		if (saves.Remove(slot))
		{
			Save();
		}
	}

	// ---- what the coach shows -----------------------------------------------------------------------------------

	/// <summary>The lesson the bubble is showing (-1 none), the ghost's step, which way it runs, and where (world).</summary>
	public static int Showing = -1;

	public static int ActiveStep = -1;

	public static int Facing = 1;

	public static Vector3 BubbleAt;

	public static float BubbleAlpha;

	/// <summary>The bubble sits at the top of the screen instead of over the ghost (the Tech Tutorial).</summary>
	public static bool Pinned;

	/// <summary>The Tech Tutorial's tests: another name and words for the move's bubble (null: the lesson's own).</summary>
	public static string NameOverride;

	/// <summary>The training room's step: how many done of how many (need 0: none).</summary>
	public static int PipsDone;

	public static int PipsNeed;

	public static string HowOverride;

	/// <summary>A short line under the words: how the last try went.</summary>
	public static string NoteText = "";

	public static float NoteAt = -10f;

	public static bool NoteGood;

	public static void Note(string text, bool good)
	{
		NoteText = text;
		NoteAt = Time.unscaledTime;
		NoteGood = good;
	}

	/// <summary>The last move done for real, and when.</summary>
	public static string LastTechId = "";

	public static float LastTechAt = -10f;

	/// <summary>Showing -3: a card of labelled button rows (the Tech Tutorial's start and practice room).</summary>
	public struct CardRow
	{
		public string Label;

		public Step[] Steps;
	}

	public static readonly List<CardRow> CardRows = new List<CardRow>();

	/// <summary>Showing -2: a bubble with just a name and words (the Tech Tutorial's combo run).</summary>
	public static string ExtraName = "";

	public static string ExtraHow = "";

	public static string HowText(int lesson)
	{
		string t = lesson == -2 ? ExtraHow : lesson >= 0 ? Lessons[lesson].How ?? "" : "";
		return t.Replace("{fwd}", Facing >= 0 ? "RIGHT" : "LEFT");
	}

	/// <summary>The ghost's head (world) and when it pressed each step, for the button that pops over it.</summary>
	public static Vector3 GhostHead;

	public static float GhostPressAt = -10f;

	public static int GhostPressStep = -1;

	/// <summary>The ghost's own timing: time into its window, and when it jumped (seconds; NaN when not in it).</summary>
	public static float DemoCursor = float.NaN;

	public static float DemoJump = float.NaN;

	public static float DemoZone = 0.15f;

	/// <summary>A move done for real: the pip that fills, and the check over her head when one is learned.</summary>
	public static float RepAt = -10f;

	public static int RepLesson = -1;

	public static float DoneAt = -10f;

	public static string DoneName = "";

	public static Vector3 DoneOver;

	public static Vector3 Head;

	/// <summary>The player's last try at the lesson being taught: where the jump was against the window.</summary>
	public struct Try
	{
		public float At;

		public int Lesson;

		public float Offset;   // seconds from the window opening (negative: early)

		public float Zone;   // the window's length

		public bool Ok;
	}

	public static Try LastTry = new Try { At = -10f, Lesson = -1 };

	/// <summary>A step that went wrong shakes red for a moment.</summary>
	public static float HintAt = -10f;

	public static int HintLesson = -1;

	public static int HintStep = -1;

	/// <summary>The last time the player tried the lesson being taught (keeps the bubble up while practising).</summary>
	public static float PracticeAt = -10f;

	// ---- watching the real moves (called from MadelineController) -----------------------------------------------

	private enum Kind
	{
		None,
		Side,
		GroundDiag,
		AirDiag,
		Up
	}

	private static Kind dashKind;

	private static float dashAt = -10f;

	private static float dashLen = 0.15f;

	private static float landAt = -1f;

	private static float dashEndAt = -10f;

	private static float jumpAt = -10f;

	private static bool resolved = true;

	private static float Now => Time.unscaledTime;

	private static int LessonFor(Kind k)
	{
		switch (k)
		{
		case Kind.Side:
			return 0;
		case Kind.GroundDiag:
			return 1;
		case Kind.AirDiag:
			return 2;
		case Kind.Up:
			return 3;
		default:
			return -1;
		}
	}

	private static float ZoneStart => dashKind == Kind.AirDiag ? landAt : dashAt;

	private static float ZoneLen => dashKind == Kind.AirDiag ? Math.Max(0.02f, dashLen - (landAt - dashAt)) : dashLen;

	private static bool Coaching(int lesson)
	{
		// in the Tech Tutorial, the move its ghost is showing is coached too
		return On && lesson >= 0 && (lesson == Current || TechRoom.Active && lesson == Showing) && Introduced(lesson);
	}

	/// <summary>A yes/no note kept with the file's lessons (deleting the file clears it too).</summary>
	public static bool Flag(string id)
	{
		int slot = Slot;
		return slot >= 0 && Progress(slot).TryGetValue("#" + id, out int v) && v > 0;
	}

	public static void SetFlag(string id)
	{
		int slot = Slot;
		if (slot >= 0)
		{
			Progress(slot)["#" + id] = 1;
			Save();
		}
	}

	private static readonly HashSet<string> introduced = new HashSet<string>();

	/// <summary>Coaching starts once the ghost has shown that move on this file (no pop-ups for players who never stopped).</summary>
	public static bool Introduced(int lesson) => lesson >= 0 && introduced.Contains(Slot + ":" + Lessons[lesson].Id);

	public static void Introduce(int lesson)
	{
		if (lesson >= 0)
		{
			introduced.Add(Slot + ":" + Lessons[lesson].Id);
		}
	}

	private static void Record(int lesson, float offset, float zone, bool ok)
	{
		LastTry = new Try { At = Now, Lesson = lesson, Offset = offset, Zone = zone, Ok = ok };
		PracticeAt = Now;
	}

	private static void Hint(int lesson, int step)
	{
		HintAt = Now;
		HintLesson = lesson;
		HintStep = step;
		PracticeAt = Now;
	}

	public static void OnDash(Vector2 dir, bool ground, float len, bool nearWall)
	{
		// this controller's y points down
		Kind k = Kind.None;
		if (dir.x != 0f && Math.Abs(dir.y) < 0.1f)
		{
			k = ground ? Kind.Side : Kind.None;
		}
		else if (dir.x != 0f && dir.y > 0.1f)
		{
			k = ground ? Kind.GroundDiag : Kind.AirDiag;
		}
		else if (Math.Abs(dir.x) <= 0.2f && dir.y <= -0.75f && nearWall)
		{
			k = Kind.Up;
		}
		// jumped first, then dashed: early for the super and the hyper (a jump then a down dash is the wavedash itself)
		float early = jumpAt - Now;
		int cur = Current;
		if (early > -0.25f && !ground)
		{
			if (cur == 0 && dir.x != 0f && Math.Abs(dir.y) < 0.1f && Coaching(0))
			{
				Record(0, early, len, ok: false);
				Hint(0, 1);
			}
			else if (cur == 1 && k == Kind.AirDiag && Coaching(1))
			{
				Record(1, early, len, ok: false);
				Hint(1, 1);
			}
		}
		TechRoom.OnTrainDash(k == Kind.Side ? 1 : k == Kind.GroundDiag ? 2 : k == Kind.AirDiag ? 3 : 0);
		dashKind = k;
		dashAt = Now;
		dashLen = len;
		landAt = ground ? Now : -1f;
		dashEndAt = -10f;
		resolved = k == Kind.None;
	}

	public static void OnDashLanded()
	{
		if (dashKind == Kind.AirDiag && landAt < 0f)
		{
			landAt = Now;
			TechRoom.OnTrainLanded();
		}
	}

	public static void OnDashEnd()
	{
		TechRoom.OnTrainDashEnd();
		dashEndAt = Now;
		if (!resolved && dashKind == Kind.AirDiag && landAt < 0f)
		{
			// the down dash ended before reaching the ground: dash closer to it
			resolved = true;
			if (Coaching(2))
			{
				Hint(2, 1);
			}
		}
	}

	/// <summary>A plain jump or wall jump: right after a dash that could have been a move, it was too late.</summary>
	public static void OnJump()
	{
		TechRoom.OnTrainJump();
		jumpAt = Now;
		if (resolved || dashEndAt < 0f || Now - dashEndAt > 0.3f)
		{
			return;
		}
		resolved = true;
		int l = LessonFor(dashKind);
		if (dashKind == Kind.AirDiag && landAt < 0f)
		{
			return;
		}
		if (Coaching(l))
		{
			Record(l, Now - ZoneStart, ZoneLen, ok: false);
			Hint(l, Lessons[l].TimedStep);
		}
	}

	/// <summary>The move itself, done: counts toward learning it.</summary>
	public static void OnTech(string id)
	{
		TechRoom.OnTrainTech(id);
		int l = Array.FindIndex(Lessons, (Lesson x) => x.Id == id);
		if (l < 0)
		{
			return;
		}
		jumpAt = Now;
		LastTechId = id;
		LastTechAt = Now;
		int cur = Current;
		float offset = resolved ? ZoneLen * 0.5f : Now - ZoneStart;
		float zone = resolved ? 0.15f : ZoneLen;
		resolved = true;
		// the wrong move for the lesson being taught: point at the step that differs
		if (cur == 1 && l == 0 && Coaching(1))
		{
			Hint(1, 0);   // aim down-diagonal
		}
		else if (cur == 2 && l == 1 && Coaching(2))
		{
			Hint(2, 0);   // jump first
		}
		int slot = Slot;
		if (slot < 0 || Done(l))
		{
			return;
		}
		Dictionary<string, int> p = Progress(slot);
		p[id] = Count(l) + 1;
		Save();
		bool coached = Coaching(l) || l == cur && On;
		if (coached)
		{
			Record(l, offset, zone, ok: true);
		}
		if (p[id] >= Reps)
		{
			Plugin.Log.LogInfo("Tech tips: " + id + " learned on file " + slot);
			if (On)
			{
				DoneAt = Now;
				DoneName = Lessons[l].Name;
				DoneOver = Head;
				Sfx.Play(Sounds.FeatherGet.Id);
			}
		}
		else if (coached && On)
		{
			RepAt = Now;
			RepLesson = l;
			Sfx.Play(Sounds.Refill.Id);
		}
	}

	// ---- the player's own buttons ------------------------------------------------------------------------------

	private static UIButtonSkins skins;

	private static float skinsTried = -10f;

	private static readonly Dictionary<string, KeyValuePair<float, object>> labelCache = new Dictionary<string, KeyValuePair<float, object>>();

	/// <summary>The controller glyph for Dash (Silksong's Attack) or Jump, as bound; null when there is none.</summary>
	public static Sprite PadGlyph(bool dash)
	{
		string key = dash ? "pad_dash" : "pad_jump";
		if (labelCache.TryGetValue(key, out KeyValuePair<float, object> c) && Now - c.Key < 1f)
		{
			return c.Value as Sprite;
		}
		Sprite s = null;
		try
		{
			InputHandler ih = ManagerSingleton<InputHandler>.Instance;
			if (skins == null && Now - skinsTried > 5f)
			{
				skinsTried = Now;
				UIManager ui = UIManager.instance;
				skins = ui != null ? ui.uiButtonSkins : null;
			}
			if (ih != null && ih.inputActions != null && skins != null)
			{
				ButtonSkin b = skins.GetControllerButtonSkinFor(dash ? ih.inputActions.Attack : ih.inputActions.Jump);
				s = b != null ? b.sprite : null;
			}
		}
		catch (Exception)
		{
		}
		labelCache[key] = new KeyValuePair<float, object>(Now, s);
		return s;
	}

	/// <summary>The key or mouse button for Dash or Jump, as bound.</summary>
	public static string KeyLabel(bool dash)
	{
		string key = dash ? "key_dash" : "key_jump";
		if (labelCache.TryGetValue(key, out KeyValuePair<float, object> c) && Now - c.Key < 1f)
		{
			return (string)c.Value;
		}
		string s = dash ? "X" : "Z";
		try
		{
			InputHandler ih = ManagerSingleton<InputHandler>.Instance;
			if (ih != null && ih.inputActions != null)
			{
				InputHandler.KeyOrMouseBinding b = ih.GetKeyBindingForAction(dash ? ih.inputActions.Attack : ih.inputActions.Jump);
				if (b.Mouse != InControl.Mouse.None)
				{
					s = b.Mouse == InControl.Mouse.LeftButton ? "Left Click" : b.Mouse == InControl.Mouse.RightButton ? "Right Click" : b.Mouse == InControl.Mouse.MiddleButton ? "Middle Click" : b.Mouse.ToString();
				}
				else if (b.Key != Key.None)
				{
					s = Pretty(b.Key.ToString());
				}
			}
		}
		catch (Exception)
		{
		}
		labelCache[key] = new KeyValuePair<float, object>(Now, s);
		return s;
	}

	private static string Pretty(string k)
	{
		switch (k)
		{
		case "LeftShift":
			return "L Shift";
		case "RightShift":
			return "R Shift";
		case "LeftControl":
			return "L Ctrl";
		case "RightControl":
			return "R Ctrl";
		case "LeftAlt":
			return "L Alt";
		case "RightAlt":
			return "R Alt";
		case "Return":
			return "Enter";
		default:
			if (k.StartsWith("Key") && k.Length == 4)
			{
				return k.Substring(3);
			}
			if (k.StartsWith("Pad") && k.Length == 4)
			{
				return "Num " + k.Substring(3);
			}
			return k;
		}
	}
}

/// <summary>Draws the Tech Tips bubble: the move's name and pips, one pill per step with the player's buttons, the
/// timing bar, the button popping over the ghost, and the check when a move is learned.</summary>
public class TechTipsGui : MonoBehaviour
{
	private bool padHints;

	private Rect lastBubble;

	private void Update()
	{
		padHints = MenuMouse.PadHints(padHints);
		PadNow = padHints;
	}

	/// <summary>The player last used a controller (for button pictures outside this class).</summary>
	public static bool PadNow;

	public static void DrawSpriteAt(Sprite s, Rect r, Color col)
	{
		DrawSprite(s, r, col);
	}

	private static readonly Color Panel = new Color(0.06f, 0.06f, 0.12f, 1f);

	private static readonly Color Gold = new Color(1f, 0.85f, 0.35f, 1f);

	private static readonly Color Ink = new Color(0.1f, 0.08f, 0.12f, 1f);

	private static readonly Color Good = new Color(0.45f, 1f, 0.5f, 1f);

	private static readonly Color Bad = new Color(1f, 0.4f, 0.4f, 1f);

	private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

	private static void Box(Rect r, Color c, float radius)
	{
		GUI.color = Color.white;
		GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, alphaBlend: true, 0f, c, 0f, Math.Min(radius, Math.Min(r.width, r.height) / 2f));
	}

	private void OnGUI()
	{
		GuiArt art = AssistMode.Art;
		Camera cam = Camera.main;
		if (art == null || cam == null || Event.current.type != EventType.Repaint || TheoCamera.Hiding || AssistMode.IsOpen || BossCard.RecordsOpen)
		{
			return;
		}
		float k = Screen.height / 1080f;
		float sw = Screen.width / k;
		Matrix4x4 old = GUI.matrix;
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(k, k, 1f));
		if ((TechTips.Showing >= 0 || TechTips.Showing == -2 || TechTips.Showing == -3) && TechTips.BubbleAlpha > 0.01f)
		{
			DrawBubble(art, cam, k, sw);
			if (TechTips.Showing >= 0)
			{
				DrawGhostPress(art, cam, k);
			}
		}
		DrawLearned(art, cam, k);
		GUI.matrix = old;
		GUI.color = Color.white;
	}

	private float PillWidth(GuiArt art, TechTips.Step s)
	{
		float w = 20f;
		if (s.Dir >= 0)
		{
			w += 48f;
		}
		if (s.Dash || s.Jump)
		{
			Sprite g = padHints ? TechTips.PadGlyph(s.Dash) : null;
			w += g != null ? 52f : art.Measure(TechTips.KeyLabel(s.Dash)) * 0.5f + 4f;
		}
		return w + 20f;
	}

	private void DrawBubble(GuiArt art, Camera cam, float k, float sw)
	{
		float a = TechTips.BubbleAlpha;
		int li = TechTips.Showing;
		TechTips.Lesson l = li >= 0 ? TechTips.Lessons[li] : new TechTips.Lesson { Id = "extra", Name = TechTips.ExtraName, Steps = new TechTips.Step[0] };
		float now = Time.unscaledTime;
		const float pillH = 60f;
		// the rows of buttons: the move's steps, or a card's labelled rows (user 2026-10-20: every part shows its keys)
		List<TechTips.CardRow> rows = new List<TechTips.CardRow>();
		if (li >= 0)
		{
			rows.Add(new TechTips.CardRow { Label = null, Steps = l.Steps });
		}
		else if (li == -3)
		{
			rows.AddRange(TechTips.CardRows);
		}
		float labelScale = 0.45f;
		float labelW = 0f;
		foreach (TechTips.CardRow r in rows)
		{
			if (!string.IsNullOrEmpty(r.Label))
			{
				labelW = Math.Max(labelW, art.Measure(r.Label) * labelScale + 20f);
			}
		}
		List<List<float>> widths = new List<List<float>>();
		float rowsW = 0f;
		foreach (TechTips.CardRow r in rows)
		{
			List<float> ws = new List<float>();
			float sum = labelW;
			foreach (TechTips.Step s in r.Steps)
			{
				float w = PillWidth(art, s);
				ws.Add(w);
				sum += w;
			}
			sum += Math.Max(0, r.Steps.Length - 1) * 34f;
			widths.Add(ws);
			rowsW = Math.Max(rowsW, sum);
		}
		float rowsH = rows.Count * pillH + Math.Max(0, rows.Count - 1) * 10f;
		float nameScale = 0.55f;
		string title = TechTips.NameOverride ?? (li == -3 ? TechTips.ExtraName : l.Name);
		float nameW = art.Measure(title) * nameScale;
		int pipTotal = TechTips.PipsNeed > 0 ? TechTips.PipsNeed : li >= 0 && TechTips.NameOverride == null ? TechTips.Reps : 0;
		int pipFull = TechTips.PipsNeed > 0 ? TechTips.PipsDone : TechTips.Count(li);
		float pipsW = pipTotal * 26f;
		float headW = nameW + 24f + pipsW;
		float inner = Math.Max(rowsW, Math.Max(headW, 560f));
		float headH = art.LineHeight * nameScale + 6f;
		float zone = 0f, cursor = float.NaN, jump = float.NaN, barAlpha = 0f;
		Color jumpCol = Gold;
		bool showBar = li >= 0 && ShowBar(li, now, out zone, out cursor, out jump, out jumpCol, out barAlpha);
		float barH = 34f;
		// the words: how to do it, in plain steps
		float howScale = 0.42f;
		string howText = TechTips.HowOverride != null ? TechTips.HowOverride.Replace("{fwd}", TechTips.Facing >= 0 ? "RIGHT" : "LEFT") : li == -3 ? TechTips.ExtraHow : TechTips.HowText(li);
		List<string> how = new List<string>();
		foreach (string para in howText.Split('\n'))
		{
			how.AddRange(art.Wrap(para, inner / howScale));
		}
		bool noteOn = now - TechTips.NoteAt < 2.5f && TechTips.NoteText.Length > 0;
		if (noteOn)
		{
			how.Add("");
		}
		float howH = how.Count * art.LineHeight * howScale;
		float h = 16f + headH + 10f + rowsH + (howH > 0f ? 12f + howH : 0f) + (showBar ? 30f + barH : 0f) + 16f;
		float w0 = inner + 36f;
		Vector3 sp = cam.WorldToScreenPoint(TechTips.BubbleAt);
		Vector2 at = new Vector2(sp.x, Screen.height - sp.y) / k;
		float x0 = Mathf.Clamp(at.x - w0 / 2f, 16f, sw - w0 - 16f);
		float y0 = Mathf.Clamp(at.y - h - 24f, 16f, 1080f - h - 16f);
		if (TechTips.Pinned)
		{
			x0 = sw / 2f - w0 / 2f;
			y0 = 40f;
		}
		Rect bubble = new Rect(x0, y0, w0, h);
		lastBubble = bubble;
		// the panel and a little tail down toward the ghost
		Box(new Rect(bubble.x - 3f, bubble.y - 3f, bubble.width + 6f, bubble.height + 6f), new Color(1f, 1f, 1f, 0.18f * a), 16f);
		Box(bubble, A(Panel, 0.86f * a), 14f);
		if (!TechTips.Pinned)
		{
			float tailX = Mathf.Clamp(at.x, bubble.x + 24f, bubble.xMax - 24f);
			Matrix4x4 m0 = GUI.matrix;
			GUIUtility.RotateAroundPivot(45f, new Vector2(tailX, bubble.yMax) * k);
			Box(new Rect(tailX - 10f, bubble.yMax - 10f, 20f, 20f), A(Panel, 0.86f * a), 3f);
			GUI.matrix = m0;
		}
		// the move's name, and a pip per rep (the newest one pops)
		float hx = bubble.x + 18f;
		float hy = bubble.y + 14f;
		art.DrawOutlined(title, hx, hy, nameScale, A(Color.white, a), A(Color.black, a), 2f);
		int count = pipFull;
		float pipX = bubble.xMax - 18f - pipsW;
		for (int i = 0; i < pipTotal; i++)
		{
			bool full = i < count;
			float pop = TechTips.RepLesson == li && i == count - 1 ? Mathf.Clamp01(1f - (now - TechTips.RepAt) / 0.35f) : 0f;
			float r = 8f + pop * 6f;
			Vector2 c = new Vector2(pipX + 13f + i * 26f, hy + headH / 2f - 2f);
			Box(new Rect(c.x - r - 2f, c.y - r - 2f, (r + 2f) * 2f, (r + 2f) * 2f), A(Color.black, 0.6f * a), r + 2f);
			Box(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), full ? A(Gold, a) : new Color(1f, 1f, 1f, 0.18f * a), r);
		}
		// the steps, row by row
		float y = hy + headH + 10f;
		bool shaking = li >= 0 && TechTips.HintLesson == li && now - TechTips.HintAt < 0.9f;
		for (int ri = 0; ri < rows.Count; ri++)
		{
			TechTips.CardRow row = rows[ri];
			List<float> ws = widths[ri];
			float rowW = labelW;
			foreach (float w in ws)
			{
				rowW += w;
			}
			rowW += Math.Max(0, row.Steps.Length - 1) * 34f;
			float x = bubble.x + 18f + (labelW > 0f ? 0f : (inner - rowW) / 2f);
			if (!string.IsNullOrEmpty(row.Label))
			{
				art.DrawOutlined(row.Label, x, y + (pillH - art.LineHeight * labelScale) / 2f, labelScale, A(Gold, a), A(Color.black, a), 2f);
				x += labelW;
			}
			for (int i = 0; i < row.Steps.Length; i++)
			{
				TechTips.Step s = row.Steps[i];
				bool lit = li >= 0 && i == TechTips.ActiveStep;
				bool wrong = shaking && i == TechTips.HintStep;
				float shake = wrong ? Mathf.Sin((now - TechTips.HintAt) * 60f) * 6f * (1f - (now - TechTips.HintAt) / 0.9f) : 0f;
				Rect pill = new Rect(x + shake, y, ws[i], pillH);
				Color fill = wrong ? A(Bad, a) : lit ? A(Gold, a) : new Color(1f, 1f, 1f, 0.14f * a);
				Color fg = lit || wrong ? A(Ink, a) : A(Color.white, a);
				Box(pill, fill, 12f);
				float px = pill.x + 20f;
				if (s.Dir >= 0)
				{
					// a stick's ring behind the arrow, so a diagonal reads as "push to the corner"
					Vector2 sc = new Vector2(px + 20f, pill.center.y);
					Box(new Rect(sc.x - 24f, sc.y - 24f, 48f, 48f), lit || wrong ? new Color(0f, 0f, 0f, 0.25f * a) : new Color(1f, 1f, 1f, 0.22f * a), 24f);
					Box(new Rect(sc.x - 20f, sc.y - 20f, 40f, 40f), fill, 20f);
					DrawArrow(art, sc, s.Dir, TechTips.Facing, fg);
					px += 48f;
				}
				if (s.Dash || s.Jump)
				{
					Sprite g = padHints ? TechTips.PadGlyph(s.Dash) : null;
					if (g != null)
					{
						DrawSprite(g, new Rect(px, pill.center.y - 24f, 48f, 48f), A(Color.white, a));
					}
					else
					{
						string t = TechTips.KeyLabel(s.Dash);
						art.DrawOutlined(t, px, pill.y + (pillH - art.LineHeight * 0.5f) / 2f, 0.5f, fg, lit || wrong ? new Color(0f, 0f, 0f, 0f) : A(Color.black, a), 2f);
					}
				}
				x += ws[i];
				if (i < row.Steps.Length - 1)
				{
					DrawChevron(new Vector2(x + 17f, y + pillH / 2f), new Color(1f, 1f, 1f, 0.7f * a));
					x += 34f;
				}
			}
			y += pillH + 10f;
		}
		float ty = y + (rows.Count > 0 ? 2f : -10f);
		for (int i = 0; i < how.Count; i++)
		{
			bool note = noteOn && i == how.Count - 1;
			string line = note ? TechTips.NoteText : how[i];
			Color col = note ? (TechTips.NoteGood ? A(Good, a) : A(Bad, a)) : new Color(0.92f, 0.92f, 0.98f, a);
			art.DrawOutlined(line, bubble.x + 18f, ty, howScale, col, A(Color.black, a), 1.5f);
			ty += art.LineHeight * howScale;
		}
		if (showBar)
		{
			DrawBar(new Rect(bubble.x + 18f, ty + 30f, inner, barH), zone, cursor, jump, jumpCol, a * barAlpha);
		}
	}

	/// <summary>The timing bar: the ghost's while it dashes, the player's after a try at this move.</summary>
	private bool ShowBar(int li, float now, out float zone, out float cursor, out float jump, out Color jumpCol, out float alpha)
	{
		TechTips.Try t = TechTips.LastTry;
		float since = now - t.At;
		if (t.Lesson == li && since < 3f)
		{
			zone = t.Zone;
			cursor = float.NaN;
			jump = t.Offset;
			jumpCol = t.Ok ? Good : Bad;
			alpha = Mathf.Clamp01((3f - since) * 2f);
			return true;
		}
		zone = TechTips.DemoZone;
		cursor = TechTips.DemoCursor;
		jump = TechTips.DemoJump;
		jumpCol = Gold;
		alpha = 1f;
		return !float.IsNaN(cursor) || !float.IsNaN(jump);
	}

	private static void DrawBar(Rect r, float zone, float cursor, float jump, Color jumpCol, float a)
	{
		// the try in words, right of the bar title (user 2026-10-20)
		if (float.IsNaN(cursor) && !float.IsNaN(jump) && AssistMode.Art != null && jumpCol != Gold)
		{
			string word = jump < 0f ? "Too early! Dash first, then Jump." : jump > zone ? "Too late! Jump sooner." : "Perfect!";
			GuiArt art = AssistMode.Art;
			art.DrawOutlined(word, r.x, r.y - art.LineHeight * 0.4f - 2f, 0.4f, A(jump < 0f || jump > zone ? Bad : Good, a), A(Color.black, a), 1.5f);
		}
		// the bar runs from a little before the window to a little after it
		float pre = Math.Max(0.12f, zone * 0.6f);
		float post = Math.Max(0.15f, zone * 0.8f);
		float span = pre + zone + post;
		float X(float s) => r.x + Mathf.Clamp01((s + pre) / span) * r.width;
		Rect track = new Rect(r.x, r.center.y - 7f, r.width, 14f);
		Box(track, new Color(1f, 1f, 1f, 0.12f * a), 7f);
		Rect win = new Rect(X(0f), track.y, X(zone) - X(0f), track.height);
		Box(win, A(Good, 0.75f * a), 7f);
		if (!float.IsNaN(cursor))
		{
			float cx = X(cursor);
			Box(new Rect(cx - 2f, r.y + 2f, 4f, r.height - 4f), new Color(1f, 1f, 1f, 0.8f * a), 2f);
		}
		if (!float.IsNaN(jump))
		{
			float jx = X(jump);
			Box(new Rect(jx - 9f, r.center.y - 9f, 18f, 18f), A(Color.black, a), 9f);
			Box(new Rect(jx - 7f, r.center.y - 7f, 14f, 14f), A(jumpCol, a), 7f);
		}
	}

	/// <summary>The button the ghost just pressed, popping over its head.</summary>
	private void DrawGhostPress(GuiArt art, Camera cam, float k)
	{
		float since = Time.unscaledTime - TechTips.GhostPressAt;
		int si = TechTips.GhostPressStep;
		if (since > 0.6f || si < 0 || TechTips.Showing < 0 || si >= TechTips.Lessons[TechTips.Showing].Steps.Length)
		{
			return;
		}
		TechTips.Step s = TechTips.Lessons[TechTips.Showing].Steps[si];
		float a = TechTips.BubbleAlpha * Mathf.Clamp01((0.6f - since) * 4f);
		float grow = 1f + 0.35f * Mathf.Clamp01(1f - since / 0.15f);
		Vector3 sp = cam.WorldToScreenPoint(TechTips.GhostHead);
		Vector2 c = new Vector2(sp.x, Screen.height - sp.y) / k + new Vector2(0f, -30f - since * 30f);
		Sprite g = padHints ? TechTips.PadGlyph(s.Dash) : null;
		if (g != null)
		{
			float sz = 44f * grow;
			DrawSprite(g, new Rect(c.x - sz / 2f, c.y - sz / 2f, sz, sz), A(Color.white, a));
			return;
		}
		string t = TechTips.KeyLabel(s.Dash);
		float sc = 0.45f * grow;
		float tw = art.Measure(t) * sc;
		Box(new Rect(c.x - tw / 2f - 10f, c.y - art.LineHeight * sc / 2f - 4f, tw + 20f, art.LineHeight * sc + 8f), A(Gold, a), 8f);
		art.DrawLine(t, c.x - tw / 2f, c.y - art.LineHeight * sc / 2f, sc, A(Ink, a));
	}

	/// <summary>A move learned: a green check and its name over her head, rising and fading.</summary>
	private void DrawLearned(GuiArt art, Camera cam, float k)
	{
		float since = Time.unscaledTime - TechTips.DoneAt;
		if (since >= 2.2f || !TechRoom.Active)
		{
			return;   // the green check is the Tech Tutorial's only (user 2026-10-20)
		}
		Vector3 sp = cam.WorldToScreenPoint(TechTips.DoneOver);
		Vector2 c = new Vector2(sp.x, Screen.height - sp.y) / k + new Vector2(0f, -70f - since * 30f);
		float a = Mathf.Clamp01(Math.Min(since * 6f, (2.2f - since) * 2f));
		float s = 1f + 0.3f * Mathf.Clamp01(1f - since * 4f);
		Matrix4x4 m0 = GUI.matrix;
		GUI.color = new Color(0f, 0f, 0f, a);
		DrawCheck(c, 1.15f * s, 14f, k);
		GUI.color = A(Good, a);
		DrawCheck(c, s, 10f, k);
		GUI.matrix = m0;
		float sc = 0.6f;
		float tw = art.Measure(TechTips.DoneName) * sc;
		art.DrawOutlined(TechTips.DoneName, c.x - tw / 2f, c.y + 34f, sc, A(Good, a), A(Color.black, a), 3f);
	}

	private static void DrawCheck(Vector2 c, float s, float thick, float k)
	{
		Matrix4x4 m0 = GUI.matrix;
		Vector2 pivot = c + new Vector2(-14f, 8f) * s;
		GUIUtility.RotateAroundPivot(45f, pivot * k);
		GUI.DrawTexture(new Rect(pivot.x - 22f * s, pivot.y - thick / 2f, 26f * s, thick), Texture2D.whiteTexture);
		GUI.matrix = m0;
		GUIUtility.RotateAroundPivot(-50f, pivot * k);
		GUI.DrawTexture(new Rect(pivot.x - thick / 2f, pivot.y - thick / 2f, 52f * s, thick), Texture2D.whiteTexture);
		GUI.matrix = m0;
	}

	private static void DrawChevron(Vector2 c, Color col)
	{
		Matrix4x4 m0 = GUI.matrix;
		float k = m0.m00;
		GUI.color = col;
		GUIUtility.RotateAroundPivot(45f, new Vector2(c.x - 2f, c.y) * k);
		GUI.DrawTexture(new Rect(c.x - 12f, c.y - 2.5f, 12f, 5f), Texture2D.whiteTexture);
		GUI.matrix = m0;
		GUIUtility.RotateAroundPivot(-45f, new Vector2(c.x - 2f, c.y) * k);
		GUI.DrawTexture(new Rect(c.x - 12f, c.y - 2.5f, 12f, 5f), Texture2D.whiteTexture);
		GUI.matrix = m0;
	}

	private static void DrawSprite(Sprite s, Rect r, Color col)
	{
		if (s == null || s.texture == null)
		{
			return;
		}
		Rect tr;
		try
		{
			tr = s.textureRect;   // throws for a tightly packed sprite
		}
		catch (Exception)
		{
			tr = s.rect;
		}
		Texture2D tex = s.texture;
		Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
		// keep the glyph's shape inside the square
		float asp = tr.width / Math.Max(1f, tr.height);
		if (asp > 1f)
		{
			float hh = r.height / asp;
			r = new Rect(r.x, r.center.y - hh / 2f, r.width, hh);
		}
		else if (asp < 1f)
		{
			float ww = r.width * asp;
			r = new Rect(r.center.x - ww / 2f, r.y, ww, r.height);
		}
		GUI.color = col;
		GUI.DrawTextureWithTexCoords(r, tex, uv);
	}

	private static void DrawArrow(GuiArt art, Vector2 c, int dir, int facing, Color col)
	{
		// dir counter-clockwise from right; flipped when the ghost runs left
		float ang = dir * 45f;
		if (facing < 0)
		{
			ang = 180f - ang;
		}
		GuiArt.Img img = art.Image("dotarrow");
		Matrix4x4 m0 = GUI.matrix;
		GUIUtility.RotateAroundPivot(-ang, c * m0.m00);
		GUI.color = col;
		if (img != null)
		{
			GUI.DrawTexture(new Rect(c.x - 20f, c.y - 20f, 40f, 40f), img.Tex);
		}
		else
		{
			GUI.DrawTexture(new Rect(c.x - 16f, c.y - 3f, 32f, 6f), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(c.x + 6f, c.y - 9f, 9f, 18f), Texture2D.whiteTexture);
		}
		GUI.matrix = m0;
	}
}
