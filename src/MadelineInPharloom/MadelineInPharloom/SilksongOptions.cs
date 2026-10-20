using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using UnityEngine;
using UnityEngine.UI;

namespace MadelineInPharloom;

/// <summary>
/// Unified settings, barebones (user 2026-10-17): Silksong's own Game, Audio and Video options shown as tabs in
/// Silkeste's settings screen. Each Silksong control becomes a row ("ss_" ids) appended to AssistOptions.All; its
/// value is read from the control and a change is sent back to it, so Silksong still saves and applies everything.
/// </summary>
public static class SilksongOptions
{
	public static readonly string[] Sections = new string[3] { "Game", "Audio", "Video" };

	private static readonly Dictionary<string, Selectable> controls = new Dictionary<string, Selectable>();

	private static readonly FieldInfo AllField = AccessTools.Field(typeof(AssistOptions), "All");

	private static readonly MethodInfo UpdateSetting = AccessTools.Method(typeof(MenuOptionHorizontal), "UpdateSetting");

	public static bool Is(AssistRow r)
	{
		return r.Id != null && (r.Id.StartsWith("ss_", StringComparison.Ordinal) || r.Id.StartsWith("ab_", StringComparison.Ordinal));
	}

	/// <summary>Reads Silksong's option pages again and puts their rows into AssistOptions.All.</summary>
	public static void Refresh()
	{
		try
		{
			UIManager ui = UIManager.instance;
			if (ui == null)
			{
				return;
			}
			List<AssistRow> rows = new List<AssistRow>();
			foreach (AssistRow r in AssistOptions.All)
			{
				if (!Is(r) && !IsGroup(r))   // group headers are made fresh below
				{
					rows.Add(r);
				}
			}
			controls.Clear();
			MenuScreen[] screens = new MenuScreen[3] { ui.gameOptionsMenuScreen, ui.audioMenuScreen, ui.videoMenuScreen };
			List<AssistRow> added = new List<AssistRow>();
			for (int s = 0; s < screens.Length; s++)
			{
				if (screens[s] == null)
				{
					continue;
				}
				int n = 0;
				foreach (Selectable sel in screens[s].GetComponentsInChildren<Selectable>(true))
				{
					AssistRow? row = RowFor(sel, Sections[s], n);
					if (row.HasValue)
					{
						added.Add(row.Value);
						controls[row.Value.Id] = sel;
						n++;
					}
				}
			}
			// buttons to Silksong's own binding pages, at the top of Controls (user 2026-10-17)
			added.Add(new AssistRow { Id = "ss_bind_keyboard_btn", Section = "Controls", Label = "Keyboard Bindings", Values = "Open", Default = "Open", Description = "Opens Silksong's keyboard page to change which key does what." });
			added.Add(new AssistRow { Id = "ss_bind_controller_btn", Section = "Controls", Label = "Controller Bindings", Values = "Open", Default = "Open", Description = "Opens Silksong's controller page to change the buttons." });
			// Silksong's tabs first, then Silkeste's own
			added.AddRange(rows);
			// the Game Modes tab (user 2026-10-17): which one is on, then one button per mode
			Presets.Preset now = Presets.Find(Presets.Current);
			foreach (Presets.Preset p in Presets.All)
			{
				string pv = (p.Id == now.Id) ? "On" : ((AssistMode.SandboxLocked && p.Id != "sandbox") ? "Locked" : "Play");
				string pd = (AssistMode.SandboxLocked && p.Id != "sandbox") ? "This file is in Sandbox for good. Delete it to play it another way." : p.Blurb;
				added.Add(new AssistRow { Id = "preset_" + p.Id + "_btn", Section = "Game Modes", Label = p.Name, Values = pv, Default = pv, Description = pd });
			}
			// the About tab (user 2026-10-17)
			added.Add(new AssistRow { Id = "ab_version", Section = "About", Label = "Silkeste", Values = "Version " + Plugin.Version, Default = "Version " + Plugin.Version, Description = "Celeste's Madeline in Hollow Knight: Silksong, made by Twifty. Thanks for playing! A rating on Melty helps a lot." });
			added.Add(new AssistRow { Id = "ab_new", Section = "About", Label = "What's New", Values = "This update", Default = "This update", Description = "Silkeste 2.1: much smoother menus and cutscenes, a new-file menu with game mode, difficulty and optional starting abilities, difficulty in the pause menu, 8 emotes with their own menu, headwear, companions, aura, footprints and dash trail shapes, and a fairer lock with two dashes." });
			added.Add(new AssistRow { Id = "ab_celeste", Section = "About", Label = "Celeste", Values = "Maddy Makes Games", Default = "Maddy Makes Games", Description = "Madeline, her art, sounds and menus come from your own copy of Celeste, by Maddy Makes Games (Extremely OK Games). Nothing from Celeste is in Silkeste's download." });
			added.Add(new AssistRow { Id = "ab_silksong", Section = "About", Label = "Hollow Knight: Silksong", Values = "Team Cherry", Default = "Team Cherry", Description = "Pharloom and everything in it belong to Team Cherry." });
			added.Add(new AssistRow { Id = "ab_license", Section = "About", Label = "License", Values = "MIT", Default = "MIT", Description = "Silkeste's own code is MIT licensed, and remixes are welcome." });
			added.Add(new AssistRow { Id = "ab_melty_btn", Section = "About", Label = "Silkeste on Melty", Values = "Open", Default = "Open", Description = "Opens Silkeste's Melty page in your browser: updates, ratings and the full list of features." });
			AddGroups(added);
			AllField.SetValue(null, added.ToArray());
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Unified settings: " + e.Message);
		}
	}

