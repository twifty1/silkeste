using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Starting abilities (user 2026-10-19): a file can start with Cling Grip, Faydown Cloak, Drifter's Cloak, Clawline
/// and Silk Soar. Five Off/On rows per save, picked in the new-file menu ("Pack for the climb") that opens when Begin
/// is pressed on an empty file, before the opening cutscene, together with the game mode and difficulty. All off is
/// the intended experience; Sandbox has everything already.
/// The game's own flags are switched: an ability Silkeste gave is taken back when its row goes off, but never one the
/// player found for real (the game setting the flag itself marks it earned).
/// </summary>
public class StartingAbilities : MonoBehaviour
{
	private struct Ability
	{
		public AssistRow Row;

		public string Flag;

		public string Short;
	}

	private static readonly Ability[] All = new Ability[5]
	{
		new Ability { Row = AssistOptions.StartClimb, Flag = "hasWalljump", Short = "Grab, climb and wall jump" },
		new Ability { Row = AssistOptions.StartDoubleDash, Flag = "hasDoubleJump", Short = "A double jump" },
		new Ability { Row = AssistOptions.StartGlide, Flag = "hasBrolly", Short = "The jellyfish glide" },
		new Ability { Row = AssistOptions.StartClawline, Flag = "hasHarpoonDash", Short = "Grapple with L2 or Q" },
		new Ability { Row = AssistOptions.StartSilkSoar, Flag = "hasSuperJump", Short = "Down + hold L2 or Q, costs 1 silk" }
	};

	public static bool IsOpen;

	public static float ClosedAt = -10f;

	/// <summary>When Begin was last pressed in the menu (Settings stays shut for a moment after).</summary>
	public static float BeganAt = -10f;

	private static readonly Dictionary<string, AccessTools.FieldRef<PlayerData, bool>> fields = new Dictionary<string, AccessTools.FieldRef<PlayerData, bool>>();

	// per save: the flags Silkeste switched on
	private static Dictionary<int, HashSet<string>> given;

	private static StartingAbilities instance;

	private Action onBegin;

	private int slot = -1;

	private static bool writing;

	private int sel;

	private float t;

	private bool closing;

	private bool padHints;

	private float flash;

	private readonly Rect[] rowRects = new Rect[8];

	private static string GivenFile => Path.Combine(AssistMode.DataDir ?? "", "start_abilities.cfg");

	private void Awake()
	{
		instance = this;
		try
		{
			new Harmony("melty.madelineinpharloom.start").Patch(AccessTools.Method(typeof(PlayerData), "SetBool", new Type[2] { typeof(string), typeof(bool) }), new HarmonyMethod(typeof(StartingAbilities), "SetBoolPrefix"));
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Starting abilities: " + e.Message);
		}
	}

	/// <summary>The game set an ability flag itself (the real item): it's earned, so turning the row off keeps it.</summary>
	private static void SetBoolPrefix(string boolName, bool value)
	{
		if (writing || !value || BossRematch.Active || GameManager.SilentInstance == null)
		{
			return;
		}
		foreach (Ability a in All)
		{
			if (a.Flag == boolName)
			{
				LoadState();
				if (given.TryGetValue(GameManager.SilentInstance.profileID, out HashSet<string> set) && set.Remove(boolName))
				{
					SaveState();
					Plugin.Log.LogInfo("Starting abilities: " + boolName + " found for real, kept");
				}
			}
		}
	}

	private static AccessTools.FieldRef<PlayerData, bool> Field(string name)
	{
		if (!fields.TryGetValue(name, out var f))
		{
			f = fields[name] = AccessTools.FieldRefAccess<PlayerData, bool>(name);
		}
		return f;
	}

	private static void LoadState()
	{
		if (given != null)
		{
			return;
		}
		given = new Dictionary<int, HashSet<string>>();
		try
		{
			if (!File.Exists(GivenFile))
			{
				return;
			}
			foreach (string line in File.ReadAllLines(GivenFile))
			{
				string[] p = line.Split('|');
				if (p.Length < 2 || !int.TryParse(p[0], out int slot))
				{
					continue;
				}
				given[slot] = new HashSet<string>(p[1].Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries));
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Starting abilities: " + e.Message);
		}
	}

