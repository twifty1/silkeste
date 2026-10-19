using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MadelineInPharloom.Sheets;

namespace MadelineInPharloom
{
    /// <summary>
    /// The save files as Celeste's file select (user 2026-10-14: "just like in Celeste ... without breaking anything").
    /// Silksong's save slots keep doing everything (selecting, loading, the erase button and its prompts, restoring);
    /// on a slot that shows a save or a new game, their look is faded out and Celeste's file card is drawn exactly over
    /// it, so the mouse and the controller still land on the right slot. The selected file slides out a little with
    /// Celeste's shine running across it, and its ticket pulls out from behind with the save's details. Erase prompts,
    /// broken saves and every other state keep Silksong's own look.
    /// </summary>
    public class CelesteFileSelect : MonoBehaviour
    {
        static GuiArt Art { get { return SpeedrunTimer.Art; } }

        class Slot
        {
            public SaveSlotButton Button;
            public float Sel;          // 0..1, eased toward being selected
            public float ShineT = -1f; // the shine running across the card after it's selected
            public Rect Gui;           // where Silksong's slot is, in 1080p GUI units
            public bool Ours;          // drawn as a Celeste card this frame
        }
        readonly Dictionary<SaveSlotButton, Slot> slots = new Dictionary<SaveSlotButton, Slot>();
        bool loggedError, logged;

        static readonly SaveSlotButton.SlotState[] Drawn = { SaveSlotButton.SlotState.SavePresent, SaveSlotButton.SlotState.EmptySlot };

        void Awake()
        {
            StartSky();
            try
            {
                var target = HarmonyLib.AccessTools.Method(typeof(MenuAudioController), "PlaySelect");
                if (target != null) new HarmonyLib.Harmony("silkeste.fileselectsound").Patch(target, prefix: new HarmonyLib.HarmonyMethod(typeof(CelesteFileSelect), nameof(QuietSelect)));
            }
            catch (Exception e) { Plugin.Log.LogWarning("file select sound patch: " + e.Message); }
        }
        // Silksong's hover sound on a save file: off while the Celeste screen is up (user 2026-10-14)
        static bool QuietSelect() { return !Showing; }

        void LateUpdate()
        {
            try { Tick(); }
            catch (Exception e) { if (!loggedError) { loggedError = true; Plugin.Log.LogError("CelesteFileSelect: " + e); } }
        }

        // the whole screen (user 2026-10-14: "exactly like the photo, even the background ... top to bottom")
        float screenA;        // the Celeste screen fading in over Silksong's
        float findIn;
        /// <summary>The Celeste save screen is up: Silksong's own menu hover sound stays quiet (Celeste's plays instead).</summary>
        public static bool Showing;
        bool active;          // every file showing is one it can draw (an erase prompt or a broken save hands back)
        int index;            // the chosen file, top to bottom
        readonly List<Slot> ordered = new List<Slot>();
        readonly Dictionary<SaveSlotButton, Rect> cardRects = new Dictionary<SaveSlotButton, Rect>();

        // ---- the screen, driven here (user 2026-10-14: no flashes of Silksong's look, and a confirm menu) --------
        // While it's up, nothing in Silksong's menu is selected, so Silksong can't react (no gold highlight, no hover
        // sound, no loading on a stray press). This screen reads the buttons and, when a file is begun, hands that
        // file's submit to Silksong, which then loads it exactly as it always does.
        static readonly SaveSlotButton.SlotState[] HandBack = { SaveSlotButton.SlotState.ClearPrompt, SaveSlotButton.SlotState.ClearConfirm, SaveSlotButton.SlotState.RestoreSave, SaveSlotButton.SlotState.BlackThreadInfected };
        bool confirmOpen;     // the chosen file's card at the top with Begin and Assist Mode under it
        int confirmIndex;     // 0 Begin, 1 Assist Mode
        float confirmT;       // 0..1 the confirm view easing in
        bool begun;           // a file was begun: hands off until the screen goes
        int lastSlot = -1;