	private static AssistRow? RowFor(Selectable sel, string section, int n)
	{
		string id = "ss_" + section.ToLowerInvariant() + "_" + n;
		string lab0 = LabelOf(sel, (sel as MenuOptionHorizontal)?.optionText).ToLowerInvariant();
		if (lab0.Contains("tv") || sel.name.IndexOf("TV", StringComparison.Ordinal) >= 0)
		{
			return null;   // a console TV-mode option that does nothing on PC (user 2026-10-17)
		}
		MenuOptionHorizontal opt = sel as MenuOptionHorizontal;
		if (opt != null && opt.optionList != null && opt.optionList.Length > 0)
		{
			try { opt.RefreshCurrentIndex(); } catch (Exception) { }
			string[] names = new string[opt.optionList.Length];
			for (int i = 0; i < names.Length; i++)
			{
				names[i] = Clean(OptionText(opt, i));
			}
			int cur = Mathf.Clamp(opt.selectedOptionIndex, 0, names.Length - 1);
			return new AssistRow { Id = id, Section = section, Label = LabelOf(sel, opt.optionText), Values = string.Join(",", names), Default = names[cur], Description = Describe(LabelOf(sel, opt.optionText), null) };
		}
		Slider sl = sel as Slider ?? sel.GetComponentInChildren<Slider>(true);
		if (sl != null && (sel is Slider || sel.GetComponent<Slider>() == null))
		{
			int lo = Mathf.RoundToInt(sl.minValue);
			int hi = Mathf.RoundToInt(sl.maxValue);
			if (hi <= lo || hi - lo > 100)
			{
				return null;
			}
			string[] vals = new string[hi - lo + 1];
			for (int i = 0; i < vals.Length; i++)
			{
				vals[i] = (lo + i).ToString();
			}
			return new AssistRow { Id = id, Section = section, Label = LabelOf(sel, null), Values = string.Join(",", vals), Default = Mathf.RoundToInt(sl.value).ToString(), Description = Describe(LabelOf(sel, null), null) };
		}
		return null;
	}

	/// <summary>A binding button: Settings closes and Silksong's keyboard or controller page opens.</summary>
	public static void OpenBindings(AssistRow r, Action closeSettings)
	{
		if (r.Id == "ss_pausemenu_btn")
		{
			closeSettings();
			GameManager gm = GameManager.SilentInstance;
			if (gm != null && !gm.isPaused)
			{
				CelesteMenus.AllowPause = true;
				gm.StartCoroutine(gm.PauseGameToggle(playSound: true));
			}
			return;
		}
		if (r.Id == "ab_melty_btn")
		{
			Application.OpenURL("https://melty.gg/m/silkeste");
			return;
		}
		UIManager ui = UIManager.instance;
		if (ui == null)
		{
			return;
		}
		closeSettings();
		// through Silksong's own Options first, the way its menus expect (user 2026-10-17: jumping straight there
		// from the title broke the menus)
		ui.StartCoroutine(ViaOptions(ui, r.Id == "ss_bind_keyboard_btn"));
	}

