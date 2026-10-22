using System;
using HarmonyLib;
using InControl;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Closing one of Silkeste's menus with Circle on a controller also reached Hornet's Bind and healed (spending silk).
/// While a menu is open, and after it closes until the button is let go, the game's bind and silk skill listeners don't
/// hear that button (user 2026-10-22).
/// </summary>
public static class MenuInputGuard
{
	private static bool castLatch;

	private static bool quickLatch;

	private static bool MenusOpen
	{
		get
		{
			GameManager gm = GameManager.SilentInstance;
			return AssistMode.IsOpen || BossCard.RecordsOpen || EmoteMenu.IsOpen || StartingAbilities.IsOpen || TechRoom.InputLocked || (gm != null && gm.isPaused);
		}
	}

	public static void Patch(Harmony h)
	{
		HarmonyMethod cast = new HarmonyMethod(typeof(MenuInputGuard), "CastPrefix");
		HarmonyMethod quick = new HarmonyMethod(typeof(MenuInputGuard), "QuickPrefix");
		h.Patch(AccessTools.Method(typeof(HutongGames.PlayMaker.Actions.ListenForCast), "CheckForInput"), cast);
		h.Patch(AccessTools.Method(typeof(HutongGames.PlayMaker.Actions.ListenForQuickCast), "OnUpdate"), quick);
		h.Patch(AccessTools.Method(typeof(HutongGames.PlayMaker.Actions.ListenForQuickCastV2), "OnUpdate"), quick);
		h.Patch(AccessTools.Method(typeof(HutongGames.PlayMaker.Actions.ListenForSilkSpecial), "CheckForInput"), quick);
		h.Patch(AccessTools.Method(typeof(HutongGames.PlayMaker.Actions.ListenForSilkSpecialV2), "CheckForInput"), quick);
	}

	private static bool Held(ref bool latch, PlayerAction button)
	{
		if (MenusOpen)
		{
			latch = true;
			return true;
		}
		if (latch)
		{
			if (button != null && button.IsPressed)
			{
				return true;
			}
			latch = false;
		}
		return false;
	}

	private static bool CastPrefix()
	{
		HeroActions a = ManagerSingleton<InputHandler>.Instance?.inputActions;
		return a == null || !Held(ref castLatch, a.Cast);
	}

	private static bool QuickPrefix()
	{
		HeroActions a = ManagerSingleton<InputHandler>.Instance?.inputActions;
		return a == null || !Held(ref quickLatch, a.QuickCast);
	}
}
