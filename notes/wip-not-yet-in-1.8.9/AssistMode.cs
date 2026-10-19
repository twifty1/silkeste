using System;
using System.Collections.Generic;
using System.IO;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Celeste's Assist Mode (sheets/assist.json): game speed, infinite stamina, air dashes, dash assist and
    /// invincibility. F5 (inputs.assist_menu), or holding Taunt on a controller, opens the menu; the game stops
    /// while it is open. The choice is kept in %LOCALAPPDATA%/MadelineInPharloom/assist.cfg.
    /// </summary>
    public class AssistMode : MonoBehaviour
    {
        public static GuiArt Art;
        public static string DataDir;

        static readonly Dictionary<string, int> chosen = new Dictionary<string, int>();
        static bool loaded;

        // ---- settings ----------------------------------------------------------------------------------------

        static string[] Values(AssistRow r) { return r.Values.Split(','); }
        // assist.look: four saved looks; every other Appearance row is kept per look ("look2.skin=Knight").
        static bool IsLookRow(AssistRow r) { return r.Section == "Appearance" && r.Id != AssistOptions.Look.Id && r.Id != AssistOptions.DashColourRush.Id && r.Id != AssistOptions.DashTrail.Id && r.Id != AssistOptions.GrannyLaugh.Id; }
        static int LookIndex { get { Load(); int i; return chosen.TryGetValue(AssistOptions.Look.Id, out i) ? i : 0; } }
        static string KeyFor(AssistRow r, int look) { return IsLookRow(r) ? "look" + (look + 1) + "." + r.Id : r.Id; }
        static string Key(AssistRow r) { return KeyFor(r, LookIndex); }
        // each look starts as one of the preset skins (visuals.look_default_skins)
        static int DefaultIndex(AssistRow r, int look)
        {
            if (r.Id == AssistOptions.Skin.Id)
            {
                string[] d = Visuals.LookDefaultSkins.Split(',');
                int k = Array.IndexOf(Values(r), d[Math.Min(look, d.Length - 1)]);
                if (k >= 0) return k;
            }
            return Array.IndexOf(Values(r), r.Default);
        }
        static int IndexFor(AssistRow r, int look)
        {
            Load();
            int i;
            return chosen.TryGetValue(KeyFor(r, look), out i) ? i : DefaultIndex(r, look);
        }
        static int Index(AssistRow r) { return IndexFor(r, IsLookRow(r) ? LookIndex : 0); }
        /// <summary>R in the Appearance section: the current look back to how it started.</summary>
        static void ResetLook()
        {
            string prefix = "look" + (LookIndex + 1) + ".";
            foreach (string k in new List<string>(chosen.Keys)) if (k.StartsWith(prefix, StringComparison.Ordinal)) chosen.Remove(k);
            Sfx.Play(Sounds.MenuToggleOff.Id);
        }
        /// <summary>The Switch Skin key: the next saved look.</summary>
        public static void NextLook() { chosen[AssistOptions.Look.Id] = (LookIndex + 1) % Values(AssistOptions.Look).Length; Save(); }
        public static bool CatEars { get { return Value(AssistOptions.CatEars) == "On"; } }
        /// <summary>assist.hair_length: how many hair nodes (Celeste's own is 4).</summary>
        public const int MaxHair = 8;
        public static int HairLength { get { return new[] { 3, 4, 6, 8 }[Math.Min(3, Index(AssistOptions.HairLength))]; } }
        /// <summary>assist.hair_size: hair node scale.</summary>
        public static float HairSize { get { return new[] { 0.8f, 1f, 1.3f }[Math.Min(2, Index(AssistOptions.HairSize))]; } }
        /// <summary>assist.hair_effect: node i of n in colour c (rainbow only while she has a dash).</summary>
        public static Color HairNodeColor(Color c, int i, int n, bool hasDash)
        {
            // assist.dash_colour_rush: just after each dash her hair bursts through the rainbow, then settles back
            if (DashColourRush)
            {
                float since = Time.time - MadelineController.LastDashTime;
                if (since < 1f)
                {
                    Color rc = Color.HSVToRGB(Mathf.Repeat(MadelineController.DashNumber * 0.17f + i * 0.08f + since * 1.5f, 1f), 0.75f, 1f);
                    rc.a = c.a; return Color.Lerp(rc, c, Mathf.Clamp01((since - 0.6f) / 0.4f));
                }
            }
            switch (Value(AssistOptions.HairEffect))
            {
                case "Rainbow":
                    if (!hasDash) return c;
                    Color r = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.4f + i * 0.09f, 1f), 0.65f, 1f); r.a = c.a; return r;
                case "Light Tips": return Color.Lerp(c, new Color(1f, 1f, 1f, c.a), 0.55f * i / Math.Max(1, n - 1));
                case "Dark Tips": return Color.Lerp(c, new Color(0.1f, 0.08f, 0.12f, c.a), 0.55f * i / Math.Max(1, n - 1));
            }
            return c;
        }
        static string Value(AssistRow r) { return Values(r)[Index(r)]; }

        /// <summary>Time scale for the whole game: 0.5 .. 1.</summary>
        /// <summary>Celeste's Assist Mode switch, set on the save file screen (user 2026-10-14). Off: every Assist Mode
        /// option is ignored. It starts on when any option was already set, so nobody loses what they chose.</summary>
        public static bool AssistOn
        {
            get { return true; }   // the switch left the save screen (user 2026-10-14): the options always apply as set
            set { Load(); chosen["assist_on"] = value ? 1 : 0; Save(); }
        }
        static bool AnyRaw { get { foreach (AssistRow r in AssistOptions.All) if (r.Section == "Assist Mode" && Value(r) != r.Default) return true; return false; } }
        public static float GameSpeed { get { if (!AssistOn) return 1f; return float.Parse(Value(AssistOptions.GameSpeed).TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture) / 100f; } }
        public static bool InfiniteStamina { get { return AssistOn && Value(AssistOptions.InfiniteStamina) == "On"; } }
        /// <summary>0 default, 1 two, 2 infinite.</summary>
        public static int AirDashes { get { return !AssistOn ? 0 : Index(AssistOptions.AirDashes); } }
        public static bool DashAssist { get { return AssistOn && Value(AssistOptions.DashAssist) == "On"; } }
        public static bool Invincible { get { return AssistOn && Value(AssistOptions.Invincibility) == "On"; } }
        public static bool ForgivingJumps { get { return Value(AssistOptions.ForgivingJumps) == "On"; } }
        // Celeste's Variant Mode (user 2026-10-14)
        public static bool MirrorMode { get { return Value(AssistOptions.MirrorMode) == "On" || ChaosMode.Is("Mirror Mode"); } }
        public static bool Chaos { get { return Value(AssistOptions.ChaosMode) == "On"; } }
        public static bool InstantRespawn { get { return Value(AssistOptions.InstantRespawn) == "On"; } }
        public static bool GrannyLaugh { get { return Value(AssistOptions.GrannyLaugh) == "On"; } }
        /// <summary>assist.low_friction (Friction): 1 normal, down to near nothing on Ice.</summary>
        public static float Friction { get { if (ChaosMode.Is("Ice")) return 0.04f; return new[] { 1f, 0.4f, 0.12f, 0.04f }[Math.Min(3, Index(AssistOptions.LowFriction))]; } }
        public static bool LowFriction { get { return Friction < 1f; } }
        /// <summary>Jumps before landing: 1 normal, 2, 3, or -1 for infinite.</summary>
        public static int JumpCount { get { int i = Index(AssistOptions.JumpCount); return i >= 3 ? -1 : i + 1; } }
        /// <summary>assist.dash_length: how long each dash lasts (so how far it goes).</summary>
        public static float DashLength { get { return new[] { 0.7f, 1f, 1.35f, 1.7f }[Math.Min(3, Index(AssistOptions.DashLength))]; } }
        public static float Gravity { get { if (ChaosMode.Is("Low Gravity")) return 0.55f; if (ChaosMode.Is("High Gravity")) return 1.6f; return new[] { 0.55f, 1f, 1.6f }[Math.Min(2, Index(AssistOptions.Gravity))]; } }
        /// <summary>assist.dash_count: -2 default (no change), -1 infinite, else that many.</summary>
        public static int DashCount { get { int i = Index(AssistOptions.DashCount); return i == 0 ? -2 : i >= 5 ? -1 : i - 1; } }
        public static bool NoClimbJump { get { return Value(AssistOptions.NoClimbJump) == "On"; } }
        public static bool NoWallJump { get { return Value(AssistOptions.NoWallJump) == "On"; } }
        /// <summary>assist.wind: -2..2 (strong left .. strong right).</summary>
        public static int Wind { get { if (ChaosMode.Is("Wind Left")) return -1; if (ChaosMode.Is("Wind Right")) return 1; return new[] { 0, -1, 1, -2, 2 }[Math.Min(4, Index(AssistOptions.Wind))]; } }
        public static float MadelineSpeed { get { if (ChaosMode.Is("Fast Madeline")) return 1.5f; return new[] { 1f, 1.25f, 1.5f }[Math.Min(2, Index(AssistOptions.MadelineSpeed))]; } }
        public static bool OneHit { get { return Value(AssistOptions.OneHit) == "On"; } }
        public static bool Bouncy { get { return Value(AssistOptions.Bouncy) == "On" || ChaosMode.Is("Bouncy"); } }
        // Challenges (user 2026-10-14)
        public static bool HalfStamina { get { return Value(AssistOptions.HalfStamina) == "On"; } }
        public static bool NoWallSlide { get { return Value(AssistOptions.NoWallSlide) == "On"; } }
        public static bool LongHitLock { get { return Value(AssistOptions.LongHitLock) == "On"; } }
        public static bool DashColourRush { get { return Value(AssistOptions.DashColourRush) == "On"; } }
        public static bool SuperDashing { get { return Value(AssistOptions.SuperDashing) == "On" || ChaosMode.Is("Super Dashing"); } }
        public static bool Hiccups { get { return Value(AssistOptions.Hiccups) == "On"; } }
        public static bool InvisibleMotion { get { return Value(AssistOptions.InvisibleMotion) == "On"; } }
        public static bool NoGrabbing { get { return Value(AssistOptions.NoGrabbing) == "On"; } }
        /// <summary>Screen shake: 0 off, 0.5 half, 1 full.</summary>
        public static float ShakeMult { get { return 1f; } }   // the Screen Shake option was dropped (user 2026-10-14)
        public static bool DashTrail { get { return Value(AssistOptions.DashTrail) == "On"; } }
        /// <summary>Anything other than the defaults, Variants too (the boss card shows Celeste's Assist Mode stamp, and
        /// no golden berry, as Celeste does for both).</summary>
        public static bool AnyOn { get { if (!AssistOn) return false; foreach (AssistRow r in AssistOptions.All) if ((r.Section == "Assist Mode" || r.Section == "Variants") && Value(r) != r.Default) return true; return false; } }

        /// <summary>assist.timer: what the speedrun timer shows (index into its values; 0 Off).</summary>
        public static int TimerMode { get { return Index(AssistOptions.Timer); } }
        public static string TimerModeName { get { return Value(AssistOptions.Timer); } }
        /// <summary>The Controls section (assist.key_*): was this action's F key pressed this frame?</summary>
        public static bool KeyPressed(AssistRow row)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return false;
            UnityEngine.InputSystem.Key key;
            if (!Enum.TryParse(Value(row), out key)) return false;
            return kb[key].wasPressedThisFrame;
        }
        public static string KeyName(AssistRow row) { return Value(row); }

        /// <summary>assist.hair_*: the chosen hair colour as hex, or the skin's own (visuals.hair_color_names).</summary>
        public static string HairHex(AssistRow row, string skinHex)
        {
            string v = Value(row);
            foreach (string pair in Visuals.HairColorNames.Split(';'))
            {
                int c = pair.IndexOf(':');
                if (c > 0 && pair.Substring(0, c) == v) return pair.Substring(c + 1);
            }
            return skinHex;
        }

        /// <summary>Changes whenever a hair colour setting changes.</summary>
        public static int HairVersion { get { return Index(AssistOptions.HairTwo) * 1024 + Index(AssistOptions.HairOne) * 32 + Index(AssistOptions.HairZero); } }
        static bool IsKeyRow(AssistRow r) { return r.Section == "Controls"; }
        static bool KeyTaken(AssistRow row, int index)
        {
            string v = Values(row)[index];
            foreach (AssistRow r in AssistOptions.All) if (IsKeyRow(r) && r.Id != row.Id && Value(r) == v) return true;
            return false;
        }

        /// <summary>assist.timer_berries: golden berries beside the speedrun timer.</summary>
        public static bool TimerBerries { get { return Value(AssistOptions.TimerBerries) == "On"; } }
        /// <summary>assist.backpack: Madeline (and her palettes) with or without her backpack.</summary>
        public static bool Backpack { get { return Value(AssistOptions.Backpack) == "On"; } }
        /// <summary>assist.skin: the chosen skin's name (sheets/skins.json name).</summary>
        public static string SkinName { get { return Value(AssistOptions.Skin); } }
        public static string OutfitName { get { return Value(AssistOptions.Outfit); } }
        /// <summary>F1, or the skin loaded from skin.cfg before this setting existed.</summary>
        public static void SetSkin(string name, bool save)
        {
            int i = Array.IndexOf(Values(AssistOptions.Skin), name);
            if (i < 0) return;
            chosen[Key(AssistOptions.Skin)] = i;
            if (save) Save();
        }
        public static bool HasSaved(string id) { Load(); foreach (AssistRow r in AssistOptions.All) if (r.Id == id) return chosen.ContainsKey(Key(r)); return chosen.ContainsKey(id); }
        /// <summary>F7: the next timer mode, kept like the menu's choices.</summary>
        public static void NextTimerMode() { chosen[AssistOptions.Timer.Id] = (TimerMode + 1) % Values(AssistOptions.Timer).Length; Save(); }

        static string SaveFile { get { return Path.Combine(DataDir ?? "", "assist.cfg"); } }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (DataDir == null || !File.Exists(SaveFile)) return;
                foreach (string line in File.ReadAllLines(SaveFile))
                {
                    string[] kv = line.Split('=');
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim(), id = key;
                    if (key == "assist_on") { chosen[key] = kv[1].Trim() == "On" ? 1 : 0; continue; }
                    if (key.StartsWith("look", StringComparison.Ordinal) && key.IndexOf('.') > 0) id = key.Substring(key.IndexOf('.') + 1);
                    foreach (AssistRow r in AssistOptions.All)
                        if (r.Id == id)
                        {
                            int i = Array.IndexOf(Values(r), kv[1].Trim());
                            if (i < 0) continue;
                            // a setting from before looks existed belongs to look 1
                            chosen[IsLookRow(r) && key == id ? KeyFor(r, 0) : key] = i;
                        }
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Assist Mode: " + e.Message); }
        }

        static void Save()
        {
            try
            {
                var lines = new List<string>();
                foreach (AssistRow r in AssistOptions.All)
                    if (!IsLookRow(r)) lines.Add(r.Id + "=" + Value(r));
                    else for (int look = 0; look < Values(AssistOptions.Look).Length; look++) lines.Add(KeyFor(r, look) + "=" + Values(r)[IndexFor(r, look)]);
                int on; if (chosen.TryGetValue("assist_on", out on)) lines.Add("assist_on=" + (on == 1 ? "On" : "Off"));
                Directory.CreateDirectory(DataDir);
                File.WriteAllLines(SaveFile, lines.ToArray());
            }
            catch (Exception e) { Plugin.Log.LogWarning("Assist Mode: " + e.Message); }
        }

        // ---- effects -----------------------------------------------------------------------------------------

        TimeManager.TimeControlInstance speedControl, menuPause, dashFreeze;
        float tauntHeld, dashFreezeFor;
        bool testHold;   // the test harness holds dash for the arrow screenshot
        bool open, relinquished, dashFreezing;
        /// <summary>The settings are open: look changes wait until they close (the preview shows them).</summary>
        public static bool IsOpen;
        bool cursorWas; CursorLockMode lockWas;
        Rect arrowLeft, arrowRight, previewBody;   // the preview's arrows and body, in 1080p GUI units
        int previewPose;   // click her (or Tab): idle, running, dashing, jumping, climbing
        static readonly string[] PreviewPoses = { Animations.Idle.CelesteAnim, Animations.RunFast.CelesteAnim, Animations.Dash.CelesteAnim, Animations.JumpSlow.CelesteAnim, Animations.ClimbUp.CelesteAnim };
        static readonly string[] PoseNames = { "Standing", "Running", "Dashing", "Jumping", "Climbing" };
        int selected;
        float openT, flash, menuScroll;
        // the menu's tabs (user 2026-10-13): one section each, switched with L1/R1 or Q/E
        static readonly string[] Tabs = { "Assist Mode", "Variants", "Challenges", "Appearance", "Speedrun Timer", "Controls" };
        static readonly string[] TabNames = { "Assist", "Variants", "Challenges", "Appearance", "Speedrun", "Controls" };
        int tab;
        // where things were drawn last frame (1080p GUI units), for the mouse
        readonly Rect[] tabRects = new Rect[Tabs.Length];
        readonly Dictionary<int, Rect> rowRects = new Dictionary<int, Rect>();
        Rect arrowL, arrowR;
        bool padHints;   // the last input came from a controller: the hints show PlayStation buttons
        List<int> TabRows()
        {
            var l = new List<int>();
            for (int i = 0; i < AssistOptions.All.Length; i++) if (AssistOptions.All[i].Section == Tabs[tab]) l.Add(i);
            return l;
        }
        void SwitchTab(int dir)
        {
            tab = (tab + dir + Tabs.Length) % Tabs.Length;
            selected = TabRows()[0]; menuScroll = 0f; flash = 0f;
            Sfx.Play(Sounds.MenuRolloverDown.Id);
        }
        void TrackInputKind(UnityEngine.InputSystem.Keyboard kb)
        {
            try
            {
                var dev = InControl.InputManager.ActiveDevice;
                if (dev != null && dev != InControl.InputDevice.Null && (dev.AnyButtonWasPressed || dev.LeftStick.Vector.sqrMagnitude > 0.25f || dev.DPad.Vector.sqrMagnitude > 0.25f)) padHints = true;
            }
            catch (Exception) { }
            if (kb != null && kb.anyKey.wasPressedThisFrame) padHints = false;
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame) padHints = false;
        }

        public static AssistMode Instance;
        static bool fromMenu;
        /// <summary>The title screen's Extras: Settings without a save loaded.</summary>
        public static void OpenFromMenu() { if (Instance == null || Instance.open) return; fromMenu = true; Instance.Open(null); }
        void Awake() { Instance = this; }

