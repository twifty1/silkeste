using System;
using HarmonyLib;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Game Modes.difficulty_level (user 2026-10-17): Story takes a quarter and deals double, Easy takes half and deals 50% more,
/// Hard takes 50% more, Hardcore dies to any enemy hit but deals double. Damage taken carries its fractions over, so on Easy a
/// one-mask hit costs a mask every other time and on Hard every other hit costs an extra one.
/// </summary>
public static class Difficulty
{
	private static float carry;

	public static void Patch(Harmony h)
	{
		try
		{
			h.Patch(AccessTools.Method(typeof(HeroController), "TakeDamage"), new HarmonyMethod(typeof(Difficulty), "TakenPrefix") { priority = 300 });
			h.Patch(AccessTools.Method(typeof(HealthManager), "Hit"), new HarmonyMethod(typeof(Difficulty), "DealtPrefix"));
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Difficulty hooks: " + e.Message);
		}
	}

	private static bool TakenPrefix(HeroController __instance, ref int damageAmount, GlobalEnums.HazardType hazardType)
	{
		if (TechRoom.Active && damageAmount > 0)
		{
			TechRoom.Hurt();   // the Tech Tutorial: back to the start point, no masks lost
			return false;
		}
		if (damageAmount <= 0 || hazardType == GlobalEnums.HazardType.PIT || hazardType == GlobalEnums.HazardType.RESPAWN_PIT)
		{
			return true;
		}
		int d = AssistMode.Difficulty;
		bool enemy = hazardType == GlobalEnums.HazardType.ENEMY || hazardType == GlobalEnums.HazardType.NON_HAZARD || hazardType == GlobalEnums.HazardType.EXPLOSION;
		if (d == 4 && enemy)
		{
			damageAmount = 999;   // Hardcore: one enemy hit and she's down for real
			return true;
		}
		float m = AssistMode.DamageTaken;
		if (m == 1f)
		{
			return true;
		}
		float total = damageAmount * m + carry;
		int whole = Mathf.FloorToInt(total + 0.0001f);
		carry = total - whole;
		if (whole <= 0)
		{
			return false;   // Easy: this hit is shrugged off
		}
		damageAmount = whole;
		return true;
	}

	private static bool DealtPrefix(HealthManager __instance, ref HitInstance hitInstance)
	{
		if (TechRoom.IsTutorialEnemy(__instance))
		{
			TechRoom.OnEnemyHit(__instance);   // the Tech Tutorial's practice enemies: hits land, they never die
		}
		float m = AssistMode.DamageDealt;
		if (m != 1f && hitInstance.IsHeroDamage && hitInstance.DamageDealt > 0)
		{
			hitInstance.DamageDealt = Math.Max(1, Mathf.RoundToInt(hitInstance.DamageDealt * m));
		}
		return true;
	}
}
