using System;
using System.IO;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// The title's mountain changes with you (user 2026-10-17): its light follows your PC clock (dawn, day, sunset,
/// night) and its colours follow the area your last save was in (Moss Grotto greens, Bellhart gold, the Docks' deep
/// blue, the Citadel's red-gold...), with black thread once a save reaches Act 3. Picked once as the game starts.
/// </summary>
public static class TitleScene
{
	public static Color Top = new Color(0.05f, 0.06f, 0.19f);

	public static Color Mid = new Color(0.15f, 0.13f, 0.38f);

	public static Color Bottom = new Color(0.44f, 0.29f, 0.58f);

	public static Color Moon = new Color(0.96f, 0.95f, 1f);

	public static float Stars = 1f;

	public static Color Tint = Color.white;

	public static float TintAmount;

	public static Color Particle = Color.white;

	public static bool Rising;

	public static string Name = "night";

	private static bool picked;

	private static string File => Path.Combine(AssistMode.DataDir ?? "", "title_scene.cfg");

	/// <summary>Called while playing: remembers where you are for the next title screen.</summary>
	public static void Remember(string area, bool act3)
	{
		try
		{
			System.IO.File.WriteAllText(File, (area ?? "") + "|" + (act3 ? "act3" : ""));
		}
		catch (Exception)
		{
		}
	}

	private static string pickedFor;

	/// <summary>What the settings ask for now; the sky is repainted when it changes.</summary>
	public static string Wanted => AssistMode.TitleSky + "|" + AssistMode.TitleArea;

	public static bool Stale => picked && pickedFor != Wanted;

	public static void Pick()
	{
		if (picked && !Stale)
		{
			return;
		}
		picked = true;
		pickedFor = Wanted;
		Top = new Color(0.05f, 0.06f, 0.19f);
		Mid = new Color(0.15f, 0.13f, 0.38f);
		Bottom = new Color(0.44f, 0.29f, 0.58f);
		Moon = new Color(0.96f, 0.95f, 1f);
		Stars = 1f;
		Tint = Color.white;
		TintAmount = 0f;
		Particle = Color.white;
		Rising = false;
		Name = "night";
		// the light: your clock, or the one picked in Settings > Video > Title Sky
		int h = DateTime.Now.Hour;
		string sky = AssistMode.TitleSky;
		if (sky == "Night")
		{
			h = 23;
		}
		else if (sky == "Dawn")
		{
			h = 6;
		}
		else if (sky == "Day")
		{
			h = 12;
		}
		else if (sky == "Sunset")
		{
			h = 18;
		}
		if (h >= 5 && h < 8)
		{
			Name = "dawn";
			Top = new Color(0.16f, 0.17f, 0.42f);
			Mid = new Color(0.62f, 0.4f, 0.62f);
			Bottom = new Color(0.98f, 0.68f, 0.55f);
			Moon = new Color(1f, 0.86f, 0.7f);
			Stars = 0.35f;
		}
		else if (h >= 8 && h < 17)
		{
			Name = "day";
			Top = new Color(0.2f, 0.42f, 0.8f);
			Mid = new Color(0.45f, 0.68f, 0.95f);
			Bottom = new Color(0.78f, 0.88f, 1f);
			Moon = new Color(1f, 0.98f, 0.88f);
			Stars = 0f;
		}
		else if (h >= 17 && h < 20)
		{
			Name = "sunset";
			Top = new Color(0.14f, 0.1f, 0.34f);
			Mid = new Color(0.7f, 0.32f, 0.38f);
			Bottom = new Color(1f, 0.62f, 0.3f);
			Moon = new Color(1f, 0.78f, 0.5f);
			Stars = 0.2f;
		}
		// the colours: your last save's area
		string area = "";
		bool act3 = false;
		try
		{
			if (System.IO.File.Exists(File))
			{
				string[] p = System.IO.File.ReadAllText(File).Split('|');
				area = p[0].ToLowerInvariant();
				act3 = p.Length > 1 && p[1] == "act3";
			}
		}
		catch (Exception)
		{
		}
		// Settings > Video > Title Mountain picks an area by hand
		string pick = AssistMode.TitleArea;
		if (pick != "Auto")
		{
			act3 = pick == "Act 3";
			area = pick == "Moss" ? "moss" : (pick == "Bellhart" ? "bell" : (pick == "Docks" ? "dock" : (pick == "Citadel" ? "citadel" : (pick == "Marrow" ? "marrow" : (pick == "Coral" ? "coral" : "")))));
		}
		string tint = null;
		string part = "FFFFFF";
		if (act3)
		{
			tint = "8C1C2E"; part = "2A0A10"; Rising = true;   // black thread drifting up
		}
		else if (area.Contains("moss") || area.Contains("shellwood") || area.Contains("wisp") || area.Contains("verdania"))
		{
			tint = "4FA35C"; part = "B8F28A"; Rising = true;   // spores
		}
		else if (area.Contains("bell") || area.Contains("far fields") || area.Contains("hunter"))
		{
			tint = "D9A21B"; part = "FFD27A";
		}
		else if (area.Contains("dock") || area.Contains("blasted") || area.Contains("bilewater") || area.Contains("weavenest"))
		{
			tint = "1C6E8C"; part = "8FD6E8"; Rising = true;   // bubbles and mist
		}
		else if (area.Contains("citadel") || area.Contains("song") || area.Contains("choral") || area.Contains("high halls") || area.Contains("underworks") || area.Contains("memorium"))
		{
			tint = "B5452E"; part = "FFB070"; Rising = true;   // embers
		}
		else if (area.Contains("marrow") || area.Contains("bone") || area.Contains("mosshome") || area.Contains("greymoor"))
		{
			tint = "8C8FA6"; part = "E8E8F0";
		}
		else if (area.Contains("coral") || area.Contains("sands") || area.Contains("karak"))
		{
			tint = "D96A6A"; part = "FFC2A8";
		}
		if (tint != null && ColorUtility.TryParseHtmlString("#" + tint, out Color c))
		{
			Tint = c;
			TintAmount = 0.35f;
		}
		if (ColorUtility.TryParseHtmlString("#" + part, out Color pc))
		{
			Particle = pc;
		}
		Plugin.Log.LogInfo("Title scene: " + Name + (tint != null ? (", " + (act3 ? "act 3" : area)) : ""));
	}
}
