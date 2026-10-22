namespace MadelineInPharloom.Sheets;

public static class AssistOptions
{
	public static readonly AssistRow GameSpeed = new AssistRow
	{
		Id = "game_speed",
		Section = "Assist Mode",
		Label = "Game Speed",
		Values = "50%,60%,70%,80%,90%,100%",
		Default = "100%",
		Description = "Slows the whole game down, enemies and bosses too."
	};

	public static readonly AssistRow InfiniteStamina = new AssistRow
	{
		Id = "infinite_stamina",
		Section = "Assist Mode",
		Label = "Infinite Stamina",
		Values = "Off,On",
		Default = "Off",
		Description = "Climb and hold onto walls for as long as you like."
	};

	public static readonly AssistRow AirDashes = new AssistRow
	{
		Id = "air_dashes",
		Section = "Assist Mode",
		Label = "Air Dashes",
		Values = "Default,Two,Infinite",
		Default = "Default",
		Description = "How many times Madeline can dash before landing."
	};

	public static readonly AssistRow DashAssist = new AssistRow
	{
		Id = "dash_assist",
		Section = "Assist Mode",
		Label = "Dash Assist",
		Values = "Off,On",
		Default = "Off",
		Description = "Time stops while you hold dash, and an arrow shows where you'll go."
	};

	public static readonly AssistRow Invincibility = new AssistRow
	{
		Id = "invincibility",
		Section = "Assist Mode",
		Label = "Invincibility",
		Values = "Off,On",
		Default = "Off",
		Description = "Enemies, spikes and hazards can't hurt you. Bottomless pits still send you back."
	};

	public static readonly AssistRow ForgivingJumps = new AssistRow
	{
		Id = "forgiving_jumps",
		Section = "Assist Mode",
		Label = "Forgiving Jumps",
		Values = "Off,On",
		Default = "Off",
		Description = "A little longer to jump after leaving a ledge, and presses count a bit earlier."
	};

	public static readonly AssistRow JumpCount = new AssistRow
	{
		Id = "jump_count",
		Section = "Assist Mode",
		Label = "Jump Count",
		Values = "1,2,3,Infinite",
		Default = "1",
		Description = "How many times she can jump before landing again: 1 is normal."
	};

	public static readonly AssistRow DashLength = new AssistRow
	{
		Id = "dash_length",
		Section = "Assist Mode",
		Label = "Dash Length",
		Values = "Short,Normal,Long,Very Long",
		Default = "Normal",
		Description = "How far each dash goes: Short, Normal, Long or Very Long."
	};

	public static readonly AssistRow DashCount = new AssistRow
	{
		Id = "dash_count",
		Section = "Assist Mode",
		Label = "Dash Count",
		Values = "Default,0,1,2,3,Infinite",
		Default = "Default",
		Description = "How many dashes she gets before landing. 0 means no dashing at all (and no attacking)."
	};

	public static readonly AssistRow MirrorMode = new AssistRow
	{
		Id = "mirror_mode",
		Section = "Variants",
		Label = "Mirror Mode",
		Values = "Off,On",
		Default = "Off",
		Description = "The whole game is shown flipped left to right, and left and right on the controls flip with it."
	};

	public static readonly AssistRow LowFriction = new AssistRow
	{
		Id = "low_friction",
		Section = "Variants",
		Label = "Friction",
		Values = "Normal,Low,Very Low,Ice",
		Default = "Normal",
		Description = "How slippery floors and the air are. Lower slides further, crouching too."
	};

	public static readonly AssistRow SuperDashing = new AssistRow
	{
		Id = "super_dashing",
		Section = "Variants",
		Label = "Super Dashing",
		Values = "Off,On",
		Default = "Off",
		Description = "Hold a direction during a dash to steer it as it goes."
	};

	public static readonly AssistRow InvisibleMotion = new AssistRow
	{
		Id = "invisible_motion",
		Section = "Variants",
		Label = "Invisible Motion",
		Values = "Off,On",
		Default = "Off",
		Description = "Madeline vanishes whenever she moves and only shows when she stands still."
	};

	public static readonly AssistRow Gravity = new AssistRow
	{
		Id = "gravity",
		Section = "Variants",
		Label = "Gravity",
		Values = "Low,Normal,High",
		Default = "Normal",
		Description = "Low makes floaty moon jumps; High makes her fall fast and jumps short and snappy."
	};

