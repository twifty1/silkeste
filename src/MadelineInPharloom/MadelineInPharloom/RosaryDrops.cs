using System;
using System.Globalization;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Settings > Game > Rosary Drops (user 2026-11-10): 0x to 3x the rosaries enemies drop. Every enemy drop goes
/// through HealthManager.SpawnCurrency (the Thief's extra too), so its rosary counts are scaled there; a part left
/// over (2.5 of a bead) drops the extra bead that often. Shell shards are left as they are.
/// </summary>
public static class RosaryDrops
{
	public static void Patch(Harmony h)
	{
		h.Patch(AccessTools.Method(typeof(HealthManager), "SpawnCurrency"), new HarmonyMethod(typeof(RosaryDrops), "Prefix"));
	}

	private static float Multiplier
	{
		get
		{
			try
			{
				return AssistMode.RosaryDrops;
			}
			catch (Exception)
			{
				return 1f;
			}
		}
	}

	private static int Scale(int n, float m)
	{
		if (n <= 0)
		{
			return n;
		}
		float f = n * m;
		int whole = Mathf.FloorToInt(f);
		return whole + ((UnityEngine.Random.value < f - whole) ? 1 : 0);
	}

	private static void Prefix(ref int smallGeoCount, ref int mediumGeoCount, ref int largeGeoCount, ref int largeSmoothGeoCount)
	{
		float m = Multiplier;
		if (Math.Abs(m - 1f) < 0.001f)
		{
			return;
		}
		smallGeoCount = Scale(smallGeoCount, m);
		mediumGeoCount = Scale(mediumGeoCount, m);
		largeGeoCount = Scale(largeGeoCount, m);
		largeSmoothGeoCount = Scale(largeSmoothGeoCount, m);
	}
}
