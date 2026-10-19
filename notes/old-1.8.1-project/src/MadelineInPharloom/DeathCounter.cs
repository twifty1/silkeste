using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Celeste's GUI art from the player's Celeste (hooks.death_counter_art / death_counter_font / postcard_art):
    /// the blue skull, the x, the postcard and the Renogare 64 font, cut out of the Gui atlas page.
    /// </summary>
    public class GuiArt
    {
        internal class Img { public int W, H, XOffset, YOffset, XAdvance; public byte[] Rgba; public Texture2D Tex; }   // rows top-down

        internal Img Skull, X, Postcard;
        internal readonly Img[] Digits = new Img[10];
        internal readonly Dictionary<char, Img> Glyphs = new Dictionary<char, Img>();
        internal int LineHeight;
        internal bool TextboxDark = true;

        // More Gui art for the boss card, Assist Mode and the speedrun timer (hooks.gui_art_extra). Missing pieces
        // are left out; the screens that use them check for null.
        static readonly string[] Extra =
        {
            "collectables/goldberry", "collectables/strawberry", "collectables/skullGold", "collectables/dashes", "collectables/heartgem/0/spin00", "collectables/heartgem/1/spin00", "collectables/heartgem/2/spin00", "collectables/heartgem/2/spin01", "collectables/heartgem/2/spin02", "collectables/heartgem/2/spin03", "collectables/heartgem/2/spin04", "collectables/heartgem/2/spin05", "collectables/heartgem/2/spin06", "collectables/heartgem/2/spin07", "collectables/heartgem/2/spin08", "collectables/heartgem/2/spin09", "collectables/heartgem/2/spin10",
            "strawberryCountBG", "areaselect/card", "areaselect/card_golden", "areaselect/cardtop", "areaselect/cardtop_golden", "areaselect/title", "areaselect/accent",
            "assistmode", "assistmodestamp", "textboxbutton", "menu/start", "menu/options", "menu/exit", "menu/journal", "menu/credits", "menu/play", "adjustarrowleft", "adjustarrowright", "dotarrow", "dotarrow_outline",
        };
        internal readonly Dictionary<string, Img> Images = new Dictionary<string, Img>();
        internal Img Image(string name) { Img i; return Images.TryGetValue(name, out i) && i.Tex != null ? i : null; }

        /// <summary>Worker thread: reads Gui.meta, decodes the page holding the art, and cuts out the pieces.</summary>
        public static GuiArt Prepare(string celesteDir)
        {
            var art = new GuiArt();
            string atlasDir = Path.Combine(celesteDir, "Content/Graphics/Atlases");
            var wanted = new Dictionary<string, CelesteAssets.SubTex>();
            var pageNames = new List<string>();
            using (var r = new BinaryReader(File.OpenRead(Path.Combine(atlasDir, "Gui.meta"))))
            {
                r.ReadInt32(); r.ReadString(); r.ReadInt32();
                int pages = r.ReadInt16();
                for (int i = 0; i < pages; i++)
                {
                    pageNames.Add(r.ReadString());
                    int count = r.ReadInt16();
                    for (int j = 0; j < count; j++)
                    {
                        string path = r.ReadString().Replace('\\', '/');
                        var s = new CelesteAssets.SubTex { Page = i, X = r.ReadInt16(), Y = r.ReadInt16(), W = r.ReadInt16(), H = r.ReadInt16() };
                        s.OffX = -r.ReadInt16(); s.OffY = -r.ReadInt16(); s.FullW = r.ReadInt16(); s.FullH = r.ReadInt16();
                        if (path == "collectables/skullBlue" || path == "x" || path == "renogare64_0" || path == "postcard" || Array.IndexOf(Extra, path) >= 0) wanted[path] = s;
                    }
                }
            }
            foreach (string n in new[] { "collectables/skullBlue", "x", "renogare64_0", "postcard" })
                if (!wanted.ContainsKey(n)) throw new Exception("Gui atlas is missing " + n);
            var decoded = new Dictionary<int, CelesteAssets.Page>();
            Func<CelesteAssets.SubTex, CelesteAssets.Page> page = t =>
            {
                CelesteAssets.Page pg;
                if (!decoded.TryGetValue(t.Page, out pg)) decoded[t.Page] = pg = CelesteAssets.DecodeData(Path.Combine(atlasDir, pageNames[t.Page] + ".data"));
                return pg;
            };
            art.Skull = Cut(page(wanted["collectables/skullBlue"]), wanted["collectables/skullBlue"], 0, 0, wanted["collectables/skullBlue"].FullW, wanted["collectables/skullBlue"].FullH);
            art.X = Cut(page(wanted["x"]), wanted["x"], 0, 0, wanted["x"].FullW, wanted["x"].FullH);
            art.Postcard = Cut(page(wanted["postcard"]), wanted["postcard"], 0, 0, wanted["postcard"].FullW, wanted["postcard"].FullH);
            foreach (string n in Extra)
            {
                CelesteAssets.SubTex t;
                if (wanted.TryGetValue(n, out t)) art.Images[n] = Cut(page(t), t, 0, 0, t.FullW, t.FullH);
                else Plugin.Log.LogWarning("Gui atlas has no " + n);
            }

            // Celeste's dialogue box (user 2026-10-13: Celeste text boxes): the Portraits atlas keeps each picture in a
            // file of its own, Portraits/textbox/default.data
            try
            {
                CelesteAssets.Page pg = CelesteAssets.DecodeData(Path.Combine(atlasDir, "Portraits/textbox/madeline.data"));
                var tb = new Img { W = pg.W, H = pg.H, Rgba = pg.Rgba };
                art.Images["textbox/default"] = tb;
                int m = ((tb.H / 2) * tb.W + tb.W / 2) * 4;   // its middle pixel: a dark box gets light text
                art.TextboxDark = tb.Rgba[m] * 0.3f + tb.Rgba[m + 1] * 0.59f + tb.Rgba[m + 2] * 0.11f < 128f;
            }
            catch (Exception e) { Plugin.Log.LogWarning("Celeste text box: " + e.Message); }
            // Celeste's file select (user 2026-10-14: the save files like Celeste's): its own atlas, and Madeline's portrait
            try
            {
                var fsNames = new List<string>();
                var fsWanted = new Dictionary<string, CelesteAssets.SubTex>();
                string[] want = { "card", "ticket", "portraitOverlay", "goldberry", "flag", "dot", "cardShine000", "cardShine001", "cardShine002", "cardShine003", "cardShine004", "cardShine005" };
                using (var r = new BinaryReader(File.OpenRead(Path.Combine(atlasDir, "FileSelect.meta"))))
                {
                    r.ReadInt32(); r.ReadString(); r.ReadInt32();
                    int pages = r.ReadInt16();
                    for (int i = 0; i < pages; i++)
                    {
                        fsNames.Add(r.ReadString());
                        int count = r.ReadInt16();
                        for (int j = 0; j < count; j++)
                        {
                            string path = r.ReadString().Replace('\\', '/');
                            var s = new CelesteAssets.SubTex { Page = i, X = r.ReadInt16(), Y = r.ReadInt16(), W = r.ReadInt16(), H = r.ReadInt16() };
                            s.OffX = -r.ReadInt16(); s.OffY = -r.ReadInt16(); s.FullW = r.ReadInt16(); s.FullH = r.ReadInt16();
                            if (Array.IndexOf(want, path) >= 0) fsWanted[path] = s;
                        }
                    }
                }
                var fsPages = new Dictionary<int, CelesteAssets.Page>();
                foreach (var kv in fsWanted)
                {
                    CelesteAssets.Page pg;
                    if (!fsPages.TryGetValue(kv.Value.Page, out pg)) fsPages[kv.Value.Page] = pg = CelesteAssets.DecodeData(Path.Combine(atlasDir, fsNames[kv.Value.Page] + ".data"));
                    art.Images["fileselect/" + kv.Key] = Cut(pg, kv.Value, 0, 0, kv.Value.FullW, kv.Value.FullH);
                }
                CelesteAssets.Page pt = CelesteAssets.DecodeData(Path.Combine(atlasDir, "Portraits/madeline/normal00.data"));
                art.Images["portrait/madeline"] = new Img { W = pt.W, H = pt.H, Rgba = pt.Rgba };
            }
            catch (Exception e) { Plugin.Log.LogWarning("Celeste file select art: " + e.Message); }

            var font = new XmlDocument();
            font.Load(Path.Combine(celesteDir, "Content/Dialog/Fonts/renogare64.fnt"));
            art.LineHeight = int.Parse(font.SelectSingleNode("/font/common").Attributes["lineHeight"].Value, CultureInfo.InvariantCulture);
            CelesteAssets.SubTex fontPage = wanted["renogare64_0"];
            foreach (XmlNode c in font.SelectNodes("/font/chars/char"))
            {
                int id = Attr(c, "id");
                if (id < 32 || (id > 126 && id < 160) || id > 383) continue;   // ASCII and Latin accents: postcards, menus and Silksong's boss names
                Img g = Cut(page(fontPage), fontPage, Attr(c, "x"), Attr(c, "y"), Attr(c, "width"), Attr(c, "height"));
                g.XOffset = Attr(c, "xoffset"); g.YOffset = Attr(c, "yoffset"); g.XAdvance = Attr(c, "xadvance");
                art.Glyphs[(char)id] = g;
                if (id >= '0' && id <= '9') art.Digits[id - '0'] = g;
            }
            foreach (Img d in art.Digits) if (d == null) throw new Exception("renogare64.fnt is missing a digit");
            return art;
        }

        /// <summary>Width of a line in font pixels at scale 1 (sum of advances, as Monocle's PixelFontSize.Measure).</summary>
        internal float Measure(string line)
        {
            float w = 0f;
            Img g;
            foreach (char ch in line) if (Glyphs.TryGetValue(ch, out g)) w += g.XAdvance;
            return w;
        }

        /// <summary>One line in GUI space, its line box's top-left at (x, top), font pixels times scale.</summary>
        internal void DrawLine(string line, float x, float top, float scale, Color c)
        {
            GUI.color = c;
            Img g;
            foreach (char ch in line)
            {
                if (!Glyphs.TryGetValue(ch, out g)) continue;
                if (g.Tex != null) GUI.DrawTexture(new Rect(x + g.XOffset * scale, top + g.YOffset * scale, g.W * scale, g.H * scale), g.Tex);
                x += g.XAdvance * scale;
            }
        }

        /// <summary>ActiveFont.DrawOutline: the line at eight offsets in the outline colour, then on top.</summary>
        internal void DrawOutlined(string line, float x, float top, float scale, Color c, Color outline, float stroke)
        {
            // four sides instead of eight (user 2026-10-14: faster menus); at these stroke widths it looks the same
            if (outline.a > 0f)
            {
                DrawLine(line, x - stroke, top, scale, outline); DrawLine(line, x + stroke, top, scale, outline);
                DrawLine(line, x, top - stroke, scale, outline); DrawLine(line, x, top + stroke, scale, outline);
            }
            DrawLine(line, x, top, scale, c);
        }

        internal static void Draw(Img img, Rect r, Color c)
        {
            if (img == null || img.Tex == null) return;
            GUI.color = c;
            GUI.DrawTexture(r, img.Tex);
        }

        /// <summary>Word wrap at maxWidth font pixels (scale 1); "\n" starts a new line, an empty line stays empty.</summary>
        internal List<string> Wrap(string text, float maxWidth)
        {
            var lines = new List<string>();
            foreach (string para in text.Split('\n'))
            {
                string line = "";
                foreach (string word in para.Split(' '))
                {
                    string next = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && Measure(next) > maxWidth) { lines.Add(line); line = word; }
                    else line = next;
                }
                lines.Add(line);
            }
            return lines;
        }

        static int Attr(XmlNode n, string name) { return int.Parse(n.Attributes[name].Value, CultureInfo.InvariantCulture); }

        // A rectangle of a sub-texture's full (untrimmed) frame, as straight-alpha RGBA rows top-down.
        static Img Cut(CelesteAssets.Page page, CelesteAssets.SubTex t, int fx, int fy, int w, int h)
        {
            var img = new Img { W = w, H = h, Rgba = new byte[w * h * 4] };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int tx = fx + x - t.OffX, ty = fy + y - t.OffY;
                    if (tx < 0 || ty < 0 || tx >= t.W || ty >= t.H) continue;
                    Array.Copy(page.Rgba, ((t.Y + ty) * page.W + (t.X + tx)) * 4, img.Rgba, (y * w + x) * 4, 4);
                }
            return img;
        }

        /// <summary>Main thread: textures for OnGUI.</summary>
        public void Build()
        {
            var all = new List<Img>(Glyphs.Values) { Skull, X, Postcard };
            all.AddRange(Images.Values);
            foreach (Img i in all)
            {
                if (i.W <= 0 || i.H <= 0) { i.Rgba = null; continue; }   // the space glyph has no pixels
                var tex = new Texture2D(i.W, i.H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[i.W * i.H];
                for (int y = 0; y < i.H; y++)
                    for (int x = 0; x < i.W; x++)
                    {
                        int s = (y * i.W + x) * 4;
                        px[(i.H - 1 - y) * i.W + x] = new Color32(i.Rgba[s], i.Rgba[s + 1], i.Rgba[s + 2], i.Rgba[s + 3]);
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                i.Tex = tex;
                if (!Glyphs.ContainsValue(i)) i.Rgba = null;   // letters keep theirs: big ones are redrawn sharp from them (CelesteMenus.HiFont)
            }
        }
    }

    /// <summary>
    /// Celeste's DeathsCounter (sheets/visuals.json deaths_*): real deaths count, per save slot
    /// (hooks.death_counter_slot); hazard respawns don't. It shows top-right for a moment after each death, with
    /// Celeste's wiggle and flash, and the whole time Madeline rests at a bench.
    /// </summary>
    public class DeathCounter : MonoBehaviour
    {
        public static GuiArt Art;
        public static string DataDir;

        readonly Dictionary<int, int> counts = new Dictionary<int, int>();
        bool loaded, wasDead;
        float showTimer, alpha, wiggleCounter, sineCounter, flashTimer;

        string SaveFile { get { return Path.Combine(DataDir, "deaths.cfg"); } }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            HeroController h = HeroController.SilentInstance;
            GameManager gm = GameManager.SilentInstance;
            bool show = false;
            if (Plugin.Ready && h != null && gm != null)
            {
                Load();
                // Only a real death (Silksong's cState.dead) counts; a hazard respawn that keeps her alive does not.
                bool dead = h.cState.dead;
                if (dead && !wasDead) Add(gm.profileID);
                if (!dead && wasDead)
                {
                    showTimer = Visuals.DeathsShowTime;
                    wiggleCounter = 1f; sineCounter = 0f;   // Wiggler with StartZero: sin, fading out
                    flashTimer = Visuals.DeathsFlashTime;
                }
                wasDead = dead;
                if (!gm.isPaused) showTimer = Math.Max(0f, showTimer - dt);
                show = !dead && !gm.isPaused && (showTimer > 0f || h.playerData.atBench);
            }
            alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, dt / Visuals.DeathsFadeTime);
            if (wiggleCounter > 0f)
            {
                wiggleCounter = Math.Max(0f, wiggleCounter - dt / Visuals.DeathsWiggleTime);
                sineCounter += Mathf.PI * 2f * Visuals.DeathsWiggleFrequency * dt;
            }
            if (flashTimer > 0f) flashTimer -= dt;
        }

        void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (!File.Exists(SaveFile)) return;
                foreach (string line in File.ReadAllLines(SaveFile))
                {
                    string[] kv = line.Split('=');
                    int slot, n;
                    if (kv.Length == 2 && int.TryParse(kv[0], out slot) && int.TryParse(kv[1], out n)) counts[slot] = n;
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Death counter: " + e.Message); }
        }

        void Add(int slot)
        {
            int n;
            counts.TryGetValue(slot, out n);
            counts[slot] = n + 1;
            try
            {
                var lines = new List<string>();
                foreach (var kv in counts) lines.Add(kv.Key + "=" + kv.Value);
                Directory.CreateDirectory(DataDir);
                File.WriteAllLines(SaveFile, lines.ToArray());
            }
            catch (Exception e) { Plugin.Log.LogWarning("Death counter: " + e.Message); }
        }

        // Celeste.DeathsCounter.Render, in Celeste's 1920x1080 GUI units scaled to the screen height.
        void OnGUI()
        {
            if (TheoCamera.Hiding || TheoCamera.HidingHud) return;   // out of Theo's photos
            if (alpha <= 0f || Art == null || Art.Skull.Tex == null || Event.current.type != EventType.Repaint) return;
            GameManager gm = GameManager.SilentInstance;
            if (gm == null) return;
            int amount;
            counts.TryGetValue(gm.profileID, out amount);
            string text = amount.ToString(CultureInfo.InvariantCulture);

            GUI.depth = 1;   // under Celeste's wipe
            float s = Screen.height / 1080f;
            float num = 0f;
            foreach (char ch in text) num += Art.Digits[ch - '0'].XAdvance;
            float num2 = Visuals.DeathsIconWidth + 2f + Art.X.W + 2f + num;
            var pos = new Vector2(Screen.width / s - Visuals.DeathsMargin - num2, Visuals.DeathsMargin + Art.Skull.H / 2f);
            float wiggle = (float)Math.Sin(sineCounter) * wiggleCounter;

            bool flash = flashTimer > 0f && (Time.unscaledTime % (Visuals.DeathsFlashInterval * 2f)) < Visuals.DeathsFlashInterval;
            Color color = flash ? Hex(Visuals.DeathsFlashColor) : Color.white;
            color.a = alpha;
            var black = new Color(0f, 0f, 0f, alpha);

            Centered(Art.Skull, pos + new Vector2(Visuals.DeathsIconWidth / 2f, 0f), new Color(1f, 1f, 1f, alpha), s);
            Centered(Art.X, pos + new Vector2(Visuals.DeathsIconWidth + 2f + Art.X.W / 2f, 2f), color, s);
            // ActiveFont.DrawOutline: the text at eight offsets in black, then in colour; justified at its centre.
            var textCenter = pos + new Vector2(num2 - num / 2f, -wiggle * Visuals.DeathsWigglePx);
            float st = Visuals.DeathsStroke;
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                    if (i != 0 || j != 0) Text(text, textCenter + new Vector2(i * st, j * st), num, black, s);
            Text(text, textCenter, num, color, s);
            GUI.color = Color.white;
        }

        static void Text(string text, Vector2 center, float width, Color c, float s)
        {
            float x = center.x - width / 2f, top = center.y - Art.LineHeight / 2f;
            GUI.color = c;
            foreach (char ch in text)
            {
                GuiArt.Img g = Art.Digits[ch - '0'];
                GUI.DrawTexture(new Rect((x + g.XOffset) * s, (top + g.YOffset) * s, g.W * s, g.H * s), g.Tex);
                x += g.XAdvance;
            }
        }

        static void Centered(GuiArt.Img img, Vector2 center, Color c, float s)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect((center.x - img.W / 2f) * s, (center.y - img.H / 2f) * s, img.W * s, img.H * s), img.Tex);
        }

        static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString("#" + hex, out c);
            return c;
        }
    }
}
