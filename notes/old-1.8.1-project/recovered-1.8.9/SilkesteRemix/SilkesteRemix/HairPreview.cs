using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MadelineInPharloom;
using MadelineInPharloom.Sheets;

namespace SilkesteRemix;

internal static class HairPreview
{
	public static void Patch(Harmony h)
	{
		h.Patch(AccessTools.Method(typeof(AssistMode), "DrawPreview"), null, null, new HarmonyMethod(typeof(HairPreview), "Swap"));
	}

	private static IEnumerable<CodeInstruction> Swap(IEnumerable<CodeInstruction> code)
	{
		MethodInfo hairHex = AccessTools.Method(typeof(AssistMode), "HairHex");
		MethodInfo mine = AccessTools.Method(typeof(HairPreview), "PreviewHex");
		foreach (CodeInstruction item in code)
		{
			if (item.Calls(hairHex))
			{
				yield return new CodeInstruction(OpCodes.Call, mine).MoveLabelsFrom(item).MoveBlocksFrom(item);
			}
			else
			{
				yield return item;
			}
		}
	}

	public static string PreviewHex(AssistRow row, string skinHex)
	{
		try
		{
			AssistMode instance = AssistMode.Instance;
			AssistRow[] array = SpeedrunPlus.AllRows();
			if (instance != null && array != null && instance.selected >= 0 && instance.selected < array.Length)
			{
				string id = array[instance.selected].Id;
				SkinRow skinRow = Array.Find(Skins.All, (SkinRow k) => k.Name == AssistMode.SkinName);
				if (skinRow.Id != null)
				{
					if (id == AssistOptions.HairTwo.Id)
					{
						return AssistMode.HairHex(AssistOptions.HairTwo, skinRow.HairTwoDashes);
					}
					if (id == AssistOptions.HairZero.Id)
					{
						return AssistMode.HairHex(AssistOptions.HairZero, skinRow.HairUsed);
					}
				}
			}
		}
		catch (Exception)
		{
		}
		return AssistMode.HairHex(row, skinHex);
	}
}
