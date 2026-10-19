using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Records' Run page (user 2026-10-17): a timeline of each save, stamped with its play time: bosses beaten, golden
/// berries earned and abilities found. Kept from this update on; rematches don't count.
/// </summary>
public class RunLog : MonoBehaviour
{
	public struct Entry
	{
		public float Time;

		public string Kind;   // boss, golden, ability

		public string Text;
	}

	private static readonly Dictionary<int, List<Entry>> entries = new Dictionary<int, List<Entry>>();

	private static bool loaded;

	private static string File => Path.Combine(AssistMode.DataDir ?? "", "run_log.cfg");

	// abilities watched: save value, name players know
	private static readonly string[][] Abilities = new string[10][]
	{
		new string[2] { "hasDash", "Swift Step" },
		new string[2] { "hasWalljump", "Cling Grip" },
		new string[2] { "hasDoubleJump", "Faydown Cloak" },
		new string[2] { "hasBrolly", "Drifter's Cloak" },
		new string[2] { "hasHarpoonDash", "Clawline" },
		new string[2] { "hasSuperJump", "Silk Soar" },
		new string[2] { "hasNeedolin", "Needolin" },
		new string[2] { "hasNeedolinMemoryPowerup", "Elegy of the Deep" },
		new string[2] { "hasChargeSlash", "Needle Strike" },
		new string[2] { "hasParry", "Cross Stitch" }
	};

	private readonly Dictionary<string, bool> had = new Dictionary<string, bool>();

	private int watchedSlot = -1;

	private float checkAt;

	private string lastArea;

	public static List<Entry> Of(int slot)
	{
		Load();
		return entries.TryGetValue(slot, out List<Entry> l) ? l : new List<Entry>();
	}

	public static void Add(string kind, string text)
	{
		GameManager gm = GameManager.SilentInstance;
		if (gm == null || BossRematch.Active || string.IsNullOrEmpty(text))
		{
			return;
		}
		Load();
		if (!entries.TryGetValue(gm.profileID, out List<Entry> l))
		{
			l = entries[gm.profileID] = new List<Entry>();
		}
		l.Add(new Entry { Time = gm.PlayTime, Kind = kind, Text = text });
		Achievements.Best("chronicler", l.Count);
		try
		{
			System.IO.File.AppendAllText(File, gm.profileID + "|" + gm.PlayTime.ToString("0.0", CultureInfo.InvariantCulture) + "|" + kind + "|" + text.Replace("|", "/") + "\n");
		}
		catch (Exception)
		{
		}
	}

	/// <summary>A deleted file's timeline goes with it.</summary>
	public static void Forget(int slot)
	{
		Load();
		if (!entries.Remove(slot))
		{
			return;
		}
		try
		{
			List<string> lines = new List<string>();
			foreach (KeyValuePair<int, List<Entry>> kv in entries)
			{
				foreach (Entry e in kv.Value)
				{
					lines.Add(kv.Key + "|" + e.Time.ToString("0.0", CultureInfo.InvariantCulture) + "|" + e.Kind + "|" + e.Text);
				}
			}
			System.IO.File.WriteAllLines(File, lines.ToArray());
		}
		catch (Exception)
		{
		}
	}

	private static void Load()
	{
		if (loaded)
		{
			return;
		}
		loaded = true;
		try
		{
			if (!System.IO.File.Exists(File))
			{
				return;
			}
			foreach (string line in System.IO.File.ReadAllLines(File))
			{
				string[] p = line.Split('|');
				if (p.Length == 4 && int.TryParse(p[0], out int slot) && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float t))
				{
					if (!entries.TryGetValue(slot, out List<Entry> l))
					{
						l = entries[slot] = new List<Entry>();
					}
					l.Add(new Entry { Time = t, Kind = p[2], Text = p[3] });
				}
			}
		}
		catch (Exception)
		{
		}
	}

	// abilities: a save value turning on while you play (not while F10 or Sandbox hand out everything at once)
	private void Update()
	{
		if (Time.unscaledTime < checkAt)
		{
			return;
		}
		checkAt = Time.unscaledTime + 1f;
		GameManager gm = GameManager.SilentInstance;
		PlayerData pd = PlayerData.instance;
		if (gm == null || pd == null || gm.IsMenuScene() || HeroController.UnsafeInstance == null)
		{
			watchedSlot = -1;
			return;
		}
		string areaNow = BossCard.CurrentAreaName;
		if (areaNow != null && areaNow != lastArea)
		{
			lastArea = areaNow;
			TitleScene.Remember(areaNow, pd.blackThreadWorld);
		}
		bool fresh = gm.profileID != watchedSlot;
		watchedSlot = gm.profileID;
		int gained = 0;
		List<string> names = new List<string>();
		foreach (string[] a in Abilities)
		{
			System.Reflection.FieldInfo f = typeof(PlayerData).GetField(a[0]);
			bool now = f != null && (bool)f.GetValue(pd);
			if (!fresh && had.TryGetValue(a[0], out bool before) && !before && now)
			{
				gained++;
				names.Add(a[1]);
			}
			had[a[0]] = now;
		}
		if (gained > 0 && gained <= 2)
		{
			foreach (string n in names)
			{
				Add("ability", n);
			}
		}
	}
}