	private static void SaveState()
	{
		try
		{
			List<string> lines = new List<string>();
			foreach (KeyValuePair<int, HashSet<string>> g in given)
			{
				lines.Add(g.Key + "|" + string.Join(",", new List<string>(g.Value).ToArray()));
			}
			Directory.CreateDirectory(AssistMode.DataDir);
			File.WriteAllLines(GivenFile, lines.ToArray());
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Starting abilities: " + e.Message);
		}
	}

	/// <summary>A deleted file forgets what it was given.</summary>
	public static void Forget(int slot)
	{
		LoadState();
		if (given.Remove(slot))
		{
			SaveState();
		}
	}

	/// <summary>True when Silkeste gave this flag to the save in play (its unlock postcard is skipped).</summary>
	public static bool WasGiven(string flag)
	{
		GameManager gm = GameManager.SilentInstance;
		if (gm == null)
		{
			return false;
		}
		LoadState();
		return given.TryGetValue(gm.profileID, out HashSet<string> set) && set.Contains(flag);
	}

	/// <summary>The prompt on the file select: Begin starts the file (onBegin), Back returns to its options.</summary>
	public static void OpenMenu(Action begin, int slot = -1)
	{
		if (instance == null || IsOpen)
		{
			return;
		}
		instance.onBegin = begin;
		instance.slot = slot;
		IsOpen = true;
		instance.closing = false;
		instance.begin = false;
		instance.t = 0f;
		instance.sel = BeginRow;   // on Begin: one press keeps the intended experience
		Sfx.Play(Sounds.MenuPause.Id);
	}

	/// <summary>Makes the save's flags match its rows: on rows give the ability, off rows take back only what Silkeste gave.</summary>
	private static void Apply(GameManager gm, PlayerData pd)
	{
		LoadState();
		int slot = gm.profileID;
		if (!given.TryGetValue(slot, out HashSet<string> set))
		{
			set = new HashSet<string>();
		}
		bool changed = false;
		bool any = false;
		bool sandbox = AssistMode.ModeId == "sandbox";
		writing = true;
		try
		{
			foreach (Ability a in All)
			{
				var f = Field(a.Flag);
				bool want = AssistMode.IsOn(a.Row);
				any |= want;
				if (want && !f(pd))
				{
					f(pd) = true;
					set.Add(a.Flag);
					changed = true;
					Plugin.Log.LogInfo("Starting abilities: gave " + a.Row.Label);
				}
				else if (!want && !sandbox && f(pd) && set.Contains(a.Flag))
				{
					f(pd) = false;
					set.Remove(a.Flag);
					changed = true;
					Plugin.Log.LogInfo("Starting abilities: took back " + a.Row.Label);
				}
			}
		}
		finally
		{
			writing = false;
		}
		if (changed)
		{
			given[slot] = set;
			SaveState();
		}
		if (any && !AssistMode.SaveMarked)
		{
			AssistMode.MarkSaveNow();   // a head start makes it an assist save (user 2026-10-19)
		}
	}

	private void Update()
	{
		if (IsOpen)
		{
			try
			{
				Drive();
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning("Starting abilities: " + e.Message);
				Finish();
			}
			return;
		}
		GameManager gm = GameManager.SilentInstance;
		PlayerData pd = PlayerData.instance;
		if (gm == null || HeroController.UnsafeInstance == null || pd == null || gm.IsMenuScene() || BossRematch.Active || AssistMode.ProfileSlot != gm.profileID)
		{
			return;
		}
		try
		{
			Apply(gm, pd);
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Starting abilities: " + e.Message);
		}
	}

