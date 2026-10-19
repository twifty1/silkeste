namespace MadelineInPharloom.Sheets;

public static class Transitions
{
	public static readonly TransitionRow Side = new TransitionRow
	{
		Id = "side",
		EntryGates = "left,right",
		SpeedX = "keep",
		SpeedY = "keep",
		State = "keep",
		SpeedYValue = 0f,
		VarJumpTime = 0f,
		DashCooldown = 0f,
		AutoJump = false,
		Refill = true
	};

	public static readonly TransitionRow Down = new TransitionRow
	{
		Id = "down",
		EntryGates = "top",
		SpeedX = "keep",
		SpeedY = "max0",
		State = "normal",
		SpeedYValue = 0f,
		VarJumpTime = 0f,
		DashCooldown = 0f,
		AutoJump = false,
		Refill = true
	};

	public static readonly TransitionRow Up = new TransitionRow
	{
		Id = "up",
		EntryGates = "bot",
		SpeedX = "zero",
		SpeedY = "set",
		State = "normal",
		SpeedYValue = -105f,
		VarJumpTime = 0.2f,
		DashCooldown = 0.2f,
		AutoJump = true,
		Refill = true
	};

	public static readonly TransitionRow[] All = new TransitionRow[3] { Side, Down, Up };
}
