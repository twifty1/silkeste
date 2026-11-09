using System;
using HarmonyLib;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Celeste's game feel (user 2026-11-09): the split-second freeze when a dash starts or lands a hit, and controller
/// rumble on dashes, hits, big landings and deaths. The freeze goes through the game's own time controls (never
/// Time.timeScale directly, which can stick), and the rumble through its own vibration system, so Silksong's
/// Controller Rumble setting (On, Reduced, Off) applies.
/// </summary>
public static class Feel
{
	private static TimeManager.TimeControlInstance freeze;

	private static float freezeUntil;

	/// <summary>The whole game stops for this long (real time), like Celeste's Freeze.</summary>
	public static void Freeze(float secs)
	{
		GameManager gm = GameManager.SilentInstance;
		if (gm == null || gm.isPaused || AssistMode.IsOpen || Time.timeScale <= 0f)
		{
			return;
		}
		freezeUntil = Math.Max(freezeUntil, Time.unscaledTime + secs);
		if (freeze == null)
		{
			freeze = TimeManager.CreateTimeControl(0f, TimeManager.TimeControlInstance.Type.MinValue);
		}
	}

	/// <summary>Every frame (Plugin.Update): the freeze lets go on time, whatever happened meanwhile.</summary>
	public static void Tick()
	{
		if (freeze != null && Time.unscaledTime >= freezeUntil)
		{
			Release();
		}
	}

	public static void Release()
	{
		freeze?.Release();
		freeze = null;
	}

	// Celeste's strengths and lengths (Input.Rumble): light, medium, strong; short, medium, long
	public const float Light = 0.35f;

	public const float Medium = 0.6f;

	public const float Strong = 1f;

	public const float Short = 0.1f;

	public const float MediumLength = 0.2f;

	public const float Long = 0.4f;

	private static readonly AccessTools.FieldRef<GamepadVibration, AnimationCurve> SmallMotor = AccessTools.FieldRefAccess<GamepadVibration, AnimationCurve>("smallMotor");

	private static readonly AccessTools.FieldRef<GamepadVibration, AnimationCurve> LargeMotor = AccessTools.FieldRefAccess<GamepadVibration, AnimationCurve>("largeMotor");

	private static readonly AccessTools.FieldRef<GamepadVibration, float> PlaybackRate = AccessTools.FieldRefAccess<GamepadVibration, float>("playbackRate");

	private static bool rumbleFailed;

	public static void Rumble(float strength, float secs)
	{
		if (rumbleFailed)
		{
			return;
		}
		try
		{
			GamepadVibration clip = ScriptableObject.CreateInstance<GamepadVibration>();
			AnimationCurve curve = AnimationCurve.Linear(0f, strength, secs, 0f);
			SmallMotor(clip) = curve;
			LargeMotor(clip) = new AnimationCurve(curve.keys);
			PlaybackRate(clip) = 1f;
			VibrationManager.PlayVibrationClipOneShot(VibrationData.Create(LowFidelityVibrations.Vibrate, null, clip), null, isLooping: false, "silkeste", isRealtime: true);
			UnityEngine.Object.Destroy(clip, secs + 1f);
		}
		catch (Exception e)
		{
			rumbleFailed = true;
			Plugin.Log.LogWarning("Rumble: " + e.Message);
		}
	}
}
