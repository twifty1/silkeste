using System;
using System.Collections.Generic;
using GlobalEnums;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Celeste's Player movement (Normal, Climb and Dash states) ported onto Silksong's hero rigidbody.
    /// Works in Celeste pixels with Y pointing down, exactly like Celeste.Player, and hands Unity a velocity
    /// of (Speed.x, -Speed.y) / px_per_unit each physics step. Unity's physics resolves the terrain contacts;
    /// the contacts are read back at the start of the next step as Celeste's OnCollideH / OnCollideV.
    /// </summary>
    public class MadelineController
    {
        public const int StNormal = 0, StClimb = 1, StDash = 2;

        static readonly AccessTools.FieldRef<HeroController, Rigidbody2D> Rb = AccessTools.FieldRefAccess<HeroController, Rigidbody2D>("rb2d");
        static readonly AccessTools.FieldRef<HeroController, Collider2D> Col = AccessTools.FieldRefAccess<HeroController, Collider2D>("col2d");
        static readonly AccessTools.FieldRef<HeroController, GameManager> Gm = AccessTools.FieldRefAccess<HeroController, GameManager>("gm");

        const int TerrainMask = 33562880;  // the mask HeroController uses for its own terrain rays (layers 8, 13, 25)
        const float BufferTime = 0.08f;    // Celeste VirtualButton buffer for Jump and Dash
        const float Px = Visuals.PxPerUnit;

        // Celeste.Player fields that the three states use.
        public int State;
        public Vector2 Speed;              // px/s, Y down
        public int Facing = 1;
        public int Dashes = 1;
        public float Stamina = Movement.ClimbStaminaMax;
        public bool Ducking, OnGround;
        public Vector2 DashDir;
        public int LastClimbMove, WallSlideDir;
        public bool Active;
        public float SafetyTimer;          // dash_attack.safety_frames_after_dash
        public float HairFlashTimer;
        public float ScaleX = 1, ScaleY = 1;  // Celeste's squash and stretch
        public event Action Dashed;
        /// <summary>unlocks.glide: true while she glides; GlideStarted fires as the jellyfish opens.</summary>
        public bool Gliding;
        public event Action GlideStarted;
        /// <summary>Lift speed of the updraft she is in (Silksong units/s), from HeroController.EnterUpdraft.</summary>
        public static float UpdraftSpeed;

        // Hornet's float sets her fall to FLOAT_SPEED (units/s, negative = down); in Celeste px/s, Y down.
        static bool floatLogged;
        static float GlideFall(HeroController h)
        {
            if (!floatLogged) { floatLogged = true; Plugin.Log.LogInfo("Glide: Hornet's FLOAT_SPEED " + h.FLOAT_SPEED + " units/s"); }
            return Math.Abs(h.FLOAT_SPEED) * Px;
        }
        /// <summary>Celeste's DashBegin (the freeze).</summary>
        public event Action DashBegan;
        /// <summary>Celeste's DashCoroutine launch: shake direction (as aimed) and slash direction (after an ultra), Celeste px, Y down.</summary>
        public event Action<Vector2, Vector2> DashLaunched;

        bool wasOnGround, autoJump, calledDashEvents, dashStartedOnGround;
        float jumpGraceTimer, varJumpTimer, varJumpSpeed, autoJumpTimer;
        float dashCooldownTimer, dashRefillCooldownTimer, dashAttackTimer, dashTimer;
        int dashPhase;                     // 0: DashBegin frame (Celeste's coroutine yields once), 1: moving
        Vector2 beforeDashSpeed;
        float wallSlideTimer = Movement.WallSlideTime, maxFall = Movement.MaxFall;
        int forceMoveX; float forceMoveXTimer;
        float climbNoMoveTimer;
        float wallSpeedRetentionTimer, wallSpeedRetained;
        float wallBoostTimer; int wallBoostDir;
        float jumpBuffer, dashBuffer;
        int moveX, moveY;
        Vector2 lastAim;
        bool jumpHeld, grabHeld;
        float restoreGravity = -1;
        readonly HashSet<object> hitThisDash = new HashSet<object>();
        int enemiesThisDash;   // achievements: Combo Breaker
        readonly Collider2D[] overlap = new Collider2D[32];
        GameObject hitSource;

        // ---- unlocks (sheets/unlocks.json) -------------------------------------------------------------------

        static readonly Dictionary<string, AccessTools.FieldRef<PlayerData, bool>> unlockFields = new Dictionary<string, AccessTools.FieldRef<PlayerData, bool>>();
        public static bool Unlocked(UnlockRow row)
        {
            AccessTools.FieldRef<PlayerData, bool> f;
            if (!unlockFields.TryGetValue(row.PlayerData, out f)) unlockFields[row.PlayerData] = f = AccessTools.FieldRefAccess<PlayerData, bool>(row.PlayerData);
            return PlayerData.instance != null && f(PlayerData.instance);
        }
        public int MaxDashes { get { int v = AssistMode.DashCount; if (v >= 0) return v; if (v == -1) return 3; return Unlocked(Unlocks.SecondDash) || AssistMode.AirDashes == 1 ? 2 : (int)Movement.DashCountMax; } }   // assist.dash_count overrides   // assist.air_dashes: Two
        bool wasDashB;

        public bool DashAttacking { get { return dashAttackTimer > 0f; } }
        public bool EnemyImmune { get { return Active && (State == StDash || (jumpAttack && jumpImmune) || SafetyTimer > 0f); } }

        // dash_attack.dash_jump_attack: a dash jumped out of (super, hyper, wavedash, wall bounce) keeps hitting;
        // dash_attack.dash_jump_immune: it also keeps the dash's enemy immunity.
        bool jumpAttack, jumpImmune;
        bool Attacking { get { return (State == StDash && dashPhase == 1) || jumpAttack; } }
        // Celeste px, Y down: the dash direction, or her motion during a dash jump.
        Vector2 AttackDir { get { return State == StDash || Speed == Vector2.zero ? DashDir : Speed.normalized; } }

        void BeginJumpAttack()
        {
            jumpAttack = DashAttack.DashJumpAttack;
            jumpImmune = DashAttack.DashJumpImmune;
        }
        public bool IsTired { get { return CheckStamina < Movement.ClimbTiredThreshold; } }
        float CheckStamina { get { return wallBoostTimer > 0f ? Stamina + Tech.ClimbJump.StaminaCost : Stamina; } }
        bool CanDash { get { return dashBuffer > 0f && dashCooldownTimer <= 0f && hitLockTimer <= 0f && Dashes > 0; } }

        // dash_attack.enemy_hit_dash_cooldown: after an enemy hit no dash for a moment, refilled or not.
        float hitLockTimer;
        public bool DashLocked { get { return hitLockTimer > 0f; } }
        bool SuperWallJumpAngleCheck { get { return Math.Abs(DashDir.x) <= 0.2f && DashDir.y <= -0.75f; } }

        /// <summary>Is the hero in plain gameplay, so Madeline drives it? (Cutscenes, benches, transitions, binds,
        /// Hornet's FSM moves such as Clawline, and hazard respawns leave the hero to the game.)</summary>
        public static bool ShouldDrive(HeroController h)
        {
            GameManager gm = Gm(h);
            return gm != null && gm.GameState == GameState.PLAYING && !gm.isPaused && h.acceptingInput && !h.controlReqlinquished
                && h.hero_state != ActorStates.no_input && h.hero_state != ActorStates.hard_landing
                && !h.cState.transitioning && !h.cState.dead && !h.cState.hazardDeath && !h.cState.hazardRespawning
                && !h.cState.isBinding && !h.cState.swimming && !h.playerData.atBench && !h.IsInputBlocked()
                && InteractManager.BlockingInteractable == null;
        }

        /// <summary>Called every rendered frame: input edges only exist in Update.</summary>
        public void ReadInput(HeroActions a)
        {
            float buffer = AssistMode.ForgivingJumps ? BufferTime * 2f : BufferTime;   // assist.forgiving_jumps
            if (a.Jump.WasPressed) jumpBuffer = buffer;
            if (a.Attack.WasPressed) dashBuffer = BufferTime;        // inputs.dash: Attack
            jumpHeld = a.Jump.IsPressed;
            grabHeld = a.Dash.IsPressed && !AssistMode.NoGrabbing;   // inputs.grab: Dash; assist.no_grabbing
            Vector2 v = a.MoveVector.Vector;
            if (AssistMode.MirrorMode) v.x = -v.x;                   // assist.mirror_mode: the world is flipped, so is left and right
            int mx = v.x > 0.3f ? 1 : v.x < -0.3f ? -1 : 0;
            int my = v.y < -0.5f ? 1 : v.y > 0.5f ? -1 : 0;           // Celeste MoveY: down = 1
            moveYInput = my; moveXInput = mx;
            // Celeste aims 8-way: snap the stick to the nearest 45 degrees.
            if (v.magnitude < 0.3f) aimInput = Vector2.zero;
            else
            {
                float ang = Mathf.Round(Mathf.Atan2(-v.y, v.x) / (Mathf.PI / 4f)) * (Mathf.PI / 4f);
                aimInput = CorrectDashPrecision(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)));
            }
        }
        int moveXInput, moveYInput; Vector2 aimInput;
        int airJumps; float hiccupTimer; bool steered;
        /// <summary>For assist.dash_colour_rush: when the last dash started, and how many so far.</summary>
        public static float LastDashTime = -10f; public static int DashNumber;

        /// <summary>Where a dash would go right now (Celeste px, Y down): the dash assist arrow.</summary>
        public Vector2 AimPreview { get { return aimInput == Vector2.zero ? new Vector2(Facing, 0f) : aimInput; } }

        /// <summary>Forget buffered presses (after a menu that stopped the game).</summary>
        public void ClearInput() { jumpBuffer = dashBuffer = 0f; }

        /// <summary>Input from a script instead of the player (the test build's scripted runs).</summary>
        public void InjectInput(int mx, int my, Vector2 aim, bool jumpPressed, bool jumpHeldNow, bool dashPressed, bool grab)
        {
            if (jumpPressed) jumpBuffer = BufferTime;
            if (dashPressed) dashBuffer = BufferTime;
            jumpHeld = jumpHeldNow; grabHeld = grab;
            moveXInput = mx; moveYInput = my;
            aimInput = aim == Vector2.zero ? Vector2.zero : CorrectDashPrecision(aim);
        }

        // hooks.bind_freeze: while Hornet's bind clips play (BindCharge / BindBurst), Madeline hangs exactly where
        // the bind started, in the air too, and comes out of it at rest.
        tk2dSpriteAnimator bindAnimator;
        bool bindFrozen;
        Vector2 bindPosition;

        bool FreezeForBind(HeroController h, Rigidbody2D rb)
        {
            if (bindAnimator == null || bindAnimator.gameObject != h.gameObject) bindAnimator = h.GetComponent<tk2dSpriteAnimator>();
            string clip = bindAnimator != null && bindAnimator.CurrentClip != null ? bindAnimator.CurrentClip.name : "";
            bool binding = clip.StartsWith("BindCharge", StringComparison.Ordinal) || clip.StartsWith("BindBurst", StringComparison.Ordinal);
            if (!binding)
            {
                if (bindFrozen && !Active && restoreGravity > 0) rb.gravityScale = restoreGravity;   // Silksong drives again
                bindFrozen = false;
                return false;
            }
            if (!bindFrozen) { bindFrozen = true; bindPosition = rb.position; }
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.position = bindPosition;
            Speed = Vector2.zero;
            return true;
        }

        /// <summary>One physics step, from the HeroController.FixedUpdate postfix (hooks.player_controller).</summary>
        public void Step(HeroController h, float dt)
        {
            Rigidbody2D rb = Rb(h);
            if (FreezeForBind(h, rb)) return;
            bool drive = ShouldDrive(h);
            if (!drive)
            {
                if (Active)
                {
                    if (h.cState.transitioning) SaveForTransition();   // leaving the room: keep the motion for the next one
                    Deactivate(h, rb);
                }
                return;
            }
            if (!Active) Activate(h, rb);
            if (hitSource == null)
            {
                hitSource = new GameObject("MadelineDashHit");
                hitSource.transform.SetParent(h.transform, false);
                try { hitSource.tag = DashAttack.NailTrigger; } catch (UnityException) { }   // ReceivedDamage checks the source tag
            }

            hero = h;
            // assist.madeline_speed: she lives a little faster than the world
            float k = AssistMode.MadelineSpeed; dt *= k;
            float fallBefore = Speed.y; bool groundBefore = OnGround;
            ApplyContacts(h);
            // assist.bouncy: landing springs her back up (hold down to stick it)
            if (AssistMode.Bouncy && !groundBefore && OnGround && fallBefore > 60f && moveYInput != 1 && State != StDash)
            {
                Speed.y = -Math.Max(fallBefore * 0.9f, -Movement.JumpSpeed); varJumpTimer = 0f; autoJump = true;
                ScaleX = 1.4f; ScaleY = 0.6f; Sfx.Play(Sounds.Jump.Id);
            }
            UpdateTimers(h, dt);
            // assist.wind: a steady push, but not while she holds a wall
            int wind = AssistMode.Wind;
            if (wind != 0 && State != StClimb) Speed.x += Math.Sign(wind) * Movement.RunAccel * (Math.Abs(wind) == 2 ? 0.8f : 0.45f) * dt;
            // a silk skill's Celeste move, queued by UseSilkSkill: a free dash of its own length and speed
            if (pendingSkillTime > 0f && Time.time - pendingSkillAt < 1f && State != StDash)
            {
                int keep = Dashes;
                State = StartDash();
                Dashes = keep;   // free: the skill paid in silk
                skillDash = true; skillTimeMult = pendingSkillTime; skillSpeedMult = pendingSkillSpeed; skillDir = pendingSkillDir;
                pendingSkillTime = 0f;
            }
            switch (State)
            {
                case StNormal: State = NormalUpdate(h, dt); break;
                case StClimb: State = ClimbUpdate(h, dt); break;
                case StDash: State = DashUpdate(h, dt); break;
            }
            if (jumpAttack && (State == StClimb || (State == StNormal && OnGround && Speed.y >= 0f))) jumpAttack = false;   // landed or grabbed a wall
            if (State != StNormal) Gliding = false;   // a dash or a grab puts the jellyfish away
            UpdatePogoArming();
            ProbeSpikes(h, dt);
            // Celeste UpdateSprite: skidding out of a ground dash against the dash direction kicks up dust.
            if (DashAttacking && OnGround && DashDir.y == 0f && !Ducking && Speed.x != 0f && moveX == -Math.Sign(Speed.x))
                Particles.Dust(Feet(), -Mathf.PI / 2f, 1);
            if (Attacking) DashHits(h);
            CircletHits(h);
            UpdateNailTrigger(h, Attacking);
            ScaleX = Mathf.MoveTowards(ScaleX, 1f, 1.75f * dt);
            ScaleY = Mathf.MoveTowards(ScaleY, 1f, 1.75f * dt);

            rb.gravityScale = 0f;
            Vector2 v = new Vector2(Speed.x, -Speed.y) / Px * k;
            if (State == StClimb) v.x = Facing * Movement.ClimbWallPress / Px;   // stay in contact with the wall
            rb.linearVelocity = v;
            if (Facing > 0 && !h.cState.facingRight) h.FaceRight();
            else if (Facing < 0 && h.cState.facingRight) h.FaceLeft();
        }

        void Activate(HeroController h, Rigidbody2D rb)
        {
            Active = true;
            restoreGravity = rb.gravityScale > 0 ? rb.gravityScale : h.DEFAULT_GRAVITY;
            Speed = new Vector2(rb.linearVelocity.x, -rb.linearVelocity.y) * Px;  // keep the entry momentum
            State = StNormal;
            Dashes = MaxDashes;
            Stamina = Movement.ClimbStaminaMax;
            Facing = h.cState.facingRight ? 1 : -1;
            dashBuffer = jumpBuffer = 0;
            ApplyTransition(Gm(h));
        }

        // ---- room transitions (sheets/transitions.json) ----------------------------------------------------

        struct Snapshot { public bool Valid; public float Time; public int State, Facing, Dashes, DashPhase; public Vector2 Speed, DashDir; public float DashTimer, DashAttackTimer, VarJumpTimer, VarJumpSpeed; public bool AutoJump, Ducking, DashedDown; }
        Snapshot exit;

        void SaveForTransition()
        {
            exit = new Snapshot
            {
                Valid = true, Time = UnityEngine.Time.unscaledTime, State = State, Facing = Facing, Dashes = Dashes, DashPhase = dashPhase,
                Speed = Speed, DashDir = DashDir, DashTimer = dashTimer, DashAttackTimer = dashAttackTimer,
                VarJumpTimer = varJumpTimer, VarJumpSpeed = varJumpSpeed, AutoJump = autoJump, Ducking = Ducking, DashedDown = dashedDown,
            };
        }

        // Celeste keeps the player's motion through a screen transition (Player.BeforeSide/Down/UpTransition + OnTransition).
        void ApplyTransition(GameManager gm)
        {
            Snapshot s = exit;
            exit.Valid = false;
            if (!s.Valid || UnityEngine.Time.unscaledTime - s.Time > 10f || gm == null || string.IsNullOrEmpty(gm.entryGateName)) return;
            string gate = gm.entryGateName.ToLowerInvariant();
            TransitionRow row = default(TransitionRow);
            bool found = false;
            foreach (TransitionRow r in Transitions.All)
                foreach (string g in r.EntryGates.Split(','))
                    if (gate.StartsWith(g, StringComparison.Ordinal)) { row = r; found = true; }
            if (!found) return;

            Facing = s.Facing; Dashes = s.Dashes; Ducking = s.Ducking;
            Speed = s.Speed;
            varJumpTimer = s.VarJumpTimer; varJumpSpeed = s.VarJumpSpeed; autoJump = s.AutoJump;
            if (row.State == "keep" && s.State == StDash)
            {
                State = StDash; dashPhase = s.DashPhase; DashDir = s.DashDir; dashTimer = s.DashTimer;
                dashAttackTimer = s.DashAttackTimer; dashedDown = s.DashedDown; hitThisDash.Clear();
            }
            else if (row.State == "keep") State = s.State == StClimb ? StNormal : s.State;
            else State = StNormal;
            if (row.SpeedX == "zero") Speed.x = 0f;
            if (row.SpeedY == "max0") Speed.y = Math.Max(0f, Speed.y);
            else if (row.SpeedY == "set") { Speed.y = row.SpeedYValue; varJumpSpeed = Speed.y; }
            if (row.State != "keep") { varJumpTimer = row.VarJumpTime; autoJump = row.AutoJump; autoJumpTimer = 0f; }
            if (row.DashCooldown > 0f) dashCooldownTimer = row.DashCooldown;
            if (row.Refill) { RefillDash(); Stamina = Movement.ClimbStaminaMax; }
            wallSlideTimer = Movement.WallSlideTime; jumpGraceTimer = 0f; forceMoveXTimer = 0f;
        }

        // dash_attack.nail_trigger: a needle-tagged trigger box for objects that react to the needle's collider
        // (PlayMaker "Nail Attack" triggers such as Mossberry bushes). Its own kinematic body, not part of the hero.
        BoxCollider2D nailTrigger;
        Rigidbody2D nailBody;
        void UpdateNailTrigger(HeroController h, bool on)
        {
            if (nailTrigger == null)
            {
                var go = new GameObject("MadelineNailTrigger");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.layer = (int)DashAttack.HitLayer;
                try { go.tag = DashAttack.NailTrigger; } catch (UnityException e) { Plugin.Log.LogWarning("nail trigger tag: " + e.Message); }
                nailBody = go.AddComponent<Rigidbody2D>();
                nailBody.bodyType = RigidbodyType2D.Kinematic;
                nailTrigger = go.AddComponent<BoxCollider2D>();
                nailTrigger.isTrigger = true;
                nailTrigger.enabled = false;
            }
            if (on)
            {
                Vector2 center, size;
                HitBox(h, out center, out size);
                nailBody.position = center;
                nailBody.transform.position = center;
                nailTrigger.size = size;
            }
            if (nailTrigger.enabled != on) nailTrigger.enabled = on;   // re-enabling fires OnTriggerEnter again each dash
        }

        void Deactivate(HeroController h, Rigidbody2D rb)
        {
            Active = false;
            if (nailTrigger != null) nailTrigger.enabled = false;
            if (State == StDash) State = StNormal;
            SafetyTimer = 0;
            Gliding = false;
            jumpAttack = false;
            pogoArmed = false;
            if (rb == null) return;
            // hooks.cutscene_gravity: a cutscene that holds Hornet in the air (silk hearts) switches her gravity off
            // with AffectedByGravity(false); keep it off and stop her there instead of handing back a falling body.
            if (h != null && !h.IsGravityApplied) { rb.gravityScale = 0f; rb.linearVelocity = Vector2.zero; Speed = Vector2.zero; return; }
            if (restoreGravity > 0) rb.gravityScale = restoreGravity;
        }

        // ---- collisions -------------------------------------------------------------------------------------

        bool Solid(HeroController h, float dxPx, float dyPx)
        {
            Bounds b = Col(h).bounds;
            Vector2 c = (Vector2)b.center + new Vector2(dxPx, -dyPx) / Px;
            Vector2 size = (Vector2)b.size - new Vector2(0.04f, 0.04f);
            int n = Physics2D.OverlapBoxNonAlloc(c, size, 0f, overlap, TerrainMask);
            bool sideways = dxPx != 0f && dyPx == 0f;
            int stepped = -1;
            for (int i = 0; i < n; i++)
            {
                Collider2D o = overlap[i];
                if (!Blocks(Col(h), o)) continue;
                // A box she already sits slightly inside from the side (Tilt Plat's particle copy sticks out 0.03 past
                // the real platform) is neither floor nor ceiling: otherwise she hangs beside it "on the ground".
                if (dxPx == 0f)
                {
                    ColliderDistance2D d = Col(h).Distance(o);
                    if (d.isValid && d.distance < 0f && Math.Abs(d.normal.x) > Math.Abs(d.normal.y)) continue;
                }
                // movement.wall_check_floor_tolerance: a floor that rose into her feet (a bobbing platform, or the
                // slope of a tilted one, whose bounding box would stand up like a wall) is not a wall if the same
                // box raised that far clears it.
                if (sideways)
                {
                    if (stepped < 0) stepped = Physics2D.OverlapBoxNonAlloc(c + new Vector2(0f, Movement.WallCheckFloorTolerance / Px), size, 0f, overlapStep, TerrainMask);
                    if (Array.IndexOf(overlapStep, o, 0, stepped) < 0) continue;
                }
                return true;
            }
            return false;
        }
        readonly Collider2D[] overlapStep = new Collider2D[32];

        bool SolidPoint(Vector2 p)
        {
            Collider2D c = Physics2D.OverlapPoint(p, TerrainMask);
            return c != null && Blocks(Hero(), c);
        }

        // Only what Hornet's own body collides with is solid to her: Silksong copies some platform boxes onto Soft
        // Terrain for particles (Tilt Plat's "terrain collider particle"), and the hero passes through those.
        static bool Blocks(Collider2D me, Collider2D o)
        {
            if (o.isTrigger || o == me) return false;
            if (me == null) return true;
            return !Physics2D.GetIgnoreLayerCollision(me.gameObject.layer, o.gameObject.layer) && !Physics2D.GetIgnoreCollision(me, o);
        }

        /// <summary>Flea Brew (Quickening) and Silkspeed Anklets (Sprintmaster) each make her run 1.5x; both together 1.75x.</summary>
        static float RunBoost(HeroController h)
        {
            bool brew = h.IsUsingQuickening, anklets = ToolOn(GlobalSettings.Gameplay.SprintmasterTool);
            return brew && anklets ? Movement.BrewAndAnkletsRunMult : brew ? Movement.FleaBrewRunMult : anklets ? Movement.AnkletsRunMult : 1f;
        }

        /// <summary>A Silksong tool equipped on the current crest.</summary>
        public static bool ToolOn(ToolItem tool) { try { return tool != null && tool.IsEquipped; } catch { return false; } }
        public static bool ToolOn(string name) { try { return ToolOn(ToolItemManager.GetToolByName(name)); } catch { return false; } }

        /// <summary>The lock after a dash hits an enemy: 0.4 s, minus 0.1 s for Swift Step, 0.05 s each for Pin Badge and an active Flea Brew (sheets/dash_attack.json).</summary>
        static float HitLock(HeroController h)
        {
            float lockTime = DashAttack.EnemyHitDashCooldown;
            if (Unlocked(Unlocks.SwiftStep)) lockTime -= DashAttack.SwiftStepHitCut;
            if (ToolOn("Pinstress Tool")) lockTime -= DashAttack.PinBadgeHitCut;
            if (h.IsUsingQuickening) lockTime -= DashAttack.FleaBrewHitCut;
            return Mathf.Max(0f, lockTime);
        }

        /// <summary>Longclaw lengthens Hornet's needle by Gameplay.LongNeedleMultiplier; Madeline's dash goes that much further.</summary>
        static float LongclawMult()
        {
            if (!ToolOn(GlobalSettings.Gameplay.LongNeedleTool)) return 1f;
            float m = GlobalSettings.Gameplay.LongNeedleMultiplier.x;
            return m > 1f ? m : 1f;
        }

        // Sawtooth Circlet (Gameplay.BrollySpikeTool): Hornet's float hurts what it touches, so the jellyfish glide does too.
        readonly Dictionary<IHitResponder, float> circletHit = new Dictionary<IHitResponder, float>();
        void CircletHits(HeroController h)
        {
            if (!Gliding || !ToolOn(GlobalSettings.Gameplay.BrollySpikeTool)) { if (circletHit.Count > 0) circletHit.Clear(); return; }
            Vector2 center, size;
            HitBox(h, out center, out size);
            int n = Physics2D.OverlapBoxNonAlloc(center, size, 0f, overlap, HitMask);
            for (int i = 0; i < n; i++)
            {
                Collider2D c = overlap[i];
                if (c == null || c.transform.IsChildOf(h.transform)) continue;
                IHitResponder r = FindResponder(c);
                if (!(r is HealthManager)) continue;
                float last;
                if (circletHit.TryGetValue(r, out last) && Time.time - last < DashAttack.CircletHitInterval) continue;
                circletHit[r] = Time.time;
                var hit = new HitInstance
                {
                    Source = hitSource, IsFirstHit = true, AttackType = AttackTypes.Nail, NailElement = NailElements.None,
                    DamageDealt = Damage(r), StunDamage = 1f, Direction = 270f, MagnitudeMultiplier = DashAttack.Knockback, Multiplier = 1f,
                    HitEffectsType = EnemyHitEffectsProfile.EffectsTypes.Full, SilkGeneration = HitSilkGeneration.Full, IsHeroDamage = true,
                };
                try { r.Hit(hit); } catch (Exception e) { Plugin.Log.LogWarning("circlet hit on " + c.name + " failed: " + e.Message); }
            }
        }

