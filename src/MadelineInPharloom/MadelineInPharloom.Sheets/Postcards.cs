namespace MadelineInPharloom.Sheets;

public static class Postcards
{
	// the Tutorial card (shown on a new save and on the help key), rewritten 2026-10-17 to guide a first run
	private const string TutorialText = "Welcome to Pharloom, Madeline!\n- Attack is your Dash: aim it any of 8 ways.\n- Dashing hits enemies, and nothing hits you.\n- Touch the ground to get your dash back.\n- Cling Grip: climb and wall jump.\n- Faydown Cloak: a second dash.\n- Try supers, hypers and wavedashes.\n- {key:key_records} Records: bosses, rematches, photos\n- {key:key_settings} Settings and keys\n- A controller is recommended. Have fun!";

	public static readonly PostcardRow Intro = new PostcardRow
	{
		Id = "intro",
		Title = "Tutorial",
		Body = TutorialText,
		Unlock = "none"
	};

	public static readonly PostcardRow GrabClimb = new PostcardRow
	{
		Id = "grab_climb",
		Title = "Cling Grip",
		Body = "Madeline can now grab walls, climb and wall jump.\n\nHold the Dash button against a wall to hold on, and move up or down to climb.\nClimbing tires her out: she flashes red just before she slips.\nJump next to a wall to wall jump,\nor while holding on for a climb jump.",
		Unlock = "grab_climb"
	};

	public static readonly PostcardRow SecondDash = new PostcardRow
	{
		Id = "second_dash",
		Title = "Faydown Cloak",
		Body = "Madeline can now dash twice before landing.\n\nHer hair turns pink while she still has both dashes.\n\nA hit with one dash leaves the other free,\nbut two hits in a row lock your dash\ntwice as long.",
		Unlock = "second_dash"
	};

	public static readonly PostcardRow Help = new PostcardRow
	{
		Id = "help",
		Title = "Tutorial",
		Body = TutorialText,
		Unlock = "none"
	};

	public static readonly PostcardRow SwiftStep = new PostcardRow
	{
		Id = "swift_step",
		Title = "Swift Step",
		Body = "Madeline could already dash,\nso Swift Step gives no new move.\n\nInstead, it makes her dash stronger:\n- Dashes go 25% further.\n- The lock after hitting an enemy\n  is 25% shorter.",
		Unlock = "swift_step"
	};

	public static readonly PostcardRow Glide = new PostcardRow
	{
		Id = "glide",
		Title = "Drifter's Cloak",
		Body = "Madeline can now glide.\n\nHold Jump while falling to glide with Celeste's jellyfish.\nGlide into updrafts to ride them up.",
		Unlock = "glide"
	};

	public static readonly PostcardRow[] All = new PostcardRow[6] { Intro, GrabClimb, SecondDash, Help, SwiftStep, Glide };
}
