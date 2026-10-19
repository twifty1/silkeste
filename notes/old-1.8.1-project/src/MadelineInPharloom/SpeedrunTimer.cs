using System;
using System.Globalization;
using System.IO;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Celeste's SpeedrunTimerDisplay (visuals.timer_*): the save file's play time (Silksong's own GameManager.PlayTime)
    /// big, the time in this room small under it, on Celeste's strawberryCountBG strip at the top left, and the golden
    /// berries of this save beside it. F7 (inputs.speedrun_timer) turns it on or off; kept in timer.cfg.
    /// </summary>
    public class SpeedrunTimer : MonoBehaviour
    {
        public static GuiArt Art;
        public static string DataDir;

        // assist.timer (the F5 settings menu, or F7): Off, Save, Room, Save + Room, All saves, All saves + Room.
        // "All saves" and "Save" never show together (user 2026-10-12).
        static int mode { get { return AssistMode.TimerMode; } }
        bool on { get { return mode != 0; } }
        float modeShown;

        // "All saves": this save's timer plus the play time Silksong records for every other save slot (the numbers its
        // save-select screen shows, GameManager.GetSaveStatsForSlot), read once each time a save is entered.
        readonly System.Collections.Generic.Dictionary<int, float> otherSaves = new System.Collections.Generic.Dictionary<int, float>();
        float AllSaves()
        {
            float sum = fileTime;
            foreach (var kv in otherSaves) if (kv.Key != slot) sum += kv.Value;
            return sum;
        }

        void ReadOtherSaves(GameManager gm)
        {
            otherSaves.Clear();
            for (int s = 1; s <= Visuals.TimerSaveSlots; s++)
            {
                if (s == gm.profileID) continue;
                int sl = s;
                try { gm.GetSaveStatsForSlot(sl, (stats, error) => { if (stats != null) otherSaves[sl] = stats.PlayTime; }); }
                catch (Exception e) { Plugin.Log.LogWarning("Timer: save slot " + sl + ": " + e.Message); }
            }
        }
        string room;
        float roomTime;

        string SaveFile { get { return Path.Combine(DataDir, "timer.cfg"); } }

        // timer_times.cfg: slot=seconds of controllable play. A save seen for the first time starts from Silksong's play time.
        readonly System.Collections.Generic.Dictionary<int, float> times = new System.Collections.Generic.Dictionary<int, float>();
        bool timesLoaded;
        int slot = -1;
        float fileTime, saveIn = 5f;
        string TimesFile { get { return Path.Combine(DataDir, "timer_times.cfg"); } }

        void LoadTimes(int profile, float playTime)
        {
            if (!timesLoaded)
            {
                timesLoaded = true;
                try
                {
                    if (File.Exists(TimesFile))
                        foreach (string line in File.ReadAllLines(TimesFile))
                        {
                            string[] kv = line.Split('=');
                            int sl; float v;
                            if (kv.Length == 2 && int.TryParse(kv[0], out sl) && float.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) times[sl] = v;
                        }
                }
                catch (Exception e) { Plugin.Log.LogWarning("Timer: " + e.Message); }
            }
            if (profile == slot) return;
            if (slot >= 0) { times[slot] = fileTime; SaveTimes(); }
            slot = profile;
            GameManager gm = GameManager.SilentInstance;
            if (gm != null) ReadOtherSaves(gm);
            float t;
            // Only moments she can move count, so a real save's time is never above Silksong's own play time: more
            // means the slot now holds a different (new) save, which starts from Silksong's time.
            fileTime = times.TryGetValue(slot, out t) && t <= playTime + 1f ? t : playTime;
        }

        void OnApplicationQuit() { SaveTimes(); }

        void SaveTimes()
        {
            if (slot < 0) return;
            times[slot] = fileTime;
            try
            {
                var lines = new System.Collections.Generic.List<string>();
                foreach (var kv in times) lines.Add(kv.Key + "=" + kv.Value.ToString("R", CultureInfo.InvariantCulture));
                Directory.CreateDirectory(DataDir);
                File.WriteAllLines(TimesFile, lines.ToArray());
            }
            catch (Exception e) { Plugin.Log.LogWarning("Timer: " + e.Message); }
        }

#if TESTHARNESS
        public static SpeedrunTimer Instance;
        void Awake() { Instance = this; }
        public void TestShow(bool show) { AssistMode.TestSet("timer", show ? 3 : 0); }
#endif

        bool loggedError;
        void Update()
        {
            try { Tick(); }
            catch (Exception e) { if (!loggedError) { loggedError = true; Plugin.Log.LogError("SpeedrunTimer: " + e); } }
        }

        void Tick()
        {
            GameManager gm = GameManager.SilentInstance;
            HeroController h = HeroController.SilentInstance;
            if (!Plugin.Ready || gm == null || h == null || gm.IsMenuScene())
            {
                // back at the menu: the next load may be another save, even in the same slot
                if (slot >= 0) { SaveTimes(); slot = -1; }
                return;
            }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (AssistMode.KeyPressed(AssistOptions.KeyTimer))
            {
                AssistMode.NextTimerMode();
                Sfx.Play(on ? Sounds.MenuToggleOn.Id : Sounds.MenuToggleOff.Id);
                modeShown = 1.5f;
            }
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (scene != room) { room = scene; roomTime = 0f; }
            // visuals.timer: only time she can actually move counts (Plugin.Controller.Active: not postcards, boss cards,
            // Assist Mode, cutscenes, benches, transitions or pauses). Real time, so Assist Mode's slower speed shows.
            LoadTimes(gm.profileID, gm.PlayTime);
            if (modeShown > 0f) modeShown -= Time.unscaledDeltaTime;
            // Time.timeScale > 0: the settings menu stops the game's clock (and FixedUpdate, so Controller.Active lags)
            if (Plugin.Controller.Active && !h.controlReqlinquished && Time.timeScale > 0f && gm.GameState == GlobalEnums.GameState.PLAYING && !gm.isPaused)
            {
                roomTime += Time.unscaledDeltaTime;
                fileTime += Time.unscaledDeltaTime;
                if ((saveIn -= Time.unscaledDeltaTime) <= 0f) { saveIn = 5f; SaveTimes(); }
            }
        }

        // Celeste.SpeedrunTimerDisplay.Render, in Celeste's 1920x1080 GUI units.
        float vis = 1f;   // 0 hidden .. 1 shown, following the HUD
        void OnGUI()
        {
            if (TheoCamera.Hiding || TheoCamera.HidingHud) return;   // out of Theo's photos
            if ((!on && modeShown <= 0f) || Art == null || Event.current.type != EventType.Repaint) return;
            // with the health bar (user 2026-10-13): it slides away when Silksong's HUD does (conversations,
            // transitions, cutscenes) and glides back in with it, instead of popping
            bool hudUp = true;
            try { hudUp = GameCameras.instance == null || GameCameras.instance.IsHudVisible; } catch (Exception) { }
            // room transitions (user 2026-10-13): it fades out with the screen and comes back in the new room
            bool moving = false;
            try
            {
                var hc = HeroController.SilentInstance; var g = GameManager.SilentInstance;
                moving = (hc != null && hc.cState.transitioning) || (g != null && g.IsInSceneTransition);
            }
            catch (Exception) { }
            float want = hudUp && !CelesteTextbox.Showing && !moving ? 1f : 0f;
            vis = Mathf.MoveTowards(vis, want, Time.unscaledDeltaTime / (want > vis ? 0.35f : 0.2f));
            if (vis <= 0f && modeShown <= 0f) return;
            GameManager gm = GameManager.SilentInstance;
            if (!Plugin.Ready || gm == null || HeroController.SilentInstance == null || gm.IsMenuScene()) return;
            if (!PostcardView.TutorialDone) return;   // after the Tutorial card of this load (user 2026-10-12)
            // not over menus: pause, inventory, the settings and the records (user 2026-10-12)
            if (gm.isPaused || AssistMode.IsOpen || BossCard.RecordsOpen || (PlayerData.instance != null && PlayerData.instance.isInventoryOpen)) return;
            float s = Screen.height / 1080f;
            Matrix4x4 old = GUI.matrix;
            GUI.depth = 2;
            float ve = 1f - (1f - vis) * (1f - vis) * (1f - vis);   // eased like the HUD's slide
            GUI.matrix = Matrix4x4.TRS(new Vector3(-(1f - ve) * 340f * s, 0f, 0f), Quaternion.identity, new Vector3(s, s, 1f));
            float y = Visuals.TimerTop;
            Color wh = new Color(1f, 1f, 1f, ve), bk = new Color(0f, 0f, 0f, ve);
            float small0 = Visuals.TimerScale * 0.6f;
            // after F7, the mode's name for a moment above the strip ("Timer: Off" too)
            if (modeShown > 0f)
            {
                float ma = Mathf.Clamp01(modeShown);
                Art.DrawOutlined("Timer: " + AssistMode.TimerModeName, 32f, y - 8f - Art.LineHeight * small0, small0, new Color(1f, 1f, 1f, ma), new Color(0f, 0f, 0f, ma), 2f);
            }
            if (!on) { GUI.matrix = old; GUI.color = Color.white; return; }
            bool showRoom = mode == 3 || mode == 5, all = mode >= 4;
            // the big line: this save, all saves, or (Room alone) the room; the small line under it: the room
            string file = mode == 2 ? BossCard.FormatTime(roomTime) : BossCard.FormatTime(all ? AllSaves() : fileTime);
            string roomText = showRoom ? BossCard.FormatTime(roomTime) : null;
            float big = Visuals.TimerScale, small = big * 0.6f;
            GuiArt.Img bg = Art.Image("strawberryCountBG");
            float barW = Math.Max(300f, Art.Measure(file) * big + 60f);
            if (bg != null)
            {
                GuiArt.Draw(bg, new Rect(barW - bg.W, y, bg.W, bg.H), wh);
                GUI.color = bk;
                if (barW > bg.W) GUI.DrawTexture(new Rect(0f, y, barW - bg.W + 1f, bg.H), Texture2D.whiteTexture);
                if (roomText != null) GuiArt.Draw(bg, new Rect(barW - bg.W, y + bg.H, bg.W * 0.75f, bg.H * 0.85f), wh);
            }
            Art.DrawOutlined(file, 32f, y + 19f - Art.LineHeight * big / 2f, big, wh, bk, 2f);
            if (roomText != null) Art.DrawOutlined(roomText, 32f, y + 38f + 16f - Art.LineHeight * small / 2f, small, new Color(0.8f, 0.8f, 0.8f, ve), bk, 2f);
            int goldens = BossCard.GoldenCount(gm.profileID);
            GuiArt.Img berry = BossCard.PixelBerry() ?? Art.Image("collectables/goldberry");
            if (goldens > 0 && berry != null && AssistMode.TimerBerries)   // assist.timer_berries
            {
                float bx = 22f, bs = 36f, by = y - bs - 2f;   // just above the time (user 2026-10-12)
                GuiArt.Draw(berry, new Rect(bx, by, bs, bs), wh);
                string n = "x" + goldens.ToString(CultureInfo.InvariantCulture);
                Art.DrawOutlined(n, bx + bs + 1f, by + bs / 2f - Art.LineHeight * small * 0.8f / 2f, small * 0.8f, wh, bk, 2f);
            }
            GUI.matrix = old;
            GUI.color = Color.white;
        }
    }
}