#if TESTHARNESS
        /// <summary>F4: what her own collision checks report right now, and the geometry of every box she overlaps.</summary>
        public string DebugCollision(HeroController h)
        {
            Bounds b = Col(h).bounds;
            var s = new System.Text.StringBuilder();
            s.AppendFormat("state={0} speed=({1:F0},{2:F0}) ground={3} solid L={4} R={5} down={6} up={7} hero=({8:F3},{9:F3})..({10:F3},{11:F3}) rbv={12}",
                State, Speed.x, Speed.y, OnGround, Solid(h, -1f, 0f), Solid(h, 1f, 0f), Solid(h, 0f, 1f), Solid(h, 0f, -1f),
                b.min.x, b.min.y, b.max.x, b.max.y, Rb(h).linearVelocity);
            int n = Physics2D.OverlapBoxNonAlloc(b.center, (Vector2)b.size + new Vector2(8f / Px, 8f / Px), 0f, overlap, TerrainMask);
            for (int i = 0; i < n; i++)
            {
                Collider2D o = overlap[i];
                var box = o as BoxCollider2D;
                s.AppendFormat("\n   box {0}/{1} layer={2} blocks={3} bounds=({4:F3},{5:F3})..({6:F3},{7:F3}) rot={8:F1} scale={9} size={10} offset={11} radius={12} touching={13}",
                    o.transform.parent ? o.transform.parent.name : "-", o.name, LayerMask.LayerToName(o.gameObject.layer), Blocks(Col(h), o),
                    o.bounds.min.x, o.bounds.min.y, o.bounds.max.x, o.bounds.max.y, o.transform.eulerAngles.z, o.transform.lossyScale,
                    box ? box.size.ToString("F3") : "-", box ? box.offset.ToString("F3") : "-", box ? box.edgeRadius.ToString("F3") : "-", Col(h).IsTouching(o));
                ColliderDistance2D d = Col(h).Distance(o);
                s.AppendFormat(" dist={0:F3} normal={1}", d.distance, d.normal);
            }
            return s.ToString();
        }
