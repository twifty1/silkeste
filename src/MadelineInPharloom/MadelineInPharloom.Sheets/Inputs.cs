namespace MadelineInPharloom.Sheets;

public static class Inputs
{
	public static readonly InputRow Move = new InputRow
	{
		Id = "move",
		Celeste = "MoveX / MoveY / Aim",
		SilksongAction = "MoveVector",
		Read = "vector, 8-way snapped like Celeste"
	};

	public static readonly InputRow Jump = new InputRow
	{
		Id = "jump",
		Celeste = "Jump",
		SilksongAction = "Jump",
		Read = "pressed + held"
	};

	public static readonly InputRow Dash = new InputRow
	{
		Id = "dash",
		Celeste = "Dash",
		SilksongAction = "Attack",
		Read = "pressed"
	};

	public static readonly InputRow SkinSwitch = new InputRow
	{
		Id = "skin_switch",
		Celeste = "(none: Celeste picks the skin per chapter)",
		SilksongAction = "key:F2",
		Read = "pressed, anywhere in gameplay (default; changeable: assist.key_skin)"
	};

	public static readonly InputRow Grab = new InputRow
	{
		Id = "grab",
		Celeste = "Grab",
		SilksongAction = "Dash",
		Read = "held"
	};

	public static readonly InputRow Help = new InputRow
	{
		Id = "help",
		Celeste = "(none)",
		SilksongAction = "key:F4",
		Read = "pressed, anywhere in gameplay (default; changeable: assist.key_help)"
	};

	public static readonly InputRow AssistMenu = new InputRow
	{
		Id = "assist_menu",
		Celeste = "Pause > Assist Mode",
		SilksongAction = "key:F1",
		Read = "pressed in gameplay; on a controller hold Taunt (R3) for visuals.assist_hold_time (default; changeable: assist.key_settings)"
	};

	public static readonly InputRow SpeedrunTimer = new InputRow
	{
		Id = "speedrun_timer",
		Celeste = "Options > Speedrun Clock",
		SilksongAction = "key:F3",
		Read = "pressed, anywhere in gameplay (default; changeable: assist.key_timer)"
	};
}
