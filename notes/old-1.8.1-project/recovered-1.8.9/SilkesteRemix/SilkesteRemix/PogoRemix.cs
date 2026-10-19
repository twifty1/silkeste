using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using HarmonyLib;
using MadelineInPharloom;
using UnityEngine;

namespace SilkesteRemix;

[BepInPlugin("silkeste.remix.pogo", "Silkeste remix: pogo", "1.0.0")]
[BepInDependency("melty.madelineinpharloom", BepInDependency.DependencyFlags.HardDependency)]
public class PogoRemix : BaseUnityPlugin
{
	private const float EnemyDamageMult = 0.5f;

	private const int MinDamage = 1;

	private static MadelineController current;

	private static bool hitEnemy;

	private static bool hitBoss;

	private static MethodInfo isBossLike;

	private void Awake()
	{
		Type typeFromHandle = typeof(MadelineController);
		BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		isBossLike = typeFromHandle.GetMethod("IsBossLike", bindingAttr);
		Harmony harmony = new Harmony("silkeste.remix.pogo");
		harmony.Patch(typeFromHandle.GetMethod("DashHits", bindingAttr), new HarmonyMethod(typeof(PogoRemix), "DashHitsPre"), new HarmonyMethod(typeof(PogoRemix), "DashHitsPost"));
		harmony.Patch(typeFromHandle.GetMethod("Damage", bindingAttr, null, new Type[1] { typeof(IHitResponder) }, null), null, new HarmonyMethod(typeof(PogoRemix), "DamagePost"));
		harmony.Patch(typeFromHandle.GetMethod("Pogo", bindingAttr), new HarmonyMethod(typeof(PogoRemix), "PogoPre"));
		base.Logger.LogInfo("Silkeste remix pogo: enemies x" + 0.5f + " and refill dash, bosses don't bounce");
		ReportTest((isBossLike != null) ? "pass pogo-rules-loaded" : "fail pogo-rules-loaded: boss check not found");
	}

	internal static void ReportTest(string line)
	{
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Melty", "test", "silkeste-remix-3.json");
			if (File.Exists(path))
			{
				Match match = Regex.Match(File.ReadAllText(path), "\"checks\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
				if (match.Success)
				{
					File.AppendAllText(Regex.Unescape(match.Groups[1].Value), line + "\n");
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static bool DashingDown(MadelineController m)
	{
		if (m.State == 2)
		{
			return m.DashDir.y > 0f;
		}
		return false;
	}

	private static void DashHitsPre(MadelineController __instance)
	{
		current = __instance;
		hitEnemy = (hitBoss = false);
	}

	private static void DashHitsPost()
	{
		current = null;
	}

	private static void DamagePost(IHitResponder r, ref int __result)
	{
		if (current != null && DashingDown(current) && r is HealthManager healthManager)
		{
			bool flag;
			try
			{
				flag = BossCard.InFight || (bool)isBossLike.Invoke(null, new object[1] { healthManager });
			}
			catch (Exception)
			{
				flag = false;
			}
			if (flag)
			{
				hitBoss = true;
				return;
			}
			hitEnemy = true;
			__result = Math.Max(1, (int)Math.Round((float)__result * 0.5f));
		}
	}

	private static bool PogoPre(GameObject spikes, ref bool refill)
	{
		if (current == null || spikes != null)
		{
			return true;
		}
		if (!hitEnemy)
		{
			return false;
		}
		refill = true;
		return true;
	}
}
