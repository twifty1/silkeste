using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using TMPro;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Celeste's dialogue box in place of Silksong's (user 2026-10-13: "exactly like the Celeste text boxes", no
    /// portraits). Silksong's DialogueBox keeps running the conversation (its timing, the reveal, Jump to advance, the
    /// NPC's callbacks); its own art is hidden, and Celeste's textbox/default is drawn at the top of the screen
    /// instead: it pops open, the text appears as Silksong reveals it, the bobbing arrow shows when Jump continues, and
    /// it shrinks away when the box closes, popping again when the speaker changes, as Celeste's Textbox does.
    /// </summary>
    public class CelesteTextbox : MonoBehaviour
    {
        static GuiArt Art { get { return SpeedrunTimer.Art; } }
        /// <summary>The box is on screen: the speedrun timer steps aside so they don't overlap.</summary>
        public static bool Showing;
        float hideFor;
        static readonly AccessTools.FieldRef<DialogueBox, bool> Running = AccessTools.FieldRefAccess<DialogueBox, bool>("isDialogueRunning");

        static readonly AccessTools.FieldRef<DialogueBox> Inst = AccessTools.StaticFieldRefAccess<DialogueBox>(AccessTools.Field(typeof(DialogueBox), "_instance"));
        static readonly AccessTools.FieldRef<DialogueBox, bool> BoxOpen = AccessTools.FieldRefAccess<DialogueBox, bool>("isBoxOpen");
        static readonly AccessTools.FieldRef<DialogueBox, bool> Waiting = AccessTools.FieldRefAccess<DialogueBox, bool>("waitingToAdvance");
        static readonly AccessTools.FieldRef<DialogueBox, bool> Printing = AccessTools.FieldRefAccess<DialogueBox, bool>("isPrintingText");
        static readonly System.Reflection.FieldInfo MeshField = AccessTools.Field(typeof(DialogueBox), "textMesh");
        static object Mesh(DialogueBox b) { return MeshField != null ? MeshField.GetValue(b) : null; }
        // the text component's own properties by name: its type from the game doesn't match ours at compile time
        static object Prop(object o, string name) { var p = o.GetType().GetProperty(name); return p != null ? p.GetValue(o, null) : null; }

        // what is shown now
        bool open;               // Silksong's box is open
        float ease;              // 0 closed .. 1 open (Celeste: Textbox.ease)
        string shown = "";       // the visible text of the current page
        string lastFull = "";    // the page's whole text, kept while the box shrinks away
        bool waiting;
        float arrowT;
        bool loggedError, logged, loggedOpen;
        const int HiddenLayer = 31;
        bool checkedCams, camSeesHidden;
        int diagCount;
        string loggedText = "";
        float findIn;
        DialogueBox found;
        // the reveal Celeste-style (user 2026-10-13): our own count follows Silksong's, pausing after punctuation; each
        // letter remembers when it appeared so it can hop in
        float dispCount, pauseT;
        readonly List<float> revealAt = new List<float>();
        string revealFor = "";
        bool isPlayer;
        string speaker = "";
        static readonly AccessTools.FieldRef<DialogueBox, DialogueBox.DialogueLine> CurLine = AccessTools.FieldRefAccess<DialogueBox, DialogueBox.DialogueLine>("currentLine");
        static readonly System.Reflection.FieldInfo InstigatorField = AccessTools.Field(typeof(DialogueBox), "instigator");

        void Awake() { Plugin.Log.LogInfo("Celeste text box: on"); }
        float diagIn = 5f;
        void LateUpdate()
        {
            if ((diagIn -= Time.unscaledDeltaTime) <= 0f && diagCount < 6)
            {
                diagIn = 5f; diagCount++;
                DialogueBox d = null; try { d = Inst(); } catch (Exception e) { Plugin.Log.LogWarning("Celeste text box: instance read failed: " + e.Message); }
                Plugin.Log.LogInfo("Celeste text box check: art " + (Art == null ? "null" : Art.Image("textbox/default") == null ? "no box" : "ok") + ", instance " + (d != null ? d.name + " open " + BoxOpen(d) : "null") + ", boxes in scene " + FindObjectsByType<DialogueBox>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            }
            try { Tick(); }
            catch (Exception e) { if (!loggedError) { loggedError = true; Plugin.Log.LogError("CelesteTextbox: " + e); } }
        }

        void Tick()
        {
            DialogueBox box = Inst();
            float dt = Time.unscaledDeltaTime;
            // a box that isn't the static one (or before it registers): look for any that is open, once a second
            if ((box == null || !BoxOpen(box)) && (findIn -= dt) <= 0f)
            {
                findIn = 1f;
                foreach (DialogueBox b in FindObjectsByType<DialogueBox>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (BoxOpen(b)) { found = b; break; }
            }
            if (found != null && BoxOpen(found)) box = found;
            if (!logged && Art != null && HeroController.SilentInstance != null)
            {
                logged = true;
                Plugin.Log.LogInfo("Celeste text box: art " + (Art != null && Art.Image("textbox/default") != null ? "ready" : "missing") + ", dialogue box " + (box != null ? "found" : "not found yet"));
            }
            if (box != null && BoxOpen(box) && !loggedOpen) { loggedOpen = true; Plugin.Log.LogInfo("Celeste text box: a conversation opened (" + box.name + ")"); }
            if (box == null || Art == null || Art.Image("textbox/default") == null) { open = false; ease = Mathf.MoveTowards(ease, 0f, dt / 0.15f); hideFor = ease > 0f ? 0.5f : Math.Max(0f, hideFor - dt); Showing = hideFor > 0f; return; }

            // Silksong's own art stays hidden (its text, box, arrow and decorations); it still runs everything else
            // a layer no camera draws: its renderers stay on (so TextMeshPro keeps laying the text out for us), and
            // nothing in the box can switch them back into view mid-frame
            foreach (Transform t in box.GetComponentsInChildren<Transform>(true)) if (t.gameObject.layer != HiddenLayer) t.gameObject.layer = HiddenLayer;
            if (!checkedCams)
            {
                checkedCams = true;
                foreach (Camera c in Camera.allCameras) if ((c.cullingMask & (1 << HiddenLayer)) != 0) { camSeesHidden = true; Plugin.Log.LogInfo("Celeste text box: camera " + c.name + " draws layer " + HiddenLayer + "; the box is moved away instead"); }
            }
            if (camSeesHidden) box.transform.localPosition = new Vector3(box.transform.localPosition.x, 5000f, box.transform.localPosition.z);

            bool wasOpen = open;
            open = BoxOpen(box);
            if (open && !wasOpen) { ease = 0f; Sfx.Play(Sounds.MenuRolloverDown.Id, 0.5f); }   // Celeste: a soft pop as it opens
            ease = open ? Mathf.MoveTowards(ease, 1f, dt / 0.2f) : Mathf.MoveTowards(ease, 0f, dt / 0.15f);
            // the timer stays away for the whole conversation, including the moment the box closes and reopens for
            // the next speaker (user 2026-10-13: it flashed back in between), and half a second after
            bool running = Running(box);
            if (ease > 0f || running) hideFor = 0.5f; else hideFor = Math.Max(0f, hideFor - dt);
            Showing = hideFor > 0f;

            waiting = open && Waiting(box) && !Printing(box) && dispCount >= shown.Length;
            arrowT = waiting ? arrowT + dt : 0f;
            if (!open) return;
            object tm = Mesh(box);
            if (tm == null) return;
            int before = shown.Length;
            shown = VisibleText(tm, out lastFull);
            if (lastFull != revealFor) { revealFor = lastFull; dispCount = 0f; pauseT = 0f; revealAt.Clear(); nameColors = NameColors(lastFull); }
            int target = shown.Length;
            if (target - before > 8 || target >= lastFull.Length && !Printing(box)) { /* skipped or finished: catch up fast */ }
            if (target - before > 8) dispCount = target;   // Jump pressed to skip: everything at once, as in Celeste
            if (pauseT > 0f) pauseT -= dt;
            else if (dispCount < target)
            {
                float next = Math.Min(target, dispCount + dt * (target - dispCount > 6 ? 140f : 70f));
                // Celeste's pauses: a beat after commas, a longer one after a sentence ends
                for (int i = (int)dispCount; i < (int)next && i < lastFull.Length; i++)
                {
                    char ch = lastFull[i];
                    bool endOfWord = i + 1 >= lastFull.Length || lastFull[i + 1] == ' ' || lastFull[i + 1] == '\n';
                    if (endOfWord && (ch == '.' || ch == '!' || ch == '?')) { next = i + 1; pauseT = 0.22f; break; }
                    if (endOfWord && (ch == ',' || ch == ';' || ch == ':')) { next = i + 1; pauseT = 0.1f; break; }
                }
                dispCount = next;
            }
            while (revealAt.Count < (int)dispCount) revealAt.Add(Time.unscaledTime);
            try { var line = CurLine(box); isPlayer = line.IsPlayer; } catch (Exception) { isPlayer = false; }
            string ev = ""; try { ev = CurLine(box).Event; } catch (Exception) { }
            speaker = isPlayer ? "Madeline" : SpeakerName(InstigatorField != null ? InstigatorField.GetValue(box) as Component : null, ev);
            if (lastFull != loggedText && lastFull.Length > 0) { loggedText = lastFull; Plugin.Log.LogInfo("Celeste text box line: " + (lastFull.Length > 60 ? lastFull.Substring(0, 60) + "..." : lastFull)); }
        }

        // The line Silksong shows, its rich-text tags taken out, as far as it has revealed it. A long line is shown by
        // its last lines that fit, so the newest words are always in view (Silksong's own pages need its layout).
        static string VisibleText(object tm, out string full)
        {
            full = System.Text.RegularExpressions.Regex.Replace(Prop(tm, "text") as string ?? "", "<[^>]*>", "");
            // Silksong's curly quotes and dashes aren't in Celeste's font
            full = full.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"').Replace('—', '-').Replace('–', '-').Replace("…", "...");
            object mv = Prop(tm, "maxVisibleCharacters");
            int max = mv is int ? (int)mv : int.MaxValue;
            int n = max == int.MaxValue ? full.Length : Mathf.Clamp(max, 0, full.Length);
            return full.Substring(0, n);
        }
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || ease <= 0f || Art == null || TheoCamera.Hiding) return;
            GuiArt.Img tex = Art.Image("textbox/default");
            if (tex == null) return;
            float s = Screen.height / 1080f, sw = Screen.width / s;
            Matrix4x4 old = GUI.matrix;
            GUI.depth = -1;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));

            // Celeste's Textbox: 1688 x 272 at the top centre; it opens growing from its middle with a little overshoot
            // (Ease.BackOut) and closes shrinking (Ease.CubeIn), fading as it goes
            // Madeline's box (1743 x 336) at 82% height, and narrower (user 2026-10-13): only the plain stretches between
            // its corners and its top flourish get shorter, so the ornaments keep their shape
            const float Scale = 0.82f, W = 1200f, Top = 40f;
            // the box fits its text (user 2026-10-14): a short line gets a short box, a long one the full height
            float lineCount = Math.Max(1, Art.Wrap(lastFull, (W - 168f) / 0.6f).Count);
            float H = Mathf.Clamp(64f + lineCount * Art.LineHeight * 0.6f, 150f, 336f * Scale);
            float k = open ? BackOut(ease) : ease * ease * ease;
            float h = H * k, alpha = Mathf.Clamp01(ease * 1.5f);
            float x = sw / 2f - W / 2f, y = Top + (H - h) / 2f;
            // a little more solid than the art alone (user 2026-10-13), and Madeline's own lines in a warmer tint
            Color tint = isPlayer ? new Color(1f, 0.86f, 0.86f) : Color.white;
            var boxRect = new Rect(x, y, W, Math.Max(1f, h));
            GUI.color = new Color(tint.r, tint.g, tint.b, alpha * 0.55f); Slices9(tex, boxRect, Scale);
            GUI.color = new Color(tint.r, tint.g, tint.b, alpha); Slices9(tex, boxRect, Scale);
            DrawNameTab(x, y, W, alpha * Mathf.Clamp01((ease - 0.5f) / 0.5f));

            // the words, once the box is mostly open
            float textA = Mathf.Clamp01((ease - 0.6f) / 0.4f) * alpha;
            if (textA > 0f && h > 40f)
            {
                Color ink = TextColor(tex); ink.a = textA;
                // as in Celeste without a portrait: off-white text from the left, the block centred in the box's height
                float pad = 64f, maxW = W - pad * 2f - 40f;
                // Celeste's dialogue size, smaller when a page has more lines than fit
                float ts = 0.6f;
                List<string> lines = Art.Wrap(lastFull, maxW / ts);
                while (ts > 0.4f && lines.Count * Art.LineHeight * ts > H - 56f) { ts -= 0.05f; lines = Art.Wrap(lastFull, maxW / ts); }
                // each letter on its own: it hops up into place as it appears (Celeste's text), words in CAPITALS
                // shake, and a word ending in "!" waves
                float now = Time.unscaledTime, ly = Top + (H - lines.Count * Art.LineHeight * ts) / 2f;
                int idx = 0;   // index into the page's text
                foreach (string line in lines)
                {
                    float lx = x + pad;
                    string[] words = line.Split(' ');
                    foreach (string word in words)
                    {
                        string bare = word.Trim('!', '?', '.', ',', '"', '\'');
                        bool shake = bare.Length >= 2 && bare.ToUpperInvariant() == bare && bare.ToLowerInvariant() != bare;
                        bool wave = word.EndsWith("!");
                        for (int c = 0; c < word.Length; c++, idx++)
                        {
                            if (idx >= revealAt.Count) goto done;
                            float kk = Mathf.Clamp01((now - revealAt[idx]) / 0.12f), hop = (1f - kk) * (1f - kk);
                            float ox = 0f, oy = -hop * 10f * ts;
                            if (shake) { ox += (Mathf.PerlinNoise(now * 30f, idx) - 0.5f) * 3f; oy += (Mathf.PerlinNoise(idx, now * 30f) - 0.5f) * 3f; }
                            if (wave) oy += Mathf.Sin(now * 8f + idx * 0.7f) * 3f;
                            Color ci = nameColors != null && idx < nameColors.Length && nameColors[idx].a > 0f ? nameColors[idx] : ink; ci.a = ink.a * kk;
                            lx = DrawChar(word[c], lx, ly, ts, ox, oy, ci);
                        }
                        idx++;   // the space after the word (or the line break)
                        GuiArt.Img sp; lx += Art.Glyphs.TryGetValue(' ', out sp) ? sp.XAdvance * ts : 16f * ts;
                    }
                    ly += Art.LineHeight * ts;
                }
            done:;
            }
            // the "continue" arrow, bottom right, bobbing (Celeste: textboxbutton)
            GuiArt.Img arrow = Art.Image("textboxbutton");
            if (arrow != null && arrowT > 0f && ease >= 1f)
            {
                float a2 = Mathf.Clamp01(arrowT / 0.15f), bob = Mathf.Sin(arrowT * 8f) * 4f;
                float aw = arrow.W, ah = arrow.H;
                Color ac = TextColor(tex); ac.a = a2;
                GuiArt.Draw(arrow, new Rect(x + W - 52f - aw * 0.4f, Top + H - 40f - ah * 0.4f + bob, aw * 0.8f, ah * 0.8f), new Color(1f, 1f, 1f, a2));
            }
            GUI.matrix = old;
            GUI.color = Color.white;
        }


        // five columns of the box art: left corner, plain, the top flourish, plain, right corner. The three ornament
        // columns keep their width (at scale); the two plain ones share what is left.
        static readonly float[] Cols = { 0f, 120f, 790f, 955f, 1623f, 1743f };
        internal static void Slices(GuiArt.Img img, Rect r, float scale)
        {
            float fixedW = ((Cols[1] - Cols[0]) + (Cols[3] - Cols[2]) + (Cols[5] - Cols[4])) * scale;
            float plain = Math.Max(0f, (r.width - fixedW) / 2f);
            float[] widths = { (Cols[1] - Cols[0]) * scale, plain, (Cols[3] - Cols[2]) * scale, plain, (Cols[5] - Cols[4]) * scale };
            float x = r.x;
            for (int i = 0; i < 5; i++)
            {
                // a pixel in from each cut, so the smoothing never pulls in the neighbouring column (that drew a seam)
                float u0 = (Cols[i] + (i > 0 ? 1f : 0f)) / img.W, u1 = (Cols[i + 1] - (i < 4 ? 1f : 0f)) / img.W;
                var uv = new Rect(u0, 0f, u1 - u0, 1f);
                GUI.DrawTextureWithTexCoords(new Rect(x - (i > 0 ? 0.5f : 0f), r.y, widths[i] + (i > 0 && i < 4 ? 1f : 0.5f), r.height), img.Tex, uv);
                x += widths[i];
            }
        }

        // one letter of Celeste's font at (x + ox, top + oy); returns where the next one starts
        float DrawChar(char ch, float x, float top, float scale, float ox, float oy, Color c)
        {
            GuiArt.Img g;
            if (!Art.Glyphs.TryGetValue(ch, out g)) return x;
            if (g.Tex != null && c.a > 0f)
            {
                GUI.color = c;
                GUI.DrawTexture(new Rect(x + ox + g.XOffset * scale, top + oy + g.YOffset * scale, g.W * scale, g.H * scale), g.Tex);
            }
            return x + g.XAdvance * scale;
        }

        // Celeste's name tab: a small plum plate on the box's top edge, at the left for others and the right for Madeline
        void DrawNameTab(float bx, float by, float bw, float a)
        {
            if (a <= 0f || string.IsNullOrEmpty(speaker)) return;
            float ns = 0.42f, tw = Art.Measure(speaker) * ns + 40f, th = Art.LineHeight * ns + 14f;
            float tx = isPlayer ? bx + bw - 150f - tw : bx + 150f, ty = by - th * 0.35f;
            var r = new Rect(tx, ty, tw, th);
            GUI.color = new Color(0.84f, 0.78f, 0.82f, a); GUI.DrawTexture(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), Texture2D.whiteTexture);
            GUI.color = isPlayer ? new Color(0.36f, 0.16f, 0.2f, a) : new Color(0.19f, 0.13f, 0.17f, a); GUI.DrawTexture(r, Texture2D.whiteTexture);
            Color nc = ColorFor(speaker); nc.a = a;
            Art.DrawLine(speaker, tx + 20f, ty + 7f, ns, nc);
        }

        // the speaker's name as Silksong titles them in the corner (user 2026-10-13: the real name, not the object's):
        // the NpcDialogueTitle that belongs to this NPC holds a key into the "Titles" text; its _MAIN line is the name
        // ---- names in colour (user 2026-10-13): like Celeste's dialogue, where names stand out in a colour of their own.
        // The recurring characters each keep one colour all game; any NPC whose tab has shown joins them.
        Color[] nameColors;
        static readonly Color Rosary = new Color(1f, 0.78f, 0.72f), Shard = new Color(0.62f, 0.86f, 0.92f);
        static readonly Color Peak = new Color(1f, 0.86f, 0.25f), Deep = new Color(0.72f, 0.5f, 1f), Place = new Color(0.45f, 0.9f, 0.85f);
        static readonly Dictionary<string, Color> NameColor = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "Hornet", new Color(1f, 0.45f, 0.45f) }, { "Madeline", new Color(1f, 0.45f, 0.45f) },
            { "Lace", new Color(0.95f, 0.95f, 1f) }, { "Grand Mother Silk", new Color(0.95f, 0.55f, 0.85f) },
            { "Sherma", new Color(1f, 0.85f, 0.4f) }, { "Shakra", new Color(1f, 0.6f, 0.3f) }, { "Garmond", new Color(0.55f, 0.75f, 1f) },
            { "Zaza", new Color(0.6f, 0.95f, 0.75f) }, { "Seamstress", new Color(0.85f, 0.65f, 1f) }, { "Pinstress", new Color(0.4f, 0.85f, 1f) },
            { "Trobbio", new Color(1f, 0.7f, 0.85f) }, { "Widow", new Color(0.75f, 0.5f, 0.95f) }, { "Moss Mother", new Color(0.6f, 0.9f, 0.45f) },
            { "Bell Beast", new Color(1f, 0.75f, 0.45f) }, { "Flick", new Color(1f, 0.8f, 0.5f) },
            { "Weaver", new Color(0.7f, 0.8f, 1f) }, { "Weavers", new Color(0.7f, 0.8f, 1f) },
            // money (user 2026-10-14): a pearly rose for rosaries, a sea-glass blue for shell shards
            { "Rosaries", Rosary }, { "Rosary", Rosary }, { "Rosary String", Rosary }, { "Rosary Strings", Rosary }, { "Rosary Necklace", Rosary },
            { "Shell Shards", Shard }, { "Shell Shard", Shard },
            // places (user 2026-10-13: the important ones, whatever they're called, in one colour each)
            // the top of Pharloom, the goal of the climb: bright gold, by every name it goes by
            { "Citadel", Peak }, { "the Citadel", Peak }, { "the peak", Peak }, { "the summit", Peak }, { "the crown", Peak },
            { "Pharloom's peak", Peak }, { "the top of the kingdom", Peak }, { "the top of Pharloom", Peak }, { "High Halls", Peak }, { "Choral Chambers", Peak },
            // the kingdom itself
            { "Pharloom", new Color(1f, 0.78f, 0.5f) },
            // the depths: a deep violet
            { "the Abyss", Deep }, { "Abyss", Deep }, { "the Void", Deep }, { "Hallownest", new Color(0.75f, 0.85f, 1f) },
            // the other regions share a calm teal, so a place reads as a place
            { "Bellhart", Place }, { "Mosshome", Place }, { "Moss Grotto", Place }, { "Greymoor", Place }, { "Shellwood", Place }, { "Bilewater", Place },
            { "Deep Docks", Place }, { "Far Fields", Place }, { "Hunter's March", Place }, { "Blasted Steps", Place }, { "Sinner's Road", Place },
            { "the Marrow", Place }, { "Whiteward", Place }, { "Underworks", Place }, { "the Cradle", Place }, { "Wormways", Place },
            { "Bone Bottom", Place }, { "Mount Fay", Place }, { "the Slab", Place }, { "Putrified Ducts", Place }, { "Weavenest", Place },
            { "Haunted Bell", new Color(1f, 0.75f, 0.45f) },
        };
        // Celeste-like colours for names met along the way, picked from the name so they never change
        static readonly Color[] Palette = { new Color(1f, 0.87f, 0.4f), new Color(0.45f, 0.85f, 1f), new Color(1f, 0.55f, 0.75f), new Color(0.6f, 0.95f, 0.55f), new Color(0.8f, 0.6f, 1f), new Color(1f, 0.65f, 0.35f) };
        static Color ColorFor(string name)
        {
            Color c;
            if (NameColor.TryGetValue(name, out c)) return c;
            int h = 0; foreach (char ch in name.ToLowerInvariant()) h = h * 31 + ch;
            c = Palette[(h & 0x7fffffff) % Palette.Length];
            NameColor[name] = c;
            return c;
        }
        internal static Color[] NameColorsFor(string text) { return NameColors(text); }
        /// <summary>A name's colour, or the fallback when it has none of its own.</summary>
        internal static Color ColorOf(string name, Color fallback) { Color c; return !string.IsNullOrEmpty(name) && NameColor.TryGetValue(name, out c) ? c : fallback; }
        static Color[] NameColors(string text)
        {
            var cols = new Color[text.Length];
            // longest names first, so "Grand Mother Silk" wins over "Mother"
            var keys = new List<string>(NameColor.Keys);
            keys.Sort((a, b) => b.Length.CompareTo(a.Length));
            foreach (string name in keys)
            {
                int from = 0, at;
                while ((at = text.IndexOf(name, from, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    bool startOk = at == 0 || !char.IsLetter(text[at - 1]);
                    int end = at + name.Length;
                    bool endOk = end >= text.Length || !char.IsLetter(text[end]) || (text[end] == 's' && (end + 1 >= text.Length || !char.IsLetter(text[end + 1])));
                    if (startOk && endOk && cols[at].a == 0f)
                    {
                        int to = end < text.Length && text[end] == 's' && (end + 1 >= text.Length || !char.IsLetter(text[end + 1])) ? end + 1 : end;
                        for (int i = at; i < to; i++) cols[i] = NameColor[name];
                    }
                    from = at + 1;
                }
            }
            return cols;
        }

        static readonly Dictionary<string, string> names = new Dictionary<string, string>();
        static readonly System.Reflection.FieldInfo TitleNpcs = AccessTools.Field(typeof(NpcDialogueTitle), "npcControls");
        static readonly System.Reflection.FieldInfo TitleList = AccessTools.Field(typeof(NpcDialogueTitle), "titles");
        static string SpeakerName(Component npc, string speakerEvent)
        {
            if (npc == null) return "";
            string cacheKey = npc.GetInstanceID() + "|" + speakerEvent;
            string n;
            if (names.TryGetValue(cacheKey, out n)) return n;
            n = "";
            try
            {
                foreach (NpcDialogueTitle nt in FindObjectsByType<NpcDialogueTitle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var npcs = TitleNpcs != null ? TitleNpcs.GetValue(nt) as Array : null;
                    bool mine = nt.gameObject == npc.gameObject;
                    if (npcs != null) foreach (object o in npcs) if (o is Component c && c == npc) mine = true;
                    if (!mine) continue;
                    var titles = TitleList != null ? TitleList.GetValue(nt) as Array : null;
                    if (titles == null) continue;
                    string key = null;
                    foreach (object st in titles)
                    {
                        if (st == null) continue;
                        var tf = st.GetType().GetField("Title"); var ef = st.GetType().GetField("SpeakerEvent");
                        string title = tf != null ? tf.GetValue(st) as string : null, ev = ef != null ? ef.GetValue(st) as string : null;
                        if (key == null || ev == speakerEvent) key = title;   // as Silksong picks it
                    }
                    if (string.IsNullOrEmpty(key)) continue;
                    string main = BossCard.Lang(key + "_MAIN") ?? BossCard.Lang(key);
                    if (!string.IsNullOrEmpty(main)) { n = BossCard.Clean(main).Trim(); break; }
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Celeste text box speaker: " + e.Message); }
            if (n.Length > 0) n = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(n.ToLowerInvariant());
            names[cacheKey] = n;
            if (n.Length > 0) ColorFor(n);
            Plugin.Log.LogInfo("Celeste text box speaker: " + npc.gameObject.name + " -> \"" + n + "\"");
            return n;
        }
        // as Slices, and also in three rows: the top and bottom bands (with the corner ornaments) keep their height,
        // only the plain middle stretches or shrinks, so a short box still has whole corners
        static readonly float[] Rows = { 0f, 80f, 256f, 336f };
        internal static void Slices9(GuiArt.Img img, Rect r, float scale)
        {
            float fixedW = ((Cols[1] - Cols[0]) + (Cols[3] - Cols[2]) + (Cols[5] - Cols[4])) * scale;
            float plain = Math.Max(0f, (r.width - fixedW) / 2f);
            float[] widths = { (Cols[1] - Cols[0]) * scale, plain, (Cols[3] - Cols[2]) * scale, plain, (Cols[5] - Cols[4]) * scale };
            float band = Math.Min((Rows[1] - Rows[0]) * scale, r.height / 2f), bandB = Math.Min((Rows[3] - Rows[2]) * scale, r.height / 2f);
            float[] heights = { band, Math.Max(0f, r.height - band - bandB), bandB };
            float y = r.y;
            for (int j = 0; j < 3; j++)
            {
                // texture rows run upward: the top band is the top of the image
                float v1 = 1f - (Rows[j] + (j > 0 ? 1f : 0f)) / img.H, v0 = 1f - (Rows[j + 1] - (j < 2 ? 1f : 0f)) / img.H;
                float x = r.x;
                for (int i = 0; i < 5; i++)
                {
                    float u0 = (Cols[i] + (i > 0 ? 1f : 0f)) / img.W, u1 = (Cols[i + 1] - (i < 4 ? 1f : 0f)) / img.W;
                    if (heights[j] > 0f) GUI.DrawTextureWithTexCoords(new Rect(x - (i > 0 ? 0.5f : 0f), y - (j > 0 ? 0.5f : 0f), widths[i] + (i > 0 && i < 4 ? 1f : 0.5f), heights[j] + (j == 1 ? 1f : 0.5f)), img.Tex, new Rect(u0, v0, u1 - u0, v1 - v0));
                    x += widths[i];
                }
                y += heights[j];
            }
        }

        static float BackOut(float t) { t = Mathf.Clamp01(t) - 1f; const float s = 1.2f; return t * t * ((s + 1f) * t + s) + 1f; }

        // the box art drawn as a nine-slice, so its corners and border keep their size at any width or height
        static void NineSlice(GuiArt.Img img, Rect r, Color c)
        {
            float cs = Mathf.Min(img.W, img.H) / 3f, cx = cs / img.W, cy = cs / img.H;
            float cw = Mathf.Min(cs, r.width / 2f), ch = Mathf.Min(cs, r.height / 2f);
            GUI.color = c;
            float[] xs = { r.x, r.x + cw, r.xMax - cw, r.xMax };
            float[] ys = { r.y, r.y + ch, r.yMax - ch, r.yMax };
            float[] us = { 0f, cx, 1f - cx, 1f };
            float[] vs = { 1f, 1f - cy, cy, 0f };   // GUI rows run down, texture rows up
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var dst = new Rect(xs[i], ys[j], xs[i + 1] - xs[i], ys[j + 1] - ys[j]);
                    if (dst.width <= 0f || dst.height <= 0f) continue;
                    var uv = new Rect(us[i], vs[j + 1], us[i + 1] - us[i], vs[j] - vs[j + 1]);
                    GUI.DrawTextureWithTexCoords(dst, img.Tex, uv);
                }
        }

        // the box's own middle colour decides the ink: light text on a dark box, dark text on a light one
        static Color? ink;
        static Color TextColor(GuiArt.Img img)
        {
            if (ink.HasValue) return ink.Value;
            ink = Art.TextboxDark ? new Color(0.93f, 0.92f, 0.94f) : new Color(0.13f, 0.11f, 0.12f);
            return ink.Value;
        }
    }
}
