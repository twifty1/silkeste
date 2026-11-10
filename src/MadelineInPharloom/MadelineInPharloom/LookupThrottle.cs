using System;
using HarmonyLib;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// The autosave notice at start stuttered (user 2026-11-10: about 20 fps, Silkeste 40 ms a frame). Until the game's
/// GameManager, GameCameras and UIManager exist, their lookups search the whole scene (FindObjectOfType) every time
/// they're asked, and GameCameras.instance and UIManager.instance also write an error to the log each time; Silkeste asks
/// dozens of times a frame. Each one registers itself when it wakes (its Awake sets _instance), so while there is none
/// the search runs at most every quarter second and the answer is "none" in between.
/// </summary>
public static class LookupThrottle
{
	private static readonly AccessTools.FieldRef<GameManager> Gm = AccessTools.StaticFieldRefAccess<GameManager>(AccessTools.Field(typeof(GameManager), "_instance"));

	private static readonly AccessTools.FieldRef<GameCameras> Cams = AccessTools.StaticFieldRefAccess<GameCameras>(AccessTools.Field(typeof(GameCameras), "_instance"));

	private static readonly AccessTools.FieldRef<UIManager> Ui = AccessTools.StaticFieldRefAccess<UIManager>(AccessTools.Field(typeof(UIManager), "_instance"));

	private static float gmAt = -10f;

	private static float camsAt = -10f;

	private static float uiAt = -10f;

	public static void Patch(Harmony h)
	{
		Try(h, typeof(GameManager), "SilentInstance", "GmPrefix");
		Try(h, typeof(GameManager), "instance", "GmInstancePrefix");
		Try(h, typeof(GameCameras), "SilentInstance", "CamsPrefix");
		Try(h, typeof(GameCameras), "instance", "CamsInstancePrefix");
		Try(h, typeof(UIManager), "instance", "UiPrefix");
	}

	private static void Try(Harmony h, Type t, string prop, string prefix)
	{
		try
		{
			h.Patch(AccessTools.PropertyGetter(t, prop), new HarmonyMethod(typeof(LookupThrottle), prefix));
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Lookup throttle " + t.Name + "." + prop + ": " + e.Message);
		}
	}

	// true: let the game look (it has one, or it's time for another search); false: "none" without searching
	private static bool Allow(bool have, ref float at)
	{
		if (have)
		{
			return true;
		}
		float now = Time.realtimeSinceStartup;
		if (now - at < 0.25f)
		{
			return false;
		}
		at = now;
		return true;
	}

	private static bool GmPrefix(ref GameManager __result)
	{
		if (Allow(Gm() != null, ref gmAt))
		{
			return true;
		}
		__result = null;
		return false;
	}

	private static bool CamsPrefix(ref GameCameras __result)
	{
		if (Allow(Cams() != null, ref camsAt))
		{
			return true;
		}
		__result = null;
		return false;
	}

	// GameManager.instance asks SilentInstance and logs an error when there's none: "none" in between searches, unlogged
	private static bool GmInstancePrefix(ref GameManager __result)
	{
		if (Gm() != null || Time.realtimeSinceStartup - gmAt >= 0.25f)
		{
			return true;
		}
		__result = null;
		return false;
	}

	// GameCameras.instance asks SilentInstance itself: it only answers "none" in between searches (skipping the error it
	// would log), and leaves the timing of the search to SilentInstance
	private static bool CamsInstancePrefix(ref GameCameras __result)
	{
		if (Cams() != null || Time.realtimeSinceStartup - camsAt >= 0.25f)
		{
			return true;
		}
		__result = null;
		return false;
	}

	private static bool UiPrefix(ref UIManager __result)
	{
		if (Allow(Ui() != null, ref uiAt))
		{
			return true;
		}
		__result = null;
		return false;
	}
}
