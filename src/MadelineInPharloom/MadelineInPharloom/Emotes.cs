using System;
using System.Collections.Generic;
using System.IO;
using MadelineInPharloom.Sheets;
using UnityEngine;
using IS = UnityEngine.InputSystem;

namespace MadelineInPharloom;

/// <summary>
/// Emote bindings (user 2026-10-19): each of the eight emotes has a keyboard key (1-8 at first) and a direction of the
/// right stick (a flick, not a press). Saved in emotes.cfg; changed in the Emotes menu (Settings, Controls).
/// </summary>
public static class EmoteBinds
{
	public static readonly string[] Names = new string[8] { "Sit", "Sleep", "Spin", "Look Up", "Stretch", "Hug", "Faint", "Shiver" };

	// the Celeste animations each may show: sleeping and fainting settle into a second one
	public static readonly string[][] Anims = new string[8][]
	{
		new string[1] { "sitDown" },
		new string[2] { "sleep", "asleep" },
		new string[1] { "spin" },
		new string[1] { "lookUp" },
		new string[1] { "idleA" },
		new string[1] { "hug" },
		new string[2] { "faint", "fainted" },
		new string[1] { "shaking" }
	};

	// how long each lasts, in seconds
	public static readonly float[] Seconds = new float[8] { 3f, 4f, 1.2f, 2f, 3f, 2.5f, 3f, 1.5f };

	/// <summary>Right stick directions, counter-clockwise from right (an angle of n x 45 degrees).</summary>
	public static readonly string[] DirNames = new string[8] { "Right", "Up-Right", "Up", "Up-Left", "Left", "Down-Left", "Down", "Down-Right" };

	private static readonly IS.Key[] DefaultKeys = new IS.Key[8] { IS.Key.Digit1, IS.Key.Digit2, IS.Key.Digit3, IS.Key.Digit4, IS.Key.Digit5, IS.Key.Digit6, IS.Key.Digit7, IS.Key.Digit8 };

	// sit down, sleep down-left, spin up-right, look up up, stretch down-right, hug right, faint left, shiver up-left
	private static readonly int[] DefaultDirs = new int[8] { 6, 5, 1, 2, 7, 0, 4, 3 };

	public static readonly IS.Key[] Keys = (IS.Key[])DefaultKeys.Clone();

	public static readonly int[] Dirs = (int[])DefaultDirs.Clone();

	private static bool loaded;

	private static bool stickOut;

	private static string FilePath => Path.Combine(AssistMode.DataDir ?? "", "emotes.cfg");

