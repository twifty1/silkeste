using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>One frame of Madeline: its sprite plus Celeste's hair metadata for that frame.</summary>
    public class CelesteFrame
    {
        public Sprite Sprite;
        public bool HasHair = true;
        public Vector2 HairOffset;   // Celeste px, Y down
        public int HairFrame;        // bangs frame
        public int CarryY;           // Celeste px, Y down: where a held glider sits (PlayerSprite.CarryYOffset)
    }

    public class CelesteAnim
    {
        public string Id;
        public float Delay;
        public bool Loop;
        public string Goto;
        public List<CelesteFrame> Frames = new List<CelesteFrame>();
    }

    /// <summary>
    /// Reads Madeline from the player's own Celeste install: the Gameplay atlas (Monocle "Packer" .meta plus
    /// the run-length encoded .data page) and the &lt;player&gt; block of Sprites.xml. Nothing is shipped.
    /// </summary>
    public class CelesteAssets
    {
        public readonly Dictionary<string, CelesteAnim> Anims = new Dictionary<string, CelesteAnim>();
        public Sprite HairNode;
        public readonly List<Sprite> Bangs = new List<Sprite>();
        public Vector2 Origin = new Vector2(Visuals.SpriteOriginX, Visuals.SpriteOriginY);
        public List<Sprite> Slash = new List<Sprite>();
        public List<Sprite> Smoke = new List<Sprite>();
        public Sprite Feather;
        public readonly List<Sprite> GliderOpen = new List<Sprite>(), GliderLoop = new List<Sprite>();
        public readonly List<Sprite> StarFlyWhite = new List<Sprite>();
        /// <summary>skins.style pico8: Celeste Classic's Madeline, sprites 0..7 of pico8/atlas without her hair pixels,
        /// the hair pixels alone in white (tinted by dash count), and draw_hair's circles (radius 1 and 2).</summary>
        public readonly Sprite[] PicoBody = new Sprite[8], PicoHair = new Sprite[8];
        public Sprite PicoCircle1, PicoCircle2;
        /// <summary>Sprites.xml &lt;goldberry&gt;: idle00-11 and wings00-09, centred.</summary>
        public readonly List<Sprite> GoldIdle = new List<Sprite>(), GoldWings = new List<Sprite>();

        internal struct SubTex { public int Page, X, Y, W, H, OffX, OffY, FullW, FullH; }

        // Decoded atlas pages, kept only while building sprites.
        internal class Page { public int W, H; public byte[] Rgba; }

        /// <summary>Heavy part (file reading and decoding); safe on a worker thread.</summary>
        public static Prepared Prepare(string celesteDir)
        {
            var p = new Prepared();
            string atlasDir = Path.Combine(celesteDir, "Content/Graphics/Atlases");
            using (var r = new BinaryReader(File.OpenRead(Path.Combine(atlasDir, "Gameplay.meta"))))
            {
                r.ReadInt32(); r.ReadString(); r.ReadInt32();
                int pages = r.ReadInt16();
                for (int i = 0; i < pages; i++)
                {
                    string dataName = r.ReadString();
                    int count = r.ReadInt16();
                    bool needPage = false;
                    for (int j = 0; j < count; j++)
                    {
                        string path = r.ReadString().Replace('\\', '/');
                        var s = new SubTex { Page = i, X = r.ReadInt16(), Y = r.ReadInt16(), W = r.ReadInt16(), H = r.ReadInt16() };
                        s.OffX = -r.ReadInt16(); s.OffY = -r.ReadInt16(); s.FullW = r.ReadInt16(); s.FullH = r.ReadInt16();
                        if (path.StartsWith("characters/player", StringComparison.Ordinal)  // player/, player_badeline/, ...
                            || path.StartsWith(SlashPath, StringComparison.Ordinal)
                            || path.StartsWith(SmokePath, StringComparison.Ordinal)
                            || path == FeatherPath
                            || path == PicoPath
                            || path.StartsWith(GoldPath, StringComparison.Ordinal)
                            || path.StartsWith(GliderPath, StringComparison.Ordinal)) { p.Textures[path] = s; needPage = true; }
                    }
                    p.Pages.Add(needPage ? DecodeData(Path.Combine(atlasDir, dataName + ".data")) : null);
                }
            }
            var doc = new XmlDocument();
            doc.Load(Path.Combine(celesteDir, "Content/Graphics/Sprites.xml"));
            p.Sprites = doc.SelectSingleNode("/Sprites");
            if (p.Sprites == null || p.Sprites.SelectSingleNode("player") == null) throw new Exception("Sprites.xml has no <player>");
            return p;
        }

        public class Prepared
        {
            internal readonly Dictionary<string, SubTex> Textures = new Dictionary<string, SubTex>();
            internal readonly List<Page> Pages = new List<Page>();
            internal XmlNode Sprites;
        }

        const string SlashPath = "effects/slash/";   // hooks.slash_fx (Celeste.SlashFx)
        const string SmokePath = "particles/smoke";  // hooks.dust_fx (Celeste.ParticleTypes.Dust: smoke0-3)
        const string FeatherPath = "particles/feather";   // hooks.bind_heal (Celeste FlyFeather.P_Collect)
        const string PicoPath = "pico8/atlas";             // skins.style pico8 (Celeste Classic)
        const string GoldPath = "collectables/goldberry/"; // the golden berry following her through a boss fight
        const string GliderPath = "objects/glider/";      // unlocks.glide (Celeste Glider: fall0-n, fallLoop0-n)

        /// <summary>Celeste.SlashFx's four frames, centred. Main thread only.</summary>
        public static List<Sprite> BuildSlash(Prepared p)
        {
            var frames = new List<Sprite>();
            SubTex t;
            for (int i = 0; p.Textures.TryGetValue(SlashPath + i.ToString("00"), out t); i++)
                frames.Add(MakeSprite(p.Pages[t.Page], t, new Vector2(t.FullW / 2f, t.FullH / 2f)));
            return frames;
        }

        // Monocle.VirtualTexture .data: int w, int h, bool alpha, then runs of (count, [a], b, g, r).
        internal static Page DecodeData(string file)
        {
            byte[] b = File.ReadAllBytes(file);
            int w = BitConverter.ToInt32(b, 0), h = BitConverter.ToInt32(b, 4);
            bool alpha = b[8] == 1;
            var rgba = new byte[w * h * 4];
            int pos = 9, o = 0, end = rgba.Length;
            while (o < end && pos < b.Length)
            {
                int run = b[pos] * 4;
                byte r, g, bl, a;
                if (alpha)
                {
                    a = b[pos + 1];
                    if (a > 0) { r = b[pos + 4]; g = b[pos + 3]; bl = b[pos + 2]; pos += 5; }
                    else { r = g = bl = 0; pos += 2; }
                }
                else { r = b[pos + 3]; g = b[pos + 2]; bl = b[pos + 1]; a = 255; pos += 4; }
                if (a > 0 && a < 255)  // stored premultiplied; Unity's sprite shader wants straight alpha
                {
                    r = (byte)Math.Min(255, r * 255 / a); g = (byte)Math.Min(255, g * 255 / a); bl = (byte)Math.Min(255, bl * 255 / a);
                }
                for (int k = o, e = Math.Min(o + run, end); k < e; k += 4) { rgba[k] = r; rgba[k + 1] = g; rgba[k + 2] = bl; rgba[k + 3] = a; }
                o += run;
            }
            return new Page { W = w, H = h, Rgba = rgba };
        }

        /// <summary>Unity part (textures and sprites) for one skin (sheets/skins.json celeste_sprite); main thread only.
        /// A skin like player_badeline has copy="player": the player's animations and hair metadata with its own frames.</summary>
        /// <param name="eraseBackpack">A copy skin (player_badeline) without her backpack: her own frames, except where
        /// Madeline's backpack and no-backpack frames differ; there the no-backpack pixels, in this colour map
        /// (Madeline's colours to hers). Everything else stays her real art (user 2026-10-12).</param>
        /// <param name="fallbackPalette">skins.fallback_palette: Madeline's frames that a copy skin lacks (Badeline's bench
        /// sleep, most of the Ghost's) recoloured into the skin's own colours.</param>
        public static CelesteAssets Build(Prepared p, string spriteId, string palette = "none", string eraseBackpack = null, string fallbackPalette = "none", string bakedHair = null)
        {
            var a = new CelesteAssets();
            var cache = new Dictionary<string, Sprite>();
            Dictionary<int, Color32> ownMap = ParsePalette(palette);   // skins.palette: her clothes recoloured, pixel for pixel
            Dictionary<int, Color32> fallbackMap = ParsePalette(fallbackPalette);
            // Celeste draws her hair into some frames (bench sleep, waking, sitting) in AC3232 with 5A1A1A shade:
            // a palette skin gives those pixels its own hair colour (skins.hair_normal, shade at Celeste's ratio).
            if (ownMap != null && bakedHair != null && !ownMap.ContainsKey(0xAC3232))
            {
                int h = int.Parse(bakedHair, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var c = new Color32((byte)(h >> 16), (byte)(h >> 8), (byte)h, 255);
                ownMap[0xAC3232] = c;
                ownMap[0x5A1A1A] = new Color32((byte)(c.r * 0.52f), (byte)(c.g * 0.52f), (byte)(c.b * 0.52f), 255);
            }
            XmlNode ownNode = p.Sprites.SelectSingleNode(spriteId);
            string copyRoot = ownNode != null && Attr(ownNode, "copy", null) != null ? Attr(p.Sprites.SelectSingleNode(Attr(ownNode, "copy", null)), "path", null) : null;
            Dictionary<int, Color32> eraseMap = eraseBackpack != null ? ParsePalette(eraseBackpack) ?? new Dictionary<int, Color32>() : null;
            string ownPath = Attr(p.Sprites.SelectSingleNode(spriteId), "path", "");
            Func<string, Vector2, Sprite> sprite = (name, origin) =>
            {
                Sprite s;
                if (cache.TryGetValue(name, out s)) return s;
                SubTex t;
                if (!p.Textures.TryGetValue(name, out t)) return null;
                // a frame borrowed from the copied sprite (Madeline's) wears the skin's fallback colours
                Dictionary<int, Color32> map = fallbackMap != null && copyRoot != null && name.StartsWith(copyRoot, StringComparison.Ordinal) && !name.Contains("/hair") && !name.Contains("/bangs") ? fallbackMap : ownMap;
                SubTex withPack, noPack;
                string frame = name.StartsWith(ownPath, StringComparison.Ordinal) ? name.Substring(ownPath.Length) : null;
                if (eraseMap != null && frame != null
                    && p.Textures.TryGetValue("characters/player/" + frame, out withPack) && p.Textures.TryGetValue("characters/player_no_backpack/" + frame, out noPack)
                    && withPack.FullW == t.FullW && withPack.FullH == t.FullH && noPack.FullW == t.FullW && noPack.FullH == t.FullH)
                {
                    Color32[] own = Full(p.Pages[t.Page], t), mad = Full(p.Pages[withPack.Page], withPack), bare = Full(p.Pages[noPack.Page], noPack);
                    for (int i = 0; i < own.Length; i++)
                    {
                        if (Same(mad[i], bare[i])) continue;
                        Color32 c = bare[i], swap;
                        if (c.a > 0 && eraseMap.TryGetValue((c.r << 16) | (c.g << 8) | c.b, out swap)) { swap.a = c.a; c = swap; }
                        own[i] = c;
                    }
                    s = FromPixels(t.FullW, t.FullH, own, origin);
                }
                else s = MakeSprite(p.Pages[t.Page], t, origin, map);
                s.name = name;
                cache[name] = s;
                return s;
            };

            XmlNode skin = p.Sprites.SelectSingleNode(spriteId);
            if (skin == null) throw new Exception("Sprites.xml has no <" + spriteId + ">");
            string basePath = Attr(skin, "path", "");
            string copy = Attr(skin, "copy", null);
            XmlNode px = copy != null ? p.Sprites.SelectSingleNode(copy) : skin;
            XmlNode originNode = px.SelectSingleNode("Origin");
            if (originNode != null) a.Origin = new Vector2(F(Attr(originNode, "x", "16")), F(Attr(originNode, "y", "32")));

            // Hair metadata per frame path ("idle" -> offsets for idle00, idle01, ...).
            var meta = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlNode f in px.SelectNodes("Metadata/Frames")) meta[Attr(f, "path", "")] = Attr(f, "hair", "").Split('|');
            // Carry heights per frame ("idle_carry" -> -1,-1,-1,0,...), PlayerSprite.CarryYOffset: where a held glider sits.
            var carryMeta = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlNode f in px.SelectNodes("Metadata/Frames")) { string c = Attr(f, "carry", null); if (c != null) carryMeta[Attr(f, "path", "")] = c.Split(','); }

            foreach (XmlNode n in px.ChildNodes)
            {
                if (n.NodeType != XmlNodeType.Element || (n.Name != "Anim" && n.Name != "Loop")) continue;
                string path = Attr(n, "path", "");
                var all = new List<string>();
                // A copy skin (player_playback, player_no_backpack) lacks some of the player's frames; those come from
                // the sprite it copies, as Celeste's PlayerSprite falls back to them.
                foreach (string root in new[] { basePath, copy != null ? Attr(px, "path", basePath) : basePath })
                {
                    for (int i = 0; p.Textures.ContainsKey(root + path + i.ToString("00")); i++) all.Add(root + path + i.ToString("00"));
                    if (all.Count == 0 && p.Textures.ContainsKey(root + path)) all.Add(root + path);  // single unnumbered frame, as Monocle allows
                    if (all.Count > 0) break;
                }
                if (all.Count == 0) continue;
                var anim = new CelesteAnim { Id = Attr(n, "id", ""), Delay = F(Attr(n, "delay", "0")), Loop = n.Name == "Loop", Goto = Attr(n, "goto", null) };
                string[] hair, carry;
                meta.TryGetValue(path.TrimEnd('/'), out hair);
                carryMeta.TryGetValue(path.TrimEnd('/'), out carry);
                foreach (int index in ParseFrames(Attr(n, "frames", null), all.Count))
                {
                    // Like Celeste.PlayerSprite.CreateFramesMetadata: no metadata, "x" or "" means no hair.
                    var frame = new CelesteFrame { Sprite = sprite(all[index], a.Origin), HasHair = false };
                    if (carry != null && index < carry.Length) frame.CarryY = int.Parse(carry[index].Trim(), CultureInfo.InvariantCulture);
                    if (hair != null && index < hair.Length)
                    {
                        string h = hair[index].Trim();
                        if (!h.Equals("x", StringComparison.OrdinalIgnoreCase) && h.Length > 0)
                        {
                            frame.HasHair = true;
                            string[] parts = h.Split(':');
                            string[] xy = parts[0].Split(',');
                            frame.HairOffset = new Vector2(F(xy[0]), F(xy[1]));
                            frame.HairFrame = parts.Length > 1 ? int.Parse(parts[1].Trim(), CultureInfo.InvariantCulture) : 0;
                        }
                    }
                    anim.Frames.Add(frame);
                }
                a.Anims[anim.Id] = anim;
            }

            // visuals.no_backpack_aliases: Celeste drew no no-backpack frames for some animations, which would fall back
            // to frames with the backpack; those use a no-backpack stand-in instead.
            if (spriteId == "player_no_backpack")
            {
                string fallbackRoot = Attr(px, "path", "characters/player/");
                foreach (string rule in Visuals.NoBackpackAliases.Split(';'))
                {
                    string[] kv = rule.Split('=');
                    string[] how = kv[1].Split(':');
                    CelesteAnim target, stand;
                    if (!a.Anims.TryGetValue(kv[0], out target) || !a.Anims.TryGetValue(how[0], out stand) || stand.Frames.Count == 0) continue;
                    if (target.Frames.Count > 0 && target.Frames[0].Sprite != null && !target.Frames[0].Sprite.name.StartsWith(fallbackRoot, StringComparison.Ordinal)) continue;   // has its own frames
                    var frames = new List<CelesteFrame>(stand.Frames);
                    if (how.Length > 1 && how[1] == "last") frames = new List<CelesteFrame> { stand.Frames[stand.Frames.Count - 1] };
                    else if (how.Length > 1 && how[1] == "reverse") frames.Reverse();
                    target.Frames = frames;
                }
            }

            a.HairNode = sprite("characters/player/hair00", new Vector2(5, 5));
            for (int i = 0; p.Textures.ContainsKey("characters/player/bangs" + i.ToString("00")); i++)
                a.Bangs.Add(sprite("characters/player/bangs" + i.ToString("00"), new Vector2(5, 5)));
            foreach (AnimRow row in Animations.All)
                if (!a.Anims.ContainsKey(row.CelesteAnim)) throw new Exception("Celeste animation missing: " + row.CelesteAnim);
            a.Slash = BuildSlash(p);
            SubTex smokeTex;
            for (int i = 0; p.Textures.TryGetValue(SmokePath + i, out smokeTex); i++)
                a.Smoke.Add(MakeSprite(p.Pages[smokeTex.Page], smokeTex, new Vector2(smokeTex.FullW / 2f, smokeTex.FullH / 2f)));
            // Sprites.xml <glider>: Justify (0.5, glider_justify_y); "fall" opens it, then "fallLoop".
            foreach (var kv in new[] { new KeyValuePair<string, List<Sprite>>("fall", a.GliderOpen), new KeyValuePair<string, List<Sprite>>("fallLoop", a.GliderLoop) })
            {
                SubTex gt;
                for (int i = 0; p.Textures.TryGetValue(GliderPath + kv.Key + i, out gt); i++)
                    kv.Value.Add(MakeSprite(p.Pages[gt.Page], gt, new Vector2(gt.FullW / 2f, gt.FullH * Visuals.GliderJustifyY)));
            }
            SubTex featherTex;
            if (p.Textures.TryGetValue(FeatherPath, out featherTex)) a.Feather = MakeSprite(p.Pages[featherTex.Page], featherTex, new Vector2(featherTex.FullW / 2f, featherTex.FullH / 2f));
            // Player.Render draws startStarFlyWhite over the startStarFly frames, tinted starFlyColor * progress.
            SubTex whiteTex;
            for (int i = 0; p.Textures.TryGetValue(basePath + "startStarFlyWhite" + i.ToString("00"), out whiteTex); i++)
                a.StarFlyWhite.Add(sprite(basePath + "startStarFlyWhite" + i.ToString("00"), a.Origin));
            foreach (var kv in new[] { new KeyValuePair<string, List<Sprite>>("idle", a.GoldIdle), new KeyValuePair<string, List<Sprite>>("wings", a.GoldWings) })
            {
                SubTex gt;
                for (int i = 0; p.Textures.TryGetValue(GoldPath + kv.Key + i.ToString("00"), out gt); i++)
                    kv.Value.Add(MakeSprite(p.Pages[gt.Page], gt, new Vector2(gt.FullW / 2f, gt.FullH / 2f)));
            }
            SubTex picoTex;
            if (p.Textures.TryGetValue(PicoPath, out picoTex)) BuildPico(a, p.Pages[picoTex.Page], picoTex);
            return a;
        }

        // Celeste Classic's player sprites: spr(n) is the 8x8 cell n of the PICO-8 sheet (16 per row). Her hair is
        // colour 8 there, swapped with pal(8, ...) by dash count, so it goes into its own white sprite to tint.
        static readonly Color32 PicoHairRed = new Color32(255, 0, 77, 255);

        static void BuildPico(CelesteAssets a, Page page, SubTex t)
        {
            float ppu = Visuals.PxPerUnit / Visuals.PicoScale;   // one PICO-8 pixel is pico_scale Celeste px
            for (int n = 0; n < 8; n++)
            {
                var body = new Color32[64]; var hair = new Color32[64];
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                    {
                        int fx = n * 8 + x - t.OffX, fy = y - t.OffY;   // the cell inside the trimmed atlas frame
                        if (fx < 0 || fy < 0 || fx >= t.W || fy >= t.H) continue;
                        int src = ((t.Y + fy) * page.W + (t.X + fx)) * 4;
                        var c = new Color32(page.Rgba[src], page.Rgba[src + 1], page.Rgba[src + 2], page.Rgba[src + 3]);
                        if (c.a == 0) continue;
                        int dst = (7 - y) * 8 + x;
                        bool isHair = Math.Abs(c.r - PicoHairRed.r) < 24 && Math.Abs(c.g - PicoHairRed.g) < 24 && Math.Abs(c.b - PicoHairRed.b) < 24;
                        if (isHair) hair[dst] = new Color32(255, 255, 255, 255); else body[dst] = c;
                    }
                a.PicoBody[n] = Pixels(8, 8, body, new Vector2(0.5f, 0f), ppu);
                a.PicoHair[n] = Pixels(8, 8, hair, new Vector2(0.5f, 0f), ppu);
            }
            // circfill(x, y, r) on PICO-8: r = 1 is a plus, r = 2 a 5x5 with the corners cut.
            a.PicoCircle1 = Pixels(3, 3, Shape(3, "010111010"), new Vector2(0.5f, 0.5f), ppu);
            a.PicoCircle2 = Pixels(5, 5, Shape(5, "0111011111111111111101110"), new Vector2(0.5f, 0.5f), ppu);
        }

        static Color32[] Shape(int size, string bits)
        {
            var px = new Color32[size * size];
            for (int i = 0; i < bits.Length; i++) if (bits[i] == '1') px[i] = new Color32(255, 255, 255, 255);
            return px;
        }

        static Sprite Pixels(int w, int h, Color32[] px, Vector2 pivot, float ppu)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
        }

        // A sub-texture's full (untrimmed) frame, rows bottom-up like Unity's textures.
        static Color32[] Full(Page page, SubTex t)
        {
            var px = new Color32[t.FullW * t.FullH];
            for (int y = 0; y < t.H; y++)
                for (int x = 0; x < t.W; x++)
                {
                    int dx = t.OffX + x, dy = t.OffY + y;
                    if (dx < 0 || dy < 0 || dx >= t.FullW || dy >= t.FullH) continue;
                    int src = ((t.Y + y) * page.W + (t.X + x)) * 4;
                    px[(t.FullH - 1 - dy) * t.FullW + dx] = new Color32(page.Rgba[src], page.Rgba[src + 1], page.Rgba[src + 2], page.Rgba[src + 3]);
                }
            return px;
        }

        static bool Same(Color32 a, Color32 b) { return a.a == b.a && (a.a == 0 || (a.r == b.r && a.g == b.g && a.b == b.b)); }

        static Sprite FromPixels(int w, int h, Color32[] px, Vector2 originPx)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(originPx.x / w, 1f - originPx.y / h), Visuals.PxPerUnit, 0, SpriteMeshType.FullRect);
        }

        // "5B6EE1>E9ECF5;...": exact Celeste colours to swap (RGB packed in an int).
        static Dictionary<int, Color32> ParsePalette(string palette)
        {
            if (string.IsNullOrEmpty(palette) || palette == "none") return null;
            var map = new Dictionary<int, Color32>();
            foreach (string pair in palette.Split(';'))
            {
                string[] ab = pair.Split('>');
                int from = int.Parse(ab[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                int to = int.Parse(ab[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                map[from] = new Color32((byte)(to >> 16), (byte)(to >> 8), (byte)to, 255);
            }
            return map;
        }

        static Sprite MakeSprite(Page page, SubTex t, Vector2 originPx, Dictionary<int, Color32> map = null)
        {
            var tex = new Texture2D(t.FullW, t.FullH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[t.FullW * t.FullH];
            for (int y = 0; y < t.H; y++)
                for (int x = 0; x < t.W; x++)
                {
                    int src = ((t.Y + y) * page.W + (t.X + x)) * 4;
                    int dx = t.OffX + x, dy = t.OffY + y;
                    if (dx < 0 || dy < 0 || dx >= t.FullW || dy >= t.FullH) continue;
                    var c = new Color32(page.Rgba[src], page.Rgba[src + 1], page.Rgba[src + 2], page.Rgba[src + 3]);
                    Color32 swap;
                    if (map != null && c.a > 0 && map.TryGetValue((c.r << 16) | (c.g << 8) | c.b, out swap)) { swap.a = c.a; c = swap; }
                    px[(t.FullH - 1 - dy) * t.FullW + dx] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var pivot = new Vector2(originPx.x / t.FullW, 1f - originPx.y / t.FullH);
            return Sprite.Create(tex, new Rect(0, 0, t.FullW, t.FullH), pivot, Visuals.PxPerUnit, 0, SpriteMeshType.FullRect);
        }

        // Sprites.xml frame lists: "0-3,5*2,7" (ranges and repeats).
        static IEnumerable<int> ParseFrames(string spec, int count)
        {
            if (string.IsNullOrEmpty(spec)) { for (int i = 0; i < count; i++) yield return i; yield break; }
            foreach (string part in spec.Split(','))
            {
                string s = part.Trim();
                int times = 1, star = s.IndexOf('*');
                if (star >= 0) { times = int.Parse(s.Substring(star + 1), CultureInfo.InvariantCulture); s = s.Substring(0, star); }
                int dash = s.IndexOf('-');
                int from = int.Parse(dash >= 0 ? s.Substring(0, dash) : s, CultureInfo.InvariantCulture);
                int to = dash >= 0 ? int.Parse(s.Substring(dash + 1), CultureInfo.InvariantCulture) : from;
                for (int t = 0; t < times; t++)
                    for (int i = from; i <= to; i++)
                        if (i < count) yield return i;
            }
        }

        static string Attr(XmlNode n, string name, string fallback)
        {
            XmlAttribute a = n.Attributes == null ? null : n.Attributes[name];
            return a == null ? fallback : a.Value;
        }
        static float F(string s) { return float.Parse(s.Trim(), CultureInfo.InvariantCulture); }
    }
}
