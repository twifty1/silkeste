namespace MadelineInPharloom.Sheets;

public static class Hooks
{
	public static readonly HookRow PlayerController = new HookRow
	{
		Id = "player_controller",
		Target = "HeroController.FixedUpdate",
		Patch = "postfix",
		Ref = "HeroController.cs:2973 (sets rb2d.linearVelocity, gravityScale)"
	};

	public static readonly HookRow BlockJump = new HookRow
	{
		Id = "block_jump",
		Target = "HeroController.CanJump",
		Patch = "prefix",
		Ref = "HeroController.cs:10823"
	};

	public static readonly HookRow BlockDoubleJump = new HookRow
	{
		Id = "block_double_jump",
		Target = "HeroController.CanDoubleJump",
		Patch = "prefix",
		Ref = "HeroController.cs:10863"
	};

	public static readonly HookRow BlockWallJump = new HookRow
	{
		Id = "block_wall_jump",
		Target = "HeroController.CanWallJump",
		Patch = "prefix",
		Ref = "HeroController.cs:11260"
	};

	public static readonly HookRow BlockWallSlide = new HookRow
	{
		Id = "block_wall_slide",
		Target = "HeroController.CanWallSlide",
		Patch = "prefix",
		Ref = "HeroController.cs:11197"
	};

	public static readonly HookRow BlockFloat = new HookRow
	{
		Id = "block_float",
		Target = "HeroController.CanFloat",
		Patch = "prefix",
		Ref = "HeroController.cs:11069"
	};

	public static readonly HookRow BlockSprintDash = new HookRow
	{
		Id = "block_sprint_dash",
		Target = "HeroController.CanDash",
		Patch = "prefix",
		Ref = "HeroController.cs:10902"
	};

	public static readonly HookRow AttackInput = new HookRow
	{
		Id = "attack_input",
		Target = "HeroController.CanAttack",
		Patch = "prefix",
		Ref = "HeroController.cs:10956"
	};

	public static readonly HookRow BlockHardLanding = new HookRow
	{
		Id = "block_hard_landing",
		Target = "HeroController.ShouldHardLand",
		Patch = "prefix",
		Ref = "HeroController.cs:11337, called from OnCollisionEnter2D:11410 and Stay2D:11511"
	};

	public static readonly HookRow BlockClamber = new HookRow
	{
		Id = "block_clamber",
		Target = "HeroController.CheckClamberLedge",
		Patch = "prefix",
		Ref = "HeroController.cs:10638, called by the FSM action CheckHeroClamberLedge"
	};

	public static readonly HookRow BlockNailCharge = new HookRow
	{
		Id = "block_nail_charge",
		Target = "HeroController.CanNailCharge",
		Patch = "prefix",
		Ref = "HeroController.cs:11113"
	};

	public static readonly HookRow HitResponders = new HookRow
	{
		Id = "hit_responders",
		Target = "IHitResponder.Hit",
		Patch = "call",
		Ref = "Lever.cs:148, Breakable.cs:617 (IsNailDamage)"
	};

	public static readonly HookRow EnemyHitDetection = new HookRow
	{
		Id = "enemy_hit_detection",
		Target = "HealthManager.Hit",
		Patch = "call",
		Ref = "HealthManager.cs:779 (Nail type gives silk via TakeDamage:1107)"
	};

	public static readonly HookRow EnemyContactDamage = new HookRow
	{
		Id = "enemy_contact_damage",
		Target = "HeroController.TakeDamage",
		Patch = "prefix",
		Ref = "HeroController.cs:5280 (HazardType ENEMY/NON_HAZARD/EXPLOSION)"
	};

	public static readonly HookRow SpikePogo = new HookRow
	{
		Id = "spike_pogo",
		Target = "HeroController.TakeDamage",
		Patch = "prefix",
		Ref = "HeroController.cs:5280 (HazardType SPIKES / COAL_SPIKES while dashing down)"
	};

	public static readonly HookRow HazardDamage = new HookRow
	{
		Id = "hazard_damage",
		Target = "HeroController.TakeDamage",
		Patch = "prefix",
		Ref = "HeroController.cs:5280 (other HazardType values pass through)"
	};

	public static readonly HookRow HideHornet = new HookRow
	{
		Id = "hide_hornet",
		Target = "HeroController.renderer",
		Patch = "field",
		Ref = "HeroController.cs:747 (MeshRenderer.forceRenderingOff)"
	};

	public static readonly HookRow HideHornetDeath = new HookRow
	{
		Id = "hide_hornet_death",
		Target = "HeroController.GetHeroDeathPrefab",
		Patch = "field",
		Ref = "HeroController.cs:9391 Die and 9508 DieFromHazard spawn prefabs; their tk2d sprites use Hornet's animation library"
	};