	public static void Load()
	{
		if (loaded)
		{
			return;
		}
		loaded = true;
		try
		{
			if (!File.Exists(FilePath))
			{
				return;
			}
			foreach (string line in File.ReadAllLines(FilePath))
			{
				string[] p = line.Split('=');
				if (p.Length != 2 || !int.TryParse(p[0], out int i) || i < 0 || i >= Names.Length)
				{
					continue;
				}
				string[] v = p[1].Split(',');
				if (v.Length > 0 && Enum.TryParse(v[0], out IS.Key k))
				{
					Keys[i] = k;
				}
				if (v.Length > 1 && int.TryParse(v[1], out int d) && d >= -1 && d < 8)
				{
					Dirs[i] = d;
				}
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Emotes: " + e.Message);
		}
	}

	public static void Save()
	{
		try
		{
			List<string> lines = new List<string>();
			for (int i = 0; i < Names.Length; i++)
			{
				lines.Add(i + "=" + Keys[i] + "," + Dirs[i]);
			}
			Directory.CreateDirectory(AssistMode.DataDir);
			File.WriteAllLines(FilePath, lines.ToArray());
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Emotes: " + e.Message);
		}
	}

	public static void Reset()
	{
		Array.Copy(DefaultKeys, Keys, Keys.Length);
		Array.Copy(DefaultDirs, Dirs, Dirs.Length);
		Save();
	}

	/// <summary>Gives emote i this key; the emote that had it gets i's old one.</summary>
	public static void SetKey(int i, IS.Key k)
	{
		int other = Array.IndexOf(Keys, k);
		if (other >= 0 && other != i)
		{
			Keys[other] = Keys[i];
		}
		Keys[i] = k;
		Save();
	}

	/// <summary>Gives emote i this stick direction; the emote that had it gets i's old one.</summary>
	public static void SetDir(int i, int d)
	{
		int other = Array.IndexOf(Dirs, d);
		if (other >= 0 && other != i)
		{
			Dirs[other] = Dirs[i];
		}
		Dirs[i] = d;
		Save();
	}

	public static string KeyName(IS.Key k)
	{
		try
		{
			IS.Keyboard kb = IS.Keyboard.current;
			if (kb != null)
			{
				string n = kb[k].displayName;
				if (!string.IsNullOrEmpty(n))
				{
					return n.ToUpperInvariant();
				}
			}
		}
		catch (Exception)
		{
		}
		return k.ToString().Replace("Digit", "");
	}

	/// <summary>The direction the right stick has just been pushed to (0-7), or -1. One flick is one emote: the stick has
	/// to come back to the middle before the next.</summary>
	public static int StickFlick()
	{
		Vector2 v = Vector2.zero;
		try
		{
			InControl.InputDevice d = InControl.InputManager.ActiveDevice;
			if (d != null && d != InControl.InputDevice.Null)
			{
				v = d.RightStick.Vector;
			}
		}
		catch (Exception)
		{
			return -1;
		}
		float m = v.magnitude;
		if (m < 0.4f)
		{
			stickOut = false;
			return -1;
		}
		if (stickOut || m < 0.75f)
		{
			return -1;
		}
		stickOut = true;
		float a = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
		return ((int)Mathf.Round(a / 45f) % 8 + 8) % 8;
	}

	/// <summary>The emote asked for this frame in the game (its key or its stick direction), or -1.</summary>
	public static int Pressed()
	{
		Load();
		if (AssistMode.IsOpen || BossCard.RecordsOpen || StartingAbilities.IsOpen || EmoteMenu.IsOpen)
		{
			return -1;
		}
		try
		{
			IS.Keyboard kb = IS.Keyboard.current;
			if (kb != null)
			{
				for (int i = 0; i < Keys.Length; i++)
				{
					if (Keys[i] != IS.Key.None && kb[Keys[i]].wasPressedThisFrame)
					{
						return i;
					}
				}
			}
		}
		catch (Exception)
		{
		}
		int dir = StickFlick();
		return dir >= 0 ? Array.IndexOf(Dirs, dir) : -1;
	}
}

/// <summary>
/// The Emotes menu (user 2026-10-19): every emote with Madeline acting it out, and its key or stick direction, only
/// for whichever was used last (keyboard or controller). Choose one and press a key or push the stick to change it.
/// Opened from Settings, Controls.
/// </summary>
public class EmoteMenu : MonoBehaviour
{
	public static bool IsOpen;

	public static float ClosedAt = -10f;

	private static EmoteMenu instance;

	private int sel;

	private float t;

	private bool closing;

	private bool padHints;

	private bool listening;

	private float listenAt;

	private float flash;

	private readonly Rect[] rowRects = new Rect[10];

	private int Rows => EmoteBinds.Names.Length + 1;   // the emotes, then Reset

	private void Awake()
	{
		instance = this;
	}

	public static void Open()
	{
		if (instance == null || IsOpen)
		{
			return;
		}
		EmoteBinds.Load();
		IsOpen = true;
		instance.closing = false;
		instance.listening = false;
		instance.t = 0f;
		instance.sel = 0;
		instance.padHints = MenuMouse.PadActive;
		Sfx.Play(Sounds.MenuPause.Id);
	}

	private void Update()
	{
		if (!IsOpen)
		{
			return;
		}
		try
		{
			Drive();
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Emote menu: " + e.Message);
			IsOpen = false;
		}
	}

