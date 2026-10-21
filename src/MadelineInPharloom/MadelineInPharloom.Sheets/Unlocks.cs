namespace MadelineInPharloom.Sheets;

public static class Unlocks
{
	public static readonly UnlockRow GrabClimb = new UnlockRow
	{
		Id = "grab_climb",
		SilksongUnlock = "Cling Grip",
		PlayerData = "hasWalljump",
		Gives = "Celeste grab, climb, climb jump and wall jumps (wall bounces too; user 2026-10-12: wall jump comes with climbing)"
	};

	public static readonly UnlockRow SecondDash = new UnlockRow
	{
		Id = "second_dash",
		SilksongUnlock = "Faydown Cloak",
		PlayerData = "hasDoubleJump",
		Gives = "a double jump, with the Faydown Cloak's silk wings"
	};

	public static readonly UnlockRow SwiftStep = new UnlockRow
	{
		Id = "swift_step",
		SilksongUnlock = "Swift Step",
		PlayerData = "hasDash",
		Gives = "a faster, longer dash: the dash lasts 1.25x as long (movement.swift_step_dash_time_mult) and the lock after an enemy hit is 0.3 s instead of 0.4 s (dash_attack.swift_step_hit_cooldown); she dashes from the start without it"
	};

	public static readonly UnlockRow Glide = new UnlockRow
	{
		Id = "glide",
		SilksongUnlock = "Drifter's Cloak",
		PlayerData = "hasBrolly",
		Gives = "Celeste's jellyfish glide: hold Jump while falling or in an updraft; updrafts lift her"
	};

	public static readonly UnlockRow[] All = new UnlockRow[4] { GrabClimb, SecondDash, SwiftStep, Glide };
}
