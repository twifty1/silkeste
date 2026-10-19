using System;
using System.Collections.Generic;
using MadelineInPharloom.Sheets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MadelineInPharloom
{
    /// <summary>
    /// Every other menu in Celeste's style (user 2026-10-14), matching the save file screen: the title menu, Options and
    /// its pages, Achievements, Extras, the prompts and the pause menu. Silksong's menus keep doing all the work (their
    /// buttons, sliders, options, rebinding, navigation); this reads the screen that's open and draws it as Celeste's
    /// menu over the mountain sky (a dim over the game for the pause menu), following Silksong's selection. The mouse
    /// works on the Celeste items. A screen it can't read is left as Silksong draws it.
    /// </summary>
    public class CelesteMenus : MonoBehaviour
    {
        static GuiArt Art { get { return SpeedrunTimer.Art; } }
        /// <summary>A Celeste menu is up: Silksong's own hover sound stays quiet.</summary>
        public static bool Showing;

        class Item { public Selectable Sel; public string Label = "", Value = ""; public bool Arrows; public Rect Rect; public Sprite Icon; }
        CanvasGroup screen;          // the screen being drawn
        string kind = "";            // "title", "pause", "prompt" or "page"
        string heading = "";
        readonly List<Item> items = new List<Item>();
        float refreshIn, alpha, flash;
        GameObject lastSel;
        bool loggedError;

        void Awake()
        {
            Instance = this;
            try
            {
                var h = new HarmonyLib.Harmony("silkeste.menusound");
                var t = typeof(MenuAudioController);
                h.Patch(HarmonyLib.AccessTools.Method(t, "PlaySelect"), prefix: new HarmonyLib.HarmonyMethod(typeof(CelesteMenus), nameof(Quiet)));
                h.Patch(HarmonyLib.AccessTools.Method(t, "PlaySubmit"), prefix: new HarmonyLib.HarmonyMethod(typeof(CelesteMenus), nameof(Submit)));
                h.Patch(HarmonyLib.AccessTools.Method(t, "PlayCancel"), prefix: new HarmonyLib.HarmonyMethod(typeof(CelesteMenus), nameof(Cancel)));
                // the title's Achievements opens the records on their Achievements tab, Extras opens Settings (user 2026-10-14)
                h.Patch(HarmonyLib.AccessTools.Method(typeof(UIManager), "UIGoToAchievementsMenu"), prefix: new HarmonyLib.HarmonyMethod(typeof(CelesteMenus), nameof(ToAchievements)));
                h.Patch(HarmonyLib.AccessTools.Method(typeof(UIManager), "UIGoToExtrasMenu"), prefix: new HarmonyLib.HarmonyMethod(typeof(CelesteMenus), nameof(ToExtras)));
            }
            catch (Exception e) { Plugin.Log.LogWarning("menu sound patch: " + e.Message); }
        }
        // Silksong's menu sounds give way to Celeste's while a Celeste menu is up (user 2026-10-14)
        static bool Up() { return Showing || CelesteFileSelect.Showing || skyOnly; }
        static bool Quiet() { return !Up(); }
        static bool Submit() { if (!Up()) return true; Sfx.Play(Sounds.MenuButtonSelect.Id); return false; }
        static bool ToAchievements() { if (Instance == null || Art == null) return true; Instance.Overlay(); BossCard.OpenAchievementsFromMenu(); return false; }
        static bool ToExtras() { if (Instance == null || Art == null) return true; Instance.Overlay(); AssistMode.OpenFromMenu(); return false; }
        static CelesteMenus Instance;

        float restoreAt, diagUntil, diagIn;
        GameObject beforeOverlay;   // the title's selection, given back when the records or settings close
        void Overlay()
        {
            var es = EventSystem.current;
            // Silksong has already let go of the pressed button by now: the one last shown as chosen here
            beforeOverlay = es != null && es.currentSelectedGameObject != null ? es.currentSelectedGameObject : shownSel;
            if (es != null) es.SetSelectedGameObject(null);   // the title takes no presses while they're open
        }
        static bool OverlayOpen { get { return BossCard.RecordsOpen || AssistMode.IsOpen; } }
        static bool Cancel() { if (!Up()) return true; Sfx.Play(Sounds.MenuUnpause.Id); return false; }

        void LateUpdate()
        {
            try { Tick(); }
            catch (Exception e) { if (!loggedError) { loggedError = true; Plugin.Log.LogError("CelesteMenus: " + e); } Showing = false; screen = null; }
        }

        // the open screen, most specific first (a prompt over a page over the main menu)
        CanvasGroup FindScreen(out string k)
        {
            k = "";
            UIManager ui = UIManager.instance;
            if (ui == null || CelesteFileSelect.Showing) return null;
            MenuScreen[] prompts = { ui.quitGamePrompt, ui.returnMainMenuPrompt, ui.resolutionPrompt };
            foreach (var m in prompts) if (Up(m)) { k = "prompt"; return m.ScreenCanvasGroup; }
            MenuScreen[] pages = { ui.remapGamepadMenuScreen, ui.keyboardMenuScreen, ui.advancedGamepadMenuScreen, ui.gamepadMenuScreen, ui.advancedVideoMenuScreen,
                ui.videoMenuScreen, ui.audioMenuScreen, ui.gameOptionsMenuScreen, ui.overscanMenuScreen, ui.brightnessMenuScreen, ui.achievementsMenuScreen,
                ui.extrasContentMenuScreen, ui.extrasMenuScreen, ui.playModeMenuScreen, ui.optionsMenuScreen };
            foreach (var m in pages) if (Up(m)) { k = "page"; return m.ScreenCanvasGroup; }
            // the pause screen's own look is faded out while drawn here, so it counts as up while the game is paused
            var gmP = GameManager.SilentInstance;
            if (ui.pauseMenuScreen != null && ui.pauseMenuScreen.gameObject.activeInHierarchy && gmP != null && gmP.isPaused && ui.pauseMenuScreen.ScreenCanvasGroup != null && ui.pauseMenuScreen.ScreenCanvasGroup.interactable)
            { k = "pause"; return ui.pauseMenuScreen.ScreenCanvasGroup; }
            if (ui.mainMenuScreen != null && ui.mainMenuScreen.gameObject.activeInHierarchy && ui.mainMenuScreen.alpha > 0.01f) { k = "title"; return ui.mainMenuScreen; }
            return null;
        }
        static bool Up(MenuScreen m) { if (m == null || !m.gameObject.activeInHierarchy) return false; var cg = m.ScreenCanvasGroup; return cg != null && cg.alpha > 0.5f; }

        void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            flash += dt;
            string k = "";
            CanvasGroup cg = Art != null ? FindScreen(out k) : null;
            // just after the save screen (going back to the title): the sky stays up until the title is ready
            if (CelesteFileSelect.Showing) sinceFiles = 0f; else sinceFiles += dt;
            var gmS = GameManager.SilentInstance;
            skyOnly = cg == null && !CelesteFileSelect.Showing && sinceFiles < 1.5f && (gmS == null || gmS.IsMenuScene()) && UIManager.instance != null;
            // the game's first title: the sky covers it from the moment the title scene starts, even before Celeste's
            // art has loaded or Silksong's menu has faded in far enough to count (user 2026-10-14: a glimpse of it)
            if (k == "title") titleSeen = true;
            if (!titleSeen && cg == null && UIManager.instance != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Menu_Title") skyOnly = true;
            if (Art == null)
            {
                // Celeste's art failed to load: after a while Silksong's own menu is better than an empty sky
                if (Time.realtimeSinceStartup > 60f) skyOnly = false;
                HideAll(skyOnly); return;
            }
            var gmA = GameManager.SilentInstance;
            HideAll(skyOnly || cg != null || screen != null || CelesteFileSelect.Showing || sinceFiles < 1.5f || (gmA != null && gmA.isPaused && !gmA.IsMenuScene()));
            if (cg == null) k = "";
            // between two screens (one fading out, the next not ready) keep the sky up for a moment, so Silksong's
            // menu never shows through (user 2026-10-14)
            var gmH = GameManager.SilentInstance;
            // in the menus before the game, or in the game while it's paused (the pause menu and its Options pages)
            holding = cg == null && screen != null && (holdFor += dt) < 1.5f && UIManager.instance != null && (gmH == null || gmH.IsMenuScene() || gmH.isPaused);
            if (holding) return;
            holdFor = 0f;
            if (cg != screen) { RestoreRaycasts(); screen = cg; kind = k; refreshIn = 0f; autoPicked = false; shownSel = null; screenShownAt = Time.unscaledTime; barY = -1000f; barX = -1000f; barW = 0f; smoothScroll = 0f; }
            Showing = screen != null;
            alpha = screen != null ? 1f : Mathf.MoveTowards(alpha, 0f, dt / 0.15f);
            if (screen == null) return;
            // the mouse goes to the Celeste items, not Silksong's hidden ones; Silksong's own art for this screen is
            // shrunk away (its buttons still work: selection doesn't care about size)
            screen.blocksRaycasts = false;
            if ((refreshIn -= dt) <= 0f) { refreshIn = 0.6f; ShowRemap(); Read(); }
            if (items.Count == 0) { Showing = false; return; }

            // follow Silksong's selection; play Celeste's sounds when it moves
            var es = EventSystem.current;
            if (OverlayOpen) { if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null); wasOverlay = true; return; }
            // after the records or settings close, the title waits a moment with nothing selected, so the Circle that
            // closed them reaches nothing (on the title Silksong reads it as "quit?" and locks its input, user 2026-10-14)
            if (wasOverlay) { wasOverlay = false; restoreAt = Time.unscaledTime + 0.3f; }
            if (restoreAt > 0f)
            {
                if (Time.unscaledTime < restoreAt) { if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null); return; }
                restoreAt = 0f;
                GameObject back = beforeOverlay != null && beforeOverlay.activeInHierarchy ? beforeOverlay : (items.Count > 0 && items[0].Sel != null ? items[0].Sel.gameObject : null);
                if (es != null && back != null) es.SetSelectedGameObject(back);
                // Silksong may have switched its menu input off on the way (the controller then can't move): back on
                if (InputHandler.Instance != null) InputHandler.Instance.StartUIInput();
            }
            GameObject sel = es != null ? es.currentSelectedGameObject : null;
            // nothing of this screen selected (coming back from the save files leaves none): the first item, so the
            // arrows work straight away
            // only once, when the screen opens with nothing selected: after a press Silksong lets go of the selection
            // on its way to the next screen, and jumping to the top then looked wrong (user 2026-10-14)
            if (!autoPicked && es != null && (sel == null || items.Find(i => i.Sel != null && i.Sel.gameObject == sel) == null) && items[0].Sel != null)
            {
                es.SetSelectedGameObject(items[0].Sel.gameObject);
                sel = items[0].Sel.gameObject;
            }
            autoPicked = true;
            if (sel != null && items.Find(i => i.Sel != null && i.Sel.gameObject == sel) != null) shownSel = sel;
            if (sel != lastSel && sel != null && lastSel != null) Sfx.Play(Sounds.MenuRolloverDown.Id, 0.8f);
            if (sel != lastSel) flash = 0f;
            lastSel = sel;

            // the mouse: hover selects, a click submits (or steps an option: left half back, right half on)
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null && es != null)
            {
                Vector2 g = MenuMouse.Gui();
                foreach (Item it in items)
                {
                    if (!it.Rect.Contains(g) || it.Sel == null) continue;
                    if (m.delta.ReadValue().sqrMagnitude > 0.5f && sel != it.Sel.gameObject) es.SetSelectedGameObject(it.Sel.gameObject);
                    if (m.leftButton.wasPressedThisFrame)
                    {
                        es.SetSelectedGameObject(it.Sel.gameObject);
                        if (it.Arrows)
                        {
                            var ad = new AxisEventData(es) { moveDir = g.x < it.Rect.center.x ? MoveDirection.Left : MoveDirection.Right };
                            ExecuteEvents.Execute(it.Sel.gameObject, ad, ExecuteEvents.moveHandler);
                        }
                        else ExecuteEvents.Execute(it.Sel.gameObject, new BaseEventData(es), ExecuteEvents.submitHandler);
                    }
                }
            }
            // values change as they're set: read them often
            foreach (Item it in items) { it.Value = ValueOf(it.Sel); it.Icon = IconOf(it.Sel); }
        }

        // Silksong draws its menus over this screen, so while any Celeste menu is up EVERY one of its menu screens is
        // shrunk away (not just the open one: the one leaving and the one arriving flashed through, user 2026-10-14).
        // Selection, navigation and settings don't care about size. All of them come back as soon as no menu is up.
        List<Transform> allScreens; UIManager allScreensFor;   // Silksong's menu screens, gathered once
        readonly Dictionary<Transform, Vector3> shrunk = new Dictionary<Transform, Vector3>();
        void HideAll(bool on)
        {
            UIManager ui = UIManager.instance;
            if (ui == null || (!on && !CelesteFileSelect.Showing))
            {
                if (shrunk.Count > 0) { foreach (var kv in shrunk) if (kv.Key != null) kv.Key.localScale = kv.Value; shrunk.Clear(); }
                return;
            }
            if (allScreens == null || allScreensFor != ui)
            {
                allScreensFor = ui;
                allScreens = new List<Transform>();
                MenuScreen[] ms0 = { ui.quitGamePrompt, ui.returnMainMenuPrompt, ui.resolutionPrompt, ui.remapGamepadMenuScreen, ui.keyboardMenuScreen, ui.advancedGamepadMenuScreen,
                    ui.gamepadMenuScreen, ui.advancedVideoMenuScreen, ui.videoMenuScreen, ui.audioMenuScreen, ui.gameOptionsMenuScreen, ui.overscanMenuScreen,
                    ui.brightnessMenuScreen, ui.achievementsMenuScreen, ui.extrasContentMenuScreen, ui.extrasMenuScreen, ui.playModeMenuScreen, ui.optionsMenuScreen, ui.pauseMenuScreen };
                foreach (var m in ms0) if (m != null) allScreens.Add(m.transform);
                if (ui.mainMenuScreen != null) allScreens.Add(ui.mainMenuScreen.transform);
            }
            var all = allScreens;
            if (all != null) goto shrink;
            MenuScreen[] ms = { ui.quitGamePrompt, ui.returnMainMenuPrompt, ui.resolutionPrompt, ui.remapGamepadMenuScreen, ui.keyboardMenuScreen, ui.advancedGamepadMenuScreen,
                ui.gamepadMenuScreen, ui.advancedVideoMenuScreen, ui.videoMenuScreen, ui.audioMenuScreen, ui.gameOptionsMenuScreen, ui.overscanMenuScreen,
                ui.brightnessMenuScreen, ui.achievementsMenuScreen, ui.extrasContentMenuScreen, ui.extrasMenuScreen, ui.playModeMenuScreen, ui.optionsMenuScreen, ui.pauseMenuScreen };
            foreach (var m in ms) if (m != null) all.Add(m.transform);
            if (ui.mainMenuScreen != null) all.Add(ui.mainMenuScreen.transform);
        shrink:
            foreach (Transform t in all)
            {
                if (t == null || t.localScale == Vector3.zero) continue;
                if (!shrunk.ContainsKey(t)) shrunk[t] = t.localScale;
                t.localScale = Vector3.zero;
            }
        }
        static bool skyOnly, titleSeen;
        bool wasOverlay;   // only the sky, between the save screen and the title
        float sinceFiles = 99f;
        readonly HashSet<string> loggedScreens = new HashSet<string>();
        float holdFor;
        bool holding;
        bool autoPicked;      // the first item was picked for this screen already
        GameObject shownSel;  // the item shown as chosen: stays put while Silksong lets go between screens   // between two screens: the sky alone, so nothing of Silksong's shows
        Vector3 hiddenScale = Vector3.one;
        void RestoreRaycasts() { if (screen != null) screen.blocksRaycasts = true; }

        // the screen's heading and its items, top to bottom, with their labels and values
        void Read()
        {
            items.Clear();
            heading = "";
            float bestSize = 0f;
            foreach (Text t in screen.GetComponentsInChildren<Text>(false))
            {
                if (t.GetComponentInParent<Selectable>() != null || string.IsNullOrEmpty(t.text) || !t.isActiveAndEnabled) continue;
                if (t.fontSize > bestSize && t.text.Trim().Length > 1) { bestSize = t.fontSize; heading = Clean(t.text); }
            }
            var list = new List<Selectable>();
            if (!loggedScreens.Contains(screen.name))
            {
                loggedScreens.Add(screen.name);
                var sb = new System.Text.StringBuilder("Celeste menus: " + screen.name + ":");
                foreach (Selectable s in screen.GetComponentsInChildren<Selectable>(true))
                    sb.Append(" [" + s.name + " " + s.GetType().Name + (s.gameObject.activeInHierarchy ? "" : " inactive") + (s.interactable ? "" : " noninteract") + (s.gameObject.activeInHierarchy && Invisible(s) ? " invisible" : "") + "]");
                Plugin.Log.LogInfo(sb.ToString());
            }
            foreach (Selectable s in screen.GetComponentsInChildren<Selectable>(false))
                if (s.isActiveAndEnabled && (s.interactable || s is MappableKey || s is MappableControllerButton) && s.gameObject.activeInHierarchy && !(s is Scrollbar) && !Invisible(s)) list.Add(s);
            // Silksong's own order (its layout lists them top to bottom), so it holds while its art is shrunk away
            foreach (Selectable s in list)
            {
                string lab = LabelOf(s);
                if (lab.Equals("Sprint", StringComparison.OrdinalIgnoreCase)) lab = "Climb";
                if (kind == "title" && lab.Equals("Extras", StringComparison.OrdinalIgnoreCase)) lab = "Extra Settings";   // it opens Silkeste's settings   // Madeline grabs and climbs with it (user 2026-10-14)
                var it = new Item { Sel = s, Label = lab, Arrows = s is MenuOptionHorizontal || s is Slider || s.GetComponentInChildren<Slider>() != null };
                it.Value = ValueOf(s);
                if (it.Label.Length == 0 && it.Value.Length == 0) it.Label = Pretty(s.gameObject.name);
                items.Add(it);
            }
        }

        // hidden by Silksong itself (an "Apply" that only shows after a change): a fade group of its own at nothing
        bool Invisible(Selectable s)
        {
            for (Transform t = s.transform; t != null && t != screen.transform; t = t.parent)
            {
                var g = t.GetComponent<CanvasGroup>();
                if (g != null && g.alpha < 0.1f) return true;
            }
            return false;
        }

        static string LabelOf(Selectable s)
        {
            // the row's name, not its description: the shortest short text, leftmost on a tie
            Text best = null;
            var opt = s as MenuOptionHorizontal;
            foreach (Text t in s.GetComponentsInChildren<Text>(false))
            {
                if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text)) continue;
                if (opt != null && t == opt.optionText) continue;
                bool shorter = best == null || (t.text.Length <= 32) && (best.text.Length > 32 || t.transform.position.x < best.transform.position.x);
                if (shorter) best = t;
            }
            if (best != null) return Clean(best.text);
            foreach (Component c in s.GetComponentsInChildren<Component>(false))
            {
                // TextMeshPro labels: their text by name, so no compile-time type is needed
                var p = c != null ? c.GetType().GetProperty("text") : null;
                if (p != null && c.GetType().Name.StartsWith("TextMeshPro")) { string v = p.GetValue(c, null) as string; if (!string.IsNullOrEmpty(v)) return Clean(v); }
            }
            return "";
        }

        // a binding shown as a picture (controller buttons): Silksong's own button art
        static Sprite IconOf(Selectable s)
        {
            Image im = s is MappableControllerButton ? ((MappableControllerButton)s).buttonmapSprite : s is MappableKey ? ((MappableKey)s).keymapSprite : null;
            return im != null && im.isActiveAndEnabled && im.sprite != null ? im.sprite : null;
        }

        static string ValueOf(Selectable s)
        {
            if (s == null) return "";
            // a binding: its own text ("Press a button" while it listens), even while it's waiting for your press
            if (s is MappableControllerButton) { var t2 = ((MappableControllerButton)s).buttonmapText; return t2 != null ? Clean(t2.text) : ""; }
            if (s is MappableKey) { var t3 = ((MappableKey)s).keymapText; return t3 != null ? Clean(t3.text) : ""; }
            var opt = s as MenuOptionHorizontal;
            if (opt != null && opt.optionText != null) return Clean(opt.optionText.text);
            Slider sl = s as Slider ?? s.GetComponentInChildren<Slider>();
            if (sl != null) return Mathf.RoundToInt(sl.value).ToString();
            // a second text to the right of the label (key bindings and the like)
            Text label = null, val = null;
            foreach (Text t in s.GetComponentsInChildren<Text>(false))
            {
                if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text)) continue;
                if (label == null || t.transform.position.x < label.transform.position.x) { val = label; label = t; }
                else if (val == null || t.transform.position.x > val.transform.position.x) val = t;
            }
            return val != null && val != label ? Clean(val.text) : "";
        }

        static string Clean(string s)
        {
            s = System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]*>", "").Replace('\n', ' ').Trim();
            s = s.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"').Replace('—', '-').Replace('–', '-');
            return s;
        }
        static string Pretty(string n) { return System.Text.RegularExpressions.Regex.Replace(n.Replace("Button", "").Replace("_", " "), "([a-z])([A-Z])", "$1 $2").Trim(); }

        // ---- drawing -------------------------------------------------------------------------------------------
        void OnGUI()
        {
            if (Event.current.type == EventType.Repaint && screen == null && skyOnly)
            {
                float s0 = Screen.height / 1080f;
                Matrix4x4 o0 = GUI.matrix; GUI.depth = -6;
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s0, s0, 1f));
                CelesteFileSelect.Background(Screen.width / s0, 1f);
                GUI.matrix = o0; GUI.color = Color.white;
                return;
            }
            if (Event.current.type != EventType.Repaint || Art == null || screen == null) return;
            leaveFade = holding ? Mathf.Clamp01(1f - holdFor / 0.14f) : 1f;
            bool listUp = (!holding || leaveFade > 0f) && items.Count > 0 && !OverlayOpen;
            if (OverlayOpen) GUI.depth = 3;   // behind the records / settings drawn over it   // the sky is drawn even while the next screen is still arriving
            float s = Screen.height / 1080f, sw = Screen.width / s;
            Matrix4x4 old = GUI.matrix;
            GUI.depth = OverlayOpen ? 3 : -6;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            if (kind == "pause")
            {
                // Celeste's pause (user's screenshot): the game dimmed but visible, a big grey PAUSED with a dark edge
                GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(0f, 0f, sw, 1080f), Texture2D.whiteTexture);
                float ps = 1.7f;
                HiFont.Draw(Art, "PAUSED", sw / 2f - Art.Measure("PAUSED") * ps / 2f, 250f, ps, new Color(0.72f, 0.72f, 0.74f), new Color(0.16f, 0.12f, 0.22f, 1f), 6f);
            }
            else
            {
                CelesteFileSelect.Background(sw, 1f);
                // Celeste dims the mountain behind its option pages so the words read clearly: a light dim overall and
                // a darker panel down the middle that fades out at its sides, so the mountain still shows at the edges
                if (kind != "title")
                {
                    GUI.color = new Color(0.03f, 0.02f, 0.08f, 0.3f); GUI.DrawTexture(new Rect(0f, 0f, sw, 1080f), Texture2D.whiteTexture);
                    float pw = Math.Min(sw, 1700f);
                    GUI.color = new Color(0.02f, 0.01f, 0.07f, 0.55f); GUI.DrawTexture(new Rect(sw / 2f - pw / 2f, 0f, pw, 1080f), MenuFx.FadeBoth);
                }
                else
                {
                    // the title side gets a soft shade so the menu reads over the bright mountain
                    GUI.color = new Color(0.02f, 0.01f, 0.07f, 0.55f); GUI.DrawTexture(new Rect(0f, 0f, 1100f, 1080f), MenuFx.FadeOut);
                }
            }
            GameObject sel = shownSel;
            if (listUp)
            {
                if (kind == "title") DrawTitle(sw, sel); else DrawList(sw, sel);
                DrawHints(sw);
            }
            GUI.matrix = old;
            GUI.color = Color.white;
        }

        Color Hi() { return (flash % 0.2f) < 0.1f ? Hex(Visuals.MenuHighlightA) : Hex(Visuals.MenuHighlightB); }
        static Color Hex(string h) { Color c; return ColorUtility.TryParseHtmlString("#" + h, out c) ? c : Color.white; }
        static readonly Color Ink = new Color(0f, 0f, 0f, 0.75f);

        // the title: SILKESTE large at the top left, Celeste's main menu below it, each option with its icon; the
        // chosen one slides right, its icon grows and its words flash
        void DrawTitle(float sw, GameObject sel)
        {
            float t = Time.unscaledTime;
            string title = "SILKESTE";
            float ts = 2.2f, tx = 140f, ty = 120f;
            // a soft pink-to-blue glow breathing behind the title
            float tw0 = Art.Measure(title) * ts, th0 = Art.LineHeight * ts, br = 0.8f + Mathf.Sin(t * 0.9f) * 0.2f;
            GUI.color = new Color(1f, 0.45f, 0.6f, 0.4f * br); GUI.DrawTexture(new Rect(tx - 120f, ty - 90f, tw0 * 0.65f + 120f, th0 + 180f), MenuFx.Glow);
            GUI.color = new Color(0.5f, 0.7f, 1f, 0.4f * br); GUI.DrawTexture(new Rect(tx + tw0 * 0.35f, ty - 90f, tw0 * 0.65f + 120f, th0 + 180f), MenuFx.Glow);
            HiFont.Draw(Art, title, tx + 6f, ty + 8f, ts, new Color(0f, 0f, 0f, 0.35f), new Color(0f, 0f, 0f, 0f), 0f);
            float x = tx;
            for (int i = 0; i < title.Length; i++)
            {
                string ch = title.Substring(i, 1);
                Color c = Color.Lerp(new Color(1f, 0.45f, 0.5f), new Color(0.7f, 0.85f, 1f), i / (float)(title.Length - 1));
                HiFont.Draw(Art, ch, x, ty + Mathf.Sin(t * 1.6f + i * 0.5f) * 4f, ts, c, new Color(0.12f, 0.08f, 0.2f, 1f), 5f);
                x += Art.Measure(ch) * ts;
            }
            float suby = ty + Art.LineHeight * ts + 4f;
            Art.DrawOutlined("Madeline in Pharloom", tx + 6f, suby, 0.6f, new Color(0.85f, 0.85f, 0.95f, 1f), Ink, 2f);
            // a thin line under the subtitle that fades out to the right
            GUI.color = new Color(1f, 1f, 1f, 0.45f); GUI.DrawTexture(new Rect(tx + 6f, suby + Art.LineHeight * 0.6f + 10f, 520f, 2f), MenuFx.FadeOut);

            float y = 470f;
            int ti = 0;
            foreach (Item it in items)
            {
                float tap = Appear(ti++);
                bool on = it.Sel != null && it.Sel.gameObject == sel;
                float amt = OnAmount(it.Sel, on, Time.unscaledDeltaTime);
                GuiArt.Img icon = Art.Image(IconFor(it.Label));
                float slide = amt * 30f, isz = 60f + amt * 16f, rowH = 82f;
                float ix = 150f + slide, cy = y + rowH / 2f;
                if (amt > 0.01f)
                {
                    // the chosen row: a light streak fading out to the right, and a glow behind its icon
                    GUI.color = new Color(1f, 1f, 1f, 0.09f * amt * tap); GUI.DrawTexture(new Rect(ix - 70f, y + 6f, 660f, rowH - 12f), MenuFx.Streak);
                    GUI.color = new Color(1f, 0.85f, 0.6f, 0.35f * amt * tap); GUI.DrawTexture(new Rect(ix - isz, cy - isz, isz * 2f, isz * 2f), MenuFx.Glow);
                }
                if (icon != null) { GUI.color = new Color(1f, 1f, 1f, (on ? 1f : 0.85f) * tap); GUI.DrawTexture(new Rect(ix - isz / 2f, cy - isz / 2f, isz, isz * icon.H / Math.Max(1, icon.W)), icon.Tex); }
                float sc = 0.8f + amt * 0.15f;
                Color c = Color.Lerp(Color.white, Hi(), amt); c.a = tap;
                Art.DrawOutlined(it.Label, ix + 60f + (1f - tap) * 20f, cy - Art.LineHeight * sc / 2f, sc, c, new Color(0f, 0f, 0f, 0.75f * tap), 3f);
                it.Rect = new Rect(ix - 50f, y, 120f + Art.Measure(it.Label) * sc, rowH);
                y += rowH;
            }
            GUI.color = Color.white;
        }

        // ---- smoothing --------------------------------------------------------------------------------------
        float smoothScroll, barY = -1000f, barX = -1000f, barW, screenShownAt;
        // where a row sits across Silksong's layout, from local positions (its art is shrunk, so world ones collapse)
        // Silksong shows "Remap" only once it knows the controller's type, i.e. after you press something on it; here it
        // shows as soon as one is plugged in (user 2026-10-14)
        void ShowRemap()
        {
            var cd = screen.GetComponentInChildren<ControllerDetect>(true);
            var ih = InputHandler.Instance;
            if (cd == null || cd.remapButton == null || ih == null || ih.gamepadState.ToString() == "DETACHED" || cd.remapButton.gameObject.activeSelf) return;
            cd.remapButton.gameObject.SetActive(true);
            var list = HarmonyLib.AccessTools.Field(typeof(ControllerDetect), "menuButtonList")?.GetValue(cd) as MenuButtonList;
            if (list != null) list.SetupActive();
        }
        static bool IsMap(Selectable s) { return s is MappableControllerButton || s is MappableKey; }
        float LocalX(Transform t) { float x = 0f; for (; t != null && t != screen.transform; t = t.parent) x += t.localPosition.x; return x; }
        readonly Dictionary<Selectable, float> onAmt = new Dictionary<Selectable, float>();
        float Appear(int i) { float t = Time.unscaledTime - screenShownAt - i * 0.03f; float a = Mathf.Clamp01(t / 0.18f); return Mathf.Min(1f - (1f - a) * (1f - a), leaveFade); }
        float leaveFade = 1f;   // 1 while the screen is up, easing to 0 as it leaves (the next one fades in after)
        float OnAmount(Selectable s, bool on, float dt)
        {
            if (s == null) return 0f;
            float v; onAmt.TryGetValue(s, out v);
            v = Mathf.MoveTowards(v, on ? 1f : 0f, dt / 0.12f);
            onAmt[s] = v;
            return v * v * (3f - 2f * v);
        }

        static string IconFor(string label)
        {
            string l = label.ToLowerInvariant();
            if (l.Contains("option") || l.Contains("setting")) return "menu/options";
            if (l.Contains("quit") || l.Contains("exit")) return "menu/exit";
            if (l.Contains("achiev")) return "menu/journal";
            if (l.Contains("extra") || l.Contains("credit")) return "menu/credits";
            if (l.Contains("continue") || l.Contains("resume")) return "menu/play";
            return "menu/start";
        }

        // every other screen: Celeste's TextMenu, the heading at the top, the items centred, values right with < >
        void DrawList(float sw, GameObject sel)
        {
            UIManager ui = UIManager.instance;
            bool keys = ui != null && ((ui.keyboardMenuScreen != null && screen == ui.keyboardMenuScreen.ScreenCanvasGroup) || (ui.remapGamepadMenuScreen != null && screen == ui.remapGamepadMenuScreen.ScreenCanvasGroup) || (ui.gamepadMenuScreen != null && screen == ui.gamepadMenuScreen.ScreenCanvasGroup));
            float cx = sw / 2f, colW = keys ? 640f : Math.Min(1100f, sw - 200f);
            float y = kind == "prompt" ? 330f : kind == "pause" ? 380f : 110f;
            if (kind == "pause") heading = "";   // PAUSED is its heading
            if (heading.Length > 0)
            {
                float hs = 1.15f; while (hs > 0.5f && Art.Measure(heading) * hs > sw - 200f) hs -= 0.05f;
                // Celeste's page title: each letter shading from lavender to blue, with a soft dark edge
                string ht = heading;
                float hx = cx - Art.Measure(ht) * hs / 2f;
                for (int i = 0; i < ht.Length; i++)
                {
                    string ch = ht.Substring(i, 1);
                    Color hc = Color.Lerp(new Color(0.78f, 0.66f, 0.85f), new Color(0.55f, 0.66f, 0.92f), ht.Length > 1 ? i / (float)(ht.Length - 1) : 0f);
                    HiFont.Draw(Art, ch, hx, y, hs, hc, new Color(0.08f, 0.06f, 0.16f, 0.85f), 4f);
                    hx += Art.Measure(ch) * hs;
                }
                // a fine rule under the heading, brightest in the middle
                GUI.color = new Color(0.8f, 0.78f, 1f, 0.5f); GUI.DrawTexture(new Rect(cx - 340f, y + Art.LineHeight * hs + 10f, 680f, 2f), MenuFx.FadeBoth);
                y += Art.LineHeight * hs + 36f;
            }
            float sc = kind == "pause" ? 0.95f : 0.8f, rowH = Art.LineHeight * sc + 22f;
            // long lists shrink to fit, and scroll to keep the chosen one in view; short ones sit in the middle
            float avail = 1080f - 150f - y;
            if (items.Count * rowH < avail) y += (avail - items.Count * rowH) * 0.35f;
            avail = 1080f - 150f - y;
            if (items.Count * rowH > avail) { sc = Math.Max(0.45f, sc * avail / (items.Count * rowH)); rowH = Art.LineHeight * sc + 12f; }
            // the controller remap page sits in two columns like Silksong's, so the D-pad moves where it looks like it
            // should (user 2026-10-14: in one list it seemed to skip rows); everything else is one column
            int n = items.Count;
            var rowOf = new int[n]; var colOf = new int[n];
            bool twoCol = keys && items.Exists(i => IsMap(i.Sel));   // both the controller and the keyboard binding pages
            int rows = 0;
            if (twoCol)
            {
                var xs = new List<float>();
                for (int i = 0; i < n; i++) if (IsMap(items[i].Sel)) xs.Add(LocalX(items[i].Sel.transform));
                xs.Sort();
                float mid = xs.Count > 0 ? (xs[0] + xs[xs.Count - 1]) / 2f : 0f;
                int left = 0, right = 0;
                for (int i = 0; i < n; i++)
                    if (IsMap(items[i].Sel)) { bool r = LocalX(items[i].Sel.transform) > mid; colOf[i] = r ? 1 : 0; rowOf[i] = r ? right++ : left++; }
                rows = Math.Max(left, right);
                for (int i = 0; i < n; i++) if (!IsMap(items[i].Sel)) { colOf[i] = -1; rowOf[i] = rows++; }
                colW = Math.Min(1400f, sw - 160f);
            }
            else { for (int i = 0; i < n; i++) { rowOf[i] = i; colOf[i] = -1; } rows = n; }
            if (rows * rowH > avail) { sc = Math.Max(0.45f, sc * avail / (rows * rowH)); rowH = Art.LineHeight * sc + 12f; }
            int selIdx = items.FindIndex(i => i.Sel != null && i.Sel.gameObject == sel);
            float total = rows * rowH, scroll = 0f;
            if (total > avail && selIdx >= 0) scroll = Mathf.Clamp(rowOf[selIdx] * rowH - avail / 2f, 0f, total - avail);
            // smooth (user 2026-10-14: it looked sharp): the scroll and the highlight bar glide, the chosen item eases in
            float dt = Time.unscaledDeltaTime, k = 1f - Mathf.Exp(-dt * 16f);
            smoothScroll = Mathf.Lerp(smoothScroll, scroll, k);
            scroll = smoothScroll;
            float baseCx = cx, baseW = colW;
            Func<int, float> colCx = i => colOf[i] < 0 ? baseCx : baseCx + (colOf[i] == 0 ? -1f : 1f) * baseW * 0.26f;
            Func<int, float> colWidth = i => colOf[i] < 0 ? baseW : baseW * 0.46f;
            if (selIdx >= 0)
            {
                float targetY = y + rowOf[selIdx] * rowH - scroll, targetX = colCx(selIdx) - colWidth(selIdx) / 2f - 20f, targetW = colWidth(selIdx) + 40f;
                barY = barY < -500f ? targetY : Mathf.Lerp(barY, targetY, k);
                barX = barX < -500f ? targetX : Mathf.Lerp(barX, targetX, k);
                barW = barW < 1f ? targetW : Mathf.Lerp(barW, targetW, k);
                // the highlight: a soft lavender band fading at both ends, with thin bright edges above and below
                float ap0 = Appear(selIdx);
                GUI.color = new Color(0.55f, 0.5f, 0.95f, 0.22f * ap0); GUI.DrawTexture(new Rect(barX - 60f, barY - 6f, barW + 120f, rowH), MenuFx.FadeBoth);
                GUI.color = new Color(1f, 1f, 1f, 0.35f * ap0);
                GUI.DrawTexture(new Rect(barX, barY - 6f, barW, 1.5f), MenuFx.FadeBoth);
                GUI.DrawTexture(new Rect(barX, barY - 6f + rowH - 1.5f, barW, 1.5f), MenuFx.FadeBoth);
                GUI.color = Color.white;
            }
            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                float ap = Appear(i);   // a new screen's items slide in one after another
                float ry = y + rowOf[i] * rowH - scroll;
                cx = colCx(i); colW = colWidth(i);
                it.Rect = new Rect(cx - colW / 2f - 20f, ry - 6f, colW + 40f, rowH);
                if (ry < y - 4f || ry + rowH > 1080f - 140f) { it.Rect = new Rect(); continue; }
                bool on = it.Sel != null && it.Sel.gameObject == sel;
                float amt = OnAmount(it.Sel, on, dt);
                ry += (1f - ap) * 24f;
                float lift = -amt * (2f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 2f);
                Color c = Color.Lerp(Color.white, Hi(), amt); c.a = ap;
                Color ink = new Color(0f, 0f, 0f, 0.75f * ap);   // the outline fades with its letters (it looked dark otherwise)
                bool picture = it.Value.Length == 0 && it.Icon != null && it.Icon.texture != null;
                if (it.Value.Length == 0 && !picture)
                {
                    // a plain button: centred
                    Art.DrawOutlined(it.Label, cx - Art.Measure(it.Label) * sc / 2f, ry + lift, sc, c, ink, 2.5f);
                }
                else if (picture)
                {
                    // a controller binding: the name at the left, Silksong's own button picture at the right
                    Art.DrawOutlined(it.Label, cx - colW / 2f, ry + lift, sc, c, ink, 2.5f);
                    Rect tr = it.Icon.textureRect; Texture2D tx = it.Icon.texture;
                    float ih = rowH - 8f, iw = ih * tr.width / Math.Max(1f, tr.height);
                    GUI.color = new Color(1f, 1f, 1f, ap);
                    GUI.DrawTextureWithTexCoords(new Rect(cx + colW / 2f - iw, ry + lift - 2f, iw, ih), tx, new Rect(tr.x / tx.width, tr.y / tx.height, tr.width / tx.width, tr.height / tx.height));
                    GUI.color = Color.white;
                }
                else
                {
                    string v = it.Value;
                    float vw = Art.Measure(v) * sc, vx = cx + colW / 2f - vw;
                    // the name shrinks rather than run into the value
                    float ls = sc, room = colW - vw - 90f;
                    while (ls > 0.35f && Art.Measure(it.Label) * ls > room) ls -= 0.02f;
                    Art.DrawOutlined(it.Label, cx - colW / 2f, ry + lift + (sc - ls) * Art.LineHeight / 2f, ls, c, ink, 2.5f);
                    Color vc = on ? c : new Color(0.8f, 0.8f, 0.85f, ap);
                    if (keys) Pill(v, cx + colW / 2f, ry + lift, rowH - 14f);   // a key: Celeste's white pill
                    else Art.DrawOutlined(v, vx, ry + lift, sc, vc, ink, 2.5f);
                    if (it.Arrows && on)
                    {
                        Art.DrawOutlined("<", vx - Art.Measure("<") * sc - 18f, ry + lift, sc, c, ink, 2.5f);
                        Art.DrawOutlined(">", cx + colW / 2f + 14f, ry + lift, sc, c, ink, 2.5f);
                    }
                }
            }
            if (scroll > 1f) Art.DrawOutlined("^", cx - 8f, y - 30f, 0.5f, Color.white, Ink, 2f);
            if (scroll < total - avail - 1f && total > avail) Art.DrawOutlined("v", cx - 8f, 1080f - 140f, 0.5f, Color.white, Ink, 2f);
        }

        // ---- big letters, sharp (user 2026-10-14: SILKESTE in higher quality) ----------------------------------
        // Celeste's font is 64 px, so at title size it looked soft. Each big letter is redrawn once at 4x with
        // sharpened edges, and its outline is grown from the letter's real shape (a distance field) instead of four
        // shifted copies: crisper, rounder corners, and two draws per letter instead of five.
        internal static class HiFont
        {
            const int F = 4;
            class Hi { public Texture2D Fill, Line; public int Pad; }
            static readonly Dictionary<int, Hi> cache = new Dictionary<int, Hi>();

            // like GuiArt.DrawOutlined, stroke in GUI units at this scale
            public static void Draw(GuiArt art, string text, float x, float top, float scale, Color c, Color outline, float stroke)
            {
                int r = Mathf.Clamp(Mathf.RoundToInt(stroke / scale * F), 0, 24);
                foreach (char ch in text)
                {
                    GuiArt.Img g;
                    if (!art.Glyphs.TryGetValue(ch, out g)) continue;
                    Hi h = Get(ch, g, r);
                    if (h != null)
                    {
                        float k = scale / F, p = h.Pad * k;
                        var rect = new Rect(x + g.XOffset * scale - p, top + g.YOffset * scale - p, g.W * scale + 2f * p, g.H * scale + 2f * p);
                        if (outline.a > 0f && r > 0) { GUI.color = outline; GUI.DrawTexture(rect, h.Line); }
                        GUI.color = c; GUI.DrawTexture(rect, h.Fill);
                    }
                    x += g.XAdvance * scale;
                }
            }

            static Hi Get(char ch, GuiArt.Img g, int r)
            {
                int key = ch * 32 + r;
                Hi h;
                if (cache.TryGetValue(key, out h)) return h;
                cache[key] = h = Build(g, r);
                return h;
            }

            static Hi Build(GuiArt.Img g, int r)
            {
                if (g.Rgba == null || g.W <= 0 || g.H <= 0) return null;
                int pad = r + 2, w = g.W * F + pad * 2, hh = g.H * F + pad * 2;
                var fill = new float[w * hh];
                // the letter at 4x: smooth (bilinear) then its edge pulled tight, so it's sharp without stair steps
                for (int y = 0; y < hh; y++) for (int x = 0; x < w; x++)
                    {
                        float sx = (x - pad + 0.5f) / F - 0.5f, sy = (y - pad + 0.5f) / F - 0.5f;
                        float a = Sample(g, sx, sy);
                        fill[y * w + x] = Mathf.Clamp01((a - 0.5f) * 3f + 0.5f);
                    }
                // distance (in 4x pixels) from each pixel to the letter: a two-pass chamfer sweep
                var d = new float[w * hh];
                const float big = 1e6f, dg = 1.4142f;
                for (int i = 0; i < d.Length; i++) d[i] = fill[i] >= 0.5f ? 0f : big;
                for (int y = 0; y < hh; y++) for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x; float v = d[i];
                        if (x > 0) v = Math.Min(v, d[i - 1] + 1f);
                        if (y > 0) { v = Math.Min(v, d[i - w] + 1f); if (x > 0) v = Math.Min(v, d[i - w - 1] + dg); if (x < w - 1) v = Math.Min(v, d[i - w + 1] + dg); }
                        d[i] = v;
                    }
                for (int y = hh - 1; y >= 0; y--) for (int x = w - 1; x >= 0; x--)
                    {
                        int i = y * w + x; float v = d[i];
                        if (x < w - 1) v = Math.Min(v, d[i + 1] + 1f);
                        if (y < hh - 1) { v = Math.Min(v, d[i + w] + 1f); if (x < w - 1) v = Math.Min(v, d[i + w + 1] + dg); if (x > 0) v = Math.Min(v, d[i + w - 1] + dg); }
                        d[i] = v;
                    }
                var fp = new Color32[w * hh]; var lp = new Color32[w * hh];
                for (int y = 0; y < hh; y++) for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x, o = (hh - 1 - y) * w + x;   // texture rows run upward
                        float la = Mathf.Max(fill[i], Mathf.Clamp01(r + 0.5f - d[i]));
                        fp[o] = new Color32(255, 255, 255, (byte)(fill[i] * 255f));
                        lp[o] = new Color32(255, 255, 255, (byte)(la * 255f));
                    }
                return new Hi { Fill = Tex(w, hh, fp), Line = Tex(w, hh, lp), Pad = pad };
            }

            static float Alpha(GuiArt.Img g, int x, int y) { if (x < 0 || y < 0 || x >= g.W || y >= g.H) return 0f; return g.Rgba[(y * g.W + x) * 4 + 3] / 255f; }
            static float Sample(GuiArt.Img g, float x, float y)
            {
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0;
                float a = Mathf.Lerp(Alpha(g, x0, y0), Alpha(g, x0 + 1, y0), fx), b = Mathf.Lerp(Alpha(g, x0, y0 + 1), Alpha(g, x0 + 1, y0 + 1), fx);
                return Mathf.Lerp(a, b, fy);
            }
            static Texture2D Tex(int w, int h, Color32[] px)
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 2 };
                t.SetPixels32(px); t.Apply(true, true);   // mipmaps: clean when drawn smaller, too
                return t;
            }
        }

        // ---- soft shapes for the menus, each made once (tiny textures, stretched by the GPU) -------------------
        internal static class MenuFx
        {
            static Texture2D glow, fadeBoth, fadeOut, fadeIn;
            // a round glow, bright in the middle fading to nothing
            public static Texture2D Glow { get { return glow ?? (glow = Make(64, 64, (x, y) => { float d = Mathf.Sqrt(x * x + y * y); float g = Mathf.Clamp01(1f - d); return g * g * (3f - 2f * g); })); } }
            // across: clear, solid in the middle, clear again
            public static Texture2D FadeBoth { get { return fadeBoth ?? (fadeBoth = Make(128, 1, (x, y) => { float g = 1f - Mathf.Abs(x); return Mathf.Clamp01(g * 2.2f); })); } }
            // across: solid at the left, fading out to the right
            public static Texture2D FadeOut { get { return fadeOut ?? (fadeOut = Make(128, 1, (x, y) => { float g = 1f - (x + 1f) / 2f; return g * g; })); } }
            static Texture2D streak;
            // across: easing in at the left, then a long fade out to the right (the chosen title row)
            public static Texture2D Streak { get { return streak ?? (streak = Make(128, 1, (x, y) => { float u = (x + 1f) / 2f; float g = Mathf.Clamp01(u / 0.12f) * (1f - u); return g * g; })); } }
            // across: clear at the left, solid at the right (a shooting star's tail)
            public static Texture2D FadeIn { get { return fadeIn ?? (fadeIn = Make(128, 1, (x, y) => { float g = (x + 1f) / 2f; return g * g; })); } }
            // f gets x and y from -1 to 1 and gives the alpha
            static Texture2D Make(int w, int h, Func<float, float, float> f)
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                    {
                        float fx = w > 1 ? x / (w - 1f) * 2f - 1f : 0f, fy = h > 1 ? y / (h - 1f) * 2f - 1f : 0f;
                        px[y * w + x] = new Color(1f, 1f, 1f, f(fx, fy));
                    }
                t.SetPixels32(px); t.Apply(false, true);
                return t;
            }
        }

        // a key as Celeste shows it: white pill (a circle for one letter), dark capitals, right edge at x
        static Texture2D dot;
        static void Pill(string key, float right, float top, float h)
        {
            if (dot == null)
            {
                const int n = 64;
                dot = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                var px = new Color32[n * n];
                for (int py = 0; py < n; py++) for (int pxi = 0; pxi < n; pxi++)
                    {
                        float d = Mathf.Sqrt((pxi - 31.5f) * (pxi - 31.5f) + (py - 31.5f) * (py - 31.5f));
                        px[py * n + pxi] = new Color(1f, 1f, 1f, Mathf.Clamp01(31.5f - d));
                    }
                dot.SetPixels32(px); dot.Apply(false, true);
            }
            string k = key.ToUpperInvariant();
            float ks = Mathf.Min(0.5f, (h - 10f) / Art.LineHeight * 1.1f);
            if (k.Length > 6) ks *= 0.75f;
            float tw = Art.Measure(k) * ks, w = Math.Max(h, tw + h * 0.6f), x = right - w;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, top, h, h), dot);
            GUI.DrawTexture(new Rect(x + w - h, top, h, h), dot);
            if (w > h) GUI.DrawTexture(new Rect(x + h / 2f, top, w - h, h), Texture2D.whiteTexture);
            Art.DrawLine(k, x + (w - tw) / 2f, top + (h - Art.LineHeight * ks) / 2f, ks, new Color(0.08f, 0.07f, 0.12f));
        }

        void DrawHints(float sw)
        {
            bool pad = MenuMouse.PadActive;
            string[] parts = kind == "title" ? (pad ? new[] { "Confirm  Cross" } : new[] { "Confirm  Enter" })
                : (pad ? new[] { "Confirm  Cross", "Back  Circle" } : new[] { "Confirm  Enter", "Back  Esc" });
            float hs = 0.5f, x = sw - 50f;
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                x -= Art.Measure(parts[i]) * hs;
                Art.DrawOutlined(parts[i], x, 1080f - 70f, hs, Color.white, new Color(0f, 0f, 0f, 0.6f), 2f);
                x -= 50f;
            }
        }
    }
}