	private void Drive()
	{
		float dt = Time.unscaledDeltaTime;
		flash += dt;
		if (closing)
		{
			t -= dt / 0.2f;
			if (t <= 0f)
			{
				IsOpen = false;
				ClosedAt = Time.unscaledTime;
			}
			return;
		}
		t = Math.Min(1f, t + dt / 0.25f);
		padHints = MenuMouse.PadHints(padHints);
		if (t < 0.6f)
		{
			return;
		}
		IS.Keyboard kb = IS.Keyboard.current;
		HeroActions a = ManagerSingleton<InputHandler>.Instance?.inputActions;
		if (listening)
		{
			Listen(kb);
			return;
		}
		bool up = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (a?.Up.WasPressed ?? false);
		bool down = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (a?.Down.WasPressed ?? false);
		bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (a?.Jump.WasPressed ?? false);
		bool back = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) || (a?.MenuCancel.WasPressed ?? false);
		IS.Mouse m = IS.Mouse.current;
		if (m != null)
		{
			Vector2 p = MenuMouse.Gui();
			bool moved = m.delta.ReadValue().sqrMagnitude > 0.5f;
			for (int i = 0; i < Rows; i++)
			{
				if (rowRects[i].Contains(p))
				{
					if (moved && sel != i)
					{
						sel = i;
						Sfx.Play(Sounds.MenuRolloverDown.Id);
					}
					if (m.leftButton.wasPressedThisFrame)
					{
						sel = i;
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
		if (confirm)
		{
			if (sel == Rows - 1)
			{
				EmoteBinds.Reset();
				Sfx.Play(Sounds.MenuToggleOff.Id);
			}
			else
			{
				listening = true;
				listenAt = Time.unscaledTime;
				Sfx.Play(Sounds.MenuButtonSelect.Id);
			}
		}
		else if (back)
		{
			Sfx.Play(Sounds.MenuUnpause.Id);
			closing = true;
		}
	}

	/// <summary>Waiting for the new key (keyboard) or stick direction (controller) for the chosen emote.</summary>
	private void Listen(IS.Keyboard kb)
	{
		if (Time.unscaledTime - listenAt < 0.2f)
		{
			EmoteBinds.StickFlick();   // let the press that started listening pass
			return;
		}
		if (kb != null && kb.escapeKey.wasPressedThisFrame)
		{
			listening = false;
			Sfx.Play(Sounds.MenuUnpause.Id);
			return;
		}
		if (padHints)
		{
			int d = EmoteBinds.StickFlick();
			if (d >= 0)
			{
				EmoteBinds.SetDir(sel, d);
				listening = false;
				Sfx.Play(Sounds.MenuToggleOn.Id);
			}
			else if (ManagerSingleton<InputHandler>.Instance?.inputActions?.MenuCancel.WasPressed ?? false)
			{
				listening = false;
				Sfx.Play(Sounds.MenuUnpause.Id);
			}
			return;
		}
		if (kb == null)
		{
			return;
		}
		foreach (IS.Controls.KeyControl k in kb.allKeys)
		{
			if (k != null && k.wasPressedThisFrame && k.keyCode != IS.Key.Escape)
			{
				EmoteBinds.SetKey(sel, k.keyCode);
				listening = false;
				Sfx.Play(Sounds.MenuToggleOn.Id);
				return;
			}
		}
	}

	private static Color Hex(string hex)
	{
		ColorUtility.TryParseHtmlString("#" + hex, out Color c);
		return c;
	}

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
		Color acc = Hex("4FC1C9");
		GUI.color = new Color(0f, 0f, 0f, 0.85f * al);
		GUI.DrawTexture(new Rect(0f, 0f, w, 1080f), Texture2D.whiteTexture);
		Vector2 mouse = MenuMouse.Gui();
		// the card, on the left; Madeline acting out the chosen emote on the right
		float slide = (1f - e) * 40f;
		Rect card = new Rect(120f - slide, 86f, Math.Min(900f, w * 0.52f), 900f);
		GUI.color = new Color(0.05f, 0.045f, 0.09f, 0.94f * al);
		GUI.DrawTexture(card, Texture2D.whiteTexture);
		GUI.color = new Color(acc.r, acc.g, acc.b, al);
		GUI.DrawTexture(new Rect(card.x, card.y, card.width, 6f), Texture2D.whiteTexture);
		GUI.color = new Color(acc.r, acc.g, acc.b, 0.18f * al);
		GUI.DrawTexture(new Rect(card.x, card.y + 6f, card.width, 90f), CelesteMenus.MenuFx.FadeOut);
		art.DrawOutlined("Emotes", card.x + 40f, card.y + 30f, 0.62f, new Color(1f, 1f, 1f, al), ink, 3f);
		string sub = padHints ? "right stick" : "keyboard";
		art.DrawOutlined(sub, card.xMax - 40f - art.Measure(sub) * 0.42f, card.y + 40f, 0.42f, new Color(0.7f, 0.7f, 0.75f, al), ink, 2f);
		float rowH = art.LineHeight * 0.55f + 26f;
		float rowL = card.x + 40f;
		float rowR = card.xMax - 40f;
		float y = card.y + 120f;
		Color hiCol = Color.Lerp(Color.white, acc, 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f));
		for (int i = 0; i < Rows; i++)
		{
			bool isSel = sel == i;
			bool reset = i == Rows - 1;
			if (reset)
			{
				y += 20f;
			}
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
			Color lab = isSel ? hiCol : Color.white;
			lab.a = al;
			float ty = y + (rr.height - art.LineHeight * 0.55f) / 2f;
			art.DrawOutlined(reset ? "Reset to defaults" : EmoteBinds.Names[i], rowL + 10f, ty, 0.55f, lab, ink, 2f);
			if (!reset)
			{
				// the binding as a chip: a key, or a stick direction
				string v = (isSel && listening) ? (padHints ? "push the stick..." : "press a key...") : (padHints ? (EmoteBinds.Dirs[i] >= 0 ? EmoteBinds.DirNames[EmoteBinds.Dirs[i]] : "-") : EmoteBinds.KeyName(EmoteBinds.Keys[i]));
				float vs = 0.48f;
				float vw = Math.Max(120f, art.Measure(v) * vs + 36f);
				Rect chip = new Rect(rowR - vw, y + (rr.height - 40f) / 2f, vw, 40f);
				bool live = isSel && listening;
				GUI.color = live ? new Color(acc.r, acc.g, acc.b, (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f)) * al) : new Color(1f, 1f, 1f, 0.1f * al);
				GUI.DrawTexture(chip, Texture2D.whiteTexture);
				art.DrawOutlined(v, chip.center.x - art.Measure(v) * vs / 2f, chip.y + (chip.height - art.LineHeight * vs) / 2f, vs, new Color(1f, 1f, 1f, al), ink, 2f);
				if (padHints && !live && EmoteBinds.Dirs[i] >= 0)
				{
					// a little stick dial: where the direction points
					Vector2 c = new Vector2(chip.x - 34f, chip.center.y);
					GUI.color = new Color(1f, 1f, 1f, 0.15f * al);
					GUI.DrawTexture(new Rect(c.x - 14f, c.y - 14f, 28f, 28f), CelesteMenus.MenuFx.Glow);
					float ang = EmoteBinds.Dirs[i] * 45f * Mathf.Deg2Rad;
					GUI.color = new Color(acc.r, acc.g, acc.b, al);
					GUI.DrawTexture(new Rect(c.x + Mathf.Cos(ang) * 11f - 4f, c.y - Mathf.Sin(ang) * 11f - 4f, 8f, 8f), Texture2D.whiteTexture);
				}
			}
			y += rowH;
		}
		// Madeline doing it, in the preview's style
		if (sel < EmoteBinds.Names.Length)
		{
			AssistMode.DrawEmotePreview(EmoteBinds.Anims[sel][0], al, card.xMax + 40f, w - 60f);
			string n = EmoteBinds.Names[sel];
			art.DrawOutlined(n, (card.xMax + w) / 2f - art.Measure(n) * 0.7f / 2f, 780f, 0.7f, new Color(1f, 1f, 1f, al), ink, 3f);
		}
		string hint = listening ? (padHints ? "Push the right stick a way   Circle cancel" : "Press any key   Esc cancel") : (padHints ? "Up/Down choose   Cross change   Circle back" : "Up/Down choose   Enter change   Esc back");
		art.DrawOutlined(hint, w / 2f - art.Measure(hint) * 0.42f / 2f, 1030f, 0.42f, new Color(1f, 1f, 1f, al * 0.75f), ink, 2f);
		GUI.matrix = old;
		GUI.color = Color.white;
	}
}
