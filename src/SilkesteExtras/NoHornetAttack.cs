using HarmonyLib;
using MadelineInPharloom;

namespace SilkesteExtras;

[HarmonyPatch(typeof(HeroController), "CanAttack")]
internal static class NoHornetAttack
{
	private static bool Prefix(ref bool __result)
	{
		if (!MadelineInPharloom.Plugin.Ready)
		{
			return true;
		}
		__result = false;
		return false;
	}
}