	public static readonly AssistRow Wind = new AssistRow
	{
		Id = "wind",
		Section = "Variants",
		Label = "Wind",
		Values = "Off,Left,Right,Strong Left,Strong Right",
		Default = "Off",
		Description = "A constant wind pushes Madeline that way, on the ground and in the air."
	};

	public static readonly AssistRow MadelineSpeed = new AssistRow
	{
		Id = "madeline_speed",
		Section = "Variants",
		Label = "Madeline Speed",
		Values = "Normal,125%,150%",
		Default = "Normal",
		Description = "Madeline runs, jumps and dashes faster while the rest of the world keeps its speed."
	};

	public static readonly AssistRow Bouncy = new AssistRow
	{
		Id = "bouncy",
		Section = "Variants",
		Label = "Bouncy",
		Values = "Off,On",
		Default = "Off",
		Description = "Every landing bounces you back up like a spring. Hold down to stick the landing."
	};

	public static readonly AssistRow Hiccups = new AssistRow
	{
		Id = "hiccups",
		Section = "Challenges",
		Label = "Hiccups",
		Values = "Off,On",
		Default = "Off",
		Description = "Every second or so Madeline hiccups: a big hop and a shove to a random side."
	};

	public static readonly AssistRow NoGrabbing = new AssistRow
	{
		Id = "no_grabbing",
		Section = "Challenges",
		Label = "No Grabbing",
		Values = "Off,On",
		Default = "Off",
		Description = "Madeline can no longer grab or climb walls."
	};

	public static readonly AssistRow NoClimbJump = new AssistRow
	{
		Id = "no_climb_jump",
		Section = "Challenges",
		Label = "No Climb Jumping",
		Values = "Off,On",
		Default = "Off",
		Description = "Jumping while holding a wall kicks you away from it instead of climbing up."
	};

	public static readonly AssistRow NoWallJump = new AssistRow
	{
		Id = "no_wall_jump",
		Section = "Challenges",
		Label = "No Wall Jumping",
		Values = "Off,On",
		Default = "Off",
		Description = "You can no longer jump off walls, so every climb has to be done with grabs and dashes."
	};

	public static readonly AssistRow OneHit = new AssistRow
	{
		Id = "one_hit",
		Section = "Challenges",
		Label = "One-Hit Mode",
		Values = "Off,On",
		Default = "Off",
		Description = "Any enemy hit kills, like Celeste: you have one heart, and you start again at the entrance of the room you came in by."
	};

	public static readonly AssistRow HalfStamina = new AssistRow
	{
		Id = "half_stamina",
		Section = "Challenges",
		Label = "Half Stamina",
		Values = "Off,On",
		Default = "Off",
		Description = "Madeline can only climb and hold walls for half as long."
	};

	public static readonly AssistRow NoWallSlide = new AssistRow
	{
		Id = "no_wall_slide",
		Section = "Challenges",
		Label = "No Wall Sliding",
		Values = "Off,On",
		Default = "Off",
		Description = "Holding into a wall no longer slows your fall."
	};

	public static readonly AssistRow LongHitLock = new AssistRow
	{
		Id = "long_hit_lock",
		Section = "Challenges",
		Label = "Long Hit Lock",
		Values = "Off,On",
		Default = "Off",
		Description = "After your dash hits an enemy, it stays locked more than twice as long."
	};

	public static readonly AssistRow Timer = new AssistRow
	{
		Id = "timer",
		Section = "Speedrun Timer",
		Label = "Show",
		Values = "Off,Save,Room,Save + Room,All saves,All saves + Room",
		Default = "Save + Room",
		Description = "Save: this save's time. All saves: every save together. Room: the time in this room. The Timer key cycles it too."
	};

	public static readonly AssistRow TimerBerries = new AssistRow
	{
		Id = "timer_berries",
		Section = "Speedrun Timer",
		Label = "Golden Berries",
		Values = "On,Off",
		Default = "On",
		Description = "Show this save's golden berries next to the timer."
	};

	public static readonly AssistRow AutoSkip = new AssistRow
	{
		Id = "auto_skip_cutscenes",
		Section = "Speedrun Timer",
		Label = "Auto-Skip Cutscenes",
		Values = "Off,On",
		Default = "Off",
		Description = "Skips every cutscene the moment Silksong lets you skip it, like a runner pressing skip."
	};

