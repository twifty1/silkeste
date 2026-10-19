using System;
using InControl;
using UnityEngine;

namespace MadelineInPharloom;

public static class MenuMouse
{
	public static bool PadActive;

	public static Vector2 Gui()
	{
		Mouse current = Mouse.current;
		if (current == null)
		{
			return new Vector2(-1f, -1f);
		}
		float num = (float)Screen.height / 1080f;
		Vector2 vector = current.position.ReadValue();
		return new Vector2(vector.x / num, ((float)Screen.height - vector.y) / num);
	}

	public static bool Clicked(Rect r)
	{
		Mouse current = Mouse.current;
		if (current != null && current.leftButton.wasPressedThisFrame)
		{
			return r.Contains(Gui());
		}
		return false;
	}

	public static float Wheel()
	{
		return Mouse.current?.scroll.ReadValue().y ?? 0f;
	}

	public static bool SharePressed()
	{
		try
		{
			InControl.InputDevice activeDevice = InputManager.ActiveDevice;
			if (activeDevice == null || activeDevice == InControl.InputDevice.Null)
			{
				return false;
			}
			InputControlType[] array = new InputControlType[2]
			{
				InputControlType.Share,
				InputControlType.Create
			};
			foreach (InputControlType controlType in array)
			{
				InControl.InputControl control = activeDevice.GetControl(controlType);
				if (control != null && control != InControl.InputControl.Null && control.WasPressed)
				{
					return true;
				}
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	public static bool PadHints(bool was)
	{
		try
		{
			InControl.InputDevice activeDevice = InputManager.ActiveDevice;
			if (activeDevice != null && activeDevice != InControl.InputDevice.Null && (activeDevice.AnyButtonWasPressed || activeDevice.LeftStick.Vector.sqrMagnitude > 0.25f || activeDevice.DPad.Vector.sqrMagnitude > 0.25f))
			{
				was = true;
			}
		}
		catch (Exception)
		{
		}
		Keyboard current = Keyboard.current;
		if (current != null && current.anyKey.wasPressedThisFrame)
		{
			was = false;
		}
		Mouse current2 = Mouse.current;
		if (current2 != null && (current2.leftButton.wasPressedThisFrame || current2.delta.ReadValue().sqrMagnitude > 4f))
		{
			was = false;
		}
		return was;
	}
}
