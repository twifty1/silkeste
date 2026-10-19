using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Achievements (user 2026-10-13): four tiers (Easy green, Medium orange, Hard red, Farewell purple), kept across
    /// all saves in achievements.cfg, shown as a grid of glowing squares in the Records menu, and announced with a banner.
    /// The starting boss (Moss Mother) and area (Moss Grotto) don't count for the combat and golden berry ones.
    /// </summary>
    public class Achievements : MonoBehaviour
    {
        public enum Tier { Easy, Medium, Hard, Farewell }
        public enum Kind { Movement, Combat, Golden, Explore, Silly, Summit }
        public class Def { public string Id, Name, How; public Tier Tier; public Kind Kind; public int Target; }

        public static readonly Def[] All =
        {
            D("wavedasher", "Wavedasher", "Do 50 wavedashes.", Tier.Easy, Kind.Movement, 50),
            D("climber", "Climber", "Climb 250 m of wall in total.", Tier.Easy, Kind.Movement, 250),
            D("jellyfish", "Jellyfish Jam", "Glide for 60 seconds without landing.", Tier.Hard, Kind.Movement, 60),
            D("hyperactive", "Hyperactive", "Chain a hyper (or wavedash) straight into a super.", Tier.Medium, Kind.Movement, 1),
            D("untouchable", "Untouchable", "In a boss fight, dash through 10 attacks in a row without getting hit.", Tier.Farewell, Kind.Combat, 10),
            D("combo", "Combo Breaker", "Hit 2 enemies with one dash.", Tier.Easy, Kind.Combat, 2),
            D("pogo", "Pogo Pro", "Bounce off spikes 25 times in one room.", Tier.Medium, Kind.Combat, 25),
            D("dash_master", "Dash Master", "Beat an Act 2 boss with only dash hits: no silk skills, no tools.", Tier.Farewell, Kind.Combat, 1),
            D("first_golden", "First Golden", "Earn your first golden berry.", Tier.Easy, Kind.Golden, 1),
            D("golden_hoard", "Golden Hoard", "Earn 5 golden berries.", Tier.Hard, Kind.Golden, 5),
            D("flawless", "Flawless", "Beat a boss without taking a single hit.", Tier.Hard, Kind.Golden, 1),
            D("golden_all", "Golden Everything", "Earn 10 golden berries.", Tier.Farewell, Kind.Golden, 10),
            D("tourist", "Tourist", "Visit 10 areas.", Tier.Easy, Kind.Explore, 10),
            D("fashionista", "Fashionista", "Wear every skin.", Tier.Easy, Kind.Explore, 4),
            D("photographer", "Photographer", "Take 25 photos with Theo's camera ({camera}).", Tier.Medium, Kind.Explore, 25),
            D("night_owl", "Night Owl", "Play 10 hours on one save.", Tier.Medium, Kind.Explore, 10),
            D("achoo", "Achoo", "Stand still until Madeline sneezes.", Tier.Easy, Kind.Silly, 1),
            D("sleepy", "Sleepy", "Rest at benches 30 times.", Tier.Easy, Kind.Silly, 30),
            D("spam_dash", "Spam Dash", "Get dash-locked by hitting enemies 50 times.", Tier.Medium, Kind.Silly, 50),
            D("skill_issue", "Skill Issue", "Die 50 times.", Tier.Easy, Kind.Silly, 50),
            D("speedrunner", "Speedrunner?", "Beat a boss in under 60 seconds.", Tier.Farewell, Kind.Silly, 1),
            // user 2026-10-13: one for getting all the others, a Farewell achievement with its own celebration
            D("summit", "The Summit", "Unlock every other achievement.", Tier.Farewell, Kind.Summit, 1),
        };
        static Def D(string id, string name, string how, Tier t, Kind k, int target) { return new Def { Id = id, Name = name, How = how, Tier = t, Kind = k, Target = target }; }

        public static readonly string[] TierNames = { "Easy", "Medium", "Hard", "Farewell" };
        // categories (user 2026-10-13): their own tag colours, none of the tier colours
        public static readonly string[] KindNames = { "Movement", "Combat", "Golden Berries", "Exploring", "Silly", "Summit" };
        public static Color KindColor(Kind k)
        {
            switch (k)
            {
                case Kind.Movement: return new Color(0.36f, 0.62f, 1f);    // blue
                case Kind.Combat: return new Color(0.66f, 0.72f, 0.78f);   // steel
                case Kind.Golden: return new Color(1f, 0.84f, 0.3f);       // gold
                case Kind.Explore: return new Color(0.65f, 0.5f, 0.38f);   // earth brown
                case Kind.Summit: return Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.25f, 1f), 0.45f, 1f);   // every colour in turn
                default: return new Color(0.24f, 0.86f, 0.77f);            // teal
            }
        }
        public static Color TierColor(Tier t, float time)
        {
            switch (t)
            {
                case Tier.Easy: return new Color(0.36f, 0.9f, 0.42f);
                case Tier.Medium: return new Color(1f, 0.6f, 0.15f);
                case Tier.Hard: return new Color(1f, 0.25f, 0.25f);
                default:   // Farewell: a slow purple-pink shimmer
                    float k = 0.5f + 0.5f * Mathf.Sin(time * 2.2f);
                    return Color.Lerp(new Color(0.62f, 0.3f, 1f), new Color(1f, 0.45f, 0.85f), k);
            }
        }

        public static Achievements Instance;
        readonly Dictionary<string, int> progress = new Dictionary<string, int>();
        readonly Dictionary<string, DateTime> unlocked = new Dictionary<string, DateTime>();
        readonly HashSet<string> skinsWorn = new HashSet<string>();
        bool loaded;

        /// <summary>The description with the player's own keys filled in.</summary>
        public static string HowText(Def d) { return d.How.Replace("{camera}", AssistMode.KeyName(AssistOptions.KeyCamera) + " or L3"); }
        public static int Progress(Def d) { return Instance == null ? 0 : Math.Min(d.Target, Instance.Get(d.Id)); }
        public static bool Unlocked(Def d, out DateTime when) { when = default(DateTime); return Instance != null && Instance.unlocked.TryGetValue(d.Id, out when); }
        public static int UnlockedCount(Tier t) { int n = 0; DateTime w; foreach (Def d in All) if (d.Tier == t && Unlocked(d, out w)) n++; return n; }
        public static int TierCount(Tier t) { int n = 0; foreach (Def d in All) if (d.Tier == t) n++; return n; }

        void Awake()
        {
            Instance = this;
            // InputHandler.SetCursorEnabled(false) every unpaused frame locks the cursor to the centre: while Settings
            // or Records is open it is told "enabled" instead, so the mouse moves and clicks (user 2026-10-13)
            try
            {
                var target = HarmonyLib.AccessTools.Method(typeof(InputHandler), "SetCursorEnabled");
                if (target != null) new HarmonyLib.Harmony("silkeste.menucursor").Patch(target, prefix: new HarmonyLib.HarmonyMethod(typeof(Achievements), nameof(CursorPrefix)));
            }
            catch (Exception e) { Plugin.Log.LogWarning("menu cursor patch: " + e.Message); }
        }
        static void CursorPrefix(ref bool isEnabled) { if (AssistMode.IsOpen || BossCard.RecordsOpen) isEnabled = !MenuMouse.PadActive; }
        int Get(string id) { Load(); int v; return progress.TryGetValue(id, out v) ? v : 0; }

        // ---- reporting ----------------------------------------------------------------------------------------
        /// <summary>Adds to a counted achievement.</summary>
        public static void Add(string id, int n = 1) { if (Instance != null) Instance.Set(id, Instance.Get(id) + n); }
        /// <summary>Raises a "best so far" achievement (a streak, a combo) to at least v.</summary>
        public static void Best(string id, int v) { if (Instance != null && v > Instance.Get(id)) Instance.Set(id, v); }
        public static void Done(string id) { Best(id, 1); }

        void Set(string id, int v)
        {
            Load();
            Def d = Array.Find(All, x => x.Id == id);
            if (d == null) return;
            if (unlocked.ContainsKey(id)) return;
            progress[id] = v;
            if (v >= d.Target) { unlocked[id] = DateTime.Now; banners.Add(d); Plugin.Log.LogInfo("Achievement unlocked: " + d.Name); }
            dirty = true;
        }

        /// <summary>Moss Grotto and Moss Mother: the start of the game, left out of combat and golden berry achievements.</summary>
        public static bool StartingArea { get { string a = BossCard.CurrentAreaName; return a != null && a.IndexOf("Moss Grotto", StringComparison.OrdinalIgnoreCase) >= 0; } }
        static bool StartingBoss(string title) { return title != null && title.IndexOf("Moss Mother", StringComparison.OrdinalIgnoreCase) >= 0; }

        // combat: dodges (blocked enemy hits while dashing), once per attacker per dash; a real hit ends the streak
        readonly HashSet<int> dodgedThisDash = new HashSet<int>();
        int dodgeStreak, dodgeDash = -1;
        public static void Dodged(GameObject attacker)
        {
            if (Instance == null || attacker == null || StartingArea || !BossCard.InFight) return;   // boss fights only (user 2026-10-13)
            var a = Instance;
            if (a.dodgeDash != MoveStats.Dashes) { a.dodgeDash = MoveStats.Dashes; a.dodgedThisDash.Clear(); }
            if (!a.dodgedThisDash.Add(attacker.transform.root.GetInstanceID())) return;
            a.dodgeStreak++;
            MoveStats.Dodges = a.dodgeStreak;
            Best("untouchable", a.dodgeStreak);
        }
        public static void DashEnemyHits(int n) { if (n > 0 && !StartingArea) Best("combo", n); }
        string pogoScene; int pogoCount;
        public static void Pogoed()
        {
            if (Instance == null || StartingArea) return;
            var a = Instance;
            string scene = GameManager.SilentInstance != null ? GameManager.SilentInstance.sceneName : "";
            if (scene != a.pogoScene) { a.pogoScene = scene; a.pogoCount = 0; }
            Best("pogo", ++a.pogoCount);
        }
        public static void Sneezed() { Done("achoo"); }
        public static void Photo() { Add("photographer"); }
        public static void CornerCorrected() { Add("corner_kid"); }

        // boss fights: whether anything but the dash hurt the boss
        static bool otherAttack;
        public static void FightStarted() { otherAttack = false; }
        public static void UsedNonDash() { otherAttack = true; }
        public static void BossBeaten(string title, float time, int dashes, int hits, bool golden, bool assist, int goldens, int beaten, int goldenBeaten)
        {
            if (assist || StartingBoss(title)) return;
            PlayerData pd = PlayerData.instance;
            if (!otherAttack && pd != null && pd.act2Started) Done("dash_master");   // an Act 2 (or later) boss
            if (hits == 0) Done("flawless");
            if (time >= 0f && time < 60f) Done("speedrunner");
            if (goldens > 0) Done("first_golden");
            Best("golden_hoard", goldens);
            Best("golden_all", goldens);
        }

        // ---- polling: things that are easiest to watch frame by frame -----------------------------------------
        int lastWave = -1, lastHyper = -1, lastSuper = -1, lastHp = -1;
        float hyperAt = -9f, climbCarry, glideFor;
        bool wasBench, wasDead, wasLocked;
        float lastY;
        bool loggedError;
        void Update()
        {
            try { Tick(); }
            catch (Exception e) { if (!loggedError) { loggedError = true; Plugin.Log.LogError("Achievements: " + e); } }
        }

        // Silksong's InputHandler hides and locks the cursor every frame; while Settings or Records is open it stays free
        void LateUpdate()
        {
            MenuMouse.PadActive = MenuMouse.PadHints(MenuMouse.PadActive);
            if (AssistMode.IsOpen || BossCard.RecordsOpen) { Cursor.visible = !MenuMouse.PadActive; Cursor.lockState = CursorLockMode.None; }
        }

        void Tick()
        {
            float dt = Time.unscaledDeltaTime;
#if TESTHARNESS
            // test: F8 previews the unlock banner, one tier after another, then The Summit (nothing is unlocked)
            var tk = UnityEngine.InputSystem.Keyboard.current;
            if (tk != null && tk.f8Key.wasPressedThisFrame)
            {
                Def pick = testTier == 4 ? Array.Find(All, x => x.Id == "summit") : Array.Find(All, x => (int)x.Tier == testTier && x.Id != "summit");
                testTier = (testTier + 1) % 5;   // Easy, Medium, Hard, Farewell, then The Summit
                banners.Add(pick);
            }
#endif
            UpdateBanners(dt);
            if (dirty && (saveIn -= dt) <= 0f) { saveIn = 2f; Save(); }
            HeroController h = HeroController.SilentInstance;
            GameManager gm = GameManager.SilentInstance;
            var c = Plugin.Controller;
            if (!Plugin.Ready || h == null || gm == null || c == null || gm.IsMenuScene()) return;
            Load();

            if (lastWave >= 0 && MoveStats.Wavedashes > lastWave) Add("wavedasher", MoveStats.Wavedashes - lastWave);
            if ((lastHyper >= 0 && MoveStats.Hypers + MoveStats.Wavedashes > lastHyper)) hyperAt = Time.unscaledTime;
            if (lastSuper >= 0 && MoveStats.Supers > lastSuper && Time.unscaledTime - hyperAt < 0.9f) Done("hyperactive");
            lastWave = MoveStats.Wavedashes; lastHyper = MoveStats.Hypers + MoveStats.Wavedashes; lastSuper = MoveStats.Supers;

            float y = h.transform.position.y;
            if (c.State == MadelineController.StClimb && y > lastY && y - lastY < 1f)
            {
                climbCarry += y - lastY;
                if (climbCarry >= 1f) { int m = (int)climbCarry; climbCarry -= m; Add("climber", m); }
            }
            lastY = y;

            if (c.Gliding && !c.OnGround) { glideFor += Time.deltaTime; Best("jellyfish", (int)glideFor); } else glideFor = 0f;

            bool bench = h.playerData.atBench;
            if (bench && !wasBench) Add("sleepy");
            wasBench = bench;
            bool dead = h.cState.dead;
            if (dead && !wasDead) Add("skill_issue");
            wasDead = dead;
            bool locked = c.DashLocked;
            if (locked && !wasLocked) Add("spam_dash");
            wasLocked = locked;
            int hp = h.playerData.health;
            if (lastHp >= 0 && hp < lastHp && !c.EnemyImmune) { dodgeStreak = 0; MoveStats.Dodges = 0; }
            lastHp = hp;

            if (!unlocked.ContainsKey("summit"))
            {
                bool all = true;
                foreach (Def od in All) if (od.Id != "summit" && !unlocked.ContainsKey(od.Id)) { all = false; break; }
                if (all) Done("summit");
            }
            skinsWorn.Add(AssistMode.SkinName);
            Best("fashionista", skinsWorn.Count);
            Best("tourist", BossCard.AreasVisited);
            Best("night_owl", (int)(gm.PlayTime / 3600f));
        }

        // ---- saving -------------------------------------------------------------------------------------------
        bool dirty; float saveIn = 2f;
        static string FilePath { get { return Path.Combine(BossCard.DataDir ?? "", "achievements.cfg"); } }
        void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string[] kv = line.Split('=');
                    if (kv.Length != 2) continue;
                    if (kv[0] == "skins") { foreach (string s in kv[1].Split(',')) if (s.Length > 0) skinsWorn.Add(s); continue; }
                    string[] v = kv[1].Split('|');
                    int p; if (int.TryParse(v[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out p)) progress[kv[0]] = p;
                    long ticks; if (v.Length > 1 && long.TryParse(v[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)) unlocked[kv[0]] = new DateTime(ticks);
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("achievements.cfg: " + e.Message); }
        }
        void Save()
        {
            dirty = false;
            try
            {
                var lines = new List<string>();
                foreach (Def d in All)
                {
                    int p; progress.TryGetValue(d.Id, out p);
                    DateTime w;
                    lines.Add(d.Id + "=" + p.ToString(CultureInfo.InvariantCulture) + (unlocked.TryGetValue(d.Id, out w) ? "|" + w.Ticks.ToString(CultureInfo.InvariantCulture) : ""));
                }
                lines.Add("skins=" + string.Join(",", new List<string>(skinsWorn).ToArray()));
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (Exception e) { Plugin.Log.LogWarning("achievements.cfg: " + e.Message); }
        }
        void OnApplicationQuit() { if (dirty) Save(); }

        // ---- art ----------------------------------------------------------------------------------------------
        static GuiArt Art { get { return SpeedrunTimer.Art; } }
        internal static GuiArt.Img IconFor(Kind k)
        {
            if (Art == null) return null;
            switch (k)
            {
                case Kind.Movement: return Art.Image("collectables/dashes");
                case Kind.Combat: return Art.Image("collectables/skullGold") ?? Art.Skull;
                case Kind.Golden: return BossCard.PixelBerry() ?? Art.Image("collectables/goldberry");
                case Kind.Explore: return Art.Image("collectables/strawberry");
                case Kind.Summit: return Art.Image("collectables/heartgem/2/spin" + ((int)(Time.unscaledTime * 12f) % 11).ToString("00")) ?? Art.Image("collectables/heartgem/2/spin00");   // the gold crystal heart, spinning
                default: return Art.Image("collectables/heartgem/0/spin00");
            }
        }

        /// <summary>A soft glow: rings of the colour fading outward around r.</summary>
        public static void Glow(Rect r, Color c, float size, float strength)
        {
            const int rings = 10;
            for (int i = rings; i >= 1; i--)
            {
                float f = i / (float)rings, grow = size * f;
                GUI.color = new Color(c.r, c.g, c.b, strength * (1f - f) * (1f - f) * 0.5f);
                GUI.DrawTexture(new Rect(r.x - grow, r.y - grow, r.width + grow * 2f, r.height + grow * 2f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
        static void Border(Rect r, float w, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, w), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - w, r.width, w), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - w, r.y, w, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // ---- the grid (the Records menu's Achievements tab) ----------------------------------------------------
        public static int Selected;   // a position in Order()
        /// <summary>Sorting (user 2026-10-13): easiest first by default, or hardest first.</summary>
        public static int SortMode;   // 0 easiest first, 1 hardest first, 2 category A-Z, 3 category Z-A
        static readonly string[] SortNames = { "Sort: Easiest first", "Sort: Hardest first", "Sort: Category A-Z", "Sort: Category Z-A" };
        static int[] Order()
        {
            var idx = new List<int>();
            for (int i = 0; i < All.Length; i++) idx.Add(i);
            idx.Sort((a, b) =>
            {
                int c;
                if (SortMode >= 2)
                {
                    c = string.Compare(KindNames[(int)All[a].Kind], KindNames[(int)All[b].Kind], StringComparison.Ordinal);
                    if (SortMode == 3) c = -c;
                    if (c == 0) c = All[a].Tier.CompareTo(All[b].Tier);   // inside a category: easiest first
                }
                else c = SortMode == 1 ? All[b].Tier.CompareTo(All[a].Tier) : All[a].Tier.CompareTo(All[b].Tier);
                return c != 0 ? c : a.CompareTo(b);
            });
            return idx.ToArray();
        }
        static Rect sortRect;

        // ---- resetting (user 2026-10-13): one achievement or all, behind a question so a stray press does nothing ----
        // The question defaults to "No"; "Reset all" must be held for a moment before it happens.
        public static bool Confirming;
        static int choice;            // 0 No, 1 Reset this one, 2 Reset all
        static float holdAll;
        static readonly Rect[] choiceRects = new Rect[3];
        const float HoldAllTime = 1.5f;
        static readonly string[] Choices = { "No", "Reset this one", "Reset ALL" };
        public static void OpenConfirm() { Confirming = true; choice = 0; holdAll = 0f; Sfx.Play(Sounds.MenuPause.Id); }
        public static void CancelConfirm() { Confirming = false; holdAll = 0f; Sfx.Play(Sounds.MenuUnpause.Id); }
        public static void ConfirmInput(bool left, bool right, bool confirm, bool held)
        {
            int was = choice;
            if (left) choice = (choice + 2) % 3;
            if (right) choice = (choice + 1) % 3;
            var m = UnityEngine.InputSystem.Mouse.current;
            Vector2 g = MenuMouse.Gui();
            bool mouseDown = false;
            if (m != null)
            {
                if (m.delta.ReadValue().sqrMagnitude > 0.5f || m.leftButton.wasPressedThisFrame)
                    for (int i = 0; i < 3; i++) if (choiceRects[i].Contains(g)) choice = i;
                if (m.leftButton.wasPressedThisFrame && choiceRects[choice].Contains(g)) confirm = true;
                mouseDown = m.leftButton.isPressed && choiceRects[2].Contains(g);
            }
            if (choice != was) { holdAll = 0f; Sfx.Play(Sounds.MenuRolloverDown.Id); }
            if (choice == 2)
            {
                // held, not pressed: the bar fills, and only a full bar resets everything
                if (held || mouseDown) holdAll += Time.unscaledDeltaTime; else holdAll = Math.Max(0f, holdAll - Time.unscaledDeltaTime * 2f);
                if (holdAll >= HoldAllTime) { Instance?.ResetAll(); Confirming = false; holdAll = 0f; Sfx.Play(Sounds.CardOut.Id); }
                return;
            }
            if (!confirm) return;
            if (choice == 0) { CancelConfirm(); return; }
            Instance?.ResetOne(All[Order()[Selected]].Id);
            Confirming = false;
            Sfx.Play(Sounds.CardOut.Id);
        }
        void ResetOne(string id)
        {
            Load();
            progress.Remove(id); unlocked.Remove(id);
            if (id == "fashionista") skinsWorn.Clear();
            if (id == "untouchable") { dodgeStreak = 0; MoveStats.Dodges = 0; }
            dirty = true; Save();
            Plugin.Log.LogInfo("Achievement reset: " + id);
        }
        void ResetAll()
        {
            Load();
            progress.Clear(); unlocked.Clear(); skinsWorn.Clear(); banners.Clear(); bannerT = -1f;
            dodgeStreak = 0; MoveStats.Dodges = 0;
            dirty = true; Save();
            Plugin.Log.LogInfo("Achievements: all reset");
        }

        static void DrawConfirm(float sw, float alpha)
        {
            var ink = new Color(0f, 0f, 0f, alpha);
            float s = Screen.height / 1080f;
            GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha); GUI.DrawTexture(new Rect(0f, 0f, sw, 1080f), Texture2D.whiteTexture);
            Def d = All[Order()[Selected]];
            Color tc = TierColor(d.Tier, Time.unscaledTime); tc.a = alpha;
            float pw = 900f, ph = 300f, px = sw / 2f - pw / 2f, py = 540f - ph / 2f;
            var pr = new Rect(px, py, pw, ph);
            Glow(pr, new Color(1f, 0.3f, 0.3f), 24f, 0.4f * alpha);
            GUI.color = new Color(0.07f, 0.06f, 0.09f, 0.97f * alpha); GUI.DrawTexture(pr, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.3f, 0.3f, alpha); GUI.DrawTexture(new Rect(px, py, pw, 5f), Texture2D.whiteTexture); GUI.color = Color.white;
            const string title = "Reset achievements?";
            Art.DrawOutlined(title, sw / 2f - Art.Measure(title) * 0.8f / 2f, py + 26f, 0.8f, new Color(1f, 1f, 1f, alpha), ink, 3f);
            string sub = choice == 2 ? "Every achievement goes back to zero. This can't be undone." : "\"" + d.Name + "\" goes back to zero. This can't be undone.";
            float ss = 0.5f; while (ss > 0.3f && Art.Measure(sub) * ss > pw - 60f) ss -= 0.02f;
            Art.DrawOutlined(sub, sw / 2f - Art.Measure(sub) * ss / 2f, py + 26f + Art.LineHeight * 0.8f + 14f, ss, new Color(0.8f, 0.8f, 0.8f, alpha), ink, 2f);
            // the three choices; "No" is picked first
            float bs = 0.55f, gap = 30f, tw = -gap;
            for (int i = 0; i < 3; i++) tw += Art.Measure(Choices[i]) * bs + 50f + gap;
            float bx = sw / 2f - tw / 2f, by = py + ph - 110f, bh = Art.LineHeight * bs + 22f;
            for (int i = 0; i < 3; i++)
            {
                float bw = Art.Measure(Choices[i]) * bs + 50f;
                var r = new Rect(bx, by, bw, bh);
                choiceRects[i] = r;
                bool sel = i == choice;
                Color col = i == 0 ? new Color(0.85f, 0.85f, 0.85f, alpha) : new Color(1f, 0.35f, 0.35f, alpha);
                GUI.color = new Color(col.r, col.g, col.b, (sel ? 0.28f : 0.08f) * alpha); GUI.DrawTexture(r, Texture2D.whiteTexture);
                if (i == 2 && sel && holdAll > 0f) { GUI.color = new Color(1f, 0.3f, 0.3f, 0.55f * alpha); GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(holdAll / HoldAllTime), r.height), Texture2D.whiteTexture); }
                GUI.color = Color.white;
                if (sel) Border(r, 3f, new Color(1f, 1f, 1f, alpha));
                Art.DrawOutlined(Choices[i], r.x + 25f, r.y + 11f, bs, sel ? new Color(1f, 1f, 1f, alpha) : col, ink, 2f);
                bx += bw + gap;
            }
            string hint = choice == 2 ? "Hold to reset everything" : "Press to choose   Back to cancel";
            Art.DrawOutlined(hint, sw / 2f - Art.Measure(hint) * 0.4f / 2f, by + bh + 16f, 0.4f, new Color(0.6f, 0.6f, 0.6f, alpha), ink, 2f);
        }
        public static void ToggleSort() { SortMode = (SortMode + 1) % SortNames.Length; Selected = 0; Sfx.Play(Sounds.MenuButtonSelect.Id); }
        static readonly Rect[] cellRects = new Rect[All.Length];
        const int Cols = 8;

        /// <summary>Arrow keys / stick move the cursor; the mouse picks the square it is over.</summary>
        public static void Navigate(bool left, bool right, bool up, bool down)
        {
            int n = All.Length, was = Selected;
            if (left) Selected = (Selected + n - 1) % n;
            if (right) Selected = (Selected + 1) % n;
            if (up) Selected = Selected - Cols >= 0 ? Selected - Cols : Selected;
            if (down) Selected = Selected + Cols < n ? Selected + Cols : Selected;
            var m = UnityEngine.InputSystem.Mouse.current;
            if (MenuMouse.Clicked(sortRect)) { ToggleSort(); return; }
            if (m != null && (m.delta.ReadValue().sqrMagnitude > 0.5f || m.leftButton.wasPressedThisFrame))
            {
                Vector2 g = MenuMouse.Gui();
                for (int i = 0; i < n; i++) if (cellRects[i].Contains(g)) { Selected = i; break; }
            }
            if (m != null && m.rightButton.wasPressedThisFrame && cellRects[Selected].Contains(MenuMouse.Gui())) { OpenConfirm(); return; }
            if (Selected != was) Sfx.Play(Sounds.MenuRolloverDown.Id);
        }

        public static void DrawGrid(float sw, float top, float alpha)
        {
            if (Art == null) return;
            Instance?.Load();
            float t = Time.unscaledTime;
            var ink = new Color(0f, 0f, 0f, alpha);

            // tier totals
            float ts = 0.5f, gap = 46f, tw = -gap;
            var labels = new string[4];
            for (int i = 0; i < 4; i++) { labels[i] = TierNames[i] + "  " + UnlockedCount((Tier)i) + "/" + TierCount((Tier)i); tw += 26f + Art.Measure(labels[i]) * ts + gap; }
            float tx = sw / 2f - tw / 2f;
            for (int i = 0; i < 4; i++)
            {
                Color tc = TierColor((Tier)i, t); tc.a = alpha;
                GUI.color = tc; GUI.DrawTexture(new Rect(tx, top + 8f, 16f, 16f), Texture2D.whiteTexture); GUI.color = Color.white;
                Art.DrawOutlined(labels[i], tx + 26f, top, ts, new Color(0.9f, 0.9f, 0.9f, alpha), ink, 2f);
                tx += 26f + Art.Measure(labels[i]) * ts + gap;
            }
            top += Art.LineHeight * ts + 14f;
            {
                // the sort switch, a small button: click it, or Tab / Triangle
                string label = SortNames[SortMode];
                float bs = 0.42f, bw = Art.Measure(label) * bs + 30f, bh = Art.LineHeight * bs + 12f;
                sortRect = new Rect(sw / 2f - bw / 2f, top, bw, bh);
                bool hov = sortRect.Contains(MenuMouse.Gui());
                MenuStyle.Panel(sortRect, alpha * (hov ? 2f : 1f));
                Art.DrawOutlined(label, sortRect.x + 15f, top + 6f, bs, hov ? new Color(1f, 0.9f, 0.5f, alpha) : new Color(0.85f, 0.85f, 0.85f, alpha), ink, 2f);
                top += bh + 26f;
            }

            // the squares
            float cell = 104f, cg = 30f;
            int rows = (All.Length + Cols - 1) / Cols;
            float gw = Cols * cell + (Cols - 1) * cg, gx = sw / 2f - gw / 2f;
            Selected = Mathf.Clamp(Selected, 0, All.Length - 1);
            int[] order = Order();
            for (int i = 0; i < All.Length; i++)
            {
                Def d = All[order[i]];
                int col = i % Cols, row = i / Cols;
                var r = new Rect(gx + col * (cell + cg), top + row * (cell + cg), cell, cell);
                cellRects[i] = r;
                DateTime when;
                bool got = Unlocked(d, out when), sel = i == Selected;
                Color tc = TierColor(d.Tier, t + i * 0.3f);
                float pulse = 0.75f + 0.25f * Mathf.Sin(t * 2.4f + i * 0.7f);
                if (sel) r = new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 12f);
                // the glow in the tier's colour: bright once unlocked, a faint hint while locked
                // two glows (user 2026-10-13: the category's colour must show too): the tier's from the top left,
                // the category's from the bottom right, each spilling out its own side
                {
                    Color kcg = KindColor(d.Kind);
                    float gs = (got ? 0.9f * pulse : 0.45f) * alpha * (sel ? 1.25f : 1f), off = r.width * 0.12f;
                    Glow(new Rect(r.x - off * 0.5f, r.y - off * 0.5f, r.width - off * 0.5f, r.height - off * 0.5f), tc, got ? 16f : 11f, gs);
                    Glow(new Rect(r.x + off, r.y + off, r.width - off * 0.5f, r.height - off * 0.5f), kcg, got ? 16f : 11f, gs);
                }
                GUI.color = new Color(0.07f, 0.06f, 0.09f, 0.95f * alpha); GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = new Color(tc.r, tc.g, tc.b, (got ? 0.22f : 0.07f) * alpha); GUI.DrawTexture(r, Texture2D.whiteTexture);
                // the outline, half and half (user 2026-10-13): top and left in the tier's colour, bottom and right in the category's
                {
                    float bw = sel ? 5f : 4f, ba = (got ? 1f : 0.75f) * alpha;
                    Color kc = KindColor(d.Kind);
                    GUI.color = new Color(tc.r, tc.g, tc.b, ba);
                    GUI.DrawTexture(new Rect(r.x, r.y, r.width, bw), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.x, r.y, bw, r.height), Texture2D.whiteTexture);
                    GUI.color = new Color(kc.r, kc.g, kc.b, ba);
                    GUI.DrawTexture(new Rect(r.x, r.yMax - bw, r.width, bw), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMax - bw, r.y, bw, r.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    if (sel) Border(new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 12f), 2f, new Color(1f, 1f, 1f, alpha * (0.6f + 0.4f * Mathf.Sin(t * 6f))));
                }
                // its icon: full colour once unlocked, a dark silhouette while locked
                GuiArt.Img icon = IconFor(d.Kind);
                if (icon != null)
                {
                    float isz = r.width * 0.56f, iw = isz * icon.W / Math.Max(icon.W, icon.H), ih = isz * icon.H / Math.Max(icon.W, icon.H);
                    float bob = got ? Mathf.Sin(t * 3f + i) * 2f : 0f;
                    var ir = new Rect(r.center.x - iw / 2f, r.center.y - ih / 2f - 6f + bob, iw, ih);
                    GuiArt.Draw(icon, ir, got ? new Color(1f, 1f, 1f, alpha) : new Color(0.16f, 0.15f, 0.2f, alpha));
                }
                // progress along the bottom for counted ones still locked
                if (!got && d.Target > 1)
                {
                    float p = Progress(d) / (float)d.Target;
                    var bar = new Rect(r.x + 10f, r.yMax - 16f, r.width - 20f, 6f);
                    GUI.color = new Color(1f, 1f, 1f, 0.12f * alpha); GUI.DrawTexture(bar, Texture2D.whiteTexture);
                    GUI.color = new Color(tc.r, tc.g, tc.b, 0.9f * alpha); GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * p, bar.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                if (!got && d.Target <= 1)
                {
                    const string q = "?";
                    Art.DrawOutlined(q, r.xMax - 10f - Art.Measure(q) * 0.42f, r.y + 6f, 0.42f, new Color(tc.r, tc.g, tc.b, 0.75f * alpha), ink, 2f);
                }
            }
            float y = top + rows * (cell + cg) + 14f;

            // the chosen square's details: name in its tier colour, a tier pill, how to get it, progress or the date
            {
                Def d = All[Order()[Selected]];
                DateTime when;
                bool got = Unlocked(d, out when);
                Color tc = TierColor(d.Tier, t); tc.a = alpha;
                bool mossNote = d.Kind == Kind.Combat || d.Kind == Kind.Golden || d.Id == "speedrunner";
                float pw = Math.Min(sw - 160f, 980f), px = sw / 2f - pw / 2f, ph = mossNote ? 222f : 190f;
                MenuStyle.Panel(new Rect(px, y, pw, ph), alpha);
                GUI.color = tc; GUI.DrawTexture(new Rect(px, y, 6f, ph), Texture2D.whiteTexture); GUI.color = Color.white;
                float nx = px + 34f, ny = y + 20f, ns = 0.85f;
                Art.DrawOutlined(d.Name, nx, ny, ns, got ? tc : new Color(0.85f, 0.85f, 0.85f, alpha), ink, 3f);
                // tier pill
                string tier = TierNames[(int)d.Tier].ToUpperInvariant();
                float ps = 0.42f, pillW = Art.Measure(tier) * ps + 28f, pillX = nx + Art.Measure(d.Name) * ns + 24f, pillY = ny + (Art.LineHeight * ns - (Art.LineHeight * ps + 10f)) / 2f;
                GUI.color = new Color(tc.r, tc.g, tc.b, 0.85f * alpha); GUI.DrawTexture(new Rect(pillX, pillY, pillW, Art.LineHeight * ps + 10f), Texture2D.whiteTexture); GUI.color = Color.white;
                Art.DrawOutlined(tier, pillX + 14f, pillY + 5f, ps, new Color(1f, 1f, 1f, alpha), ink, 2f);
                // the category tag beside it, in the category's colour
                string kind = KindNames[(int)d.Kind].ToUpperInvariant();
                Color kc = KindColor(d.Kind);
                float kx = pillX + pillW + 12f, kwid = Art.Measure(kind) * ps + 28f;
                GUI.color = new Color(kc.r, kc.g, kc.b, 0.85f * alpha); GUI.DrawTexture(new Rect(kx, pillY, kwid, Art.LineHeight * ps + 10f), Texture2D.whiteTexture); GUI.color = Color.white;
                Art.DrawOutlined(kind, kx + 14f, pillY + 5f, ps, new Color(1f, 1f, 1f, alpha), ink, 2f);
                ny += Art.LineHeight * ns + 10f;
                Art.DrawOutlined(HowText(d), nx, ny, 0.55f, new Color(0.88f, 0.88f, 0.88f, alpha), ink, 2f);
                ny += Art.LineHeight * 0.55f + 14f;
                if (got)
                {
                    string s = "Unlocked " + when.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
                    Art.DrawOutlined(s, nx, ny, 0.5f, tc, ink, 2f);
                }
                else if (d.Target > 1)
                {
                    int p = Progress(d);
                    var bar = new Rect(nx, ny + 10f, pw - 260f, 12f);
                    GUI.color = new Color(1f, 1f, 1f, 0.12f * alpha); GUI.DrawTexture(bar, Texture2D.whiteTexture);
                    GUI.color = tc; GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * p / d.Target, bar.height), Texture2D.whiteTexture); GUI.color = Color.white;
                    string s = p + " / " + d.Target;
                    Art.DrawOutlined(s, bar.xMax + 20f, ny, 0.5f, new Color(0.9f, 0.9f, 0.9f, alpha), ink, 2f);
                }
                else Art.DrawOutlined("Locked", nx, ny, 0.5f, new Color(0.6f, 0.6f, 0.6f, alpha), ink, 2f);
                if (mossNote)
                {
                    const string note = "Moss Grotto and Moss Mother don't count.";
                    Art.DrawOutlined(note, nx, ny + Art.LineHeight * 0.5f + 12f, 0.4f, new Color(0.55f, 0.55f, 0.55f, alpha), ink, 2f);
                }
            }
            if (Confirming) DrawConfirm(sw, alpha);
        }

        // ---- the unlock banner: slides in at the top right in the tier's colour --------------------------------
        readonly List<Def> banners = new List<Def>();
        float bannerT = -1f;
        void UpdateBanners(float dt)
        {
            if (banners.Count == 0) return;
            if (bannerT < 0f)
            {
                bannerT = 0f;
                // a bigger Celeste sound for each tier: a refill crystal, a feather, a golden berry, and Farewell's
                // golden berry with a revive swell and a wall-bounce crack under it
                Sfx.Play(Sounds.CardIn.Id, 0.7f);
                if (banners[0].Id == "summit") { summitCue = 0; popSoundDone = true; }
                else switch (banners[0].Tier)
                {
                    case Tier.Easy: Sfx.Play(Sounds.Refill.Id); break;
                    case Tier.Medium: Sfx.Play(Sounds.FeatherGet.Id); break;
                    case Tier.Hard: Sfx.Play(Sounds.GoldenGet.Id); break;
                    default: Sfx.Play(Sounds.GoldenGet.Id); Sfx.Play(Sounds.Revive.Id, 0.8f); Sfx.Play(Sounds.JumpSuperwall.Id, 0.6f); break;
                }
                popSoundDone = false;
            }
            float before = bannerT;
            bannerT += dt;
            if (banners[0].Id == "summit")
            {
                // The Summit: a rising chain of Celeste sounds instead of one
                while (summitCue < SummitCues.Length && bannerT >= SummitCues[summitCue].Key)
                {
                    string id = SummitCues[summitCue].Value;
                    float vol = id == Sounds.Refill.Id ? 0.6f : 1f;
                    Sfx.Play(id, vol);
                    summitCue++;
                }
            }
            // the second beat: when the icon pops, a small sparkle sound
            if (!popSoundDone && before < 0.32f && bannerT >= 0.32f) { popSoundDone = true; Sfx.Play(Sounds.Refill.Id, banners[0].Tier >= Tier.Hard ? 0.8f : 0.45f); }
            if (bannerT > LifeOf(banners[0])) { banners.RemoveAt(0); bannerT = -1f; }
        }
        bool popSoundDone;
        int summitCue;
        static readonly KeyValuePair<float, string>[] SummitCues =
        {
            new KeyValuePair<float, string>(0.0f, Sounds.FeatherStateEnd.Id), new KeyValuePair<float, string>(0.35f, Sounds.Refill.Id),
            new KeyValuePair<float, string>(0.55f, Sounds.Refill.Id), new KeyValuePair<float, string>(0.75f, Sounds.Refill.Id),
            new KeyValuePair<float, string>(1.0f, Sounds.JumpSuperwall.Id), new KeyValuePair<float, string>(1.05f, Sounds.GoldenGet.Id),
            new KeyValuePair<float, string>(1.1f, Sounds.Revive.Id), new KeyValuePair<float, string>(1.9f, Sounds.FeatherGet.Id),
            new KeyValuePair<float, string>(2.6f, Sounds.CampfireStand.Id),
        };
        int testTier;
        const float BannerLife = 4.5f, SummitLife = 8f;
        static float LifeOf(Def d) { return d.Id == "summit" ? SummitLife : BannerLife; }

        // The Summit (every other achievement): a full-screen celebration rather than a banner. The screen dims, a
        // white flash, rainbow light rays turning behind the spinning gold crystal heart, shockwave rings, confetti
        // and golden berries raining down, the title in rainbow letters, and the screen edges glowing every colour.
        void DrawSummit(Def d)
        {
            float t = bannerT, life = SummitLife, now = Time.unscaledTime;
            float s = Screen.height / 1080f, sw = Screen.width / s;
            Matrix4x4 old = GUI.matrix;
            GUI.depth = -5;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            float fade = t > life - 0.8f ? Mathf.Clamp01((life - t) / 0.8f) : 1f;
            Vector2 c = new Vector2(sw / 2f, 430f);
            Func<float, float, Color> rainbow = (off, a) => { Color col = Color.HSVToRGB(Mathf.Repeat(now * 0.3f + off, 1f), 0.55f, 1f); col.a = a; return col; };

            // dim
            GUI.color = new Color(0f, 0f, 0f, 0.6f * Mathf.Clamp01(t / 0.6f) * fade); GUI.DrawTexture(new Rect(0f, 0f, sw, 1080f), Texture2D.whiteTexture);
            // rays: thin rotated bars turning slowly behind the heart
            float rayA = Mathf.Clamp01((t - 0.9f) / 0.6f) * fade;
            if (rayA > 0f)
            {
                Matrix4x4 m0 = GUI.matrix;
                for (int i = 0; i < 18; i++)
                {
                    GUI.matrix = m0;
                    GUIUtility.RotateAroundPivot(i * 20f + now * 12f, c * s);
                    float len = 900f, wdt = 26f + 18f * Mathf.Sin(now * 2f + i);
                    GUI.color = rainbow(i / 18f, 0.10f * rayA);
                    GUI.DrawTexture(new Rect(c.x, c.y - wdt / 2f, len, wdt), Texture2D.whiteTexture);
                    GUI.color = rainbow(i / 18f, 0.06f * rayA);
                    GUI.DrawTexture(new Rect(c.x, c.y - wdt, len, wdt * 2f), Texture2D.whiteTexture);
                }
                GUI.matrix = m0;
            }
            // a soft halo behind the heart
            Glow(new Rect(c.x - 90f, c.y - 90f, 180f, 180f), rainbow(0f, 1f), 120f, 0.7f * rayA);
            // shockwave rings
            for (int k = 0; k < 4; k++)
            {
                float rt = (t - 1.0f - k * 0.3f) / 1.4f;
                if (rt <= 0f || rt >= 1f) continue;
                float rs = 120f + rt * 900f;
                Border(new Rect(c.x - rs / 2f, c.y - rs / 2f, rs, rs), 6f * (1f - rt) + 1f, rainbow(k * 0.25f, (1f - rt) * 0.8f * fade));
            }
            // the gold crystal heart, popping in
            GuiArt.Img heart = IconFor(Kind.Summit);
            float pop = t < 1.0f ? 0f : BackOut((t - 1.0f) / 0.5f);
            if (heart != null && pop > 0f)
            {
                float hs = 230f * pop, hh = hs * heart.H / Math.Max(1, heart.W), bob = Mathf.Sin(now * 2.2f) * 8f;
                GuiArt.Draw(heart, new Rect(c.x - hs / 2f + 8f, c.y - hh / 2f + 10f + bob, hs, hh), new Color(0f, 0f, 0f, 0.4f * fade));
                GuiArt.Draw(heart, new Rect(c.x - hs / 2f, c.y - hh / 2f + bob, hs, hh), new Color(1f, 1f, 1f, fade));
            }
            // sparks bursting from the heart
            {
                float pt = t - 1.0f;
                if (pt > 0f && pt < 2.5f)
                    for (int i = 0; i < 90; i++)
                    {
                        float ang = Hash(i, 11) * Mathf.PI * 2f, spd = 250f + Hash(i, 12) * 650f, lifeI = 1f + Hash(i, 13) * 1.4f;
                        if (pt > lifeI) continue;
                        float f = pt / lifeI;
                        Vector2 p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd * pt * (1f - 0.5f * f) + new Vector2(0f, 160f * pt * pt);
                        float sz = (4f + Hash(i, 14) * 7f) * (1f - f * 0.5f);
                        GUI.color = i % 5 == 0 ? new Color(1f, 1f, 1f, (1f - f) * fade) : rainbow(i * 0.07f, (1f - f) * fade);
                        GUI.DrawTexture(new Rect(p.x - sz / 2f, p.y - sz / 2f, sz, sz), Texture2D.whiteTexture);
                        if (i % 4 == 0) { GUI.DrawTexture(new Rect(p.x - sz * 1.6f, p.y - 1f, sz * 3.2f, 2f), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(p.x - 1f, p.y - sz * 1.6f, 2f, sz * 3.2f), Texture2D.whiteTexture); }
                    }
            }
            // confetti and golden berries raining down
            float rainT = t - 1.1f;
            if (rainT > 0f)
            {
                for (int i = 0; i < 140; i++)
                {
                    float x0 = Hash(i, 21) * sw, delay = Hash(i, 22) * 3f, fall = 220f + Hash(i, 23) * 260f;
                    float lt = rainT - delay;
                    if (lt <= 0f) continue;
                    float yy = -30f + lt * fall;
                    if (yy > 1110f) continue;
                    float xx = x0 + Mathf.Sin(lt * (1.5f + Hash(i, 24) * 2f) + i) * 40f;
                    float flip = Mathf.Abs(Mathf.Sin(lt * (4f + Hash(i, 25) * 5f) + i));
                    float cw = 10f + Hash(i, 26) * 8f;
                    GUI.color = rainbow(Hash(i, 27), 0.9f * fade);
                    GUI.DrawTexture(new Rect(xx, yy, cw * flip + 2f, cw * 0.6f), Texture2D.whiteTexture);
                }
                GuiArt.Img berry = BossCard.PixelBerry() ?? (Art != null ? Art.Image("collectables/goldberry") : null);
                if (berry != null)
                    for (int i = 0; i < 14; i++)
                    {
                        float lt = rainT - Hash(i, 31) * 3.5f;
                        if (lt <= 0f) continue;
                        float yy = -80f + lt * (180f + Hash(i, 32) * 120f);
                        if (yy > 1100f) continue;
                        float xx = Hash(i, 33) * (sw - 80f) + Mathf.Sin(lt * 1.3f + i) * 30f, bs = 54f + Hash(i, 34) * 30f;
                        GuiArt.Draw(berry, new Rect(xx, yy, bs, bs), new Color(1f, 1f, 1f, 0.95f * fade));
                    }
            }
            // a white flash as the heart arrives
            float fl = Mathf.Clamp01(1f - Mathf.Abs(t - 1.0f) / 0.35f);
            if (fl > 0f) { GUI.color = new Color(1f, 1f, 1f, 0.85f * fl); GUI.DrawTexture(new Rect(0f, 0f, sw, 1080f), Texture2D.whiteTexture); }
            // the screen edges glow, cycling every colour
            for (int e = 0; e < 14; e++)
            {
                float f = e / 14f, th = 6f + e * 8f;
                GUI.color = rainbow(e * 0.03f, 0.08f * (1f - f) * fade * Mathf.Clamp01(t / 0.8f));
                GUI.DrawTexture(new Rect(0f, 0f, sw, th), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0f, 1080f - th, sw, th), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0f, 0f, th, 1080f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(sw - th, 0f, th, 1080f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            // the words: a small header, then THE SUMMIT letter by letter in rainbow, then the line under it
            if (Art != null)
            {
                var ink = new Color(0f, 0f, 0f, fade);
                const string top = "ACHIEVEMENT UNLOCKED";
                float ha = Mathf.Clamp01((t - 1.3f) / 0.4f) * fade;
                Art.DrawOutlined(top, sw / 2f - Art.Measure(top) * 0.5f / 2f, 640f, 0.5f, new Color(1f, 1f, 1f, ha), new Color(0f, 0f, 0f, ha), 2f);
                string title = d.Name.ToUpperInvariant();
                float ts = 1.8f, tx = sw / 2f - Art.Measure(title) * ts / 2f;
                for (int i = 0; i < title.Length; i++)
                {
                    string ch = title.Substring(i, 1);
                    float lk = Mathf.Clamp01((t - 1.5f - i * 0.06f) / 0.25f), le = BackOut(lk);
                    float cwid = Art.Measure(ch) * ts;
                    if (lk > 0f)
                    {
                        float wave = Mathf.Sin(now * 3f + i * 0.5f) * 6f;
                        float yy = 690f + (1f - le) * 60f + wave;
                        Color lc = rainbow(i * 0.07f, lk * fade);
                        Art.DrawOutlined(ch, tx, yy, ts, lc, new Color(0f, 0f, 0f, lk * fade), 4f);
                    }
                    tx += cwid;
                }
                const string sub = "Every achievement unlocked. You reached the top!";
                float sa = Mathf.Clamp01((t - 2.6f) / 0.5f) * fade;
                Art.DrawOutlined(sub, sw / 2f - Art.Measure(sub) * 0.6f / 2f, 690f + Art.LineHeight * ts + 20f, 0.6f, new Color(1f, 0.95f, 0.8f, sa), new Color(0f, 0f, 0f, sa), 2f);
                // every tier, lit one after another
                for (int k = 0; k < 4; k++)
                {
                    float pk = Mathf.Clamp01((t - 3.0f - k * 0.15f) / 0.2f) * fade;
                    string lbl = TierNames[k] + " " + TierCount((Tier)k) + "/" + TierCount((Tier)k);
                    float lw = Art.Measure(lbl) * 0.45f + 40f, lx = sw / 2f - 2f * 210f + k * 210f + (210f - lw) / 2f, ly = 690f + Art.LineHeight * ts + 90f;
                    Color tcol = TierColor((Tier)k, now); tcol.a = pk;
                    GUI.color = tcol; GUI.DrawTexture(new Rect(lx, ly + 6f, 18f, 18f), Texture2D.whiteTexture); GUI.color = Color.white;
                    Art.DrawOutlined(lbl, lx + 28f, ly, 0.45f, new Color(1f, 1f, 1f, pk), new Color(0f, 0f, 0f, pk), 2f);
                }
            }
            GUI.matrix = old;
            GUI.color = Color.white;
        }

        static float BackOut(float t) { t = Mathf.Clamp01(t) - 1f; const float s = 1.9f; return t * t * ((s + 1f) * t + s) + 1f; }
        static float Hash(int i, int salt) { return Mathf.Repeat(Mathf.Sin(i * 12.9898f + salt * 78.233f) * 43758.5453f, 1f); }
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || banners.Count == 0 || bannerT < 0f || Art == null || TheoCamera.Hiding) return;
            Def d = banners[0];
            if (d.Id == "summit") { DrawSummit(d); return; }
            float t = bannerT, life = BannerLife;
            // in with an overshoot, out with a quick fade to the right
            float inK = BackOut(t / 0.45f), outK = t > life - 0.5f ? Mathf.Clamp01((t - (life - 0.5f)) / 0.5f) : 0f;
            float ease = Mathf.Clamp01(t / 0.2f) * (1f - outK);
            float s = Screen.height / 1080f, sw = Screen.width / s;
            Matrix4x4 old = GUI.matrix;
            GUI.depth = -4;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            float now = Time.unscaledTime;
            Color tc = TierColor(d.Tier, now); tc.a = ease;
            var ink = new Color(0f, 0f, 0f, ease);
            int tier = (int)d.Tier;
            float w = 600f, h = 128f, x = sw - w - 30f + (1f - inK) * (w + 60f) + outK * outK * 300f, y = 150f;
            var r = new Rect(x, y, w, h);

            // Farewell: the screen edges glow purple for a moment
            if (d.Tier == Tier.Farewell)
            {
                float v = Mathf.Clamp01(1f - Mathf.Abs(t - 0.6f) / 1.6f) * (1f - outK);
                for (int e = 0; e < 12; e++)
                {
                    float f = e / 12f, th = 6f + e * 7f;
                    Color vc = TierColor(Tier.Farewell, now + e * 0.1f); vc.a = v * 0.07f * (1f - f);
                    GUI.color = vc;
                    GUI.DrawTexture(new Rect(0f, 0f, sw, th), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(0f, 1080f - th, sw, th), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(0f, 0f, th, 1080f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(sw - th, 0f, th, 1080f), Texture2D.whiteTexture);
                }
                GUI.color = Color.white;
            }

            // the card: a glow that swells as it lands, its dark body, the tier stripe
            float swell = 1f + 0.8f * Mathf.Clamp01(1f - Mathf.Abs(t - 0.35f) / 0.35f);
            Glow(r, tc, 22f * swell + tier * 6f, (0.55f + 0.1f * tier) * ease);
            GUI.color = new Color(0.07f, 0.06f, 0.09f, 0.96f * ease); GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(tc.r, tc.g, tc.b, 0.12f * ease); GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = tc; GUI.DrawTexture(new Rect(x, y, 8f, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + h - 3f, w * Mathf.Clamp01(1f - t / life), 3f), Texture2D.whiteTexture);   // time left
            // a white flash as it lands
            float flashA = Mathf.Clamp01(1f - Mathf.Abs(t - 0.3f) / 0.15f);
            if (flashA > 0f) { GUI.color = new Color(1f, 1f, 1f, 0.5f * flashA * ease); GUI.DrawTexture(r, Texture2D.whiteTexture); }
            // a shine sweeping across, twice for the higher tiers
            for (int pass = 0; pass < (tier >= 2 ? 2 : 1); pass++)
            {
                float st = (t - 0.5f - pass * 1.2f) / 0.6f;
                if (st <= 0f || st >= 1f) continue;
                float sx = x - 80f + st * (w + 160f);
                for (int b = 0; b < 6; b++)
                {
                    float bx = sx + b * 7f - 21f;
                    float cl = Mathf.Max(x, bx), cr = Mathf.Min(x + w, bx + 14f);
                    if (cr <= cl) continue;
                    GUI.color = new Color(1f, 1f, 1f, 0.07f * (1f - Mathf.Abs(b - 2.5f) / 3f) * ease);
                    GUI.DrawTexture(new Rect(cl, y, cr - cl, h), Texture2D.whiteTexture);
                }
            }
            GUI.color = Color.white;

            // the icon pops in after the card lands, with a ring and a burst of sparks in the tier's colour
            GuiArt.Img icon = IconFor(d.Kind);
            Vector2 ic = new Vector2(x + 70f, y + h / 2f);
            float popT = (t - 0.3f) / 0.35f, pop = popT <= 0f ? 0f : BackOut(popT);
            if (icon != null && pop > 0f)
            {
                float isz = 82f * pop, ih = isz * icon.H / Math.Max(1, icon.W);
                float wob = Mathf.Sin(now * 3f) * 2f;
                GuiArt.Draw(icon, new Rect(ic.x - isz / 2f + 4f, ic.y - ih / 2f + 4f + wob, isz, ih), new Color(0f, 0f, 0f, 0.4f * ease));
                GuiArt.Draw(icon, new Rect(ic.x - isz / 2f, ic.y - ih / 2f + wob, isz, ih), new Color(1f, 1f, 1f, ease));
            }
            if (popT > 0f && popT < 2.2f)
            {
                // the shockwave ring: an outlined square growing and fading
                float rg = popT / 2.2f, rs = 40f + rg * (140f + tier * 40f), ra = (1f - rg) * ease;
                Color rc = new Color(tc.r, tc.g, tc.b, ra * 0.9f);
                Border(new Rect(ic.x - rs / 2f, ic.y - rs / 2f, rs, rs), 3f * (1f - rg) + 1f, rc);
            }
            {
                // sparks: more for each tier, flung out from the icon, falling a little, twinkling as they fade
                int count = 14 + tier * 10;
                float pt = t - 0.3f;
                if (pt > 0f && pt < 1.6f)
                    for (int i = 0; i < count; i++)
                    {
                        float ang = Hash(i, 1) * Mathf.PI * 2f, spd = 160f + Hash(i, 2) * (220f + tier * 80f), lifeI = 0.7f + Hash(i, 3) * 0.8f;
                        if (pt > lifeI) continue;
                        float f = pt / lifeI;
                        float dist = spd * pt * (1f - 0.45f * f);
                        Vector2 p = ic + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist + new Vector2(0f, 120f * pt * pt);
                        float sz = (3f + Hash(i, 4) * 5f) * (1f - f * 0.6f);
                        float tw = 0.6f + 0.4f * Mathf.Sin(now * 25f + i);
                        Color pc = i % 4 == 0 ? new Color(1f, 1f, 1f, 1f) : (d.Tier == Tier.Farewell ? TierColor(Tier.Farewell, now + i * 0.37f) : tc);
                        pc.a = (1f - f) * tw * ease;
                        GUI.color = pc;
                        GUI.DrawTexture(new Rect(p.x - sz / 2f, p.y - sz / 2f, sz, sz), Texture2D.whiteTexture);
                        if (i % 3 == 0) { GUI.DrawTexture(new Rect(p.x - sz * 1.5f, p.y - 1f, sz * 3f, 2f), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(p.x - 1f, p.y - sz * 1.5f, 2f, sz * 3f), Texture2D.whiteTexture); }
                    }
                GUI.color = Color.white;
            }

            // the words: the header types itself in, the name rises into place
            string head = "ACHIEVEMENT UNLOCKED  -  " + TierNames[tier].ToUpperInvariant();
            int shown = Mathf.Clamp(Mathf.FloorToInt((t - 0.15f) / 0.018f), 0, head.Length);
            Art.DrawOutlined(head.Substring(0, shown), x + 132f, y + 20f, 0.4f, tc, ink, 2f);
            float ns = 0.75f;
            while (ns > 0.4f && Art.Measure(d.Name) * ns > w - 160f) ns -= 0.05f;
            float nk = Mathf.Clamp01((t - 0.4f) / 0.3f), nease = 1f - (1f - nk) * (1f - nk);
            Art.DrawOutlined(d.Name, x + 132f, y + 20f + Art.LineHeight * 0.4f + 8f + (1f - nease) * 18f, ns, new Color(1f, 1f, 1f, ease * nease), new Color(0f, 0f, 0f, ease * nease), 3f);
            // the tier pips under the name: one lit per tier step
            for (int pi = 0; pi < 4; pi++)
            {
                float pk = Mathf.Clamp01((t - 0.75f - pi * 0.08f) / 0.15f);
                Color pcol = pi <= tier ? new Color(tc.r, tc.g, tc.b, ease * pk) : new Color(1f, 1f, 1f, 0.15f * ease * pk);
                GUI.color = pcol; GUI.DrawTexture(new Rect(x + 132f + pi * 22f, y + h - 26f, 14f, 6f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            GUI.matrix = old;
            GUI.color = Color.white;
        }
    }
}
