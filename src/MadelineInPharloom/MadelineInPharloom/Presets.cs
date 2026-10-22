using System;
using System.Collections.Generic;

namespace MadelineInPharloom;

/// <summary>
/// Game modes (user 2026-10-17): one press sets every gameplay option (Assist, Variants, Challenges, Speedrun) for a
/// way of playing (the difficulty is its own row under them); Sandbox also unlocks everything. Looks are never touched. A Game Modes tab in
/// Settings, and a picker on the file select.
/// </summary>
public static class Presets
{
	public struct Preset
	{
		public string Id;

		public string Name;

		public string Blurb;

		public string Values;   // "row_id=Value;..." on top of every gameplay option at its default
	}

	// game modes set how you play; the difficulty below them in the tab is separate and mixes with any of them
	public static readonly Preset[] All = new Preset[3]
	{
		new Preset { Id = "standard", Name = "Standard", Blurb = "Silkeste as it comes: every Assist, Variant, Challenge and Speedrun option back to normal. Your difficulty stays as it is.", Values = "" },
		new Preset { Id = "speedrun", Name = "Speedrunner", Blurb = "Standard play with the timer on save + room, cutscenes and dialogue skipped, plus the speedometer and the input display.", Values = "timer=Save + Room;timer_berries=On;auto_skip_cutscenes=On;sr_dialogue=Instant;sr_speed=On;sr_inputs=On" },
		new Preset { Id = "sandbox", Name = "Sandbox", Blurb = "Everything unlocked: every ability, tool, crest and map, full silk, money and every boss rematch, plus endless stamina and infinite dashes. Unlocks stay on that save, which becomes an assist save.", Values = "infinite_stamina=On" }
	};

	/// <summary>The gameplay tabs a preset sets.</summary>
	public static readonly string[] Sections = new string[4] { "Assist Mode", "Variants", "Challenges", "Speedrun Timer" };

	/// <summary>The game mode of the save file in use.</summary>
	public static string Current => AssistMode.ModeId;

	public static Preset Find(string id)
	{
		foreach (Preset p in All)
		{
			if (p.Id == id)
			{
				return p;
			}
		}
		return All[0];
	}

	public static Dictionary<string, string> ValuesOf(Preset p)
	{
		Dictionary<string, string> d = new Dictionary<string, string>();
		foreach (string pair in (p.Values ?? "").Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries))
		{
			int eq = pair.IndexOf('=');
			if (eq > 0)
			{
				d[pair.Substring(0, eq).Trim()] = pair.Substring(eq + 1).Trim();
			}
		}
		return d;
	}

	public static void Apply(string id)
	{
		if (AssistMode.SandboxLocked && id != "sandbox")
		{
			Plugin.Log.LogInfo("Game mode: this file is in Sandbox until it's deleted");
			return;
		}
		Preset p = Find(id);
		AssistMode.ApplyPreset(ValuesOf(p), Sections);
		AssistMode.ModeId = p.Id;
		AssistMode.SaveNow();
		Plugin.Log.LogInfo("Game mode: " + p.Name);
		// only the mode you end on counts: cycling past Sandbox on the file select unlocks nothing
		sandboxPending = p.Id == "sandbox";
		if (sandboxPending && CelesteFileSelect.HasSave(AssistMode.ProfileSlot))
		{
			AssistMode.LockSandbox();   // a file that exists is in Sandbox for good; an empty one locks when it starts
		}
		Tick();
	}

	private static bool sandboxPending;

	private static int sandboxRecordsSlot = -1;

	/// <summary>Sandbox unlocks happen in a save: right away, or once the chosen file has loaded.</summary>
	public static void Tick()
	{
		// Sandbox files have all 3 silk hearts and the fully upgraded needle, older ones too (user 2026-10-21); not in a
		// rematch, which has the needle of that point of the game
		PlayerData spd = PlayerData.instance;
		if (spd != null && AssistMode.ModeId == "sandbox" && !BossRematch.Active && (spd.silkRegenMax < 3 || spd.nailUpgrades < 4) && GameManager.SilentInstance != null && !GameManager.SilentInstance.IsMenuScene())
		{
			spd.silkRegenMax = Math.Max(spd.silkRegenMax, 3);
			spd.HasSeenSilkHearts = true;
			spd.nailUpgrades = Math.Max(spd.nailUpgrades, 4);
		}
		// a Sandbox file's Records list every boss for a rematch, fought or not
		GameManager sgm = GameManager.SilentInstance;
		if (spd != null && sgm != null && !sgm.IsMenuScene() && AssistMode.ModeId == "sandbox" && sandboxRecordsSlot != sgm.profileID)
		{
			sandboxRecordsSlot = sgm.profileID;
			BossCard.Instance?.ImportRematches();
		}
		else if (sgm == null || sgm.IsMenuScene())
		{
			sandboxRecordsSlot = -1;
		}
		// a Sandbox file that never got its unlocks (the mode was already Sandbox when the new file began, so
		// Apply never ran this session: user 2026-10-22)
		bool missing = spd != null && AssistMode.ModeId == "sandbox" && !TechRoom.Active && !BossRematch.Active && (!spd.hasSuperJump || !spd.mapAllRooms);
		if ((!sandboxPending && !missing) || PlayerData.instance == null || HeroController.UnsafeInstance == null || GameManager.SilentInstance == null || GameManager.SilentInstance.IsMenuScene())
		{
			return;
		}
		sandboxPending = false;
		AssistMode.LockSandbox();
		MadelineTouches.UnlockEverything();
	}

	/// <summary>The next preset after the current one (the file select's picker).</summary>
	public static void Next()
	{
		int i = Array.FindIndex(All, (Preset p) => p.Id == Current);
		Apply(All[(i + 1) % All.Length].Id);
	}
}
