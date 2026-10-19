using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MadelineInPharloom;

// Keyboard and mouse for the mod's own screens. Silksong builds since its first patches ship
// Unity's Input System; the launch build doesn't, and any method touching it fails to load there.
// So only NewInput below names the Input System, and it's used only once a probe shows it's there;
// otherwise the same keys are read through Unity's classic input. The names mirror the Input
// System's (Keyboard.current.wKey.wasPressedThisFrame...), so the screens read the same either way.
internal static class InputCompat
{
	private static int state;

	public static bool New
	{
		get
		{
			if (state == 0)
			{
				Detect();
			}
			return state == 1;
		}
	}

	private static void Detect()
	{
		try
		{
			NewInput.Probe();
			state = 1;
		}
		catch (Exception ex)
		{
			state = 2;
			Plugin.Log?.LogInfo("Input: this Silksong has no Unity Input System (" + ex.GetType().Name + "); keyboard and mouse use Unity's classic input");
		}
	}

	private static readonly Dictionary<string, KeyCode> codes = new Dictionary<string, KeyCode>();

	public static KeyCode Code(string name)
	{
		if (codes.TryGetValue(name, out var value))
		{
			return value;
		}
		string text = name;
		if (text.StartsWith("Digit"))
		{
			text = "Alpha" + text.Substring(5);
		}
		else if (text.StartsWith("Numpad"))
		{
			text = "Keypad" + text.Substring(6);
		}
		else
		{
			switch (text)
			{
			case "Enter":
				text = "Return";
				break;
			case "LeftCtrl":
				text = "LeftControl";
				break;
			case "RightCtrl":
				text = "RightControl";
				break;
			case "Backquote":
				text = "BackQuote";
				break;
			case "LeftMeta":
				text = "LeftWindows";
				break;
			case "RightMeta":
				text = "RightWindows";
				break;
			case "ContextMenu":
				text = "Menu";
				break;
			}
		}
		if (!Enum.TryParse<KeyCode>(text, ignoreCase: true, out value))
		{
			value = KeyCode.None;
		}
		codes[name] = value;
		return value;
	}

	public static bool LegacyDown(KeyCode k)
	{
		try
		{
			return k != KeyCode.None && UnityEngine.Input.GetKeyDown(k);
		}
		catch (Exception)
		{
			return false;
		}
	}