	public static readonly AssistRow Respawn = new AssistRow
	{
		Id = "instant_respawn",
		Section = "Assist Mode",
		Label = "Quick Respawn",
		Values = "Off,Room Entrance,Bench",
		Default = "Off",
		Description = "Spikes, pits and One-Hit deaths send you straight back, losing no masks: to where you came into the room, or to your last bench."
	};

	public static readonly AssistRow Look = new AssistRow
	{
		Id = "look",
		Section = "Appearance",
		Label = "Look",
		Values = "1,2,3,4",
		Default = "1",
		Description = "Four saved looks: everything below is kept for each. The Switch Skin key cycles them."
	};

	public static readonly AssistRow CatEars = new AssistRow
	{
		Id = "cat_ears",
		Section = "Appearance",
		Label = "Cat Ears",
		Values = "Off,On",
		Default = "Off",
		Description = "Cat ears and a tail, in her hair colour. Madeline-shaped skins."
	};

	public static readonly AssistRow Skin = new AssistRow
	{
		Id = "skin",
		Section = "Appearance",
		Label = "Skin",
		Values = "Madeline,Badeline,Sunset,Midnight,Mint,Crimson,Royal,Hornet,Lavender,Golden",   // no cat skins (user 2026-10-22)
		Default = "Madeline",
		Description = "Who this look is. Make your own with the rows below."
	};

	// user 2026-10-19: customizing beyond colours
	public static readonly AssistRow Headwear = new AssistRow
	{
		Id = "headwear",
		Section = "Appearance",
		Label = "Headwear",
		Values = "None,Bow,Hair Flower,Flower Crown,Sprout,Crown,Party Hat,Top Hat,Witch Hat,Headphones,Halo",
		Default = "None",
		Description = "Something on her head, in Celeste-sized pixels: a bow, a flower, a flower crown, a little sprout, a crown, a party hat, a top hat, a witch hat, headphones or a floating halo. Madeline-shaped skins."
	};

	public static readonly AssistRow TrailShape = new AssistRow
	{
		Id = "trail_shape",
		Section = "Appearance",
		Label = "Trail Shape",
		Values = "Afterimage,Hearts,Stars,Feathers",
		Default = "Afterimage",
		Description = "What her dash leaves behind: Celeste's afterimages, or hearts, stars or golden-feather wisps in her hair colour. Needs Dash Trail on."
	};

	public static readonly AssistRow Companion = new AssistRow
	{
		Id = "companion",
		Section = "Appearance",
		Label = "Companion",
		Values = "None,Bird,Strawberry,Golden Berry,Winged Berry,Moon Berry,Crystal Heart,Cassette,Key,Dash Crystal,Feather,Jellyfish",
		Default = "None",
		Description = "A friend from Celeste that follows her around Pharloom. The bird flies behind her and hops down to peck beside her when she stops; the rest float along behind her: berries, a crystal heart, a cassette, a key, a dash crystal, a golden feather or a jellyfish."
	};

	public static readonly AssistRow Aura = new AssistRow
	{
		Id = "aura",
		Section = "Appearance",
		Label = "Aura",
		Values = "None,Fireflies,Snowfall,Sparkles,Petals",
		Default = "None",
		Description = "Little things around her all the time: fireflies drifting about, a light snowfall, sparkles in her hair colour or falling petals."
	};

	public static readonly AssistRow Footprints = new AssistRow
	{
		Id = "footprints",
		Section = "Appearance",
		Label = "Footprints",
		Values = "None,Flowers,Sparkles,Hearts,Snow",
		Default = "None",
		Description = "What she leaves behind when she runs: little flowers that pop up and fade, sparkles, hearts, or a spray of snow."
	};

	public static readonly AssistRow Backpack = new AssistRow
	{
		Id = "backpack",
		Section = "Appearance",
		Label = "Backpack",
		Values = "On,Off",
		Default = "On",
		Description = "Off: Madeline without her backpack, as in the Prologue and Farewell."
	};

	public static readonly AssistRow Outfit = new AssistRow
	{
		Id = "outfit",
		Section = "Appearance",
		Label = "Outfit",
		Values = "Skin default,Madeline,Snowbound,Core,Reflection,Forest,Pharloom,Sunset,Midnight,Mint,Crimson,Royal,Hornet,Lavender,Golden",
		Default = "Skin default",
		Description = "Her clothes in another skin's colours. Madeline-shaped skins only (not Badeline or the Knight)."
	};

