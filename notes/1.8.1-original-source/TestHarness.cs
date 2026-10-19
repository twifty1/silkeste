#if TESTHARNESS
using System;
using System.Collections;
using System.IO;
using System.Linq;
using GlobalEnums;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MadelineInPharloom
{
    /// <summary>
    /// Test build only (-p:DefineConstants=TESTHARNESS): loads a save, plays a scripted move list through the
    /// controller's input, logs the results and saves in-game screenshots. Never part of a release.
    /// </summary>
    public class TestHarness : MonoBehaviour
    {
        public static bool Driving;
        public static string ConfigFile;
        public static int MoveX, MoveY; public static Vector2 Aim; public static bool JumpPressed, JumpHeld, DashPressed, GrabHeld;
        static readonly AccessTools.FieldRef<HeroController, Rigidbody2D> Rb = AccessTools.FieldRefAccess<HeroController, Rigidbody2D>("rb2d");
        string outDir;
        int slot = 1;
        string script = "all";

        void Awake() { L("harness awake"); }

        void Start()
        {
            try { StartHarness(); }
            catch (Exception e) { L("harness failed to start: " + e); }
        }

        float menuWait;
        void Update()
        {
            // a heartbeat while waiting for the main menu, so a stuck run shows up in the log
            if (GameManager.instance != null && (menuWait += Time.unscaledDeltaTime) > 10f) { menuWait = -1e9f; L("game state " + GameManager.instance.GameState); }
        }

        void StartHarness()
        {
            L("harness start");
            string cfg = ConfigFile ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MadelineInPharloom/test.cfg");
            foreach (string line in File.ReadAllLines(cfg))
            {
                string[] kv = line.Split('=');
                if (kv.Length != 2) continue;
                if (kv[0].Trim() == "slot") slot = int.Parse(kv[1].Trim());
                if (kv[0].Trim() == "script") script = kv[1].Trim();
            }
            if (script == "off") { L("harness off"); enabled = false; return; }   // a leftover test.cfg that can't be deleted
            outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MadelineInPharloom/test");
            Directory.CreateDirectory(outDir);
            StartCoroutine(Run());
        }

        static void L(string s) { Plugin.Log.LogInfo("[TEST] " + s); }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, name + ".png"));
            L("screenshot " + name);
        }

        void Release() { MoveX = MoveY = 0; Aim = Vector2.zero; JumpPressed = JumpHeld = DashPressed = GrabHeld = false; }

        string Status()
        {
            var c = Plugin.Controller; var h = HeroController.instance;
            return string.Format("pos=({0:F2},{1:F2}) state={2} speed=({3:F0},{4:F0}) ground={5} dashes={6} stamina={7:F0} ducking={8} active={9} hp={10}",
                h.transform.position.x, h.transform.position.y, c.State, c.Speed.x, c.Speed.y, c.OnGround, c.Dashes, c.Stamina, c.Ducking, c.Active, PlayerData.instance.health);
        }

        IEnumerator Run()
        {
            float t = 0;
            while (GameManager.instance == null || GameManager.instance.GameState != GameState.MAIN_MENU) { yield return null; t += Time.unscaledDeltaTime; if (t > 120) { L("never reached main menu"); yield break; } }
            yield return new WaitForSecondsRealtime(4f);
            L("loading save slot " + slot);
            GameManager.instance.LoadGameFromUI(slot);
            if (script == "cat")
            {
                // the skin resting on the save's bench, before anything gets her up
                for (t = 0; t < 120f && (HeroController.instance == null || PlayerData.instance == null || !PlayerData.instance.atBench || !Plugin.Ready); t += Time.unscaledDeltaTime) yield return null;
                AssistMode.SetSkin("Knight", false);
                yield return new WaitForSecondsRealtime(2.5f);
                yield return Shot("cat_00_bench");
                yield return new WaitForSecondsRealtime(12f);
                yield return Shot("cat_00_bench_sleep");
            }
            t = 0;
            while (HeroController.instance == null || !Plugin.Controller.Active) { yield return null; t += Time.unscaledDeltaTime; if (t > 600) { L("hero never became controllable"); yield break; } }
            yield return new WaitForSeconds(2f);
            var h = HeroController.instance;
            L("scene " + SceneManager.GetActiveScene().name + " " + Status());
            var enemies = FindObjectsByType<HealthManager>(FindObjectsSortMode.None).Where(e => !e.GetIsDead() && e.isActiveAndEnabled)
                .OrderBy(e => Vector2.Distance(e.transform.position, h.transform.position)).Take(8).ToList();
            foreach (var e in enemies) L(string.Format("enemy {0} at ({1:F1},{2:F1}) hp={3} dist={4:F1}", e.name, e.transform.position.x, e.transform.position.y, e.hp, Vector2.Distance(e.transform.position, h.transform.position)));
            yield return Shot("01_spawn");
            var mr = AccessTools.FieldRefAccess<HeroController, MeshRenderer>("renderer")(h);
            L("colorSpace=" + QualitySettings.activeColorSpace + " heroShader=" + (mr.sharedMaterial ? mr.sharedMaterial.shader.name : "none")
              + " sortingLayer=" + mr.sortingLayerName + "/" + mr.sortingOrder + " camera=" + string.Join(",", Camera.main.GetComponents<Component>().Select(c => c.GetType().Name).ToArray()));
            Driving = true;

            if (script == "all" || script == "moves")
            {
                L("idle " + Status());
                MoveX = 1; yield return new WaitForSeconds(0.6f); L("run right " + Status());
                yield return Shot("02_run");
                JumpPressed = true; JumpHeld = true; yield return null; JumpPressed = false;
                yield return new WaitForSeconds(0.25f); L("jump rising " + Status());
                yield return Shot("03_jump");
                JumpHeld = false; Aim = new Vector2(0.7071f, -0.7071f); DashPressed = true; yield return null; DashPressed = false;
                yield return new WaitForSeconds(0.06f); L("dash up-right " + Status());
                yield return Shot("04_dash_upright");
                Release();
                for (t = 0; t < 4f && !(Plugin.Controller.OnGround && Plugin.Controller.Dashes > 0); t += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(0.3f);
                L("landed " + Status());

                // super: horizontal ground dash, jump during it (expect speed.x 260, speed.y -105)
                Aim = new Vector2(1, 0); DashPressed = true; yield return null; DashPressed = false;
                yield return new WaitForSeconds(0.06f);
                JumpPressed = true; JumpHeld = true; yield return null; JumpPressed = false; yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                L("super " + Status());
                Release();
                for (t = 0; t < 4f && !(Plugin.Controller.OnGround && Plugin.Controller.Dashes > 0); t += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(0.3f);

                // hyper: down-diagonal dash on the ground (becomes a ducking dash), then jump (expect 325, -52.5)
                MoveX = -1; MoveY = 1; Aim = new Vector2(-0.7071f, 0.7071f); DashPressed = true; yield return null; DashPressed = false;
                yield return new WaitForSeconds(0.06f); L("ground down-left dash " + Status());
                MoveY = 0; JumpPressed = true; yield return null; JumpPressed = false; yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                L("hyper " + Status());
                yield return Shot("05_hyper");
                Release();
                for (t = 0; t < 4f && !(Plugin.Controller.OnGround && Plugin.Controller.Dashes > 0); t += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(0.3f);
                L("after hyper " + Status());

                // climb: find a wall to the side, run to it, grab and climb, then climb-jump
                int wallDir = 0;
                Bounds hb = h.GetComponent<Collider2D>().bounds;
                for (int dir = 1; dir >= -1 && wallDir == 0; dir -= 2)
                {
                    RaycastHit2D wall = Physics2D.Raycast(hb.center, new Vector2(dir, 0), 12f, 33562880);
                    if (wall.collider != null && !wall.collider.isTrigger) { wallDir = dir; L("wall at " + wall.distance.ToString("F1") + " to the " + (dir > 0 ? "right" : "left")); }
                }
                if (wallDir != 0)
                {
                    MoveX = wallDir;
                    for (t = 0; t < 3f && Plugin.Controller.Speed.x != 0f || t < 0.3f; t += Time.deltaTime) yield return null;
                    JumpPressed = true; JumpHeld = true; yield return null; JumpPressed = false;
                    yield return new WaitForSeconds(0.15f);
                    GrabHeld = true; MoveY = -1;
                    yield return new WaitForSeconds(0.6f);
                    L("climbing " + Status());
                    yield return Shot("07_climb");
                    yield return new WaitForSeconds(0.6f);
                    L("climbing longer " + Status());
                    MoveY = 0; JumpPressed = true; yield return null; JumpPressed = false; yield return new WaitForFixedUpdate();
                    L("climb jump " + Status());
                    GrabHeld = false; JumpHeld = false; MoveX = -wallDir;
                    yield return new WaitForSeconds(0.1f);
                    Release();
                    for (t = 0; t < 4f && !Plugin.Controller.OnGround; t += Time.deltaTime) yield return null;
                    L("after climb " + Status());
                }

                // 8 directions
                Vector2[] dirs = { new Vector2(1,0), new Vector2(-1,0), new Vector2(0,-1), new Vector2(0.7071f,-0.7071f), new Vector2(-0.7071f,-0.7071f) };
                foreach (var d in dirs)
                {
                    yield return new WaitForSeconds(0.6f);
                    Aim = d; DashPressed = true; yield return null; DashPressed = false;
                    yield return new WaitForSeconds(0.05f); L("dash " + d + " " + Status());
                    Release();
                }
                yield return new WaitForSeconds(1.5f);
            }

            if (script == "climb")
            {
                // grab a wall and hold still: expect state=1, y steady, stamina falling 10/s; then climb up 45/s
                int wallDir = 0; float wallDist = 0;
                Bounds hb = h.GetComponent<Collider2D>().bounds;
                for (int dir = 1; dir >= -1 && wallDir == 0; dir -= 2)
                {
                    RaycastHit2D wall = Physics2D.Raycast(hb.center, new Vector2(dir, 0), 15f, 33562880);
                    if (wall.collider != null && !wall.collider.isTrigger) { wallDir = dir; wallDist = wall.distance; }
                }
                L("wall dir=" + wallDir + " dist=" + wallDist.ToString("F2"));
                if (wallDir != 0)
                {
                    MoveX = wallDir;
                    for (t = 0; t < 5f; t += Time.deltaTime) { if (t > 0.3f && Math.Abs(Plugin.Controller.Speed.x) < 1f) break; yield return null; }
                    MoveX = 0;
                    JumpPressed = true; JumpHeld = true; yield return null; JumpPressed = false;
                    yield return new WaitForSeconds(0.2f);
                    MoveX = wallDir; GrabHeld = true; JumpHeld = false;
                    yield return new WaitForSeconds(0.2f);
                    MoveX = 0;
                    for (int i = 0; i < 4; i++) { L("hold " + i + "s " + Status()); yield return new WaitForSeconds(1f); }
                    yield return Shot("07_climb");
                    MoveY = -1;
                    for (int i = 0; i < 3; i++) { yield return new WaitForSeconds(0.5f); L("climb up " + Status()); }
                    MoveY = 0;
                    for (t = 0; t < 12f && Plugin.Controller.State == 1; t += 1f) { yield return new WaitForSeconds(1f); L("hold " + Status()); }
                    L("let go by stamina? " + Status());
                    Release();
                }
            }

            if ((script == "all" || script == "attack") && enemies.Count > 0)
            {
                var target = enemies.FirstOrDefault(e => e.hp > 0);
                Vector2 ep = target.transform.position;
                int side = ep.x > h.transform.position.x ? -1 : 1;
                Rb(h).position = new Vector2(ep.x + side * 2.2f, ep.y + 0.2f);
                yield return new WaitForSeconds(0.4f);
                int before = target.hp;
                Plugin.Controller.Dashes = 1;
                L(string.Format("attack {0} hp={1} from side {2} {3}", target.name, before, side, Status()));
                Aim = new Vector2(-side, 0); MoveX = -side; DashPressed = true; yield return null; DashPressed = false;
                yield return new WaitForSeconds(0.07f);
                yield return Shot("06_dash_attack");
                yield return new WaitForSeconds(0.1f);
                L(string.Format("after dash: {0} hp {1} -> {2} immune={3} {4}", target.name, before, target.hp, Plugin.Controller.EnemyImmune, Status()));
                Release();
                yield return new WaitForSeconds(1f);
                L("settled " + Status());
            }
            if (script == "all" || script == "pogo")
            {
                // spike pogo: drop onto the nearest spikes with a down dash; expect a bounce, no damage
                h = HeroController.instance;
                for (t = 0; t < 10f && !Plugin.Controller.Active; t += Time.deltaTime) yield return null;
                var spikes = FindObjectsByType<DamageHero>(FindObjectsSortMode.None)
                    .Where(d => d.hazardType == HazardType.SPIKES && d.isActiveAndEnabled && d.GetComponent<Collider2D>() != null)
                    .OrderBy(d => Vector2.Distance(d.transform.position, h.transform.position)).FirstOrDefault();
                if (spikes == null) L("no spikes in this scene");
                else
                {
                    Bounds sb = spikes.GetComponent<Collider2D>().bounds;
                    L(string.Format("spikes {0} at ({1:F1},{2:F1}) size ({3:F1},{4:F1})", spikes.name, sb.center.x, sb.max.y, sb.size.x, sb.size.y));
                  foreach (Vector2 pogoAim in new[] { new Vector2(0, 1), new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f) })
                  {
                    int hpBefore = PlayerData.instance.health;
                    for (t = 0; t < 10f && !Plugin.Controller.Active; t += Time.deltaTime) yield return null;
                    Rb(h).position = new Vector2(sb.center.x - pogoAim.x * 2f, sb.max.y + 2.2f);
                    Plugin.Controller.Speed = Vector2.zero; Plugin.Controller.Dashes = 1;
                    yield return new WaitForSeconds(0.15f);
                    Aim = pogoAim; MoveY = 1; DashPressed = true; JumpHeld = true; yield return null; DashPressed = false;
                    float maxUp = 0; bool bounced = false;
                    for (t = 0; t < 0.8f; t += Time.deltaTime)
                    {
                        if (Plugin.Controller.Speed.y < -50f) { bounced = true; maxUp = Mathf.Min(maxUp, Plugin.Controller.Speed.y); }
                        if (bounced && t > 0.1f && maxUp < -100f) break;
                        yield return null;
                    }
                    yield return Shot("08_pogo_" + pogoAim.x.ToString("F1"));
                    L(string.Format("pogo aim={0} bounced={1} speedY={2:F0} hp {3} -> {4} {5}", pogoAim, bounced, maxUp, hpBefore, PlayerData.instance.health, Status()));
                    Release();
                    yield return new WaitForSeconds(1.5f);
                  }
                }
            }
            if (script == "assist")
            {
                Driving = false;
                for (t = 0; t < 4f && (PostcardView.Instance == null || !PostcardView.Instance.TestShowing); t += Time.unscaledDeltaTime) yield return null;
                yield return new WaitForSecondsRealtime(1.6f);
                if (PostcardView.Instance != null) PostcardView.Instance.TestDismiss();
                yield return new WaitForSecondsRealtime(1.5f);
                L("titles: " + BossCard.TestTitles());

                // assist.game_speed: Silksong's time scale follows
                L("timeScale default " + Time.timeScale);
                AssistMode.TestSet("game_speed", 0); yield return new WaitForSecondsRealtime(0.3f);
                L("timeScale at 50% " + Time.timeScale);
                AssistMode.TestSet("game_speed", 5); yield return new WaitForSecondsRealtime(0.3f);
                L("timeScale back at 100% " + Time.timeScale);

                // assist.air_dashes
                Driving = true;
                AssistMode.TestSet("air_dashes", 1); yield return new WaitForSeconds(0.2f);
                L("Two: MaxDashes " + Plugin.Controller.MaxDashes);
                AssistMode.TestSet("air_dashes", 2);
                JumpPressed = true; JumpHeld = true; yield return null; JumpPressed = false;
                yield return new WaitForSeconds(0.15f);
                for (int i = 0; i < 3; i++)
                {
                    Aim = new Vector2(i % 2 == 0 ? 0.7071f : -0.7071f, -0.7071f); DashPressed = true; yield return null; DashPressed = false;
                    yield return new WaitForSeconds(0.3f);
                    L("Infinite: air dash " + (i + 1) + " " + Status());
                }
                Release();
                AssistMode.TestSet("air_dashes", 0);
                for (t = 0; t < 4f && !Plugin.Controller.OnGround; t += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(0.5f);

                // assist.invincibility: stand on the nearest spikes
                var spikes = FindObjectsByType<DamageHero>(FindObjectsSortMode.None)
                    .Where(d => d.hazardType == HazardType.SPIKES && d.isActiveAndEnabled && d.GetComponent<Collider2D>() != null)
                    .OrderBy(d => Vector2.Distance(d.transform.position, h.transform.position)).FirstOrDefault();
                if (spikes == null) L("no spikes in this scene");
                else
                {
                    AssistMode.TestSet("invincibility", 1);
                    int hp = PlayerData.instance.health;
                    Bounds sb = spikes.GetComponent<Collider2D>().bounds;
                    Rb(h).position = new Vector2(sb.center.x, sb.max.y + 1.2f);
                    yield return new WaitForSeconds(1.5f);
                    L("invincible on spikes: hp " + hp + " -> " + PlayerData.instance.health + " dead=" + h.cState.dead + " hazardRespawning=" + h.cState.hazardRespawning + " " + Status());
                    AssistMode.TestSet("invincibility", 0);
                    Rb(h).position = new Vector2(sb.center.x + 6f, sb.max.y + 6f);
                    yield return new WaitForSeconds(1.5f);
                }

                // a whole fight through the real code: title, dashes, a win, the card, the golden berry
                BossCard.OnBossTitle("TEST_BOSS");
                for (int i = 0; i < 2; i++) { Aim = new Vector2(i == 0 ? 1 : -1, 0); DashPressed = true; yield return null; DashPressed = false; yield return new WaitForSeconds(0.6f); }
                Release();
                yield return new WaitForSeconds(1f);
                L("fight: " + BossCard.TestFight);
                BossCard.TestDefeat();
                Driving = false;
                for (t = 0; t < 6f && !BossCard.TestShowing; t += Time.unscaledDeltaTime) yield return null;
                yield return new WaitForSecondsRealtime(1.6f);
                yield return Shot("assist_fight_card");
                L("goldens on this slot: " + BossCard.GoldenCount(GameManager.instance.profileID));
                BossCard.TestDismiss();
                for (t = 0; t < 3f && BossCard.TestShowing; t += Time.unscaledDeltaTime) yield return null;
                BossCard.TestForget("TEST_BOSS");
                L("goldens after cleanup: " + BossCard.GoldenCount(GameManager.instance.profileID));

                // assist.dash_assist with real input: hold the left mouse button now
                AssistMode.TestSet("dash_assist", 1);
                L("ready for dash assist");
                bool sawFreeze = false;
                for (t = 0; t < 20f; t += Time.unscaledDeltaTime)
                {
                    if (Time.timeScale == 0f && !sawFreeze) { sawFreeze = true; L("dash assist froze time " + Status()); }
                    if (sawFreeze && Time.timeScale > 0f) { L("dash assist released after " + t.ToString("F2") + " s " + Status()); break; }
                    yield return null;
                }
                if (!sawFreeze) L("dash assist: no freeze seen");
                yield return new WaitForSeconds(0.3f);
                L("after assisted dash " + Status());
                AssistMode.TestSet("dash_assist", 0);
                AssistMode.Instance.TestClose();
            }

            if (script == "golden")
            {
                // the golden berry following her through a hitless fight, then flying off on the first hit
                Driving = false;
                for (t = 0; t < 4f && (PostcardView.Instance == null || !PostcardView.Instance.TestShowing); t += Time.unscaledDeltaTime) yield return null;
                yield return new WaitForSecondsRealtime(1.6f);
                if (PostcardView.Instance != null) PostcardView.Instance.TestDismiss();
                yield return new WaitForSecondsRealtime(1.5f);
                Driving = true;
                BossCard.OnBossTitle("TEST_BOSS");
                L("golden run " + BossCard.GoldenRun);
                yield return new WaitForSeconds(0.8f);
                yield return Shot("golden_01_follow");
                MoveX = 1; yield return new WaitForSeconds(0.5f); MoveX = 0;
                yield return Shot("golden_02_run");
                yield return new WaitForSeconds(0.5f);
                PlayerData.instance.health -= 1;   // a hit, as the fight sees it
                yield return new WaitForSeconds(0.25f);
                L("after hit: golden run " + BossCard.GoldenRun + " fight " + BossCard.TestFight);
                yield return Shot("golden_03_laugh");
                yield return new WaitForSeconds(0.5f);
                yield return Shot("golden_04_flyaway");
                PlayerData.instance.health += 1;
                yield return new WaitForSeconds(1.5f);
                BossCard.TestForget("TEST_BOSS");
                Release();
            }

            if (script == "menu")
            {
                // the settings on the Appearance tab, left open for the player; the shot goes next to the plugin
                Driving = false;
                yield return new WaitForSecondsRealtime(3f);
                if (PostcardView.Instance != null && PostcardView.Instance.TestShowing) { PostcardView.Instance.TestDismiss(); yield return new WaitForSecondsRealtime(1f); }
                AssistMode.Instance.TestOpen(Array.FindIndex(Sheets.AssistOptions.All, r => r.Id == "skin"));
                yield return new WaitForSecondsRealtime(1f);
                outDir = Path.GetDirectoryName(typeof(TestHarness).Assembly.Location);
                yield return Shot("menu_appearance");
                L("menu left open");
                yield break;
            }
            if (script == "ui")
            {
                // 1.4 screens: Tutorial card, speedrun timer, Assist Mode menu, dash assist arrow, boss cards, skins.
                Driving = false;
                for (t = 0; t < 4f && (PostcardView.Instance == null || !PostcardView.Instance.TestShowing); t += Time.unscaledDeltaTime) yield return null;
                yield return new WaitForSecondsRealtime(1.6f);
                yield return Shot("ui_01_tutorial");
                if (PostcardView.Instance != null) PostcardView.Instance.TestDismiss();
                yield return new WaitForSecondsRealtime(1.5f);
                SpeedrunTimer.Instance.TestShow(true);
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("ui_02_timer");
                AssistMode.Instance.TestOpen(0);
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot("ui_03_assist");
                AssistMode.TestSet("game_speed", 2); AssistMode.TestSet("air_dashes", 2); AssistMode.Instance.TestOpen(2);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot("ui_04_assist_changed");
                foreach (var r in Sheets.AssistOptions.All) AssistMode.TestSet(r.Id, Array.IndexOf(r.Values.Split(','), r.Default));
                AssistMode.Instance.TestClose();
                yield return new WaitForSecondsRealtime(0.6f);
                AssistMode.Instance.TestDashFreeze(true);
                yield return new WaitForSecondsRealtime(0.2f);
                yield return Shot("ui_05_dash_arrow");
                AssistMode.Instance.TestDashFreeze(false);
                foreach (bool golden in new[] { true, false })
                {
                    BossCard.TestCard(golden);
                    for (t = 0; t < 5f && !BossCard.TestShowing; t += Time.unscaledDeltaTime) yield return null;
                    yield return new WaitForSecondsRealtime(1.6f);
                    yield return Shot(golden ? "ui_06_card_golden" : "ui_07_card");
                    BossCard.TestDismiss();
                    for (t = 0; t < 3f && BossCard.TestShowing; t += Time.unscaledDeltaTime) yield return null;
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                SpeedrunTimer.Instance.TestShow(false);
                Driving = true;
                for (int i = 0; i < Sheets.Skins.All.Length; i++)
                {
                    Plugin.NextSkinRequested = true;
                    yield return new WaitForSeconds(0.6f);
                    yield return Shot("ui_skin_" + i + "_idle");
                    MoveX = i % 2 == 0 ? 1 : -1; yield return new WaitForSeconds(0.35f);
                    yield return Shot("ui_skin_" + i + "_run");
                    JumpPressed = true; JumpHeld = true; yield return null; JumpPressed = false;
                    yield return new WaitForSeconds(0.2f);
                    yield return Shot("ui_skin_" + i + "_jump");
                    Release();
                    for (t = 0; t < 3f && !Plugin.Controller.OnGround; t += Time.deltaTime) yield return null;
                }
            }
            if (script == "hurt")
            {
                // every sound that plays while she takes an enemy hit, then a spike hit
                for (t = 0; t < 4f && (PostcardView.Instance == null || !PostcardView.Instance.TestShowing); t += Time.unscaledDeltaTime) yield return null;
                yield return new WaitForSecondsRealtime(1.2f);
                if (PostcardView.Instance != null) PostcardView.Instance.TestDismiss();
                yield return new WaitForSecondsRealtime(1.5f);
                var harmony = new HarmonyLib.Harmony("silkeste.test.sounds");
                var log1 = new HarmonyLib.HarmonyMethod(typeof(TestHarness).GetMethod(nameof(LogOneShot), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
                var log2 = new HarmonyLib.HarmonyMethod(typeof(TestHarness).GetMethod(nameof(LogPlay), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
                harmony.Patch(HarmonyLib.AccessTools.Method(typeof(AudioSource), "PlayOneShot", new[] { typeof(AudioClip), typeof(float) }), prefix: log1);
                harmony.Patch(HarmonyLib.AccessTools.Method(typeof(AudioSource), "Play", Type.EmptyTypes), prefix: log2);
                var log3 = new HarmonyLib.HarmonyMethod(typeof(TestHarness).GetMethod(nameof(LogTable), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
                foreach (var m in typeof(RandomAudioClipTableExtensions).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public))
                    if (m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(RandomAudioClipTable)) harmony.Patch(m, prefix: log3);
                var log4 = new HarmonyLib.HarmonyMethod(typeof(TestHarness).GetMethod(nameof(LogSelect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
                harmony.Patch(HarmonyLib.AccessTools.Method(typeof(RandomAudioClipTable), "SelectClip"), prefix: log4);
                HeroController hh = HeroController.instance;
                foreach (HazardType hz in new[] { HazardType.ENEMY, HazardType.SPIKES })
                {
                    L("hurt by " + hz);
                    logSounds = true;
                    try { hh.TakeDamage(hh.gameObject, CollisionSide.top, 1, hz, DamagePropertyFlags.None); } catch (Exception e) { L("TakeDamage failed: " + e.Message); }
                    yield return new WaitForSecondsRealtime(3f);
                    logSounds = false;
                    yield return new WaitForSecondsRealtime(3f);
                }
                harmony.UnpatchSelf();
            }
            if (script == "records")
            {
                // the area card on arrival, then each Records tab
                for (t = 0; t < 4f && (PostcardView.Instance == null || !PostcardView.Instance.TestShowing); t += Time.unscaledDeltaTime) yield return null;
                if (PostcardView.Instance != null) PostcardView.Instance.TestDismiss();
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("rec_00_area_card");
                foreach (int tab in new[] { 0, 1, 2 })
                    foreach (bool all in new[] { false, true })
                    {
                        BossCard.TestView(true, tab, all);
                        yield return new WaitForSecondsRealtime(0.6f);
                        yield return Shot("rec_tab" + tab + (all ? "_all" : "_save"));
                    }
                BossCard.TestView(false, 0, false);
                yield return new WaitForSecondsRealtime(1f);
                TheoCamera.Instance.TestTake();
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shot("rec_cam_polaroid");
                yield return new WaitForSecondsRealtime(3f);
            }
            if (script == "cat")
            {
                // the Cat skin: ears and tail standing, running both ways, jumping, then the skin chosen before
                for (t = 0; t < 4f && (PostcardView.Instance == null || !PostcardView.Instance.TestShowing); t += Time.unscaledDeltaTime) yield return null;
                yield return new WaitForSecondsRealtime(1.2f);
                if (PostcardView.Instance != null) PostcardView.Instance.TestDismiss();
                yield return new WaitForSecondsRealtime(1.5f);
                string before = AssistMode.SkinName;
                AssistMode.SetSkin("Knight", false);
                Driving = true;
                yield return new WaitForSeconds(0.8f);
                yield return Shot("cat_01_idle");
                MoveX = 1; yield return new WaitForSeconds(0.4f);
                yield return Shot("cat_02_run_right");
                MoveX = -1; yield return new WaitForSeconds(0.5f);
                yield return Shot("cat_03_run_left");
                JumpPressed = true; JumpHeld = true; yield return null; JumpPressed = false;
                yield return new WaitForSeconds(0.2f);
                yield return Shot("cat_04_jump");
                Release();
                for (t = 0; t < 3f && !Plugin.Controller.OnGround; t += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(0.5f);
                yield return Shot("cat_05_land");
                AssistMode.SetSkin(before, false);
            }
            Driving = false;
            Release();
            L("done");
        }

        static bool logSounds;
        static void LogOneShot(AudioSource __instance, AudioClip clip) { if (logSounds) Plugin.Log.LogInfo("[TEST] sound oneshot " + (clip ? clip.name : "null") + " on " + __instance.name); }
        static void LogTable(RandomAudioClipTable __0, System.Reflection.MethodBase __originalMethod) { if (logSounds) Plugin.Log.LogInfo("[TEST] table " + (__0 ? __0.name : "null") + " via " + __originalMethod.Name); }
        static void LogSelect(RandomAudioClipTable __instance) { if (logSounds) Plugin.Log.LogInfo("[TEST] select " + __instance.name); }
        static void LogPlay(AudioSource __instance) { if (logSounds) Plugin.Log.LogInfo("[TEST] sound play " + (__instance.clip ? __instance.clip.name : "null") + " on " + __instance.name); }
        static void Unused()
        {
        }
    }
}
#endif