	public static bool LegacyHeld(KeyCode k)
	{
		try
		{
			return k != KeyCode.None && UnityEngine.Input.GetKey(k);
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static int deltaFrame = -1;

	private static Vector2 lastPos;

	private static Vector2 delta;

	public static Vector2 LegacyPosition()
	{
		try
		{
			return UnityEngine.Input.mousePosition;
		}
		catch (Exception)
		{
			return Vector2.zero;
		}
	}

	public static Vector2 LegacyDelta()
	{
		if (deltaFrame != Time.frameCount)
		{
			Vector2 vector = LegacyPosition();
			delta = ((deltaFrame < 0) ? Vector2.zero : (vector - lastPos));
			lastPos = vector;
			deltaFrame = Time.frameCount;
		}
		return delta;
	}

	public static Vector2 LegacyScroll()
	{
		try
		{
			// The Input System reports a wheel notch as 120, classic input as 1.
			return UnityEngine.Input.mouseScrollDelta * 120f;
		}
		catch (Exception)
		{
			return Vector2.zero;
		}
	}
}

// Everything that names Unity's Input System lives here.
internal static class NewInput
{
	// No Input System type in any field or signature here, so this class itself always loads.
	private static readonly Dictionary<string, int> keys = new Dictionary<string, int>();

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void Probe()
	{
		_ = UnityEngine.InputSystem.Keyboard.current;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static bool HasKeyboard()
	{
		return UnityEngine.InputSystem.Keyboard.current != null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static bool HasMouse()
	{
		return UnityEngine.InputSystem.Mouse.current != null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static bool Down(string name)
	{
		return Button(name, held: false);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static bool Held(string name)
	{
		return Button(name, held: true);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool Button(string name, bool held)
	{
		UnityEngine.InputSystem.Controls.ButtonControl b = Control(name);
		if (b == null)
		{
			return false;
		}
		return held ? b.isPressed : b.wasPressedThisFrame;
	}

	private static UnityEngine.InputSystem.Controls.ButtonControl Control(string name)
	{
		if (name == "anyKey")
		{
			return UnityEngine.InputSystem.Keyboard.current?.anyKey;
		}
		if (name == "leftButton")
		{
			return UnityEngine.InputSystem.Mouse.current?.leftButton;
		}
		if (name == "rightButton")
		{
			return UnityEngine.InputSystem.Mouse.current?.rightButton;
		}
		UnityEngine.InputSystem.Keyboard current = UnityEngine.InputSystem.Keyboard.current;
		if (current == null)
		{
			return null;
		}
		if (!keys.TryGetValue(name, out var value))
		{
			value = (Enum.TryParse<UnityEngine.InputSystem.Key>(name, out var key) ? ((int)key) : 0);
			keys[name] = value;
		}
		if (value == 0)
		{
			return null;
		}
		return current[(UnityEngine.InputSystem.Key)value];
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Vector2 Read(string name)
	{
		UnityEngine.InputSystem.Mouse current = UnityEngine.InputSystem.Mouse.current;
		if (current == null)
		{
			return Vector2.zero;
		}
		return name switch
		{
			"position" => current.position.ReadValue(),
			"delta" => current.delta.ReadValue(),
			"scroll" => current.scroll.ReadValue(),
			_ => Vector2.zero,
		};
	}
}

public sealed class KeyState
{
	private readonly string name;

	private readonly KeyCode legacy;

	internal KeyState(string name, KeyCode legacy)
	{
		this.name = name;
		this.legacy = legacy;
	}

	public bool wasPressedThisFrame
	{
		get
		{
			if (InputCompat.New)
			{
				return NewInput.Down(name);
			}
			if (name == "anyKey")
			{
				try
				{
					return UnityEngine.Input.anyKeyDown && !UnityEngine.Input.GetMouseButtonDown(0) && !UnityEngine.Input.GetMouseButtonDown(1) && !UnityEngine.Input.GetMouseButtonDown(2);
				}
				catch (Exception)
				{
					return false;
				}
			}
			return InputCompat.LegacyDown(legacy);
		}
	}

	public bool isPressed
	{
		get
		{
			if (InputCompat.New)
			{
				return NewInput.Held(name);
			}
			if (name == "anyKey")
			{
				try
				{
					return UnityEngine.Input.anyKey && !UnityEngine.Input.GetMouseButton(0) && !UnityEngine.Input.GetMouseButton(1) && !UnityEngine.Input.GetMouseButton(2);
				}
				catch (Exception)
				{
					return false;
				}
			}
			return InputCompat.LegacyHeld(legacy);
		}
	}
}

public sealed class PointerValue
{
	private readonly string name;

	internal PointerValue(string name)
	{
		this.name = name;
	}

	public Vector2 ReadValue()
	{
		if (InputCompat.New)
		{
			return NewInput.Read(name);
		}
		return name switch
		{
			"position" => InputCompat.LegacyPosition(),
			"delta" => InputCompat.LegacyDelta(),
			"scroll" => InputCompat.LegacyScroll(),
			_ => Vector2.zero,
		};
	}
}

public sealed class Keyboard
{
	private static readonly Keyboard instance = new Keyboard();

	private readonly Dictionary<string, KeyState> named = new Dictionary<string, KeyState>();

	public static Keyboard current
	{
		get
		{
			if (InputCompat.New && !NewInput.HasKeyboard())
			{
				return null;
			}
			return instance;
		}
	}

	// A key by its Input System name ("F1", "Tab", "Digit1"...); null for a name neither knows.
	public KeyState this[string key]
	{
		get
		{
			if (string.IsNullOrEmpty(key))
			{
				return null;
			}
			if (!named.TryGetValue(key, out var value))
			{
				KeyCode keyCode = InputCompat.Code(key);
				value = new KeyState(key, keyCode);
				named[key] = value;
			}
			return value;
		}
	}

	public readonly KeyState anyKey = new KeyState("anyKey", KeyCode.None);

	public readonly KeyState upArrowKey = new KeyState("UpArrow", KeyCode.UpArrow);

	public readonly KeyState downArrowKey = new KeyState("DownArrow", KeyCode.DownArrow);

	public readonly KeyState leftArrowKey = new KeyState("LeftArrow", KeyCode.LeftArrow);

	public readonly KeyState rightArrowKey = new KeyState("RightArrow", KeyCode.RightArrow);

	public readonly KeyState wKey = new KeyState("W", KeyCode.W);

	public readonly KeyState aKey = new KeyState("A", KeyCode.A);

	public readonly KeyState sKey = new KeyState("S", KeyCode.S);

	public readonly KeyState dKey = new KeyState("D", KeyCode.D);

	public readonly KeyState eKey = new KeyState("E", KeyCode.E);

	public readonly KeyState qKey = new KeyState("Q", KeyCode.Q);

	public readonly KeyState rKey = new KeyState("R", KeyCode.R);

	public readonly KeyState tabKey = new KeyState("Tab", KeyCode.Tab);

	public readonly KeyState enterKey = new KeyState("Enter", KeyCode.Return);

	public readonly KeyState spaceKey = new KeyState("Space", KeyCode.Space);

	public readonly KeyState escapeKey = new KeyState("Escape", KeyCode.Escape);

	public readonly KeyState backspaceKey = new KeyState("Backspace", KeyCode.Backspace);

	public readonly KeyState deleteKey = new KeyState("Delete", KeyCode.Delete);
}

public sealed class Mouse
{
	private static readonly Mouse instance = new Mouse();

	public static Mouse current
	{
		get
		{
			if (InputCompat.New && !NewInput.HasMouse())
			{
				return null;
			}
			return instance;
		}
	}

	public readonly KeyState leftButton = new KeyState("leftButton", KeyCode.Mouse0);

	public readonly KeyState rightButton = new KeyState("rightButton", KeyCode.Mouse1);

	public readonly PointerValue position = new PointerValue("position");

	public readonly PointerValue delta = new PointerValue("delta");

	public readonly PointerValue scroll = new PointerValue("scroll");
}