	public static readonly AssistRow HairTwo = new AssistRow
	{
		Id = "hair_two",
		Section = "Appearance",
		Label = "Hair: 2 Dashes",
		Values = "Skin default,Red,Purple,Ghost Blue,Pale Blue,Blue,Pink,PICO Red,PICO Blue,PICO Green,Snow,Orange,Violet,Rust,Ivory,Light Pink,Gold,Blonde,Brown,Black,White,Silver,Green,Mint,Teal,Navy,Lavender,Crimson,Peach",
		Default = "Skin default",
		Description = "Her hair colour while she has two dashes."
	};

	public static readonly AssistRow HairOne = new AssistRow
	{
		Id = "hair_one",
		Section = "Appearance",
		Label = "Hair: 1 Dash",
		Values = "Skin default,Red,Purple,Ghost Blue,Pale Blue,Blue,Pink,PICO Red,PICO Blue,PICO Green,Snow,Orange,Violet,Rust,Ivory,Light Pink,Gold,Blonde,Brown,Black,White,Silver,Green,Mint,Teal,Navy,Lavender,Crimson,Peach",
		Default = "Skin default",
		Description = "Her hair colour while she has one dash."
	};

	public static readonly AssistRow HairZero = new AssistRow
	{
		Id = "hair_zero",
		Section = "Appearance",
		Label = "Hair: No Dash",
		Values = "Skin default,Red,Purple,Ghost Blue,Pale Blue,Blue,Pink,PICO Red,PICO Blue,PICO Green,Snow,Orange,Violet,Rust,Ivory,Light Pink,Gold,Blonde,Brown,Black,White,Silver,Green,Mint,Teal,Navy,Lavender,Crimson,Peach",
		Default = "Skin default",
		Description = "Her hair colour with no dash left."
	};

	public static readonly AssistRow HairLength = new AssistRow
	{
		Id = "hair_length",
		Section = "Appearance",
		Label = "Hair Length",
		Values = "Short,Normal,Long,Very Long",
		Default = "Normal",
		Description = "How far her hair trails behind her."
	};

	public static readonly AssistRow HairSize = new AssistRow
	{
		Id = "hair_size",
		Section = "Appearance",
		Label = "Hair Volume",
		Values = "Thin,Normal,Fluffy",
		Default = "Normal",
		Description = "How thick her hair is."
	};

	public static readonly AssistRow HairEffect = new AssistRow
	{
		Id = "hair_effect",
		Section = "Appearance",
		Label = "Hair Effect",
		Values = "None,Rainbow,Light Tips,Dark Tips",
		Default = "None",
		Description = "Rainbow cycles colours while she has a dash. Tips fade the end of her hair lighter or darker."
	};

	public static readonly AssistRow DashTrail = new AssistRow
	{
		Id = "dash_trail",
		Section = "Appearance",
		Label = "Dash Trail",
		Values = "Off,On",
		Default = "On",
		Description = "The afterimages Madeline leaves when she dashes."
	};

	public static readonly AssistRow LandingEffects = new AssistRow
	{
		Id = "landing_effects",
		Section = "Appearance",
		Label = "Landing Effects",
		Values = "Off,On",
		Default = "On",
		Description = "Small dust puffs when Madeline lands after a hard fall."
	};

	public static readonly AssistRow HerName = new AssistRow
	{
		Id = "her_name",
		Section = "Appearance",
		Label = "Her Name",
		Values = "Madeline,Hornet",
		Default = "Madeline",
		Description = "What everyone in Pharloom calls her, in conversations and item text."
	};

	public static readonly AssistRow BenchFlags = new AssistRow
	{
		Id = "bench_flags",
		Section = "Appearance",
		Label = "Bench Flags",
		Values = "On,Off",
		Default = "On",
		Description = "A little Celeste checkpoint flag goes up beside every bench you've rested at."
	};

	public static readonly AssistRow DashColourRush = new AssistRow
	{
		Id = "dash_colour_rush",
		Section = "Appearance",
		Label = "Dash Colour Rush",
		Values = "Off,On",
		Default = "Off",
		Description = "Every dash sends her hair through a burst of rainbow colours."
	};

	public static readonly AssistRow KeySkin = new AssistRow
	{
		Id = "key_skin",
		Section = "Controls",
		Label = "Next Look",
		Values = "F1,F2,F3,F4,F5,F6,F7,F8,F9,F10,F11,F12",
		Default = "F2",
		Description = "Switches to the next saved look. Two actions can't share a key."
	};