	public static readonly HookRow DelayHazardFade = new HookRow
	{
		Id = "delay_hazard_fade",
		Target = "GameManager.PlayerDeadFromHazard",
		Patch = "prefix",
		Ref = "GameManager.cs:1013 (waitTime before HAZARD FADE)"
	};

	public static readonly HookRow DelayDeathFade = new HookRow
	{
		Id = "delay_death_fade",
		Target = "GameManager.PlayerDead",
		Patch = "prefix",
		Ref = "GameManager.cs:954 (waitTime before the respawn transition)"
	};

	public static readonly HookRow ShortenHazardReload = new HookRow
	{
		Id = "shorten_hazard_reload",
		Target = "GameManager.PlayerDeadFromHazard",
		Patch = "transpiler",
		Ref = "GameManager.cs:1021 yield return new WaitForSeconds(0.65f) inside the coroutine (MoveNext)"
	};

	public static readonly HookRow SkipWalkIn = new HookRow
	{
		Id = "skip_walk_in",
		Target = "HeroController.EnterScene",
		Patch = "prefix",
		Ref = "HeroController.cs:6735 enterSkip: EnterHeroSubHorizontal:6963 places the hero 0.33 s in and calls FinishedEnteringScene at once; EnterHeroSubFadeUp:7007 skips entry delays (user choice 2026-10-10)"
	};

	public static readonly HookRow HornetVoice = new HookRow
	{
		Id = "hornet_voice",
		Target = "NPCSpeakingAudio.PlayVoice(RandomAudioClipTable,Vector3)",
		Patch = "prefix",
		Ref = "NPCSpeakingAudio.cs:190; only caller HeroTalkAnimation.InternalSetTalking:358 with HeroTalkAnimation.TalkAudioTable"
	};

	public static readonly HookRow NpcVoice = new HookRow
	{
		Id = "npc_voice",
		Target = "NPCSpeakingAudio.PlayVoice(RandomAudioClipTable,Vector3,AudioSource,NPCSpeakingAudio)",
		Patch = "prefix",
		Ref = "NPCSpeakingAudio.cs:195 (stops _currentPlayingSource first)"
	};

	public static readonly HookRow MuteHornetSelect = new HookRow
	{
		Id = "mute_hornet_select",
		Target = "RandomAudioClipTable.SelectClip(Boolean)",
		Patch = "prefix",
		Ref = "HeroController woundAudioTable, woundHeavyAudioTable, woundFrostAudioTable, hazardDamageAudioTable, pitFallAudioTable, deathAudioTable, gruntAudioTable, attackAudioTable, warriorRageAttackAudioTable: no clip is chosen from them"
	};

	public static readonly HookRow MuteHornetSelectAny = new HookRow
	{
		Id = "mute_hornet_select_any",
		Target = "RandomAudioClipTable.SelectClipIgnoreProbability(Boolean)",
		Patch = "prefix",
		Ref = "same tables"
	};

	public static readonly HookRow MuteHornetSelectRandom = new HookRow
	{
		Id = "mute_hornet_select_random",
		Target = "RandomAudioClipTable.SelectRandomClip",
		Patch = "prefix",
		Ref = "same tables"
	};

	public static readonly HookRow MuteHornetPlay = new HookRow
	{
		Id = "mute_hornet_play",
		Target = "RandomAudioClipTable.PlayOneShotUnsafe(AudioSource,Single,Boolean)",
		Patch = "prefix",
		Ref = "same tables"
	};

	public static readonly HookRow MuteHornetPlayPitch = new HookRow
	{
		Id = "mute_hornet_play_pitch",
		Target = "RandomAudioClipTable.PlayOneShotUnsafe(AudioSource,Single,Single,Boolean)",
		Patch = "prefix",
		Ref = "same tables"
	};

	public static readonly HookRow DashFreezeShake = new HookRow
	{
		Id = "dash_freeze_shake",
		Target = "CameraManagerReference.DoShake",
		Patch = "call",
		Ref = "CameraManagerReference.cs:58 -> CameraShakeManager.DoShake:221, FreezeFrames:338 (1/60 s per frame), EvaluateShakes x ShakeMultiplier:335"
	};

	public static readonly HookRow SlashFx = new HookRow
	{
		Id = "slash_fx",
		Target = "{game:celeste}/Content/Graphics/Atlases/Gameplay.meta",
		Patch = "read",
		Ref = "effects/slash/00-03 (Celeste.SlashFx)"
	};

	public static readonly HookRow FloorSurface = new HookRow
	{
		Id = "floor_surface",
		Target = "PlayerData.environmentType",
		Patch = "field",
		Ref = "PlayerData.cs; set per area by EnviroRegion, read by HeroController.checkEnvironment:4887 for Hornet's footsteps"
	};

