using System;
using HarmonyLib;
using MadelineInPharloom;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace SilkesteRemix;

internal static class DialogueSkip
{
	public static readonly AssistRow Row = new AssistRow
	{
		Id = "sr_dialogue",
		Section = "Speedrun Timer",
		Label = "Auto-Skip Dialogue",
		Values = "Off,Instant,Quick Read",
		Default = "Off",
		Description = "Instant: every line of dialogue prints and turns the page on its own at once. Quick Read: same, but each page stays up for a moment so you can read it. Choices still wait for you."
	};

	private const float ReadPause = 0.7f;

	private static float waitingSince = -1f;

	public static void Patch(Harmony h)
	{
		h.Patch(AccessTools.Method(typeof(DialogueBox), "Update"), null, new HarmonyMethod(typeof(DialogueSkip), "Post"));
	}

	private static void Post(DialogueBox __instance)
	{
		string text;
		try
		{
			text = AssistMode.Value(Row);
		}
		catch (Exception)
		{
			return;
		}
		if (text == "Off" || !__instance.isDialogueRunning)
		{
			waitingSince = -1f;
			return;
		}
		if (__instance.isPrintingText)
		{
			__instance.currentRevealSpeed = 100000f;
		}
		if (!__instance.waitingToAdvance)
		{
			waitingSince = -1f;
			return;
		}
		float unscaledTime = Time.unscaledTime;
		if (waitingSince < 0f)
		{
			waitingSince = unscaledTime;
		}
		if (text == "Instant" || unscaledTime - waitingSince >= 0.7f)
		{
			__instance.AdvanceConversation();
			waitingSince = -1f;
		}
	}
}