        void Tick()
        {
            if (Art == null || Art.Image("fileselect/card") == null) return;
            float dt = Time.unscaledDeltaTime;
            var es = UnityEngine.EventSystems.EventSystem.current;
            if ((findIn -= dt) <= 0f)
            {
                findIn = 0.5f;
                foreach (SaveSlotButton b in FindObjectsByType<SaveSlotButton>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (!slots.ContainsKey(b)) slots[b] = new Slot { Button = b };
            }
            var gone = new List<SaveSlotButton>();
            ordered.Clear();
            bool prompt = false, anyShown = false;
            foreach (var kv in slots)
            {
                Slot s = kv.Value; SaveSlotButton b = kv.Key;
                if (b == null) { gone.Add(kv.Key); continue; }
                s.Ours = false;
                if (!b.isActiveAndEnabled) continue;
                if (Array.IndexOf(HandBack, b.State) >= 0) { prompt = true; continue; }
                if (b.State == SaveSlotButton.SlotState.Hidden) continue;
                // every file Silksong lists is a card here, even one still loading its details
                anyShown |= b.myCanvasGroup == null || b.myCanvasGroup.alpha > 0.05f || active;
                s.Ours = true; ordered.Add(s);
            }
            foreach (var g in gone) slots.Remove(g);
            ordered.Sort((x, y) => x.Button.SaveSlotIndex.CompareTo(y.Button.SaveSlotIndex));
            bool want = ordered.Count > 0 && anyShown && !prompt && !begun;
            if (want && !active) { screenA = 1f; confirmOpen = false; confirmT = 0f; }   // up at once: no glimpse of Silksong's look
            active = want;
            Showing = active;
            if (!active) screenA = Mathf.MoveTowards(screenA, 0f, dt / 0.2f);
            if (ordered.Count == 0) begun = false;   // Silksong's files gone (in the game, or back on the title): ready again
            foreach (var kv in slots) if (kv.Key != null && kv.Key.myCanvasGroup != null) kv.Key.myCanvasGroup.blocksRaycasts = !active;
            if (!active)
            {
                // an erase prompt is up: give Silksong its selection back on that file so the prompt can be answered
                return;
            }
            if (!logged) { logged = true; Plugin.Log.LogInfo("Celeste file select: the save files as Celeste's"); }
            if (es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.GetComponentInParent<SaveSlotButton>() != null) es.SetSelectedGameObject(null);
            if (lastSlot >= 0) for (int i = 0; i < ordered.Count; i++) if (ordered[i].Button.SaveSlotIndex == lastSlot) { index = i; lastSlot = -1; }
            index = Mathf.Clamp(index, 0, ordered.Count - 1);

            var kb = UnityEngine.InputSystem.Keyboard.current;
            InputHandler ih = InputHandler.Instance;
            HeroActions a = ih != null ? ih.inputActions : null;
            InControl.InputDevice dev = null;
            try { dev = InControl.InputManager.ActiveDevice; } catch (Exception) { }
            bool up = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (a != null && a.Up.WasPressed);
            bool down = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (a != null && a.Down.WasPressed);
            bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (a != null && a.Left.WasPressed);
            bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (a != null && a.Right.WasPressed);
            bool ok = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (dev != null && dev.Action1.WasPressed) || (a != null && a.Jump.WasPressed);
            bool back = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) || (dev != null && dev.Action2.WasPressed);
            bool erase = (kb != null && kb.deleteKey.wasPressedThisFrame) || (dev != null && dev.Action3.WasPressed);
            var m = UnityEngine.InputSystem.Mouse.current;
            Vector2 gp = MenuMouse.Gui();
            bool click = m != null && m.leftButton.wasPressedThisFrame, moved = m != null && m.delta.ReadValue().sqrMagnitude > 0.5f;
            SaveSlotButton chosen = ordered[index].Button;
            confirmT = Mathf.MoveTowards(confirmT, confirmOpen ? 1f : 0f, dt / 0.25f);
            // Assist Options open over the files: they take the input, and the press that closes them isn't a Back here
            if (AssistMode.IsOpen) { assistShutAt = Time.unscaledTime; return; }
            if (Time.unscaledTime - assistShutAt < 0.25f) return;

            bool rclick = m != null && m.rightButton.wasPressedThisFrame;
            eraseT = Mathf.MoveTowards(eraseT, eraseOpen ? 1f : 0f, dt / 0.18f);
            if (eraseOpen)
            {
                // Celeste's own delete check: Cancel or Delete, Cancel first so a stray press is safe
                int was = eraseIndex;
                if (left || up) eraseIndex = 0;
                if (right || down) eraseIndex = 1;
                for (int i = 0; i < 2; i++) if (eraseRects[i].Contains(gp) && (moved || click)) eraseIndex = i;
                if (eraseIndex != was) Sfx.Play(eraseIndex == 0 ? Sounds.MenuRolloverUp.Id : Sounds.MenuRolloverDown.Id);
                bool hit = ok || (click && eraseRects[eraseIndex].Contains(gp));
                if (back || rclick || (hit && eraseIndex == 0)) { eraseOpen = false; Sfx.Play(Sounds.MenuUnpause.Id); }
                else if (hit && eraseIndex == 1)
                {
                    eraseOpen = false;
                    Sfx.Play(Sounds.MenuButtonSelect.Id);
                    // Silksong deletes the file and turns its slot to an empty one, exactly as after its own prompts
                    try { HarmonyLib.AccessTools.Method(typeof(SaveSlotButton), "ClearSaveFile").Invoke(chosen, null); Plugin.Log.LogInfo("File " + chosen.SaveSlotIndex + " deleted from the file select"); }
                    catch (Exception e) { Plugin.Log.LogWarning("Couldn't delete the file: " + e.Message); }
                }
            }
            else if (confirmOpen)
            {
                // Begin, or Assist Mode on and off; Back returns to the files
                int wasC = confirmIndex;
                // a new file also offers Assist Options (user 2026-10-14); a saved one just Begin
                int nOpts = chosen.State == SaveSlotButton.SlotState.EmptySlot ? 2 : 1;
                if (up || left) confirmIndex = Math.Max(0, confirmIndex - 1);
                if (down || right) confirmIndex = Math.Min(nOpts - 1, confirmIndex + 1);
                for (int i = 0; i < nOpts; i++) if (optionRects[i].Contains(gp) && (moved || click)) confirmIndex = i;
                confirmIndex = Mathf.Clamp(confirmIndex, 0, nOpts - 1);

                if (confirmIndex != wasC) Sfx.Play(up ? Sounds.MenuRolloverUp.Id : Sounds.MenuRolloverDown.Id);
                bool hit = ok || (click && optionRects[confirmIndex].Contains(gp));
                if (confirmIndex == 1 && hit)
                {
                    // Assist Options: Silkeste's Assist tab opens over the files; closing it comes back here
                    Sfx.Play(Sounds.MenuButtonSelect.Id);
                    AssistMode.OpenFromMenu();
                }
                else if (confirmIndex == 0 && hit)
                {
                    // Begin: Silksong loads (or starts) the file exactly as it always does
                    begun = true;
                    Sfx.Play(Sounds.MenuButtonSelect.Id);
                    if (es != null)
                    {
                        es.SetSelectedGameObject(chosen.gameObject);
                        UnityEngine.EventSystems.ExecuteEvents.Execute(chosen.gameObject, new UnityEngine.EventSystems.BaseEventData(es), UnityEngine.EventSystems.ExecuteEvents.submitHandler);
                    }
                }
                else if (back) { confirmOpen = false; Sfx.Play(Sounds.MenuUnpause.Id); }
            }
            else
            {
                int was = index;
                if (up) index = (index + ordered.Count - 1) % ordered.Count;
                if (down) index = (index + 1) % ordered.Count;
                for (int i = 0; i < ordered.Count; i++)
                {
                    Rect cr;
                    if (cardRects.TryGetValue(ordered[i].Button, out cr) && cr.Contains(gp) && (moved || click || rclick)) index = i;
                }
                Rect rcr;
                if (rclick && cardRects.TryGetValue(ordered[index].Button, out rcr) && rcr.Contains(gp)) erase = true;   // right-click a file to delete it
                if (index != was) Sfx.Play(up ? Sounds.MenuRolloverUp.Id : Sounds.MenuRolloverDown.Id);
                chosen = ordered[index].Button;
                Rect ccr;
                bool onCard = cardRects.TryGetValue(chosen, out ccr) && ccr.Contains(gp);
                if (ok || (click && onCard)) { confirmOpen = true; confirmIndex = 0; Sfx.Play(Sounds.MenuPause.Id); }
                else if (back && chosen.backButton != null && es != null)
                {
                    // Back to the title, through Silksong's own back button
                    es.SetSelectedGameObject(chosen.backButton.gameObject);
                    UnityEngine.EventSystems.ExecuteEvents.Execute(chosen.backButton.gameObject, new UnityEngine.EventSystems.BaseEventData(es), UnityEngine.EventSystems.ExecuteEvents.submitHandler);
                }
                else if (erase && chosen.State == SaveSlotButton.SlotState.SavePresent)
                {
                    // erase: Delete, Square or right-click opens Celeste's delete check (user 2026-10-14)
                    eraseOpen = true; eraseIndex = 0; Sfx.Play(Sounds.MenuPause.Id);
                }
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                Slot s = ordered[i];
                bool selected = i == index;
                s.Sel = Mathf.MoveTowards(s.Sel, selected && !confirmOpen ? 1f : 0f, dt / 0.18f);   // no shine on hover (user 2026-10-14)
                if (s.ShineT >= 0f && (s.ShineT += dt) > 0.6f) s.ShineT = -1f;
            }
        }
        readonly Rect[] optionRects = new Rect[2];
        bool eraseOpen; int eraseIndex; float eraseT, assistShutAt = -10f;
        readonly Rect[] eraseRects = new Rect[2];