#endif

        static Collider2D Hero() { HeroController h = HeroController.SilentInstance; return h != null ? Col(h) : null; }

        // Celeste OnCollideH / OnCollideV, read from what Unity's contacts did to the last step.
        void ApplyContacts(HeroController h)
        {
            if (Speed.x != 0f && Solid(h, Math.Sign(Speed.x), 0f) && !DashLedgeCorrect(h))
            {
                if (wallSpeedRetentionTimer <= 0f) { wallSpeedRetained = Speed.x; wallSpeedRetentionTimer = Movement.WallSpeedRetentionTime; }
                Speed.x = 0f;
                dashAttackTimer = 0f;
            }
            if (Speed.y > 0f && Solid(h, 0f, 1f))
            {
                // Ultra / wavedash: a down-diagonal dash that touches ground becomes a ducking ground dash.
                if (State == StDash && DashDir.x != 0f && DashDir.y > 0f)
                {
                    DashDir = new Vector2(Math.Sign(DashDir.x), 0f);
                    Speed.x *= Tech.Ultra.XMult; MoveStats.Ultras++;
                    Ducking = true;
                }
                if (State != StClimb)
                {
                    float amount = Math.Min(Speed.y / 240f, 1f);
                    ScaleX = Mathf.Lerp(1f, 1.6f, amount); ScaleY = Mathf.Lerp(1f, 0.4f, amount);
                    if (Speed.y >= 40f) Sfx.PlaySurface("landing", 0.6f);
                    if (Speed.y >= Visuals.DustLandMinSpeed) Particles.Dust(Feet(), -Mathf.PI / 2f, (int)Visuals.DustLandCount);
                }
                Speed.y = 0f;
            }
            else if (Speed.y < 0f && Solid(h, 0f, -1f) && !CornerCorrect(h))
            {
                Speed.y = 0f;
                varJumpTimer = 0f;
            }
        }

        // Celeste Player.OnCollideH, dashing: a ledge top only a few pixels above her feet lifts her onto it instead of
        // stopping her dead (movement.dash_ledge_correct_px)
        bool DashLedgeCorrect(HeroController h)
        {
            if (State != StDash || DashDir.y > 0f) return false;
            int dir = Math.Sign(Speed.x);
            for (int i = 1; i <= (int)Movement.DashLedgeCorrectPx; i++)
            {
                if (Solid(h, dir, -i) || Solid(h, 0f, -i)) continue;
                var rb = h.GetComponent<Rigidbody2D>();
                if (rb == null) return false;
                rb.position += new Vector2(dir / Px, i / Px);
                return true;
            }
            return false;
        }

        // Celeste Player.OnCollideV, rising: a ceiling edge only a few pixels in the way shifts her sideways past it
        // (movement.corner_correct_px, _dash_px), toward the way she is moving, or either way when straight up.
        bool CornerCorrect(HeroController h)
        {
            int max = (int)(State == StDash ? Movement.CornerCorrectDashPx : Movement.CornerCorrectPx);
            for (int i = 1; i <= max; i++)
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    if ((dir < 0 && Speed.x > 0f) || (dir > 0 && Speed.x < 0f)) continue;
                    if (Solid(h, dir * i, -1f) || Solid(h, dir * i, 0f)) continue;
                    var rb = h.GetComponent<Rigidbody2D>();
                    if (rb == null) return false;
                    rb.position += new Vector2(dir * i / Px, 0f);
                    Achievements.CornerCorrected();
                    return true;
                }
            return false;
        }

        // ---- Celeste.Player.Update (the parts outside the state machine) ------------------------------------

        void UpdateTimers(HeroController h, float dt)
        {
            if (jumpBuffer > 0) jumpBuffer -= dt;
            if (dashBuffer > 0) dashBuffer -= dt;
            if (SafetyTimer > 0) SafetyTimer -= dt;
            if (pogoGraceTimer > 0) pogoGraceTimer -= dt;
            if (HairFlashTimer > 0) HairFlashTimer -= dt;

            wasOnGround = OnGround;
            OnGround = Speed.y >= 0f && Solid(h, 0f, 1f);
            if (WallSlideDir != 0) { wallSlideTimer = Math.Max(wallSlideTimer - dt, 0f); WallSlideDir = 0; }
            if (wallBoostTimer > 0f)
            {
                wallBoostTimer -= dt;
                if (moveX == wallBoostDir) { Speed.x = Movement.WallJumpHSpeed * moveX; Stamina += Tech.ClimbJump.StaminaCost; wallBoostTimer = 0f; }
            }
            if (OnGround && State != StClimb) { autoJump = false; Stamina = Movement.ClimbStaminaMax; wallSlideTimer = Movement.WallSlideTime; }
            if (dashAttackTimer > 0f) dashAttackTimer -= dt;
            if (OnGround) { jumpGraceTimer = Movement.CoyoteTime * (AssistMode.ForgivingJumps ? 2f : 1f); airJumps = 0; }
            else if (jumpGraceTimer > 0f) jumpGraceTimer -= dt;
            if (dashCooldownTimer > 0f) dashCooldownTimer -= dt;
            if (hitLockTimer > 0f)
            {
                hitLockTimer -= dt;
                if (hitLockTimer <= 0f && Dashes > 0) HairFlashTimer = Visuals.HairFlashTime;   // the dash is back
            }
            if (dashRefillCooldownTimer > 0f) dashRefillCooldownTimer -= dt;
            else if (OnGround) RefillDash();
            if ((AssistMode.AirDashes == 2 || AssistMode.DashCount == -1) && State != StDash) Dashes = MaxDashes;   // assist.air_dashes: Infinite
            if (AssistMode.InfiniteStamina) Stamina = Movement.ClimbStaminaMax;   // assist.infinite_stamina
            else if (AssistMode.HalfStamina && Stamina > Movement.ClimbStaminaMax / 2f) Stamina = Movement.ClimbStaminaMax / 2f;   // assist.half_stamina
            if (varJumpTimer > 0f) varJumpTimer -= dt;
            if (autoJumpTimer > 0f) { if (autoJump) { autoJumpTimer -= dt; if (autoJumpTimer <= 0f) autoJump = false; } else autoJumpTimer = 0f; }
            if (forceMoveXTimer > 0f) { forceMoveXTimer -= dt; moveX = forceMoveX; } else moveX = moveXInput;
            moveY = moveYInput;
            if (grabHeld && !Unlocked(Unlocks.GrabClimb)) grabHeld = false;   // grab and climb come with Cling Grip (unlocks.grab_climb)
            if (moveX != 0 && State != StClimb) Facing = moveX;
            lastAim = aimInput == Vector2.zero ? new Vector2(Facing, 0f) : aimInput;
            if (wallSpeedRetentionTimer > 0f)
            {
                if (Math.Sign(Speed.x) == -Math.Sign(wallSpeedRetained)) wallSpeedRetentionTimer = 0f;
                else if (!Solid(h, Math.Sign(wallSpeedRetained), 0f)) { Speed.x = wallSpeedRetained; wallSpeedRetentionTimer = 0f; }
                else wallSpeedRetentionTimer -= dt;
            }
        }

        public bool RefillDash()
        {
            int max = MaxDashes;
            if (Dashes < max) { Dashes = max; if (hitLockTimer <= 0f) HairFlashTimer = Visuals.HairFlashTime; return true; }
            return false;
        }

        // ---- Normal state (Celeste.Player.NormalUpdate) -----------------------------------------------------

        int NormalUpdate(HeroController h, float dt)
        {
            if (!OnGround && Speed.y >= 0f && (moveX == Facing || grabHeld) && LedgeBoost(h)) return StNormal;
            if (grabHeld && !IsTired && !Ducking && Speed.y >= 0f && Math.Sign(Speed.x) != -Facing)
            {
                if (ClimbCheck(h, Facing, 0f)) { Ducking = false; return ClimbBegin(h); }
                if (moveY < 1)
                    for (int i = 1; i <= 2; i++)
                        if (!Solid(h, 0f, -i) && ClimbCheck(h, Facing, -i)) { Ducking = false; return ClimbBegin(h); }
            }
            if (CanDash) return StartDash();

            if (Ducking) { if (OnGround && moveY != 1) { Ducking = false; ScaleX = 0.8f; ScaleY = 1.2f; } }
            else if (OnGround && moveY == 1 && Speed.y >= 0f) { Ducking = true; ScaleX = 1.4f; ScaleY = 0.6f; }

            // unlocks.glide: Celeste's jellyfish. Hold Jump while falling (or anywhere in an updraft) once the
            // rising part of a jump is over; letting go, landing, dashing or grabbing ends it.
            bool inUpdraft = h.cState.inUpdraft;
            bool glide = Unlocked(Unlocks.Glide) && !OnGround && jumpHeld && varJumpTimer <= 0f && (Speed.y >= 0f || inUpdraft);
            if (glide && !Gliding && GlideStarted != null) GlideStarted();
            Gliding = glide;

            // assist.low_friction: crouching doesn't brake either, she slides on (user 2026-10-14: more chaos)
            if (Ducking && OnGround) Speed.x = Mathf.MoveTowards(Speed.x, 0f, Movement.DuckFriction * AssistMode.Friction * (AssistMode.LowFriction ? 0.7f : 1f) * dt);
            else
            {
                float mult = OnGround ? 1f : Movement.AirMult;
                mult *= AssistMode.Friction;   // assist.low_friction: icy floors and slippery air, she slides a long way
                float max = Movement.RunMax * RunBoost(h);   // Flea Brew / Silkspeed Anklets
                if (Gliding) { max = Movement.GliderMaxRun; mult *= Movement.GliderRunMult; }
                if (Math.Abs(Speed.x) > max && Math.Sign(Speed.x) == moveX)
                    Speed.x = Mathf.MoveTowards(Speed.x, max * moveX, Movement.RunReduce * mult * dt);
                else
                    Speed.x = Mathf.MoveTowards(Speed.x, max * moveX, Movement.RunAccel * mult * dt);
            }

            if (Gliding) maxFall = GlideFall(h);   // hooks.float_speed: Hornet's own steady float, no faster or slower drop
            else if (moveY == 1 && Speed.y >= Movement.MaxFall)
            {
                maxFall = Mathf.MoveTowards(maxFall, Movement.FastMaxFall, Movement.FastMaxAccel * dt);
                // Celeste NormalUpdate: over the faster half of a fast fall she stretches thin and tall.
                float from = Movement.MaxFall + (Movement.FastMaxFall - Movement.MaxFall) * Visuals.FastFallStretchFrom;
                if (Speed.y >= from)
                {
                    float amount = Math.Min(1f, (Speed.y - from) / (Movement.FastMaxFall - from));
                    ScaleX = Mathf.Lerp(1f, Visuals.FastFallStretchX, amount);
                    ScaleY = Mathf.Lerp(1f, Visuals.FastFallStretchY, amount);
                }
            }
            else
                maxFall = Mathf.MoveTowards(maxFall, Movement.MaxFall, Movement.FastMaxAccel * dt);

            if (!OnGround)
            {
                float target = maxFall;
                if (!Gliding && !AssistMode.NoWallSlide && (moveX == Facing || (moveX == 0 && grabHeld)) && moveY != 1)   // Celeste: no wall slide while holding
                {
                    if (Speed.y >= 0f && wallSlideTimer > 0f && Solid(h, Facing, 0f) && HandsOnWall(h, Facing, 0f)) { Ducking = false; WallSlideDir = Facing; }
                    if (WallSlideDir != 0)
                    {
                        target = Mathf.Lerp(Movement.MaxFall, Movement.WallSlideStartMax, wallSlideTimer / Movement.WallSlideTime);
                        if (wallSlideTimer / Movement.WallSlideTime > Visuals.WallslideDustFraction) WallSlideDust(WallSlideDir, dt);
                    }
                }
                float grav = Math.Abs(Speed.y) < Movement.HalfGravThreshold && (jumpHeld || autoJump) && !Gliding ? 0.5f : 1f;
                // hooks.updraft_speed: gliding in an updraft rises at that updraft's own lift speed, so Hornet's routes work.
                if (Gliding && inUpdraft) Speed.y = Mathf.MoveTowards(Speed.y, -UpdraftSpeed * Px, Movement.UpdraftAccel * dt);
                else Speed.y = Mathf.MoveTowards(Speed.y, target * (AssistMode.Gravity > 1f ? AssistMode.Gravity * 0.8f : 1f), Movement.Gravity * grav * AssistMode.Gravity * dt);   // assist.gravity
            }

            if (varJumpTimer > 0f)
            {
                if (autoJump || jumpHeld) Speed.y = Math.Min(Speed.y, varJumpSpeed);
                else varJumpTimer = 0f;
            }

            if (jumpBuffer > 0f)
            {
                if (jumpGraceTimer > 0f) Jump(true);
                else if (WallJumpCheck(h, 1))
                {
                    if (Facing == 1 && grabHeld && Stamina > 0f) ClimbJump();
                    else if (DashAttacking && SuperWallJumpAngleCheck) SuperWallJump(-1);
                    else WallJump(-1);
                }
                else if (WallJumpCheck(h, -1))
                {
                    if (Facing == -1 && grabHeld && Stamina > 0f) ClimbJump();
                    else if (DashAttacking && SuperWallJumpAngleCheck) SuperWallJump(1);
                    else WallJump(1);
                }
                else if (AssistMode.JumpCount != 1 && (AssistMode.JumpCount < 0 || airJumps < AssistMode.JumpCount - 1))
                {
                    // assist.jump_count: Celeste's extra jumps in the air
                    airJumps++;
                    Jump(true);
                }
            }
            // assist.hiccups: every so often she hops by herself, as in Celeste
            if (AssistMode.Hiccups)
            {
                if (hiccupTimer <= 0f) hiccupTimer = UnityEngine.Random.Range(0.9f, 1.6f);
                else if ((hiccupTimer -= dt) <= 0f)
                {
                    // a full jump's kick upward and a shove to a random side (user 2026-10-14: "not nearly annoying enough")
                    Speed.y = Math.Min(Speed.y, Movement.JumpSpeed * 1.15f);
                    Speed.x += (UnityEngine.Random.value < 0.5f ? -1f : 1f) * Movement.RunMax * 1.4f;
                    varJumpTimer = 0f; ScaleX = 0.6f; ScaleY = 1.4f;
                    Sfx.Play(Sounds.Jump.Id);
                }
            }
            return StNormal;
        }

        // ---- jumps (Celeste.Player.Jump / SuperJump / WallJump / SuperWallJump / ClimbJump) ------------------

        void StartJumpCommon(float varTime)
        {
            jumpBuffer = 0f; jumpGraceTimer = 0f; varJumpTimer = varTime; autoJump = false;
            dashAttackTimer = 0f; wallSlideTimer = Movement.WallSlideTime; wallBoostTimer = 0f;
            ScaleX = 0.6f; ScaleY = 1.4f;
        }

        // Celeste's Jump(particles, playSfx): a climb jump turns both off and puffs off the wall instead.
        void Jump(bool playSfx)
        {
            StartJumpCommon(Movement.VarJumpTime);
            Speed.x += Movement.JumpHBoost * moveX;
            Speed.y = Movement.JumpSpeed;
            varJumpSpeed = Speed.y;
            if (playSfx) { Sfx.Play(Sounds.Jump.Id); Particles.Dust(Feet(), -Mathf.PI / 2f, (int)Visuals.DustJumpCount); }
        }

        // ---- dust positions (Celeste Position = bottom centre, Center = 5.5 px up) ------------------------

        HeroController hero;

        Vector3 Feet()
        {
            if (hero == null) return Vector3.zero;
            Bounds b = Col(hero).bounds;
            return new Vector3(b.center.x, b.min.y, hero.transform.position.z);
        }

        Vector3 Center() { return Feet() + new Vector3(0f, Visuals.DashFxCenterPx / Px, 0f); }

        // WallJump / SuperWallJump / ClimbJump: away from the wall on side wallSide (+1 = wall on the right).
        void WallDust(int wallSide)
        {
            float off = Visuals.DustWallJumpOffset / Px;
            if (wallSide == 1) Particles.Dust(Center() + new Vector3(off, 0f, 0f), -3f * Mathf.PI / 4f, (int)Visuals.DustJumpCount);
            else Particles.Dust(Center() - new Vector3(off, 0f, 0f), -Mathf.PI / 4f, (int)Visuals.DustJumpCount);
        }

        // Celeste.Player.CreateWallSlideParticles: one puff beside the hands, at most every 0.01 s.
        float wallDustTimer;
        void WallSlideDust(int dir, float dt)
        {
            wallDustTimer -= dt;
            if (wallDustTimer > 0f) return;
            wallDustTimer = Visuals.WallslideDustInterval;
            Particles.Dust(Center() + new Vector3(dir * Visuals.WallslideDustOffset / Px, -4f / Px, 0f), -Mathf.PI / 2f, 1);
        }

        void SuperJump()
        {
            TechRow t = Ducking ? Tech.Hyper : Tech.Super;
            if (!Ducking) MoveStats.Supers++; else if (airDownDiag) MoveStats.Wavedashes++; else MoveStats.Hypers++;
            StartJumpCommon(t.VarJumpTime);
            Speed.x = t.SpeedX * Facing * t.XMult;
            Speed.y = t.SpeedY * t.YMult;
            Ducking = false;
            varJumpSpeed = Speed.y;
            Sfx.Play(Sounds.Jump.Id);
            Sfx.Play(t.Sfx);
            Particles.Dust(Feet(), -Mathf.PI / 2f, (int)Visuals.DustJumpCount);
        }

        bool WallJumpCheck(HeroController h, int dir)
        {
            if (!Unlocked(Unlocks.GrabClimb)) return false;   // wall jumps (and wall bounces) come with Cling Grip too (user 2026-10-12)
            if (AssistMode.NoWallJump) return false;   // assist.no_wall_jump
            float dist = DashAttacking && DashDir.x == 0f && DashDir.y == -1f ? Movement.SuperWallJumpCheckDist : Movement.WallJumpCheckDist;
            return Solid(h, dir * dist, 0f);
        }

        bool airDownDiag;   // this dash started in the air, down-diagonal: a hyper out of it is a wavedash
        void WallJump(int dir)
        {
            if (AssistMode.NoWallJump) return;   // assist.no_wall_jump
            MoveStats.WallJumps++;
            Ducking = false;
            StartJumpCommon(Tech.WallJump.VarJumpTime);
            if (moveX != 0) { forceMoveX = dir; forceMoveXTimer = Movement.WallJumpForceTime; }
            Speed.x = Tech.WallJump.SpeedX * dir;
            Speed.y = Tech.WallJump.SpeedY;
            varJumpSpeed = Speed.y;
            Sfx.PlaySurface("landing", 0.5f);
            Sfx.Play(dir < 0 ? Sounds.JumpWallRight.Id : Sounds.JumpWallLeft.Id);
            WallDust(-dir);
        }

        void SuperWallJump(int dir)
        {
            MoveStats.WallBounces++;
            Ducking = false;
            StartJumpCommon(Tech.WallBounce.VarJumpTime);
            Speed.x = Tech.WallBounce.SpeedX * dir;
            Speed.y = Tech.WallBounce.SpeedY;
            varJumpSpeed = Speed.y;
            Sfx.Play(dir < 0 ? Sounds.JumpWallRight.Id : Sounds.JumpWallLeft.Id);
            Sfx.Play(Sounds.JumpSuperwall.Id);
            WallDust(-dir);
        }

        void ClimbJump()
        {
            if (AssistMode.NoClimbJump) { WallJump(-Facing); return; }   // assist.no_climb_jump: kicks off the wall instead
            if (!OnGround) Stamina -= Tech.ClimbJump.StaminaCost;
            Jump(false);
            if (moveX == 0) { wallBoostDir = -Facing; wallBoostTimer = Movement.ClimbJumpBoostTime; }
            Sfx.Play(Facing == 1 ? Sounds.JumpClimbRight.Id : Sounds.JumpClimbLeft.Id);
            WallDust(Facing);
        }

        // ---- Climb state (Celeste.Player.ClimbBegin / ClimbUpdate / ClimbHop) --------------------------------

        // Her hands are tested where they are now: the yAdd re-grab (Celeste moves her up there, this port doesn't)
        // would otherwise grab with the hands a pixel off the wall and let go again every frame.
        bool ClimbCheck(HeroController h, int dir, float yAdd) { return Solid(h, dir * 2f, yAdd) && HandsOnWall(h, dir, 0f); }

        // movement.climb_hand_*: (user 2026-10-11) her body is far taller than Celeste's, so a wall touching only
        // her feet or head is not one she can hold: the wall must be there at her hands, or she grabs thin air.
        bool HandsOnWall(HeroController h, int dir, float yAdd)
        {
            Bounds b = Col(h).bounds;
            float w = Movement.SlipProbeWidth / Px;
            float x = dir == 1 ? b.max.x + w / 2f : b.min.x - w / 2f;
            float y = b.min.y + b.size.y * Movement.ClimbHandHeight - yAdd / Px;
            int n = Physics2D.OverlapBoxNonAlloc(new Vector2(x, y), new Vector2(w, b.size.y * Movement.ClimbHandBand), 0f, overlapStep, TerrainMask);
            Collider2D me = Hero();
            for (int i = 0; i < n; i++) if (Blocks(me, overlapStep[i])) return true;
            return false;
        }

        int ClimbBegin(HeroController h)
        {
            autoJump = false;
            Speed.x = 0f;
            Speed.y *= Movement.ClimbGrabYMult;
            wallSlideTimer = Movement.WallSlideTime;
            climbNoMoveTimer = Movement.ClimbNoMoveTime;
            wallBoostTimer = 0f;
            LastClimbMove = 0;
            Sfx.PlaySurface("grab");
            return StClimb;
        }

        int ClimbUpdate(HeroController h, float dt)
        {
            climbNoMoveTimer -= dt;
            if (OnGround) Stamina = Movement.ClimbStaminaMax;
            if (jumpBuffer > 0f && (!Ducking))
            {
                if (moveX == -Facing) WallJump(-Facing); else ClimbJump();
                return StNormal;
            }
            if (CanDash) return StartDash();
            if (!grabHeld) { Sfx.Play(Sounds.GrabLetgo.Id); return StNormal; }
            if (moveY != 1 && LedgeBoost(h)) return StNormal;
            if (!Solid(h, Facing, 0f) || !HandsOnWall(h, Facing, 0f))
            {
                if (Speed.y < 0f) ClimbHop(h);
                return StNormal;
            }

            float target = 0f;
            bool still = false;
            if (climbNoMoveTimer <= 0f)
            {
                if (moveY == -1)
                {
                    target = Movement.ClimbUpSpeed;
                    if (Solid(h, 0f, -1f)) { if (Speed.y < 0f) Speed.y = 0f; target = 0f; still = true; }
                    else if (SlipCheck(h, 0f)) { ClimbHop(h); return StNormal; }
                }
                else if (moveY == 1)
                {
                    target = Movement.ClimbDownSpeed;
                    if (OnGround) { if (Speed.y > 0f) Speed.y = 0f; target = 0f; }
                    else WallSlideDust(Facing, dt);
                }
                else still = true;
            }
            else still = true;
            LastClimbMove = Math.Sign(target);
            if (still && SlipCheck(h, 0f)) target = Movement.ClimbSlipSpeed;
            Speed.y = Mathf.MoveTowards(Speed.y, target, Movement.ClimbAccel * dt);
            if (moveY != 1 && Speed.y > 0f && !Solid(h, Facing, 1f)) Speed.y = 0f;

            if (climbNoMoveTimer <= 0f)
            {
                if (LastClimbMove == -1) Stamina -= Movement.ClimbUpCost * dt;
                else if (LastClimbMove == 0) Stamina -= Movement.ClimbStillCost * dt;
            }
            if (Stamina <= 0f) return StNormal;
            return StClimb;
        }

        // Celeste SlipCheck: is the wall missing beside the head on the facing side? Celeste tests the single pixel
        // column next to its hitbox; here a strip slip_probe_width px wide, because Unity leaves a contact gap.
        bool SlipCheck(HeroController h, float addY)
        {
            Bounds b = Col(h).bounds;
            float w = Movement.SlipProbeWidth / Px;
            float x = Facing == 1 ? b.max.x + w / 2f : b.min.x - w / 2f;
            Vector2 p = new Vector2(x, b.max.y - (4f + addY) / Px);
            if (!SolidStrip(p, w)) return !SolidStrip(p + new Vector2(0f, (4f - addY) / Px), w);
            return false;
        }

        bool SolidStrip(Vector2 center, float width)
        {
            int n = Physics2D.OverlapBoxNonAlloc(center, new Vector2(width, 1f / Px), 0f, overlap, TerrainMask);
            Collider2D me = Hero();
            for (int i = 0; i < n; i++) if (Blocks(me, overlap[i])) return true;
            return false;
        }

        // movement.ledge_boost_*: (user 2026-10-11) her body is far taller than Celeste's, so a ledge that ends below
        // her chest (a thin floating platform) leaves no wall at her head for Celeste's climb hop, and her feet catch
        // on the corner. When the wall in front ends that low and she fits on top, she is boosted up onto it.
        bool LedgeBoost(HeroController h)
        {
            Bounds b = Col(h).bounds;
            float px = 1f / Px;
            float frontX = Facing == 1 ? b.max.x + 2f * px : b.min.x - 2f * px;
            // the wall's top in front of her: down from her head; starting inside a solid means the wall goes higher
            Vector2 from = new Vector2(frontX, b.max.y);
            if (SolidPoint(from)) return false;
            RaycastHit2D hit = Physics2D.Raycast(from, Vector2.down, b.size.y, TerrainMask);
            if (hit.collider == null || !Blocks(Col(h), hit.collider)) return false;
            float rise = hit.point.y - b.min.y;
            if (rise <= 0f || rise > b.size.y * Movement.LedgeBoostMaxHeight) return false;
            // room to stand up there, above her and on top of the ledge
            Vector2 size = (Vector2)b.size - new Vector2(0.04f, 0.04f);
            float upY = hit.point.y + b.size.y / 2f + Movement.LedgeBoostMargin * px;
            if (Occupied(new Vector2(b.center.x, upY), size) || Occupied(new Vector2(b.center.x + Facing * b.size.x, upY), size)) return false;
            float risePx = rise * Px + Movement.LedgeBoostMargin;
            Speed.y = -Mathf.Sqrt(2f * Movement.Gravity * risePx);
            Speed.x = Facing * Movement.ClimbHopX;
            forceMoveX = 0;
            forceMoveXTimer = Movement.ClimbHopForceTime;
            varJumpTimer = 0f;
            Sfx.Play(Sounds.ClimbLedge.Id);
            return true;
        }

        bool Occupied(Vector2 center, Vector2 size)
        {
            int n = Physics2D.OverlapBoxNonAlloc(center, size, 0f, overlapStep, TerrainMask);
            Collider2D me = Hero();
            for (int i = 0; i < n; i++) if (Blocks(me, overlapStep[i])) return true;
            return false;
        }

        void ClimbHop(HeroController h)
        {
            Speed.x = Facing * Movement.ClimbHopX;
            Speed.y = Math.Min(Speed.y, Movement.ClimbHopY);
            forceMoveX = 0;
            forceMoveXTimer = Movement.ClimbHopForceTime;
            Sfx.Play(Sounds.ClimbLedge.Id);
        }

        // ---- Dash state (Celeste.Player.StartDash / DashBegin / DashCoroutine / DashUpdate) -----------------

        // ---- silk skills as Celeste moves (user 2026-10-12): Silksong's own skill still happens ----------------
        float pendingSkillTime, pendingSkillSpeed, pendingSkillAt, skillTimeMult = 1f, skillSpeedMult = 1f;
        Vector2 pendingSkillDir, skillDir;
        public bool SkillDashing { get { return skillDash && State == StDash; } }
        bool skillDash;
        public event Action<string> SkillUsed;

        /// <summary>The silk skill a tool name stands for, as Madeline does it.</summary>
        public void UseSilkSkill(string tool)
        {
            Achievements.UsedNonDash();
            switch (tool)
            {
                case "Thread Sphere":   // Thread Storm: a spin that gives every dash back
                    Dashes = Math.Max(Dashes, MaxDashes);
                    HairFlashTimer = Visuals.HairFlashTime;
                    Sfx.Play(Sounds.Refill.Id);
                    break;
                case "Silk Charge":     // Sharpdart: a long, fast dash straight ahead that pierces everything
                    QueueSkillDash(2.2f, 1.25f, new Vector2(Facing, 0f));
                    break;
                case "Silk Bomb":       // Rune Rage: Celeste's red booster, launched where you aim
                    QueueSkillDash(2.8f, 1.0f, lastAim);
                    break;
                default: return;
            }
            if (SkillUsed != null) SkillUsed(tool);
        }

        void QueueSkillDash(float timeMult, float speedMult, Vector2 dir)
        {
            pendingSkillTime = timeMult; pendingSkillSpeed = speedMult; pendingSkillDir = dir; pendingSkillAt = Time.time;
        }

        int StartDash()
        {
            wasDashB = Dashes == 2;
            Dashes = Math.Max(0, Dashes - 1);
            dashBuffer = 0f;
            calledDashEvents = false;
            dashStartedOnGround = OnGround;
            dashCooldownTimer = Movement.DashCooldown;
            dashRefillCooldownTimer = Movement.DashRefillCooldown;
            wallSlideTimer = Movement.WallSlideTime;
            dashAttackTimer = Movement.DashAttackTime;
            beforeDashSpeed = Speed;
            Speed = Vector2.zero;
            DashDir = Vector2.zero;
            if (!OnGround && Ducking) Ducking = false;
            else if (!Ducking && moveY == 1) Ducking = true;
            dashPhase = 0;
            dashedDown = false; steered = false;
            pogoArmed = false;
            jumpAttack = false;
            hitThisDash.Clear(); enemiesThisDash = 0;
            if (DashBegan != null) DashBegan();
            return StDash;
        }

        int DashUpdate(HeroController h, float dt)
        {
            if (dashPhase == 0)
            {
                // Celeste's DashCoroutine body after its first "yield return null".
                dashPhase = 1;
                dashTimer = Movement.DashTime * (Unlocked(Unlocks.SwiftStep) ? Movement.SwiftStepDashTimeMult : 1f) * LongclawMult() * AssistMode.DashLength;   // unlocks.swift_step and Longclaw: a longer dash; assist.dash_length
                Vector2 dir = CorrectDashPrecision(skillDash && skillDir != Vector2.zero ? skillDir : lastAim);
                Vector2 speed = dir * Movement.DashSpeed * Movement.DashScale;
                if (skillDash) { dashTimer *= skillTimeMult; speed *= skillSpeedMult; }
                if (Math.Sign(beforeDashSpeed.x) == Math.Sign(speed.x) && Math.Abs(beforeDashSpeed.x) > Math.Abs(speed.x)) speed.x = beforeDashSpeed.x;
                Speed = speed;
                DashDir = dir;
                dashedDown = dir.y > 0f;
                MoveStats.Dashes++; LastDashTime = Time.time; DashNumber++; airDownDiag = !OnGround && dir.x != 0f && dir.y > 0f;
                if (DashDir.x != 0f) Facing = Math.Sign(DashDir.x);
                Vector2 shakeDir = DashDir;
                CallDashEvents();
                if (OnGround && DashDir.x != 0f && DashDir.y > 0f && Speed.y > 0f)
                {
                    DashDir = new Vector2(Math.Sign(DashDir.x), 0f);
                    Speed.y = 0f;
                    Speed.x *= Tech.Ultra.XMult; MoveStats.Ultras++;
                    Ducking = true;
                }
                if (DashLaunched != null) DashLaunched(shakeDir, DashDir);
                if (Dashed != null) Dashed();
            }
            else
            {
                dashTimer -= dt;
                // assist.super_dashing: Celeste's variant, the dash turns toward where you hold
                // (at plain dash speed, never more: carried speed made a steered dash zoom through rooms, user 2026-10-14)
                if (AssistMode.SuperDashing && aimInput != Vector2.zero && DashDir != Vector2.zero && !skillDash)
                {
                    float spd = Math.Min(Speed.magnitude, Movement.DashSpeed * Movement.DashScale);
                    Vector2 nd = Vector3.RotateTowards(DashDir, aimInput.normalized, Mathf.PI * 2.5f * dt, 0f);
                    DashDir = nd.normalized; Speed = DashDir * spd;
                    steered = true;
                }
                if (dashTimer <= 0f)
                {
                    autoJump = true;
                    autoJumpTimer = 0f;
                    if (DashDir.y <= 0f || steered) Speed = DashDir * Movement.DashEndSpeed * Movement.DashScale;   // a steered dash always slows at its end
                    steered = false;
                    // assist.low_friction: a sideways dash keeps much of its speed and glides on (a toned-down zoom, user 2026-10-14)
                    if (AssistMode.LowFriction && DashDir.x != 0f)
                    {
                        float keep = Math.Sign(DashDir.x) * Movement.DashSpeed * Movement.DashScale * Mathf.Lerp(0.85f, 0.45f, AssistMode.Friction / 0.4f);
                        if (Math.Abs(keep) > Math.Abs(Speed.x)) Speed.x = keep;
                    }
                    if (Speed.y < 0f) Speed.y *= Movement.DashEndUpMult;
                    SafetyTimer = DashAttack.SafetyFramesAfterDash;
                    skillDash = false;
                    if (Dashed != null) Dashed();
                    return StNormal;
                }
            }

            if (Math.Abs(DashDir.y) < 0.1f && jumpBuffer > 0f && jumpGraceTimer > 0f) { SuperJump(); BeginJumpAttack(); SafetyTimer = DashAttack.SafetyFramesAfterDash; return StNormal; }
            if (SuperWallJumpAngleCheck)
            {
                if (jumpBuffer > 0f)
                {
                    if (WallJumpCheck(h, 1)) { SuperWallJump(-1); BeginJumpAttack(); SafetyTimer = DashAttack.SafetyFramesAfterDash; return StNormal; }
                    if (WallJumpCheck(h, -1)) { SuperWallJump(1); BeginJumpAttack(); SafetyTimer = DashAttack.SafetyFramesAfterDash; return StNormal; }
                }
            }
            else if (jumpBuffer > 0f)
            {
                if (WallJumpCheck(h, 1))
                {
                    if (Facing == 1 && grabHeld && Stamina > 0f) ClimbJump(); else WallJump(-1);
                    SafetyTimer = DashAttack.SafetyFramesAfterDash;
                    return StNormal;
                }
                if (WallJumpCheck(h, -1))
                {
                    if (Facing == -1 && grabHeld && Stamina > 0f) ClimbJump(); else WallJump(1);
                    SafetyTimer = DashAttack.SafetyFramesAfterDash;
                    return StNormal;
                }
            }
            return StDash;
        }

        void CallDashEvents()
        {
            if (calledDashEvents) return;
            calledDashEvents = true;
            bool right = DashDir.y < 0f || (DashDir.y == 0f && DashDir.x > 0f);
            if (wasDashB) Sfx.Play(right ? Sounds.DashPinkRight.Id : Sounds.DashPinkLeft.Id);
            else Sfx.Play(right ? Sounds.DashRight.Id : Sounds.DashLeft.Id);
        }

        static Vector2 CorrectDashPrecision(Vector2 dir)
        {
            if (dir.x != 0f && Math.Abs(dir.x) < 0.001f) { dir.x = 0f; dir.y = Math.Sign(dir.y); }
            else if (dir.y != 0f && Math.Abs(dir.y) < 0.001f) { dir.y = 0f; dir.x = Math.Sign(dir.x); }
            return dir;
        }

        // ---- dash attack (sheets/dash_attack.json) ----------------------------------------------------------

        void DashHits(HeroController h)
        {
            Vector2 center, size;
            HitBox(h, out center, out size);
            int n = Physics2D.OverlapBoxNonAlloc(center, size, 0f, overlap, HitMask);
            Vector2 dir = AttackDir;
            float angle = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            bool hitEnemy = false;
            for (int i = 0; i < n; i++)
            {
                Collider2D c = overlap[i];
                if (c == null || c.transform.IsChildOf(h.transform)) continue;
                IHitResponder r = FindResponder(c);
                if (r == null || !hitThisDash.Add(r)) continue;   // dash_attack.hits_per_enemy_per_dash
                var hit = new HitInstance
                {
                    Source = hitSource,
                    IsFirstHit = true,
                    AttackType = AttackTypes.Nail,
                    NailElement = NailElements.None,
                    DamageDealt = Damage(r),                         // dash_attack.damage: one nail hit, more on bosses
                    StunDamage = 1f,
                    Direction = angle,
                    MagnitudeMultiplier = DashAttack.Knockback,     // dash_attack.knockback: 0
                    Multiplier = 1f,
                    HitEffectsType = EnemyHitEffectsProfile.EffectsTypes.Full,
                    SilkGeneration = HitSilkGeneration.Full,
                    IsHeroDamage = true,
                };
                // dash_attack.bouncer_refill: Hornet pogos off balloons and bounce pods with a down slash (a needle hit
                // that recoils or pulls her). Madeline dashes through instead: a Spikes hit, which they answer with
                // their wobble and sound only, and it gives her dash back like Celeste's refill crystal.
                bool bouncer = r is BounceBalloon || r is BouncePod || IsBounceObject(r);
                if (bouncer) { hit.AttackType = AttackTypes.Spikes; hit.SilkGeneration = HitSilkGeneration.None; }
                IHitResponder.HitResponse res;
                try { res = r.Hit(hit); }
                catch (Exception e) { Plugin.Log.LogWarning("dash hit on " + c.name + " failed: " + e.Message); continue; }
                if (res.response == IHitResponder.Response.DamageEnemy && r is HealthManager) { hitEnemy = true; Achievements.DashEnemyHits(++enemiesThisDash); }
                if (bouncer && res.response != IHitResponder.Response.None && DashAttack.BouncerRefill)
                {
                    Dashes = MaxDashes;
                    HairFlashTimer = Visuals.HairFlashTime;
                    Sfx.Play(Sounds.Refill.Id, 0.7f);
                }
#if TESTHARNESS
                Plugin.Log.LogInfo("[TEST] dash hit " + ((MonoBehaviour)r).name + " (" + r.GetType().Name + ") -> " + res.response);
#endif
            }
            if (hitEnemy && !skillDash) hitLockTimer = HitLock(h) * (AssistMode.LongHitLock ? 2.5f : 1f);   // assist.long_hit_lock
            // a down dash into an enemy bounces off it like spikes, but the dash stays locked (user 2026-10-14: in the base game)
            if (hitEnemy && State == StDash && DashDir.y > 0f) Pogo(null, h, false);   // a silk skill dash pierces on without the lock
            if (hitEnemy && DashAttack.DashRefillOnEnemyHit)
            {
                Dashes = MaxDashes;
                HairFlashTimer = Visuals.HairFlashTime;
                Sfx.Play(Sounds.Refill.Id, 0.7f);
            }
        }

        static readonly AccessTools.FieldRef<HealthManager, bool> BigEnemyDeath = AccessTools.FieldRefAccess<HealthManager, bool>("bigEnemyDeath");
        static readonly AccessTools.FieldRef<HealthManager, int> InitHp = AccessTools.FieldRefAccess<HealthManager, int>("initHp");

        /// <summary>A nail hit, times dash_attack.boss_damage_mult on bosses (hooks.boss_check).</summary>
        static int Damage(IHitResponder r)
        {
            int nail = PlayerData.instance.nailDamage;
            var hm = r as HealthManager;
            if (hm == null) return nail;
            bool boss = BigEnemyDeath(hm) || Math.Max(InitHp(hm), hm.hp) >= DashAttack.BossMinHp;
#if TESTHARNESS
            Plugin.Log.LogInfo("[TEST] " + hm.name + " hp " + hm.hp + "/" + InitHp(hm) + " big " + BigEnemyDeath(hm) + " boss " + boss);
#endif
            return boss ? Mathf.RoundToInt(nail * DashAttack.BossDamageMult) : nail;
        }

        // Other pogo objects by name (Hunter's March Ant_Bounce_Flower / Ant_Bounce_Bulb and the like), in case they
        // are not BouncePods: the responder or one of its parents is named with "Bounce". Enemies never count.
        static bool IsBounceObject(IHitResponder r)
        {
            var mb = r as MonoBehaviour;
            if (mb == null || r is HealthManager) return false;
            for (Transform t = mb.transform; t != null; t = t.parent)
                if (t.name.IndexOf("Bounce", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // ---- spike pogo (dash_attack.spike_pogo) -----------------------------------------------------------

        float pogoGraceTimer;
        bool dashedDown;
        bool pogoArmed;   // dash_attack.pogo_armed_until_landing
        static readonly string[] PogoHazards = DashAttack.PogoHazards.Split(',');
        readonly Collider2D[] pogoHits = new Collider2D[16];

        // dashedDown is kept for the whole dash: a down-diagonal dash that meets the floor is turned into a
        // ground dash (ultra) before the spikes are reached, so DashDir alone would miss it.
        bool PogoReady { get { return pogoArmed || (State == StDash && (dashedDown || (dashPhase == 0 && lastAim.y > 0f))); } }

        // Armed by a down dash; disarmed on landing, moving up, grabbing a wall or dashing another way.
        void UpdatePogoArming()
        {
            if (State == StDash) { if (dashedDown) pogoArmed = DashAttack.PogoArmedUntilLanding; return; }
            if (State == StClimb || Speed.y < 0f || OnGround) pogoArmed = false;
        }

        /// <summary>dash_attack.pogo_reach: while the pogo is ready, spikes just below her feet bounce her
        /// before she touches them, rather than waiting for Silksong's spike hit.</summary>
        void ProbeSpikes(HeroController h, float dt)
        {
            if (!DashAttack.SpikePogo || !PogoReady || Speed.y < 0f || pogoGraceTimer > 0f) return;
            Bounds b = Col(h).bounds;
            float depth = DashAttack.PogoReach / Px + Math.Max(0f, Speed.y) * dt / Px;
            int n = Physics2D.OverlapBoxNonAlloc(new Vector2(b.center.x, b.min.y - depth / 2f), new Vector2(b.size.x, depth), 0f, pogoHits, ~0);
            for (int i = 0; i < n; i++)
            {
                Collider2D c = pogoHits[i];
                if (c == null || c.transform.IsChildOf(h.transform)) continue;
                if (IsBellBeast(c))
                {
                    // dash_attack.bell_beast_pogo: no reach, only where her feet actually meet him (this step's fall)
                    ColliderDistance2D d = Col(h).Distance(c);
                    if (d.isValid && d.distance <= Math.Max(0f, Speed.y) * dt / Px + 0.5f / Px) { Pogo(c.gameObject, h); return; }
                    continue;
                }
                DamageHero spikes = c.GetComponent<DamageHero>() ?? c.GetComponentInParent<DamageHero>();
                if (spikes == null || !spikes.isActiveAndEnabled || Array.IndexOf(PogoHazards, spikes.hazardType.ToString()) < 0) continue;
                Pogo(spikes.gameObject, h);
                return;
            }
        }

        // dash_attack.bell_beast_pogo (user 2026-10-11): after the fight (PlayerData.defeatedBellBeast) Hornet can pogo
        // off the Bell Beast; Madeline's down dash bounces off him like off spikes. Only his body: no detector
        // or talk-range triggers, and nothing before he is beaten.
        static bool IsBellBeast(Collider2D c)
        {
            if (!DashAttack.BellBeastPogo || PlayerData.instance == null || !PlayerData.instance.defeatedBellBeast) return false;
            int layer = c.gameObject.layer;
            if (c.isTrigger && layer != (int)PhysLayers.INTERACTIVE_OBJECT && layer != (int)PhysLayers.ENEMIES) return false;
            if (layer == (int)PhysLayers.HERO_DETECTOR) return false;
            // In the game files he is "Bone Beast NPC"; Hornet pogos off its "Awake Hit Response" (F4, 2026-10-11).
            for (Transform t = c.transform; t != null; t = t.parent)
            {
                string n = t.name.Replace(" ", "").Replace("_", "");
                if (n.IndexOf("BoneBeast", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("BellBeast", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        /// <summary>Backup for spikes the probe missed: Silksong's own spike hit during a ready pogo bounces instead.</summary>
        public bool TryPogo(HazardType hazard, GameObject spikes, HeroController h)
        {
            if (!Active || !DashAttack.SpikePogo || Array.IndexOf(PogoHazards, hazard.ToString()) < 0) return false;
            if (pogoGraceTimer > 0f) return true;   // still touching the spikes we just bounced off
            if (!PogoReady) return false;
            Pogo(spikes, h);
            return true;
        }

        /// <summary>A downward dash into spikes bounces like Celeste's Player.Bounce instead of hurting.</summary>
        void Pogo(GameObject spikes, HeroController h, bool refill = true)
        {
            Achievements.Pogoed();
            pogoArmed = false;
            PlaySpikeTink(spikes, h);
            State = StNormal;
            Ducking = false;
            Speed.y = DashAttack.PogoSpeed;
            varJumpTimer = DashAttack.PogoVarJumpTime;
            varJumpSpeed = Speed.y;
            autoJump = true;
            autoJumpTimer = 0f;
            jumpGraceTimer = 0f;
            if (refill) { RefillDash(); Stamina = Movement.ClimbStaminaMax; }
            SafetyTimer = DashAttack.SafetyFramesAfterDash;
            pogoGraceTimer = DashAttack.SafetyFramesAfterDash;
            ScaleX = 0.6f; ScaleY = 1.4f;
            Sfx.Play(Sounds.Jump.Id);
        }

        // Silksong's own needle-on-spikes clang and spark: the spikes' TinkEffect.blockEffect, spawned under the
        // feet facing down, the way TinkEffect.TryDoTinkReactionNoDamager does for a down-slash pogo.
        static void PlaySpikeTink(GameObject spikes, HeroController h)
        {
            if (spikes == null) return;   // a pogo off an enemy (assist.pogo_everything)
            if (spikes == null) return;
            TinkEffect tink = spikes.GetComponent<TinkEffect>() ?? spikes.GetComponentInParent<TinkEffect>() ?? spikes.GetComponentInChildren<TinkEffect>();
            GameObject prefab = tink != null && tink.blockEffect != null ? tink.blockEffect : GlobalSettings.Effects.TinkEffectDullPrefab;
            if (prefab == null) return;
            Bounds b = Col(h).bounds;
            GameObject fx = prefab.Spawn(new Vector3(b.center.x, b.min.y, 0.002f), Quaternion.Euler(0f, 0f, 270f));
            AudioSource src = fx != null ? fx.GetComponent<AudioSource>() : null;
            if (src != null) { src.pitch = UnityEngine.Random.Range(0.85f, 1.15f); src.volume = 1f; }
#if TESTHARNESS
            Plugin.Log.LogInfo("[TEST] spike tink " + prefab.name + " sound=" + (src != null && src.clip != null ? src.clip.name : "none"));
#endif
        }

        // Only what the needle can touch: layers HERO_ATTACK collides with, plus hit_extra_layers (no detector layers).
        static int hitMask;
        static int HitMask
        {
            get
            {
                if (hitMask == 0)
                {
                    hitMask = Physics2D.GetLayerCollisionMask((int)DashAttack.HitLayer);
                    foreach (string l in DashAttack.HitExtraLayers.Split(','))
                        hitMask |= 1 << int.Parse(l.Trim(), System.Globalization.CultureInfo.InvariantCulture);
                }
                return hitMask;
            }
        }

        // The hero box grown by hit_box_padding, stretched hit_reach ahead in the dash direction.
        void HitBox(HeroController h, out Vector2 center, out Vector2 size)
        {
            Bounds b = Col(h).bounds;
            Vector2 dir = new Vector2(AttackDir.x, -AttackDir.y);   // Celeste Y down -> Unity Y up
            float reach = DashAttack.HitReach / Px;
            center = (Vector2)b.center + dir * (reach / 2f);
            size = (Vector2)b.size + Vector2.one * (DashAttack.HitBoxPadding * 2f / Px) + new Vector2(Math.Abs(dir.x), Math.Abs(dir.y)) * reach;
        }

        static IHitResponder FindResponder(Collider2D c)
        {
            Transform t = c.transform;
            for (int depth = 0; t != null && depth < 4; depth++, t = t.parent)
            {
                var r = t.GetComponent<IHitResponder>();
                if (r != null && ((MonoBehaviour)r).isActiveAndEnabled) return r;
                if (r != null && !r.HitRecurseUpwards) return null;
                if (depth == 0 && c.isTrigger && t.GetComponent<DamageHero>() == null && t.gameObject.layer != (int)PhysLayers.ENEMIES) return null;
            }
            return null;
        }
    }
}
