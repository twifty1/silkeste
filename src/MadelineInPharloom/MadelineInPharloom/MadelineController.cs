using System;
using System.Collections.Generic;
using System.Globalization;
using GlobalEnums;
using GlobalSettings;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

public class MadelineController
{
	private struct Snapshot
	{
		public bool Valid;

		public float Time;

		public int State;

		public int Facing;

		public int Dashes;

		public int DashPhase;

		public Vector2 Speed;

		public Vector2 DashDir;

		public float DashTimer;

		public float DashAttackTimer;

		public float VarJumpTimer;

		public float VarJumpSpeed;

		public bool AutoJump;

		public bool Ducking;

		public bool DashedDown;
	}

	public const int StNormal = 0;

	public const int StClimb = 1;

	public const int StDash = 2;

	private static readonly AccessTools.FieldRef<HeroController, Rigidbody2D> Rb = AccessTools.FieldRefAccess<HeroController, Rigidbody2D>("rb2d");

	private static readonly AccessTools.FieldRef<HeroController, Collider2D> Col = AccessTools.FieldRefAccess<HeroController, Collider2D>("col2d");

	private static readonly AccessTools.FieldRef<HeroController, GameManager> Gm = AccessTools.FieldRefAccess<HeroController, GameManager>("gm");

	private const int TerrainMask = 33562880;

	private const float BufferTime = 0.08f;

	private const float Px = 8f;

	public int State;

	public Vector2 Speed;

	public int Facing = 1;

	public int Dashes = 1;

	public float Stamina = 110f;

	public bool Ducking;

	public bool OnGround;

	public Vector2 DashDir;

	public int LastClimbMove;

	public int WallSlideDir;

	public bool Active;

	public float SafetyTimer;

	public float HairFlashTimer;

	public float ScaleX = 1f;

	public float ScaleY = 1f;

	public bool Gliding;

	public static float UpdraftSpeed;

	private static bool floatLogged;

	private bool wasOnGround;

	private bool autoJump;

	private bool calledDashEvents;

	private bool dashStartedOnGround;

	private float jumpGraceTimer;

	private float varJumpTimer;

	private float varJumpSpeed;

	private float autoJumpTimer;

	private float dashCooldownTimer;

	private float dashRefillCooldownTimer;

	private float dashAttackTimer;

	private float dashTimer;

	private int dashPhase;

	private Vector2 beforeDashSpeed;

	private float wallSlideTimer = 1.2f;

	private float maxFall = 160f;

	private int forceMoveX;

	private float forceMoveXTimer;

	private float climbNoMoveTimer;

	private float wallSpeedRetentionTimer;

	private float wallSpeedRetained;

	private float wallBoostTimer;

	private int wallBoostDir;

	private float jumpBuffer;

	private float dashBuffer;

	private int moveX;

	private int moveY;

	private Vector2 lastAim;

	private bool jumpHeld;

	private bool grabHeld;

	private float restoreGravity = -1f;

	private readonly HashSet<object> hitThisDash = new HashSet<object>();

	private int enemiesThisDash;

	// two dashes: hitting dashes in a row, for the lock (user 2026-10-19)
	private int hitStreak;

	private float lastHitDashAt = -10f;

	private bool countedThisDash;

	private readonly Collider2D[] overlap = new Collider2D[32];

	private GameObject hitSource;

	private static readonly Dictionary<string, AccessTools.FieldRef<PlayerData, bool>> unlockFields = new Dictionary<string, AccessTools.FieldRef<PlayerData, bool>>();

	private bool wasDashB;

	private bool jumpAttack;

	private bool jumpImmune;

	private float hitLockTimer;

	private int moveXInput;

	private int moveYInput;

	private Vector2 aimInput;

	private int airJumps;

	private float hiccupTimer;

	private bool steered;

	public static float LastDashTime = -10f;

	public static int DashNumber;

	private tk2dSpriteAnimator bindAnimator;

	private bool bindFrozen;

	private Vector2 bindPosition;

	private Snapshot exit;

	private BoxCollider2D nailTrigger;

	private Rigidbody2D nailBody;

	private readonly Collider2D[] overlapStep = new Collider2D[32];

	private readonly Dictionary<IHitResponder, float> circletHit = new Dictionary<IHitResponder, float>();

	private HeroController hero;

	private float wallDustTimer;

	private bool airDownDiag;

	private float pendingSkillTime;

	private float pendingSkillSpeed;

	private float pendingSkillAt;

	private float skillTimeMult = 1f;

	private float skillSpeedMult = 1f;

	private Vector2 pendingSkillDir;

	private Vector2 skillDir;

	private bool skillDash;

	private static readonly AccessTools.FieldRef<HealthManager, bool> BigEnemyDeath = AccessTools.FieldRefAccess<HealthManager, bool>("bigEnemyDeath");

	private static readonly AccessTools.FieldRef<HealthManager, int> InitHp = AccessTools.FieldRefAccess<HealthManager, int>("initHp");

	private float pogoGraceTimer;

	private bool dashedDown;

	private bool pogoArmed;

	private static readonly string[] PogoHazards = "SPIKES,COAL_SPIKES".Split(new char[1] { ',' });

	private readonly Collider2D[] pogoHits = new Collider2D[16];

	private static int hitMask;

	public int MaxDashes
	{
		get
		{
			int dashCount = AssistMode.DashCount;
			if (dashCount >= 0)
			{
				return dashCount;
			}
			if (dashCount == -1)
			{
				return 3;
			}
			return 1;   // the Faydown Cloak gives a double jump, not a second dash (user 2026-10-21)
		}
	}

	public bool DashAttacking => dashAttackTimer > 0f;

	public bool EnemyImmune
	{
		get
		{
			if (Active)
			{
				if (State != 2 && (!jumpAttack || !jumpImmune))
				{
					return SafetyTimer > 0f;
				}
				return true;
			}
			return false;
		}
	}

	private bool Attacking
	{
		get
		{
			if (State != 2 || dashPhase != 1)
			{
				return jumpAttack;
			}
			return true;
		}
	}

	private Vector2 AttackDir
	{
		get
		{
			if (State != 2 && !(Speed == Vector2.zero))
			{
				return Speed.normalized;
			}
			return DashDir;
		}
	}

	public bool IsTired => CheckStamina < 20f;

	private float CheckStamina
	{
		get
		{
			if (!(wallBoostTimer > 0f))
			{
				return Stamina;
			}
			return Stamina + Tech.ClimbJump.StaminaCost;
		}
	}

	private bool CanDash
	{
		get
		{
			if (dashBuffer > 0f && dashCooldownTimer <= 0f && hitLockTimer <= 0f && !hunterLocked)
			{
				return Dashes > 0;
			}
			return false;
		}
	}

	public bool DashLocked => hitLockTimer > 0f || hunterLocked;

	// ---- the rebound (user 2026-10-21: an enemy by a wall was unhittable, since the dash stopped inside it and its
	// touch hurt for sure). A dash that hit an enemy and ends still inside it (stopped by a wall, or just ending there)
	// knocks her back out, away from it, with a short safe moment. A dash that goes cleanly through is unchanged.
	private Collider2D reboundFrom;

	private float reboundFor;

	// ---- crests (user 2026-10-22) -------------------------------------------------------------------------------------
	/// <summary>The bounce style: a shorter dash that always bounces off what it hits. The default, on every crest but
	/// the Wanderer (user 2026-10-22: the Hunter and Wanderer effects swapped places).</summary>
	private static bool Wanderer => PlayerData.instance != null && PlayerData.instance.CurrentCrestID != "Wanderer";

	private bool wandererRefillReady = true;

	// the Hunter crest (user 2026-10-22): hitting an enemy (bosses too) gives the dash back, so a second dash can
	// follow; after that the refill returns 1 s after she next touches the ground
	// the refill style, on the Wanderer crest only (user 2026-10-22)
	private static bool Hunter => PlayerData.instance != null && PlayerData.instance.CurrentCrestID == "Wanderer";

	private static float crestCheckAt;

