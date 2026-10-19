using System;
using System.Collections.Generic;
using System.IO;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Celeste's chapter postcard (sheets/postcards.json, visuals.postcard_*) telling the player how Madeline plays:
    /// the Tutorial each time a save is loaded, once Madeline can move; right after Cling Grip, Faydown Cloak or Drifter's Cloak is
    /// picked up (Silksong's own item message still describes Hornet's moves); and on F2 any time. Madeline stands
    /// still while one is up.
    /// </summary>
    public class PostcardView : MonoBehaviour
    {
        public static GuiArt Art;

        enum Phase { None, In, Shown, Out }
        Phase phase;
        PostcardRow card;
        List<string> lines;
        float t, shownFor, controlFor, textScale = Visuals.PostcardTextScale;
        readonly List<PostcardRow> queue = new List<PostcardRow>();
        bool relinquished, introShown;   // introShown: this load of this save
        public static string DataDir;
        static string TutorialFile { get { return Path.Combine(DataDir ?? "", "tutorial.cfg"); } }
        static bool TutorialDue(int slot, float playTime)
        {
            var seen = new Dictionary<int, float>();
            try
            {
                if (DataDir != null && File.Exists(TutorialFile))
                    foreach (string line in File.ReadAllLines(TutorialFile))
                    {
                        string[] kv = line.Split('=');
                        int s; float v;
                        if (kv.Length == 2 && int.TryParse(kv[0], out s) && float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) seen[s] = v;
                    }
                float was;
                // a save this young is a new file (several new files in one slot fooled the play-time check): always
                if (playTime >= 300f && seen.TryGetValue(slot, out was) && playTime + 5f >= was) return false;
                seen[slot] = playTime;
                var lines = new List<string>();
                foreach (var kv in seen) lines.Add(kv.Key + "=" + kv.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                Directory.CreateDirectory(DataDir);
                File.WriteAllLines(TutorialFile, lines.ToArray());
            }
            catch (Exception e) { Plugin.Log.LogWarning("Tutorial: " + e.Message); }
            return true;
        }
        /// <summary>The Tutorial card of this load has been read and closed (the speedrun timer waits for it).</summary>
        public static bool TutorialDone;
        int baselineSlot = -1;
        readonly Dictionary<string, bool> had = new Dictionary<string, bool>();

#if TESTHARNESS
        public static PostcardView Instance;
        void Awake() { Instance = this; }
        public bool TestShowing { get { return phase != Phase.None; } }
        public void TestDismiss() { if (phase == Phase.Shown || phase == Phase.In) Close(); }
#endif

        void Update()
        {
            if (!Plugin.Ready || Art == null || Art.Postcard == null || Art.Postcard.Tex == null) return;
            HeroController h = HeroController.SilentInstance;
            GameManager gm = GameManager.SilentInstance;
            if (h == null || gm == null) { Hide(h); introShown = false; TutorialDone = false; return; }   // back at the menu: the next load shows it again
            float dt = Time.unscaledDeltaTime;

            bool playing = gm.GameState == GlobalEnums.GameState.PLAYING && !gm.isPaused && !h.cState.transitioning && !h.cState.dead && !h.cState.hazardDeath;
            bool hasControl = playing && !h.controlReqlinquished;
            controlFor = hasControl ? controlFor + dt : 0f;

            WatchUnlocks(gm);
            // The Tutorial once she can move: not resting at a bench, not in a cutscene or the opening binding.
            // The Tutorial: once per save, at the start of a new file (user 2026-10-12: not on every load).
            // tutorial.cfg keeps the play time each slot had when it was shown; a slot whose save now has less play
            // time than that holds a new file, which gets it again.
            if (!introShown && hasControl && !h.playerData.atBench)
            {
                introShown = true;
                if (TutorialDue(gm.profileID, gm.PlayTime)) Enqueue(Postcards.Intro, false);
                else TutorialDone = true;
            }

            var kb = UnityEngine.InputSystem.Keyboard.current;   // inputs.help
            if (AssistMode.KeyPressed(AssistOptions.KeyHelp) && playing)
            {
                if (phase == Phase.Shown && card.Id == Postcards.Help.Id) Close();
                else if (phase == Phase.None) Enqueue(Postcards.Help, true);
            }
#if TESTHARNESS
            // (F8 used to preview the Swift Step card; it now previews achievement banners, user 2026-10-13)
#endif

            if (phase == Phase.None && queue.Count > 0 && hasControl && (queue[0].Id == Postcards.Help.Id || controlFor >= Visuals.PostcardShowDelay))
                Open(h);

            switch (phase)
            {
                case Phase.In:
                    t += dt / Visuals.PostcardInTime;
                    if (t >= 1f) { phase = Phase.Shown; shownFor = 0f; }
                    break;
                case Phase.Shown:
                    shownFor += dt;
                    if (shownFor >= Visuals.PostcardButtonDelay && ConfirmPressed(kb)) Close();
                    break;
                case Phase.Out:
                    t += dt / Visuals.PostcardOutTime;
                    if (t >= 1f) { if (card.Id == Postcards.Intro.Id) TutorialDone = true; phase = Phase.None; card = default(PostcardRow); Release(h); }
                    break;
            }
        }

        void Enqueue(PostcardRow row, bool front)
        {
            foreach (PostcardRow q in queue) if (q.Id == row.Id) return;
            if (front) queue.Insert(0, row); else queue.Add(row);
        }

        // hooks.unlock_watch: an unlock that turns on while playing queues its card. A save's state when it loads
        // is the baseline, so loading a save never shows cards for things already owned.
        void WatchUnlocks(GameManager gm)
        {
            PlayerData pd = PlayerData.instance;
            if (pd == null) return;
            bool fresh = gm.profileID != baselineSlot;
            if (fresh) { introShown = false; TutorialDone = false; }   // another save slot
            baselineSlot = gm.profileID;
            foreach (PostcardRow row in Postcards.All)
            {
                if (row.Unlock == "none") continue;
                bool now = MadelineController.Unlocked(UnlockRowFor(row.Unlock));
                bool before;
                if (!fresh && had.TryGetValue(row.Id, out before) && !before && now) Enqueue(row, false);
                had[row.Id] = now;
            }
        }

        static UnlockRow UnlockRowFor(string id)
        {
            foreach (UnlockRow u in Unlocks.All) if (u.Id == id) return u;
            throw new Exception("postcard unlock '" + id + "' is not in sheets/unlocks.json");
        }

        // ---- Records' postcard album (user 2026-10-16) -----------------------------------------------------------
        static string SeenFile { get { return Path.Combine(DataDir ?? "", "postcards_seen.cfg"); } }
        static HashSet<string> seenIds;
        static void LoadSeen()
        {
            if (seenIds != null) return;
            seenIds = new HashSet<string>();
            try { if (File.Exists(SeenFile)) foreach (string l in File.ReadAllLines(SeenFile)) if (l.Trim().Length > 0) seenIds.Add(l.Trim()); } catch (Exception) { }
        }
        static void MarkSeen(string id)
        {
            LoadSeen();
            if (!seenIds.Add(id)) return;
            try { File.WriteAllLines(SeenFile, new List<string>(seenIds).ToArray()); } catch (Exception) { }
        }
        /// <summary>The cards for the album: the Tutorial always, and every unlock card seen (or already earned).</summary>
        public static List<PostcardRow> Album()
        {
            LoadSeen();
            var list = new List<PostcardRow>();
            foreach (PostcardRow row in Postcards.All)
            {
                bool owned = false;
                if (row.Unlock != "none") { try { owned = PlayerData.instance != null && MadelineController.Unlocked(UnlockRowFor(row.Unlock)); } catch (Exception) { } }
                if (row.Id == Postcards.Intro.Id || seenIds.Contains(row.Id) || owned) list.Add(row);
            }
            return list;
        }
        static readonly Dictionary<string, KeyValuePair<List<string>, float>> albumLines = new Dictionary<string, KeyValuePair<List<string>, float>>();
        /// <summary>A postcard as it shows in the game, centred at c (GUI units), scaled by k.</summary>
        public static void DrawAlbumCard(PostcardRow row, Vector2 c, float k, float alpha)
        {
            if (Art == null || Art.Postcard == null || Art.Postcard.Tex == null) return;
            KeyValuePair<List<string>, float> wrapped;
            if (!albumLines.TryGetValue(row.Id, out wrapped))
            {
                float ts = Visuals.PostcardTextScale; List<string> ls = null;
                for (int i = 0; i < 12; i++)
                {
                    ls = Art.Wrap(Fill(row.Body), (Art.Postcard.W - Visuals.PostcardTextMargin) / ts);
                    if (ls.Count * Art.LineHeight * ts <= Visuals.PostcardTextMaxHeight) break;
                    ts *= 0.95f;
                }
                albumLines[row.Id] = wrapped = new KeyValuePair<List<string>, float>(ls, ts);
            }
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = old * Matrix4x4.TRS(new Vector3(c.x, c.y, 0f), Quaternion.Euler(0f, 0f, Visuals.PostcardTurnTo * Mathf.Rad2Deg), new Vector3(k, k, 1f));
            float w = Art.Postcard.W, h = Art.Postcard.H;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(-w / 2f, -h / 2f, w, h), Art.Postcard.Tex);
            Art.DrawLine(row.Title, -w / 2f + Visuals.PostcardTitleX, -h / 2f + Visuals.PostcardTitleY, Visuals.PostcardTitleScale, new Color(0f, 0f, 0f, Visuals.PostcardTitleAlpha * alpha));
            float sc = wrapped.Value, lineH = Art.LineHeight * sc, top = Visuals.PostcardTextOffsetY - wrapped.Key.Count * lineH / 2f, block = 0f;
            var ink = new Color(0f, 0f, 0f, Visuals.PostcardTextAlpha * alpha);
            foreach (string line in wrapped.Key) block = Math.Max(block, Art.Measure(line));
            foreach (string line in wrapped.Key)
            {
                float x = Visuals.PostcardLeftAligned > 0f ? -block * sc / 2f : -Art.Measure(line) * sc / 2f;
                Art.DrawLine(line, x, top, sc, ink);
                top += lineH;
            }
            GUI.matrix = old;
            GUI.color = Color.white;
        }

        void Open(HeroController h)
        {
            card = queue[0];
            queue.RemoveAt(0);
            MarkSeen(card.Id);
            // visuals.postcard_text_max_height: a long card shrinks its text until the lines fit between the title
            // and the card's printed bottom edge.
            textScale = Visuals.PostcardTextScale;
            for (int i = 0; i < 12; i++)
            {
                lines = Art.Wrap(Fill(card.Body), (Art.Postcard.W - Visuals.PostcardTextMargin) / textScale);
                if (lines.Count * Art.LineHeight * textScale <= Visuals.PostcardTextMaxHeight) break;
                textScale *= 0.95f;
            }
            phase = Phase.In; t = 0f;
            Sfx.Play(Sounds.PostcardIn.Id);
            if (!h.controlReqlinquished) { h.RelinquishControl(); relinquished = true; }   // hooks.postcard_pause
        }

        void Close()
        {
            Sfx.Play(Sounds.ButtonLowkey.Id);
            Sfx.Play(Sounds.PostcardOut.Id);
            phase = Phase.Out; t = 0f;
        }

        void Release(HeroController h)
        {
            if (relinquished && h != null) h.RegainControl();
            relinquished = false;
        }

        void Hide(HeroController h)
        {
            if (phase == Phase.None) return;
            phase = Phase.None;
            Release(h);
        }

        static bool ConfirmPressed(UnityEngine.InputSystem.Keyboard kb)
        {
            InputHandler input = InputHandler.Instance;
            if (input != null && input.inputActions != null && input.inputActions.Jump.WasPressed) return true;
            return kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
        }

        // {unlock:id} becomes "Unlocked" or where to find it.
        static string Fill(string body)
        {
            foreach (UnlockRow u in Unlocks.All)
                body = body.Replace("{unlock:" + u.Id + "}", MadelineController.Unlocked(u) ? "Unlocked" : "find " + u.SilksongUnlock);
            // {key:key_skin} and the like: the F key chosen in the settings' Controls section
            foreach (AssistRow r in AssistOptions.All)
                if (r.Section == "Controls") body = body.Replace("{key:" + r.Id + "}", AssistMode.KeyName(r));
            return body;
        }

        // Celeste.Postcard.Render / BeforeRender, in Celeste's 1920x1080 GUI units scaled to the screen height.
        void OnGUI()
        {
            if (TheoCamera.Hiding) return;   // out of Theo's photos
            if (phase == Phase.None || Art == null || Event.current.type != EventType.Repaint) return;
            float s = Screen.height / 1080f;
            Vector2 center = new Vector2(Screen.width / s / 2f, 540f);
            float p = Mathf.Clamp01(t), ease, alpha, rot;
            Vector2 pos;
            if (phase == Phase.In)
            {
                ease = 1f - (1f - p) * (1f - p) * (1f - p);   // CubeOut
                pos = center + new Vector2(0f, Visuals.PostcardRise) * (1f - ease);
                alpha = ease;
                rot = Visuals.PostcardTurnFrom + (Visuals.PostcardTurnTo - Visuals.PostcardTurnFrom) * ease;
            }
            else if (phase == Phase.Out)
            {
                ease = p * p * p;   // CubeIn
                pos = center - new Vector2(0f, Visuals.PostcardRise) * ease;
                alpha = 1f - ease;
                rot = Visuals.PostcardTurnTo + Visuals.PostcardTurnOut * ease;
            }
            else { pos = center; alpha = 1f; rot = Visuals.PostcardTurnTo; }

            Matrix4x4 old = GUI.matrix;
            GUI.depth = -1;   // above the death counter and Silksong's HUD
            GUI.matrix = Matrix4x4.TRS(new Vector3(pos.x * s, pos.y * s, 0f), Quaternion.Euler(0f, 0f, rot * Mathf.Rad2Deg), new Vector3(s, s, 1f));
            float w = Art.Postcard.W, h = Art.Postcard.H;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(-w / 2f, -h / 2f, w, h), Art.Postcard.Tex);

            Art.DrawLine(card.Title, -w / 2f + Visuals.PostcardTitleX, -h / 2f + Visuals.PostcardTitleY, Visuals.PostcardTitleScale, new Color(0f, 0f, 0f, Visuals.PostcardTitleAlpha * alpha));
            float sc = textScale, lineH = Art.LineHeight * sc;
            float top = Visuals.PostcardTextOffsetY - lines.Count * lineH / 2f;
            var ink = new Color(0f, 0f, 0f, Visuals.PostcardTextAlpha * alpha);
            float block = 0f;
            foreach (string line in lines) block = Math.Max(block, Art.Measure(line));
            foreach (string line in lines)
            {
                // visuals.postcard_left_aligned: a left-aligned block centred on the card (Celeste centres each line).
                float x = Visuals.PostcardLeftAligned > 0f ? -block * sc / 2f : -Art.Measure(line) * sc / 2f;
                Art.DrawLine(line, x, top, sc, ink);
                top += lineH;
            }
            GUI.matrix = old;

            // The continue prompt fades in at the bottom right (Celeste draws the confirm button there).
            if (phase == Phase.Shown && shownFor > Visuals.PostcardButtonDelay)
            {
                float be = Mathf.Clamp01((shownFor - Visuals.PostcardButtonDelay) * 2f), bEase = 1f - (1f - be) * (1f - be) * (1f - be);
                const string prompt = "Jump to continue";
                float ps = 0.6f, x = Screen.width / s - 120f - Art.Measure(prompt) * ps, y = 1080f - 100f - 20f * bEase - Art.LineHeight * ps / 2f;
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
                for (int i = -1; i <= 1; i++)
                    for (int j = -1; j <= 1; j++)
                        if (i != 0 || j != 0) Art.DrawLine(prompt, x + i * 2f, y + j * 2f, ps, new Color(0f, 0f, 0f, bEase));
                Art.DrawLine(prompt, x, y, ps, new Color(1f, 1f, 1f, bEase));
                GUI.matrix = old;
            }
            GUI.color = Color.white;
        }
    }
}
