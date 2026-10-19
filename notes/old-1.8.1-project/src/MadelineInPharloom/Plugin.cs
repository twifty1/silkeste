using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using GlobalEnums;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    [BepInPlugin(Id, "Silkeste", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Id = "melty.madelineinpharloom";
        public const string Version = "1.8.1";

        public static ManualLogSource Log;
        public static readonly MadelineController Controller = new MadelineController();
        static AssistMode Assist;
        /// <summary>The test harness asks for the next skin as if F1 were pressed.</summary>
        public static bool NextSkinRequested;
        static bool skinSettingSeeded, lastBackpack = true;
        readonly Dictionary<string, CelesteAssets> noBackpack = new Dictionary<string, CelesteAssets>();
        static string lastOutfit;
        /// <summary>The settings preview: the assets the current look would use.</summary>
        public static Func<SkinRow, CelesteAssets> PreviewAssets;
        // the decoded Celeste atlas, kept so skins can be built when first needed
        static CelesteAssets.Prepared Prebuilt;
        void EnsureSkin(SkinRow k)
        {
            if (skins.ContainsKey(k.Id) || Prebuilt == null) return;
            skins[k.Id] = CelesteAssets.Build(Prebuilt, k.CelesteSprite, k.Palette, null, k.FallbackPalette, k.HairNormal);
            // assist.backpack: Madeline's own sprite has Celeste's no-backpack frames (Prologue, Farewell)
            // skins.no_backpack: "same" reuses the skin's palette; Badeline's keeps her frames and only fills the backpack area
            if (k.NoBackpack == "same") noBackpack[k.Id] = CelesteAssets.Build(Prebuilt, "player_no_backpack", k.Palette, null, "none", k.HairNormal);
            else if (k.NoBackpack != "none") noBackpack[k.Id] = CelesteAssets.Build(Prebuilt, k.CelesteSprite, k.Palette, k.NoBackpack, k.FallbackPalette);   // her own art, backpack cut out
        }

        CelesteAssets SkinAssets(SkinRow k)
        {
            // assist.outfit: a Madeline-shaped skin wears another skin's clothes (its palette); ears, tail and hair stay the skin's
            SkinRow src = k;
            string o = AssistMode.OutfitName;
            if (o != "Skin default" && k.CelesteSprite == "player" && k.Style != "pico8" && !MadelineView.Masked(k.Style))
            {
                SkinRow r = Array.Find(Skins.All, x => x.Name == o);
                if (r.Id != null) src = r;
            }
            EnsureSkin(src);
            CelesteAssets nb;
            return (!AssistMode.Backpack || MadelineView.Masked(k.Style)) && noBackpack.TryGetValue(src.Id, out nb) ? nb : skins[src.Id];   // the Knight never wears one
        }
        public static bool Ready;

        static volatile CelesteAssets.Prepared prepared;
        static GuiArt guiArt;
        static string prepareError;
        CelesteAssets assets;   // current skin
        readonly Dictionary<string, CelesteAssets> skins = new Dictionary<string, CelesteAssets>();
        SkinRow skin = Skins.Madeline;
        static int hurtHp = -1;
        static float nextEh;
        static string voice = Skins.Madeline.Voice;   // skins.voice: sound-id prefix of the current skin's talk babble
        string skinFile;
        MadelineView view;
        HeroController viewHero;

        void Awake()
        {
            Log = Logger;
            string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MadelineInPharloom");
            skinFile = Path.Combine(dataDir, "skin.cfg");
            controlsFile = Path.Combine(dataDir, "controls.cfg");
            try { string saved = File.Exists(skinFile) ? File.ReadAllText(skinFile).Trim() : ""; foreach (SkinRow k in Skins.All) if (k.Id == saved) skin = k; } catch { }
            voice = skin.Voice;
            string celeste = FindCeleste(Path.Combine(dataDir, "settings.cfg"));
            if (celeste == null)
            {
                Log.LogError("Celeste not found. Melty sets CelesteDir in " + Path.Combine(dataDir, "settings.cfg") + "; Hornet stays as she is.");
                CelesteMissing = true;   // OnGUI tells the player, so a Hornet-only game is not a mystery
                return;
            }
            Log.LogInfo("Reading Madeline from the player's Celeste at " + celeste);
            new Thread(() =>
            {
                try { guiArt = GuiArt.Prepare(celeste); }   // before prepared is published, so Update sees both
                catch (Exception e) { Log.LogWarning("Death counter art unavailable: " + e.Message); }
                try { prepared = CelesteAssets.Prepare(celeste); }
                catch (Exception e) { prepareError = e.Message; }
            }) { IsBackground = true, Name = "MadelineAtlas" }.Start();
            Sfx.Start(celeste, Path.Combine(dataDir, "cache"), Path.GetDirectoryName(Info.Location));
            ApplyHooks(new Harmony(Id));
            // Silksong turns Unity's own log off, so a failure here would otherwise be silent.
            try
            {
                gameObject.AddComponent<CelesteWipe>();
                DeathCounter.DataDir = PostcardView.DataDir = dataDir;
                gameObject.AddComponent<DeathCounter>();
                gameObject.AddComponent<PostcardView>();
                AssistMode.DataDir = BossCard.DataDir = SpeedrunTimer.DataDir = dataDir;
                BossCard.ResetFlag = Path.Combine(Path.GetDirectoryName(Info.Location), "reset_records.flag");
                Assist = gameObject.AddComponent<AssistMode>();
                gameObject.AddComponent<BossCard>();
                gameObject.AddComponent<SpeedrunTimer>();
                gameObject.AddComponent<TheoCamera>();
                gameObject.AddComponent<Achievements>();
                gameObject.AddComponent<CelesteTextbox>();
                gameObject.AddComponent<CelesteYesNo>();
                gameObject.AddComponent<MirrorMode>();
                gameObject.AddComponent<OneHitMode>();
                gameObject.AddComponent<ChaosMode>();
                gameObject.AddComponent<CelesteFileSelect>();
                gameObject.AddComponent<CelesteMenus>();
#if TESTHARNESS
                gameObject.AddComponent<MoveTracker>();
#endif
                ToolItemManager.BoundAttackToolUsed += OnAttackToolUsed;
                Controller.DashBegan += () => { if (Assist != null) Assist.BeginDashFreeze(); BossCard.OnDash(); };
#if TESTHARNESS
                // test.cfg in the data folder, or next to the plugin (a sandboxed tool can write only there)
                TestHarness.ConfigFile = new[] { Path.Combine(Path.GetDirectoryName(Info.Location), "test.cfg"), Path.Combine(dataDir, "test.cfg") }.FirstOrDefault(File.Exists);
                bool testCfg = TestHarness.ConfigFile != null;
                Log.LogInfo("[TEST] test.cfg " + (testCfg ? "found" : "not found") + " in " + dataDir);
                if (testCfg) { var th = gameObject.AddComponent<TestHarness>(); Log.LogInfo("[TEST] harness added, enabled " + th.enabled + ", object active " + gameObject.activeInHierarchy); }
#endif
                Log.LogInfo("Screens ready: postcards, death counter, Settings (F1), boss cards, speedrun timer (F3)");
            }
            catch (Exception e) { Log.LogError("Could not set up the screens: " + e); }
        }

        // Melty writes CelesteDir={game:celeste} before every Play; outside Melty, fall back to the Steam libraries.
        static string FindCeleste(string settingsFile)
        {
            try
            {
                if (File.Exists(settingsFile))
                    foreach (string line in File.ReadAllLines(settingsFile))
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0 && line.Substring(0, eq).Trim() == "CelesteDir")
                        {
                            string dir = line.Substring(eq + 1).Trim();
                            if (IsCeleste(dir)) return dir;
                        }
                    }
                // Steam: wherever it is installed (the registry knows), and every one of its libraries
                var steams = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam") };
                foreach (string key in new[] { @"HKEY_CURRENT_USER\Software\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam" })
                    foreach (string name in new[] { "SteamPath", "InstallPath" })
                    {
                        try { var v = Microsoft.Win32.Registry.GetValue(key, name, null) as string; if (!string.IsNullOrEmpty(v)) steams.Add(v.Replace('/', '\\')); }
                        catch (Exception) { }
                    }
                var libs = new List<string>();
                foreach (string steam in steams)
                {
                    libs.Add(steam);
                    string vdf = Path.Combine(steam, "steamapps/libraryfolders.vdf");
                    if (File.Exists(vdf))
                        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                            libs.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
                }
                foreach (string lib in libs)
                {
                    string dir = Path.Combine(lib, "steamapps/common/Celeste");
                    if (IsCeleste(dir)) return dir;
                }
                // Epic: its install manifests name the folder
                string manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Epic\EpicGamesLauncher\Data\Manifests");
                if (Directory.Exists(manifests))
                    foreach (string item in Directory.GetFiles(manifests, "*.item"))
                    {
                        Match m = Regex.Match(File.ReadAllText(item), "\"InstallLocation\"\\s*:\\s*\"([^\"]+)\"");
                        if (m.Success) { string dir = m.Groups[1].Value.Replace("\\\\", "\\"); if (IsCeleste(dir)) return dir; }
                    }
                // the usual folders of the other stores
                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                foreach (string dir in new[] { Path.Combine(pf, @"Epic Games\Celeste"), Path.Combine(pf86, @"GOG Galaxy\Games\Celeste"), @"C:\GOG Games\Celeste", Path.Combine(pf, "Celeste"), Path.Combine(pf86, "Celeste") })
                    if (IsCeleste(dir)) return dir;
            }
            catch (Exception e) { Log.LogWarning("Looking for Celeste: " + e.Message); }
            return null;
        }

        static bool CelesteMissing;
        static string clashNotice;
        static readonly HashSet<MethodBase> ourTargets = new HashSet<MethodBase>();
        float missingShown;

        // Runs once every plugin has loaded: names the other mods in the log, so player reports show them,
        // and warns the player when one changes the same Hornet code as Silkeste.
        void Start()
        {
            try
            {
                var others = BepInEx.Bootstrap.Chainloader.PluginInfos.Values
                    .Where(p => p.Metadata.GUID != Id)
                    .Select(p => p.Metadata.Name + " " + p.Metadata.Version).ToList();
                Log.LogInfo(others.Count == 0 ? "Other mods: none" : "Other mods: " + string.Join(", ", others.ToArray()));
                var clashing = new HashSet<string>();
                foreach (var m in ourTargets)
                {
                    var info = Harmony.GetPatchInfo(m);
                    if (info == null) continue;
                    foreach (var owner in info.Owners)
                        if (owner != Id && !owner.StartsWith("silkeste", StringComparison.OrdinalIgnoreCase))
                        {
                            Log.LogWarning("Another mod (" + owner + ") also changes " + m.DeclaringType?.Name + "." + m.Name + "; it may clash with Silkeste");
                            clashing.Add(OwnerName(owner));
                        }
                }
                if (clashing.Count > 0)
                    clashNotice = "Silkeste: " + string.Join(", ", clashing.ToArray()) + (clashing.Count == 1 ? " changes" : " change") +
                        " Hornet too and may clash with Silkeste.\nIf something breaks, try turning " + (clashing.Count == 1 ? "it" : "them") + " off.";
            }
            catch (Exception e) { Log.LogWarning("Couldn't check other mods: " + e.Message); }
        }

        static string OwnerName(string harmonyId)
        {
            foreach (var p in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
                if (p.Metadata.GUID == harmonyId) return p.Metadata.Name;
            return harmonyId;
        }

        void OnGUI()
        {
            if ((!CelesteMissing && clashNotice == null) || Event.current.type != EventType.Repaint || (missingShown += Time.unscaledDeltaTime) > 25f) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = Math.Max(14, Screen.height / 45), wordWrap = true };
            string msg = CelesteMissing
                ? "Silkeste couldn't find Celeste on this PC, so you're still playing as Hornet.\nInstall Celeste, then start the game from Melty again."
                : clashNotice;
            var r = new Rect(Screen.width * 0.15f, Screen.height * 0.04f, Screen.width * 0.7f, Screen.height * 0.2f);
            GUI.color = Color.black; GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), msg, style);
            GUI.color = Color.white; GUI.Label(r, msg, style);
        }

        static bool IsCeleste(string dir)
        {
            return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "Content/Graphics/Atlases/Gameplay.meta")) && File.Exists(Path.Combine(dir, "Content/Graphics/Sprites.xml"));
        }

        // hooks.default_controls: the first time Silkeste runs it sets its default controls, once; later rebinds are
        // the player's. Controller: Silksong's defaults with hooks.controller_layout applied (R1 dash, Square tools).
        // Keyboard: the user's layout (2026-10-11); map and inventory keys stay as they are, except the tools menu,
        // which moves off Q to make room for the Clawline.
        static string controlsFile;
        static readonly AccessTools.FieldRef<InputHandler, GameSettings> InputSettings = AccessTools.FieldRefAccess<InputHandler, GameSettings>("gs");

        static void SetDefaultControlsOnce()
        {
            if (controlsFile == null) return;
            InputHandler input = InputHandler.Instance;
            GameSettings gs = input != null ? InputSettings(input) : null;
            if (gs == null) return;
            string file = controlsFile;
            controlsFile = null;
            try
            {
                if (File.Exists(file)) return;
                gs.upKey = "W"; gs.leftKey = "A"; gs.downKey = "S"; gs.rightKey = "D";
                gs.jumpKey = "Space";
                gs.dashKey = "LeftShift";      // Silksong's Dash / Sprint: Madeline's grab and climb
                gs.castKey = "E";              // bind (heal)
                gs.dreamNailKey = "F";         // Needolin
                gs.superDashKey = "Q";         // Clawline
                gs.inventoryToolsKey = "R";    // was Q
                gs.attackKey = "LeftButton";   // Madeline's dash
                gs.quickCastKey = "RightButton";   // tools and silk skills, with W / S for the others
                gs.SaveKeyboardSettings();
                input.ResetAllControllerButtonBindings();       // saved layouts of every controller type
                input.ResetDefaultControllerButtonBindings();   // live bindings: keyboard from gs, then the controller in use
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllText(file, "default_controls_applied=1\n");
                Log.LogInfo("Default controls set (first run only): WASD, Space jump, mouse dash and tools, Shift grab; controller R1 dash, Square tools, rest Silksong's");
            }
            catch (Exception e) { Log.LogWarning("Could not set the default controls: " + e.Message); }
        }

        void Update()
        {
            Sfx.Pump();
            SetDefaultControlsOnce();
            if (assets == null)
            {
                if (prepareError != null) { Log.LogError("Could not read Madeline from Celeste: " + prepareError); prepareError = null; enabled = false; return; }
                if (prepared == null) return;
                try
                {
                    // Only Madeline (the golden berry and fallbacks) and the look in use are built now; every other skin
                    // the first time it is worn or previewed (SkinAssets). Building all 24 sets at once froze the game for
                    // a long time on slower PCs (players closed it, or it never seemed to start).
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    Prebuilt = prepared;
                    EnsureSkin(Skins.Madeline);
                    SkinRow wantedAtStart = Array.Find(Skins.All, k => k.Name == AssistMode.SkinName);
                    if (wantedAtStart.Id != null) skin = wantedAtStart;
                    assets = SkinAssets(skin);
                    lastBackpack = AssistMode.Backpack; lastOutfit = AssistMode.OutfitName;
                    BossCard.GoldFrames = skins[Skins.Madeline.Id].GoldIdle;
                    if (guiArt != null) { guiArt.Build(); DeathCounter.Art = PostcardView.Art = AssistMode.Art = BossCard.Art = SpeedrunTimer.Art = guiArt; }
                    PreviewAssets = SkinAssets;
                    Ready = true;
                    Log.LogInfo("Madeline loaded: " + assets.Anims.Count + " animations from Celeste in " + watch.ElapsedMilliseconds + " ms, current " + skin.Name + " (other skins build when first worn)");
                }
                catch (Exception e) { Log.LogError("Could not build Madeline: " + e); enabled = false; return; }
            }
            HeroController h = HeroController.SilentInstance;
            if (h == null) return;
            ReadHornetVoice(h);
#if TESTHARNESS
            // Test build only: F12 diagnostic dumps, F11 refills silk (F1-F4 are the player's keys).
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (!loggedTools && PlayerData.instance != null)
            {
                loggedTools = true; Log.LogInfo("[TEST] Longclaw multiplier " + GlobalSettings.Gameplay.LongNeedleMultiplier);   // test: every tool's internal name, to match tools to Madeline's bonuses
                try { foreach (ToolItem t in ToolItemManager.GetAllTools()) if (t != null) Log.LogInfo("[TEST] tool " + t.name + " '" + t.DisplayName + "' type=" + t.Type + " equipped=" + t.IsEquipped + " class=" + t.GetType().Name); }
                catch (Exception e) { Log.LogWarning("[TEST] tools: " + e.Message); }
            }
            if (kb != null)
            {
                if (!loggedLayers)
                {
                    loggedLayers = true;
                    int me = h.gameObject.layer;
                    Log.LogInfo("[TEST] hero layer " + LayerMask.LayerToName(me) + " ignores Terrain=" + Physics2D.GetIgnoreLayerCollision(me, 8) + " Soft Terrain=" + Physics2D.GetIgnoreLayerCollision(me, 25));
                }
                if (kb.f9Key.wasPressedThisFrame && PlayerData.instance != null)
                {
                    PlayerData.instance.hasDash = !PlayerData.instance.hasDash;   // toggles Swift Step to test its bonus
                    Controller.HairFlashTimer = Visuals.HairFlashTime;
                    Log.LogInfo("[TEST] Swift Step (hasDash) = " + PlayerData.instance.hasDash);
                }
                if (kb.f10Key.wasPressedThisFrame && PlayerData.instance != null)
                {
                    // test: every tool, silk skill, crest and ability, to try them all with Madeline
                    PlayerData pd = PlayerData.instance;
                    pd.hasDash = pd.hasWalljump = pd.hasDoubleJump = pd.hasBrolly = pd.hasHarpoonDash = pd.hasSuperJump = true;
                    pd.hasNeedolin = pd.hasChargeSlash = pd.hasNeedleThrow = pd.hasThreadSphere = pd.hasParry = true;
                    pd.hasSilkCharge = pd.hasSilkBomb = pd.hasSilkBossNeedle = pd.hasSilkSpecial = true;
                    pd.UnlockedExtraBlueSlot = pd.UnlockedExtraYellowSlot = true;
                    try { ToolItemManager.UnlockAllTools(); } catch (Exception e) { Log.LogWarning("[TEST] tools: " + e.Message); }
                    try { ToolItemManager.UnlockAllCrests(); } catch (Exception e) { Log.LogWarning("[TEST] crests: " + e.Message); }
                    Controller.HairFlashTimer = Visuals.HairFlashTime;
                    // and money to buy things with (user 2026-10-14: test the shops' yes/no box)
                    try { CurrencyManager.AddCurrency(99999, CurrencyType.Money); CurrencyManager.AddCurrency(9999, CurrencyType.Shard); } catch (Exception e) { Log.LogWarning("[TEST] money: " + e.Message); }
                    Log.LogInfo("[TEST] unlocked all tools, skills, crests and abilities, and added rosaries and shell shards");
                }
                if (kb.f7Key.wasPressedThisFrame && PlayerData.instance != null)
                {
                    PlayerData.instance.hasNeedolin = true;   // to preview Madeline with the Needolin
                    Log.LogInfo("[TEST] hasNeedolin set");
                }
                if (kb.f12Key.wasPressedThisFrame)
                {
                    Log.LogInfo("[TEST] collision " + Controller.DebugCollision(h));
                    // Colliders near the hero: layer, tag, components, and what each PlayMaker FSM listens for.
                    foreach (Collider2D c in Physics2D.OverlapCircleAll(h.transform.position, 3f, ~0))
                    {
                        if (c.transform.IsChildOf(h.transform)) continue;
                        Transform t = c.transform;
                        string comps = string.Join(",", Array.ConvertAll(t.GetComponents<Component>(), x => x == null ? "null" : x.GetType().Name));
                        Log.LogInfo(string.Format("[TEST] C {0} parent={1} layer={2} tag={3} trigger={4} comps={5}", c.name, t.parent ? t.parent.name : "-", LayerMask.LayerToName(c.gameObject.layer), c.tag, c.isTrigger, comps));
                        foreach (Component fsm in t.GetComponentsInParent<Component>())
                        {
                            if (fsm == null || fsm.GetType().Name != "PlayMakerFSM") continue;
                            object f = fsm.GetType().GetProperty("Fsm").GetValue(fsm, null);
                            var states = (Array)f.GetType().GetProperty("States").GetValue(f, null);
                            var listens = new List<string>();
                            foreach (object st in states)
                                foreach (object act in (Array)st.GetType().GetProperty("Actions").GetValue(st, null))
                                {
                                    string an = act.GetType().Name;
                                    if (!an.Contains("Trigger") && !an.Contains("Collision") && !an.Contains("Damage") && !an.Contains("Hit")) continue;
                                    string extra = "";
                                    foreach (var fi in act.GetType().GetFields())
                                        if (fi.Name.ToLower().Contains("tag") || fi.Name.ToLower().Contains("layer") || fi.Name.ToLower().Contains("event"))
                                        { object val = fi.GetValue(act); extra += " " + fi.Name + "=" + (val == null ? "null" : (val.GetType().GetProperty("Value") != null ? val.GetType().GetProperty("Value").GetValue(val, null) : val)); }
                                    listens.Add(st.GetType().GetProperty("Name").GetValue(st, null) + ":" + an + extra);
                                }
                            Log.LogInfo("[TEST]   FSM " + fsm.gameObject.name + "/" + fsm.GetType().GetProperty("FsmName").GetValue(fsm, null) + " " + string.Join(" | ", listens.ToArray()));
                        }
                    }
                }
                if (kb.f11Key.wasPressedThisFrame) { h.RefillSilkToMax(); Log.LogInfo("[TEST] silk refilled"); }   // test: full silk
                if (kb.f12Key.wasPressedThisFrame)   // renderer dump, with the collision dump above
                {
                    // Every renderer near the hero, to see what draws over Madeline.
                    Vector3 hp = h.transform.position;
                    foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    {
                        if (!r.enabled || !r.gameObject.activeInHierarchy || r.bounds.SqrDistance(hp) > 4f) continue;
                        Material m = r.sharedMaterial;
                        Log.LogInfo(string.Format("[TEST] R {0} type={1} layer={2}/{3} z={4:F3} shader={5} color={6} parent={7}",
                            r.name, r.GetType().Name, r.sortingLayerName, r.sortingOrder, r.transform.position.z,
                            m ? m.shader.name : "none", r is SpriteRenderer ? ((SpriteRenderer)r).color.ToString() : "-", r.transform.parent ? r.transform.parent.name : "-"));
                    }
                    Log.LogInfo("[TEST] camera post: " + string.Join(",", Array.ConvertAll(Camera.main.GetComponents<Behaviour>(), c => c.GetType().Name + (c.enabled ? "" : "(off)"))));
                }
            }
            {
                // Log Hornet's animation clip and atBench whenever they change (bench sit investigation).
                var anim = h.GetComponent<tk2dSpriteAnimator>();
                string clip = anim != null && anim.CurrentClip != null ? anim.CurrentClip.name : "none";
                string madeline = view != null ? view.CurrentAnimId : "none";
                if (madeline.Contains("#")) madeline = madeline.Substring(0, madeline.IndexOf('#'));
                string state = clip + " atBench=" + h.playerData.atBench + " nearBench=" + h.cState.nearBench + " relinquished=" + h.controlReqlinquished + " madeline=" + madeline;
                if (state != lastHeroState)
                {
                    var col = h.GetComponent<Collider2D>();
                    var mr = h.GetComponent<MeshRenderer>();
                    Log.LogInfo(string.Format("[TEST] hero {0} pos=({1:F2},{2:F2}) col=({3:F2}..{4:F2}, enabled {5}) sprite=({6:F2}..{7:F2})", state,
                        h.transform.position.x, h.transform.position.y, col.bounds.min.y, col.bounds.max.y, col.enabled,
                        mr != null ? mr.bounds.min.y : 0f, mr != null ? mr.bounds.max.y : 0f));
                    lastHeroState = state;
                }
            }
            if (TestHarness.Driving)
            {
                Controller.InjectInput(TestHarness.MoveX, TestHarness.MoveY, TestHarness.Aim, TestHarness.JumpPressed, TestHarness.JumpHeld, TestHarness.DashPressed, TestHarness.GrabHeld);
                return;
            }
#endif
            InputHandler input = InputHandler.Instance;
            if (input != null && input.inputActions != null) Controller.ReadInput(input.inputActions);
        }

