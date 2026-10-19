using System;
using System.Collections.Generic;
using GlobalEnums;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Light Cutscenes (user 2026-10-19: "make the cutscenes less laggy"): while a cutscene plays, Silksong's heaviest
/// full-screen camera effects (the cinematic blur, bloom, lens distortion, debanding and grain) are switched off, and
/// back on when it ends. Gameplay looks the same. With silkeste_dev.txt it also logs the frame rate every 2 seconds.
/// </summary>
public class CutsceneFx : MonoBehaviour
{
	private static readonly Type[] Effects = new Type[6]
	{
		typeof(CameraBlurPlane),
		typeof(LensCAAndDistortion),
		typeof(DebandEffect),
		typeof(UnityStandardAssets.ImageEffects.FastNoise),
		typeof(BloomOptimized),
		typeof(LightBlur)
	};

	private readonly List<Behaviour> turnedOff = new List<Behaviour>();

	private float scanAt;

	private float inFor;

	private float outFor;

	private bool light;

	private float fpsTime;

	private int fpsFrames;

	private float worst;

	private int cutsceneFrames;

	private int gcAt;

	private void Update()
	{
		float dt = Time.unscaledDeltaTime;
		bool cut = false;
		try
		{
			cut = InCutscene();
			bool want = cut && AssistMode.IsOn(AssistOptions.LightCutscenes);
			// a little hysteresis, so a boss's roar or a short stagger doesn't flick the effects off and on
			inFor = want ? inFor + dt : 0f;
			outFor = want ? 0f : outFor + dt;
			if (inFor >= 0.3f)
			{
				if (!light || Time.unscaledTime >= scanAt)
				{
					scanAt = Time.unscaledTime + 1f;   // effects that turn on during the cutscene get caught too
					TurnOff();
				}
				light = true;
			}
			else if (light && outFor >= 0.2f)
			{
				Restore();
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Light cutscenes: " + e.Message);
			Restore();
		}
		if (MadelineTouches.DevOn)
		{
			PerfProbe.Install();
			fpsTime += dt;
			fpsFrames++;
			worst = Math.Max(worst, dt);
			if (cut)
			{
				cutsceneFrames++;
			}
			if (fpsTime >= 2f)
			{
				Plugin.Log.LogInfo("Perf: " + (fpsFrames / fpsTime).ToString("0") + " fps, worst frame " + (worst * 1000f).ToString("0") + " ms" + (cutsceneFrames > 0 ? ", cutscene" : "") + (light ? ", light (" + turnedOff.Count + " effects off)" : "") + (Application.isFocused ? "" : ", unfocused") + ", " + (GC.CollectionCount(0) - gcAt) + " GCs" + PerfProbe.Report(fpsFrames));
				gcAt = GC.CollectionCount(0);
				fpsTime = 0f;
				fpsFrames = 0;
				worst = 0f;
				cutsceneFrames = 0;
			}
		}
	}

	private static bool InCutscene()
	{
		GameManager gm = GameManager.SilentInstance;
		if (gm == null || gm.IsMenuScene() || gm.isPaused || BossRematch.Active)
		{
			return false;
		}
		if (gm.GameState == GameState.CUTSCENE)
		{
			return true;
		}
		HeroController h = HeroController.UnsafeInstance;
		if (h == null || !h.controlReqlinquished || h.cState.dead || h.cState.hazardDeath || h.cState.transitioning || h.playerData.atBench)
		{
			return false;
		}
		// Silkeste's own screens take control too: those aren't cutscenes
		return !AssistMode.IsOpen && !BossCard.RecordsOpen && !StartingAbilities.IsOpen;
	}

	private void TurnOff()
	{
		foreach (Type t in Effects)
		{
			foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(t, FindObjectsSortMode.None))
			{
				Behaviour b = o as Behaviour;
				if (b != null && b.enabled)
				{
					b.enabled = false;
					turnedOff.Add(b);
				}
			}
		}
	}

	private void Restore()
	{
		foreach (Behaviour b in turnedOff)
		{
			if (b != null)
			{
				b.enabled = true;
			}
		}
		turnedOff.Clear();
		light = false;
		inFor = 0f;
	}
}