	/// <summary>Every save has the Wanderer crest from the start, so both dash styles can be swapped at a bench
	/// (user 2026-10-22).</summary>
	public static void EnsureWandererCrest()
	{
		if (Time.unscaledTime < crestCheckAt || PlayerData.instance == null || HeroController.UnsafeInstance == null)
		{
			return;
		}
		crestCheckAt = Time.unscaledTime + 2f;
		try
		{
			ToolCrest crest = ToolItemManager.GetCrestByName("Wanderer");
			if (crest != null && !crest.IsUnlocked)
			{
				crest.Unlock();
				Plugin.Log.LogInfo("Crests: Wanderer unlocked on this save");
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Crests: " + e.Message);
		}
	}

	private bool hunterRefillReady = true;

	private bool hunterWaitGround;

	private float hunterLandedAt = -10f;

	private void HunterRefill()
	{
		if (!hunterRefillReady)
		{
			return;
		}
		hunterRefillReady = false;
		hunterWaitGround = true;
		wandererChainHit = true;   // this hit skips the hit lock (the flag is shared with the Wanderer's refill)
		dashCooldownTimer = 0f;    // and the second dash can go (and hit) right away (user 2026-10-22)
		Dashes = MaxDashes;
		HairFlashTimer = 0.12f;
		Sfx.Play(Sounds.Refill.Id, 0.6f);
		hunterSecondPending = true;   // the next dash, whenever it comes, is the second one
	}

	// after the Hunter's second dash: no dashing for 1.5 s from when that dash is over (user 2026-10-22)
	private bool hunterLocked;

	private bool hunterLockWaitGround;

	private float hunterLockTimer;

	// user 2026-10-22: a time window let a long wavedash or hyper slip past the lockout, so it's just "the next dash"
	private bool hunterSecondPending;

	private void HunterRecharge(float dt)
	{
		if (hunterLocked)
		{
			if (hunterLockWaitGround)
			{
				// user 2026-10-22: 1.5 s from when the second dash (and its hit) is over, wherever she is
				if (State != 2)
				{
					hunterLockWaitGround = false;
					hunterLockTimer = 1.5f;
				}
			}
			else if ((hunterLockTimer -= dt) <= 0f)
			{
				hunterLocked = false;
			}
		}
		if (hunterRefillReady)
		{
			return;
		}
		if (hunterWaitGround && OnGround)
		{
			hunterWaitGround = false;
			hunterLandedAt = Time.time;
		}
		else if (!hunterWaitGround && Time.time - hunterLandedAt >= 1f)
		{
			hunterRefillReady = true;
		}
	}

	private bool wandererChainHit;

	private bool wandererChainDash;

	// standing under a boss and dashing straight up over and over (user 2026-10-22): a hit from an upward dash that
	// left the ground locks twice as long and gives no Wanderer refill. The Hunter has its own lockout.
	private bool GroundPoke => dashStartedOnGround && DashDir.y < -0.3f && !Hunter;

	private void WandererBounce()
	{
		if (MadelineTouches.DevOn)
		{
			Plugin.Log.LogInfo("Crest: Wanderer bounce (crest " + PlayerData.instance.CurrentCrestID + ")");
		}
		Vector2 back = DashDir.sqrMagnitude > 0.01f ? -DashDir.normalized : new Vector2(-Facing, 0f);
		// a small knock back, not a big bounce (user 2026-10-22: chain dashes, the bounce was too bouncy)
		float vx = back.x * 90f;
		float vy = back.y > 0.3f ? 80f : back.y < -0.3f ? -100f : -70f;   // down off what's above, up off what's below, a hop sideways
		// one dash back per trip through the air: a hit refills it, then she has to touch the ground for the next
		if (wandererRefillReady && !OnGround && !GroundPoke)   // in the air only: on the ground the hit lock stays
		{
			wandererRefillReady = false;
			wandererChainHit = true;
			Dashes = MaxDashes;
			HairFlashTimer = 0.12f;
			Sfx.Play(Sounds.Refill.Id, 0.6f);
		}
		State = 0;
		Speed = new Vector2(vx, vy);
		varJumpTimer = vy < 0f ? 0.06f : 0f;
		varJumpSpeed = Speed.y;
		autoJump = vy < 0f;
		autoJumpTimer = 0f;
		dashAttackTimer = 0f;
		if (vx != 0f)
		{
			forceMoveX = Math.Sign(vx);
			forceMoveXTimer = 0.08f;
		}
		SafetyTimer = Math.Max(SafetyTimer, 0.25f);
		reboundFrom = null;
		ScaleX = 1.3f;
		ScaleY = 0.75f;
		Sfx.Play(Sounds.Jump.Id, 0.8f);
		// weight: a dust burst where she struck, and a short freeze on impact like Celeste's
		Particles.Dust(Center() + new Vector3(-back.x, back.y, 0f) * 0.5f, Mathf.Atan2(back.y, back.x), 8);   // back is y-down; the world is y-up
		TechRoom.HitStop(wandererChainDash ? 0.07f : 0.045f);
	}

	private Collider2D reboundSafe;

	private float reboundSafeFor;

	// the enemy's touch-damage areas: often bigger than the part a dash hits (user 2026-10-21)
	private readonly List<Collider2D> reboundZones = new List<Collider2D>();

	private void ReboundZones(GameObject enemy, Collider2D hit)
	{
		reboundZones.Clear();
		reboundZones.Add(hit);
		try
		{
			foreach (DamageHero dh in enemy.GetComponentsInChildren<DamageHero>(includeInactive: false))
			{
				foreach (Collider2D c in dh.GetComponents<Collider2D>())
				{
					if (c != null && c.enabled && !reboundZones.Contains(c))
					{
						reboundZones.Add(c);
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	/// <summary>Is she inside the enemy she hit: the part the dash hit, or any of its touch-damage areas?</summary>
	private bool InsideRebound(Bounds mine)
	{
		foreach (Collider2D c in reboundZones)
		{
			if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
			{
				continue;
			}
			Bounds b = c.bounds;
			b.Expand(0.1f);
			if (mine.Intersects(b))
			{
				return true;
			}
		}
		return false;
	}

	private void CheckRebound(HeroController h, float dt)
	{
		if (reboundSafe != null)
		{
			reboundSafeFor -= dt;
			Collider2D mc = Col(h);
			if (reboundSafeFor > 0f && mc != null && InsideRebound(mc.bounds))
			{
				SafetyTimer = Math.Max(SafetyTimer, 0.1f);
			}
			else
			{
				reboundSafe = null;
			}
		}
		if (reboundFrom == null)
		{
			return;
		}
		reboundFor -= dt;
		bool dashOver = State != 2 || !DashAttacking || Speed.x == 0f && DashDir.x != 0f;
		if (reboundFor <= 0f || !reboundFrom.enabled || !reboundFrom.gameObject.activeInHierarchy)
		{
			reboundFrom = null;
			return;
		}
		if (!dashOver)
		{
			return;
		}
		Collider2D me = Col(h);
		if (me == null)
		{
			reboundFrom = null;
			return;
		}
		Bounds mine = me.bounds;
		Bounds theirs = reboundFrom.bounds;
		theirs.Expand(0.1f);
		if (!InsideRebound(mine))
		{
			reboundFrom = null;   // out of it already: nothing to do
			return;
		}
		if (MadelineTouches.DevOn)
		{
			Plugin.Log.LogInfo("Still inside " + reboundFrom.name + " after a hit: safe while inside (crest " + (PlayerData.instance != null ? PlayerData.instance.CurrentCrestID : "?") + ")");
		}
		// no push (user 2026-10-22: it bounced only sometimes, so it felt random); only the Wanderer bounces, on every
		// hit. Every crest stays safe while still inside what it hit, so an enemy by a wall isn't a sure hit
		reboundSafe = reboundFrom;
		reboundSafeFor = 0.6f;
		SafetyTimer = Math.Max(SafetyTimer, 0.1f);
		reboundFrom = null;
	}

	private bool glideArmed;

	/// <summary>Jumps she has in the air: Assist's Jump Count, and one more with the Faydown Cloak.</summary>
	private int AirJumpsAllowed()
	{
		int n = AssistMode.JumpCount < 0 ? 999 : AssistMode.JumpCount - 1;
		if (Unlocked(Unlocks.SecondDash))
		{
			n = Math.Max(n, 1);
		}
		return n;
	}

	/// <summary>Silksong's own Faydown Cloak double jump: its silk wings and sound, plus a puff of Celeste's feathers.</summary>
	private void DoubleJumpFx(HeroController h)
	{
		try
		{
			if (h.doubleJumpEffectPrefab != null)
			{
				h.doubleJumpEffectPrefab.Spawn(h.transform, Vector3.zero);
			}
			AudioSource src = h.GetComponent<AudioSource>();
			if (src != null && h.doubleJumpClip != null)
			{
				src.PlayOneShot(h.doubleJumpClip, 1f);
			}
			else
			{
				Sfx.Play(Sounds.Jump.Id);
			}
			Particles.Feathers(Feet() + new Vector3(0f, 0.6f, 0f), 6, 0.6f);
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Double jump: " + e.Message);
			Sfx.Play(Sounds.Jump.Id);
		}
	}

	/// <summary>Inside a Tech Tutorial dream block: a dash into one carries her straight through (user 2026-10-20).</summary>
	public bool InDream;

	public void DreamTick(bool inside)
	{
		if (inside && State == 2)
		{
			if (!InDream)
			{
				Sfx.Play(Sounds.Refill.Id);
			}
			InDream = true;
			dashTimer = Math.Max(dashTimer, 0.05f);
			Speed = DashDir.normalized * 240f;
		}
		else if (InDream && !inside)
		{
			// out the other side like Celeste's DreamDashEnd: full speed kept, dash back, a moment to jump
			InDream = false;
			Dashes = MaxDashes;
			Stamina = 110f;
			State = 0;
			Speed = DashDir.normalized * 240f;
			autoJump = true;
			autoJumpTimer = 0f;
			jumpGraceTimer = DashDir.x != 0f ? 0.1f : 0f;
			dashAttackTimer = 0f;
		}
	}

	/// <summary>A Tech Tutorial spring: sets her moving and gives the dash back.</summary>
	public void Bounce(Vector2 speed)
	{
		// Celeste's Player.SideBounce: normal state, auto jump (full height without holding Jump), dash and stamina back
		State = 0;
		Ducking = false;
		Speed = speed;
		varJumpTimer = 0.2f;
		varJumpSpeed = speed.y;
		autoJump = true;
		autoJumpTimer = 0f;
		jumpGraceTimer = 0f;
		dashAttackTimer = 0f;
		wallSlideTimer = 1.2f;
		Dashes = MaxDashes;
		Stamina = 110f;
		if (speed.x != 0f)
		{
			forceMoveX = Math.Sign(speed.x);
			forceMoveXTimer = 0.3f;
			Facing = Math.Sign(speed.x);
		}
	}

	private bool SuperWallJumpAngleCheck
	{
		get
		{
			if (Math.Abs(DashDir.x) <= 0.2f)
			{
				return DashDir.y <= -0.75f;
			}
			return false;
		}
	}

	public Vector2 AimPreview
	{
		get
		{
			if (!(aimInput == Vector2.zero))
			{
				return aimInput;
			}
			return new Vector2(Facing, 0f);
		}
	}

	public bool SkillDashing
	{
		get
		{
			if (skillDash)
			{
				return State == 2;
			}
			return false;
		}
	}

	private bool PogoReady
	{
		get
		{
			if (!pogoArmed)
			{
				if (State == 2)
				{
					if (!dashedDown)
					{
						if (dashPhase == 0)
						{
							return lastAim.y > 0f;
						}
						return false;
					}
					return true;
				}
				return false;
			}
			return true;
		}
	}

	private static int HitMask
	{
		get
		{
			if (hitMask == 0)
			{
				hitMask = Physics2D.GetLayerCollisionMask(17);
				string[] array = "8,19".Split(new char[1] { ',' });
				foreach (string text in array)
				{
					hitMask |= 1 << int.Parse(text.Trim(), CultureInfo.InvariantCulture);
				}
			}
			return hitMask;
		}
	}

	public event Action Dashed;

	public event Action GlideStarted;

	public event Action DashBegan;

	public event Action<Vector2, Vector2> DashLaunched;

	public event Action<string> SkillUsed;

	private static float GlideFall(HeroController h)
	{
		if (!floatLogged)
		{
			floatLogged = true;
			Plugin.Log.LogInfo("Glide: Hornet's FLOAT_SPEED " + h.FLOAT_SPEED + " units/s");
		}
		return Math.Abs(h.FLOAT_SPEED) * 8f;
	}

	public static bool Unlocked(UnlockRow row)
	{
		// the Tech Tutorial plays like Celeste: climbing on, one dash, no glide or longer dash
		if (TechRoom.Active)
		{
			if (row.Id == Unlocks.GrabClimb.Id)
			{
				return true;
			}
			if (row.Id == Unlocks.SecondDash.Id || row.Id == Unlocks.Glide.Id || row.Id == Unlocks.SwiftStep.Id)
			{
				return false;
			}
		}
		if (!unlockFields.TryGetValue(row.PlayerData, out var value))
		{
			value = (unlockFields[row.PlayerData] = AccessTools.FieldRefAccess<PlayerData, bool>(row.PlayerData));
		}
		if (PlayerData.instance != null)
		{
			return value(PlayerData.instance);
		}
		return false;
	}

	private void BeginJumpAttack()
	{
		jumpAttack = true;
		jumpImmune = true;
	}

	public static bool ShouldDrive(HeroController h)
	{
		GameManager gameManager = Gm(h);
		if (gameManager != null && gameManager.GameState == GameState.PLAYING && !gameManager.isPaused && h.acceptingInput && !h.controlReqlinquished && h.hero_state != ActorStates.no_input && h.hero_state != ActorStates.hard_landing && !h.cState.transitioning && !h.cState.dead && !h.cState.hazardDeath && !h.cState.hazardRespawning && !h.cState.isBinding && !h.cState.swimming && !h.playerData.atBench && !h.IsInputBlocked())
		{
			return InteractManager.BlockingInteractable == null;
		}
		return false;
	}

	public void ReadInput(HeroActions a)
	{
		if (TechRoom.InputLocked)
		{
			// the Tech Tutorial's question is up (or a wipe): she stands still
			jumpHeld = false;
			grabHeld = false;
			moveXInput = 0;
			moveYInput = 0;
			aimInput = Vector2.zero;
			return;
		}
		float num = (AssistMode.ForgivingJumps ? 0.16f : 0.08f);
		if (a.Jump.WasPressed)
		{
			jumpBuffer = num;
		}
		if (a.Attack.WasPressed)
		{
			dashBuffer = 0.08f;
		}
		jumpHeld = a.Jump.IsPressed;
		grabHeld = a.Dash.IsPressed && !AssistMode.NoGrabbing;
		Vector2 vector = a.MoveVector.Vector;
		if (AssistMode.MirrorMode)
		{
			vector.x = 0f - vector.x;
		}
		int num2 = ((vector.x > 0.3f) ? 1 : ((vector.x < -0.3f) ? (-1) : 0));
		int num3 = ((vector.y < -0.5f) ? 1 : ((vector.y > 0.5f) ? (-1) : 0));
		moveYInput = num3;
		moveXInput = num2;
		if (vector.magnitude < 0.3f)
		{
			aimInput = Vector2.zero;
			return;
		}
		float f = Mathf.Round(Mathf.Atan2(0f - vector.y, vector.x) / (MathF.PI / 4f)) * (MathF.PI / 4f);
		aimInput = CorrectDashPrecision(new Vector2(Mathf.Cos(f), Mathf.Sin(f)));
	}

	public void ClearInput()
	{
		jumpBuffer = (dashBuffer = 0f);
	}

	public void InjectInput(int mx, int my, Vector2 aim, bool jumpPressed, bool jumpHeldNow, bool dashPressed, bool grab)
	{
		if (jumpPressed)
		{
			jumpBuffer = 0.08f;
		}
		if (dashPressed)
		{
			dashBuffer = 0.08f;
		}
		jumpHeld = jumpHeldNow;
		grabHeld = grab;
		moveXInput = mx;
		moveYInput = my;
		aimInput = ((aim == Vector2.zero) ? Vector2.zero : CorrectDashPrecision(aim));
	}

	private bool FreezeForBind(HeroController h, Rigidbody2D rb)
	{
		if (bindAnimator == null || bindAnimator.gameObject != h.gameObject)
		{
			bindAnimator = h.GetComponent<tk2dSpriteAnimator>();
		}
		string text = ((bindAnimator != null && bindAnimator.CurrentClip != null) ? bindAnimator.CurrentClip.name : "");
		if (!text.StartsWith("BindCharge", StringComparison.Ordinal) && !text.StartsWith("BindBurst", StringComparison.Ordinal))
		{
			if (bindFrozen && !Active && restoreGravity > 0f)
			{
				rb.gravityScale = restoreGravity;
			}
			bindFrozen = false;
			return false;
		}
		if (!bindFrozen)
		{
			bindFrozen = true;
			bindPosition = rb.position;
		}
		rb.gravityScale = 0f;
		rb.linearVelocity = Vector2.zero;
		rb.position = bindPosition;
		Speed = Vector2.zero;
		return true;
	}

	public void Step(HeroController h, float dt)
	{
		Rigidbody2D rigidbody2D = Rb(h);
		if (FreezeForBind(h, rigidbody2D))
		{
			return;
		}
		if (!ShouldDrive(h))
		{
			if (Active)
			{
				if (h.cState.transitioning)
				{
					SaveForTransition();
				}
				Deactivate(h, rigidbody2D);
			}
			return;
		}
		if (!Active)
		{
			Activate(h, rigidbody2D);
		}
		if (hitSource == null)
		{
			hitSource = new GameObject("MadelineDashHit");
			hitSource.transform.SetParent(h.transform, worldPositionStays: false);
			try
			{
				hitSource.tag = "Nail Attack";
			}
			catch (UnityException)
			{
			}
		}
		hero = h;
		float madelineSpeed = AssistMode.MadelineSpeed;
		dt *= madelineSpeed;
		float y = Speed.y;
		bool onGround = OnGround;
		ApplyContacts(h);
		CheckRebound(h, dt);
		HunterRecharge(dt);
		if (AssistMode.Bouncy && !onGround && OnGround && y > 60f && moveYInput != 1 && State != 2)
		{
			Speed.y = 0f - Math.Max(y * 0.9f, 105f);
			varJumpTimer = 0f;
			autoJump = true;
			ScaleX = 1.4f;
			ScaleY = 0.6f;
			Sfx.Play(Sounds.Jump.Id);
		}
		UpdateTimers(h, dt);
		int wind = AssistMode.Wind;
		if (wind != 0 && State != 1)
		{
			Speed.x += (float)Math.Sign(wind) * 1000f * ((Math.Abs(wind) == 2) ? 0.8f : 0.45f) * dt;
		}
		if (pendingSkillTime > 0f && Time.time - pendingSkillAt < 1f && State != 2)
		{
			int dashes = Dashes;
			State = StartDash();
			Dashes = dashes;
			skillDash = true;
			skillTimeMult = pendingSkillTime;
			skillSpeedMult = pendingSkillSpeed;
			skillDir = pendingSkillDir;
			pendingSkillTime = 0f;
		}
		switch (State)
		{
		case 0:
			State = NormalUpdate(h, dt);
			break;
		case 1:
			State = ClimbUpdate(h, dt);
			break;
		case 2:
			State = DashUpdate(h, dt);
			break;
		}
		if (jumpAttack && (State == 1 || (State == 0 && OnGround && Speed.y >= 0f)))
		{
			jumpAttack = false;
		}
		if (State != 0)
		{
			Gliding = false;
		}
		UpdatePogoArming();
		ProbeSpikes(h, dt);
		if (DashAttacking && OnGround && DashDir.y == 0f && !Ducking && Speed.x != 0f && moveX == -Math.Sign(Speed.x))
		{
			Particles.Dust(Feet(), -MathF.PI / 2f, 1);
		}
		if (Attacking)
		{
			DashHits(h);
		}
		CircletHits(h);
		UpdateNailTrigger(h, Attacking);
		ScaleX = Mathf.MoveTowards(ScaleX, 1f, 1.75f * dt);
		ScaleY = Mathf.MoveTowards(ScaleY, 1f, 1.75f * dt);
		rigidbody2D.gravityScale = 0f;
		Vector2 linearVelocity = new Vector2(Speed.x, 0f - Speed.y) / 8f * madelineSpeed;
		if (State == 1)
		{
			linearVelocity.x = (float)Facing * 30f / 8f;
		}
		rigidbody2D.linearVelocity = linearVelocity;
		if (Facing > 0 && !h.cState.facingRight)
		{
			h.FaceRight();
		}
		else if (Facing < 0 && h.cState.facingRight)
		{
			h.FaceLeft();
		}
	}

	private void Activate(HeroController h, Rigidbody2D rb)
	{
		Active = true;
		restoreGravity = ((rb.gravityScale > 0f) ? rb.gravityScale : h.DEFAULT_GRAVITY);
		Speed = new Vector2(rb.linearVelocity.x, 0f - rb.linearVelocity.y) * 8f;
		State = 0;
		Dashes = MaxDashes;
		Stamina = 110f;
		Facing = (h.cState.facingRight ? 1 : (-1));
		dashBuffer = (jumpBuffer = 0f);
		ApplyTransition(Gm(h));
	}

	private void SaveForTransition()
	{
		exit = new Snapshot
		{
			Valid = true,
			Time = Time.unscaledTime,
			State = State,
			Facing = Facing,
			Dashes = Dashes,
			DashPhase = dashPhase,
			Speed = Speed,
			DashDir = DashDir,
			DashTimer = dashTimer,
			DashAttackTimer = dashAttackTimer,
			VarJumpTimer = varJumpTimer,
			VarJumpSpeed = varJumpSpeed,
			AutoJump = autoJump,
			Ducking = Ducking,
			DashedDown = dashedDown
		};
	}

	private void ApplyTransition(GameManager gm)
	{
		Snapshot snapshot = exit;
		exit.Valid = false;
		if (!snapshot.Valid || Time.unscaledTime - snapshot.Time > 10f || gm == null || string.IsNullOrEmpty(gm.entryGateName))
		{
			return;
		}
		string text = gm.entryGateName.ToLowerInvariant();
		TransitionRow transitionRow = default(TransitionRow);
		bool flag = false;
		TransitionRow[] all = Transitions.All;
		for (int i = 0; i < all.Length; i++)
		{
			TransitionRow transitionRow2 = all[i];
			string[] array = transitionRow2.EntryGates.Split(new char[1] { ',' });
			foreach (string value in array)
			{
				if (text.StartsWith(value, StringComparison.Ordinal))
				{
					transitionRow = transitionRow2;
					flag = true;
				}
			}
		}
		if (flag)
		{
			Facing = snapshot.Facing;
			Dashes = snapshot.Dashes;
			Ducking = snapshot.Ducking;
			Speed = snapshot.Speed;
			varJumpTimer = snapshot.VarJumpTimer;
			varJumpSpeed = snapshot.VarJumpSpeed;
			autoJump = snapshot.AutoJump;
			if (transitionRow.State == "keep" && snapshot.State == 2)
			{
				State = 2;
				dashPhase = snapshot.DashPhase;
				DashDir = snapshot.DashDir;
				dashTimer = snapshot.DashTimer;
				dashAttackTimer = snapshot.DashAttackTimer;
				dashedDown = snapshot.DashedDown;
				hitThisDash.Clear();
			}
			else if (transitionRow.State == "keep")
			{
				State = ((snapshot.State != 1) ? snapshot.State : 0);
			}
			else
			{
				State = 0;
			}
			if (transitionRow.SpeedX == "zero")
			{
				Speed.x = 0f;
			}
			if (transitionRow.SpeedY == "max0")
			{
				Speed.y = Math.Max(0f, Speed.y);
			}
			else if (transitionRow.SpeedY == "set")
			{
				Speed.y = transitionRow.SpeedYValue;
				varJumpSpeed = Speed.y;
			}
			if (transitionRow.State != "keep")
			{
				varJumpTimer = transitionRow.VarJumpTime;
				autoJump = transitionRow.AutoJump;
				autoJumpTimer = 0f;
			}
			if (transitionRow.DashCooldown > 0f)
			{
				dashCooldownTimer = transitionRow.DashCooldown;
			}
			if (transitionRow.Refill)
			{
				RefillDash();
				Stamina = 110f;
			}
			wallSlideTimer = 1.2f;
			jumpGraceTimer = 0f;
			forceMoveXTimer = 0f;
		}
	}

	private void UpdateNailTrigger(HeroController h, bool on)
	{
		if (nailTrigger == null)
		{
			GameObject gameObject = new GameObject("MadelineNailTrigger");
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			gameObject.layer = 17;
			try
			{
				gameObject.tag = "Nail Attack";
			}
			catch (UnityException ex)
			{
				Plugin.Log.LogWarning("nail trigger tag: " + ex.Message);
			}
			nailBody = gameObject.AddComponent<Rigidbody2D>();
			nailBody.bodyType = RigidbodyType2D.Kinematic;
			nailTrigger = gameObject.AddComponent<BoxCollider2D>();
			nailTrigger.isTrigger = true;
			nailTrigger.enabled = false;
		}
		if (on)
		{
			HitBox(h, out var center, out var size);
			nailBody.position = center;
			nailBody.transform.position = center;
			nailTrigger.size = size;
		}
		if (nailTrigger.enabled != on)
		{
			nailTrigger.enabled = on;
		}
	}

	private void Deactivate(HeroController h, Rigidbody2D rb)
	{
		Active = false;
		if (nailTrigger != null)
		{
			nailTrigger.enabled = false;
		}
		if (State == 2)
		{
			State = 0;
		}
		SafetyTimer = 0f;
		Gliding = false;
		jumpAttack = false;
		pogoArmed = false;
		if (!(rb == null))
		{
			if (h != null && !h.IsGravityApplied)
			{
				rb.gravityScale = 0f;
				rb.linearVelocity = Vector2.zero;
				Speed = Vector2.zero;
			}
			else if (restoreGravity > 0f)
			{
				rb.gravityScale = restoreGravity;
			}
		}
	}

	private bool Solid(HeroController h, float dxPx, float dyPx)
	{
		Bounds bounds = Col(h).bounds;
		Vector2 vector = (Vector2)bounds.center + new Vector2(dxPx, 0f - dyPx) / 8f;
		Vector2 size = (Vector2)bounds.size - new Vector2(0.04f, 0.04f);
		int num = Physics2D.OverlapBoxNonAlloc(vector, size, 0f, overlap, 33562880);
		bool flag = dxPx != 0f && dyPx == 0f;
		int num2 = -1;
		for (int i = 0; i < num; i++)
		{
			Collider2D collider2D = overlap[i];
			if (!Blocks(Col(h), collider2D))
			{
				continue;
			}
			if (dxPx == 0f)
			{
				ColliderDistance2D colliderDistance2D = Col(h).Distance(collider2D);
				if (colliderDistance2D.isValid && colliderDistance2D.distance < 0f && Math.Abs(colliderDistance2D.normal.x) > Math.Abs(colliderDistance2D.normal.y))
				{
					continue;
				}
			}
			if (flag)
			{
				if (num2 < 0)
				{
					num2 = Physics2D.OverlapBoxNonAlloc(vector + new Vector2(0f, 0.375f), size, 0f, overlapStep, 33562880);
				}
				if (Array.IndexOf(overlapStep, collider2D, 0, num2) < 0)
				{
					continue;
				}
			}
			return true;
		}
		return false;
	}

	private bool SolidPoint(Vector2 p)
	{
		Collider2D collider2D = Physics2D.OverlapPoint(p, 33562880);
		if (collider2D != null)
		{
			return Blocks(Hero(), collider2D);
		}
		return false;
	}

	private static bool Blocks(Collider2D me, Collider2D o)
	{
		if (o.isTrigger || o == me)
		{
			return false;
		}
		if (me == null)
		{
			return true;
		}
		if (!Physics2D.GetIgnoreLayerCollision(me.gameObject.layer, o.gameObject.layer))
		{
			return !Physics2D.GetIgnoreCollision(me, o);
		}
		return false;
	}

	private static float RunBoost(HeroController h)
	{
		// Sprintmaster and Quickening: 15% each, 25% at most together (user 2026-10-21)
		bool isUsingQuickening = h.IsUsingQuickening;
		bool flag = ToolOn(Gameplay.SprintmasterTool);
		if (isUsingQuickening && flag)
		{
			return 1.25f;
		}
		if (isUsingQuickening || flag)
		{
			return 1.15f;
		}
		return 1f;
	}

	public static bool ToolOn(ToolItem tool)
	{
		try
		{
			return tool != null && tool.IsEquipped;
		}
		catch
		{
			return false;
		}
	}

	public static bool ToolOn(string name)
	{
		try
		{
			return ToolOn(ToolItemManager.GetToolByName(name));
		}
		catch
		{
			return false;
		}
	}

	private static float HitLock(HeroController h)
	{
		float num = 0.4f;
		if (Unlocked(Unlocks.SwiftStep))
		{
			num -= 0.1f;
		}
		if (ToolOn("Pinstress Tool"))
		{
			num -= 0.05f;
		}
		if (h.IsUsingQuickening)
		{
			num -= 0.05f;
		}
		return Mathf.Max(0f, num);
	}

	private static float LongclawMult()
	{
		if (!ToolOn(Gameplay.LongNeedleTool))
		{
			return 1f;
		}
		float x = Gameplay.LongNeedleMultiplier.x;
		if (!(x > 1f))
		{
			return 1f;
		}
		return x;
	}

	private void CircletHits(HeroController h)
	{
		if (!Gliding || !ToolOn(Gameplay.BrollySpikeTool))
		{
			if (circletHit.Count > 0)
			{
				circletHit.Clear();
			}
			return;
		}
		HitBox(h, out var center, out var size);
		int num = Physics2D.OverlapBoxNonAlloc(center, size, 0f, overlap, HitMask);
		for (int i = 0; i < num; i++)
		{
			Collider2D collider2D = overlap[i];
			if (collider2D == null || collider2D.transform.IsChildOf(h.transform))
			{
				continue;
			}
			IHitResponder hitResponder = FindResponder(collider2D);
			if (hitResponder is HealthManager && (!circletHit.TryGetValue(hitResponder, out var value) || !(Time.time - value < 0.4f)))
			{
				circletHit[hitResponder] = Time.time;
				HitInstance damageInstance = new HitInstance
				{
					Source = hitSource,
					IsFirstHit = true,
					AttackType = AttackTypes.Nail,
					NailElement = NailElements.None,
					DamageDealt = Damage(hitResponder),
					IsUsingNeedleDamageMult = true,
					StunDamage = 1f,
					Direction = 270f,
					MagnitudeMultiplier = 0f,
					Multiplier = 1f,
					HitEffectsType = EnemyHitEffectsProfile.EffectsTypes.Full,
					SilkGeneration = HitSilkGeneration.Full,
					IsHeroDamage = true
				};
				try
				{
					hitResponder.Hit(damageInstance);
				}
				catch (Exception ex)
				{
					Plugin.Log.LogWarning("circlet hit on " + collider2D.name + " failed: " + ex.Message);
				}
			}
		}
	}

	private static Collider2D Hero()
	{
		HeroController silentInstance = HeroController.UnsafeInstance;
		if (!(silentInstance != null))
		{
			return null;
		}
		return Col(silentInstance);
	}

	private void ApplyContacts(HeroController h)
	{
		if (Speed.x != 0f && Solid(h, Math.Sign(Speed.x), 0f) && !DashLedgeCorrect(h))
		{
			if (wallSpeedRetentionTimer <= 0f)
			{
				wallSpeedRetained = Speed.x;
				wallSpeedRetentionTimer = 0.06f;
			}
			Speed.x = 0f;
			dashAttackTimer = 0f;
		}
		if (Speed.y > 0f && Solid(h, 0f, 1f))
		{
			if (State == 2 && DashDir.x != 0f && DashDir.y > 0f)
			{
				DashDir = new Vector2(Math.Sign(DashDir.x), 0f);
				Speed.x *= Tech.Ultra.XMult;
				MoveStats.Ultras++;
				Ducking = true;
				TechTips.OnDashLanded();
			}
			if (State != 1)
			{
				float t = Math.Min(Speed.y / 240f, 1f);
				ScaleX = Mathf.Lerp(1f, 1.6f, t);
				ScaleY = Mathf.Lerp(1f, 0.4f, t);
				if (Speed.y >= 40f)
				{
					Sfx.PlaySurface("landing", 0.6f);
				}
				if (Speed.y >= 80f)
				{
					Particles.Dust(Feet(), -MathF.PI / 2f, 8);
				}
			}
			Speed.y = 0f;
		}
		else if (Speed.y < 0f && Solid(h, 0f, -1f) && !CornerCorrect(h))
		{
			Speed.y = 0f;
			varJumpTimer = 0f;
		}
	}

	private bool DashLedgeCorrect(HeroController h)
	{
		if (State != 2 || DashDir.y > 0f)
		{
			return false;
		}
		int num = Math.Sign(Speed.x);
		for (int i = 1; i <= 4; i++)
		{
			if (!Solid(h, num, -i) && !Solid(h, 0f, -i))
			{
				Rigidbody2D component = h.GetComponent<Rigidbody2D>();
				if (component == null)
				{
					return false;
				}
				component.position += new Vector2((float)num / 8f, (float)i / 8f);
				return true;
			}
		}
		return false;
	}

	private bool CornerCorrect(HeroController h)
	{
		int num = (int)((State == 2) ? 5f : 4f);
		for (int i = 1; i <= num; i++)
		{
			for (int j = -1; j <= 1; j += 2)
			{
				if ((j >= 0 || !(Speed.x > 0f)) && (j <= 0 || !(Speed.x < 0f)) && !Solid(h, j * i, -1f) && !Solid(h, j * i, 0f))
				{
					Rigidbody2D component = h.GetComponent<Rigidbody2D>();
					if (component == null)
					{
						return false;
					}
					component.position += new Vector2((float)(j * i) / 8f, 0f);
					Achievements.CornerCorrected();
					return true;
				}
			}
		}
		return false;
	}

	private void UpdateTimers(HeroController h, float dt)
	{
		if (jumpBuffer > 0f)
		{
			jumpBuffer -= dt;
		}
		if (dashBuffer > 0f)
		{
			dashBuffer -= dt;
		}
		if (SafetyTimer > 0f)
		{
			SafetyTimer -= dt;
		}
		if (pogoGraceTimer > 0f)
		{
			pogoGraceTimer -= dt;
		}
		if (HairFlashTimer > 0f)
		{
			HairFlashTimer -= dt;
		}
		wasOnGround = OnGround;
		OnGround = Speed.y >= 0f && Solid(h, 0f, 1f);
		if (WallSlideDir != 0)
		{
			wallSlideTimer = Math.Max(wallSlideTimer - dt, 0f);
			WallSlideDir = 0;
		}
		if (wallBoostTimer > 0f)
		{
			wallBoostTimer -= dt;
			if (moveX == wallBoostDir)
			{
				Speed.x = 130f * (float)moveX;
				Stamina += Tech.ClimbJump.StaminaCost;
				wallBoostTimer = 0f;
			}
		}
		if (OnGround && State != 1)
		{
			autoJump = false;
			Stamina = 110f;
			wallSlideTimer = 1.2f;
		}
		if (dashAttackTimer > 0f)
		{
			dashAttackTimer -= dt;
		}
		if (OnGround)
		{
			jumpGraceTimer = 0.1f * (AssistMode.ForgivingJumps ? 2f : 1f);
			airJumps = 0;
			wandererRefillReady = true;
		}
		else if (jumpGraceTimer > 0f)
		{
			jumpGraceTimer -= dt;
		}
		if (dashCooldownTimer > 0f)
		{
			dashCooldownTimer -= dt;
		}
		if (hitLockTimer > 0f)
		{
			hitLockTimer -= dt;
			if (hitLockTimer <= 0f && Dashes > 0)
			{
				HairFlashTimer = 0.12f;
			}
		}
		if (dashRefillCooldownTimer > 0f)
		{
			dashRefillCooldownTimer -= dt;
		}
		else if (OnGround)
		{
			RefillDash();
		}
		if ((AssistMode.AirDashes == 2 || AssistMode.DashCount == -1) && State != 2)
		{
			Dashes = MaxDashes;
		}
		if (AssistMode.InfiniteStamina)
		{
			Stamina = 110f;
		}
		else if (AssistMode.HalfStamina && Stamina > 55f)
		{
			Stamina = 55f;
		}
		if (varJumpTimer > 0f)
		{
			varJumpTimer -= dt;
		}
		if (autoJumpTimer > 0f)
		{
			if (autoJump)
			{
				autoJumpTimer -= dt;
				if (autoJumpTimer <= 0f)
				{
					autoJump = false;
				}
			}
			else
			{
				autoJumpTimer = 0f;
			}
		}
		if (forceMoveXTimer > 0f)
		{
			forceMoveXTimer -= dt;
			moveX = forceMoveX;
		}
		else
		{
			moveX = moveXInput;
		}
		moveY = moveYInput;
		if (grabHeld && !Unlocked(Unlocks.GrabClimb))
		{
			grabHeld = false;
		}
		if (moveX != 0 && State != 1)
		{
			Facing = moveX;
		}
		lastAim = ((aimInput == Vector2.zero) ? new Vector2(Facing, 0f) : aimInput);
		if (wallSpeedRetentionTimer > 0f)
		{
			if (Math.Sign(Speed.x) == -Math.Sign(wallSpeedRetained))
			{
				wallSpeedRetentionTimer = 0f;
			}
			else if (!Solid(h, Math.Sign(wallSpeedRetained), 0f))
			{
				Speed.x = wallSpeedRetained;
				wallSpeedRetentionTimer = 0f;
			}
			else
			{
				wallSpeedRetentionTimer -= dt;
			}
		}
	}

	public bool RefillDash()
	{
		int maxDashes = MaxDashes;
		if (Dashes < maxDashes)
		{
			Dashes = maxDashes;
			if (hitLockTimer <= 0f)
			{
				HairFlashTimer = 0.12f;
			}
			return true;
		}
		return false;
	}

	private int NormalUpdate(HeroController h, float dt)
	{
		if (!OnGround && Speed.y >= 0f && (moveX == Facing || grabHeld) && LedgeBoost(h))
		{
			return 0;
		}
		if (grabHeld && !IsTired && !Ducking && Speed.y >= 0f && Math.Sign(Speed.x) != -Facing)
		{
			if (ClimbCheck(h, Facing, 0f))
			{
				Ducking = false;
				return ClimbBegin(h);
			}
			if (moveY < 1)
			{
				for (int i = 1; i <= 2; i++)
				{
					if (!Solid(h, 0f, -i) && ClimbCheck(h, Facing, -i))
					{
						Ducking = false;
						return ClimbBegin(h);
					}
				}
			}
		}
		if (CanDash)
		{
			return StartDash();
		}
		if (Ducking)
		{
			if (OnGround && moveY != 1)
			{
				Ducking = false;
				ScaleX = 0.8f;
				ScaleY = 1.2f;
			}
		}
		else if (OnGround && moveY == 1 && Speed.y >= 0f)
		{
			Ducking = true;
			ScaleX = 1.4f;
			ScaleY = 0.6f;
		}
		bool inUpdraft = h.cState.inUpdraft;
		// the glide opens on a new Jump press in the air with no jump left, then stays while it's held (user
		// 2026-10-21: holding Jump after a jump or the double jump opened it by accident)
		if (OnGround || !jumpHeld)
		{
			glideArmed = false;
		}
		bool flag = Unlocked(Unlocks.Glide) && !OnGround && jumpHeld && glideArmed && varJumpTimer <= 0f && (Speed.y >= 0f || inUpdraft);
		if (flag && !Gliding && this.GlideStarted != null)
		{
			this.GlideStarted();
		}
		Gliding = flag;
		if (Ducking && OnGround)
		{
			Speed.x = Mathf.MoveTowards(Speed.x, 0f, 500f * AssistMode.Friction * (AssistMode.LowFriction ? 0.7f : 1f) * dt);
		}
		else
		{
			float num = (OnGround ? 1f : 0.65f);
			num *= AssistMode.Friction;
			float num2 = 90f * RunBoost(h);
			if (Gliding)
			{
				num2 = 108f;
				num *= 0.5f;
			}
			if (Math.Abs(Speed.x) > num2 && Math.Sign(Speed.x) == moveX)
			{
				Speed.x = Mathf.MoveTowards(Speed.x, num2 * (float)moveX, 400f * num * dt);
			}
			else
			{
				Speed.x = Mathf.MoveTowards(Speed.x, num2 * (float)moveX, 1000f * num * dt);
			}
		}
		if (Gliding)
		{
			maxFall = GlideFall(h);
		}
		else if (moveY == 1 && Speed.y >= 160f)
		{
			maxFall = Mathf.MoveTowards(maxFall, 240f, 300f * dt);
			float num3 = 200f;
			if (Speed.y >= num3)
			{
				float t = Math.Min(1f, (Speed.y - num3) / (240f - num3));
				ScaleX = Mathf.Lerp(1f, 0.5f, t);
				ScaleY = Mathf.Lerp(1f, 1.5f, t);
			}
		}
		else
		{
			maxFall = Mathf.MoveTowards(maxFall, 160f, 300f * dt);
		}
		if (!OnGround)
		{
			float num4 = maxFall;
			if (!Gliding && !AssistMode.NoWallSlide && (moveX == Facing || (moveX == 0 && grabHeld)) && moveY != 1)
			{
				if (Speed.y >= 0f && wallSlideTimer > 0f && Solid(h, Facing, 0f) && HandsOnWall(h, Facing, 0f))
				{
					Ducking = false;
					WallSlideDir = Facing;
				}
				if (WallSlideDir != 0)
				{
					num4 = Mathf.Lerp(160f, 20f, wallSlideTimer / 1.2f);
					if (wallSlideTimer / 1.2f > 0.65f)
					{
						WallSlideDust(WallSlideDir, dt);
					}
				}
			}
			float num5 = ((Math.Abs(Speed.y) < 40f && (jumpHeld || autoJump) && !Gliding) ? 0.5f : 1f);
			if (Gliding && inUpdraft)
			{
				Speed.y = Mathf.MoveTowards(Speed.y, (0f - UpdraftSpeed) * 8f, 450f * dt);
			}
			else
			{
				Speed.y = Mathf.MoveTowards(Speed.y, num4 * ((AssistMode.Gravity > 1f) ? (AssistMode.Gravity * 0.8f) : 1f), 900f * num5 * AssistMode.Gravity * dt);
			}
		}
		if (varJumpTimer > 0f)
		{
			if (autoJump || jumpHeld)
			{
				Speed.y = Math.Min(Speed.y, varJumpSpeed);
			}
			else
			{
				varJumpTimer = 0f;
			}
		}
		if (jumpBuffer > 0f)
		{
			if (jumpGraceTimer > 0f)
			{
				Jump(playSfx: true);
			}
			else if (WallJumpCheck(h, 1))
			{
				if (Facing == 1 && grabHeld && Stamina > 0f)
				{
					ClimbJump();
				}
				else if (DashAttacking && SuperWallJumpAngleCheck)
				{
					SuperWallJump(-1);
				}
				else
				{
					WallJump(-1);
				}
			}
			else if (WallJumpCheck(h, -1))
			{
				if (Facing == -1 && grabHeld && Stamina > 0f)
				{
					ClimbJump();
				}
				else if (DashAttacking && SuperWallJumpAngleCheck)
				{
					SuperWallJump(1);
				}
				else
				{
					WallJump(1);
				}
			}
			else if (AirJumpsAllowed() <= airJumps)
			{
				glideArmed = true;   // a press with no jump left opens the glide
			}
			else if (AirJumpsAllowed() > airJumps)
			{
				// the Faydown Cloak's double jump (user 2026-10-21), or Assist's Jump Count
				bool faydown = airJumps == 0 && Unlocked(Unlocks.SecondDash);
				airJumps++;
				Jump(playSfx: !faydown);
				if (faydown)
				{
					DoubleJumpFx(h);
				}
			}
		}
		if (AssistMode.Hiccups)
		{
			if (hiccupTimer <= 0f)
			{
				hiccupTimer = UnityEngine.Random.Range(0.9f, 1.6f);
			}
			else if ((hiccupTimer -= dt) <= 0f)
			{
				Speed.y = Math.Min(Speed.y, -120.75f);
				Speed.x += ((UnityEngine.Random.value < 0.5f) ? (-1f) : 1f) * 90f * 1.4f;
				varJumpTimer = 0f;
				ScaleX = 0.6f;
				ScaleY = 1.4f;
				Sfx.Play(Sounds.Jump.Id);
			}
		}
		return 0;
	}

	private void StartJumpCommon(float varTime)
	{
		jumpBuffer = 0f;
		jumpGraceTimer = 0f;
		varJumpTimer = varTime;
		autoJump = false;
		dashAttackTimer = 0f;
		wallSlideTimer = 1.2f;
		wallBoostTimer = 0f;
		ScaleX = 0.6f;
		ScaleY = 1.4f;
	}

	private void Jump(bool playSfx)
	{
		TechTips.OnJump();
		StartJumpCommon(0.2f);
		Speed.x += 40f * (float)moveX;
		Speed.y = -105f;
		varJumpSpeed = Speed.y;
		if (playSfx)
		{
			Sfx.Play(Sounds.Jump.Id);
			Particles.Dust(Feet(), -MathF.PI / 2f, 4);
		}
	}

	private Vector3 Feet()
	{
		if (hero == null)
		{
			return Vector3.zero;
		}
		Bounds bounds = Col(hero).bounds;
		return new Vector3(bounds.center.x, bounds.min.y, hero.transform.position.z);
	}

	private Vector3 Center()
	{
		return Feet() + new Vector3(0f, 0.6875f, 0f);
	}

	private void WallDust(int wallSide)
	{
		float x = 0.25f;
		if (wallSide == 1)
		{
			Particles.Dust(Center() + new Vector3(x, 0f, 0f), MathF.PI * -3f / 4f, 4);
		}
		else
		{
			Particles.Dust(Center() - new Vector3(x, 0f, 0f), -MathF.PI / 4f, 4);
		}
	}

	private void WallSlideDust(int dir, float dt)
	{
		wallDustTimer -= dt;
		if (!(wallDustTimer > 0f))
		{
			wallDustTimer = 0.01f;
			Particles.Dust(Center() + new Vector3((float)dir * 5f / 8f, -0.5f, 0f), -MathF.PI / 2f, 1);
		}
	}

	private void SuperJump()
	{
		TechRow techRow = (Ducking ? Tech.Hyper : Tech.Super);
		if (!Ducking)
		{
			MoveStats.Supers++;
		}
		else if (airDownDiag)
		{
			MoveStats.Wavedashes++;
		}
		else
		{
			MoveStats.Hypers++;
		}
		TechTips.OnTech((!Ducking) ? "super" : (airDownDiag ? "wavedash" : "hyper"));
		StartJumpCommon(techRow.VarJumpTime);
		Speed.x = techRow.SpeedX * (float)Facing * techRow.XMult;
		Speed.y = techRow.SpeedY * techRow.YMult;
		Ducking = false;
		varJumpSpeed = Speed.y;
		Sfx.Play(Sounds.Jump.Id);
		Sfx.Play(techRow.Sfx);
		Particles.Dust(Feet(), -MathF.PI / 2f, 4);
	}

	private bool WallJumpCheck(HeroController h, int dir)
	{
		if (!Unlocked(Unlocks.GrabClimb))
		{
			return false;
		}
		if (AssistMode.NoWallJump)
		{
			return false;
		}
		float num = ((DashAttacking && DashDir.x == 0f && DashDir.y == -1f) ? 5f : 5f);
		return Solid(h, (float)dir * num, 0f);
	}

	private void WallJump(int dir)
	{
		if (!AssistMode.NoWallJump)
		{
			MoveStats.WallJumps++;
			TechTips.OnJump();
			Ducking = false;
			StartJumpCommon(Tech.WallJump.VarJumpTime);
			if (moveX != 0)
			{
				forceMoveX = dir;
				forceMoveXTimer = 0.16f;
			}
			Speed.x = Tech.WallJump.SpeedX * (float)dir;
			Speed.y = Tech.WallJump.SpeedY;
			varJumpSpeed = Speed.y;
			Sfx.PlaySurface("landing", 0.5f);
			Sfx.Play((dir < 0) ? Sounds.JumpWallRight.Id : Sounds.JumpWallLeft.Id);
			WallDust(-dir);
		}
	}

	private void SuperWallJump(int dir)
	{
		MoveStats.WallBounces++;
		TechTips.OnTech("wallbounce");
		Ducking = false;
		StartJumpCommon(Tech.WallBounce.VarJumpTime);
		Speed.x = Tech.WallBounce.SpeedX * (float)dir;
		Speed.y = Tech.WallBounce.SpeedY;
		varJumpSpeed = Speed.y;
		Sfx.Play((dir < 0) ? Sounds.JumpWallRight.Id : Sounds.JumpWallLeft.Id);
		Sfx.Play(Sounds.JumpSuperwall.Id);
		WallDust(-dir);
	}

	private void ClimbJump()
	{
		if (AssistMode.NoClimbJump)
		{
			WallJump(-Facing);
			return;
		}
		if (!OnGround)
		{
			Stamina -= Tech.ClimbJump.StaminaCost;
		}
		Jump(playSfx: false);
		if (moveX == 0)
		{
			wallBoostDir = -Facing;
			wallBoostTimer = 0.2f;
		}
		Sfx.Play((Facing == 1) ? Sounds.JumpClimbRight.Id : Sounds.JumpClimbLeft.Id);
		WallDust(Facing);
	}

	private bool ClimbCheck(HeroController h, int dir, float yAdd)
	{
		if (Solid(h, (float)dir * 2f, yAdd))
		{
			return HandsOnWall(h, dir, 0f);
		}
		return false;
	}

	private bool HandsOnWall(HeroController h, int dir, float yAdd)
	{
		Bounds bounds = Col(h).bounds;
		float num = 0.25f;
		float x = ((dir == 1) ? (bounds.max.x + num / 2f) : (bounds.min.x - num / 2f));
		float y = bounds.min.y + bounds.size.y * 0.6f - yAdd / 8f;
		int num2 = Physics2D.OverlapBoxNonAlloc(new Vector2(x, y), new Vector2(num, bounds.size.y * 0.3f), 0f, overlapStep, 33562880);
		Collider2D me = Hero();
		for (int i = 0; i < num2; i++)
		{
			if (Blocks(me, overlapStep[i]))
			{
				return true;
			}
		}
		return false;
	}

	private int ClimbBegin(HeroController h)
	{
		autoJump = false;
		Speed.x = 0f;
		Speed.y *= 0.2f;
		wallSlideTimer = 1.2f;
		climbNoMoveTimer = 0.1f;
		wallBoostTimer = 0f;
		LastClimbMove = 0;
		Sfx.PlaySurface("grab");
		return 1;
	}

	private int ClimbUpdate(HeroController h, float dt)
	{
		climbNoMoveTimer -= dt;
		if (OnGround)
		{
			Stamina = 110f;
		}
		if (jumpBuffer > 0f && !Ducking)
		{
			if (moveX == -Facing)
			{
				WallJump(-Facing);
			}
			else
			{
				ClimbJump();
			}
			return 0;
		}
		if (CanDash)
		{
			return StartDash();
		}
		if (!grabHeld)
		{
			Sfx.Play(Sounds.GrabLetgo.Id);
			return 0;
		}
		if (moveY != 1 && LedgeBoost(h))
		{
			return 0;
		}
		if (!Solid(h, Facing, 0f) || !HandsOnWall(h, Facing, 0f))
		{
			if (Speed.y < 0f)
			{
				ClimbHop(h);
			}
			return 0;
		}
		float num = 0f;
		bool flag = false;
		if (climbNoMoveTimer <= 0f)
		{
			if (moveY == -1)
			{
				num = -45f;
				if (Solid(h, 0f, -1f))
				{
					if (Speed.y < 0f)
					{
						Speed.y = 0f;
					}
					num = 0f;
					flag = true;
				}
				else if (SlipCheck(h, 0f))
				{
					ClimbHop(h);
					return 0;
				}
			}
			else if (moveY == 1)
			{
				num = 80f;
				if (OnGround)
				{
					if (Speed.y > 0f)
					{
						Speed.y = 0f;
					}
					num = 0f;
				}
				else
				{
					WallSlideDust(Facing, dt);
				}
			}
			else
			{
				flag = true;
			}
		}
		else
		{
			flag = true;
		}
		LastClimbMove = Math.Sign(num);
		if (flag && SlipCheck(h, 0f))
		{
			num = 30f;
		}
		Speed.y = Mathf.MoveTowards(Speed.y, num, 900f * dt);
		if (moveY != 1 && Speed.y > 0f && !Solid(h, Facing, 1f))
		{
			Speed.y = 0f;
		}
		if (climbNoMoveTimer <= 0f)
		{
			if (LastClimbMove == -1)
			{
				Stamina -= 45.454544f * dt;
			}
			else if (LastClimbMove == 0)
			{
				Stamina -= 10f * dt;
			}
		}
		if (Stamina <= 0f)
		{
			return 0;
		}
		return 1;
	}

	private bool SlipCheck(HeroController h, float addY)
	{
		Bounds bounds = Col(h).bounds;
		float num = 0.25f;
		float x = ((Facing == 1) ? (bounds.max.x + num / 2f) : (bounds.min.x - num / 2f));
		Vector2 vector = new Vector2(x, bounds.max.y - (4f + addY) / 8f);
		if (!SolidStrip(vector, num))
		{
			return !SolidStrip(vector + new Vector2(0f, (4f - addY) / 8f), num);
		}
		return false;
	}

	private bool SolidStrip(Vector2 center, float width)
	{
		int num = Physics2D.OverlapBoxNonAlloc(center, new Vector2(width, 0.125f), 0f, overlap, 33562880);
		Collider2D me = Hero();
		for (int i = 0; i < num; i++)
		{
			if (Blocks(me, overlap[i]))
			{
				return true;
			}
		}
		return false;
	}

	private bool LedgeBoost(HeroController h)
	{
		if (TechRoom.Active)
		{
			return false;   // not in Celeste, so not in the Tech Tutorial (user 2026-10-20)
		}
		Bounds bounds = Col(h).bounds;
		float num = 0.125f;
		float x = ((Facing == 1) ? (bounds.max.x + 2f * num) : (bounds.min.x - 2f * num));
		Vector2 vector = new Vector2(x, bounds.max.y);
		if (SolidPoint(vector))
		{
			return false;
		}
		RaycastHit2D raycastHit2D = Physics2D.Raycast(vector, Vector2.down, bounds.size.y, 33562880);
		if (raycastHit2D.collider == null || !Blocks(Col(h), raycastHit2D.collider))
		{
			return false;
		}
		float num2 = raycastHit2D.point.y - bounds.min.y;
		if (num2 <= 0f || num2 > bounds.size.y * 0.6f)
		{
			return false;
		}
		Vector2 size = (Vector2)bounds.size - new Vector2(0.04f, 0.04f);
		float y = raycastHit2D.point.y + bounds.size.y / 2f + 2f * num;
		if (Occupied(new Vector2(bounds.center.x, y), size) || Occupied(new Vector2(bounds.center.x + (float)Facing * bounds.size.x, y), size))
		{
			return false;
		}
		float num3 = num2 * 8f + 2f;
		Speed.y = 0f - Mathf.Sqrt(1800f * num3);
		Speed.x = (float)Facing * 100f;
		forceMoveX = 0;
		forceMoveXTimer = 0.2f;
		varJumpTimer = 0f;
		Sfx.Play(Sounds.ClimbLedge.Id);
		return true;
	}

	private bool Occupied(Vector2 center, Vector2 size)
	{
		int num = Physics2D.OverlapBoxNonAlloc(center, size, 0f, overlapStep, 33562880);
		Collider2D me = Hero();
		for (int i = 0; i < num; i++)
		{
			if (Blocks(me, overlapStep[i]))
			{
				return true;
			}
		}
		return false;
	}

	private void ClimbHop(HeroController h)
	{
		Speed.x = (float)Facing * 100f;
		Speed.y = Math.Min(Speed.y, -120f);
		forceMoveX = 0;
		forceMoveXTimer = 0.2f;
		Sfx.Play(Sounds.ClimbLedge.Id);
	}

	public void UseSilkSkill(string tool)
	{
		Achievements.UsedNonDash();
		switch (tool)
		{
		default:
			return;
		case "Thread Sphere":
			Dashes = Math.Max(Dashes, MaxDashes);
			HairFlashTimer = 0.12f;
			Sfx.Play(Sounds.Refill.Id);
			break;
		case "Silk Charge":
			QueueSkillDash(2.2f, 1.25f, new Vector2(Facing, 0f));
			break;
		case "Silk Bomb":
			QueueSkillDash(2.8f, 1f, lastAim);
			break;
		}
		if (this.SkillUsed != null)
		{
			this.SkillUsed(tool);
		}
	}

	private void QueueSkillDash(float timeMult, float speedMult, Vector2 dir)
	{
		pendingSkillTime = timeMult;
		pendingSkillSpeed = speedMult;
		pendingSkillDir = dir;
		pendingSkillAt = Time.time;
	}

	private int StartDash()
	{
		wasDashB = Dashes == 2 && MaxDashes == 2;
		Dashes = Math.Max(0, Dashes - 1);
		dashBuffer = 0f;
		calledDashEvents = false;
		dashStartedOnGround = OnGround;
		if (hunterSecondPending)
		{
			hunterSecondPending = false;
			hunterLocked = true;   // this is the Hunter's second dash: no dashing for 1.5 s once it's over
			hunterLockWaitGround = true;
		}
		wandererChainDash = Wanderer && !OnGround && !wandererRefillReady;   // the dash the air refill gave back
		dashCooldownTimer = 0.2f;
		dashRefillCooldownTimer = 0.1f;
		wallSlideTimer = 1.2f;
		dashAttackTimer = 0.3f;
		beforeDashSpeed = Speed;
		Speed = Vector2.zero;
		DashDir = Vector2.zero;
		if (!OnGround && Ducking)
		{
			Ducking = false;
		}
		else if (!Ducking && moveY == 1)
		{
			Ducking = true;
		}
		dashPhase = 0;
		dashedDown = false;
		steered = false;
		pogoArmed = false;
		jumpAttack = false;
		hitThisDash.Clear();
		enemiesThisDash = 0;
		countedThisDash = false;
		if (this.DashBegan != null)
		{
			this.DashBegan();
		}
		return 2;
	}

	private int DashUpdate(HeroController h, float dt)
	{
		if (dashPhase == 0)
		{
			dashPhase = 1;
			dashTimer = 0.15f * (Unlocked(Unlocks.SwiftStep) ? 1.25f : 1f) * LongclawMult() * AssistMode.DashLength * (Wanderer ? 0.7f : 1f);   // Wanderer: a shorter, snappier dash
			Vector2 vector = CorrectDashPrecision((skillDash && skillDir != Vector2.zero) ? skillDir : lastAim);
			Vector2 speed = vector * 240f * 0.9f * (Wanderer ? 1.15f : 1f);   // Wanderer: faster and shorter, a snappier dash
			if (skillDash)
			{
				dashTimer *= skillTimeMult;
				speed *= skillSpeedMult;
			}
			if (Math.Sign(beforeDashSpeed.x) == Math.Sign(speed.x) && Math.Abs(beforeDashSpeed.x) > Math.Abs(speed.x))
			{
				speed.x = beforeDashSpeed.x;
			}
			Speed = speed;
			DashDir = vector;
			dashedDown = vector.y > 0f;
			MoveStats.Dashes++;
			LastDashTime = Time.time;
			DashNumber++;
			airDownDiag = !OnGround && vector.x != 0f && vector.y > 0f;
			if (!skillDash)
			{
				TechTips.OnDash(vector, OnGround, dashTimer, WallJumpCheck(h, 1) || WallJumpCheck(h, -1));
			}
			if (DashDir.x != 0f)
			{
				Facing = Math.Sign(DashDir.x);
			}
			Vector2 dashDir = DashDir;
			CallDashEvents();
			if (OnGround && DashDir.x != 0f && DashDir.y > 0f && Speed.y > 0f)
			{
				DashDir = new Vector2(Math.Sign(DashDir.x), 0f);
				Speed.y = 0f;
				Speed.x *= Tech.Ultra.XMult;
				MoveStats.Ultras++;
				Ducking = true;
			}
			if (this.DashLaunched != null)
			{
				this.DashLaunched(dashDir, DashDir);
			}
			if (this.Dashed != null)
			{
				this.Dashed();
			}
		}
		else
		{
			dashTimer -= dt;
			if (AssistMode.SuperDashing && aimInput != Vector2.zero && DashDir != Vector2.zero && !skillDash)
			{
				float num = Math.Min(Speed.magnitude, 216f);
				DashDir = ((Vector2)Vector3.RotateTowards(DashDir, aimInput.normalized, 7.853982f * dt, 0f)).normalized;
				Speed = DashDir * num;
				steered = true;
			}
			if (dashTimer <= 0f)
			{
				TechTips.OnDashEnd();
				autoJump = true;
				autoJumpTimer = 0f;
				if (DashDir.y <= 0f || steered)
				{
					Speed = DashDir * 160f * 0.9f;
				}
				steered = false;
				if (AssistMode.LowFriction && DashDir.x != 0f)
				{
					float num2 = (float)Math.Sign(DashDir.x) * 240f * 0.9f * Mathf.Lerp(0.85f, 0.45f, AssistMode.Friction / 0.4f);
					if (Math.Abs(num2) > Math.Abs(Speed.x))
					{
						Speed.x = num2;
					}
				}
				if (Speed.y < 0f)
				{
					Speed.y *= 0.75f;
				}
				SafetyTimer = 0.1f;
				skillDash = false;
				if (this.Dashed != null)
				{
					this.Dashed();
				}
				return 0;
			}
		}
		if (Math.Abs(DashDir.y) < 0.1f && jumpBuffer > 0f && jumpGraceTimer > 0f)
		{
			SuperJump();
			BeginJumpAttack();
			SafetyTimer = 0.1f;
			return 0;
		}
		if (SuperWallJumpAngleCheck)
		{
			if (jumpBuffer > 0f)
			{
				if (WallJumpCheck(h, 1))
				{
					SuperWallJump(-1);
					BeginJumpAttack();
					SafetyTimer = 0.1f;
					return 0;
				}
				if (WallJumpCheck(h, -1))
				{
					SuperWallJump(1);
					BeginJumpAttack();
					SafetyTimer = 0.1f;
					return 0;
				}
			}
		}
		else if (jumpBuffer > 0f)
		{
			if (WallJumpCheck(h, 1))
			{
				if (Facing == 1 && grabHeld && Stamina > 0f)
				{
					ClimbJump();
				}
				else
				{
					WallJump(-1);
				}
				SafetyTimer = 0.1f;
				return 0;
			}
			if (WallJumpCheck(h, -1))
			{
				if (Facing == -1 && grabHeld && Stamina > 0f)
				{
					ClimbJump();
				}
				else
				{
					WallJump(1);
				}
				SafetyTimer = 0.1f;
				return 0;
			}
		}
		return 2;
	}

	private void CallDashEvents()
	{
		if (!calledDashEvents)
		{
			calledDashEvents = true;
			bool flag = DashDir.y < 0f || (DashDir.y == 0f && DashDir.x > 0f);
			if (wasDashB)
			{
				Sfx.Play(flag ? Sounds.DashPinkRight.Id : Sounds.DashPinkLeft.Id);
			}
			else
			{
				Sfx.Play(flag ? Sounds.DashRight.Id : Sounds.DashLeft.Id);
			}
		}
	}

	private static Vector2 CorrectDashPrecision(Vector2 dir)
	{
		if (dir.x != 0f && Math.Abs(dir.x) < 0.001f)
		{
			dir.x = 0f;
			dir.y = Math.Sign(dir.y);
		}
		else if (dir.y != 0f && Math.Abs(dir.y) < 0.001f)
		{
			dir.y = 0f;
			dir.x = Math.Sign(dir.x);
		}
		return dir;
	}

	private void DashHits(HeroController h)
	{
		HitBox(h, out var center, out var size);
		int num = Physics2D.OverlapBoxNonAlloc(center, size, 0f, overlap, HitMask);
		Vector2 attackDir = AttackDir;
		float num2 = Mathf.Atan2(0f - attackDir.y, attackDir.x) * 57.29578f;
		if (num2 < 0f)
		{
			num2 += 360f;
		}
		bool flag = false;
		for (int i = 0; i < num; i++)
		{
			Collider2D collider2D = overlap[i];
			if (collider2D == null || collider2D.transform.IsChildOf(h.transform))
			{
				continue;
			}
			IHitResponder hitResponder = FindResponder(collider2D);
			if (hitResponder == null && BossRematch.Active && MadelineTouches.DevOn && missLogged.Add(collider2D.name))
			{
				Plugin.Log.LogInfo("Dash touched " + collider2D.name + " (layer " + collider2D.gameObject.layer + (collider2D.isTrigger ? ", trigger" : "") + ", parent " + (collider2D.transform.parent != null ? collider2D.transform.parent.name : "-") + ") but nothing there takes hits");
			}
			if (hitResponder != null && hitThisDash.Add(hitResponder))
			{
				bool bouncing = BounceEnabled && State == 2 && DashDir.y > 0f && hitResponder is HealthManager;
				if (bouncing)
				{
					bounceTarget = (HealthManager)hitResponder;
					bounceCollider = collider2D;
				}
				HitInstance damageInstance = new HitInstance
				{
					Source = hitSource,
					IsFirstHit = true,
					AttackType = AttackTypes.Nail,
					NailElement = NailElements.None,
					DamageDealt = (bouncing ? BounceDamage((HealthManager)hitResponder) : Damage(hitResponder)),   // (the Wanderer's +30% chain damage is gone, user 2026-10-22)
					IsUsingNeedleDamageMult = !bouncing,   // each enemy's own needle-level damage modifiers, as for Hornet's needle
					StunDamage = 1f,
					Direction = num2,
					MagnitudeMultiplier = 0f,
					Multiplier = 1f,
					HitEffectsType = EnemyHitEffectsProfile.EffectsTypes.Full,
					SilkGeneration = HitSilkGeneration.Full,
					IsHeroDamage = true
				};
				bool flag2 = hitResponder is BounceBalloon || hitResponder is BouncePod || IsBounceObject(hitResponder);
				if (flag2)
				{
					damageInstance.AttackType = AttackTypes.Spikes;
					damageInstance.SilkGeneration = HitSilkGeneration.None;
				}
				IHitResponder.HitResponse hitResponse;
				try
				{
					hitResponse = hitResponder.Hit(damageInstance);
				}
				catch (Exception ex)
				{
					Plugin.Log.LogWarning("dash hit on " + collider2D.name + " failed: " + ex.Message);
					continue;
				}
				if (hitResponse.response == IHitResponder.Response.DamageEnemy && hitResponder is HealthManager)
				{
					if (!bouncing && Hunter && State == 2 && !skillDash)
					{
						HunterRefill();   // a hit gives the dash back, once until she's been on the ground a second
					}
					if (!bouncing && Wanderer && State == 2 && !skillDash)
					{
						WandererBounce();   // every dash hit bounces her straight back off it
					}
					else if (!bouncing)
					{
						reboundFrom = collider2D;   // a pogo's bounce already takes her off it
						reboundFor = 0.4f;
						ReboundZones(((HealthManager)hitResponder).gameObject, collider2D);
					}
					flag = true;
					Achievements.DashEnemyHits(++enemiesThisDash);
				}
				if (flag2 && hitResponse.response != IHitResponder.Response.None)
				{
					Dashes = MaxDashes;
					HairFlashTimer = 0.12f;
					Sfx.Play(Sounds.Refill.Id, 0.7f);
				}
			}
		}
		// user 2026-10-19: with one dash every hit locks, as always. With two, the first hitting dash stays free for a
		// second hit, and the second hitting dash in a row (or the last dash) locks for twice as long; the ground's
		// instant refill doesn't reset the count, so ground dashes can't be spammed. Shorteners count double too.
		if (flag && !skillDash)
		{
			float lockFor = 0f;
			if (wandererChainHit)
			{
				wandererChainHit = false;   // the Wanderer's refill hit: no lock, so the next dash can follow
			}
			else if (GroundPoke)
			{
				lockFor = 2f * HitLock(h);
			}
			else if (MaxDashes < 2)
			{
				lockFor = HitLock(h);
			}
			else if (!countedThisDash)
			{
				countedThisDash = true;
				if (Time.time - lastHitDashAt > 1f)
				{
					hitStreak = 0;
				}
				hitStreak++;
				lastHitDashAt = Time.time;
				if (hitStreak >= 2 || Dashes <= 0)
				{
					hitStreak = 0;
					lockFor = 2f * HitLock(h);
				}
			}
			if (lockFor > 0f)
			{
				hitLockTimer = lockFor * (AssistMode.LongHitLock ? 2.5f : 1f);
			}
		}
		if (flag && State == 2 && DashDir.y > 0f)
		{
			if (BounceEnabled)
			{
				Bounce(h);
			}
			else
			{
				Pogo(null, h, refill: false);
			}
		}
	}

	// Bounce: dashing down (straight or diagonal) into an enemy bounces Madeline off it and gives
	// the dash back, for a sliver of damage (none to bosses). Bouncing off the same enemy again
	// without landing or grabbing a wall gives less each time, so nobody camps on a boss's head.
	// Off for now: the bounce is designed and kept here, but the release keeps the original pogo.
	private const bool BounceEnabled = false;

	private HealthManager bounceTarget;

	private HealthManager lastBounced;

	private int bounceChain;

	private Collider2D bounceCollider;

	private void Bounce(HeroController h)
	{
		if (bounceTarget != lastBounced)
		{
			bounceChain = 0;
		}
		lastBounced = bounceTarget;
		bounceChain++;
		// Only bosses weaken repeat bounces; normal enemies always give a full bounce.
		bool flag = AssistMode.UnlimitedBounces || bounceTarget == null || !IsBossLike(bounceTarget);
		float height = (flag ? 1f : ((bounceChain <= 1) ? 1f : ((bounceChain == 2) ? 0.8f : ((bounceChain == 3) ? 0.6f : 0.35f))));
		bool refill = flag || bounceChain <= 2;
		LiftClear(h);
		Pogo(null, h, refill, height);
		Speed.x = 0f;
		if (refill)
		{
			HairFlashTimer = 0.12f;
			Sfx.Play(Sounds.Refill.Id, 0.6f);
		}
	}

	private void ResetBounceChain()
	{
		if (OnGround || State == 1)
		{
			bounceChain = 0;
			lastBounced = null;
		}
	}

	private static bool IsBossLike(HealthManager hm)
	{
		if (!BigEnemyDeath(hm))
		{
			return (float)Math.Max(InitHp(hm), hm.hp) >= 150f;
		}
		return true;
	}

	// 1 damage per needle level: 1 at the start, 5 fully upgraded. Bosses too.
	private static int BounceDamage(HealthManager hm)
	{
		return 1 + Mathf.Clamp(PlayerData.instance.nailUpgrades, 0, 4);
	}

	// Bounce from on top of the enemy, not from inside it: lift Madeline so her feet sit just
	// above its top (at most a short way, so she can't be pushed through a ceiling).
	private void LiftClear(HeroController h)
	{
		try
		{
			if (bounceCollider == null)
			{
				return;
			}
			Bounds bounds = Col(h).bounds;
			float num = bounceCollider.bounds.max.y + 0.15f - bounds.min.y;
			if (num > 0f)
			{
				num = Math.Min(num, 1.5f);
				RaycastHit2D raycastHit2D = Physics2D.BoxCast(bounds.center, bounds.size * 0.9f, 0f, Vector2.up, num, 256);
				if (raycastHit2D.collider != null)
				{
					num = Math.Max(0f, raycastHit2D.distance - 0.05f);
				}
				h.transform.position += new Vector3(0f, num, 0f);
			}
		}
		catch (Exception)
		{
		}
	}

	// the needle's damage; the enemy's own modifiers for that needle level are applied by the game, as for Hornet
	// (user 2026-10-22: they replace the old 1.5x boss bonus, so a boss takes as many hits as it does from Hornet)
	private static int Damage(IHitResponder r)
	{
		return PlayerData.instance.nailDamage;
	}

	private static bool IsBounceObject(IHitResponder r)
	{
		MonoBehaviour monoBehaviour = r as MonoBehaviour;
		if (monoBehaviour == null || r is HealthManager)
		{
			return false;
		}
		Transform transform = monoBehaviour.transform;
		while (transform != null)
		{
			if (transform.name.IndexOf("Bounce", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			transform = transform.parent;
		}
		return false;
	}

	private void UpdatePogoArming()
	{
		ResetBounceChain();
		if (State == 2)
		{
			if (dashedDown)
			{
				pogoArmed = true;
			}
		}
		else if (State == 1 || Speed.y < 0f || OnGround)
		{
			pogoArmed = false;
		}
	}

	private void ProbeSpikes(HeroController h, float dt)
	{
		if (!PogoReady || Speed.y < 0f || pogoGraceTimer > 0f)
		{
			return;
		}
		Bounds bounds = Col(h).bounds;
		float num = 0.25f + Math.Max(0f, Speed.y) * dt / 8f;
		int num2 = Physics2D.OverlapBoxNonAlloc(new Vector2(bounds.center.x, bounds.min.y - num / 2f), new Vector2(bounds.size.x, num), 0f, pogoHits, -1);
		for (int i = 0; i < num2; i++)
		{
			Collider2D collider2D = pogoHits[i];
			if (collider2D == null || collider2D.transform.IsChildOf(h.transform))
			{
				continue;
			}
			if (IsBellBeast(collider2D))
			{
				ColliderDistance2D colliderDistance2D = Col(h).Distance(collider2D);
				if (colliderDistance2D.isValid && colliderDistance2D.distance <= Math.Max(0f, Speed.y) * dt / 8f + 0.0625f)
				{
					Pogo(collider2D.gameObject, h);
					break;
				}
			}
			else
			{
				DamageHero damageHero = collider2D.GetComponent<DamageHero>() ?? collider2D.GetComponentInParent<DamageHero>();
				if (!(damageHero == null) && damageHero.isActiveAndEnabled && Array.IndexOf(PogoHazards, damageHero.hazardType.ToString()) >= 0)
				{
					Pogo(damageHero.gameObject, h);
					break;
				}
			}
		}
	}

	private static bool IsBellBeast(Collider2D c)
	{
		if (PlayerData.instance == null || !PlayerData.instance.defeatedBellBeast)
		{
			return false;
		}
		int layer = c.gameObject.layer;
		if (c.isTrigger && layer != 19 && layer != 11)
		{
			return false;
		}
		if (layer == 13)
		{
			return false;
		}
		Transform transform = c.transform;
		while (transform != null)
		{
			string text = transform.name.Replace(" ", "").Replace("_", "");
			if (text.IndexOf("BoneBeast", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("BellBeast", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			transform = transform.parent;
		}
		return false;
	}

	public bool TryPogo(HazardType hazard, GameObject spikes, HeroController h)
	{
		if (!Active || Array.IndexOf(PogoHazards, hazard.ToString()) < 0)
		{
			return false;
		}
		if (pogoGraceTimer > 0f)
		{
			return true;
		}
		if (!PogoReady)
		{
			return false;
		}
		Pogo(spikes, h);
		return true;
	}

	private void Pogo(GameObject spikes, HeroController h, bool refill = true, float height = 1f)
	{
		Achievements.Pogoed();
		pogoArmed = false;
		PlaySpikeTink(spikes, h);
		State = 0;
		Ducking = false;
		Speed.y = -140f * height;
		varJumpTimer = 0.2f;
		varJumpSpeed = Speed.y;
		autoJump = true;
		autoJumpTimer = 0f;
		jumpGraceTimer = 0f;
		if (refill)
		{
			RefillDash();
			Stamina = 110f;
		}
		SafetyTimer = 0.1f;
		pogoGraceTimer = 0.1f;
		ScaleX = 0.6f;
		ScaleY = 1.4f;
		Sfx.Play(Sounds.Jump.Id);
	}

	private static void PlaySpikeTink(GameObject spikes, HeroController h)
	{
		if (spikes == null || spikes == null)
		{
			return;
		}
		TinkEffect tinkEffect = spikes.GetComponent<TinkEffect>() ?? spikes.GetComponentInParent<TinkEffect>() ?? spikes.GetComponentInChildren<TinkEffect>();
		GameObject gameObject = ((tinkEffect != null && tinkEffect.blockEffect != null) ? tinkEffect.blockEffect : Effects.TinkEffectDullPrefab);
		if (!(gameObject == null))
		{
			Bounds bounds = Col(h).bounds;
			GameObject gameObject2 = gameObject.Spawn(new Vector3(bounds.center.x, bounds.min.y, 0.002f), Quaternion.Euler(0f, 0f, 270f));
			AudioSource audioSource = ((gameObject2 != null) ? gameObject2.GetComponent<AudioSource>() : null);
			if (audioSource != null)
			{
				audioSource.pitch = UnityEngine.Random.Range(0.85f, 1.15f);
				audioSource.volume = 1f;
			}
		}
	}

	private void HitBox(HeroController h, out Vector2 center, out Vector2 size)
	{
		Bounds bounds = Col(h).bounds;
		Vector2 vector = new Vector2(AttackDir.x, 0f - AttackDir.y);
		float num = 0.75f;
		center = (Vector2)bounds.center + vector * (num / 2f);
		size = (Vector2)bounds.size + Vector2.one * 0.5f + new Vector2(Math.Abs(vector.x), Math.Abs(vector.y)) * num;
	}

	private static readonly HashSet<string> missLogged = new HashSet<string>();

	private static IHitResponder ProxyAbove(Transform t)
	{
		for (int i = 0; t != null && i < 3; i++, t = t.parent)
		{
			ReceivedDamageProxy p = t.GetComponent<ReceivedDamageProxy>();
			if (p != null && p.isActiveAndEnabled)
			{
				return p;
			}
		}
		return null;
	}

	private static IHitResponder FindResponder(Collider2D c)
	{
		Transform transform = c.transform;
		int num = 0;
		while (transform != null && num < 4)
		{
			IHitResponder component = transform.GetComponent<IHitResponder>();
			if (component != null && ((MonoBehaviour)component).isActiveAndEnabled)
			{
				return component;
			}
			if (component != null && !component.HitRecurseUpwards)
			{
				return null;
			}
			if (num == 0 && c.isTrigger && transform.GetComponent<DamageHero>() == null && transform.gameObject.layer != 11)
			{
				// a sensor trigger: not a body to hit, unless a script up the chain listens for hits (Father of the
				// Flame's braziers: ReceivedDamage on a parent, user 2026-10-17)
				return ProxyAbove(transform.parent);
			}
			num++;
			transform = transform.parent;
		}
		return null;
	}
}