        // Celeste's delete check: the files dim, a heading asks, Cancel and Delete side by side
        void EraseBox(float sw, SaveSlotButton b)
        {
            float a = eraseT * eraseT * (3f - 2f * eraseT) * screenA;
            if (a <= 0.01f) return;
            GUI.color = new Color(0.02f, 0.01f, 0.06f, 0.7f * a); GUI.DrawTexture(new Rect(0f, 0f, sw, 1080f), Texture2D.whiteTexture);
            GUI.color = new Color(0.02f, 0.01f, 0.07f, 0.6f * a); GUI.DrawTexture(new Rect(sw / 2f - 700f, 330f, 1400f, 420f), CelesteMenus.MenuFx.FadeBoth);
            float rise = (1f - a) * 30f, cx = sw / 2f;
            string q = "Delete File " + b.SaveSlotIndex + "?";
            float qs = 1.1f;
            CelesteMenus.HiFont.Draw(Art, q, cx - Art.Measure(q) * qs / 2f, 380f + rise, qs, new Color(1f, 1f, 1f, a), new Color(0.08f, 0.06f, 0.16f, 0.85f * a), 4f);
            string w = "This can't be undone.";
            Art.DrawOutlined(w, cx - Art.Measure(w) * 0.55f / 2f, 380f + rise + Art.LineHeight * qs + 6f, 0.55f, new Color(0.85f, 0.8f, 0.95f, a), new Color(0f, 0f, 0f, 0.6f * a), 2f);
            GUI.color = new Color(0.8f, 0.78f, 1f, 0.5f * a); GUI.DrawTexture(new Rect(cx - 300f, 380f + rise + Art.LineHeight * qs + 60f, 600f, 2f), CelesteMenus.MenuFx.FadeBoth);
            string[] opts = { "Cancel", "Delete" };
            float os = 0.85f, oy = 590f + rise;
            for (int i = 0; i < 2; i++)
            {
                bool on = i == eraseIndex;
                float ox = cx + (i == 0 ? -200f : 200f), ow = Art.Measure(opts[i]) * os;
                eraseRects[i] = new Rect(ox - ow / 2f - 40f, oy - 10f, ow + 80f, Art.LineHeight * os + 20f);
                if (on)
                {
                    Color band = i == 1 ? new Color(1f, 0.35f, 0.4f, 0.28f * a) : new Color(0.55f, 0.5f, 0.95f, 0.25f * a);
                    GUI.color = band; GUI.DrawTexture(eraseRects[i], CelesteMenus.MenuFx.FadeBoth);
                }
                float lift = on ? -3f - Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 2f : 0f;
                Color c = !on ? new Color(0.8f, 0.8f, 0.85f, a) : i == 1 ? new Color(1f, 0.45f, 0.45f, a) : new Color(0.6f, 1f, 0.6f, a);
                Art.DrawOutlined(opts[i], ox - ow / 2f, oy + lift, os, c, new Color(0f, 0f, 0f, 0.75f * a), 2.5f);
            }
            GUI.color = Color.white;
        }

        void TickOld()
        {
            if (Art == null || Art.Image("fileselect/card") == null) return;
            float dt = Time.unscaledDeltaTime;
            var es = UnityEngine.EventSystems.EventSystem.current;
            GameObject sel = es != null ? es.currentSelectedGameObject : null;
            if ((findIn -= dt) <= 0f)
            {
                findIn = 0.5f;   // twice a second is plenty (user 2026-10-14: less laggy)
                foreach (SaveSlotButton b in FindObjectsByType<SaveSlotButton>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (!slots.ContainsKey(b)) slots[b] = new Slot { Button = b };
            }
            var gone = new List<SaveSlotButton>();
            ordered.Clear();
            bool anyOther = false;
            foreach (var kv in slots)
            {
                Slot s = kv.Value; SaveSlotButton b = kv.Key;
                if (b == null) { gone.Add(kv.Key); continue; }
                s.Ours = false;
                if (!b.isActiveAndEnabled) continue;
                float vis = b.myCanvasGroup != null ? b.myCanvasGroup.alpha : 1f;
                if (vis < 0.3f) continue;
                if (Array.IndexOf(Drawn, b.State) >= 0) { s.Ours = true; ordered.Add(s); }
                else if (b.State != SaveSlotButton.SlotState.Hidden && b.State != SaveSlotButton.SlotState.OperationInProgress) anyOther = true;
            }
            foreach (var g in gone) slots.Remove(g);
            ordered.Sort((x, y) => x.Button.SaveSlotIndex.CompareTo(y.Button.SaveSlotIndex));
            // an erase prompt (or a broken save, a restore) brings Silksong's own screen back, so you see what you confirm
            active = ordered.Count > 0 && !anyOther;
            Showing = active;
            screenA = Mathf.MoveTowards(screenA, active ? 1f : 0f, dt / 0.25f);
            // the mouse goes to the Celeste cards while they're up, not to Silksong's hidden slots underneath
            foreach (var kv in slots) if (kv.Key != null && kv.Key.myCanvasGroup != null) kv.Key.myCanvasGroup.blocksRaycasts = !active;
            if (!active) return;
            if (!logged) { logged = true; Plugin.Log.LogInfo("Celeste file select: the save files as Celeste's"); }

            // the chosen file: Silksong's selection when it's on a file, else the one kept here
            for (int i = 0; i < ordered.Count; i++) if (sel == ordered[i].Button.gameObject) index = i;
            index = Mathf.Clamp(index, 0, ordered.Count - 1);

            // up and down move between them, top to bottom as in Celeste; the mouse picks the card it's over
            var kb = UnityEngine.InputSystem.Keyboard.current;
            InputHandler ih = InputHandler.Instance;
            HeroActions a = ih != null ? ih.inputActions : null;
            bool up = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (a != null && a.Up.WasPressed);
            bool down = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (a != null && a.Down.WasPressed);
            int was = index;
            if (up) index = (index + ordered.Count - 1) % ordered.Count;
            if (down) index = (index + 1) % ordered.Count;
            var m = UnityEngine.InputSystem.Mouse.current;
            bool click = false;
            if (m != null)
            {
                Vector2 gp = MenuMouse.Gui();
                for (int i = 0; i < ordered.Count; i++)
                {
                    Rect cr;
                    if (cardRects.TryGetValue(ordered[i].Button, out cr) && cr.Contains(gp))
                    {
                        if (m.delta.ReadValue().sqrMagnitude > 0.5f) index = i;
                        if (m.leftButton.wasPressedThisFrame) { index = i; click = true; }
                    }
                }
            }
            SaveSlotButton chosen = ordered[index].Button;
            if (index != was) Sfx.Play(up ? Sounds.MenuRolloverUp.Id : Sounds.MenuRolloverDown.Id);
            if (es != null && sel != chosen.gameObject) es.SetSelectedGameObject(chosen.gameObject);
            if (click && es != null) UnityEngine.EventSystems.ExecuteEvents.Execute(chosen.gameObject, new UnityEngine.EventSystems.BaseEventData(es), UnityEngine.EventSystems.ExecuteEvents.submitHandler);

            // erase: Silksong's own erase button for the chosen file; its prompts then show in Silksong's look
            bool erase = kb != null && kb.deleteKey.wasPressedThisFrame;
            try { var dev = InControl.InputManager.ActiveDevice; if (dev != null && dev.Action3.WasPressed) erase = true; } catch (Exception) { }
            if (erase && chosen.State == SaveSlotButton.SlotState.SavePresent && chosen.clearSaveButton != null && es != null)
            {
                var target = chosen.clearSaveButton.GetComponentInChildren<Selectable>(true);
                if (target != null) { es.SetSelectedGameObject(target.gameObject); UnityEngine.EventSystems.ExecuteEvents.Execute(target.gameObject, new UnityEngine.EventSystems.BaseEventData(es), UnityEngine.EventSystems.ExecuteEvents.submitHandler); }
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                Slot s = ordered[i];
                bool selected = i == index;
                if (selected && s.Sel < 0.5f) s.ShineT = 0f;
                s.Sel = Mathf.MoveTowards(s.Sel, selected ? 1f : 0f, dt / 0.18f);
                if (s.ShineT >= 0f && (s.ShineT += dt) > 0.6f) s.ShineT = -1f;
            }
        }

        static void Hide(CanvasGroup cg) { if (cg != null) cg.alpha = 0f; }

        // the slot's rectangle on screen, in the 1080p units OnGUI draws in
        static Rect ScreenRect(RectTransform rt)
        {
            if (rt == null) return new Rect();
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            float s = Screen.height / 1080f;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]), c = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return new Rect(a.x / s, (Screen.height - c.y) / s, (c.x - a.x) / s, (c.y - a.y) / s);
        }