	public static readonly HookRow DeathCounterSlot = new HookRow
	{
		Id = "death_counter_slot",
		Target = "GameManager.profileID",
		Patch = "field",
		Ref = "GameManager.cs (save slot number)"
	};

	public static readonly HookRow DustFx = new HookRow
	{
		Id = "dust_fx",
		Target = "{game:celeste}/Content/Graphics/Atlases/Gameplay.meta",
		Patch = "read",
		Ref = "particles/smoke0-3 (Celeste.ParticleTypes.Dust)"
	};

	public static readonly HookRow DeathCounterArt = new HookRow
	{
		Id = "death_counter_art",
		Target = "{game:celeste}/Content/Graphics/Atlases/Gui.meta",
		Patch = "read",
		Ref = "collectables/skullBlue, x, renogare64_0 (glyphs from Dialog/Fonts/renogare64.fnt)"
	};

	public static readonly HookRow DeathCounterFont = new HookRow
	{
		Id = "death_counter_font",
		Target = "{game:celeste}/Content/Dialog/Fonts/renogare64.fnt",
		Patch = "read",
		Ref = "BMFont XML; page renogare64_0 lives in the Gui atlas"
	};

	public static readonly HookRow BindHeal = new HookRow
	{
		Id = "bind_heal",
		Target = "HeroAnimationController.GetClip",
		Patch = "read",
		Ref = "Hornet's tk2d clip names while binding: BindCharge Ground/Air, then BindBurst Ground/Air (test-build log 2026-10-10); a clip change away from both without a burst means the bind was interrupted"
	};

	public static readonly HookRow BindFreeze = new HookRow
	{
		Id = "bind_freeze",
		Target = "HeroController.FixedUpdate",
		Patch = "call",
		Ref = "inside player_controller's postfix, before anything else: while the BindCharge / BindBurst clips play, rb velocity 0, gravity 0, position held"
	};

	public static readonly HookRow PostcardPause = new HookRow
	{
		Id = "postcard_pause",
		Target = "HeroController.RelinquishControl",
		Patch = "call",
		Ref = "HeroController.cs:7472; RegainControl:7493 when the card closes"
	};

	public static readonly HookRow BossCheck = new HookRow
	{
		Id = "boss_check",
		Target = "HealthManager.bigEnemyDeath",
		Patch = "field",
		Ref = "HealthManager.cs:427 private bool bigEnemyDeath; 456 private int initHp (set in Awake, line 630); 229 public int hp"
	};

	public static readonly HookRow ControllerLayout = new HookRow
	{
		Id = "controller_layout",
		Target = "GameSettings.ResetGamepadSettings",
		Patch = "postfix",
		Ref = "GameSettings.cs:385 ResetGamepadSettings builds new ControllerMapping() (ControllerMapping.cs: attack=Action3, quickCast=RightBumper); called for a first run, a new controller type and Reset to defaults"
	};

	public static readonly HookRow DefaultControls = new HookRow
	{
		Id = "default_controls",
		Target = "InputHandler.ResetDefaultControllerButtonBindings",
		Patch = "call",
		Ref = "GameSettings.cs:68.. jumpKey etc (InControl Key/Mouse names), 337 SaveKeyboardSettings; InputHandler.cs:662 ResetAllControllerButtonBindings, 645 ResetDefaultControllerButtonBindings (re-reads the keyboard from gs), AddKeyBinding parses InControl.Mouse names; once per PC: %LOCALAPPDATA%/MadelineInPharloom/controls.cfg"
	};

	public static readonly HookRow UnlockWatch = new HookRow
	{
		Id = "unlock_watch",
		Target = "PlayerData.hasWalljump",
		Patch = "field",
		Ref = "PlayerData.cs:261 hasWalljump (Cling Grip), 263 hasDoubleJump (Faydown Cloak), 259 hasBrolly (Drifter's Cloak): a change from false to true while playing queues the card"
	};

	public static readonly HookRow PostcardArt = new HookRow
	{
		Id = "postcard_art",
		Target = "{game:celeste}/Content/Graphics/Atlases/Gui.meta",
		Patch = "read",
		Ref = "postcard (859x583) and renogare64_0 glyphs 32-126"
	};

	public static readonly HookRow UpdraftSpeed = new HookRow
	{
		Id = "updraft_speed",
		Target = "HeroController.EnterUpdraft",
		Patch = "postfix",
		Ref = "HeroController.cs:6682 EnterUpdraft(float speed) sets cState.inUpdraft and hands the speed to Hornet's brolly FSM, which block_float keeps off"
	};