#if TESTHARNESS
        public void TestOpen(int select) { HeroController h = HeroController.SilentInstance; if (h != null && !open) Open(h); selected = select; tab = Math.Max(0, Array.IndexOf(Tabs, AssistOptions.All[select].Section)); }
        public void TestClose() { Close(HeroController.SilentInstance, true); }
        public static void TestSet(string id, int index) { Load(); foreach (AssistRow r in AssistOptions.All) if (r.Id == id) { chosen[Key(r)] = index; return; } chosen[id] = index; }
        public void TestDashFreeze(bool on) { testHold = on; if (on) { dashFreezing = true; dashFreezeFor = 0f; } else EndDashFreeze(); }
#endif

        bool loggedError;
        void Update()
        {
            // Silksong turns Unity's log off: an exception here would silently skip the rest of the frame.
            try { Tick(); }
            catch (Exception e) { if (!loggedError) { loggedError = true; Plugin.Log.LogError("Assist Mode: " + e); } }
        }

        void Tick()
        {
            HeroController h = HeroController.SilentInstance;
            GameManager gm = GameManager.SilentInstance;
            bool inGame = Plugin.Ready && h != null && gm != null && !gm.IsMenuScene() && gm.GameState != GlobalEnums.GameState.MAIN_MENU;
            // opened from the title screen's Extras (user 2026-10-14): runs without a save, until it's closed
            if (fromMenu && (gm == null || !gm.IsMenuScene())) fromMenu = false;
            if (!inGame && !fromMenu) { Close(h, false); EndDashFreeze(); SetSpeed(1f); openT = Math.Max(0f, openT - Time.unscaledDeltaTime / Visuals.AssistFadeTime); return; }   // its fade finishes on the title too
            float dt = Time.unscaledDeltaTime;
            flash += dt;
            if (fromMenu)
            {
                var kbm = UnityEngine.InputSystem.Keyboard.current;
                InputHandler inm = InputHandler.Instance;
                HeroActions am = inm != null ? inm.inputActions : null;
                openT = Math.Min(1f, openT + dt / Visuals.AssistFadeTime);
                bool circle = false;
                try { var devB = InControl.InputManager.ActiveDevice; if (devB != null) circle = devB.Action2.WasPressed; } catch (Exception) { }
                if (KeyPressed(AssistOptions.KeySettings) || Back(kbm, am) || circle) { Close(null, true); fromMenu = false; return; }
                TrackInputKind(kbm);
                bool l1m = false, r1m = false;
                try { var dev = InControl.InputManager.ActiveDevice; if (dev != null) { l1m = dev.LeftBumper.WasPressed; r1m = dev.RightBumper.WasPressed; } } catch (Exception) { }
                if (l1m || (kbm != null && kbm.qKey.wasPressedThisFrame)) SwitchTab(-1);
                else if (r1m || (kbm != null && kbm.eKey.wasPressedThisFrame)) SwitchTab(1);
                else Navigate(kbm, am);
                if (Tabs[tab] == "Appearance") PreviewClick();
                return;
            }

            // assist.game_speed: one multiplicative time control on top of Silksong's own (pauses, hit freezes).
            SetSpeed(GameSpeed);

            bool playing = gm.GameState == GlobalEnums.GameState.PLAYING && !gm.isPaused && !h.cState.transitioning && !h.cState.dead && !h.cState.hazardDeath;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            InputHandler input = InputHandler.Instance;
            HeroActions a = input != null ? input.inputActions : null;

            // inputs.assist_menu: F5, or Taunt held for assist_hold_time on a controller.
            tauntHeld = a != null && a.Taunt.IsPressed && playing ? tauntHeld + dt : 0f;
            bool toggle = KeyPressed(AssistOptions.KeySettings) || (tauntHeld >= Visuals.AssistHoldTime && !open);
            if (tauntHeld >= Visuals.AssistHoldTime) tauntHeld = -999f;   // once per hold
            if (open)
            {
                openT = Math.Min(1f, openT + dt / Visuals.AssistFadeTime);
                if (gm.isPaused || !playing && !relinquished) { Close(h, false); return; }
                if (toggle || Back(kb, a)) { Close(h, true); return; }
                TrackInputKind(kb);
                bool l1 = false, r1 = false;
                try { var dev = InControl.InputManager.ActiveDevice; if (dev != null) { l1 = dev.LeftBumper.WasPressed; r1 = dev.RightBumper.WasPressed; } } catch (Exception) { }
                if (l1 || (kb != null && kb.qKey.wasPressedThisFrame)) SwitchTab(-1);
                else if (r1 || (kb != null && kb.eKey.wasPressedThisFrame)) SwitchTab(1);
                else Navigate(kb, a);
                if (Tabs[tab] == "Appearance") PreviewClick();
                bool square = false, triangle = false;
                try { var dev = InControl.InputManager.ActiveDevice; if (dev != null) { square = dev.Action3.WasPressed; triangle = dev.Action4.WasPressed; } } catch (Exception) { }
                if (Tabs[tab] == "Appearance" && ((kb != null && kb.tabKey.wasPressedThisFrame) || square)) { previewPose = (previewPose + 1) % PreviewPoses.Length; Sfx.Play(Sounds.MenuRolloverDown.Id); }
                if (((kb != null && kb.rKey.wasPressedThisFrame) || triangle) && AssistOptions.All[selected].Section == "Appearance") ResetLook();
            }
            else
            {
                openT = Math.Max(0f, openT - dt / Visuals.AssistFadeTime);
                if (toggle && playing && !h.controlReqlinquished) Open(h);
            }

            UpdateDashFreeze(dt, a, playing);
        }

        void SetSpeed(float speed)
        {
            if (Math.Abs(speed - 1f) < 0.001f)
            {
                if (speedControl != null) { speedControl.Release(); speedControl = null; }
                return;
            }
            if (speedControl == null) speedControl = TimeManager.CreateTimeControl(speed, TimeManager.TimeControlInstance.Type.Multiplicative);
            else if (Math.Abs(speedControl.TimeScale - speed) > 0.001f) speedControl.TimeScale = speed;
        }

        // assist.dash_assist: Celeste's Engine.DashAssistFreeze. Time stops from the dash's start while the dash
        // button stays held (at most assist_dash_freeze_max), so the aim can still change; the dash then goes
        // where she is aiming.
        public void BeginDashFreeze()
        {
            if (!DashAssist || open) return;
            dashFreezing = true;
            dashFreezeFor = 0f;
            if (dashFreeze == null) dashFreeze = TimeManager.CreateTimeControl(0f, TimeManager.TimeControlInstance.Type.MinValue);
        }

        void UpdateDashFreeze(float dt, HeroActions a, bool playing)
        {
            if (!dashFreezing) return;
            dashFreezeFor += dt;
            bool held = (a != null && a.Attack.IsPressed) || testHold;
            if (!held || !playing || dashFreezeFor >= Visuals.AssistDashFreezeMax)
            {
#if TESTHARNESS
                Plugin.Log.LogInfo("[TEST] dash assist ends: held " + held + " playing " + playing + " after " + dashFreezeFor.ToString("F2") + " s");
#endif
                EndDashFreeze();
            }
        }

        void EndDashFreeze()
        {
            dashFreezing = false;
            if (dashFreeze != null) { dashFreeze.Release(); dashFreeze = null; }
        }

        // ---- menu --------------------------------------------------------------------------------------------

        void Open(HeroController h)
        {
            open = true; IsOpen = true;
            selected = TabRows()[0]; menuScroll = 0f;   // the tab used last time
            try { var dev = InControl.InputManager.ActiveDevice; padHints = dev != null && dev != InControl.InputDevice.Null && h != null && !(UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.anyKey.isPressed); } catch (Exception) { padHints = false; }
            cursorWas = Cursor.visible; lockWas = Cursor.lockState;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;   // the preview's arrows are clicked
            flash = 0f;
            if (menuPause == null) menuPause = TimeManager.CreateTimeControl(0f, TimeManager.TimeControlInstance.Type.MinValue);
            if (h != null && !h.controlReqlinquished) { h.RelinquishControl(); relinquished = true; }
            Sfx.Play(Sounds.MenuPause.Id);
        }

        void Close(HeroController h, bool sound)
        {
            if (!open) return;
            open = false; IsOpen = false;
            Cursor.visible = cursorWas; Cursor.lockState = lockWas;
            if (menuPause != null) { menuPause.Release(); menuPause = null; }
            if (relinquished && h != null) h.RegainControl();
            relinquished = false;
            Plugin.Controller.ClearInput();   // nothing pressed in the menu carries into the game
            if (sound) Sfx.Play(Sounds.MenuUnpause.Id);
            Save();
        }

        static bool Back(UnityEngine.InputSystem.Keyboard kb, HeroActions a)
        {
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) return true;
            return a != null && (a.Dash.WasPressed || a.MenuCancel.WasPressed);
        }

        void Navigate(UnityEngine.InputSystem.Keyboard kb, HeroActions a)
        {
            List<int> rows = TabRows();
            int n = rows.Count, at = Math.Max(0, rows.IndexOf(selected));
            bool up = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (a != null && a.Up.WasPressed);
            bool down = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (a != null && a.Down.WasPressed);
            bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (a != null && a.Left.WasPressed);
            bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (a != null && a.Right.WasPressed);
            bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (a != null && a.Jump.WasPressed);
            // the mouse (user 2026-10-13): hover picks a row, the wheel scrolls, clicks change values and tabs
            int mouseDir = 0;
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null)
            {
                Vector2 g = MenuMouse.Gui();
                bool click = m.leftButton.wasPressedThisFrame, rclick = m.rightButton.wasPressedThisFrame;
                float wheel = m.scroll.ReadValue().y;
                if (wheel > 0.1f) up = true; else if (wheel < -0.1f) down = true;
                for (int t = 0; t < tabRects.Length; t++)
                    if (click && tabRects[t].Contains(g)) { if (t != tab) SwitchTab(t - tab); return; }
                if (m.delta.ReadValue().sqrMagnitude > 0.5f || click || rclick)
                    foreach (var kv in rowRects)
                        if (kv.Value.Contains(g) && kv.Key != selected) { selected = kv.Key; flash = 0f; Sfx.Play(Sounds.MenuRolloverDown.Id); break; }
                if (click && arrowL.Contains(g)) mouseDir = -1;
                else if (click && arrowR.Contains(g)) mouseDir = 1;
                else if ((click || rclick) && rowRects.ContainsKey(selected) && rowRects[selected].Contains(g)) mouseDir = rclick ? -1 : 2;   // 2: step and wrap
            }
            if (up) { selected = rows[(at + n - 1) % n]; flash = 0f; Sfx.Play(Sounds.MenuRolloverUp.Id); }
            if (down) { selected = rows[(at + 1) % n]; flash = 0f; Sfx.Play(Sounds.MenuRolloverDown.Id); }
            AssistRow row = AssistOptions.All[selected];
            if (mouseDir == -1) left = true; else if (mouseDir == 1) right = true; else if (mouseDir == 2) confirm = true;
            int count = Values(row).Length, i = Index(row), was = i;
            if (IsKeyRow(row))
            {
                // F keys: step over keys another action already uses
                int dir = left ? -1 : (right || confirm) ? 1 : 0;
                if (dir != 0)
                    for (int k = i + dir; k >= 0 && k < count; k += dir)
                        if (!KeyTaken(row, k)) { i = k; break; }
            }
            else
            {
                if (left) i = Math.Max(0, i - 1);
                if (right || confirm) i = confirm && !right ? (i + 1) % count : Math.Min(count - 1, i + 1);
            }
            if (i != was)
            {
                chosen[Key(row)] = i;
                string v = Values(row)[i];
                Sfx.Play(v == "Off" ? Sounds.MenuToggleOff.Id : v == "On" ? Sounds.MenuToggleOn.Id : Sounds.MenuButtonSelect.Id);
            }
        }

        // ---- look preview: the current look standing to the right of the list, with arrows to change it ----

        /// <summary>A click on the preview's arrows changes the selected Appearance row (or the skin).</summary>
        void PreviewClick()
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m == null || !m.leftButton.wasPressedThisFrame) return;
            float s = Screen.height / 1080f;
            Vector2 mp = m.position.ReadValue(), g = new Vector2(mp.x / s, (Screen.height - mp.y) / s);
            if (previewBody.Contains(g)) { previewPose = (previewPose + 1) % PreviewPoses.Length; Sfx.Play(Sounds.MenuRolloverDown.Id); return; }
            int dir = arrowLeft.Contains(g) ? -1 : arrowRight.Contains(g) ? 1 : 0;
            if (dir == 0) return;
            AssistRow row = AssistOptions.All[selected];
            if (row.Section != "Appearance") { selected = Array.IndexOf(AssistOptions.All, AssistOptions.Skin); row = AssistOptions.Skin; }
            int count = Values(row).Length;
            chosen[Key(row)] = (Index(row) + dir + count) % count;
            Sfx.Play(Sounds.MenuButtonSelect.Id);
        }

        static void DrawSprite(Sprite sp, Vector2 pivotAt, float scale, Color c)
        {
            if (sp == null) return;
            Rect tr = sp.textureRect;
            Texture tex = sp.texture;
            Vector2 pv = sp.pivot;
            var r = new Rect(pivotAt.x - pv.x * scale, pivotAt.y - (tr.height - pv.y) * scale, tr.width * scale, tr.height * scale);
            GUI.color = c;
            GUI.DrawTextureWithTexCoords(r, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
        }

        static Color HexColor(string hex) { Color c; return ColorUtility.TryParseHtmlString("#" + hex, out c) ? c : Color.white; }

        void DrawPreview(float alpha, float x0, float x1)
        {
            if (Plugin.PreviewAssets == null) return;
            SkinRow skin = Array.Find(Skins.All, k => k.Name == SkinName);
            if (skin.Id == null) return;
            CelesteAssets a;
            try { a = Plugin.PreviewAssets(skin); } catch { return; }
            CelesteAnim idle;
            if (a == null) return;
            if (!a.Anims.TryGetValue(PreviewPoses[previewPose], out idle) || idle.Frames.Count == 0)
                if (!a.Anims.TryGetValue(Animations.Idle.CelesteAnim, out idle) || idle.Frames.Count == 0) return;
            CelesteFrame fr = idle.Frames[(int)(Time.unscaledTime / Math.Max(0.05f, idle.Delay)) % idle.Frames.Count];
            float S = Visuals.LookPreviewScale, cx = (x0 + x1) / 2f;
            Vector2 feet = new Vector2(cx, 540f + 16f * S);
            // a faint panel behind her
            float pw = 30f * S, ph = 40f * S;
            GUI.color = new Color(1f, 1f, 1f, 0.06f * alpha);
            GUI.DrawTexture(new Rect(cx - pw / 2f, feet.y - ph + 4f * S, pw, ph), Texture2D.whiteTexture);
            bool knight = MadelineView.Masked(skin.Style), cat = skin.Style == "cat" || (CatEars && skin.Style == "celeste" && skin.CelesteSprite == "player");
            Color hair = HexColor(HairHex(AssistOptions.HairOne, skin.HairNormal)); hair.a = alpha;
            Color ink = new Color(0f, 0f, 0f, alpha), white = new Color(1f, 1f, 1f, alpha);
            Color bodyC = HexColor(skin.BodyColor); bodyC.a *= alpha;
            // Celeste's hair: the bangs at the head, then the nodes trailing back and down (MadelineView.UpdateHair at rest)
            int n = HairLength;
            float hs = HairSize;
            var nodes = new Vector2[n];
            nodes[0] = feet + new Vector2(fr.HairOffset.x, Visuals.HairHeadOffset + fr.HairOffset.y) * S;
            for (int i = 1; i < n; i++)
                nodes[i] = nodes[i - 1] + new Vector2(-Visuals.HairStepInFacing * (i == 1 ? 2f : 1f) + Mathf.Sin(Time.unscaledTime * 2f + i) * 0.15f, Visuals.HairStepPerSegment) * S;
            if (cat)
            {
                Vector2 hip = feet + new Vector2(-3f, -3.5f) * S;
                for (int i = MadelineView.TailLength - 1; i >= 0; i--)
                {
                    float f = i / (float)(MadelineView.TailLength - 1), sway = Mathf.Sin(Time.unscaledTime * 3f - i * 0.7f) * f * 2.2f;
                    Vector2 pos = hip + new Vector2(-(i * 1.3f + sway * 0.3f), -(i * i * 0.22f + sway * 0.7f)) * S;
                    float sc = S * 0.6f * (1f - f * 0.3f);
                    DrawSprite(MadelineView.TailOutlineSprite, pos, sc, white);
                    DrawSprite(MadelineView.TailSprite, pos, sc, hair);
                }
            }
            if (fr.HasHair && !knight && a.HairNode != null && a.Bangs.Count > 0)
            {
                for (int pass = 0; pass < 2; pass++)
                    for (int i = n - 1; i >= 0; i--)
                    {
                        Sprite sp = i == 0 ? a.Bangs[Mathf.Clamp(fr.HairFrame, 0, a.Bangs.Count - 1)] : a.HairNode;
                        float sc = S * (i == 0 ? 1f : (0.25f + (1f - (float)i / n) * 0.75f) * hs);
                        if (pass == 0) { DrawSprite(sp, nodes[i] + Vector2.left * S, sc, ink); DrawSprite(sp, nodes[i] + Vector2.right * S, sc, ink); DrawSprite(sp, nodes[i] + Vector2.up * S, sc, ink); DrawSprite(sp, nodes[i] + Vector2.down * S, sc, ink); }
                        else DrawSprite(sp, nodes[i], sc, HairNodeColor(hair, i, n, true));
                    }
            }
            DrawSprite(fr.Sprite, feet, S, bodyC);
            previewBody = new Rect(cx - 8f * S, feet.y - 24f * S, 16f * S, 24f * S);
            if (cat)
            {
                Vector2 head = nodes[0] + new Vector2(-0.5f, -2f) * S;
                DrawSprite(MadelineView.EarSprite, head, S, hair);
                DrawSprite(MadelineView.EarInnerSprite, head, S, white);
            }
            if (knight) DrawSprite(MadelineView.MaskFor(skin.Style), nodes[0] + new Vector2(0.5f, -2f + (skin.Style == "hornet" ? Visuals.HornetChinDrop : 0f)) * S, S, white);
            // arrows either side of her, and what they change
            float ay = feet.y - 12f * S, aw = 70f;
            arrowLeft = new Rect(cx - pw / 2f - aw, ay - aw / 2f, aw, aw);
            arrowRight = new Rect(cx + pw / 2f, ay - aw / 2f, aw, aw);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            Art.DrawOutlined("<", arrowLeft.center.x - Art.Measure("<") * 1.4f / 2f, arrowLeft.center.y - Art.LineHeight * 1.4f / 2f, 1.4f, new Color(1f, 1f, 1f, alpha * pulse), ink, 3f);
            Art.DrawOutlined(">", arrowRight.center.x - Art.Measure(">") * 1.4f / 2f, arrowRight.center.y - Art.LineHeight * 1.4f / 2f, 1.4f, new Color(1f, 1f, 1f, alpha * pulse), ink, 3f);
            AssistRow row = AssistOptions.All[selected];
            if (row.Section != "Appearance") row = AssistOptions.Skin;
            string cap = row.Label + ": " + Value(row), lookCap = "Look " + Value(AssistOptions.Look);
            float cs = Mathf.Min(0.7f, (x1 - x0 - 20f) / Math.Max(1f, Art.Measure(cap)));
            Art.DrawOutlined(lookCap, cx - Art.Measure(lookCap) * 0.9f / 2f, feet.y - ph + 4f * S - Art.LineHeight * 0.9f - 6f, 0.9f, white, ink, 2f);
            Art.DrawOutlined(cap, cx - Art.Measure(cap) * cs / 2f, feet.y + 4f * S + 10f, cs, new Color(0.85f, 0.85f, 0.85f, alpha), ink, 2f);
            // two short lines, shrunk to fit the space right of the list so nothing overlaps
            string[] tips = padHints
                ? new[] { PoseNames[previewPose] + " - Square for a pose", "Triangle resets this look" }
                : new[] { PoseNames[previewPose] + " - Tab for a pose", "R resets this look" };
            float ty = feet.y + 4f * S + 10f + Art.LineHeight * cs + 4f, room = x1 - x0 - 20f;
            foreach (string tip in tips)
            {
                float tps = Mathf.Min(0.42f, room / Math.Max(1f, Art.Measure(tip)));
                Art.DrawOutlined(tip, cx - Art.Measure(tip) * tps / 2f, ty, tps, new Color(0.7f, 0.7f, 0.7f, alpha), ink, 2f);
                ty += Art.LineHeight * tps + 2f;
            }
            GUI.color = Color.white;
        }

        // Celeste TextMenu, in Celeste's 1920x1080 GUI units scaled to the screen height.
        void OnGUI()
        {
            if (TheoCamera.Hiding) return;   // out of Theo's photos
            if (Event.current.type != EventType.Repaint || Art == null) return;
            float s = Screen.height / 1080f, w = Screen.width / s;
            if (dashFreezing) DrawDashArrow(s);
            if (openT <= 0f) return;
            float ease = 1f - (1f - openT) * (1f - openT) * (1f - openT), alpha = ease;
            Matrix4x4 old = GUI.matrix;
            GUI.depth = -2;   // above postcards
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            GUI.color = new Color(0f, 0f, 0f, Visuals.AssistDim * alpha);
            GUI.DrawTexture(new Rect(0f, 0f, w, 1080f), Texture2D.whiteTexture);

            float slide = (1f - ease) * 60f;
            GuiArt.Img icon = Art.Image("assistmode");
            float cx = w / 2f;
            float top = 70f + slide;
            if (icon != null)
            {
                float isz = 110f;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(cx - isz / 2f, top, isz, isz), icon.Tex);
                top += isz + 10f;
            }
            Art.DrawOutlined("SETTINGS", cx - Art.Measure("SETTINGS") * 1.2f / 2f, top, 1.2f, new Color(1f, 1f, 1f, alpha), new Color(0f, 0f, 0f, alpha), 3f);
            top += Art.LineHeight * 1.2f + 14f;

            Color hi = (flash % 0.2f) < 0.1f ? Hex(Visuals.MenuHighlightA) : Hex(Visuals.MenuHighlightB);
            float rowH = Art.LineHeight * Visuals.AssistTextScale + 14f, half = Visuals.AssistColumnWidth / 2f;
            var ink0 = new Color(0f, 0f, 0f, alpha);
            // the tab bar: L1 / Q on the left, R1 / E on the right, the current tab highlighted
            {
                float ts = Visuals.AssistTextScale * 0.75f, gap = 50f, tw = 0f;
                for (int t = 0; t < Tabs.Length; t++) tw += Art.Measure(TabNames[t]) * ts + (t > 0 ? gap : 0f);
                float tx = cx - tw / 2f;
                string lk = padHints ? "L1" : "Q", rk = padHints ? "R1" : "E";
                float ks = ts * 0.8f;
                Art.DrawOutlined(lk, tx - 40f - Art.Measure(lk) * ks, top + 4f, ks, new Color(0.7f, 0.7f, 0.7f, alpha), ink0, 2f);
                Art.DrawOutlined(rk, tx + tw + 40f, top + 4f, ks, new Color(0.7f, 0.7f, 0.7f, alpha), ink0, 2f);
                for (int t = 0; t < Tabs.Length; t++)
                {
                    float lw = Art.Measure(TabNames[t]) * ts;
                    tabRects[t] = new Rect(tx - gap / 2f, top - 8f, lw + gap, Art.LineHeight * ts + 20f);
                    if (t != tab && tabRects[t].Contains(MenuMouse.Gui()) && !padHints) { GUI.color = new Color(1f, 1f, 1f, 0.06f * alpha); GUI.DrawTexture(tabRects[t], Texture2D.whiteTexture); GUI.color = Color.white; }
                    Color tc = t == tab ? Hex(Visuals.MenuHighlightA) : new Color(0.55f, 0.55f, 0.55f, 1f);
                    tc.a = alpha;
                    Art.DrawOutlined(TabNames[t], tx, top, ts, tc, ink0, 2f);
                    if (t == tab) { GUI.color = tc; GUI.DrawTexture(new Rect(tx, top + Art.LineHeight * ts + 4f, lw, 4f), Texture2D.whiteTexture); GUI.color = Color.white; }
                    tx += lw + gap;
                }
                top += Art.LineHeight * ts + 34f;
            }
            // the list scrolls when it is taller than the space between the tabs and the description
            List<int> tabRows = TabRows();
            int n = AssistOptions.All.Length;
            var rowY = new float[n];
            float y = 0f;
            foreach (int i in tabRows) { rowY[i] = y; y += rowH; }
            float listTop = top, listH = 1080f - 230f - listTop, total = y;
            float want = Mathf.Clamp(menuScroll, rowY[selected] + rowH - listH, rowY[selected]);
            menuScroll = Mathf.Clamp(want, 0f, Math.Max(0f, total - listH));
            rowRects.Clear(); arrowL = arrowR = new Rect();
            foreach (int i in tabRows)
            {
                AssistRow r = AssistOptions.All[i];
                float ry = listTop + rowY[i] - menuScroll;
                if (ry < listTop - 1f || ry + rowH > listTop + listH + 1f) continue;
                top = ry;
                bool sel = i == selected;
                rowRects[i] = new Rect(cx - half - 24f, ry - 6f, half * 2f + 48f, rowH);
                if (sel)
                {
                    // a soft bar behind the selected row, like a highlighted menu item
                    GUI.color = new Color(1f, 1f, 1f, 0.07f * alpha); GUI.DrawTexture(rowRects[i], Texture2D.whiteTexture); GUI.color = Color.white;
                }
                Color c = sel ? hi : Color.white;
                c.a = alpha;
                float sc = Visuals.AssistTextScale;
                Art.DrawOutlined(r.Label, cx - half, top, sc, c, new Color(0f, 0f, 0f, alpha), 2f);
                string v = Value(r);
                float vw = Art.Measure(v) * sc;
                float vx = cx + half - vw;
                Color vc = v != r.Default ? c : (sel ? c : new Color(0.75f, 0.75f, 0.75f, alpha));
                Art.DrawOutlined(v, vx, top, sc, vc, new Color(0f, 0f, 0f, alpha), 2f);
                // assist.hair_*: a swatch of the chosen colour before its name
                string hex = r.Id.StartsWith("hair_", StringComparison.Ordinal) ? HairHex(r, null) : null;
                Color sw;
                if (hex != null && ColorUtility.TryParseHtmlString("#" + hex, out sw))
                {
                    float ss = 28f, sxw = vx - (sel ? Art.Measure("<") * sc + 30f : 12f) - ss, syw = top + (Art.LineHeight * sc - ss) / 2f;
                    GUI.color = new Color(0f, 0f, 0f, alpha); GUI.DrawTexture(new Rect(sxw - 2f, syw - 2f, ss + 4f, ss + 4f), Texture2D.whiteTexture);
                    sw.a = alpha; GUI.color = sw; GUI.DrawTexture(new Rect(sxw, syw, ss, ss), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                if (sel)
                {
                    // Celeste's TextMenu.Option draws "<" and ">" beside the value, dimmed at either end.
                    int idx = Index(r), nv = Values(r).Length;
                    var black = new Color(0f, 0f, 0f, alpha);
                    float lax = vx - Art.Measure("<") * sc - 18f, aw2 = Art.Measure(">") * sc;
                    Art.DrawOutlined("<", lax, top, sc, new Color(c.r, c.g, c.b, idx > 0 ? alpha : alpha * 0.25f), black, 2f);
                    Art.DrawOutlined(">", cx + half + 18f, top, sc, new Color(c.r, c.g, c.b, idx < nv - 1 ? alpha : alpha * 0.25f), black, 2f);
                    arrowL = new Rect(lax - 14f, top - 6f, aw2 + 28f, rowH);
                    arrowR = new Rect(cx + half + 4f, top - 6f, aw2 + 28f, rowH);
                }
            }
            // more above / below the visible part
            if (menuScroll > 1f) Art.DrawOutlined("^", cx - 8f, listTop - 26f, 0.5f, new Color(1f, 1f, 1f, alpha * 0.7f), new Color(0f, 0f, 0f, alpha), 2f);
            if (menuScroll < total - listH - 1f) Art.DrawOutlined("v", cx - 8f, listTop + listH, 0.5f, new Color(1f, 1f, 1f, alpha * 0.7f), new Color(0f, 0f, 0f, alpha), 2f);
            top = listTop + listH + 30f;
            // the selected option's description, in Celeste's grey menu subtext
            float ds = Visuals.AssistTextScale * 0.7f;
            foreach (string line in Art.Wrap(AssistOptions.All[selected].Description, Visuals.AssistColumnWidth * 1.3f / ds))
            {
                Art.DrawOutlined(line, cx - Art.Measure(line) * ds / 2f, top, ds, new Color(0.7f, 0.7f, 0.7f, alpha), new Color(0f, 0f, 0f, alpha), 2f);
                top += Art.LineHeight * ds;
            }

            string hint = padHints
                ? "D-pad Up/Down choose   Left/Right change   L1/R1 menus   Circle to close"
                : "Up/Down choose   Left/Right change   Q/E menus   " + KeyName(AssistOptions.KeySettings) + " or Esc to close";
            float hs = 0.45f;
            Art.DrawOutlined(hint, cx - Art.Measure(hint) * hs / 2f, 1080f - 90f, hs, new Color(1f, 1f, 1f, alpha * 0.8f), new Color(0f, 0f, 0f, alpha), 2f);
            if (Tabs[tab] == "Appearance") DrawPreview(alpha, cx + Visuals.AssistColumnWidth / 2f + 20f, w - 20f);
            GUI.matrix = old;
            GUI.color = Color.white;
        }

        // Celeste's dash assist arrow, around Madeline, pointing where the dash will go.
        void DrawDashArrow(float s)
        {
            HeroController h = HeroController.SilentInstance;
            Camera cam = Camera.main;
            GuiArt.Img arrow = Art.Image("dotarrow"), outline = Art.Image("dotarrow_outline");
            if (h == null || cam == null || arrow == null) return;
            Vector2 aim = Plugin.Controller.AimPreview;   // Celeste px, Y down
            Vector3 sp = cam.WorldToScreenPoint(h.transform.position);
            Vector2 c = new Vector2(sp.x, Screen.height - sp.y) / s;
            float ang = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            Vector2 at = c + aim.normalized * Visuals.AssistArrowDistance;
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(at.x * s, at.y * s, 0f), Quaternion.Euler(0f, 0f, ang), new Vector3(s, s, 1f));
            float sz = Visuals.AssistArrowSize;
            if (outline != null) { GUI.color = Color.black; GUI.DrawTexture(new Rect(-sz / 2f - 4f, -sz / 2f - 4f, sz + 8f, sz + 8f), outline.Tex); }
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(-sz / 2f, -sz / 2f, sz, sz), arrow.Tex);
            GUI.matrix = old;
        }

        static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString("#" + hex, out c);
            return c;
        }
    }
}