        // where each card goes: stacked top to bottom down the middle of the screen
        Rect Layout(int i, int n, float sw)
        {
            float ch = n <= 3 ? 250f : 205f, gap = n <= 3 ? 40f : 26f;
            float total = n * ch + (n - 1) * gap, top = (1080f - total) / 2f - 20f;
            return new Rect(sw / 2f - 10f, top + i * (ch + gap), 20f, ch / 1.1f);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || Art == null || screenA <= 0f) return;
            GuiArt.Img card = Art.Image("fileselect/card"), ticket = Art.Image("fileselect/ticket");
            if (card == null) return;
            float s = Screen.height / 1080f, sw = Screen.width / s;
            Matrix4x4 old = GUI.matrix;
            GUI.depth = AssistMode.IsOpen ? 3 : -6;   // over Silksong's menu: this is the screen now (under Assist Options)
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            Background(sw, screenA);
            float ce = confirmT * confirmT * (3f - 2f * confirmT);   // smoothstep
            Slot pickedSlot = index < ordered.Count ? ordered[index] : null;
            for (int i = 0; i < ordered.Count; i++)
            {
                Rect lr = Layout(i, ordered.Count, sw);
                // in the confirm view the chosen file rises to the top, as in Celeste
                if (ordered[i] == pickedSlot && ce > 0f)
                {
                    var topR = new Rect(sw / 2f - 10f, 170f, 20f, 250f / 1.1f);
                    lr = new Rect(Mathf.Lerp(lr.x, topR.x, ce), Mathf.Lerp(lr.y, topR.y, ce), lr.width, Mathf.Lerp(lr.height, topR.height, ce));
                }
                ordered[i].Gui = lr;
            }
            // the unselected ones first, so the chosen file sits on top as it slides out
            var order = new List<Slot>(ordered);
            order.Sort((x, y) => (x == pickedSlot ? 2f : x.Sel).CompareTo(y == pickedSlot ? 2f : y.Sel));
            cardRects.Clear();
            foreach (Slot sl in order)
            {
                if (sl.Button == null) continue;
                cardAlpha = sl == pickedSlot ? 1f : 1f - ce;   // the others fade away in the confirm view
                if (cardAlpha > 0.01f) DrawFile(sl, card, ticket);
            }
            cardAlpha = 1f;
            if (ce > 0.01f) Options(sw, ce * screenA);
            Hints(sw, screenA);
            if (pickedSlot != null && pickedSlot.Button != null) EraseBox(sw, pickedSlot.Button);
            GUI.matrix = old;
            GUI.color = Color.white;
        }

        // Celeste's file select sky: night blue fading to violet at the horizon, stars twinkling, snow drifting down
        // and the mountains in layers (Celeste's own background is its 3D mountain, so this paints its look)
        internal static void Background(float sw, float a)
        {
            // the painted sky and mountains: one picture, made once
            float t = Time.unscaledTime;
            // the picture drifts very slowly, so the mountain feels alive (one texture, no cost)
            float zoom = 1.04f + Mathf.Sin(t * 0.05f) * 0.01f, dx = Mathf.Sin(t * 0.031f) * sw * 0.012f, dy = Mathf.Sin(t * 0.043f) * 6f;
            GUI.color = new Color(1f, 1f, 1f, a); GUI.DrawTexture(new Rect((sw - sw * zoom) / 2f + dx, (1080f - 1080f * zoom) / 2f + dy, sw * zoom, 1080f * zoom), Sky());
            // now and then a shooting star crosses the upper sky
            float sp0 = Mathf.Repeat(t, 11f);
            if (sp0 < 0.9f)
            {
                int n0 = Mathf.FloorToInt(t / 11f);
                float k0 = sp0 / 0.9f, sx0 = (0.15f + Hash(n0, 31) * 0.6f) * sw, sy0 = 60f + Hash(n0, 32) * 220f;
                Vector2 head = new Vector2(sx0 + k0 * 520f, sy0 + k0 * 190f);
                Matrix4x4 m0 = GUI.matrix;
                GUIUtility.RotateAroundPivot(20f, head);
                GUI.color = new Color(1f, 0.95f, 1f, a * Mathf.Sin(k0 * Mathf.PI) * 0.9f);
                GUI.DrawTexture(new Rect(head.x - 170f, head.y - 1.5f, 170f, 3f), CelesteMenus.MenuFx.FadeIn);
                GUI.matrix = m0;
            }
            // a few bright stars twinkling on top
            for (int i = 0; i < 24; i++)
            {
                float x = Hash(i, 1) * sw, y = Hash(i, 2) * 560f, tw = 0.5f + 0.5f * Mathf.Sin(t * (1f + Hash(i, 3) * 2f) + i), sz = 2f + Hash(i, 4) * 2.5f;
                GUI.color = new Color(0.92f, 0.94f, 1f, a * (0.2f + 0.7f * tw));
                GUI.DrawTexture(new Rect(x, y, sz, sz), Texture2D.whiteTexture);
            }
            // soft snow drifting down and across, as on Celeste's mountain
            Texture2D fl = Flake();
            for (int i = 0; i < 40; i++)
            {
                // depth: near flakes are bigger, faster and brighter, far ones small, slow and faint
                float depth = Hash(i, 8), sz = 3f + depth * depth * 11f, sp = 22f + depth * 75f, y = Mathf.Repeat(Hash(i, 6) * 1080f + t * sp, 1110f) - 15f;
                float x = Mathf.Repeat(Hash(i, 7) * sw + Mathf.Sin(t * 0.6f + i) * (15f + depth * 30f) - t * (8f + depth * 22f), sw);
                GUI.color = new Color(1f, 1f, 1f, a * (0.18f + depth * 0.55f));
                GUI.DrawTexture(new Rect(x, y, sz, sz), fl);
            }
            GUI.color = Color.white;
        }

        // ---- the background, painted once into one picture (user 2026-10-14: less laggy, and nicer) --------------
        static Texture2D sky, flake;
        // Celeste's mountain at night (user's screenshot): a purple-blue sky, dark peaks either side, the big icy
        // mountain in the middle in flat low-poly faces lit from the left, soft clouds around its foot and cyan crystals
        // glowing on its lower slopes. Celeste's own is a 3D model, so this paints it once into a picture.
        // painted on a worker thread when the game starts, so opening the screen never hitches
        static volatile Color[] skyPixels;
        static bool skyStarted;
        static void StartSky()
        {
            if (skyStarted) return;
            skyStarted = true;
            new System.Threading.Thread(() => { try { skyPixels = PaintSky(); } catch (Exception e) { Plugin.Log.LogWarning("file select sky: " + e.Message); } }) { IsBackground = true }.Start();
        }
        static Texture2D Sky()
        {
            if (sky != null) return sky;
            StartSky();
            Color[] ready = skyPixels;
            if (ready == null) return Texture2D.blackTexture;   // a moment at the very start: plain dark until it's done
            sky = new Texture2D(SkyW, SkyH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            sky.SetPixels(ready); sky.Apply(false, true);
            skyPixels = null;
            return sky;
        }
        const int SkyW = 1280, SkyH = 720;
        static float HashT(int i, int salt) { double v = Math.Sin(i * 12.9898 + salt * 78.233) * 43758.5453; return (float)(v - Math.Floor(v)); }
        static Color[] PaintSky()
        {
            const int W = SkyW, H = SkyH;
            var px = new Color[W * H];
            Color top = new Color(0.05f, 0.06f, 0.19f), mid = new Color(0.15f, 0.13f, 0.38f), bot = new Color(0.44f, 0.29f, 0.58f);
            // the facets: points scattered over the mountain; each pixel takes the shade of its nearest point
            var rnd = new System.Random(7);
            var seeds = new List<Vector2>();
            var shade = new List<float>();
            for (int i = 0; i < 70; i++)
            {
                float sy = (float)rnd.NextDouble(), sx = 0.5f + ((float)rnd.NextDouble() - 0.5f) * (0.08f + sy * 0.75f);
                seeds.Add(new Vector2(sx * W, (0.08f + sy * 0.95f) * H));
                shade.Add((float)rnd.NextDouble());
            }
            for (int y = 0; y < H; y++)
            {
                float fy = y / (float)(H - 1);   // 0 = top of the screen
                Color skyC = fy < 0.45f ? Color.Lerp(top, mid, fy / 0.45f) : Color.Lerp(mid, bot, (fy - 0.45f) / 0.55f);
                for (int x = 0; x < W; x++)
                {
                    float fx = x / (float)(W - 1);
                    Color p = skyC;
                    // an aurora: a soft teal-to-pink ribbon waving across the upper sky
                    float band = 0.2f + Mathf.Sin(fx * 5.5f + 0.8f) * 0.06f + Mathf.Sin(fx * 13f) * 0.015f;
                    float au = Mathf.Exp(-(fy - band) * (fy - band) * 160f) * (0.35f + Mathf.PerlinNoise(fx * 9f, 0.5f) * 0.8f);
                    au += Mathf.Exp(-(fy - band + 0.05f) * (fy - band + 0.05f) * 400f) * Mathf.PerlinNoise(fx * 22f, 4.2f) * 0.5f;   // fine curtains above
                    p += Color.Lerp(new Color(0.15f, 0.55f, 0.5f), new Color(0.55f, 0.25f, 0.55f), fx) * au * 0.7f;
                    // the moon, upper right, with a soft halo
                    float mdx = (fx - 0.82f) * W / H, mdy = fy - 0.16f, md = Mathf.Sqrt(mdx * mdx + mdy * mdy);
                    p += new Color(0.5f, 0.5f, 0.75f) * Mathf.Exp(-md * 9f) * 0.35f;
                    if (md < 0.045f) p = Color.Lerp(p, new Color(0.96f, 0.95f, 1f) - new Color(0.06f, 0.06f, 0.03f) * Mathf.PerlinNoise(fx * 90f, fy * 90f), Mathf.Clamp01((0.045f - md) * H * 0.9f));
                    // dark distant peaks, left and right
                    float far = 0.72f - Mathf.PerlinNoise(fx * 4.2f, 1.7f) * 0.22f - Mathf.Max(0f, 0.25f - Mathf.Abs(fx - 0.12f)) * 0.5f - Mathf.Max(0f, 0.2f - Mathf.Abs(fx - 0.88f)) * 0.35f;
                    if (fy > far) p = Color.Lerp(p, Color.Lerp(new Color(0.17f, 0.2f, 0.4f), new Color(0.12f, 0.14f, 0.3f), (fy - far) * 3f), Mathf.Clamp01((fy - far) * H * 0.5f));
                    // clouds: soft pale violet puffs low in the sky
                    float cl = Mathf.PerlinNoise(fx * 7f, fy * 9f + 3f) * Mathf.Clamp01(1f - Mathf.Abs(fy - 0.68f) * 7f);
                    if (cl > 0.45f) p = Color.Lerp(p, new Color(0.62f, 0.6f, 0.82f), Mathf.Clamp01((cl - 0.45f) * 4f) * 0.55f);
                    // the big mountain: a peak at the top centre widening to the bottom
                    float half = 0.015f + (fy - 0.09f) * 0.55f;
                    float edge = half - Mathf.Abs(fx - 0.5f + Mathf.Sin(fy * 9f) * 0.012f);
                    if (fy > 0.09f && edge > 0f)
                    {
                        int best = 0; float bd = float.MaxValue;
                        for (int i = 0; i < seeds.Count; i++) { float d = (seeds[i] - new Vector2(x, y)).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
                        Vector2 sp = seeds[best];
                        float lit = Mathf.Clamp01(0.55f + (0.5f - sp.x / W) * 1.8f + (shade[best] - 0.5f) * 0.35f);   // lit from the left
                        Color ice = Color.Lerp(new Color(0.24f, 0.45f, 0.78f), new Color(0.62f, 0.85f, 0.98f), lit);
                        ice = Color.Lerp(ice, new Color(0.3f, 0.5f, 0.85f), Mathf.Clamp01((fy - 0.6f) * 1.4f));   // deeper blue lower down
                        // a snowcap, ragged where it meets the ice
                        // the snowcap follows the facets, so its edge is as low-poly as the ice
                        float cap = 0.2f + (shade[best] - 0.5f) * 0.05f;
                        if (sp.y / H < cap) ice = Color.Lerp(new Color(0.78f, 0.8f, 0.97f), Color.white, lit * 0.9f);
                        // moonlight rim along the right edge, faint
                        if (fx > 0.5f) ice += new Color(0.2f, 0.2f, 0.3f) * Mathf.Clamp01(1f - edge * W * 0.08f) * 0.6f;
                        p = Color.Lerp(p, ice, Mathf.Clamp01(edge * W * 0.8f));   // a smooth outline
                    }
                    // crystals: cyan glows on the lower slopes
                    for (int c = 0; c < 14; c++)
                    {
                        float cx2 = 0.3f + Hash(c, 21) * 0.4f, cy2 = 0.76f + Hash(c, 22) * 0.22f, cs = 0.7f + Hash(c, 23) * 0.6f;
                        float dx = (fx - cx2) * 34f / cs, dy = (fy - cy2) * 13f / cs;
                        float shard = 1f - (Mathf.Abs(dx) * 2.2f + Mathf.Abs(dy));   // a slim diamond
                        if (shard > 0f) p = Color.Lerp(p, dx < 0f ? new Color(0.7f, 1f, 0.98f) : new Color(0.35f, 0.85f, 0.9f), Mathf.Clamp01(shard * 6f) * 0.95f);   // two-tone faces
                        float glow = Mathf.Exp(-(dx * dx * 0.25f + dy * dy) * 0.5f);
                        p += new Color(0.1f, 0.35f, 0.35f) * glow * 0.3f;
                    }
                    // faint stars in the sky
                    if (fy < 0.6f && Hash(x * 131 + y * 7, 3) > 0.9985f) p = Color.Lerp(p, Color.white, 0.35f + Hash(x, y) * 0.4f);
                    // mist rolling along the bottom
                    float mist = Mathf.Clamp01((fy - 0.8f) * 5f) * (0.5f + Mathf.PerlinNoise(fx * 5f, fy * 3f) * 0.6f);
                    p = Color.Lerp(p, new Color(0.5f, 0.42f, 0.7f), mist * 0.45f);
                    // a soft vignette to frame it, then a touch of noise so the gradients don't band
                    float vx = fx - 0.5f, vy = fy - 0.5f;
                    p *= 1f - (vx * vx * 0.9f + vy * vy * 1.1f) * 0.75f;
                    p += new Color(1f, 1f, 1f) * ((HashT(x * 7 + y * 1031, 5) - 0.5f) / 160f);
                    p.a = 1f;
                    px[(H - 1 - y) * W + x] = p;   // texture rows run upward
                }
            }
            return px;
        }

        static Texture2D SkyOld()
        {
            if (sky != null) return sky;
            const int W = 960, H = 540;
            var px = new Color32[W * H];
            Color top = new Color(0.04f, 0.07f, 0.19f), mid = new Color(0.15f, 0.16f, 0.4f), bot = new Color(0.42f, 0.29f, 0.55f);
            // three ridges, far to near: each lighter at its snowy crest, fading into a mist above the next
            float[] baseY = { 0.6f, 0.71f, 0.82f }, height = { 0.17f, 0.2f, 0.17f }, freq = { 0.0062f, 0.0045f, 0.0072f };
            Color[] body = { new Color(0.3f, 0.27f, 0.53f), new Color(0.21f, 0.17f, 0.4f), new Color(0.12f, 0.09f, 0.25f) };
            var crest = new float[3, W];   // each ridge's height per column, worked out once
            for (int r = 0; r < 3; r++) for (int x = 0; x < W; x++)
                    crest[r, x] = baseY[r] - (Mathf.PerlinNoise(x * freq[r] * 2f, r * 9.1f) * 0.72f + Mathf.PerlinNoise(x * freq[r] * 6.3f, r * 4.3f) * 0.28f) * height[r];
            for (int y = 0; y < H; y++)
            {
                float fy = 1f - y / (float)(H - 1);   // texture rows run upward; fy 0 = top of the screen
                Color c = fy < 0.6f ? Color.Lerp(top, mid, fy / 0.6f) : Color.Lerp(mid, bot, (fy - 0.6f) / 0.4f);
                for (int x = 0; x < W; x++)
                {
                    Color p = c;
                    // a soft glow low in the sky behind the mountains, like Celeste's dusk
                    float gx = (x - W * 0.62f) / (W * 0.5f), gy = (fy - 0.62f) / 0.25f;
                    p = Color.Lerp(p, new Color(0.62f, 0.45f, 0.72f), Mathf.Clamp01(0.35f - (gx * gx + gy * gy) * 0.35f));
                    for (int r = 0; r < 3; r++)
                    {
                        float ridge = crest[r, x];
                        if (fy >= ridge)
                        {
                            float d = (fy - ridge) * H;   // pixels below the crest
                            Color m = body[r];
                            if (d < 3f) m = Color.Lerp(new Color(0.8f, 0.78f, 0.95f), m, d / 3f);   // a snowy rim
                            else m = Color.Lerp(m * 1.15f, m, Mathf.Clamp01(d / 40f));          // lit a little near the top
                            p = Color.Lerp(p, m, Mathf.Clamp01((fy - ridge) * H * 0.9f));      // a smooth edge
                        }
                        else if (ridge - fy < 0.04f) p = Color.Lerp(p, body[r] * 1.3f, (1f - (ridge - fy) / 0.04f) * 0.25f);   // mist just above
                    }
                    // a few faint stars that stay put
                    if (fy < 0.55f && Hash(x * 131 + y * 7, 3) > 0.9985f) p = Color.Lerp(p, Color.white, 0.6f);
                    p.a = 1f;
                    px[y * W + x] = p;
                }
            }
            sky = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            sky.SetPixels32(px); sky.Apply(false, true);
            return sky;
        }
        // a soft round snowflake
        static Texture2D Flake()
        {
            if (flake != null) return flake;
            const int n = 16;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f)) / 7.5f;
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d * d));
                }
            flake = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            flake.SetPixels32(px); flake.Apply(false, true);
            return flake;
        }

        static float Hash(int i, int salt) { return Mathf.Repeat(Mathf.Sin(i * 12.9898f + salt * 78.233f) * 43758.5453f, 1f); }

        float cardAlpha = 1f;

        // Celeste's file menu under the card: "Begin", then "Assist Mode : ON/OFF" (user 2026-10-14), the chosen one
        // in Celeste's flashing highlight with a little bounce
        void Options(float sw, float a)
        {
            bool fresh = index < ordered.Count && ordered[index].Button != null && ordered[index].Button.State == SaveSlotButton.SlotState.EmptySlot;
            string[] opts = fresh ? new[] { "Begin", "Settings" } : new[] { "Begin" };
            float[] scale = { 1.0f, 0.75f };
            float y = 520f;
            float t = Time.unscaledTime;
            Color hi = (t % 0.2f) < 0.1f ? Hex(Visuals.MenuHighlightA) : Hex(Visuals.MenuHighlightB);
            for (int i = 0; i < opts.Length; i++)
            {
                float sc = scale[i], w = Art.Measure(opts[i]) * sc, x = sw / 2f - w / 2f;
                bool sel = i == confirmIndex;
                float lift = sel ? -Mathf.Abs(Mathf.Sin(t * 5f)) * 3f : 0f;
                Color c = sel ? hi : Color.white; c.a = a;
                optionRects[i] = new Rect(x - 30f, y - 6f, w + 60f, Art.LineHeight * sc + 12f);
                Art.DrawOutlined(opts[i], x, y + lift, sc, c, new Color(0f, 0f, 0f, a * 0.7f), 3f);
                y += Art.LineHeight * sc + 26f;
            }
        }
        static Color Hex(string h) { Color c; return ColorUtility.TryParseHtmlString("#" + h, out c) ? c : Color.white; }

        // bottom right, as Celeste: what the buttons do
        void Hints(float sw, float a)
        {
            bool pad = MenuMouse.PadActive;
            string[] parts = confirmOpen ? (pad ? new[] { "Confirm  Cross", "Back  Circle" } : new[] { "Confirm  Enter", "Back  Esc" })
                : (pad ? new[] { "Confirm  Cross", "Erase  Square", "Back  Circle" } : new[] { "Confirm  Enter", "Erase  Delete / Right-click", "Back  Esc" });
            float hs = 0.5f, x = sw - 50f;
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                float w = Art.Measure(parts[i]) * hs;
                x -= w;
                Art.DrawOutlined(parts[i], x, 1080f - 70f, hs, new Color(1f, 1f, 1f, a), new Color(0f, 0f, 0f, a * 0.6f), 2f);
                x -= 50f;
            }
        }

        // As Celeste's file select (user's reference): a big card centred on the slot, Madeline's portrait under tape
        // at the left, her name, the area, and golden berries x count; the chosen file slides left and its ticket pulls
        // out to the right with the deaths (skull) and the play time. Stamps on the card for what the save has done.
        void DrawFile(Slot sl, GuiArt.Img card, GuiArt.Img ticket)
        {
            SaveSlotButton b = sl.Button;
            Rect r = sl.Gui;
            if (r.height < 10f) return;
            float vis = screenA * cardAlpha;
            float e = 1f - (1f - sl.Sel) * (1f - sl.Sel);   // eased
            float ch = r.height * 1.1f, cw = ch * card.W / card.H, k = ch / 270f;
            float cx = r.center.x - cw / 2f - e * cw * 0.32f, cy = r.center.y - ch / 2f;
            bool empty = b.State == SaveSlotButton.SlotState.EmptySlot;
            var ink = new Color(0.27f, 0.23f, 0.29f, vis);
            var sub = new Color(0.47f, 0.4f, 0.52f, vis);

            // the ticket, sliding out from behind the card's right edge
            if (ticket != null && e > 0.01f)
            {
                float th = ch * 0.9f, tw = th * ticket.W / ticket.H * 0.95f;
                float tx = cx + cw - 30f * k - tw * (1f - e), ty = cy + (ch - th) / 2f;
                GUI.color = new Color(1f, 1f, 1f, vis); GUI.DrawTexture(new Rect(tx, ty, tw, th), ticket.Tex);
                float ta = vis * Mathf.Clamp01((e - 0.6f) / 0.4f);
                if (!empty && ta > 0f)
                {
                    // top: what she carries, small; bottom: the skull with the deaths, and the time at the right
                    float ix = tx + 70f * k, s1 = 0.5f * k;
                    string ros = Txt(b.rosaryText), sh = Txt(b.shardText), comp = Txt(b.completionText);
                    string carry = (ros.Length > 0 ? ros + " Rosaries" : "") + (sh.Length > 0 ? "    " + sh + " Shards" : "");
                    Art.DrawLine(carry, ix, ty + 40f * k, s1, new Color(0.55f, 0.5f, 0.58f, ta));
                    if (comp.Length > 0)
                    {
                        // completion with a silk spool in front
                        float cy3 = ty + 40f * k + Art.LineHeight * s1, isz = Art.LineHeight * s1;
                        GUI.color = new Color(1f, 1f, 1f, ta); GUI.DrawTexture(new Rect(ix, cy3, isz, isz), FileIcons.Spool);
                        Art.DrawLine(comp, ix + isz + 8f * k, cy3, s1, new Color(0.55f, 0.5f, 0.58f, ta));
                    }
                    GuiArt.Img skull = Art.Skull;
                    float sk = 62f * k, sy = ty + th - 40f * k - sk;
                    if (skull != null) { GUI.color = new Color(1f, 1f, 1f, ta); GUI.DrawTexture(new Rect(ix, sy, sk, sk * skull.H / Math.Max(1, skull.W)), skull.Tex); }
                    Outlined("x" + Deaths(b.SaveSlotIndex), ix + sk + 12f * k, sy + sk / 2f, 0.8f * k, ta);
                    string time = Txt(b.playTimeText);
                    float ts = 0.6f * k;
                    Art.DrawLine(time, tx + tw - 50f * k - Art.Measure(time) * ts, sy + sk / 2f - Art.LineHeight * ts / 2f, ts, new Color(0.55f, 0.5f, 0.58f, ta));
                }
            }

            // the chosen card floats gently, with a warm glow behind it
            if (e > 0.01f)
            {
                cy += Mathf.Sin(Time.unscaledTime * 1.8f) * 4f * e;
                GUI.color = new Color(1f, 0.85f, 0.6f, 0.28f * e * vis);
                GUI.DrawTexture(new Rect(cx - cw * 0.12f, cy - ch * 0.35f, cw * 1.24f, ch * 1.7f), CelesteMenus.MenuFx.Glow);
            }
            // the card with a soft shadow, Celeste's shine once when picked
            GUI.color = new Color(0f, 0f, 0f, 0.3f * vis); GUI.DrawTexture(new Rect(cx + 8f, cy + 10f, cw, ch), card.Tex);
            GUI.color = new Color(1f, 1f, 1f, vis); GUI.DrawTexture(new Rect(cx, cy, cw, ch), card.Tex);
            cardRects[b] = new Rect(cx, cy, cw, ch);
            if (!empty)
            {
                // stamps, as Celeste stamps a file: a golden berry once she has one, the flag at 100%
                int berries = BossCard.GoldensOnSlot(b.SaveSlotIndex);
                GuiArt.Img gb = Art.Image("fileselect/goldberry"), flag = Art.Image("fileselect/flag");
                if (berries > 0 && gb != null) { GUI.color = new Color(1f, 1f, 1f, vis * 0.9f); GUI.DrawTexture(new Rect(cx, cy, cw, ch), gb.Tex); }
                if (Txt(b.completionText).StartsWith("100") && flag != null) { GUI.color = new Color(1f, 1f, 1f, vis * 0.9f); GUI.DrawTexture(new Rect(cx, cy, cw, ch), flag.Tex); }
            }
            if (sl.ShineT >= 0f)
            {
                int f = Mathf.Clamp((int)(sl.ShineT / 0.1f), 0, 5);
                GuiArt.Img shine = Art.Image("fileselect/cardShine00" + f);
                if (shine != null) { GUI.color = new Color(1f, 1f, 1f, vis); GUI.DrawTexture(new Rect(cx, cy, cw, ch), shine.Tex); }
            }

            // Madeline's portrait, big, under Celeste's tape
            GuiArt.Img portrait = Art.Image("portrait/madeline"), tape = Art.Image("fileselect/portraitOverlay");
            float ps = 200f * k, px = cx + 52f * k, py = cy + ch / 2f - ps / 2f;
            if (portrait != null) { GUI.color = new Color(1f, 1f, 1f, vis * (empty ? 0.4f : 1f)); GUI.DrawTexture(new Rect(px, py, ps, ps), portrait.Tex); }
            if (tape != null) { float tsz = ps * 1.36f; GUI.color = new Color(1f, 1f, 1f, vis); GUI.DrawTexture(new Rect(px + ps / 2f - tsz / 2f, py + ps / 2f - tsz / 2f, tsz, tsz), tape.Tex); }

            // the words, centred in the space right of the portrait
            float mid = (px + ps + cx + cw - 40f * k) / 2f;
            float ns = 0.9f * k, as2 = 0.62f * k;
            if (empty)
            {
                // a plus in a ring above the words, for a fresh file
                float pr = 46f * k;
                GUI.color = new Color(ink.r, ink.g, ink.b, vis * 0.55f); GUI.DrawTexture(new Rect(mid - pr / 2f, cy + ch / 2f - Art.LineHeight * ns / 2f - pr - 8f * k, pr, pr), FileIcons.NewFile);
                Centered("New Game", mid, cy + ch / 2f - Art.LineHeight * ns / 2f, ns, ink);
                Centered("FILE " + b.SaveSlotIndex, mid, cy + ch / 2f + Art.LineHeight * ns * 0.55f, as2 * 0.8f, sub);
            }
            else
            {
                float y0 = cy + ch * 0.16f;
                Centered("Madeline", mid, y0, ns, ink);
                string where = Txt(b.locationText);
                float w2 = as2; while (w2 > 0.25f * k && Art.Measure(where) * w2 > cw * 0.55f) w2 -= 0.02f * k;
                // the area with a small map pin in front
                float pin = Art.LineHeight * w2 * 0.95f, wx = mid - (Art.Measure(where) * w2 + pin + 6f * k) / 2f;
                GUI.color = new Color(sub.r, sub.g, sub.b, vis); GUI.DrawTexture(new Rect(wx, y0 + Art.LineHeight * ns, pin, pin), FileIcons.Pin);
                Art.DrawLine(where, wx + pin + 6f * k, y0 + Art.LineHeight * ns, w2, sub);
                // golden berries x count, the number outlined as Celeste writes counts
                int berries = BossCard.GoldensOnSlot(b.SaveSlotIndex);
                GuiArt.Img berry = BossCard.PixelBerry() ?? Art.Image("collectables/goldberry");
                string n = "x" + berries;
                float bs = 70f * k, ns2 = 0.85f * k, rowW = bs + 8f * k + Art.Measure(n) * ns2, rx = mid - rowW / 2f, ry = y0 + Art.LineHeight * (ns + w2) + 6f * k;
                if (berry != null) { GUI.color = new Color(1f, 1f, 1f, vis * (berries > 0 ? 1f : 0.35f)); GUI.DrawTexture(new Rect(rx, ry, bs, bs), berry.Tex); }
                Outlined(n, rx + bs + 8f * k, ry + bs / 2f, ns2, vis);
            }
            GUI.color = Color.white;
        }

        // Silkeste's own little icons for the file cards, drawn once in code (no art files needed)
        static class FileIcons
        {
            static Texture2D spool, pin, newFile;
            // a silk spool: a ring with thread wound round it, for completion
            public static Texture2D Spool { get { return spool ?? (spool = Make((x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                if (d > 0.9f) return Color.clear;
                Color c = d > 0.72f ? new Color(0.6f, 0.5f, 0.62f) : (Mathf.Repeat((x + y) * 6f, 1f) < 0.5f ? new Color(0.92f, 0.86f, 0.96f) : new Color(0.8f, 0.72f, 0.88f));
                if (d < 0.18f) c = new Color(0.6f, 0.5f, 0.62f);
                c.a = Mathf.Clamp01((0.9f - d) * 25f); return c;
            })); } }
            // a map pin (white, tinted when drawn)
            public static Texture2D Pin { get { return pin ?? (pin = Make((x, y) =>
            {
                float d = Mathf.Sqrt(x * x + (y + 0.3f) * (y + 0.3f));
                float a = d < 0.55f && d > 0.22f ? 1f : 0f;
                if (y > 0.1f && Mathf.Abs(x) < (0.95f - y) * 0.55f) a = 1f;
                if (d <= 0.22f) a = 0f;
                return new Color(1f, 1f, 1f, a);
            })); } }
            // a plus in a ring, for an empty file (white, tinted when drawn)
            public static Texture2D NewFile { get { return newFile ?? (newFile = Make((x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                float a = Mathf.Abs(d - 0.85f) < 0.07f ? 1f : 0f;
                if ((Mathf.Abs(x) < 0.08f && Mathf.Abs(y) < 0.45f) || (Mathf.Abs(y) < 0.08f && Mathf.Abs(x) < 0.45f)) a = 1f;
                return new Color(1f, 1f, 1f, a);
            })); } }
            // f gets x and y from -1 to 1 (y down) and gives the colour; 4x supersampled so edges stay smooth
            static Texture2D Make(Func<float, float, Color> f)
            {
                const int n = 64;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int py = 0; py < n; py++) for (int pxi = 0; pxi < n; pxi++)
                    {
                        Color sum = Color.clear;
                        for (int s = 0; s < 4; s++)
                        {
                            float fx = (pxi + 0.25f + (s & 1) * 0.5f) / n * 2f - 1f, fy = (py + 0.25f + (s >> 1) * 0.5f) / n * 2f - 1f;
                            Color c = f(fx, fy); sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                        }
                        sum /= 4f;
                        px[(n - 1 - py) * n + pxi] = sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
                    }
                t.SetPixels(px); t.Apply(false, true);
                return t;
            }
        }

        static void Centered(string s, float mid, float y, float scale, Color c) { Art.DrawLine(s, mid - Art.Measure(s) * scale / 2f, y, scale, c); }
        // white with a dark outline, vertically centred on y (Celeste's counters)
        static void Outlined(string s, float x, float y, float scale, float a)
        {
            Art.DrawOutlined(s, x, y - Art.LineHeight * scale / 2f, scale, new Color(1f, 1f, 1f, a), new Color(0.2f, 0.16f, 0.2f, a), 3f * scale / 0.8f);
        }

        // deaths per save, from the death counter's file (read again at most once a second)
        static Dictionary<int, int> deaths = new Dictionary<int, int>();
        static float deathsReadAt = -10f;
        static int Deaths(int slot)
        {
            if (Time.unscaledTime - deathsReadAt > 1f)
            {
                deathsReadAt = Time.unscaledTime;
                var d = new Dictionary<int, int>();
                try
                {
                    string f = System.IO.Path.Combine(DeathCounter.DataDir ?? "", "deaths.cfg");
                    if (System.IO.File.Exists(f))
                        foreach (string line in System.IO.File.ReadAllLines(f))
                        {
                            string[] kv = line.Split('=');
                            int sl, n;
                            if (kv.Length == 2 && int.TryParse(kv[0], out sl) && int.TryParse(kv[1], out n)) d[sl] = n;
                        }
                }
                catch (Exception) { }
                deaths = d;
            }
            int v; return deaths.TryGetValue(slot, out v) ? v : 0;
        }
        static string Txt(Text t) { return t != null && t.gameObject.activeInHierarchy ? (t.text ?? "").Trim() : ""; }
    }
}