	// ---- the new-file menu ----------------------------------------------------------------------------------------
	// user 2026-10-19: the game mode and difficulty live here too (the file's Game Mode button is gone): mode, difficulty,
	// the five abilities, then Begin
	private const int ModeRow = 0;

	private const int DiffRow = -1;   // difficulties are gone (user 2026-10-20)

	private const int FirstAbility = 1;

	private const int BeginRow = FirstAbility + 5;

	private const int Rows = BeginRow + 1;

	private bool begin;

	private readonly Rect[][] arrows = new Rect[2][] { new Rect[2], new Rect[2] };

	private static bool Sandbox => Presets.Current == "sandbox";

	private void Change(int row, int dir)
	{
		if (row == ModeRow)
		{
			int i = Array.FindIndex(Presets.All, (Presets.Preset p) => p.Id == Presets.Current);
			Presets.Apply(Presets.All[(i + dir + Presets.All.Length) % Presets.All.Length].Id);
			Sfx.Play(dir < 0 ? Sounds.MenuRolloverUp.Id : Sounds.MenuRolloverDown.Id);
		}
		else if (row == DiffRow)
		{
			AssistMode.Cycle(AssistOptions.Difficulty, dir);
			Sfx.Play(dir < 0 ? Sounds.MenuRolloverUp.Id : Sounds.MenuRolloverDown.Id);
		}
		else if (row >= FirstAbility && row < BeginRow)
		{
			if (Sandbox)
			{
				Sfx.Play(Sounds.MenuToggleOff.Id);   // Sandbox starts with everything anyway
				return;
			}
			AssistRow r = All[row - FirstAbility].Row;
			bool on = !AssistMode.IsOn(r);
			AssistMode.SetOn(r, on);
			Sfx.Play(on ? Sounds.MenuToggleOn.Id : Sounds.MenuToggleOff.Id);
		}
		flash = 0f;
	}

