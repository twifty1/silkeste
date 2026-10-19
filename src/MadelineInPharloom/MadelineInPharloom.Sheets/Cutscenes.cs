namespace MadelineInPharloom.Sheets;

public static class Cutscenes
{
	public static readonly CutsceneRow ProstrateRise = new CutsceneRow
	{
		Id = "prostrate_rise",
		HornetClip = "Prostrate Rise",
		CelesteAnim = "wakeUp"
	};

	public static readonly CutsceneRow Prostrate = new CutsceneRow
	{
		Id = "prostrate",
		HornetClip = "Prostrate",
		CelesteAnim = "asleep"
	};

	public static readonly CutsceneRow Collect = new CutsceneRow
	{
		Id = "collect",
		HornetClip = "Collect",
		CelesteAnim = "pickUp"
	};

	public static readonly CutsceneRow GetItem = new CutsceneRow
	{
		Id = "get_item",
		HornetClip = "Get Item",
		CelesteAnim = "pickUp"
	};

	public static readonly CutsceneRow LookUp = new CutsceneRow
	{
		Id = "look_up",
		HornetClip = "Look Up",
		CelesteAnim = "lookUp"
	};

	public static readonly CutsceneRow Lookup = new CutsceneRow
	{
		Id = "lookup",
		HornetClip = "LookUp",
		CelesteAnim = "lookUp"
	};

	public static readonly CutsceneRow Roar = new CutsceneRow
	{
		Id = "roar",
		HornetClip = "Roar",
		CelesteAnim = "shaking"
	};

	public static readonly CutsceneRow Stun = new CutsceneRow
	{
		Id = "stun",
		HornetClip = "Stun",
		CelesteAnim = "shaking"
	};

	public static readonly CutsceneRow Bound = new CutsceneRow
	{
		Id = "bound",
		HornetClip = "Bound",
		CelesteAnim = "tentacle_grabbed"
	};

	public static readonly CutsceneRow IdleBound = new CutsceneRow
	{
		Id = "idle_bound",
		HornetClip = "Idle Bound",
		CelesteAnim = "tentacle_grabbed"
	};

	public static readonly CutsceneRow Needolin = new CutsceneRow
	{
		Id = "needolin",
		HornetClip = "Needolin",
		CelesteAnim = "sitDown"
	};

	public static readonly CutsceneRow CurseBound = new CutsceneRow
	{
		Id = "curse_bound",
		HornetClip = "Curse Bound",
		CelesteAnim = "tentacle_grabbed"
	};

	public static readonly CutsceneRow[] All = new CutsceneRow[12]
	{
		ProstrateRise, Prostrate, Collect, GetItem, LookUp, Lookup, Roar, Stun, Bound, IdleBound,
		Needolin, CurseBound
	};
}