	public static readonly AssistRow KeyHelp = new AssistRow
	{
		Id = "key_help",
		Section = "Controls",
		Label = "Tutorial",
		Values = "F1,F2,F3,F4,F5,F6,F7,F8,F9,F10,F11,F12",
		Default = "F4",
		Description = "Shows the Tutorial postcard. Two actions can't share a key."
	};

	public static readonly AssistRow KeySettings = new AssistRow
	{
		Id = "key_settings",
		Section = "Controls",
		Label = "Settings",
		Values = "F1,F2,F3,F4,F5,F6,F7,F8,F9,F10,F11,F12",
		Default = "F1",
		Description = "Opens this menu (a controller can also hold Taunt). Two actions can't share a key."
	};

	public static readonly AssistRow KeyTimer = new AssistRow
	{
		Id = "key_timer",
		Section = "Controls",
		Label = "Timer",
		Values = "F1,F2,F3,F4,F5,F6,F7,F8,F9,F10,F11,F12",
		Default = "F3",
		Description = "Cycles what the speedrun timer shows. Two actions can't share a key."
	};

	public static readonly AssistRow KeyRecords = new AssistRow
	{
		Id = "key_records",
		Section = "Controls",
		Label = "Records",
		Values = "F1,F2,F3,F4,F5,F6,F7,F8,F9,F10,F11,F12",
		Default = "F3",
		Description = "Opens the records: bosses, areas and golden berries, on this save or all saves. Two actions can't share a key."
	};

	public static readonly AssistRow KeyEmote = new AssistRow
	{
		Id = "emotes_btn",
		Section = "Controls",
		Label = "Emotes",
		Values = "Open",
		Default = "Open",
		Description = "See every emote and Madeline acting it out, and change its key or right-stick direction. In the game, an emote plays for a few seconds while she stands still."
	};

	public static readonly AssistRow KeyCamera = new AssistRow
	{
		Id = "key_camera",
		Section = "Controls",
		Label = "Theo's Camera",
		Values = "F1,F2,F3,F4,F5,F6,F7,F8,F9,F10,F11,F12",
		Default = "F6",
		Description = "Takes a photo without the HUD, saved to Pictures/Silkeste. L3 on a controller too. Two actions can't share a key."
	};

	public static readonly AssistRow UnlimitedBounces = new AssistRow
	{
		Id = "unlimited_bounces",
		Section = "Assist Mode",
		Label = "Unlimited Bounces",
		Values = "Off,On",
		Default = "Off",
		Description = "Bouncing off the same enemy again never gets weaker and always gives your dash back."
	};

	public static readonly AssistRow Difficulty = new AssistRow
	{
		Id = "difficulty_level",
		Section = "Game Modes",
		Label = "Difficulty",
		Values = "Story,Easy,Normal,Hard,Hardcore",
		Default = "Normal",
		Description = "Mixes with any game mode. Story: take a quarter of the damage, deal double, with forgiving jumps. Easy: take half, deal 50% more. Hard: take 50% more. Hardcore: any enemy hit kills you, but you deal double."
	};

	public static readonly AssistRow TitleSky = new AssistRow
	{
		Id = "title_sky",
		Section = "Video",
		Label = "Title Sky",
		Values = "Auto,Night,Dawn,Day,Sunset",
		Default = "Auto",
		Description = "The light on the title screen's mountain. Auto follows your PC's clock."
	};

	public static readonly AssistRow TitleArea = new AssistRow
	{
		Id = "title_area",
		Section = "Video",
		Label = "Title Mountain",
		Values = "Auto,Classic,Moss,Bellhart,Docks,Citadel,Marrow,Coral,Act 3",
		Default = "Auto",
		Description = "The colours and drifting particles on the title's mountain. Auto follows the area your last save was in."
	};

	// starting abilities (user 2026-10-19): per save, picked in the new-file menu with the game mode and difficulty
	// user 2026-10-21: Sandbox dashes forever by default; its own switch, so it never sits behind Assist's lock
	public static readonly AssistRow SandboxInfiniteDash = new AssistRow
	{
		Id = "sandbox_infinite_dash",
		Section = "Game",
		Label = "Sandbox: Infinite Dashes",
		Values = "On,Off",
		Default = "On",
		Description = "Sandbox files only: dash as many times as you like before landing. Off gives the normal single dash."
	};