	private void Drive()
	{
		float dt = Time.unscaledDeltaTime;
		flash += dt;
		if (closing)
		{
			t -= dt / 0.25f;
			if (t <= 0f)
			{
				Finish();
			}
			return;
		}
		t = Math.Min(1f, t + dt / 0.3f);
		padHints = MenuMouse.PadHints(padHints);
		// Silksong's own menu underneath must not take these presses (user 2026-10-20: Customization opened behind)
		if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null)
		{
			UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
		}
		if (t < 0.6f)
		{
			return;
		}
		Keyboard kb = Keyboard.current;
		HeroActions a = ManagerSingleton<InputHandler>.Instance?.inputActions;
		bool up = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (a?.Up.WasPressed ?? false);
		bool down = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (a?.Down.WasPressed ?? false);
		bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (a?.Left.WasPressed ?? false);
		bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (a?.Right.WasPressed ?? false);
		bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (a?.Jump.WasPressed ?? false);
		bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (a?.MenuCancel.WasPressed ?? false);
		Mouse m = Mouse.current;
		if (m != null)
		{
			Vector2 p = MenuMouse.Gui();
			bool moved = m.delta.ReadValue().sqrMagnitude > 0.5f;
			bool click = m.leftButton.wasPressedThisFrame;
			for (int i = 0; i < Rows; i++)
			{
				if (!rowRects[i].Contains(p))
				{
					continue;
				}
				if (moved && sel != i)
				{
					sel = i;
					flash = 0f;
					Sfx.Play(Sounds.MenuRolloverDown.Id);
				}
				if (click)
				{
					sel = i;
					if (i == ModeRow && arrows[i][0].Contains(p))
					{
						left = true;
					}
					else
					{
						confirm = true;
					}
				}
			}
		}
		if (up || down)
		{
			sel = (sel + (up ? Rows - 1 : 1)) % Rows;
			flash = 0f;
			Sfx.Play(up ? Sounds.MenuRolloverUp.Id : Sounds.MenuRolloverDown.Id);
		}
		if (sel < BeginRow && (left || right || confirm))
		{
			Change(sel, left ? -1 : 1);
		}
		else if (sel == BeginRow && confirm)
		{
			Sfx.Play(Sounds.MenuButtonSelect.Id);
			begin = true;
			closing = true;
		}
		else if (back)
		{
			Sfx.Play(Sounds.MenuUnpause.Id);
			closing = true;
		}
	}

	private void Finish()
	{
		IsOpen = false;
		closing = false;
		ClosedAt = Time.unscaledTime;
		Plugin.Log.LogInfo("New file menu: " + (begin ? "begin, " : "back, ") + Presets.Current + ", " + AssistMode.ValueOf(AssistOptions.Difficulty) + ", " + CountOn() + " abilities on");
		if (begin)
		{
			BeganAt = Time.unscaledTime;
		}
		Action go = begin ? onBegin : null;
		onBegin = null;
		begin = false;
		go?.Invoke();
	}

	private static Color Hex(string hex)
	{
		ColorUtility.TryParseHtmlString("#" + hex, out Color c);
		return c;
	}

	private static int CountOn()
	{
		int n = 0;
		foreach (Ability a in All)
		{
			if (AssistMode.IsOn(a.Row))
			{
				n++;
			}
		}
		return n;
	}

	// drawn like the Settings pages (user 2026-10-19: "more like the settings"): a side title, a dark card with the
	// accent bar, selectors and On/Off switches, a Begin button and the description strip at the card's foot
	private void OnGUI()
	{
		GuiArt art = AssistMode.Art;
		if (!IsOpen || art == null || Event.current.type != EventType.Repaint)
		{
			return;
		}
		float k = Screen.height / 1080f;
		float w = Screen.width / k;
		float e = Mathf.Clamp01(t);
		e = 1f - (1f - e) * (1f - e) * (1f - e);
		float al = e;
		Matrix4x4 old = GUI.matrix;
		GUI.depth = -20;
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(k, k, 1f));
		Color ink = new Color(0f, 0f, 0f, al);
		Color acc = Hex("5BA4E6");   // the Game Modes colour
		Color gold = new Color(1f, 0.85f, 0.35f, al);
		Color warn = new Color(1f, 0.72f, 0.45f, al);
		GUI.color = new Color(0f, 0f, 0f, 0.9f * al);
		GUI.DrawTexture(new Rect(0f, 0f, w, 1080f), Texture2D.whiteTexture);
		float slide = (1f - e) * 40f;
		Vector2 mouse = MenuMouse.Gui();
		int count = CountOn();
		bool sandbox = Sandbox;
		// the side: what this page is
		float side0 = 70f - slide;
		float sideW = 330f;
		float top = 96f;
		GuiArt.Img icon = art.Image("assistmode");
		if (icon != null)
		{
			GUI.color = new Color(1f, 1f, 1f, al);
			GUI.DrawTexture(new Rect(side0, top - 8f, 64f, 64f), icon.Tex);
		}
		art.DrawOutlined("New File", side0 + 76f, top - 4f, 0.95f, new Color(1f, 1f, 1f, al), ink, 3f);
		float ty = top + art.LineHeight * 0.85f + 40f;
		GUI.color = new Color(acc.r, acc.g, acc.b, al);
		GUI.DrawTexture(new Rect(side0, ty - 14f, sideW, 4f), Texture2D.whiteTexture);
		float fy = ty + 6f;
		if (slot >= 0)
		{
			art.DrawOutlined("File " + slot, side0 + 6f, fy, 0.6f, new Color(1f, 1f, 1f, al), ink, 2f);
			fy += art.LineHeight * 0.6f + 10f;
		}
		foreach (string note in new string[4] { "How this file plays, and", "what it starts with.", "All abilities off is the", "intended experience." })
		{
			art.DrawOutlined(note, side0 + 6f, fy, 0.4f, new Color(0.78f, 0.78f, 0.84f, al), ink, 2f);
			fy += art.LineHeight * 0.4f + 4f;
		}
		// the card
		float p0 = side0 + sideW + 50f;
		float p1 = Math.Min(w - 90f, p0 + 1180f);
		Rect card = new Rect(p0, top - 10f, p1 - p0, 900f);
		GUI.color = new Color(0.05f, 0.045f, 0.09f, 0.92f * al);
		GUI.DrawTexture(card, Texture2D.whiteTexture);
		GUI.color = new Color(acc.r, acc.g, acc.b, al);
		GUI.DrawTexture(new Rect(card.x, card.y, card.width, 6f), Texture2D.whiteTexture);
		GUI.color = new Color(acc.r, acc.g, acc.b, 0.18f * al);
		GUI.DrawTexture(new Rect(card.x, card.y + 6f, card.width, 90f), CelesteMenus.MenuFx.FadeOut);
		art.DrawOutlined("Pack for the climb", card.x + 40f, card.y + 30f, 0.62f, new Color(1f, 1f, 1f, al), ink, 3f);
		string sub = sandbox ? "Sandbox: everything unlocked" : (count == 0 ? "the intended experience" : ("assist save: " + count + " of " + All.Length + " on"));
		art.DrawOutlined(sub, card.xMax - 40f - art.Measure(sub) * 0.42f, card.y + 40f, 0.42f, (count == 0 && !sandbox) ? gold : warn, ink, 2f);
		float rowH = art.LineHeight * 0.55f + 22f;
		float rowL = card.x + 40f;
		float rowR = card.xMax - 40f;
		float y = card.y + 116f;
		Color hiCol = Color.Lerp(Color.white, acc, 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f));
		for (int i = 0; i < BeginRow; i++)
		{
			if (i == FirstAbility)
			{
				// the line between how the file plays and what it starts with
				float ly = y + 16f;
				GUI.color = new Color(acc.r, acc.g, acc.b, 0.7f * al);
				GUI.DrawTexture(new Rect(rowL - 16f, ly, rowR - rowL + 32f, 2f), Texture2D.whiteTexture);
				string dn = "starting abilities";
				float dw = art.Measure(dn) * 0.4f;
				GUI.color = new Color(0.07f, 0.08f, 0.14f, al);
				GUI.DrawTexture(new Rect(rowR - dw - 14f, ly - 16f, dw + 14f, 34f), Texture2D.whiteTexture);
				art.DrawOutlined(dn, rowR - dw, ly - art.LineHeight * 0.4f / 2f, 0.4f, new Color(0.8f, 0.8f, 0.88f, al), ink, 2f);
				y += 40f;
			}
			bool isSel = sel == i;
			bool dim = sandbox && i >= FirstAbility;
			Rect rr = new Rect(rowL - 16f, y, rowR - rowL + 32f, rowH - 6f);
			rowRects[i] = rr;
			if (isSel)
			{
				GUI.color = new Color(acc.r, acc.g, acc.b, 0.22f * al);
				GUI.DrawTexture(rr, CelesteMenus.MenuFx.FadeOut);
				GUI.color = new Color(acc.r, acc.g, acc.b, al);
				GUI.DrawTexture(new Rect(rr.x, rr.y, 5f, rr.height), Texture2D.whiteTexture);
			}
			else if (!padHints && rr.Contains(mouse))
			{
				GUI.color = new Color(1f, 1f, 1f, 0.05f * al);
				GUI.DrawTexture(rr, Texture2D.whiteTexture);
			}
			float ts = 0.55f;
			Color lab = isSel ? hiCol : (dim ? new Color(0.6f, 0.6f, 0.65f) : Color.white);
			lab.a = al;
			string label = i == ModeRow ? "Game Mode" : (i == DiffRow ? "Difficulty" : All[i - FirstAbility].Row.Label);
			float textY = y + (rr.height - art.LineHeight * ts) / 2f;
			art.DrawOutlined(label, rowL + 10f, textY, ts, lab, ink, 2f);
			if (i == ModeRow)
			{
				// a selector, as in Settings: [<] value [>] with pips for where it is in the list
				string[] vals = i == ModeRow ? Array.ConvertAll(Presets.All, (Presets.Preset p) => p.Name) : AssistOptions.Difficulty.Values.Split(',');
				string val = i == ModeRow ? Presets.Find(Presets.Current).Name : AssistMode.ValueOf(AssistOptions.Difficulty);
				int idx = Math.Max(0, Array.IndexOf(vals, val));
				float vs = 0.48f;
				float boxW = 40f;
				float valW = Mathf.Max(220f, art.Measure(val) * vs + 36f);
				float cy = y + (rr.height - 40f) / 2f;
				Rect rBox = new Rect(rowR - boxW, cy, boxW, 40f);
				Rect vBox = new Rect(rBox.x - 6f - valW, cy, valW, 40f);
				Rect lBox = new Rect(vBox.x - 6f - boxW, cy, boxW, 40f);
				arrows[i][0] = lBox;
				arrows[i][1] = rBox;
				for (int s = 0; s < 2; s++)
				{
					Rect bx = s == 0 ? lBox : rBox;
					bool hot = isSel && !padHints && bx.Contains(mouse);
					GUI.color = hot ? new Color(acc.r, acc.g, acc.b, al) : new Color(1f, 1f, 1f, (isSel ? 0.16f : 0.07f) * al);
					GUI.DrawTexture(bx, Texture2D.whiteTexture);
					string ar = s == 0 ? "<" : ">";
					art.DrawOutlined(ar, bx.center.x - art.Measure(ar) * vs / 2f, bx.y + (bx.height - art.LineHeight * vs) / 2f, vs, new Color(1f, 1f, 1f, (isSel ? 1f : 0.45f) * al), ink, 2f);
				}
				bool changed = i == ModeRow ? Presets.Current != "standard" : val != AssistOptions.Difficulty.Default;
				GUI.color = changed ? new Color(acc.r, acc.g, acc.b, 0.85f * al) : new Color(1f, 1f, 1f, 0.08f * al);
				GUI.DrawTexture(vBox, Texture2D.whiteTexture);
				art.DrawOutlined(val, vBox.center.x - art.Measure(val) * vs / 2f, vBox.y + (vBox.height - art.LineHeight * vs) / 2f - 3f, vs, new Color(1f, 1f, 1f, al), ink, 2f);
				float px = vBox.center.x - (vals.Length * 6f + (vals.Length - 1) * 4f) / 2f;
				for (int q = 0; q < vals.Length; q++)
				{
					GUI.color = new Color(1f, 1f, 1f, (q == idx ? 0.95f : 0.25f) * al);
					GUI.DrawTexture(new Rect(px + q * 10f, vBox.yMax - 7f, 6f, 3f), Texture2D.whiteTexture);
				}
			}
			else
			{
				Ability a = All[i - FirstAbility];
				bool on = sandbox || AssistMode.IsOn(a.Row);
				art.DrawOutlined(a.Short, rowL + 380f, y + (rr.height - art.LineHeight * 0.42f) / 2f, 0.42f, new Color(0.72f, 0.72f, 0.78f, al * (dim ? 0.6f : 1f)), ink, 2f);
				// the On/Off switch, as in Settings
				Rect track = new Rect(rowR - 84f, y + (rr.height - 36f) / 2f, 84f, 36f);
				GUI.color = on ? new Color(acc.r, acc.g, acc.b, al * (dim ? 0.5f : 1f)) : new Color(1f, 1f, 1f, 0.14f * al);
				GUI.DrawTexture(track, Texture2D.whiteTexture);
				GUI.color = new Color(1f, 1f, 1f, al * (dim ? 0.6f : 1f));
				GUI.DrawTexture(new Rect(on ? (track.xMax - 32f) : (track.x + 4f), track.y + 4f, 28f, 28f), Texture2D.whiteTexture);
				string st = sandbox ? "Included" : (on ? "On" : "Off");
				art.DrawOutlined(st, track.x - 16f - art.Measure(st) * 0.42f, track.y + (track.height - art.LineHeight * 0.42f) / 2f, 0.42f, on ? new Color(1f, 1f, 1f, al) : new Color(0.7f, 0.7f, 0.75f, al), ink, 2f);
			}
			y += rowH;
		}
		// Begin
		y += 28f;
		string bl = "Begin  >";
		float bs = 0.6f;
		float bw = art.Measure(bl) * bs + 80f;
		Rect btn = new Rect(card.center.x - bw / 2f, y, bw, art.LineHeight * bs + 26f);
		rowRects[BeginRow] = btn;
		bool bSel = sel == BeginRow;
		bool bHot = bSel || (!padHints && btn.Contains(mouse));
		GUI.color = new Color(0f, 0f, 0f, 0.35f * al);
		GUI.DrawTexture(new Rect(btn.x + 3f, btn.y + 4f, btn.width, btn.height), Texture2D.whiteTexture);
		GUI.color = new Color(acc.r, acc.g, acc.b, (bHot ? 1f : 0.7f) * al);
		GUI.DrawTexture(btn, Texture2D.whiteTexture);
		GUI.color = new Color(1f, 1f, 1f, 0.25f * al);
		GUI.DrawTexture(new Rect(btn.x, btn.y, btn.width, 3f), Texture2D.whiteTexture);
		if (bSel)
		{
			GUI.color = new Color(1f, 1f, 1f, (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f)) * al);
			GUI.DrawTexture(new Rect(btn.x - 4f, btn.y - 4f, btn.width + 8f, 2f), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(btn.x - 4f, btn.yMax + 2f, btn.width + 8f, 2f), Texture2D.whiteTexture);
		}
		art.DrawOutlined(bl, btn.center.x - art.Measure(bl) * bs / 2f, btn.y + (btn.height - art.LineHeight * bs) / 2f, bs, new Color(1f, 1f, 1f, al), ink, 2f);
		// the description strip at the card's foot
		Rect desc = new Rect(card.x + 30f, card.yMax - 120f, card.width - 60f, 96f);
		GUI.color = new Color(1f, 1f, 1f, 0.05f * al);
		GUI.DrawTexture(desc, Texture2D.whiteTexture);
		GUI.color = new Color(acc.r, acc.g, acc.b, al);
		GUI.DrawTexture(new Rect(desc.x, desc.y, 4f, desc.height), Texture2D.whiteTexture);
		string d;
		if (sel == ModeRow)
		{
			d = Presets.Find(Presets.Current).Blurb + (sandbox ? " Starting it keeps this file in Sandbox until you delete it." : "");
		}
		else if (sel == DiffRow)
		{
			d = AssistOptions.Difficulty.Description;
		}
		else if (sel < BeginRow)
		{
			d = sandbox ? "Sandbox starts with every ability, so there's nothing to pick here." : All[sel - FirstAbility].Row.Description;
		}
		else
		{
			d = "Start the file. " + (sandbox ? "It stays in Sandbox until you delete it, and can't earn achievements or golden berries." : (count == 0 ? "All abilities off is Silkeste as it's meant to be played: every ability is a reward for exploring Pharloom." : "A head start makes this an assist save: no achievements or golden berries."));
		}
		float dy = desc.y + 14f;
		foreach (string dl in art.Wrap(d, (desc.width - 50f) / 0.42f))
		{
			art.DrawOutlined(dl, desc.x + 24f, dy, 0.42f, new Color(0.82f, 0.82f, 0.86f, al), ink, 2f);
			dy += art.LineHeight * 0.42f;
		}
		string hint = padHints ? "Up/Down choose   Left/Right change   Cross switch   Circle back" : "Up/Down choose   Left/Right change   Enter switch   Esc back";
		art.DrawOutlined(hint, w / 2f - art.Measure(hint) * 0.42f / 2f, 1030f, 0.42f, new Color(1f, 1f, 1f, al * 0.75f), ink, 2f);
		GUI.matrix = old;
		GUI.color = Color.white;
	}
}
