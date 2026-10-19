namespace MadelineInPharloom.Sheets;

public static class Tech
{
	public static readonly TechRow Super = new TechRow
	{
		Id = "super",
		Trigger = "jump during horizontal ground dash",
		Mode = "set",
		Sfx = "jump_super",
		SpeedX = 260f,
		SpeedY = -105f,
		XMult = 1f,
		YMult = 1f,
		VarJumpTime = 0.2f,
		StaminaCost = 0f
	};

	public static readonly TechRow Hyper = new TechRow
	{
		Id = "hyper",
		Trigger = "jump during ducking ground dash (down-diagonal dash on ground)",
		Mode = "set",
		Sfx = "jump_superslide",
		SpeedX = 260f,
		SpeedY = -105f,
		XMult = 1.25f,
		YMult = 0.5f,
		VarJumpTime = 0.2f,
		StaminaCost = 0f
	};

	public static readonly TechRow Wavedash = new TechRow
	{
		Id = "wavedash",
		Trigger = "diagonal-down air dash lands, then jump while still dashing",
		Mode = "set",
		Sfx = "jump_superslide",
		SpeedX = 260f,
		SpeedY = -105f,
		XMult = 1.25f,
		YMult = 0.5f,
		VarJumpTime = 0.2f,
		StaminaCost = 0f
	};

	public static readonly TechRow Ultra = new TechRow
	{
		Id = "ultra",
		Trigger = "diagonal-down dash touches ground",
		Mode = "scale",
		Sfx = "none",
		SpeedX = 0f,
		SpeedY = 0f,
		XMult = 1.2f,
		YMult = 0f,
		VarJumpTime = 0f,
		StaminaCost = 0f
	};

	public static readonly TechRow WallBounce = new TechRow
	{
		Id = "wall_bounce",
		Trigger = "jump near wall during up dash (dash attacking)",
		Mode = "set",
		Sfx = "jump_superwall",
		SpeedX = 170f,
		SpeedY = -160f,
		XMult = 1f,
		YMult = 1f,
		VarJumpTime = 0.25f,
		StaminaCost = 0f
	};

	public static readonly TechRow WallJump = new TechRow
	{
		Id = "wall_jump",
		Trigger = "jump next to wall (not grabbing)",
		Mode = "set",
		Sfx = "jump_wall",
		SpeedX = 130f,
		SpeedY = -105f,
		XMult = 1f,
		YMult = 1f,
		VarJumpTime = 0.2f,
		StaminaCost = 0f
	};

	public static readonly TechRow ClimbJump = new TechRow
	{
		Id = "climb_jump",
		Trigger = "jump while grabbing a wall",
		Mode = "jump",
		Sfx = "jump_climb",
		SpeedX = 40f,
		SpeedY = -105f,
		XMult = 1f,
		YMult = 1f,
		VarJumpTime = 0.2f,
		StaminaCost = 27.5f
	};
}