	public static readonly HookRow FloatSpeed = new HookRow
	{
		Id = "float_speed",
		Target = "HeroController.FLOAT_SPEED",
		Patch = "field",
		Ref = "HeroController.cs:255 public float FLOAT_SPEED; the float sets rb2d.linearVelocity.y = FLOAT_SPEED (line 3077)"
	};

	public static readonly HookRow CutsceneAnims = new HookRow
	{
		Id = "cutscene_anims",
		Target = "HeroAnimationController.GetClip",
		Patch = "read",
		Ref = "Hornet's current tk2d clip while Silksong has control; clip names from the fsmtemplates bundle"
	};

	public static readonly HookRow CutsceneGravity = new HookRow
	{
		Id = "cutscene_gravity",
		Target = "HeroController.AffectedByGravity",
		Patch = "call",
		Ref = "HeroController.cs: AffectedByGravity sets IsGravityApplied; the controller keeps gravity off and stops her when it hands Hornet back with IsGravityApplied false"
	};

	public static readonly HookRow PlayerSprite = new HookRow
	{
		Id = "player_sprite",
		Target = "{game:celeste}/Content/Graphics/Atlases/Gameplay.meta",
		Patch = "read",
		Ref = "Monocle.Atlas Packer format + VirtualTexture .data RLE"
	};

	public static readonly HookRow PlayerAnims = new HookRow
	{
		Id = "player_anims",
		Target = "{game:celeste}/Content/Graphics/Sprites.xml",
		Patch = "read",
		Ref = "<player> block: anims + hair offsets"
	};

	public static readonly HookRow DashSfx = new HookRow
	{
		Id = "dash_sfx",
		Target = "{game:celeste}/Content/FMOD/Desktop/sfx.bank",
		Patch = "read",
		Ref = "rendered once to WAV by CelesteSfx.exe using the player's own fmodstudio.dll"
	};

	public static readonly HookRow BossTitle = new HookRow
	{
		Id = "boss_title",
		Target = "DisplayBossTitle.OnEnter",
		Patch = "postfix",
		Ref = "HutongGames.PlayMaker.Actions/DisplayBossTitle.cs: bossTitle (FsmString) is the title id; names come from TeamCherry.Localization Language sheet Titles, <id>_MAIN / <id>_SUPER"
	};

	public static readonly HookRow BossDefeated = new HookRow
	{
		Id = "boss_defeated",
		Target = "PlayerData.defeatedBellBeast",
		Patch = "field",
		Ref = "PlayerData.cs: every public bool defeated* / killed* is watched; one turning on during a fight ends it"
	};

	public static readonly HookRow GameSpeed = new HookRow
	{
		Id = "game_speed",
		Target = "TimeManager.CreateTimeControl",
		Patch = "call",
		Ref = "TimeManager.cs:223 CreateTimeControl (Multiplicative for game speed, MinValue 0 for the menu and dash assist)"
	};

	public static readonly HookRow AssistInvincibility = new HookRow
	{
		Id = "assist_invincibility",
		Target = "HeroController.TakeDamage",
		Patch = "call",
		Ref = "shares the enemy_contact_damage prefix; HazardType.PIT / RESPAWN_PIT still go through"
	};

	public static readonly HookRow GuiArtExtra = new HookRow
	{
		Id = "gui_art_extra",
		Target = "{game:celeste}/Content/Graphics/Atlases/Gui.meta",
		Patch = "read",
		Ref = "collectables/goldberry, strawberry, skullGold, dashes; strawberryCountBG; areaselect/card(_golden), cardtop(_golden), title; assistmode, assistmodestamp, adjustarrowleft/right, dotarrow(_outline)"
	};

	public static readonly HookRow[] All = new HookRow[56]
	{
		PlayerController, BlockJump, BlockDoubleJump, BlockWallJump, BlockWallSlide, BlockFloat, BlockSprintDash, AttackInput, BlockHardLanding, BlockClamber,
		BlockNailCharge, HitResponders, EnemyHitDetection, EnemyContactDamage, SpikePogo, HazardDamage, HideHornet, HideHornetDeath, DelayHazardFade, DelayDeathFade,
		ShortenHazardReload, SkipWalkIn, HornetVoice, NpcVoice, MuteHornetSelect, MuteHornetSelectAny, MuteHornetSelectRandom, MuteHornetPlay, MuteHornetPlayPitch, DashFreezeShake,
		SlashFx, FloorSurface, DeathCounterSlot, DustFx, DeathCounterArt, DeathCounterFont, BindHeal, BindFreeze, PostcardPause, BossCheck,
		ControllerLayout, DefaultControls, UnlockWatch, PostcardArt, UpdraftSpeed, FloatSpeed, CutsceneAnims, CutsceneGravity, PlayerSprite, PlayerAnims,
		DashSfx, BossTitle, BossDefeated, GameSpeed, AssistInvincibility, GuiArtExtra
	};
}
