using MadelineInPharloom.Sheets;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MadelineInPharloom;

/// <summary>
/// The pause menu's Difficulty (user 2026-10-19): a row between Settings and Quit that changes the save's difficulty
/// with Left/Right (or a press, or a click on its arrows). Up/Down move on as usual.
/// </summary>
public class DifficultyOption : Selectable, ISubmitHandler
{
	public override void OnMove(AxisEventData eventData)
	{
		if (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right)
		{
			Change(eventData.moveDir == MoveDirection.Left ? -1 : 1);
			eventData.Use();
			return;
		}
		base.OnMove(eventData);
	}

	public void OnSubmit(BaseEventData eventData)
	{
		Change(1);
	}

	private static void Change(int dir)
	{
		AssistMode.Cycle(AssistOptions.Difficulty, dir);
		Sfx.Play(dir < 0 ? Sounds.MenuRolloverUp.Id : Sounds.MenuRolloverDown.Id);
		Plugin.Log.LogInfo("Pause: difficulty " + AssistMode.ValueOf(AssistOptions.Difficulty));
	}
}

/// <summary>The pause menu's Leave Tutorial (user 2026-10-20): asks first, with the moves not learned yet.</summary>
public class LeaveTutorialOption : Selectable, ISubmitHandler
{
	public void OnSubmit(BaseEventData eventData)
	{
		TechRoom.AskLeave();
	}
}
