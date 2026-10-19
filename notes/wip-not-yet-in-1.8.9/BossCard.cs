using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Celeste's chapter-complete card for each boss (visuals.boss_card_*): when a boss falls, a Celeste area-select
    /// card slides in with the fight's deaths, time and dashes. Beating a boss without healing gives a golden
    /// berry, once per boss and save slot (goldens.cfg). A fight starts when Silksong shows the boss's title
    /// (hooks.boss_title) and ends when one of PlayerData's defeated* flags turns on (hooks.boss_defeated).
    /// </summary>
    public class BossCard : MonoBehaviour
    {
        public static GuiArt Art;
        public static string DataDir;
        public static BossCard Instance;

        class Fight
        {
            public string Id, Title, Sub, Scene;
            public float Time, LeftSceneAt = -1f;
            public int Hits, Heals, Dashes;
            public bool Assist, Invincible, HadGolden, FirstTry;
        }

        /// <summary>A healless attempt (no bind) at a boss whose golden berry isn't collected yet: the berry follows her.</summary>
        public static bool GoldenRun { get { var f = Instance != null ? Instance.fight : null; return f != null && !f.HadGolden && !f.Assist && (f.FirstTry || f.Heals == 0) && f.LeftSceneAt < 0f; } }
        /// <summary>Seconds into the current fight (0 without one): the berry follows her for visuals.golden_intro_time,
        /// then moves to the HUD corner (user 2026-10-12: a berry beside her during the fight was distracting).</summary>
        /// <summary>A boss fight is going on (achievements: Untouchable).</summary>
        public static bool InFight { get { var f = Instance != null ? Instance.fight : null; return f != null && f.LeftSceneAt < 0f; } }
        public static float FightTime { get { var f = Instance != null ? Instance.fight : null; return f != null ? f.Time : 0f; } }
        public static bool GoldenInHud { get { return GoldenRun && FightTime >= Visuals.GoldenIntroTime; } }
        float hudAlpha, hudLostT = -1f;

        /// <summary>The first heal of a golden run: the berry laughs and flies off.</summary>
        public static event Action GoldenLost;

        class Card
        {
            public string Title, Sub;
            public float Time;
            public int Deaths, Dashes, Hits, Heals, Goldens;
            public bool Golden, HadGolden, Assist, Invincible, FirstTry;
            public bool NewBest, Viewing;   // Viewing: shown in the records menu, not after a fight
            public bool AreaMode;           // an area record: deaths and time only
            public string Note = "";
            public float PrevBest = -1f;
        }

        // ---- records: the best win per boss, per save (best:<slot>|<boss>) -----------------------------------
        class Record { public float Time; public int Deaths, Dashes, Hits, Heals; }
        readonly Dictionary<string, Record> best = new Dictionary<string, Record>();
        readonly Dictionary<string, string> titles = new Dictionary<string, string>();    // boss id -> its name
        readonly Dictionary<string, string> journal = new Dictionary<string, string>();   // boss id -> Hunter's Journal record

        Fight fight;
        readonly Dictionary<string, int> deaths = new Dictionary<string, int>();      // "slot|boss": deaths while fighting
        readonly HashSet<string> goldens = new HashSet<string>();                      // "slot|boss"
        bool loaded, wasDead;
        int lastHealth = -1, baselineSlot = -1;
        readonly Dictionary<string, bool> flags = new Dictionary<string, bool>();
        static List<FieldInfo> defeatFields;

        enum Phase { None, In, Shown, Out }
        Phase phase;
        Card card;
        readonly List<Card> queue = new List<Card>();
        float t, shownFor, controlFor;
        bool relinquished;

        void Awake() { Instance = this; }

        /// <summary>Celeste's in-game golden berry (Gameplay collectables/goldberry idle, 18x16, point filtered) as a GUI
        /// image, on its idle animation (user 2026-10-12: pixelated, not the Gui art). Null before the atlas is read.</summary>
        public static List<Sprite> GoldFrames;
        static readonly Dictionary<Sprite, GuiArt.Img> goldImgs = new Dictionary<Sprite, GuiArt.Img>();
        internal static GuiArt.Img PixelBerry()
        {
            if (GoldFrames == null || GoldFrames.Count == 0) return null;
            int f = (int)(Time.unscaledTime / 0.08f) % 32;
            Sprite s = GoldFrames[Math.Min(f < 24 ? f % 6 : f - 24, GoldFrames.Count - 1)];
            GuiArt.Img img;
            if (!goldImgs.TryGetValue(s, out img)) goldImgs[s] = img = new GuiArt.Img { W = s.texture.width, H = s.texture.height, Tex = s.texture };
            return img;
        }

        // ---- the fight ---------------------------------------------------------------------------------------

        /// <summary>hooks.boss_title: Silksong's DisplayBossTitle is the start of a boss fight.</summary>
        public static void OnBossTitle(string id)
        {
            if (Instance == null || string.IsNullOrEmpty(id)) return;
            string title, sub;
            Names(id, out title, out sub);
            Achievements.FightStarted();
            Instance.fight = new Fight
            {
                Id = id, Title = title, Sub = sub, Scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                Assist = AssistMode.AnyOn, Invincible = AssistMode.Invincible,
            };
            GameManager gm = GameManager.instance;
            Instance.Load();
            Instance.fight.HadGolden = Instance.HasGolden(id);   // one berry per boss across all saves (user 2026-10-12)
            // the first attempt at this boss on this save (no deaths to it yet): healing is allowed, only a death costs the berry
            int died; Instance.fight.FirstTry = gm == null || !Instance.deaths.TryGetValue(Key(gm.profileID, id), out died) || died == 0;
            Instance.lastHealth = PlayerData.instance != null ? PlayerData.instance.health : -1;
            Instance.titles[id] = title;
            if (!Instance.journal.ContainsKey(id)) { string rec = JournalFor(title); if (rec != null) Instance.journal[id] = rec; }
            Plugin.Log.LogInfo("Boss fight: " + id + " (" + title + (sub.Length > 0 ? ", " + sub : "") + ")");
        }

        /// <summary>The dashes of the fight (MadelineController.DashBegan).</summary>
        public static void OnDash() { if (Instance != null && Instance.fight != null) Instance.fight.Dashes++; }

        // Silksong's titles (the Titles sheet) come in parts: <id>_SUPER the small line above ("Moss", "Sister"),
        // <id>_MAIN the big word ("Mother", "Splinter") and <id>_SUB the line below. The card names the boss
        // SUPER + MAIN, with SUB under it.
        static void Names(string id, out string title, out string sub)
        {
            string main = Clean(Lang(id + "_MAIN") ?? Lang(id) ?? Pretty(id));
            string super = Clean(Lang(id + "_SUPER") ?? "");
            title = super.Length > 0 ? super + " " + main : main;
            sub = "";   // <id>_SUB is sometimes a credit line ("By ...") rather than part of the name
        }

        internal static string Lang(string key)
        {
            try
            {
                if (TeamCherry.Localization.Language.Has(key, "Titles")) { string s = TeamCherry.Localization.Language.Get(key, "Titles"); if (!string.IsNullOrEmpty(s)) return s; }
            }
            catch (Exception) { }
            return null;
        }

        // Only what the Renogare glyphs can draw; Silksong's own markup and line breaks become spaces.
        internal static string Clean(string s)
        {
            var b = new System.Text.StringBuilder();
            bool tag = false;
            foreach (char c in s)
            {
                if (c == '<') { tag = true; continue; }
                if (c == '>') { tag = false; continue; }
                if (tag) continue;
                b.Append(c < 32 ? ' ' : c == '’' || c == '‘' ? '\'' : c == '“' || c == '”' ? '"' : c == '–' || c == '—' ? '-' : c);
            }
            return b.ToString().Trim();
        }

        static string Pretty(string id)
        {
            var parts = id.ToLowerInvariant().Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }

        static string Key(int slot, string id) { return slot + "|" + id; }

        // The boss's Hunter's Journal entry (for its picture in the records menu): the record whose name matches the title.
        static string JournalFor(string title)
        {
            try
            {
                string want = Norm(title);
                EnemyJournalRecord near = null;
                foreach (EnemyJournalRecord r in EnemyJournalManager.GetAllEnemies())
                {
                    if (r == null) continue;
                    string n = Norm(r.DisplayName.ToString());
                    if (n.Length == 0) continue;
                    if (n == want) return r.name;
                    if (near == null && (want.Contains(n) || n.Contains(want))) near = r;
                }
                return near != null ? near.name : null;
            }
            catch (Exception e) { Plugin.Log.LogWarning("Boss journal: " + e.Message); return null; }
        }

        static string Norm(string s)
        {
            var b = new System.Text.StringBuilder();
            foreach (char c in (s ?? "").ToLowerInvariant()) if (char.IsLetter(c)) b.Append(c);
            return b.ToString();
        }

        // ---- the records menu (assist.key_records): every boss beaten, its best card, on this save or all saves ----
        bool viewing, viewAll, viewRelinquished, photoTaken;
        public static bool CardShowing { get { return Instance != null && Instance.phase != Phase.None; } }
        static bool fromMenu;
        /// <summary>The title screen's Achievements: the records, on their Achievements tab.</summary>
        public static void OpenAchievementsFromMenu()
        {
            if (Instance == null || Instance.viewing) return;
            fromMenu = true;
            Instance.viewing = true; Instance.viewTab = 3; Instance.viewIndex = 0; Instance.viewPad = false;
            Instance.cursorWas = Cursor.visible; Instance.lockWas = Cursor.lockState;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            Sfx.Play(Sounds.CardIn.Id);
        }
        public static bool RecordsOpen { get { return Instance != null && Instance.viewing; } }
        int viewTab;   // 0 bosses, 1 areas, 2 golden berries
        bool viewPad, cursorWas; CursorLockMode lockWas;
        readonly Rect[] vTabRects = new Rect[6], scopeRects = new Rect[2];
        Rect pickL, pickR;
        int viewIndex;
        float viewT;
        TimeManager.TimeControlInstance viewPause;

        List<KeyValuePair<string, Record>> ViewList(int slot)
        {
            var bestOf = new Dictionary<string, Record>();
            foreach (var kv in best)
            {
                int bar = kv.Key.IndexOf('|');
                string boss = kv.Key.Substring(bar + 1);
                if (!viewAll && kv.Key.Substring(0, bar) != slot.ToString(CultureInfo.InvariantCulture)) continue;
                Record r;
                if (!bestOf.TryGetValue(boss, out r) || (kv.Value.Time >= 0f && (r.Time < 0f || kv.Value.Time < r.Time))) bestOf[boss] = kv.Value;
            }
            var list = new List<KeyValuePair<string, Record>>(bestOf);
            list.Sort((a, b) => string.Compare(TitleOf(a.Key), TitleOf(b.Key), StringComparison.OrdinalIgnoreCase));
            return list;
        }

        string TitleOf(string boss) { string t; return titles.TryGetValue(boss, out t) ? t : Pretty(boss); }

        // ---- import: bosses a save beat before records existed (user 2026-10-12) -----------------------------
        // Silksong keeps a defeated* flag per boss; each loaded save adds its beaten bosses as records without stats
        // (time -1). A flag a real fight ended on is mapped to that fight's boss (flagboss:), so nothing doubles.
        readonly Dictionary<string, string> flagBoss = new Dictionary<string, string>();
        readonly HashSet<int> importedSlots = new HashSet<int>();

        void ImportBeaten(int slot, PlayerData pd)
        {
            if (defeatFields == null || !importedSlots.Add(slot)) return;
            // bosses already known by id (deaths, berries, fights): their journal entry, so an imported flag joins them
            var known = new Dictionary<string, string>();
            var ids = new HashSet<string>(titles.Keys);
            foreach (string k in deaths.Keys) ids.Add(k.Substring(k.IndexOf('|') + 1));
            foreach (string k in goldens) ids.Add(k.Substring(k.IndexOf('|') + 1));
            foreach (string id in ids)
            {
                if (id.StartsWith("flag:", StringComparison.Ordinal)) continue;
                string jr;
                if (!journal.TryGetValue(id, out jr))
                {
                    string title, sub; Names(id, out title, out sub);
                    if (!titles.ContainsKey(id)) titles[id] = title;
                    jr = JournalFor(title);
                    if (jr != null) journal[id] = jr;
                }
                if (jr != null && !known.ContainsKey(jr)) known[jr] = id;
            }
            int added = 0;
            foreach (FieldInfo f in defeatFields)
            {
                if (!f.Name.StartsWith("defeated", StringComparison.OrdinalIgnoreCase) || !(bool)f.GetValue(pd)) continue;
                string boss;
                if (!flagBoss.TryGetValue(f.Name, out boss))
                {
                    string title = FlagTitle(f.Name), rec = JournalFor(title);
                    if (rec == null) continue;   // not a journal creature: not a boss fight to list
                    if (!known.TryGetValue(rec, out boss)) { boss = "flag:" + f.Name; titles[boss] = title; journal[boss] = rec; }
                    flagBoss[f.Name] = boss;
                }
                string key = Key(slot, boss);
                if (best.ContainsKey(key)) continue;
                best[key] = new Record { Time = -1f, Deaths = -1, Dashes = -1, Hits = -1, Heals = -1 };
                added++;
            }
            if (added > 0) { Save(); Plugin.Log.LogInfo("Boss records: imported " + added + " bosses beaten before on save " + slot); }
        }

        // "defeatedMossMother" -> "Moss Mother"
        static string FlagTitle(string flag)
        {
            string s = flag.Substring("defeated".Length).TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');   // "Lace1" is Lace
            var b = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '_') { b.Append(' '); continue; }
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1])) b.Append(' ');
                b.Append(c);
            }
            return b.ToString().Trim();
        }

        void UpdateViewer(HeroController h, GameManager gm)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            InputHandler input = InputHandler.Instance;
            HeroActions a = input != null ? input.inputActions : null;
            float dt = Time.unscaledDeltaTime;
            if (!viewing)
            {
                viewT = Math.Max(0f, viewT - dt / 0.15f);
                bool playing = h != null && gm.GameState == GlobalEnums.GameState.PLAYING && !gm.isPaused && !h.cState.dead && !h.cState.transitioning;
                if ((AssistMode.KeyPressed(AssistOptions.KeyRecords) || MenuMouse.SharePressed()) && playing && phase == Phase.None && !AssistMode.IsOpen)
                {
                    viewing = true; viewIndex = 0; viewPad = false;
                    cursorWas = Cursor.visible; lockWas = Cursor.lockState;
                    Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
                    if (viewPause == null) viewPause = TimeManager.CreateTimeControl(0f, TimeManager.TimeControlInstance.Type.MinValue);
                    if (!h.controlReqlinquished) { h.RelinquishControl(); viewRelinquished = true; }
                    Sfx.Play(Sounds.CardIn.Id);
                }
                return;
            }
            viewT = Math.Min(1f, viewT + dt / 0.15f);
            bool close = AssistMode.KeyPressed(AssistOptions.KeyRecords) || MenuMouse.SharePressed() || (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) || (a != null && (a.Dash.WasPressed || a.MenuCancel.WasPressed));
            try { var devC = InControl.InputManager.ActiveDevice; if (devC != null && devC.Action2.WasPressed) close = true; } catch (Exception) { }   // Circle, also on the title screen
            if (close && Achievements.Confirming) { Achievements.CancelConfirm(); return; }   // Back closes the reset question first
            if (close)
            {
                viewing = false;
                Cursor.visible = cursorWas; Cursor.lockState = lockWas;
                if (viewPause != null) { viewPause.Release(); viewPause = null; }
                if (viewRelinquished) h.RegainControl();
                viewRelinquished = false;
                Plugin.Controller.ClearInput();
                Sfx.Play(Sounds.CardOut.Id);
                return;
            }
            bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (a != null && a.Left.WasPressed);
            bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (a != null && a.Right.WasPressed);
            bool toggle = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (a != null && (a.Up.WasPressed || a.Down.WasPressed));
            viewPad = MenuMouse.PadHints(viewPad);
            bool l1 = false, r1 = false;
            try { var dev = InControl.InputManager.ActiveDevice; if (dev != null) { l1 = dev.LeftBumper.WasPressed; r1 = dev.RightBumper.WasPressed; } } catch (Exception) { }
            bool nextTab = (kb != null && (kb.tabKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) || r1;
            bool prevTab = (kb != null && kb.qKey.wasPressedThisFrame) || l1;
            // the mouse (user 2026-10-13): tabs, This Save / All Saves, the < > arrows, the wheel
            for (int t = 0; t < TabNames.Length; t++)
                if (t != viewTab && MenuMouse.Clicked(vTabRects[t])) { viewTab = t; viewIndex = 0; Sfx.Play(Sounds.MenuRolloverDown.Id); return; }
            if (viewTab == 3)
            {
                // the achievements grid takes the arrows (and the mouse) for itself; achievements are for all saves
                bool up = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (a != null && a.Up.WasPressed);
                bool down = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (a != null && a.Down.WasPressed);
                bool tri = false;
                try { var dev = InControl.InputManager.ActiveDevice; if (dev != null) tri = dev.Action4.WasPressed; } catch (Exception) { }
                bool sq = false;
                try { var dev = InControl.InputManager.ActiveDevice; if (dev != null) sq = dev.Action3.WasPressed; } catch (Exception) { }
                bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (a != null && a.Jump.WasPressed);
                bool held = (kb != null && (kb.enterKey.isPressed || kb.spaceKey.isPressed)) || (a != null && a.Jump.IsPressed);
                if (Achievements.Confirming) { Achievements.ConfirmInput(left, right, confirm, held); nextTab = prevTab = false; }
                else if (sq || (kb != null && kb.rKey.wasPressedThisFrame)) Achievements.OpenConfirm();
                else if (tri || (kb != null && kb.tabKey.wasPressedThisFrame)) { Achievements.ToggleSort(); nextTab = false; }
                else if (!nextTab && !prevTab) Achievements.Navigate(left, right, up, down);
                left = right = toggle = false;
            }
            else
            {
                // user 2026-10-13: Left/Right switch This Save / All Saves (two choices, so it wraps either way);
                // Up/Down step through the bosses or areas
                bool upK = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (a != null && a.Up.WasPressed);
                bool downK = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (a != null && a.Down.WasPressed);
                toggle = left || right;
                left = upK; right = downK;
            }
            if (fromMenu) { toggle = false; viewAll = true; }   // the title screen shows every save's records
            if (MenuMouse.Clicked(scopeRects[0]) && viewAll) toggle = true;
            if (MenuMouse.Clicked(scopeRects[1]) && !viewAll) toggle = true;
            if (MenuMouse.Clicked(pickL)) left = true;
            if (MenuMouse.Clicked(pickR)) right = true;
            float wheel = MenuMouse.Wheel();
            if (wheel > 0.1f) left = true; else if (wheel < -0.1f) right = true;
            if (nextTab || prevTab) { viewTab = (viewTab + (nextTab ? 1 : TabNames.Length - 1)) % TabNames.Length; viewIndex = 0; Sfx.Play(Sounds.MenuRolloverDown.Id); }
            int n = viewTab == 1 ? AreaList(gm.profileID).Count : viewTab == 0 ? ViewList(gm.profileID).Count : viewTab == 4 ? PostcardView.Album().Count : viewTab == 5 ? Album.Photos.Count : 0;
            if (left && n > 0) { viewIndex = (viewIndex + n - 1) % n; Sfx.Play(Sounds.MenuRolloverUp.Id); }
            if (right && n > 0) { viewIndex = (viewIndex + 1) % n; Sfx.Play(Sounds.MenuRolloverDown.Id); }
            if (toggle) { viewAll = !viewAll; viewIndex = 0; Sfx.Play(Sounds.MenuToggleOn.Id); }
        }

        // ---- areas: time and deaths per Silksong map zone, per save (user 2026-10-12: area records and cards) ----
        readonly Dictionary<string, float> areaTime = new Dictionary<string, float>();    // "slot|ZONE"
        readonly Dictionary<string, int> areaDeaths = new Dictionary<string, int>();
        readonly Dictionary<string, string> areaNames = new Dictionary<string, string>(); // ZONE -> its name
        string curZone;
        float bannerT = -1f, areaSaveIn = 10f;
        string bannerZone;
        bool bannerNew, wasHazard;

        void TrackArea(HeroController h, GameManager gm, bool playing, float dt)
        {
            string zone = null;
            GlobalEnums.MapZone ez = default(GlobalEnums.MapZone);
            try { ez = gm.GetCurrentMapZoneEnum(); zone = ez.ToString(); } catch (Exception) { }
            bool dead = h.cState.dead, hazard = h.cState.hazardDeath;
            if (curZone != null && ((dead && !wasDead) || (hazard && !wasHazard)))
            {
                string dk = Key(gm.profileID, curZone);
                int n; areaDeaths.TryGetValue(dk, out n); areaDeaths[dk] = n + 1;
            }
            wasHazard = hazard;
            if (zone == null || zone == "NONE") return;
            if (!areaNames.ContainsKey(zone))
            {
                try { string nm = Clean(GameManager.GetFormattedMapZoneStringV2(ez) ?? ""); areaNames[zone] = nm.Length > 0 ? nm : Pretty(zone); } catch (Exception) { areaNames[zone] = Pretty(zone); }
            }
            string ak = Key(gm.profileID, zone);
            if (zone != curZone)
            {
                bool isNew = !areaTime.ContainsKey(ak);
                if (isNew) areaTime[ak] = 0f;
                // Celeste's chapter card: the area's name, and its deaths and time so far (or "New Area")
                if (isNew) { bannerZone = zone; bannerNew = true; bannerT = 0f; }   // once per area per save (user 2026-10-12)
                curZone = zone;
                Save();
            }
            if (playing && !h.controlReqlinquished && !dead && Time.timeScale > 0f) areaTime[ak] += dt;
            if ((areaSaveIn -= dt) <= 0f) { areaSaveIn = 10f; Save(); }
            // the card waits while something else is up (the Tutorial postcard, a boss card, a cutscene)
            if (bannerT >= 0f && phase == Phase.None && !viewing && !AssistMode.IsOpen && !h.controlReqlinquished && PostcardView.TutorialDone && (bannerT += dt) > Visuals.AreaCardTime) bannerT = -1f;
        }

        /// <summary>Achievements: the area she is in now, and how many areas she has been to on any save.</summary>
        public static string CurrentAreaName { get { return Instance != null && Instance.curZone != null ? Instance.AreaName(Instance.curZone) : null; } }
        public static int AreasVisited { get { if (Instance == null) return 0; var z = new HashSet<string>(); foreach (string k in Instance.areaTime.Keys) z.Add(k.Substring(k.IndexOf('|') + 1)); return z.Count; } }
        string AreaName(string zone) { string n; return areaNames.TryGetValue(zone, out n) ? n : Pretty(zone); }

        List<KeyValuePair<string, KeyValuePair<float, int>>> AreaList(int slot)
        {
            var sum = new Dictionary<string, KeyValuePair<float, int>>();
            foreach (var kv in areaTime)
            {
                int bar = kv.Key.IndexOf('|');
                if (!viewAll && kv.Key.Substring(0, bar) != slot.ToString(CultureInfo.InvariantCulture)) continue;
                string zone = kv.Key.Substring(bar + 1);
                int d; areaDeaths.TryGetValue(kv.Key, out d);
                KeyValuePair<float, int> was;
                sum.TryGetValue(zone, out was);
                sum[zone] = new KeyValuePair<float, int>(was.Key + kv.Value, was.Value + d);
            }
            var list = new List<KeyValuePair<string, KeyValuePair<float, int>>>(sum);
            list.Sort((a, b) => string.Compare(AreaName(a.Key), AreaName(b.Key), StringComparison.OrdinalIgnoreCase));
            return list;
        }

        void DrawAreaBanner()
        {
            HeroController hc = HeroController.SilentInstance;
            if (bannerT < 0f || bannerZone == null || phase != Phase.None || viewing || AssistMode.IsOpen || hc == null || hc.controlReqlinquished || !PostcardView.TutorialDone) return;
            GameManager gm = GameManager.SilentInstance;
            if (gm == null) return;
            float life = Visuals.AreaCardTime, k = bannerT < 0.4f ? bannerT / 0.4f : bannerT > life - 0.5f ? (life - bannerT) / 0.5f : 1f;
            k = Mathf.Clamp01(k);
            float ease = 1f - (1f - k) * (1f - k) * (1f - k);
            float s = Screen.height / 1080f, sw = Screen.width / s;
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            GuiArt.Img ribbon = Art.Image("areaselect/title"), accent = Art.Image("areaselect/accent");
            string name = AreaName(bannerZone);
            float ts = 1.1f;
            while (ts > 0.5f && Art.Measure(name) * ts > 760f) ts -= 0.05f;
            float rw = Math.Max(640f, Art.Measure(name) * ts + 200f), rh = rw * 173f / 916f, rx = sw / 2f - rw / 2f, ry = 40f - (1f - ease) * 220f;
            Color rc = Hex(Visuals.BossCardTitleColor); rc.a = ease;
            Color ac = Hex(Visuals.BossCardTitleAccent); ac.a = ease;
            if (ribbon != null) GuiArt.Draw(ribbon, new Rect(rx, ry, rw, rh), rc);
            if (accent != null) GuiArt.Draw(accent, new Rect(rx, ry, rw, rh), ac);
            var white = new Color(1f, 1f, 1f, ease);
            Art.DrawLine(name, sw / 2f - Art.Measure(name) * ts / 2f + 3f, ry + rh / 2f - Art.LineHeight * ts / 2f + 3f, ts, new Color(0f, 0f, 0f, 0.35f * ease));
            Art.DrawLine(name, sw / 2f - Art.Measure(name) * ts / 2f, ry + rh / 2f - Art.LineHeight * ts / 2f, ts, white);
            string ak = Key(gm.profileID, bannerZone);
            float tm; areaTime.TryGetValue(ak, out tm);
            int de; areaDeaths.TryGetValue(ak, out de);
            string line = bannerNew ? "New Area" : "Deaths " + de.ToString(CultureInfo.InvariantCulture) + "     Time " + FormatTime(tm);
            float ls = 0.6f, ly = ry + rh + 6f;
            GuiArt.Img skull = Art.Image("collectables/skullGold") ?? Art.Skull;
            float lw = Art.Measure(line) * ls, lx = sw / 2f - lw / 2f;
            if (!bannerNew && skull != null) { GuiArt.Draw(skull, new Rect(lx - 50f, ly, 40f, 40f), white); }
            Art.DrawOutlined(line, lx, ly, ls, bannerNew ? new Color(1f, 0.9f, 0.55f, ease) : white, new Color(0f, 0f, 0f, ease), 2f);
            GUI.matrix = old;
            GUI.color = Color.white;
        }

        static readonly string[] TabNames = { "Bosses", "Areas", "Golden Berries", "Achievements", "Postcards", "Photos" };

        void DrawViewer()
        {
            GameManager gm = GameManager.SilentInstance;
            if (gm == null) return;
            float s = Screen.height / 1080f, sw = Screen.width / s, alpha = viewT;
            Matrix4x4 old = GUI.matrix;
            GUI.depth = -1;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            // the Settings menu's look (user 2026-10-13): the same dim, a title, the tab bar, and a This Save / All Saves switch
            GUI.color = new Color(0f, 0f, 0f, Math.Max(0.8f, Visuals.AssistDim) * alpha);
            GUI.DrawTexture(new Rect(0f, 0f, sw, 1080f), Texture2D.whiteTexture);
            var white = new Color(1f, 1f, 1f, alpha); var black = new Color(0f, 0f, 0f, alpha);
            Color off = new Color(0.6f, 0.6f, 0.6f, alpha);
            Color hi = Hex(Visuals.MenuHighlightA); hi.a = alpha;
            float headTop = MenuStyle.Title("RECORDS", null, sw / 2f, 26f, alpha);
            headTop = MenuStyle.Tabs(TabNames, viewTab, viewPad, sw / 2f, headTop, alpha, vTabRects);
            if (!fromMenu)   // no save loaded on the title screen: no This Save / All Saves
            {
                // a two-part switch: the chosen half lit with the highlight colour
                string a1 = "This Save", a2 = "All Saves";
                float ts = 0.55f, pad = 26f, w1 = Art.Measure(a1) * ts + pad * 2f, w2 = Art.Measure(a2) * ts + pad * 2f, hgt = Art.LineHeight * ts + 14f;
                float sx = sw / 2f - (w1 + w2) / 2f, sy = headTop - 14f;
                scopeRects[0] = new Rect(sx, sy, w1, hgt); scopeRects[1] = new Rect(sx + w1, sy, w2, hgt);
                MenuStyle.Panel(new Rect(sx, sy, w1 + w2, hgt), alpha);
                Rect lit = viewAll ? scopeRects[1] : scopeRects[0];
                GUI.color = new Color(hi.r, hi.g, hi.b, 0.22f * alpha); GUI.DrawTexture(lit, Texture2D.whiteTexture); GUI.color = Color.white;
                Art.DrawOutlined(a1, sx + pad, sy + 7f, ts, viewAll ? off : hi, black, 2f);
                Art.DrawOutlined(a2, sx + w1 + pad, sy + 7f, ts, viewAll ? hi : off, black, 2f);
                headTop = sy + hgt + 24f;
            }
            float contentTop = headTop;

            float cardW = 855f * Visuals.BossCardScale, leftW = sw - cardW, lx = leftW / 2f;
            float countBottom = 0f;
            bool showRules = viewTab == 0 || viewTab == 2;
            if (viewTab == 3) { Achievements.DrawGrid(sw, contentTop - 10f, alpha); countBottom = 0f; }
            if (viewTab == 0)
            {
                var list = ViewList(gm.profileID);
                if (list.Count == 0)
                {
                    string none = viewAll ? "No bosses beaten yet." : "No bosses beaten on this save yet.";
                    Art.DrawOutlined(none, sw / 2f - Art.Measure(none) * 0.8f / 2f, 480f, 0.8f, white, black, 2f);
                }
                else
                {
                    viewIndex = Mathf.Clamp(viewIndex, 0, list.Count - 1);
                    string boss = list[viewIndex].Key;
                    Record r = list[viewIndex].Value;
                    Sprite pic = null;
                    string recName;
                    try { if (journal.TryGetValue(boss, out recName)) { var rec = EnemyJournalManager.GetRecord(recName); if (rec != null) pic = rec.EnemySprite ?? rec.IconSprite; } } catch (Exception) { }
                    float by = contentTop, box = Math.Min(Math.Min(leftW - 160f, 470f), 1080f - by - 420f);
                    if (pic != null && pic.texture != null)
                    {
                        Vector2 bs = pic.bounds.size;
                        float k = Math.Min(box / bs.x, box / bs.y);
                        DrawSpriteMesh(pic, new Rect(lx - bs.x * k / 2f, by + (box - bs.y * k) / 2f, bs.x * k, bs.y * k), s, alpha);
                    }
                    countBottom = DrawPicker(TitleOf(boss), viewIndex, list.Count, lx, leftW, by + box + 30f, alpha);
                    var c = new Card
                    {
                        Title = TitleOf(boss), Sub = "", Time = r.Time, Deaths = r.Deaths, Dashes = r.Dashes, Hits = r.Hits, Heals = r.Heals,
                        Golden = viewAll ? HasGolden(boss) : goldens.Contains(Key(gm.profileID, boss)), Goldens = GoldenTotal(), Viewing = true,
                    };
                    DrawCardBody(c, alpha, 1f);
                }
            }
            else if (viewTab == 1)
            {
                var list = AreaList(gm.profileID);
                if (list.Count == 0)
                {
                    string none = "No areas visited yet.";
                    Art.DrawOutlined(none, sw / 2f - Art.Measure(none) * 0.8f / 2f, 480f, 0.8f, white, black, 2f);
                }
                else
                {
                    viewIndex = Mathf.Clamp(viewIndex, 0, list.Count - 1);
                    string zone = list[viewIndex].Key;
                    // the area's name large, like a chapter title, with its totals
                    float by = Math.Max(330f, contentTop + 80f);
                    GuiArt.Img ribbon = Art.Image("areaselect/title");
                    float rh = (leftW - 120f) * 173f / 916f;
                    if (ribbon != null) { Color rc = Hex(Visuals.BossCardTitleColor); rc.a = alpha; GuiArt.Draw(ribbon, new Rect(lx - (leftW - 120f) / 2f, by, leftW - 120f, rh), rc); }
                    countBottom = DrawPicker(AreaName(zone), viewIndex, list.Count, lx, leftW, by + rh / 2f - Art.LineHeight * 0.5f, alpha);   // the name on the ribbon
                    var c = new Card
                    {
                        Title = AreaName(zone), Sub = "", Time = list[viewIndex].Value.Key, Deaths = list[viewIndex].Value.Value, Viewing = true, AreaMode = true,
                        Note = viewAll ? "All saves together" : "This save",
                    };
                    DrawCardBody(c, alpha, 1f);
                }
            }
            else if (viewTab == 2)
            {
                // every boss beaten, with its berry or an empty one: what is still to get
                var list = ViewList(gm.profileID);
                int got = 0;
                foreach (var kv in list) if (viewAll ? HasGolden(kv.Key) : goldens.Contains(Key(gm.profileID, kv.Key))) got++;
                GuiArt.Img berry = PixelBerry() ?? Art.Image("collectables/goldberry");
                string total = got + " / " + list.Count;
                float hy = contentTop;
                if (berry != null) GuiArt.Draw(berry, new Rect(sw / 2f - Art.Measure(total) * 1.2f / 2f - 90f, hy - 10f, 76f, 76f), white);
                Art.DrawOutlined(total, sw / 2f - Art.Measure(total) * 1.2f / 2f, hy, 1.2f, white, black, 3f);
                string sub = viewAll ? "bosses beaten on any save with their golden berry" : "bosses beaten on this save with their golden berry";
                Art.DrawOutlined(sub, sw / 2f - Art.Measure(sub) * 0.5f / 2f, hy + Art.LineHeight * 1.2f + 4f, 0.5f, off, black, 2f);
                if (list.Count == 0)
                {
                    string none = "Beat a boss to start collecting.";
                    Art.DrawOutlined(none, sw / 2f - Art.Measure(none) * 0.7f / 2f, 480f, 0.7f, white, black, 2f);
                }
                int cols = list.Count > 8 ? 3 : 2;
                float colW = 520f, rowH = 64f, gx = sw / 2f - cols * colW / 2f, gy = hy + Art.LineHeight * 1.2f + 60f;
                int rows = (list.Count + cols - 1) / cols;
                for (int i = 0; i < list.Count; i++)
                {
                    int col = i / Math.Max(1, rows), row = i % Math.Max(1, rows);
                    float x = gx + col * colW, y = gy + row * rowH;
                    bool has = viewAll ? HasGolden(list[i].Key) : goldens.Contains(Key(gm.profileID, list[i].Key));
                    if (berry != null) GuiArt.Draw(berry, new Rect(x, y, 52f, 52f), new Color(has ? 1f : 0.2f, has ? 1f : 0.2f, has ? 1f : 0.25f, (has ? 1f : 0.7f) * alpha));
                    string bn = TitleOf(list[i].Key);
                    float bs2 = 0.6f;
                    while (bs2 > 0.35f && Art.Measure(bn) * bs2 > colW - 90f) bs2 -= 0.03f;
                    Art.DrawOutlined(bn, x + 64f, y + 26f - Art.LineHeight * bs2 / 2f, bs2, has ? new Color(1f, 0.9f, 0.55f, alpha) : off, black, 2f);
                }
                countBottom = Math.Min(1080f - 200f, gy + rows * rowH + 10f);
                lx = sw / 2f; leftW = sw;
            }
            if (viewTab == 4)
            {
                // the postcard album (user 2026-10-16): every card seen, readable again
                var cards = PostcardView.Album();
                if (cards.Count == 0) { string none = "No postcards yet."; Art.DrawOutlined(none, sw / 2f - Art.Measure(none) * 0.8f / 2f, 480f, 0.8f, white, black, 2f); }
                else
                {
                    viewIndex = Mathf.Clamp(viewIndex, 0, cards.Count - 1);
                    float room = 1080f - contentTop - 230f, k = Math.Min(1f, room / Math.Max(1f, Art.Postcard.H));
                    PostcardView.DrawAlbumCard(cards[viewIndex], new Vector2(sw / 2f, contentTop + room / 2f + 10f), k, alpha);
                    countBottom = DrawPicker(cards[viewIndex].Title, viewIndex, cards.Count, sw / 2f, Math.Min(sw, 1100f), contentTop + room + 30f, alpha);
                }
            }
            else if (viewTab == 5)
            {
                // the photo album (user 2026-10-16): Theo's camera pictures from Pictures/Silkeste
                Album.Refresh();
                if (Album.Photos.Count == 0) { string none = "No photos yet. Press " + AssistMode.KeyName(AssistOptions.KeyCamera) + " to take one."; Art.DrawOutlined(none, sw / 2f - Art.Measure(none) * 0.7f / 2f, 480f, 0.7f, white, black, 2f); }
                else
                {
                    viewIndex = Mathf.Clamp(viewIndex, 0, Album.Photos.Count - 1);
                    Texture2D pic = Album.Get(viewIndex);
                    float room = 1080f - contentTop - 230f, maxW = sw - 260f;
                    if (pic != null)
                    {
                        float k = Math.Min(maxW / pic.width, room / pic.height), pw = pic.width * k, ph = pic.height * k, px = sw / 2f - pw / 2f, py = contentTop + (room - ph) / 2f + 10f;
                        // a white polaroid edge, a little wider at the bottom
                        GUI.color = new Color(0f, 0f, 0f, 0.4f * alpha); GUI.DrawTexture(new Rect(px - 8f, py - 4f, pw + 28f, ph + 48f), Texture2D.whiteTexture);
                        GUI.color = new Color(0.97f, 0.96f, 0.93f, alpha); GUI.DrawTexture(new Rect(px - 14f, py - 14f, pw + 28f, ph + 54f), Texture2D.whiteTexture);
                        GUI.color = new Color(1f, 1f, 1f, alpha); GUI.DrawTexture(new Rect(px, py, pw, ph), pic);
                    }
                    countBottom = DrawPicker(Album.Name(viewIndex), viewIndex, Album.Photos.Count, sw / 2f, Math.Min(sw, 1100f), contentTop + room + 30f, alpha);
                }
            }
            if (showRules)
            {
                // how the golden berry is earned (user 2026-10-12: explain it here)
                // revamped (user 2026-10-13): a panel with a heading and the two ways to earn it, each with its keyword
                GuiArt.Img rb = PixelBerry() ?? Art.Image("collectables/goldberry");
                Color gold2 = new Color(1f, 0.85f, 0.35f, alpha), soft = new Color(0.85f, 0.85f, 0.85f, alpha);
                string head = "HOW TO EARN A GOLDEN BERRY";
                string[,] ways = { { "First Try", "Beat the boss without dying to it. Healing is fine." }, { "No Heals", "Died to it before? Win without healing even once." } };
                string foot = "One berry per boss, across all saves. Assist Mode earns none.";
                float hs2 = 0.55f, ks2 = 0.5f, ds2 = 0.45f, fs2 = 0.4f, kw = 0f;
                for (int i = 0; i < 2; i++) kw = Math.Max(kw, Art.Measure(ways[i, 0]) * ks2);
                float inner = kw + 30f;
                for (int i = 0; i < 2; i++) inner = Math.Max(inner, kw + 30f + Art.Measure(ways[i, 1]) * ds2);
                inner = Math.Max(inner, Math.Max(Art.Measure(head) * hs2 + 70f, Art.Measure(foot) * fs2));
                float maxW = leftW - 120f, shrink2 = inner > maxW - 60f ? (maxW - 60f) / inner : 1f;
                hs2 *= shrink2; ks2 *= shrink2; ds2 *= shrink2; fs2 *= shrink2; kw *= shrink2; inner *= shrink2;
                float pw2 = inner + 60f, rowH2 = Art.LineHeight * Math.Max(ks2, ds2) + 10f;
                float ph2 = 22f + Art.LineHeight * hs2 + 16f + rowH2 * 2f + 10f + Art.LineHeight * fs2 + 22f;
                // between the picker and the control hints: shrink the whole panel if it doesn't fit (user 2026-10-13)
                float roomTop = countBottom + 16f, roomBottom = 1080f - 74f;
                if (ph2 > roomBottom - roomTop && roomBottom - roomTop > 60f)
                {
                    float f2 = (roomBottom - roomTop) / ph2;
                    hs2 *= f2; ks2 *= f2; ds2 *= f2; fs2 *= f2; kw *= f2; inner *= f2; shrink2 *= f2;
                    pw2 = inner + 60f; rowH2 = Art.LineHeight * Math.Max(ks2, ds2) + 10f * f2;
                    ph2 = (22f + 16f + 10f + 22f) * f2 + Art.LineHeight * (hs2 + fs2) + rowH2 * 2f;
                }
                float py2 = Math.Max(roomTop, roomBottom - ph2);
                float px2 = lx - pw2 / 2f;
                MenuStyle.Panel(new Rect(px2, py2, pw2, ph2), alpha);
                GUI.color = new Color(1f, 0.85f, 0.35f, 0.6f * alpha); GUI.DrawTexture(new Rect(px2, py2, 4f, ph2), Texture2D.whiteTexture); GUI.color = Color.white;   // a gold edge
                float cy2 = py2 + 22f, cx2 = px2 + 30f;
                float bsz2 = Art.LineHeight * hs2 * 1.2f;
                if (rb != null) GuiArt.Draw(rb, new Rect(cx2, cy2 - bsz2 * 0.1f, bsz2, bsz2), white);
                Art.DrawOutlined(head, cx2 + bsz2 + 14f, cy2, hs2, gold2, black, 2f);
                cy2 += Art.LineHeight * hs2 + 16f;
                for (int i = 0; i < 2; i++)
                {
                    Art.DrawOutlined(ways[i, 0], cx2, cy2, ks2, gold2, black, 2f);
                    Art.DrawOutlined(ways[i, 1], cx2 + kw + 30f * shrink2, cy2 + (Art.LineHeight * (ks2 - ds2)) / 2f, ds2, soft, black, 2f);
                    cy2 += rowH2;
                }
                cy2 += 10f;
                Art.DrawOutlined(foot, cx2, cy2, fs2, new Color(0.65f, 0.65f, 0.65f, alpha), black, 2f);
            }
            string what = viewTab == 0 ? "boss" : "area";
            string hint = viewPad
                ? (viewTab == 2 ? "" : "Up/Down " + what + "   ") + "Left/Right this save or all saves   L1/R1 menus   Circle to close"
                : (viewTab == 2 ? "" : "Up/Down or wheel " + what + "   ") + "Left/Right this save or all saves   Q/E menus   " + AssistMode.KeyName(AssistOptions.KeyRecords) + " or Esc to close";
            if (viewTab >= 4) hint = viewPad ? "Up/Down " + (viewTab == 4 ? "postcard" : "photo") + "   L1/R1 menus   Circle to close" : "Up/Down or wheel " + (viewTab == 4 ? "postcard" : "photo") + "   Q/E menus   " + AssistMode.KeyName(AssistOptions.KeyRecords) + " or Esc to close";
            if (viewTab == 3) hint = viewPad ? "D-pad choose   Square reset   Triangle sort   L1/R1 menus   Circle to close" : "Arrows or mouse choose   R or right-click reset   Tab sort   Q/E menus   " + AssistMode.KeyName(AssistOptions.KeyRecords) + " or Esc to close";
            if (viewTab == 2 || viewTab == 3) pickL = pickR = new Rect();
            MenuStyle.Hint(hint, sw / 2f, 1080f - 60f, alpha);
            GUI.matrix = old;
            GUI.color = Color.white;
        }

        // the name with < > either side and "2 / 7" under it; returns the bottom of the count
        float DrawPicker(string name, int index, int count, float lx, float leftW, float ny, float alpha)
        {
            var white = new Color(1f, 1f, 1f, alpha); var black = new Color(0f, 0f, 0f, alpha);
            float ns = 1f;
            while (ns > 0.5f && Art.Measure(name) * ns > leftW - 220f) ns -= 0.05f;
            string cnt = (index + 1) + " / " + count;
            float ax = lx - (leftW - 120f) / 2f, bx = lx + (leftW - 120f) / 2f - Art.Measure(">") * 1.2f, aw = Art.Measure(">") * 1.2f, ah = Art.LineHeight * 1.2f;
            pickL = new Rect(ax - 24f, ny - 16f, aw + 48f, ah + 32f); pickR = new Rect(bx - 24f, ny - 16f, aw + 48f, ah + 32f);
            Color hi = Hex(Visuals.MenuHighlightA); hi.a = alpha;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            Vector2 mp = MenuMouse.Gui();
            Art.DrawOutlined("<", ax, ny, 1.2f, pickL.Contains(mp) ? hi : new Color(1f, 1f, 1f, alpha * pulse), black, 3f);
            Art.DrawOutlined(">", bx, ny, 1.2f, pickR.Contains(mp) ? hi : new Color(1f, 1f, 1f, alpha * pulse), black, 3f);
            Art.DrawOutlined(name, lx - Art.Measure(name) * ns / 2f, ny, ns, white, black, 3f);
            Art.DrawOutlined(cnt, lx - Art.Measure(cnt) * 0.6f / 2f, ny + Art.LineHeight * ns + 8f, 0.6f, new Color(0.8f, 0.8f, 0.8f, alpha), black, 2f);
            return ny + Art.LineHeight * ns + 8f + Art.LineHeight * 0.6f;
        }
        bool loggedError;
        void Update()
        {
            try { Tick(); }
            catch (Exception e) { if (!loggedError) { loggedError = true; Plugin.Log.LogError("BossCard: " + e); } }
        }

        void Tick()
        {
            if (!Plugin.Ready) return;
            HeroController h = HeroController.SilentInstance;
            GameManager gm = GameManager.SilentInstance;
            PlayerData pd = PlayerData.instance;
            // opened from the title screen's Achievements (user 2026-10-14): the records without a save, on the Achievements tab
            if (gm != null && gm.IsMenuScene() && (fromMenu || viewing || viewT > 0f))   // open (or still fading out) on the title screen
            {
                fromMenu = true;
                Load();
                UpdateViewer(null, gm);
                if (!viewing && viewT <= 0f) fromMenu = false;
                return;
            }
            fromMenu = false;
            if (h == null || gm == null || pd == null || gm.IsMenuScene()) { Hide(h); fight = null; baselineSlot = -1; curZone = null; bannerT = -1f; return; }
            Load();
            float dt = Time.unscaledDeltaTime;
            bool playing = gm.GameState == GlobalEnums.GameState.PLAYING && !gm.isPaused;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (fight != null)
            {
                if (scene == fight.Scene) { if (playing && !h.cState.dead) fight.Time += dt; fight.LeftSceneAt = -1f; }
                else if (fight.LeftSceneAt < 0f) fight.LeftSceneAt = Time.unscaledTime;
                else if (Time.unscaledTime - fight.LeftSceneAt > Visuals.BossFightForgetTime) fight = null;   // walked away from it
            }
            if (fight != null)
            {
                if (AssistMode.AnyOn) fight.Assist = true;
                if (AssistMode.Invincible) fight.Invincible = true;
                if (lastHealth >= 0 && pd.health < lastHealth && !h.cState.dead) fight.Hits++;
                // a bind (health going back up) costs the golden berry (user 2026-10-12: hitless was too harsh, healless instead)
                if (lastHealth >= 0 && pd.health > lastHealth && !h.cState.dead)
                {
                    bool wasRun = GoldenRun;
                    fight.Heals++;
                    bool lost = wasRun && !GoldenRun;   // on a first try healing doesn't cost it
                    if (lost && hudAlpha > 0.5f) { hudLostT = 0f; Sfx.Play(Sounds.GoldenLaugh.Id); }   // the HUD berry flies off
                    if (lost && GoldenLost != null) GoldenLost();
                }
            }
            lastHealth = pd.health;

            // A real death ends the attempt; it counts towards that boss.
            bool dead = h.cState.dead;
            if (dead && !wasDead && fight != null)
            {
                string k = Key(gm.profileID, fight.Id);
                int n; deaths.TryGetValue(k, out n); deaths[k] = n + 1;
                Save();
                fight = null;
            }
            wasDead = dead;

            hudAlpha = Mathf.MoveTowards(hudAlpha, GoldenInHud ? 1f : 0f, dt / 0.3f);
            if (hudLostT >= 0f && (hudLostT += dt) > 1.3f) hudLostT = -1f;
            WatchDefeats(gm, pd);
            ImportBeaten(gm.profileID, pd);
            TrackArea(h, gm, playing && !h.cState.transitioning, dt);
            UpdateCard(h, gm, dt);
            UpdateViewer(h, gm);
        }

        // hooks.boss_defeated: every PlayerData bool named defeated* (and killed*); one turning on ends the fight.
        void WatchDefeats(GameManager gm, PlayerData pd)
        {
            if (defeatFields == null)
            {
                defeatFields = new List<FieldInfo>();
                foreach (FieldInfo f in typeof(PlayerData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                    if (f.FieldType == typeof(bool) && (f.Name.StartsWith("defeated", StringComparison.OrdinalIgnoreCase) || f.Name.StartsWith("killed", StringComparison.OrdinalIgnoreCase)))
                        defeatFields.Add(f);
            }
            bool fresh = gm.profileID != baselineSlot;
            baselineSlot = gm.profileID;
            foreach (FieldInfo f in defeatFields)
            {
                bool now = (bool)f.GetValue(pd), before;
                if (!fresh && flags.TryGetValue(f.Name, out before) && !before && now) Defeated(gm, f.Name);
                flags[f.Name] = now;
            }
        }

        void Defeated(GameManager gm, string flag)
        {
            Plugin.Log.LogInfo("Boss defeated: " + flag + (fight != null ? " (fight " + fight.Id + ", " + fight.Hits + " hits)" : " (no fight seen)"));
            if (fight == null) return;   // a boss without a title card: no fight to report
            flagBoss[flag] = fight.Id;
            string k = Key(gm.profileID, fight.Id);
            int d; deaths.TryGetValue(k, out d);
            bool hadGolden = HasGolden(fight.Id);
            bool golden = (fight.FirstTry || fight.Heals == 0) && !fight.Assist;   // user 2026-10-12: first try deathless, or healless   // any Assist Mode option on during the fight: no berry (user 2026-10-12)
            if (golden && !hadGolden) goldens.Add(k);
            // the best win on this save: fastest time (user 2026-10-12: records per save and across saves)
            Record prev; best.TryGetValue(k, out prev);
            if (prev != null && prev.Time < 0f) prev = null;   // an imported record (no stats) gives way to a real win
            bool newBest = prev != null && fight.Time < prev.Time;
            if (prev == null || newBest) best[k] = new Record { Time = fight.Time, Deaths = d, Dashes = fight.Dashes, Hits = fight.Hits, Heals = fight.Heals };
            Save();
            int count = 0;
            count = GoldenTotal();
            {
                // achievements: bosses beaten on this save, and how many of them carry a golden berry
                int beaten = 0, withBerry = 0;
                foreach (var kv in ViewList(gm.profileID)) { beaten++; if (goldens.Contains(Key(gm.profileID, kv.Key))) withBerry++; }
                int counted = 0; foreach (string g in goldens) if (TitleOf(g.Substring(g.IndexOf('|') + 1)).IndexOf("Moss Mother", StringComparison.OrdinalIgnoreCase) < 0) counted++;
                Achievements.BossBeaten(fight.Title, fight.Time, fight.Dashes, fight.Hits, golden, fight.Assist, counted, beaten, withBerry);
            }
            queue.Add(new Card
            {
                Title = fight.Title, Sub = fight.Sub, Time = fight.Time, Deaths = d, Dashes = fight.Dashes, Hits = fight.Hits, Heals = fight.Heals,
                Golden = golden, HadGolden = hadGolden, Goldens = count, Assist = fight.Assist, Invincible = fight.Invincible, FirstTry = fight.FirstTry,
                NewBest = newBest, PrevBest = prev != null ? prev.Time : -1f,
            });
            fight = null;
        }

        // golden:<slot>|<boss> records which save earned it; any save having it means the boss's berry is taken.
        bool HasGolden(string id) { Load(); foreach (string g in goldens) if (g.EndsWith("|" + id, StringComparison.Ordinal)) return true; return false; }
        int GoldenTotal() { var bosses = new HashSet<string>(); foreach (string g in goldens) bosses.Add(g.Substring(g.IndexOf('|') + 1)); return bosses.Count; }

        /// <summary>Golden berries collected, across all saves (one per boss; shown with the speedrun timer).</summary>
        public static int GoldenCount(int slot)
        {
            if (Instance == null) return 0;
            Instance.Load();
            return Instance.GoldenTotal();
        }

        /// <summary>Golden berries earned on one save only (the save file screen, user 2026-10-14).</summary>
        public static int GoldensOnSlot(int slot)
        {
            if (Instance == null) return 0;
            Instance.Load();
            string pre = slot.ToString(CultureInfo.InvariantCulture) + "|";
            int n = 0; foreach (string g in Instance.goldens) if (g.StartsWith(pre, StringComparison.Ordinal)) n++;
            return n;
        }

        string SaveFile { get { return Path.Combine(DataDir, "bosses.cfg"); } }

        void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (!File.Exists(SaveFile)) return;
                foreach (string line in File.ReadAllLines(SaveFile))
                {
                    string[] p = line.Split('=');
                    if (p.Length != 2) continue;
                    if (p[0].StartsWith("golden:", StringComparison.Ordinal)) goldens.Add(p[0].Substring(7));
                    else if (p[0].StartsWith("deaths:", StringComparison.Ordinal)) { int n; if (int.TryParse(p[1], out n)) deaths[p[0].Substring(7)] = n; }
                    else if (p[0].StartsWith("title:", StringComparison.Ordinal)) { string id = p[0].Substring(6); titles[id] = id.StartsWith("flag:", StringComparison.Ordinal) ? FlagTitle(id.Substring(5)) : p[1]; }
                    else if (p[0].StartsWith("flagboss:", StringComparison.Ordinal)) flagBoss[p[0].Substring(9)] = p[1];
                    else if (p[0].StartsWith("atime:", StringComparison.Ordinal)) { float v; if (float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) areaTime[p[0].Substring(6)] = v; }
                    else if (p[0].StartsWith("adeaths:", StringComparison.Ordinal)) { int v; if (int.TryParse(p[1], out v)) areaDeaths[p[0].Substring(8)] = v; }
                    else if (p[0].StartsWith("aname:", StringComparison.Ordinal)) areaNames[p[0].Substring(6)] = p[1];
                    else if (p[0].StartsWith("journal:", StringComparison.Ordinal)) journal[p[0].Substring(8)] = p[1];
                    else if (p[0].StartsWith("best:", StringComparison.Ordinal))
                    {
                        string[] v = p[1].Split(';');
                        float tm; int de, da, hi, he;
                        if (v.Length == 5 && float.TryParse(v[0], NumberStyles.Float, CultureInfo.InvariantCulture, out tm) && int.TryParse(v[1], out de) && int.TryParse(v[2], out da) && int.TryParse(v[3], out hi) && int.TryParse(v[4], out he))
                            best[p[0].Substring(5)] = new Record { Time = tm, Deaths = de, Dashes = da, Hits = hi, Heals = he };
                    }
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Boss records: " + e.Message); }
            // reset_records.flag next to the plugin: boss times, deaths and golden berries start over once (areas stay)
            try
            {
                if (ResetFlag != null && File.Exists(ResetFlag))
                {
                    best.Clear(); goldens.Clear(); deaths.Clear(); flagBoss.Clear();
                    File.Delete(ResetFlag);
                    Save();
                    Plugin.Log.LogInfo("Boss records: reset (boss times, deaths and golden berries)");
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Boss records reset: " + e.Message); }
        }
        public static string ResetFlag;

        void Save()
        {
            try
            {
                var lines = new List<string>();
                foreach (string g in goldens) lines.Add("golden:" + g + "=1");
                foreach (var kv in deaths) lines.Add("deaths:" + kv.Key + "=" + kv.Value);
                foreach (var kv in titles) lines.Add("title:" + kv.Key + "=" + kv.Value.Replace("=", " "));
                foreach (var kv in journal) lines.Add("journal:" + kv.Key + "=" + kv.Value);
                foreach (var kv in flagBoss) lines.Add("flagboss:" + kv.Key + "=" + kv.Value);
                foreach (var kv in areaTime) lines.Add("atime:" + kv.Key + "=" + kv.Value.ToString("R", CultureInfo.InvariantCulture));
                foreach (var kv in areaDeaths) lines.Add("adeaths:" + kv.Key + "=" + kv.Value);
                foreach (var kv in areaNames) lines.Add("aname:" + kv.Key + "=" + kv.Value.Replace("=", " "));
                foreach (var kv in best)
                    lines.Add("best:" + kv.Key + "=" + kv.Value.Time.ToString("R", CultureInfo.InvariantCulture) + ";" + kv.Value.Deaths + ";" + kv.Value.Dashes + ";" + kv.Value.Hits + ";" + kv.Value.Heals);
                Directory.CreateDirectory(DataDir);
                File.WriteAllLines(SaveFile, lines.ToArray());
            }
            catch (Exception e) { Plugin.Log.LogWarning("Boss records: " + e.Message); }
        }

#if TESTHARNESS
        /// <summary>Test build: show a card with made-up numbers.</summary>
        public static void TestCard(bool golden)
        {
            if (Instance == null) return;
            Instance.controlFor = Visuals.BossCardDelay;
            Instance.queue.Add(new Card { Title = "Moss Mother", Sub = "", Time = 83.47f, Deaths = 3, Dashes = 57, Hits = 2, Heals = golden ? 0 : 1, Golden = golden, HadGolden = false, Goldens = golden ? 1 : 0, Assist = !golden });
        }
#endif

#if TESTHARNESS
        public static bool TestShowing { get { return Instance != null && Instance.phase != Phase.None; } }
        public static string TestFight { get { var f = Instance != null ? Instance.fight : null; return f == null ? "none" : f.Id + " '" + f.Title + "' time " + f.Time.ToString("F2") + " hits " + f.Hits + " heals " + f.Heals + " dashes " + f.Dashes; } }
        /// <summary>Ends the current fight as if its defeated flag had turned on (the real path, minus the flag).</summary>
        public static void TestDefeat() { if (Instance != null) Instance.Defeated(GameManager.instance, "test"); }
        /// <summary>Removes a test boss from bosses.cfg again.</summary>
        public static void TestForget(string id)
        {
            if (Instance == null) return;
            foreach (int slot in new[] { 0, 1, 2, 3, 4, 5 }) { Instance.goldens.Remove(Key(slot, id)); Instance.deaths.Remove(Key(slot, id)); }
            Instance.Save();
        }
        /// <summary>The Titles sheet keys ending _MAIN, with their text (to check boss names).</summary>
        public static string TestTitles()
        {
            var b = new System.Text.StringBuilder();
            try
            {
                foreach (string k in TeamCherry.Localization.Language.GetKeys("Titles"))
                    if (k.EndsWith("_MAIN", StringComparison.Ordinal) && (k.Contains("MOTHER") || k.Contains("SPLINTER") || k.Contains("HUNTER_QUEEN") || k.Contains("BELLBEAST") || k.Contains("LACE") || k.Contains("VAMPIRE")))
                    {
                        string t, s;
                        Names(k.Substring(0, k.Length - 5), out t, out s);
                        b.Append(k).Append(" -> '").Append(t).Append("' / '").Append(s).Append("'; ");
                    }
            }
            catch (Exception e) { b.Append("error " + e.Message); }
            return b.ToString();
        }
        public static void TestView(bool open, int tab, bool all) { if (Instance == null) return; Instance.viewing = open; Instance.viewTab = tab; Instance.viewAll = all; Instance.viewIndex = 0; if (!open) Instance.viewT = 0f; }
        public static void TestDismiss() { if (Instance != null && Instance.phase == Phase.Shown) { Instance.phase = Phase.Out; Instance.t = 0f; } }
#endif

        // ---- the card ----------------------------------------------------------------------------------------

        void UpdateCard(HeroController h, GameManager gm, float dt)
        {
            bool playing = gm.GameState == GlobalEnums.GameState.PLAYING && !gm.isPaused && !h.cState.transitioning && !h.cState.dead;
            bool hasControl = playing && !h.controlReqlinquished;
            controlFor = hasControl ? controlFor + dt : 0f;
            if (phase == Phase.None && queue.Count > 0 && hasControl && controlFor >= Visuals.BossCardDelay)
            {
                card = queue[0];
                queue.RemoveAt(0);
                phase = Phase.In; t = 0f;
                Sfx.Play(Sounds.CardIn.Id);
                if (card.Golden && !card.HadGolden) Sfx.Play(Sounds.GoldenGet.Id);
                if (!h.controlReqlinquished) { h.RelinquishControl(); relinquished = true; }
            }
            switch (phase)
            {
                case Phase.In:
                    t += dt / Visuals.BossCardInTime;
                    if (t >= 1f) { phase = Phase.Shown; shownFor = 0f; photoTaken = card.Viewing; }
                    break;
                case Phase.Shown:
                    shownFor += dt;
                    // Theo's camera: the fallen boss's arena with its card, once the card has settled (user 2026-10-12)
                    if (!photoTaken && shownFor >= 0.8f) { photoTaken = true; TheoCamera.BossPhoto(card.Title); }
                    if (shownFor >= Visuals.PostcardButtonDelay && Confirm()) { phase = Phase.Out; t = 0f; Sfx.Play(Sounds.CardOut.Id); }
                    break;
                case Phase.Out:
                    t += dt / Visuals.BossCardOutTime;
                    if (t >= 1f) { phase = Phase.None; card = null; Release(h); }
                    break;
            }
        }

        static bool Confirm()
        {
            InputHandler input = InputHandler.Instance;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (input != null && input.inputActions != null && input.inputActions.Jump.WasPressed) return true;
            return kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
        }

        void Release(HeroController h)
        {
            if (relinquished && h != null) h.RegainControl();
            relinquished = false;
            Plugin.Controller.ClearInput();
        }

        void Hide(HeroController h)
        {
            if (phase == Phase.None) return;
            phase = Phase.None; card = null;
            Release(h);
        }

        public static string FormatTime(float seconds)
        {
            int cs = (int)(seconds * 100f);
            int m = cs / 6000, sec = cs / 100 % 60, c = cs % 100;
            int hours = m / 60;
            if (hours > 0) return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}.{3:00}", hours, m % 60, sec, c);
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}.{2:00}", m, sec, c);
        }

        // the golden berry of a healless attempt, top right under where the death counter shows
        void DrawHudBerry()
        {
            GuiArt.Img berry = BossCard.PixelBerry() ?? Art.Image("collectables/goldberry");
            if (berry == null || (hudAlpha <= 0f && hudLostT < 0f)) return;
            float s = Screen.height / 1080f, sw = Screen.width / s, sz = Visuals.GoldenHudSize;
            float x = sw - Visuals.DeathsMargin - sz, y = Visuals.GoldenHudTop + Mathf.Sin(Time.unscaledTime * 3f) * 4f, a = hudAlpha;
            if (hudLostT >= 0f)
            {
                float k = Mathf.Max(0f, hudLostT - Visuals.GoldenLaughTime);
                y -= k * k * 600f; a = Mathf.Clamp01(1f - k / 0.8f);
                x += Mathf.Sin(hudLostT * 40f) * (hudLostT < Visuals.GoldenLaughTime ? 4f : 0f);   // shakes while it laughs
            }
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            GuiArt.Draw(berry, new Rect(x, y, sz, sz), new Color(1f, 1f, 1f, a));
            GUI.matrix = old;
            GUI.color = Color.white;
        }

        // Celeste's area-select card (OuiChapterPanel), docked to the right edge, in 1920x1080 GUI units.
        void OnGUI()
        {
            if (TheoCamera.Hiding) return;   // out of Theo's photos
            if (Event.current.type == EventType.Repaint && Art != null && phase == Phase.None) DrawHudBerry();
            if (Event.current.type == EventType.Repaint && Art != null && viewT > 0f) { DrawViewer(); return; }
            if (Event.current.type == EventType.Repaint && Art != null) DrawAreaBanner();
            if (phase == Phase.None || card == null || Art == null || Event.current.type != EventType.Repaint) return;
            float s = Screen.height / 1080f, sw = Screen.width / s;
            float p = Mathf.Clamp01(t), ease;
            if (phase == Phase.In) ease = 1f - (1f - p) * (1f - p) * (1f - p);       // CubeOut
            else if (phase == Phase.Out) ease = 1f - p * p * p;                        // CubeIn, leaving
            else ease = 1f;
            float alpha = ease;
            Matrix4x4 old = GUI.matrix;
            GUI.depth = -1;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            GUI.color = new Color(0f, 0f, 0f, Visuals.BossCardDim * alpha);
            GUI.DrawTexture(new Rect(0f, 0f, sw, 1080f), Texture2D.whiteTexture);
            DrawCardBody(card, alpha, ease);
            GUI.matrix = old;
            GUI.color = Color.white;
        }

        // the card itself (after a fight, or in the records menu)
        void DrawCardBody(Card card, float alpha, float ease)
        {
            float s = Screen.height / 1080f, sw = Screen.width / s;
            bool gold = card.Golden || card.HadGolden;
            float sc = Visuals.BossCardScale;
            float slide = (1f - ease) * 900f;
            GuiArt.Img top = Art.Image(gold ? "areaselect/cardtop_golden" : "areaselect/cardtop");
            GuiArt.Img body = Art.Image(gold ? "areaselect/card_golden" : "areaselect/card");
            float cw = 855f * sc, x = sw - cw + slide, y = Visuals.BossCardTop;
            var white = new Color(1f, 1f, 1f, alpha);
            if (top != null) { GuiArt.Draw(top, new Rect(x, y, cw, top.H * sc), white); y += top.H * sc; }
            float bodyTop = y;
            if (body != null) GuiArt.Draw(body, new Rect(x, y, cw, body.H * sc), white);
            float bodyH = (body != null ? body.H : 900f) * sc;

            // the title ribbon (areaselect/title), sticking out to the left over the card's top, tinted
            GuiArt.Img ribbon = Art.Image("areaselect/title");
            float rh = 173f * sc, rw = 916f * sc, rx = sw - rw + slide * 1.2f, ry = Visuals.BossCardTop - rh * 0.55f;
            Color ribbonColor = Hex(gold ? Visuals.BossCardGoldColor : Visuals.BossCardTitleColor); ribbonColor.a = alpha;
            if (ribbon != null) GuiArt.Draw(ribbon, new Rect(rx, ry, rw, rh), ribbonColor);
            GuiArt.Img accent = Art.Image("areaselect/accent");   // OuiChapterPanel: the accent over the title, its own colour
            Color accentColor = Hex(gold ? Visuals.BossCardGoldAccent : Visuals.BossCardTitleAccent); accentColor.a = alpha;
            if (accent != null) GuiArt.Draw(accent, new Rect(rx, ry, rw, rh), accentColor);
            float ts = Visuals.BossCardTitleScale;
            string title = card.Title;
            while (ts > 0.5f && Art.Measure(title) * ts > rw - 140f * sc) ts -= 0.05f;
            float textX = rx + 90f * sc, mid = ry + rh / 2f;
            var shadow = new Color(0f, 0f, 0f, 0.35f * alpha);
            if (card.Sub.Length > 0)
            {
                float ss = ts * 0.55f;
                Art.DrawLine(card.Sub, textX, mid - Art.LineHeight * (ts + ss) / 2f, ss, new Color(1f, 1f, 1f, 0.85f * alpha));
                Art.DrawLine(title, textX + 3f, mid - Art.LineHeight * (ts - ss) / 2f + 3f, ts, shadow);
                Art.DrawLine(title, textX, mid - Art.LineHeight * (ts - ss) / 2f, ts, white);
            }
            else
            {
                Art.DrawLine(title, textX + 3f, mid - Art.LineHeight * ts / 2f + 3f, ts, shadow);
                Art.DrawLine(title, textX, mid - Art.LineHeight * ts / 2f, ts, white);
            }

            // stats, in dark ink on the card: deaths, time, dashes
            var ink = new Color(0.2f, 0.16f, 0.14f, alpha);
            float rs = Visuals.BossCardTextScale, isz = 90f * sc;
            // the stats block centred on the card (user 2026-10-13): icon, label, then the widest value
            float blockW = 120f * sc + 330f * sc + Art.Measure("00:00.000") * rs;
            float rowY = bodyTop + 70f * sc, iconX = x + (cw - blockW) / 2f, textLeft = iconX + 120f * sc;
            statsTop = rowY;
            // after a fight the numbers count up as the card lands (done before Theo's automatic photo at 0.4 s)
            float k = card.Viewing ? 1f : phase == Phase.Shown ? Mathf.Clamp01(shownFor / 0.3f) : phase == Phase.Out ? 1f : 0f;
            k = 1f - (1f - k) * (1f - k);
            Func<int, string> cnt = v => v < 0 ? N(v) : N(Mathf.RoundToInt(v * k));
            Stat(Art.Image("collectables/skullGold") ?? Art.Skull, "Deaths", cnt(card.Deaths), iconX, textLeft, ref rowY, isz, rs, ink, alpha, sc);
            float timeRow = rowY;
            Stat(ClockIcon(), "Time", card.Time < 0f ? "-" : FormatTime(card.Time * k), iconX, textLeft, ref rowY, isz, rs, ink, alpha, sc);
            // a faster win than this save's best before (user 2026-10-12)
            if (card.NewBest || card.PrevBest >= 0f)
            {
                string note = card.NewBest ? "New best!" : "Best " + FormatTime(card.PrevBest);
                float nx = textLeft + 330f * sc + Art.Measure(FormatTime(card.Time)) * rs + 24f * sc, ns = rs * 0.6f;
                Art.DrawLine(note, nx, timeRow + isz / 2f - Art.LineHeight * ns / 2f, ns, card.NewBest ? new Color(0.75f, 0.5f, 0.02f, alpha) : new Color(ink.r, ink.g, ink.b, 0.6f * alpha));
            }
            if (!card.AreaMode)
            {
                Stat(Art.Image("collectables/dashes"), "Dashes", cnt(card.Dashes), iconX, textLeft, ref rowY, isz, rs, ink, alpha, sc);
                Stat(Art.Image("collectables/heartgem/1/spin00"), "Hits taken", cnt(card.Hits), iconX, textLeft, ref rowY, isz, rs, ink, alpha, sc);
                Stat(Art.Image("collectables/heartgem/0/spin00"), "Heals", cnt(card.Heals), iconX, textLeft, ref rowY, isz, rs, ink, alpha, sc);
            }

            // the golden berry, in the card's grey bottom band
            GuiArt.Img berry = BossCard.PixelBerry() ?? Art.Image("collectables/goldberry");
            float bandTop = bodyTop + bodyH * 0.8f, bsz = 130f * sc * 1.4f;

            // badges for a clean fight (user 2026-10-13: better boss cards), in a row above the band when there's room
            if (!card.AreaMode && card.Time >= 0f)
            {
                var badges = new List<string>();
                if (card.Viewing ? card.Deaths == 0 : card.FirstTry) badges.Add("First Try");
                if (card.Hits == 0) badges.Add("No Hit");
                if (card.Heals == 0) badges.Add("No Heals");
                float bs = rs * 0.55f, ph = Art.LineHeight * bs + 14f * sc, bx = iconX, by = bandTop - ph - 22f * sc;
                float rightLimit = x + cw - 70f * sc - (card.Goldens > 0 ? 64f * sc + 120f * sc : 0f);
                if (badges.Count > 0 && by > rowY - 16f * sc)
                    for (int bi = 0; bi < badges.Count; bi++)
                    {
                        // each badge pops in a little after the numbers
                        float pk = card.Viewing ? 1f : Mathf.Clamp01((shownFor - 0.3f - bi * 0.12f) / 0.15f);
                        if (phase == Phase.Out) pk = 1f;
                        if (pk <= 0f) continue;
                        float pw = Art.Measure(badges[bi]) * bs + 28f * sc;
                        if (bx + pw > rightLimit) break;
                        Color fill = badges[bi] == "No Hit" ? new Color(0.75f, 0.5f, 0.05f, 0.9f * alpha * pk) : new Color(0.2f, 0.16f, 0.14f, 0.85f * alpha * pk);
                        float grow = 0.85f + 0.15f * pk;
                        var r2 = new Rect(bx + pw * (1f - grow) / 2f, by + ph * (1f - grow) / 2f, pw * grow, ph * grow);
                        GUI.color = fill; GUI.DrawTexture(r2, Texture2D.whiteTexture); GUI.color = Color.white;
                        Art.DrawLine(badges[bi], bx + 14f * sc, by + 7f * sc, bs, new Color(1f, 0.97f, 0.9f, alpha * pk));
                        bx += pw + 12f * sc;
                    }
            }
            float bob = card.Viewing || phase == Phase.Shown ? Mathf.Sin((card.Viewing ? Time.unscaledTime : shownFor) * 3f) * 6f : 0f;
            string line1, line2;
            if (card.AreaMode) { line1 = card.Note; line2 = "Deaths and time here."; }
            else if (card.Viewing) { line1 = card.Golden ? "Golden Berry" : "No Golden Berry"; line2 = card.Golden ? "Collected." : card.Time < 0f ? "From before records." : "Beat it without healing."; }
            else if (card.Golden && !card.HadGolden) { line1 = "Golden Berry!"; line2 = card.FirstTry && card.Heals > 0 ? "Beaten on the first try." : card.FirstTry ? "First try, no heals!" : "Beaten without healing."; }
            else if (card.Golden || card.HadGolden) { line1 = "Golden Berry"; line2 = card.Golden ? "No heals again!" : "Already collected."; }
            else if (card.Assist) { line1 = "No Golden Berry"; line2 = "Assist Mode was on."; }
            else { line1 = "No Golden Berry"; line2 = "Now: beat it without healing."; }
            if (berry != null && !card.AreaMode)
            {
                var br = new Rect(iconX - bsz * 0.15f, bandTop + (bodyH * 0.2f - bsz) / 2f + bob, bsz, bsz);
                GuiArt.Draw(berry, br, new Color(1f, 1f, 1f, (gold ? 1f : 0.3f) * alpha));
                if (gold)
                {
                    // Celeste's berry sparkle: little white glints winking around it
                    float tt = Time.unscaledTime;
                    for (int g = 0; g < 5; g++)
                    {
                        float ph2 = Mathf.Repeat(tt * 0.9f + g * 0.37f, 1f), a2 = Mathf.Sin(ph2 * Mathf.PI);
                        float ang = g * 2.4f + Mathf.Floor(tt * 0.9f + g * 0.37f) * 1.7f, rad = bsz * (0.45f + 0.12f * Mathf.Sin(g * 3.1f));
                        Vector2 c2 = br.center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
                        float sz = (5f + 5f * a2) * sc * 1.6f;
                        GUI.color = new Color(1f, 1f, 0.85f, a2 * alpha);
                        GUI.DrawTexture(new Rect(c2.x - sz / 2f, c2.y - 1.5f, sz, 3f), Texture2D.whiteTexture);
                        GUI.DrawTexture(new Rect(c2.x - 1.5f, c2.y - sz / 2f, 3f, sz), Texture2D.whiteTexture);
                    }
                    GUI.color = Color.white;
                }
            }
            float l1s = rs * 1.05f, l2s = rs * 0.7f, ty = bandTop + bodyH * 0.1f - Art.LineHeight * (l1s + l2s) / 2f;
            Art.DrawLine(line1, textLeft + 40f * sc, ty, l1s, gold ? new Color(0.55f, 0.38f, 0.02f, alpha) : ink);
            Art.DrawLine(line2, textLeft + 40f * sc, ty + Art.LineHeight * l1s, l2s, new Color(ink.r, ink.g, ink.b, 0.75f * alpha));
            // golden berries on this save, under the stats on the right (once there is at least one)
            if (berry != null && card.Goldens > 0 && !card.AreaMode)
            {
                string total = "x" + card.Goldens.ToString(CultureInfo.InvariantCulture);
                float tsz = 64f * sc, ts2 = rs * 0.75f, tw = tsz + 8f + Art.Measure(total) * ts2;
                float tx = x + cw - 70f * sc - tw, gy = bandTop - tsz - 24f * sc;
                GuiArt.Draw(berry, new Rect(tx, gy, tsz, tsz), white);
                Art.DrawLine(total, tx + tsz + 8f, gy + tsz / 2f - Art.LineHeight * ts2 / 2f, ts2, ink);
            }

            // Celeste stamps assisted runs (the stamp art is white: inked here)
            GuiArt.Img stamp = Art.Image("assistmodestamp");
            if (card.Assist && stamp != null)
            {
                Color stampInk = Hex(Visuals.BossCardStampColor); stampInk.a = 0.85f * alpha;
                GuiArt.Draw(stamp, new Rect(x + cw - 260f * sc, bodyTop + bodyH * 0.5f, 157f * sc * 1.3f, 131f * sc * 1.3f), stampInk);
            }

            // the continue prompt under the card, bottom right (where Celeste puts its confirm button)
            if (!card.Viewing && phase == Phase.Shown && shownFor > Visuals.PostcardButtonDelay)
            {
                float be = Mathf.Clamp01((shownFor - Visuals.PostcardButtonDelay) * 2f);
                const string prompt = "Jump to continue";
                float ps = 0.55f;
                float py = Math.Min(1080f - 8f - Art.LineHeight * ps, bodyTop + bodyH + 6f);
                Art.DrawOutlined(prompt, sw - 60f - Art.Measure(prompt) * ps, py, ps, new Color(1f, 1f, 1f, be), new Color(0f, 0f, 0f, be), 2f);
            }
            GUI.color = Color.white;
        }

        void Stat(GuiArt.Img icon, string label, string value, float iconX, float textLeft, ref float y, float isz, float rs, Color ink, float alpha, float sc)
        {
            if (icon != null)
            {
                // every icon fits the same square, centred in it
                float iw = isz * icon.W / Math.Max(icon.W, icon.H), ih = isz * icon.H / Math.Max(icon.W, icon.H);
                var ir = new Rect(iconX + (isz - iw) / 2f, y + (isz - ih) / 2f, iw, ih);
                // a soft drop shadow on the card, down and right, so every icon sits on the paper the same way
                float so = 6f * sc;
                GuiArt.Draw(icon, new Rect(ir.x + so, ir.y + so, ir.width, ir.height), new Color(0.2f, 0.12f, 0.08f, 0.28f * alpha));
                GuiArt.Draw(icon, new Rect(ir.x + so * 0.5f, ir.y + so * 0.5f, ir.width, ir.height), new Color(0.2f, 0.12f, 0.08f, 0.18f * alpha));
                GuiArt.Draw(icon, ir, new Color(1f, 1f, 1f, alpha));
            }
            float mid = y + isz / 2f - Art.LineHeight * rs / 2f;
            Art.DrawLine(label, textLeft, mid, rs * 0.8f, new Color(ink.r, ink.g, ink.b, 0.7f * alpha));
            Art.DrawLine(value, textLeft + 330f * sc, mid - Art.LineHeight * rs * 0.1f, rs, ink);
            // a faint dotted rule between rows, like a ledger (none under the last, where the berry sits)
            if (y > statsTop + 1f)
            {
                GUI.color = new Color(ink.r, ink.g, ink.b, 0.18f * alpha);
                float ly = y - 14f * sc;
                for (float dx = textLeft; dx < textLeft + 560f * sc; dx += 14f * sc) GUI.DrawTexture(new Rect(dx, ly, 7f * sc, 2f), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            y += isz + 28f * sc;
        }

        float statsTop;
        // Celeste's Gui atlas has no clock: a small pixel stopwatch in the card's colours
        static GuiArt.Img clock;
        static GuiArt.Img ClockIcon()
        {
            if (clock != null) return clock;
            // a gold stopwatch shaded like Celeste's Gui collectables: a dark outline, a gold case lit from the top left,
            // a cream face with a soft inner shadow, dark hands and a gold pin (rows are bottom-up)
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            Color32 outline = new Color32(43, 30, 24, 255), none = new Color32(0, 0, 0, 0);
            Color goldHi = new Color(1f, 0.89f, 0.5f), goldMid = new Color(0.9f, 0.66f, 0.18f), goldLo = new Color(0.62f, 0.4f, 0.08f);
            Color faceHi = new Color(1f, 0.98f, 0.92f), faceLo = new Color(0.86f, 0.8f, 0.68f);
            float c = 15.5f, cy = 13.5f, R = 12.5f;
            Func<float, float, float> light = (dx, dy) => Mathf.Clamp01(0.5f + (-dx + dy) / (2f * R));   // 1 at the top left
            for (int yy = 0; yy < n; yy++)
                for (int xx = 0; xx < n; xx++)
                {
                    float dx = xx - c, dy = yy - cy, d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color v;
                    if (d <= R - 4.2f)
                    {
                        float l = light(dx, dy);
                        v = Color.Lerp(faceLo, faceHi, Mathf.Clamp01(l * 1.3f));
                        if (d > R - 5.6f && l < 0.55f) v = Color.Lerp(v, faceLo * 0.85f, 0.5f);   // inner shadow under the case
                    }
                    else if (d <= R - 1.2f)
                    {
                        float l = light(dx, dy);
                        v = l > 0.62f ? goldHi : l > 0.32f ? goldMid : goldLo;
                    }
                    else if (d <= R + 0.3f) v = outline;
                    else { px[yy * n + xx] = none; continue; }
                    v.a = 1f; px[yy * n + xx] = v;
                }
            Action<int, int, Color32> P = (xx, yy, col) => { if (xx >= 0 && yy >= 0 && xx < n && yy < n) px[yy * n + xx] = col; };
            // the crown and its stem on top
            for (int xx = 13; xx <= 18; xx++) for (int yy = 28; yy <= 30; yy++) P(xx, yy, (xx == 13 || xx == 18 || yy == 30 || yy == 28) ? outline : (Color32)(xx < 16 ? goldHi : goldMid));
            for (int yy = 26; yy <= 27; yy++) { P(14, yy, outline); P(15, yy, (Color32)goldMid); P(16, yy, (Color32)goldLo); P(17, yy, outline); }
            // ticks at 12, 3, 6, 9
            Color32 tick = new Color32(120, 92, 60, 255);
            P(15, 21, tick); P(16, 21, tick); P(15, 6, tick); P(16, 6, tick); P(8, 13, tick); P(8, 14, tick); P(23, 13, tick); P(23, 14, tick);
            // hands: minute up, hour to the right, two pixels wide
            for (int i = 1; i <= 6; i++) { P(15, 13 + i, outline); P(16, 13 + i, outline); }
            for (int i = 1; i <= 4; i++) { P(16 + i, 13, outline); P(16 + i, 14, outline); }
            P(15, 13, (Color32)goldMid); P(16, 13, (Color32)goldHi); P(15, 14, (Color32)goldHi); P(16, 14, (Color32)goldMid);   // the pin
            // a glint on the case, top left
            P(7, 21, (Color32)Color.white); P(8, 22, (Color32)Color.white); P(6, 20, (Color32)goldHi);
            tex.SetPixels32(px); tex.Apply();
            clock = new GuiArt.Img { W = n, H = n, Tex = tex };
            return clock;
        }

        static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString("#" + hex, out c);
            return c;
        }

        // The journal pictures are tightly packed (and sometimes turned) in Silksong's atlases: drawn from the sprite's
        // own mesh with GL, so only the picture shows, the right way up. dst is in 1080p GUI units.
        static Material meshMat;
        static void DrawSpriteMesh(Sprite sp, Rect dst, float guiScale, float alpha)
        {
            if (meshMat == null) meshMat = new Material(Shader.Find("Sprites/Default"));
            meshMat.mainTexture = sp.texture;
            Bounds b = sp.bounds;
            Vector2[] v = sp.vertices, uv = sp.uv;
            ushort[] tri = sp.triangles;
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            meshMat.SetPass(0);
            GL.Begin(GL.TRIANGLES);
            GL.Color(new Color(1f, 1f, 1f, alpha));
            foreach (ushort i in tri)
            {
                float x = dst.x + (v[i].x - b.min.x) / b.size.x * dst.width;
                float y = dst.y + (b.max.y - v[i].y) / b.size.y * dst.height;   // GUI y runs down
                GL.TexCoord2(uv[i].x, uv[i].y);
                GL.Vertex3(x * guiScale, Screen.height - y * guiScale, 0f);
            }
            GL.End();
            GL.PopMatrix();
        }

        static string N(int v) { return v < 0 ? "-" : v.ToString(CultureInfo.InvariantCulture); }   // "-": beaten before records were kept
    }
}
