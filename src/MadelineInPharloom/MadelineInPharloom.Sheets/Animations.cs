namespace MadelineInPharloom.Sheets;

public static class Animations
{
	public static readonly AnimRow Idle = new AnimRow
	{
		Id = "idle",
		CelesteAnim = "idle",
		When = "on ground, |speed.x| <= 25, no input"
	};

	public static readonly AnimRow LookUp = new AnimRow
	{
		Id = "look_up",
		CelesteAnim = "lookUp",
		When = "idle and holding up"
	};

	public static readonly AnimRow RunSlow = new AnimRow
	{
		Id = "run_slow",
		CelesteAnim = "runSlow",
		When = "on ground moving, |speed.x| < 45"
	};

	public static readonly AnimRow RunFast = new AnimRow
	{
		Id = "run_fast",
		CelesteAnim = "runFast",
		When = "on ground moving, |speed.x| >= 45"
	};

	public static readonly AnimRow Push = new AnimRow
	{
		Id = "push",
		CelesteAnim = "push",
		When = "on ground pushing into a wall"
	};

	public static readonly AnimRow Skid = new AnimRow
	{
		Id = "skid",
		CelesteAnim = "skid",
		When = "on ground turning at > run_max"
	};

	public static readonly AnimRow Flip = new AnimRow
	{
		Id = "flip",
		CelesteAnim = "flip",
		When = "on ground turning at <= run_max"
	};

	public static readonly AnimRow Duck = new AnimRow
	{
		Id = "duck",
		CelesteAnim = "duck",
		When = "ducking (ground or ducked dash)"
	};

	public static readonly AnimRow Dash = new AnimRow
	{
		Id = "dash",
		CelesteAnim = "dash",
		When = "dash attacking, not ducking"
	};

	public static readonly AnimRow JumpSlow = new AnimRow
	{
		Id = "jump_slow",
		CelesteAnim = "jumpSlow",
		When = "airborne rising, |speed.x| <= run_max"
	};

	public static readonly AnimRow JumpFast = new AnimRow
	{
		Id = "jump_fast",
		CelesteAnim = "jumpFast",
		When = "airborne rising, |speed.x| > run_max"
	};

	public static readonly AnimRow FallSlow = new AnimRow
	{
		Id = "fall_slow",
		CelesteAnim = "fallSlow",
		When = "airborne falling"
	};

	public static readonly AnimRow FallFast = new AnimRow
	{
		Id = "fall_fast",
		CelesteAnim = "fallFast",
		When = "airborne falling >= max_fall or after fast jump"
	};

	public static readonly AnimRow WallSlide = new AnimRow
	{
		Id = "wall_slide",
		CelesteAnim = "wallslide",
		When = "climbing still or sliding down"
	};

	public static readonly AnimRow ClimbUp = new AnimRow
	{
		Id = "climb_up",
		CelesteAnim = "climbup",
		When = "climbing up"
	};

	public static readonly AnimRow BenchSit = new AnimRow
	{
		Id = "bench_sit",
		CelesteAnim = "sleep",
		When = "resting at a Silksong bench (PlayerData.atBench): Celeste's campfire sleep, so getting up can play wakeUp (user 2026-10-10)"
	};

	public static readonly AnimRow DeadSide = new AnimRow
	{
		Id = "dead_side",
		CelesteAnim = "deadside",
		When = "dying (Silksong death or hazard death)"
	};

	public static readonly AnimRow Dangling = new AnimRow
	{
		Id = "dangling",
		CelesteAnim = "dangling",
		When = "climbing with no wall below the feet"
	};

	public static readonly AnimRow BenchWake = new AnimRow
	{
		Id = "bench_wake",
		CelesteAnim = "wakeUp",
		When = "getting up from a bench: Celeste's campfire wake-up, until she moves"
	};

	public static readonly AnimRow IdleA = new AnimRow
	{
		Id = "idle_a",
		CelesteAnim = "idleA",
		When = "idle move (Player sprite OnLastFrame)"
	};

	public static readonly AnimRow IdleB = new AnimRow
	{
		Id = "idle_b",
		CelesteAnim = "idleB",
		When = "idle move"
	};

	public static readonly AnimRow IdleC = new AnimRow
	{
		Id = "idle_c",
		CelesteAnim = "idleC",
		When = "idle move"
	};

	public static readonly AnimRow BindStart = new AnimRow
	{
		Id = "bind_start",
		CelesteAnim = "startStarFly",
		When = "Hornet starts a bind (heal): Celeste's feather pickup, curling into a glowing star (goto starFly)"
	};

	public static readonly AnimRow BindHold = new AnimRow
	{
		Id = "bind_hold",
		CelesteAnim = "starFly",
		When = "the rest of the bind charge: Celeste's gold star"
	};

	public static readonly AnimRow GlideFall = new AnimRow
	{
		Id = "glide_fall",
		CelesteAnim = "fallSlow_carry",
		When = "gliding down with the jellyfish over her head"
	};

	public static readonly AnimRow GlideRise = new AnimRow
	{
		Id = "glide_rise",
		CelesteAnim = "jumpSlow_carry",
		When = "gliding up in an updraft"
	};

	public static readonly AnimRow CutAsleep = new AnimRow
	{
		Id = "cut_asleep",
		CelesteAnim = "asleep",
		When = "cutscene: Hornet lying down (Prostrate)"
	};

	public static readonly AnimRow CutWake = new AnimRow
	{
		Id = "cut_wake",
		CelesteAnim = "wakeUp",
		When = "cutscene: Hornet getting up off the floor (Prostrate Rise)"
	};

	public static readonly AnimRow CutPickup = new AnimRow
	{
		Id = "cut_pickup",
		CelesteAnim = "pickUp",
		When = "cutscene: Hornet collecting an item"
	};

	public static readonly AnimRow CutShaking = new AnimRow
	{
		Id = "cut_shaking",
		CelesteAnim = "shaking",
		When = "cutscene: a boss roar or a stun"
	};

	public static readonly AnimRow CutBound = new AnimRow
	{
		Id = "cut_bound",
		CelesteAnim = "tentacle_grabbed",
		When = "cutscene: Hornet bound in silk"
	};

	public static readonly AnimRow CutNeedolin = new AnimRow
	{
		Id = "cut_needolin",
		CelesteAnim = "sitDown",
		When = "Hornet playing the Needolin (user 2026-10-11: hug didn't look good, so sitDown; Celeste has no instrument pose)"
	};

	public static readonly AnimRow CutFloat = new AnimRow
	{
		Id = "cut_float",
		CelesteAnim = "spin",
		When = "cutscene: Hornet held in the air with gravity off (silk hearts and similar)"
	};

	public static readonly AnimRow[] All = new AnimRow[33]
	{
		Idle, LookUp, RunSlow, RunFast, Push, Skid, Flip, Duck, Dash, JumpSlow,
		JumpFast, FallSlow, FallFast, WallSlide, ClimbUp, BenchSit, DeadSide, Dangling, BenchWake, IdleA,
		IdleB, IdleC, BindStart, BindHold, GlideFall, GlideRise, CutAsleep, CutWake, CutPickup, CutShaking,
		CutBound, CutNeedolin, CutFloat
	};
}