	public static readonly AssistRow StartClimb = new AssistRow
	{
		Id = "start_climb",
		Section = "Game Modes",
		Label = "Cling Grip",
		Values = "Off,On",
		Default = "Off",
		Description = "Start with Madeline's grab, climbing and wall jumps. Off for the intended experience: they're yours once you find Cling Grip."
	};

	public static readonly AssistRow StartDoubleDash = new AssistRow
	{
		Id = "start_double_dash",
		Section = "Game Modes",
		Label = "Faydown Cloak",
		Values = "Off,On",
		Default = "Off",
		Description = "Start with the double jump. Off for the intended experience: it comes with the Faydown Cloak."
	};

	public static readonly AssistRow StartGlide = new AssistRow
	{
		Id = "start_glide",
		Section = "Game Modes",
		Label = "Drifter's Cloak",
		Values = "Off,On",
		Default = "Off",
		Description = "Start with the jellyfish glide: hold Jump while falling. Off for the intended experience: the glide comes with the Drifter's Cloak."
	};

	public static readonly AssistRow StartClawline = new AssistRow
	{
		Id = "start_clawline",
		Section = "Game Modes",
		Label = "Clawline",
		Values = "Off,On",
		Default = "Off",
		Description = "Start with the Clawline (L2 or Q), which pulls you to grapple points and enemies. Off for the intended experience."
	};

	public static readonly AssistRow StartSilkSoar = new AssistRow
	{
		Id = "start_silk_soar",
		Section = "Game Modes",
		Label = "Silk Soar",
		Values = "Off,On",
		Default = "Off",
		Description = "Start with Silk Soar, the spinning super jump: on the ground, hold Down and hold Clawline (L2 or Q) to charge. It costs 1 silk, which you get by hitting enemies. Off for the intended experience."
	};

	public static readonly AssistRow[] StartAbilities = new AssistRow[5] { StartClimb, StartDoubleDash, StartGlide, StartClawline, StartSilkSoar };

	// user 2026-10-19: cutscenes lag on slower PCs
	// user 2026-10-20: players don't know Celeste's tech
	public static readonly AssistRow TechTips = new AssistRow
	{
		Id = "tech_tips",
		Section = "Game",
		Label = "Tech Tips",
		Values = "On,Off",
		Default = "On",
		Description = "When Madeline stands still, a ghost Madeline shows the next Celeste technique beside her (super, hyper, wavedash, then the wall bounce) with your buttons and a timing bar. Do it 3 times to learn it. Each save file has its own lessons."
	};

	public static readonly AssistRow TechRoomButton = new AssistRow
	{
		Id = "techroom_btn",
		Section = "Game",
		Label = "Tech Tutorial",
		Values = "Play",
		Default = "Play",
		Description = "Plays Celeste's Farewell tutorial room (the wavedash), read from your Celeste, then a super gap, a hyper gap and enemies to practise on, with ghosts to copy. Press it again inside to leave."
	};

	public static readonly AssistRow LightCutscenes = new AssistRow
	{
		Id = "light_cutscenes",
		Section = "Video",
		Label = "Light Cutscenes",
		Values = "Off,On",
		Default = "Off",
		Description = "Turns off Silksong's heaviest camera effects (the cinematic blur, bloom, lens distortion and grain) while a cutscene plays, so slower PCs keep up. Cutscenes look a little plainer; gameplay is unchanged."
	};

	public static readonly AssistRow[] All = new AssistRow[61]
	{
		GameSpeed, InfiniteStamina, DashAssist, Invincibility, ForgivingJumps, JumpCount, DashLength, DashCount, Respawn, MirrorMode,
		LowFriction, SuperDashing, InvisibleMotion, Gravity, Wind, MadelineSpeed, Bouncy, Hiccups, NoGrabbing, NoClimbJump,
		NoWallJump, OneHit, HalfStamina, NoWallSlide, LongHitLock, Timer, TimerBerries, AutoSkip, Look, Skin,
		Backpack, Outfit, HairOne, HairZero, HairLength, HairSize, HairEffect, DashTrail, LandingEffects, DashColourRush, HerName,
		KeySkin, KeySettings, KeyRecords, KeyCamera, KeyEmote, TitleSky, TitleArea,
		StartClimb, StartDoubleDash, StartGlide, StartClawline, StartSilkSoar, LightCutscenes,
		Headwear, TrailShape, Companion, Aura, Footprints, TechRoomButton, SandboxInfiniteDash
	};
}