#if TESTHARNESS
        static string lastHeroState;
        static bool loggedLayers, loggedTools;

#endif

        void LateUpdate()
        {
            if (!Ready) return;
            HeroController h = HeroController.SilentInstance;
            // Quitting to the menu leaves her objects (kept across scenes) frozen on screen unless they are hidden.
            GameManager gm = GameManager.instance;
            if (h == null || gm == null || gm.GameState == GameState.MAIN_MENU || gm.IsMenuScene() || gm.IsTitleScreenScene())
            {
                if (view != null) view.Hide();
                return;
            }
            if (view == null || viewHero != h)
            {
                if (view != null) view.Destroy();
                view = new MadelineView(assets, skin, Controller);
                viewHero = h;
            }
            // inputs.skin_switch: F1 anywhere swaps the skin (cosmetic only); assist.skin picks it in the settings.
            // The setting is the one source: F1 moves it on, and a different setting switches the skin.
            if (!skinSettingSeeded) { skinSettingSeeded = true; if (!AssistMode.HasSaved("skin")) AssistMode.SetSkin(skin.Name, false); }
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (AssistMode.KeyPressed(AssistOptions.KeySkin) || NextSkinRequested)
            {
                NextSkinRequested = false;
                AssistMode.NextLook();   // assist.look: the next saved look
            }
            SkinRow wanted = Array.Find(Skins.All, k => k.Name == AssistMode.SkinName);
            bool backpack = AssistMode.Backpack;
            if (wanted.Id != null && !AssistMode.IsOpen && (wanted.Id != skin.Id || backpack != lastBackpack || AssistMode.OutfitName != lastOutfit))
            {
                lastBackpack = backpack; lastOutfit = AssistMode.OutfitName;
                skin = wanted;
                voice = skin.Voice;
                assets = SkinAssets(skin);
                view.SetSkin(assets, skin);
                if (Time.timeScale > 0f) Controller.HairFlashTimer = Visuals.HairFlashTime;   // the settings menu stops time: a flash there would never fade
                Sfx.Play(Sounds.Refill.Id, 0.7f);
                try { File.WriteAllText(skinFile, skin.Id); } catch (Exception e) { Log.LogWarning("Could not save skin: " + e.Message); }
                Log.LogInfo("Skin: " + skin.Name);
            }
            if (h.cState.dead || h.cState.hazardDeath) HideHornetDeath(h);   // hooks.hide_hornet_death
            // In place of Hornet's hurt cry (hooks.mute_hornet_*): Madeline's startled "eh!" when a hit doesn't kill her.
            int hp = h.playerData.health;
            // at most once every 7-10 s (a random wait each time, user 2026-10-12)
            if (hurtHp >= 0 && hp < hurtHp && !MadelineView.Masked(skin.Style) /* the Knight and Hornet are silent */ && hp > 0 && !h.cState.dead && !h.cState.hazardDeath && Time.unscaledTime >= nextEh && (nextEh = Time.unscaledTime + UnityEngine.Random.Range(7f, 10f)) > 0f)
                { }   // (user 2026-10-12: no voice on hits after all; Hornet's cry stays muted)
#if TESTHARNESS
            if (hp != hurtHp) Log.LogInfo("[TEST] hp " + hurtHp + " -> " + hp + " dead=" + h.cState.dead + " hazard=" + h.cState.hazardDeath + " voice=" + voice);
#endif
            hurtHp = hp;
            view.LateUpdate(h, Time.deltaTime);
        }

        // Silksong's death and hazard-death objects draw Hornet with her own tk2d sprite collections; hide just
        // those sprites so the silk, blood and splash effects (other collections, particles) still play.
        static HashSet<tk2dSpriteCollectionData> hornetCollections;
        static void HideHornetDeath(HeroController h)
        {
            if (hornetCollections == null)
            {
                hornetCollections = new HashSet<tk2dSpriteCollectionData>();
                var animator = h.GetComponent<tk2dSpriteAnimator>();
                if (animator != null && animator.Library != null)
                    foreach (tk2dSpriteAnimationClip clip in animator.Library.clips)
                        if (clip != null && clip.frames != null)
                            foreach (tk2dSpriteAnimationFrame f in clip.frames)
                                if (f != null && f.spriteCollection != null) hornetCollections.Add(f.spriteCollection);
                Log.LogInfo("Hornet uses " + hornetCollections.Count + " sprite collections; hidden in death effects");
            }
            foreach (tk2dBaseSprite s in FindObjectsByType<tk2dBaseSprite>(FindObjectsSortMode.None))
            {
                if (s.gameObject == h.gameObject || s.Collection == null || !hornetCollections.Contains(s.Collection)) continue;
                var r = s.GetComponent<Renderer>();
                if (r != null) r.forceRenderingOff = true;
            }
        }

        // ---- hooks (sheets/hooks.json) ----------------------------------------------------------------------

        static readonly Dictionary<string, KeyValuePair<string, string>> PatchFor = new Dictionary<string, KeyValuePair<string, string>>
        {
            { Hooks.PlayerController.Id, P("Postfix", nameof(FixedUpdatePostfix)) },
            { Hooks.BlockJump.Id, P("Prefix", nameof(BlockWhileActive)) },
            { Hooks.BlockDoubleJump.Id, P("Prefix", nameof(BlockWhileActive)) },
            { Hooks.BlockWallJump.Id, P("Prefix", nameof(BlockWhileActive)) },
            { Hooks.BlockWallSlide.Id, P("Prefix", nameof(BlockWhileActive)) },
            { Hooks.BlockFloat.Id, P("Prefix", nameof(BlockWhileActive)) },
            { Hooks.BlockSprintDash.Id, P("Prefix", nameof(BlockWhileActive)) },
            { Hooks.AttackInput.Id, P("Prefix", nameof(BlockWhileActive)) },
            { Hooks.BlockHardLanding.Id, P("Prefix", nameof(BlockWhileActive)) },
            { Hooks.BlockNailCharge.Id, P("Prefix", nameof(BlockWhileActive)) },
            { Hooks.BlockClamber.Id, P("Prefix", nameof(BlockWhileActive)) },
            { Hooks.EnemyContactDamage.Id, P("Prefix", nameof(TakeDamagePrefix)) },
            { Hooks.DelayHazardFade.Id, P("Prefix", nameof(DelayHazardFadePrefix)) },
            { Hooks.ShortenHazardReload.Id, P("Transpiler", nameof(HazardReloadTranspiler)) },
            { Hooks.DelayDeathFade.Id, P("Prefix", nameof(DelayFadePrefix)) },
            { Hooks.SkipWalkIn.Id, P("Prefix", nameof(SkipWalkInPrefix)) },
            { Hooks.HornetVoice.Id, P("Prefix", nameof(HornetVoicePrefix)) },
            { Hooks.UpdraftSpeed.Id, P("Postfix", nameof(UpdraftPostfix)) },
            { Hooks.NpcVoice.Id, P("Prefix", nameof(NpcVoicePrefix)) },
            { Hooks.ControllerLayout.Id, P("Postfix", nameof(ControllerLayoutPostfix)) },
            { Hooks.BossTitle.Id, P("Postfix", nameof(BossTitlePostfix)) },
            { Hooks.MuteHornetSelect.Id, P("Prefix", nameof(MuteClipPrefix)) },
            { Hooks.MuteHornetSelectAny.Id, P("Prefix", nameof(MuteClipPrefix)) },
            { Hooks.MuteHornetSelectRandom.Id, P("Prefix", nameof(MuteClipPrefix)) },
            { Hooks.MuteHornetPlay.Id, P("Prefix", nameof(MutePlayPrefix)) },
            { Hooks.MuteHornetPlayPitch.Id, P("Prefix", nameof(MutePlayPrefix)) },
        };

        // hooks.mute_hornet_*: Hornet's own voice tables on the hero never play (Madeline is silent when hurt, as in Celeste).
        static readonly HashSet<RandomAudioClipTable> hornetVoice = new HashSet<RandomAudioClipTable>();
        static HeroController voiceHero;
        static readonly string[] HornetVoiceFields = { "woundAudioTable", "woundHeavyAudioTable", "woundFrostAudioTable", "hazardDamageAudioTable", "pitFallAudioTable", "deathAudioTable", "gruntAudioTable", "attackAudioTable", "warriorRageAttackAudioTable" };   // not quickSlingAudioTable: that is the tool's twang
        static void ReadHornetVoice(HeroController h)
        {
            if (h == null || h == voiceHero) return;
            voiceHero = h;
            hornetVoice.Clear();
#if TESTHARNESS
            Log.LogInfo("[TEST] reading Hornet voice tables");
#endif
            foreach (string name in HornetVoiceFields)
            {
                try { var t = Traverse.Create(h).Field(name).GetValue<RandomAudioClipTable>(); if (t != null) hornetVoice.Add(t);
#if TESTHARNESS
                    Log.LogInfo("[TEST] voice table " + name + " = " + (t != null ? t.name : "null"));
#endif
                }
                catch (Exception e) { Log.LogWarning("Hornet voice " + name + ": " + e.Message); }
            }
        }
        static bool Muted(RandomAudioClipTable t) { return t != null && Ready && hornetVoice.Contains(t); }   // also while hurt, dying or respawning, when she has no control
        static bool MuteClipPrefix(RandomAudioClipTable __instance, ref AudioClip __result)
        {
            if (!Muted(__instance)) return true;
            __result = null;
            return false;
        }
        static bool MutePlayPrefix(RandomAudioClipTable __instance) { return !Muted(__instance); }

        // Silk skills: Silksong reports every attack tool it uses; a skill also does its Celeste move (MadelineController.UseSilkSkill).
        static void OnAttackToolUsed(AttackToolBinding binding)
        {
            Achievements.UsedNonDash();
            try
            {
                if (!Ready || Controller == null) return;
                ToolItem tool = ToolItemManager.GetBoundAttackTool(binding, ToolEquippedReadSource.Active);
#if TESTHARNESS
                Log.LogInfo("[TEST] attack tool used " + binding + ": " + (tool != null ? tool.name + " (" + tool.Type + ")" : "none"));
#endif
                if (tool != null && tool.Type == ToolItemType.Skill) Controller.UseSilkSkill(tool.name);
            }
            catch (Exception e) { Log.LogWarning("Silk skill: " + e.Message); }
        }

        // hooks.boss_title: Silksong's DisplayBossTitle (a PlayMaker action) names the boss as the fight starts.
        static void BossTitlePostfix(object __instance)
        {
            try { BossCard.OnBossTitle(Traverse.Create(__instance).Field("bossTitle").Property("Value").GetValue<string>()); }
            catch (Exception e) { Log.LogWarning("Boss title: " + e.Message); }
        }

        // hooks.controller_layout: Silkeste's default controller layout (user 2026-10-11): R1 dashes (Attack) and
        // Square uses tools (Quick Cast), swapped from Silksong's; the rest stays Silksong's. Any default Silksong
        // builds (first run, a new controller type, Reset to defaults) gets it; the player's own rebinds are kept.
        static void ControllerLayoutPostfix(GameSettings __instance)
        {
            __instance.controllerMapping.attack = InControl.InputControlType.RightBumper;
            __instance.controllerMapping.quickCast = InControl.InputControlType.Action3;
        }
        static KeyValuePair<string, string> P(string kind, string method) { return new KeyValuePair<string, string>(kind, method); }

        static void ApplyHooks(Harmony harmony)
        {
            foreach (HookRow row in Hooks.All)
            {
                if (row.Patch != "prefix" && row.Patch != "postfix" && row.Patch != "transpiler") continue;
                // Type.Member, or Type.Member(ParamType,...) to pick one overload.
                string[] parts = row.Target.Split(new[] { '.' }, 2);
                Type[] sig = null;
                int paren = parts[1].IndexOf('(');
                if (paren >= 0)
                {
                    sig = Array.ConvertAll(parts[1].Substring(paren + 1).TrimEnd(')').Split(','), AccessTools.TypeByName);
                    parts[1] = parts[1].Substring(0, paren);
                }
                MethodInfo target = AccessTools.Method(AccessTools.TypeByName(parts[0]), parts[1], sig);
                if (row.Patch == "transpiler" && target != null) target = AccessTools.EnumeratorMoveNext(target);   // coroutine body
                KeyValuePair<string, string> patch;
                if (!PatchFor.TryGetValue(row.Id, out patch))
                {
                    // hazard_damage shares enemy_contact_damage's TakeDamage prefix.
                    if (row.Target == Hooks.EnemyContactDamage.Target && row.Patch == "prefix") { Log.LogInfo("hook " + row.Id + ": shares " + Hooks.EnemyContactDamage.Id); continue; }
                    Log.LogError("hook " + row.Id + " has no patch in code"); continue;
                }
                if (target == null) { Log.LogError("hook " + row.Id + ": " + row.Target + " not found in this Silksong version"); continue; }
                var hm = new HarmonyMethod(typeof(Plugin).GetMethod(patch.Value, BindingFlags.Static | BindingFlags.NonPublic));
                // Another mod on the same Hornet code: our prefixes decide first, our postfixes have the last word.
                hm.priority = patch.Key == "Prefix" ? Priority.First : patch.Key == "Transpiler" ? Priority.Normal : Priority.Last;
                ourTargets.Add(target);
                if (patch.Key == "Prefix") harmony.Patch(target, prefix: hm);
                else if (patch.Key == "Transpiler") harmony.Patch(target, transpiler: hm);
                else harmony.Patch(target, postfix: hm);
                Log.LogInfo("hook " + row.Id + ": " + patch.Key.ToLower() + " on " + row.Target);
            }
        }

        static void FixedUpdatePostfix(HeroController __instance)
        {
            if (!Ready || __instance != HeroController.SilentInstance) return;
            try { Controller.Step(__instance, Time.fixedDeltaTime); }
            catch (Exception e) { Log.LogError("Madeline step: " + e); }
        }

        // Hornet's own jump, dash, wall moves, attack, nail charge and hard landing are off while Madeline drives.
        static bool BlockWhileActive(ref bool __result)
        {
            if (!Controller.Active) return true;
            __result = false;
            return false;
        }

        // Silksong's own death fade starts part-way into Celeste's wipe (hidden under it), whatever its own wait was.
        static void DelayFadePrefix(ref float waitTime)
        {
            if (Ready) waitTime = CelesteWipe.FadeStart;
        }

        // Silksong's own enterSkip: no walk-in, control (and Celeste momentum) right at the room edge.
        static void SkipWalkInPrefix(ref bool enterSkip)
        {
            if (Ready) enterSkip = true;
        }

        // hooks.hornet_voice: the only caller is Hornet's own dialogue line, so she babbles like Celeste's textbox instead.
        static readonly AccessTools.FieldRef<AudioSource> currentVoiceSource =
            AccessTools.StaticFieldRefAccess<AudioSource>(AccessTools.Field(typeof(NPCSpeakingAudio), "_currentPlayingSource"));
        static bool HornetVoicePrefix()
        {
            if (!Ready) return true;
            AudioSource playing = currentVoiceSource();
            if (playing != null) playing.Stop();   // as Silksong does before any new voice line
            Sfx.PlayVoice(voice);
            return false;
        }

        // hooks.updraft_speed: remember the lift of the updraft she just entered, for the glide.
        static void UpdraftPostfix(float speed)
        {
            MadelineController.UpdraftSpeed = speed;
        }

        // hooks.npc_voice: an NPC's line cuts Madeline off, as it would Hornet.
        static void NpcVoicePrefix()
        {
            Sfx.StopVoice();
        }

        static void DelayHazardFadePrefix(ref float waitTime)
        {
            if (Ready) waitTime = CelesteWipe.HazardFadeStart;
        }

        // visuals.hazard_reload_wait replaces the 0.65 s literal in PlayerDeadFromHazard's coroutine.
        static IEnumerable<CodeInstruction> HazardReloadTranspiler(IEnumerable<CodeInstruction> code)
        {
            int replaced = 0;
            foreach (CodeInstruction ci in code)
            {
                if (ci.opcode == System.Reflection.Emit.OpCodes.Ldc_R4 && ci.operand is float && Math.Abs((float)ci.operand - 0.65f) < 0.0001f) { ci.operand = Visuals.HazardReloadWait; replaced++; }
                yield return ci;
            }
            if (replaced != 1) Log.LogWarning("shorten_hazard_reload: expected one 0.65 wait, found " + replaced);
        }

        // dash_attack.enemy_damage_immune_during_dash / hazard_damage_during_dash
        static bool TakeDamagePrefix(HeroController __instance, GameObject go, HazardType hazardType)
        {
            if (Controller.TryPogo(hazardType, go, __instance)) return false;   // dash_attack.spike_pogo
            // assist.invincibility: nothing hurts, but a bottomless pit still sends her back to solid ground.
            if (AssistMode.Invincible && hazardType != HazardType.PIT && hazardType != HazardType.RESPAWN_PIT) return false;
            bool enemyHit = hazardType == HazardType.ENEMY || hazardType == HazardType.NON_HAZARD || hazardType == HazardType.EXPLOSION;
            if (Controller.EnemyImmune && enemyHit) { Achievements.Dodged(go); return false; }   // achievements: Untouchable
            // assist.one_hit: an enemy's hit is a Celeste death: back to the last safe spot, no mask lost
            if (AssistMode.OneHit && enemyHit && __instance.CanTakeDamage() && !__instance.cState.dead && !__instance.cState.hazardDeath)
            {
                try { OneHitMode.UseEntrance(__instance); __instance.StartCoroutine((System.Collections.IEnumerator)OneHitDeath.Invoke(__instance, new object[] { HazardType.SPIKES, 0f })); return false; }
                catch (Exception e) { Log.LogWarning("One-hit: " + e.Message); }
            }
            return true;
        }
        static readonly MethodInfo OneHitDeath = AccessTools.Method(typeof(HeroController), "DieFromHazard");
    }
}