	private static System.Collections.IEnumerator ViaOptions(UIManager ui, bool keyboard)
	{
		FromBindings = false;
		CelesteMenus.BypassOptions = true;   // this one really is Silksong's Options, on the way to the bindings
		ui.UIGoToOptionsMenu();
		float t = 0f;
		while (t < 0.6f)
		{
			t += Time.unscaledDeltaTime;
			yield return null;
		}
		try
		{
			if (keyboard)
			{
				ui.UIGoToKeyboardMenu();
			}
			else
			{
				ui.UIGoToControllerMenu();
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Unified settings: bindings: " + e.Message);
		}
		FromBindings = true;
	}

	// ---- folding groups (user 2026-10-17: Customization and Speedrun had too many rows) ------------------------
	private static readonly string[][] Groups = new string[6][]
	{
		new string[7] { "grp_start", "Starting Abilities", "start_climb", "start_double_dash", "start_glide", "start_clawline", "start_silk_soar" },
		new string[8] { "grp_hair", "Hair", "hair_two", "hair_one", "hair_zero", "hair_length", "hair_size", "hair_effect" },
		new string[6] { "grp_extras", "Accessories", "headwear", "companion", "cat_ears", "backpack" },
		new string[8] { "grp_effects", "Effects", "aura", "footprints", "dash_trail", "trail_shape", "landing_effects", "dash_colour_rush" },
		new string[8] { "grp_splits", "Splits", "sr_splits", "sr_split_list", "sr_compare", "sr_sum_of_best", "sr_attempts", "sr_pace" },
		new string[7] { "grp_display", "Timer Display", "sr_precision", "sr_last_room", "sr_speed", "sr_inputs", "sr_pos" }
	};

	private static readonly Dictionary<string, string> groupOf = new Dictionary<string, string>();

	public static readonly HashSet<string> Expanded = new HashSet<string>();

	private static void AddGroups(List<AssistRow> all)
	{
		LoadGroups();
		Expanded.Add("grp_difficulty");   // always open: it's the line between the modes and the difficulty
		Expanded.Add("grp_start");
		groupOf.Clear();
		// every group goes together at the end of its tab (user 2026-10-17: not one at the top and two at the bottom)
		Dictionary<string, List<AssistRow>> bySection = new Dictionary<string, List<AssistRow>>();
		List<string> order = new List<string>();
		foreach (string[] g in Groups)
		{
			List<AssistRow> members = new List<AssistRow>();
			for (int k = 2; k < g.Length; k++)
			{
				int at = all.FindIndex((AssistRow r) => r.Id == g[k]);
				if (at >= 0)
				{
					members.Add(all[at]);
					all.RemoveAt(at);
					groupOf[g[k]] = g[0];
				}
			}
			if (members.Count == 0)
			{
				continue;
			}
			string section = members[0].Section;
			if (!bySection.ContainsKey(section))
			{
				bySection[section] = new List<AssistRow>();
				order.Add(section);
			}
			bySection[section].Add(new AssistRow { Id = g[0], Section = section, Label = g[1], Values = "Show " + members.Count + ",Hide", Default = "Show " + members.Count, Description = g[0] == "grp_start" ? "Leave these all off for the intended Silkeste experience: each one is a reward for exploring Pharloom. Turning any on gives it right away and makes this an assist save: no achievements or golden berries." : "Click, Enter or Left/Right to fold these " + members.Count + " options open or closed." });
			bySection[section].AddRange(members);
		}
		foreach (string section in order)
		{
			int last = all.FindLastIndex((AssistRow r) => r.Section == section);
			all.InsertRange(last + 1, bySection[section]);
		}
	}

	public static bool IsGroup(AssistRow r)
	{
		return r.Id != null && r.Id.StartsWith("grp_", StringComparison.Ordinal);
	}

	/// <summary>The group a row folds into, or null.</summary>
	public static string GroupOf(AssistRow r)
	{
		return (r.Id != null && groupOf.TryGetValue(r.Id, out var g)) ? g : null;
	}

	/// <summary>False for a row folded away inside a closed group.</summary>
	public static bool Visible(AssistRow r)
	{
		string g = GroupOf(r);
		return g == null || Expanded.Contains(g);
	}

	public static void Toggle(AssistRow r)
	{
		if (!Expanded.Remove(r.Id))
		{
			Expanded.Add(r.Id);
		}
		// remembered between visits (user 2026-10-17)
		try
		{
			System.IO.File.WriteAllLines(GroupsFile, new List<string>(Expanded).ToArray());
		}
		catch (Exception)
		{
		}
	}

	private static string GroupsFile => System.IO.Path.Combine(AssistMode.DataDir ?? "", "settings_groups.cfg");

	private static bool groupsLoaded;

	private static void LoadGroups()
	{
		if (groupsLoaded)
		{
			return;
		}
		groupsLoaded = true;
		try
		{
			if (System.IO.File.Exists(GroupsFile))
			{
				foreach (string l in System.IO.File.ReadAllLines(GroupsFile))
				{
					if (l.Trim().Length > 0)
					{
						Expanded.Add(l.Trim());
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	/// <summary>Set while Silksong's binding pages were opened from Settings: leaving them comes back to Settings.</summary>
	public static bool FromBindings;

	public static bool IsButton(AssistRow r)
	{
		return r.Id != null && r.Id.EndsWith("_btn", StringComparison.Ordinal);
	}

	/// <summary>Presses a page button: Silksong opens that page (Settings closes first so it shows), or resets.</summary>
	public static void Press(AssistRow r, Action closeSettings)
	{
		if (!controls.TryGetValue(r.Id, out var sel) || sel == null)
		{
			return;
		}
		try
		{
			UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
			if (r.Values != "Reset")
			{
				closeSettings();
			}
			if (es != null)
			{
				es.SetSelectedGameObject(sel.gameObject);
				UnityEngine.EventSystems.ExecuteEvents.Execute(sel.gameObject, new UnityEngine.EventSystems.BaseEventData(es), UnityEngine.EventSystems.ExecuteEvents.submitHandler);
			}
			if (r.Values == "Reset")
			{
				Refresh();
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Unified settings: " + r.Label + ": " + e.Message);
		}
	}

	// what each of Silksong's options does, in plain words
	private static string Describe(string label, string verb)
	{
		string l = (label ?? "").ToLowerInvariant();
		if (l.Contains("language")) return "The language for all of the game's text.";
		if (l.Contains("shake")) return "How much the camera shakes in Silksong's big moments (Silkeste's dash shake is separate).";
		if (l.Contains("hud")) return "Shows or hides the pop-up messages on the HUD, like new map markers and items.";
		if (l.Contains("achievement")) return "Shows a pop-up when you earn one of Silksong's own achievements.";
		if (l.Contains("backer")) return "Shows the Kickstarter backers' names in the world.";
		if (l.Contains("framerate")) return "Frame rate mode (Switch-style).";
		if (l.Contains("master")) return "The overall volume of everything.";
		if (l.Contains("music")) return "The volume of the music.";
		if (l.Contains("sound") || l.Contains("sfx") || l.Contains("effect")) return "The volume of sound effects, Celeste's sounds included.";
		if (l.Contains("speaker") || l.Contains("stereo") || l.Contains("mono")) return "Stereo or mono sound.";
		if (l.Contains("resolution")) return "The screen resolution. It applies straight away.";
		if (l.Contains("fullscreen") || l.Contains("full screen") || l.Contains("window")) return "Fullscreen, borderless or windowed.";
		if (l.Contains("vsync") || l.Contains("v-sync")) return "Syncs frames to your monitor to stop tearing.";
		if (l.Contains("frame") || l.Contains("fps")) return "Caps how many frames per second the game draws.";
		if (l.Contains("display") || l.Contains("monitor")) return "Which monitor the game shows on.";
		if (l.Contains("particle")) return "How many particle effects are drawn.";
		if (l.Contains("shader") || l.Contains("quality")) return "Graphics quality. Lower is faster on slower PCs.";
		if (l.Contains("brightness")) return "Opens Silksong's brightness screen.";
		if (l.Contains("advanced")) return "Opens Silksong's advanced video settings.";
		if (l.Contains("overscan")) return "Opens the screen-edge (overscan) adjuster.";
		if (verb == "Reset") return "Puts every option on this page back to Silksong's defaults.";
		if (verb == "Open") return "Opens this page in Silksong's menu.";
		return "One of Silksong's own options. It saves and applies as usual.";
	}

	/// <summary>The value index shown now, read from the control.</summary>
	public static int Index(AssistRow r)
	{
		if (r.Id != null && r.Id.StartsWith("ab_", StringComparison.Ordinal))
		{
			return 0;
		}
		if (IsGroup(r))
		{
			return Expanded.Contains(r.Id) ? 1 : 0;
		}
		if (!controls.TryGetValue(r.Id, out var sel) || sel == null)
		{
			return 0;
		}
		if (sel is MenuOptionHorizontal opt)
		{
			return Mathf.Clamp(opt.selectedOptionIndex, 0, r.Values.Split(',').Length - 1);
		}
		Slider sl = sel as Slider ?? sel.GetComponentInChildren<Slider>(true);
		return sl != null ? Mathf.Clamp(Mathf.RoundToInt(sl.value - sl.minValue), 0, r.Values.Split(',').Length - 1) : 0;
	}

	/// <summary>Sets the control to value index i, applying it as Silksong would.</summary>
	public static void Set(AssistRow r, int i)
	{
		if (!controls.TryGetValue(r.Id, out var sel) || sel == null)
		{
			return;
		}
		try
		{
			if (sel is MenuOptionHorizontal opt)
			{
				opt.selectedOptionIndex = i;
				UpdateSetting?.Invoke(opt, null);
				if (opt.applySettingOn != MenuOptionHorizontal.ApplyOnType.Scroll)
				{
					opt.ApplySettings();
				}
				opt.RefreshMenuControls();
				return;
			}
			Slider sl = sel as Slider ?? sel.GetComponentInChildren<Slider>(true);
			if (sl != null)
			{
				sl.value = sl.minValue + i;
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Unified settings: " + r.Label + ": " + e.Message);
		}
	}

	private static string OptionText(MenuOptionHorizontal opt, int i)
	{
		string raw = opt.optionList[i];
		if (opt.localizeText)
		{
			try
			{
				string t = TeamCherry.Localization.Language.Get(raw, opt.sheetTitle);
				if (!string.IsNullOrEmpty(t) && !t.StartsWith("#!#", StringComparison.Ordinal))
				{
					return t;
				}
			}
			catch (Exception) { }
		}
		return raw;
	}

	private static string LabelOf(Selectable s, Text skip)
	{
		Text best = null;
		foreach (Text t in s.GetComponentsInChildren<Text>(true))
		{
			if (t == skip || string.IsNullOrEmpty(t.text) || t.text.Length > 40)
			{
				continue;
			}
			if (best == null || t.transform.position.x < best.transform.position.x)
			{
				best = t;
			}
		}
		if (best != null)
		{
			return Clean(best.text);
		}
		foreach (Component c in s.GetComponentsInChildren<Component>(true))
		{
			PropertyInfo p = c != null ? c.GetType().GetProperty("text") : null;
			if (p != null && c.GetType().Name.StartsWith("TextMeshPro", StringComparison.Ordinal))
			{
				string v = p.GetValue(c, null) as string;
				if (!string.IsNullOrEmpty(v))
				{
					return Clean(v);
				}
			}
		}
		return System.Text.RegularExpressions.Regex.Replace(s.gameObject.name.Replace("Option", "").Replace("_", " "), "([a-z])([A-Z])", "$1 $2").Trim();
	}

	private static string Clean(string s)
	{
		s = System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]*>", "").Replace('\n', ' ').Replace(",", "").Trim();
		return s.Replace('’', '\'').Replace('‘', '\'');
	}
}
