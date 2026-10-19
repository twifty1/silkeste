using System;
using System.Collections.Generic;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Draws Madeline where Hornet is: Celeste's own frames and animation timings, the PlayerHair simulation
    /// (bangs plus hair00 nodes with a black outline, red with a dash, blue without, white flash on refill)
    /// and a dash afterimage. Hornet's tk2d mesh is hidden with forceRenderingOff, so the game's own
    /// MeshRenderer.enabled still says whether the hero should be visible.
    /// </summary>
    public class MadelineView
    {
        static readonly AccessTools.FieldRef<HeroController, MeshRenderer> HeroRenderer = AccessTools.FieldRefAccess<HeroController, MeshRenderer>("renderer");
        static readonly AccessTools.FieldRef<HeroController, Collider2D> Col = AccessTools.FieldRefAccess<HeroController, Collider2D>("col2d");
        static readonly AccessTools.FieldRef<HeroController, Rigidbody2D> Rb = AccessTools.FieldRefAccess<HeroController, Rigidbody2D>("rb2d");

        CelesteAssets assets;
        Color Normal, Used, TwoDashes;   // sheets/skins.json hair colours
        readonly MadelineController ctrl;
        readonly GameObject root;
        readonly SpriteRenderer body;
        readonly SpriteRenderer[] hair = new SpriteRenderer[AssistMode.MaxHair];
        readonly SpriteRenderer[,] hairOutline = new SpriteRenderer[AssistMode.MaxHair, 4];
        readonly Vector2[] nodes = new Vector2[AssistMode.MaxHair];
        int hairN = (int)Visuals.HairCount;   // assist.hair_length, read when the settings are closed
        readonly List<KeyValuePair<SpriteRenderer, float>> trails = new List<KeyValuePair<SpriteRenderer, float>>();
        readonly Material material;
        CelesteAnim anim;
        int frameIndex;
        float frameTimer, wave;
        Color hairColor;
        bool fastJump, started;
        static readonly Color Flash = Hex(Visuals.HairColorFlash), Tired = Hex(Visuals.TiredFlashColor);
        static readonly Vector2[] OutlineOffsets = { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1) };

        SkinRow skin;

        public MadelineView(CelesteAssets assets, SkinRow skin, MadelineController ctrl)
        {
            this.skin = skin;
            SetColors(skin);
            this.assets = assets;
            this.ctrl = ctrl;
            material = new Material(Shader.Find("Sprites/Default"));
            root = new GameObject("Madeline");
            UnityEngine.Object.DontDestroyOnLoad(root);
            for (int i = 0; i < hair.Length; i++)
            {
                for (int k = 0; k < 4; k++) hairOutline[i, k] = MakeRenderer("HairOutline" + i + "_" + k, -2);
                hair[i] = MakeRenderer("Hair" + i, -1);
            }
            body = MakeRenderer("Body", 0);
            glider = MakeRenderer("Glider", 0);
            glider.enabled = false;
            starWhite = MakeRenderer("StarFlyWhite", 0);
            starWhite.enabled = false;
            goldBerry = MakeRenderer("GoldenBerry", 0);
            goldBerry.enabled = false;
            BossCard.GoldenLost += OnGoldenLost;
            catEars = MakeRenderer("CatEars", 0); catEarsInner = MakeRenderer("CatEarsInner", 0);
            knightMask = MakeRenderer("KnightMask", 0); knightEyes = MakeRenderer("KnightEyes", 0); needle = MakeRenderer("HornetNeedle", 0);
            for (int i = 0; i < catTail.Length; i++) { catTailOutline[i] = MakeRenderer("CatTailOutline" + i, -3); catTail[i] = MakeRenderer("CatTail" + i, -2); }
            picoMask = MakeRenderer("PicoHair", 0);
            for (int i = 0; i < picoCircles.Length; i++) picoCircles[i] = MakeRenderer("PicoHairCircle" + i, -1);
            ShowPico(false);
            hairColor = Normal;
            try { BuildLookSprites(); } catch (Exception e) { Plugin.Log.LogWarning("look sprites: " + e.Message); }
            dashFx = new DashFx(root.transform, material, assets.Slash);
            Particles.Init(root.transform, material, assets.Smoke, assets.Feather);
            ctrl.Dashed += SpawnTrail;
            ctrl.SkillUsed += OnSkillUsed;
            ctrl.DashBegan += OnDashBegan;
            ctrl.DashLaunched += OnDashLaunched;
            ctrl.GlideStarted += OnGlideStarted;
            Play(Animations.Idle.CelesteAnim);
        }

        readonly DashFx dashFx;
        int lastSortingLayer, lastSortingOrder;

        void OnDashBegan() { dashFx.Freeze(root); }

        // silk skills: Thread Storm bursts white thread, Sharpdart pale silk, Rune Rage Celeste's red booster
        void OnSkillUsed(string tool)
        {
            if (!root.activeSelf || body == null) return;
            Vector3 c = body.transform.position + new Vector3(0f, 8f * Visuals.SpriteScale / Visuals.PxPerUnit, 0f);
            Color col = tool == "Silk Bomb" ? new Color(1f, 0.25f, 0.25f, 0.95f) : new Color(1f, 1f, 1f, 0.9f);
            for (int i = 0; i < 8; i++) Particles.DustTinted(c, i * Mathf.PI / 4f, 2, col, 3f);
            if (tool == "Silk Bomb") boostT = 0.6f;
        }
        float boostT;

        void OnDashLaunched(Vector2 shakeDir, Vector2 slashDir)
        {
            if (!root.activeSelf) return;
            Vector3 center = body.transform.position + new Vector3(0f, Visuals.DashFxCenterPx * Visuals.SpriteScale / Visuals.PxPerUnit, 0f);
            dashFx.Launch(root, center, shakeDir, slashDir, lastSortingLayer, lastSortingOrder);
        }

        SkinRow colorSkin; bool hasColorSkin;
        int hairVersion = -1;
        void SetColors(SkinRow skin)
        {
            colorSkin = skin; hasColorSkin = true; hairVersion = AssistMode.HairVersion;
            float k = skin.GlowCompensation;
            // assist.hair_*: the player's own colour for each dash count, or the skin's
            Normal = UnderGlow(Hex(AssistMode.HairHex(AssistOptions.HairOne, skin.HairNormal)), k);
            Used = UnderGlow(Hex(AssistMode.HairHex(AssistOptions.HairZero, skin.HairUsed)), k);
            TwoDashes = UnderGlow(Hex(AssistMode.HairHex(AssistOptions.HairTwo, skin.HairTwoDashes)), k);
        }

        // Silksong screens a white glow over the hero; pre-darken so Celeste's colour is what comes out (skins.glow_compensation).
        static Color UnderGlow(Color c, float k)
        {
            Func<float, float> f = v => Mathf.Clamp01(1f - (1f - v) / (1f - k));
            return new Color(f(c.r), f(c.g), f(c.b), c.a);
        }

        /// <summary>Swap to another skin (sheets/skins.json): same animation, new frames and hair colours.</summary>
        public void SetSkin(CelesteAssets next, SkinRow skin)
        {
            string current = anim != null ? anim.Id : Animations.Idle.CelesteAnim;
            assets = next;
            this.skin = skin;
            SetColors(skin);
            hairColor = Normal;
            anim = null;
            Play(current, true);
            picoStarted = false;
        }

        // ---- the golden berry of a hitless boss attempt (visuals.golden_*) ------------------------------------
        // Celeste's golden strawberry follows Madeline (a Follower easing to a spot behind her, bobbing) while a
        // boss fight is still hitless and its berry not yet collected. The first hit makes it laugh and fly off
        // on its wings, like a winged strawberry when you dash (Strawberry.OnDash: strawberry_laugh, flyaway).

        readonly SpriteRenderer goldBerry;
        Vector3 goldPos;
        float goldAppear, goldFrameTime, goldFlyT = -1f, goldFlyVel;
        bool goldShown;

        void OnGoldenLost()
        {
            if (!goldShown) return;
            goldFlyT = 0f;
            goldFlyVel = 0f;
            Sfx.Play(Sounds.GoldenLaugh.Id);
        }

        void UpdateGolden(Vector3 feet, int facing, float dt)
        {
            float unit = Visuals.SpriteScale / Visuals.PxPerUnit;
            if (goldFlyT >= 0f)
            {
                // flying away: a beat to laugh, then up and off, flapping, fading out
                goldFlyT += dt;
                if (goldFlyT > Visuals.GoldenLaughTime)
                {
                    if (goldFlyVel == 0f) Sfx.Play(Sounds.GoldenFlyaway.Id);
                    goldFlyVel += Visuals.GoldenFlyAccel * dt;
                    goldPos.y += goldFlyVel * unit * dt;
                }
                goldFrameTime += dt;
                if (assets.GoldWings.Count > 0) goldBerry.sprite = assets.GoldWings[(int)(goldFrameTime / 0.08f) % Math.Min(9, assets.GoldWings.Count)];
                float fade = Mathf.Clamp01(1f - (goldFlyT - Visuals.GoldenLaughTime - 0.6f) / 0.6f);
                goldBerry.color = new Color(1f, 1f, 1f, fade);
                goldBerry.transform.position = goldPos;
                if (fade <= 0f) { goldFlyT = -1f; goldShown = false; goldBerry.enabled = false; }
                return;
            }
            // only for the start of the fight: then it rises off to the HUD corner (BossCard.DrawHudBerry)
            bool want = BossCard.GoldenRun && assets.GoldIdle.Count > 0 && ctrl.Active && BossCard.FightTime < Visuals.GoldenIntroTime + 0.3f;
            if (want && !goldShown) { goldShown = true; goldAppear = 0f; goldPos = feet + new Vector3(0f, Visuals.GoldenFollowY * unit, 0f); }
            if (!want && goldShown) goldShown = false;   // the fight ended (a win, a death, or she left)
            goldBerry.enabled = goldShown;
            if (!goldShown) return;
            goldAppear = Math.Min(1f, goldAppear + dt / 0.3f);
            Vector3 target = feet + new Vector3(-facing * Visuals.GoldenFollowX * unit, (Visuals.GoldenFollowY + Mathf.Sin(Time.time * 4f) * 1.5f) * unit, -Visuals.LayerDepthStep * 1.5f);
            goldPos = Vector3.Lerp(goldPos, target, 1f - Mathf.Exp(-Visuals.GoldenFollowEase * dt));
            goldFrameTime += dt;
            // Sprites.xml <goldberry> idle: frames 0-5 four times, then 0-7, at 0.08 s
            int f = (int)(goldFrameTime / 0.08f) % 32;
            int frame = f < 24 ? f % 6 : f - 24;
            goldBerry.sprite = assets.GoldIdle[Math.Min(frame, assets.GoldIdle.Count - 1)];
            goldBerry.transform.position = goldPos;
            float s = Visuals.SpriteScale * (0.6f + 0.4f * goldAppear);
            goldBerry.transform.localScale = new Vector3(s, s, 1f);
            float leave = Mathf.Clamp01((BossCard.FightTime - Visuals.GoldenIntroTime) / 0.3f);
            if (leave > 0f) { goldPos += new Vector3(0f, 400f * unit * dt, 0f); goldBerry.transform.position = goldPos; }
            goldBerry.color = new Color(1f, 1f, 1f, goldAppear * (1f - leave));
        }

        // ---- skins.style pico8: Celeste Classic (Pico8/Classic.cs player.draw / draw_hair) ----------------------

        readonly SpriteRenderer picoMask;
        readonly SpriteRenderer[] picoCircles = new SpriteRenderer[5];
        readonly Vector2[] picoNodes = new Vector2[5];
        bool picoStarted;
        float picoRun;

        bool IsPico { get { return skin.Style == "pico8" && assets.PicoBody[1] != null; } }

        void ShowPico(bool on)
        {
            if (picoMask.enabled != on) picoMask.enabled = on;
            foreach (SpriteRenderer c in picoCircles) if (c.enabled != on) c.enabled = on;
        }

        // Classic's sprite choice: 1 standing, 1-4 running (spr_off += 0.25 a frame at 30 fps), 3 in the air,
        // 5 in the air against a wall, 6 holding down, 7 holding up.
        int PicoSprite(HeroController h, float dt)
        {
            if (!ctrl.Active) return h.playerData.atBench || (anim != null && (anim.Id == "sleep" || anim.Id == "asleep")) ? 6 : 1;
            if (ctrl.State == MadelineController.StClimb || ctrl.WallSlideDir != 0) return 5;
            if (!ctrl.OnGround) return 3;
            if (ctrl.Ducking) return 6;
            if (anim != null && anim.Id == Animations.LookUp.CelesteAnim) return 7;
            if (Math.Abs(ctrl.Speed.x) > 1f) { picoRun += dt * 7.5f; return 1 + (int)picoRun % 4; }
            picoRun = 0f;
            return 1;
        }

        void DrawPico(HeroController h, Vector3 feet, int facing, float sx, float sy, float vs, float dt)
        {
            int spr = PicoSprite(h, dt);
            body.sprite = assets.PicoBody[spr];
            body.transform.localScale = new Vector3(facing * sx * vs, sy * vs, 1f);
            float step = Visuals.LayerDepthStep;
            picoMask.sprite = assets.PicoHair[spr];
            picoMask.color = hairColor;
            picoMask.transform.position = feet + new Vector3(0f, 0f, -step / 2f);
            picoMask.transform.localScale = body.transform.localScale;
            // draw_hair: from (x + 4 - flip.x * 2, y + (down and 4 or 3)), each node eases 1/1.5 of the way to the
            // one before it, half a pixel lower, every PICO-8 frame (30 fps); sizes 2, 2, 1, 1, 1.
            float pp = Visuals.PicoScale * vs / Visuals.PxPerUnit;
            bool down = ctrl.Active && (ctrl.Ducking || spr == 6);
            Vector2 last = new Vector2(feet.x + (facing > 0 ? 0f : -2f) * pp * Math.Abs(sx), feet.y + (8f - (down ? 4f : 3f)) * pp * sy);
            if (!picoStarted) { for (int i = 0; i < picoNodes.Length; i++) picoNodes[i] = last; picoStarted = true; }
            float k = 1f - Mathf.Pow(1f - 1f / 1.5f, dt * 30f);
            for (int i = 0; i < picoNodes.Length; i++)
            {
                Vector2 target = last + new Vector2(0f, -0.5f * pp);
                picoNodes[i] += (target - picoNodes[i]) * k;
                SpriteRenderer c = picoCircles[i];
                c.sprite = i < 2 ? assets.PicoCircle2 : assets.PicoCircle1;
                c.color = hairColor;
                c.transform.position = new Vector3(picoNodes[i].x, picoNodes[i].y, feet.z + step);
                c.transform.localScale = new Vector3(vs, vs, 1f);
                last = picoNodes[i];
            }
        }

        // ---- Cat skin (skins.style "cat"): pixel cat ears on her head and a swishing tail, in her hair colour ----
        readonly SpriteRenderer catEars, catEarsInner;
        bool earsSetting;
        /// <summary>The look sprites (ears, tail, Knight mask) for the settings preview.</summary>
        public static Sprite EarSprite, EarInnerSprite, TailSprite, TailOutlineSprite, MaskSprite;
        public const int TailLength = 6;
        readonly SpriteRenderer[] catTail = new SpriteRenderer[TailLength], catTailOutline = new SpriteRenderer[TailLength];
        Sprite earSprite, earInnerSprite, tailSprite, tailOutlineSprite;

        // O outline, H hair (tinted), P pink inside; the bottom row tucks under her bangs
        static readonly string[] EarArt =
        {
            ".O.......O.",
            "OHO.....OHO",
            "OPHO...OHPO",
            "OPHHO.OHHPO",
            "OHHHHHHHHHO",
        };

        Sprite PixelSprite(string[] art, Func<char, Color> color, Vector2 pivot)
        {
            int w = art[0].Length, hgt = art.Length;
            var tex = new Texture2D(w, hgt, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < hgt; y++)
                for (int x = 0; x < w; x++) tex.SetPixel(x, hgt - 1 - y, color(art[y][x]));
            tex.Apply();
            float ppu = assets.HairNode != null ? assets.HairNode.pixelsPerUnit : 100f;
            return Sprite.Create(tex, new Rect(0, 0, w, hgt), pivot, ppu);
        }

        // ---- Knight skin (skins.style "knight"): no hair, the Knight's white horned mask; the eyes show the dash colour ----
        readonly SpriteRenderer knightMask, knightEyes;
        bool atBenchNow;
        float soulT;   // on a bench her hair nodes drift above the sleeping pose: the mask follows the drawn sprite
        Sprite maskSprite, eyesSprite, hornetMaskSprite, needleSprite;
        SpriteRenderer needle;
        /// <summary>Skins drawn as Madeline's body under a pixel mask (no hair): the Knight and Hornet.</summary>
        public static bool Masked(string style) { return style == "knight" || style == "hornet"; }
        public static Sprite HornetMaskSprite;
        public static Sprite MaskFor(string style) { return style == "hornet" ? HornetMaskSprite : MaskSprite; }
        // Hornet's mask (user 2026-10-12, after the user's pixel reference): two long horns swept back, a pointed
        // chin, slanted black eyes; drawn facing right, S the shaded underside
        // after the user's second reference (2026-10-12): tall cream horns curving back with a slit between them,
        // a round mask with one big cross-shaped eye, navy outline; drawn facing right
        // read pixel for pixel from the user's reference (facing left there, mirrored here to face right)
        static readonly string[] HornetMaskArt =
        {
            "....OOOOO...........",
            ".O...OWWWOO.........",
            ".OWO....OWWWOO......",
            ".OWWO...OWWWWO......",
            ".OWWO....OWWWWOO....",
            "..OWWOO....OWWWO....",
            "...OWWWWO...OWWWO...",
            "...OWWWWO...WWWWO...",
            "....OWWWWO..OWWWWO..",
            ".....OWWWWOOWWWWWWO.",
            "....OWWWWWWWWWWWWWO.",
            ".....OWWWWWWWWWWWWWO",
            ".....OWWWWWWWWWWWWWO",
            "......OOWWWWOOWWOOWO",
            ".......OOWWOOOOWOOWO",
            "........OWWWOOOWOWWO",
            ".........OWWOOWWWWO.",
            ".........OOOOOOOO...",
        };
        // her needle, held point-down behind her: drawn facing right
        static readonly string[] NeedleArt =
        {
            "...........OOO",
            "..........ON.O",
            ".........ONOO.",
            "........ONO...",
            ".......ONO....",
            "......ONO.....",
            ".....ONO......",
            "....ONO.......",
            "...ONO........",
            "..ONO.........",
            ".ONO..........",
            "ONO...........",
            "OO............",
        };
        // the Knight's mask: two tapering horns, a wide rounded shell, big tall eyes; S is the mask's shaded side
        // the Knight's mask after the user's pixel reference: thick hooked horns, a flat brow, cross-shaped eyes
        // drawn facing right (flipped for left): the eyes sit one pixel toward where he looks, the far eye against the edge
        static readonly string[] MaskArt =
        {
            "..OO.....OO..",
            ".OWWO...OWWO.",
            "OWWO.....OWWO",
            "OWO.......OWO",
            "OWO.OOOOO.OWO",
            "OWWOWWWWWOWWO",
            ".OWWWWWWWWWO.",
            ".OWWWOWWWOWO.",
            ".OWWOOOWOOOO.",
            ".OWWOOOWOOOO.",
            ".OWWWOWWWOWO.",
            "..OWWWWWWWO..",
            "...OOOOOOO...",
        };

        void UpdateKnight(CelesteFrame frame, Vector3 feet, int facing, float sx, float vs)
        {
            bool on = Masked(skin.Style) && root.activeSelf;
            if (Masked(skin.Style))
                for (int i = 0; i < hair.Length; i++) { hair[i].enabled = false; for (int k = 0; k < 4; k++) hairOutline[i, k].enabled = false; }
            knightMask.enabled = on && body.enabled && body.sprite != null; knightEyes.enabled = false;
            if (!knightMask.enabled) { needle.enabled = false; return; }
            BuildLookSprites();
            float px = vs / Visuals.PxPerUnit, step = Visuals.LayerDepthStep;
            var ls = new Vector3(facing * Math.Abs(sx) * vs, vs, 1f);
            // over her head, where her bangs are, a little forward and in front of the body
            // where her bangs are; in frames without hair (the bench) the top of her drawn sprite, a little down
            Vector3 head = frame.HasHair && !atBenchNow
                ? new Vector3(nodes[0].x + facing * 0.5f * px, nodes[0].y + 2f * px, feet.z - step * 0.5f)
                : new Vector3(feet.x + facing * 0.5f * px, feet.y + Visuals.KnightBenchHeadHeight * px, feet.z - step * 0.5f);
            // soul wisps rise off him while he has no dash (or is locked after a hit)
            if (skin.Style == "knight" && (ctrl.Dashes == 0 || ctrl.DashLocked) && ctrl.Active && (soulT -= Time.deltaTime) <= 0f)
            {
                soulT = 0.12f;
                Particles.DustTinted(feet + new Vector3(UnityEngine.Random.Range(-4f, 4f) * px, UnityEngine.Random.Range(2f, 12f) * px, 0f), -Mathf.PI / 2f, 1, new Color(1f, 1f, 1f, 0.75f), 1f);
            }
            // Hornet's needle on her back, point down behind her (not while she sits)
            needle.enabled = skin.Style == "hornet" && !atBenchNow && needleSprite != null;
            if (needle.enabled)
            {
                needle.sprite = needleSprite;
                needle.color = Color.white;
                needle.transform.position = new Vector3(feet.x - facing * 1f * px, feet.y + 9f * px, feet.z + step * 3f);
                needle.transform.localScale = new Vector3(-facing * Math.Abs(sx) * vs, vs, 1f);
            }
            knightMask.sprite = skin.Style == "hornet" ? hornetMaskSprite : maskSprite; knightMask.color = Color.white;
            knightMask.transform.position = skin.Style == "hornet" ? head + new Vector3(0f, -Visuals.HornetChinDrop * px, 0f) : head;   // Hornet's mask hangs from the chin
            knightMask.transform.localScale = ls;
            knightEyes.sprite = eyesSprite; knightEyes.color = hairColor; knightEyes.transform.position = head - new Vector3(0f, 0f, step * 0.1f); knightEyes.transform.localScale = ls;
        }

        // the topmost opaque pixel of the body's current frame, in world space (cached per sprite)
        readonly Dictionary<Sprite, Vector2> opaqueTop = new Dictionary<Sprite, Vector2>();
        Vector3 OpaqueTop(SpriteRenderer sr)
        {
            Sprite s = sr.sprite;
            Vector2 local;
            if (!opaqueTop.TryGetValue(s, out local))
            {
                local = new Vector2(0f, s.bounds.max.y);
                try
                {
                    Rect r = s.textureRect;
                    Color[] px = s.texture.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
                    int w = (int)r.width, hgt = (int)r.height;
                    for (int y = hgt - 1; y >= 0 && local.x == 0f; y--)
                    {
                        int minX = -1, maxX = -1;
                        for (int x = 0; x < w; x++) if (px[y * w + x].a > 0.5f) { if (minX < 0) minX = x; maxX = x; }
                        if (minX >= 0) { local = new Vector2(((minX + maxX + 1) / 2f - s.pivot.x) / s.pixelsPerUnit + 1e-4f, (y + 1 - s.pivot.y) / s.pixelsPerUnit); break; }
                    }
                }
                catch (Exception) { }   // an unreadable texture: the sprite's own top
                opaqueTop[s] = local;
            }
            return sr.transform.TransformPoint(local);
        }

        // ---- the Knight's Focus (bind): white soul motes drift in to him under a growing glow, then a flash ----
        readonly SpriteRenderer[] focusDots = new SpriteRenderer[14];
        SpriteRenderer focusGlow, focusBody;
        Material silhouette;
        Sprite glowSprite;
        float focusT, focusFlash;

        void UpdateFocus(Vector3 feet, float dt)
        {
            if (focusGlow == null)
            {
                focusGlow = MakeRenderer("FocusGlow", 0);
                focusBody = MakeRenderer("FocusWhite", 0);
                Shader sh = Shader.Find("GUI/Text Shader");   // draws the sprite's shape in one flat colour
                silhouette = sh != null ? new Material(sh) : material;
                focusBody.sharedMaterial = silhouette;
                for (int i = 0; i < focusDots.Length; i++) focusDots[i] = MakeRenderer("FocusSoul" + i, 0);
                var circle = new string[17];
                for (int y = 0; y < 17; y++)
                {
                    var row = new char[17];
                    for (int x = 0; x < 17; x++) { float dx = x - 8, dy = y - 8; row[x] = dx * dx + dy * dy <= 64f ? (dx * dx + dy * dy <= 30f ? 'C' : 'E') : '.'; }
                    circle[y] = new string(row);
                }
                glowSprite = PixelSprite(circle, ch => ch == 'C' ? new Color(1f, 1f, 1f, 1f) : ch == 'E' ? new Color(1f, 1f, 1f, 0.5f) : Color.clear, new Vector2(0.5f, 0.5f));
                if (tailSprite == null) { string[] dot = { ".HH.", "HHHH", "HHHH", ".HH." }; tailSprite = PixelSprite(dot, ch => ch == 'H' ? Color.white : Color.clear, new Vector2(0.5f, 0.5f)); }
            }
            bool focusing = binding && Masked(skin.Style) && root.activeSelf;
            float vs = Visuals.SpriteScale, px = vs / Visuals.PxPerUnit, step = Visuals.LayerDepthStep;
            Vector3 center = feet + new Vector3(0f, 8f * px, -step * 2f);
            if (focusing) focusT += dt;
            if (focusFlash > 0f) focusFlash = Mathf.Max(0f, focusFlash - dt / 0.35f);
            for (int i = 0; i < focusDots.Length; i++)
            {
                SpriteRenderer d = focusDots[i];
                d.enabled = focusing;
                if (!focusing) continue;
                float ph = Mathf.Repeat(focusT * 1.3f + i / (float)focusDots.Length, 1f);
                float ang = i * 2.39996f + focusT * 0.8f;
                float r = (1f - ph) * 26f * px;
                d.sprite = tailSprite;
                d.transform.position = center + new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);
                float s = vs * 0.5f * (0.6f + ph * 0.6f);
                d.transform.localScale = new Vector3(s, s, 1f);
                d.color = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Sin(ph * Mathf.PI) * 1.5f));
            }
            // he turns white as the soul fills him, and is fully white in the flash
            float white = focusing ? Mathf.Clamp01(focusT / 0.8f) * 0.75f : focusFlash;
            focusBody.enabled = white > 0.01f && body.enabled && body.sprite != null;
            if (focusBody.enabled)
            {
                focusBody.sprite = body.sprite;
                focusBody.transform.position = body.transform.position - new Vector3(0f, 0f, step * 0.3f);
                focusBody.transform.localScale = body.transform.localScale;
                focusBody.color = new Color(1f, 1f, 1f, white);
            }
            focusGlow.enabled = focusing || focusFlash > 0f;
            if (!focusGlow.enabled) return;
            focusGlow.sprite = glowSprite;
            focusGlow.transform.position = center + new Vector3(0f, 0f, step);
            float grow = focusing ? Mathf.Clamp01(focusT / 1f) : 1f + (1f - focusFlash) * 1.2f;
            float gs = vs * (0.6f + grow * 0.9f) * (1f + 0.05f * Mathf.Sin(focusT * 12f));
            focusGlow.transform.localScale = new Vector3(gs, gs, 1f);
            focusGlow.color = new Color(1f, 1f, 1f, focusing ? 0.3f + 0.25f * grow : 0.85f * focusFlash);
        }

        void BuildLookSprites()
        {
            if (earSprite == null)
            {
                earSprite = PixelSprite(EarArt, ch => ch == 'O' ? Color.black : ch == 'H' ? Color.white : Color.clear, new Vector2(0.5f, 0f));
                earInnerSprite = PixelSprite(EarArt, ch => ch == 'P' ? new Color(1f, 0.62f, 0.72f) : Color.clear, new Vector2(0.5f, 0f));
                string[] dot = { ".HH.", "HHHH", "HHHH", ".HH." };
                tailSprite = PixelSprite(dot, ch => ch == 'H' ? Color.white : Color.clear, new Vector2(0.5f, 0.5f));
                string[] ring = { ".OOOO.", "OOOOOO", "OOOOOO", "OOOOOO", "OOOOOO", ".OOOO." };
                tailOutlineSprite = PixelSprite(ring, ch => ch == 'O' ? Color.black : Color.clear, new Vector2(0.5f, 0.5f));
                EarSprite = earSprite; EarInnerSprite = earInnerSprite; TailSprite = tailSprite; TailOutlineSprite = tailOutlineSprite;
            }
            if (maskSprite == null)
            {
                maskSprite = PixelSprite(MaskArt, ch => ch == 'O' || ch == 'E' ? Color.black : ch == 'W' ? new Color(0.97f, 0.97f, 0.97f) : Color.clear, new Vector2(0.5f, 0.5f));   // the Knight's black eyes
                MaskSprite = maskSprite;
                var navy = new Color(0.04f, 0.15f, 0.25f);
                hornetMaskSprite = PixelSprite(HornetMaskArt, ch => ch == 'O' || ch == 'E' ? navy : ch == 'W' ? new Color(1f, 0.94f, 0.86f) : Color.clear, new Vector2(0.67f, 0f));   // pivot: under the chin
                needleSprite = PixelSprite(NeedleArt, ch => ch == 'O' ? navy : ch == 'N' ? new Color(0.36f, 0.31f, 0.45f) : Color.clear, new Vector2(0.5f, 0.5f));
                HornetMaskSprite = hornetMaskSprite;
                eyesSprite = PixelSprite(MaskArt, ch => ch == 'E' ? Color.white : Color.clear, new Vector2(0.5f, 0.5f));
            }
        }

        void HideCat()
        {
            knightMask.enabled = knightEyes.enabled = false; if (needle != null) needle.enabled = false;
            catEars.enabled = catEarsInner.enabled = false;
            for (int i = 0; i < catTail.Length; i++) catTail[i].enabled = catTailOutline[i].enabled = false;
        }

        void UpdateCat(CelesteFrame frame, Vector3 feet, int facing, float sx, float vs)
        {
            // the Pink Cat always; Madeline-shaped skins when assist.cat_ears is On
            if (!AssistMode.IsOpen) earsSetting = AssistMode.CatEars;   // applied once the settings close
            bool on = (skin.Style == "cat" || (earsSetting && skin.Style == "celeste" && skin.CelesteSprite == "player")) && frame.HasHair && root.activeSelf;
            catEars.enabled = catEarsInner.enabled = on;
            for (int i = 0; i < catTail.Length; i++) catTail[i].enabled = catTailOutline[i].enabled = on;
            if (!on) return;
            BuildLookSprites();
            float px = vs / Visuals.PxPerUnit, step = Visuals.LayerDepthStep;
            var ls = new Vector3(facing * Math.Abs(sx) * vs, vs, 1f);
            // ears sit on top of the bangs, a little toward the back of her head; behind the hair so the bangs cover their base
            Vector3 head = new Vector3(nodes[0].x - facing * 0.5f * px, nodes[0].y + 2f * px, feet.z + step * 2.5f);
            catEars.sprite = earSprite; catEars.color = hairColor; catEars.transform.position = head; catEars.transform.localScale = ls;
            catEarsInner.sprite = earInnerSprite; catEarsInner.color = Color.white; catEarsInner.transform.position = head - new Vector3(0f, 0f, step * 0.1f); catEarsInner.transform.localScale = ls;
            // the tail: from her hip, out behind her, curling up at the tip and swishing
            float tm = Time.time;
            Vector3 hip = feet + new Vector3(-facing * 3f * px, 3.5f * px, step * 3.5f);
            for (int i = 0; i < catTail.Length; i++)
            {
                float f = i / (float)(catTail.Length - 1);
                float sway = Mathf.Sin(tm * 3f - i * 0.7f) * f * 2.2f;
                Vector3 pos = hip + new Vector3(-facing * (i * 1.3f + sway * 0.3f) * px, (i * i * 0.22f + sway * 0.7f) * px, 0f);
                float sc = vs * 0.6f * (1f - f * 0.3f);
                catTail[i].sprite = tailSprite; catTail[i].color = hairColor; catTail[i].transform.position = pos; catTail[i].transform.localScale = new Vector3(sc, sc, 1f);
                catTailOutline[i].sprite = tailOutlineSprite; catTailOutline[i].color = Color.white; catTailOutline[i].transform.position = pos + new Vector3(0f, 0f, step * 0.5f); catTailOutline[i].transform.localScale = new Vector3(sc, sc, 1f);
            }
        }

        SpriteRenderer MakeRenderer(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = material;
            sr.sortingOrder = order;
            return sr;
        }

        public string CurrentAnimId { get { return anim != null ? anim.Id + "#" + frameIndex : "none"; } }

        /// <summary>Out of gameplay (the main menu): nothing of her stays on screen.</summary>
        public void Hide()
        {
            if (root.activeSelf) root.SetActive(false);
            foreach (var t in trails) if (t.Key != null) t.Key.enabled = false;
        }

        public void Destroy()
        {
            ctrl.Dashed -= SpawnTrail;
            ctrl.DashBegan -= OnDashBegan;
            ctrl.DashLaunched -= OnDashLaunched;
            ctrl.GlideStarted -= OnGlideStarted;
            BossCard.GoldenLost -= OnGoldenLost;
            UnityEngine.Object.Destroy(root);
        }

        void Play(string id, bool restart = false)
        {
            CelesteAnim next;
            if (!assets.Anims.TryGetValue(id, out next)) return;
            if (next == anim && !restart) return;
            // Celeste's Sprite.OnChange: leaving idleB / idleC for a non-idle animation cuts its sound off.
            if (anim != null && (anim.Id == Animations.IdleB.CelesteAnim || anim.Id == Animations.IdleC.CelesteAnim) && !id.StartsWith("idle", StringComparison.Ordinal)) Sfx.StopIdle();
            anim = next; frameIndex = 0; frameTimer = 0f;
            OnFrame();
        }

        // ---- idle moves (Celeste Player sprite OnLastFrame; visuals.idle_*, skins.idle_moves / idle_sounds) ----

        float idleTimer;
        static readonly System.Random idleRng = new System.Random();

        // When the idle loop ends after idle_delay standing still: a 20% chance of a weighted idle move.
        bool TryIdleMove()
        {
            if (!ctrl.Active || dying || waking || idleTimer <= Visuals.IdleDelay || idleRng.NextDouble() >= Visuals.IdleChance) return false;
            var moves = new List<KeyValuePair<string, int>>();
            int total = 0;
            foreach (string part in skin.IdleMoves.Split(','))
            {
                string[] kv = part.Split(':');
                int w = int.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
                moves.Add(new KeyValuePair<string, int>(kv[0], w));
                total += w;
            }
            int pick = idleRng.Next(total);
            string move = moves[moves.Count - 1].Key;
            foreach (var m in moves) { if (pick < m.Value) { move = m.Key; break; } pick -= m.Value; }
            if (!assets.Anims.ContainsKey(move)) return false;
            Play(move, true);
            foreach (string part in skin.IdleSounds.Split(';'))
            {
                string[] kv = part.Split(':');
                if (kv[0] == move) { Sfx.PlayIdle(kv[1]); if (kv[1] == "idle_sneeze") Achievements.Sneezed(); }
            }
            return true;
        }

        // ---- healing (hooks.bind_heal: Celeste's feather star; visuals.star_fly_color / feather_*) -----------

        static readonly Color StarFly = Hex(Visuals.StarFlyColor);
        SpriteRenderer starWhite;
        bool binding;
        tk2dSpriteAnimator hornetAnimator;
        float wallOffset;   // Celeste px, eased toward visuals.wall_offset_px while she is on a wall

        // Hornet's BindCharge clips: Madeline curls into Celeste's glowing star. BindBurst: the heal lands and she
        // bursts out in gold feathers. Any other clip without a burst means the bind was interrupted.
        void UpdateBind(HeroController h, Vector3 feet)
        {
            if (hornetAnimator == null || hornetAnimator.gameObject != h.gameObject) hornetAnimator = h.GetComponent<tk2dSpriteAnimator>();
            string clip = hornetAnimator != null && hornetAnimator.CurrentClip != null ? hornetAnimator.CurrentClip.name : "";
            bool charge = clip.StartsWith("BindCharge", StringComparison.Ordinal), burst = clip.StartsWith("BindBurst", StringComparison.Ordinal);
            if (charge && !binding)
            {
                binding = true;
                waking = false;
                if (Masked(skin.Style)) { Play(Animations.Idle.CelesteAnim, true); focusT = 0f; }   // Hollow Knight's Focus: he stands still, soul gathers in and he glows white
                else { Play(Animations.BindStart.CelesteAnim, true); Sfx.Play(Sounds.FeatherGet.Id); }
            }
            else if (burst && binding)
            {
                binding = false;
                float u = Visuals.SpriteScale / Visuals.PxPerUnit;
                if (Masked(skin.Style)) focusFlash = 1f;   // the heal lands in a white flash
                else
                {
                    Particles.Feathers(feet + new Vector3(0f, Visuals.DashFxCenterPx * u, 0f), (int)Visuals.FeatherCount, Visuals.FeatherRange);
                    Sfx.Play(Sounds.FeatherStateEnd.Id);
                }
                ctrl.HairFlashTimer = Visuals.HairFlashTime;
                anim = null;   // back to her normal animation at once
            }
            else if (!charge && !burst && binding) { binding = false; anim = null; }   // interrupted
        }

        // ---- glide (unlocks.glide: Celeste's Glider held over her head; visuals.glider_*) --------------------

        SpriteRenderer glider;
        float gliderT, gliderRot;
        Vector2 gliderScale = Vector2.one;

        // Glider.PlayOpen: the "fall" animation from the start, squashed wide, with glider_engage.
        void OnGlideStarted()
        {
            gliderT = 0f;
            gliderScale = new Vector2(Visuals.GliderOpenSquashX, Visuals.GliderOpenSquashY);
            Sfx.Play(Sounds.GliderEngage.Id);
        }

        void UpdateGlider(CelesteFrame frame, Vector3 feet, float dt)
        {
            bool show = ctrl.Active && ctrl.Gliding && assets.GliderOpen.Count + assets.GliderLoop.Count > 0;
            glider.enabled = show;
            if (!show) return;
            gliderT += dt;
            int i = (int)(gliderT / Visuals.GliderFrameTime);
            glider.sprite = i < assets.GliderOpen.Count || assets.GliderLoop.Count == 0
                ? assets.GliderOpen[Math.Min(i, assets.GliderOpen.Count - 1)]
                : assets.GliderLoop[(i - assets.GliderOpen.Count) % assets.GliderLoop.Count];
            // Glider.Update: leans with her speed in the air; the open pop settles back to normal size.
            float lean = Mathf.Clamp(ctrl.Speed.x / Visuals.GliderTiltSpeed, -1f, 1f) * -Visuals.GliderTiltAir;
            gliderRot = Mathf.MoveTowards(gliderRot, lean, Visuals.GliderTiltRate * dt);
            gliderScale = Vector2.MoveTowards(gliderScale, Vector2.one, Visuals.GliderScaleReturn * dt);
            // Player.Update: Holding.Carry(Position + CarryOffsetTarget + (0, CarryYOffset)), Celeste Y down.
            float u = Visuals.SpriteScale / Visuals.PxPerUnit;
            glider.transform.position = feet + new Vector3(0f, -(Visuals.GliderCarryY + frame.CarryY) * u, -Visuals.LayerDepthStep * 1.5f);
            glider.transform.rotation = Quaternion.Euler(0f, 0f, -gliderRot * Mathf.Rad2Deg);
            glider.transform.localScale = new Vector3(gliderScale.x * Visuals.SpriteScale, gliderScale.y * Visuals.SpriteScale, 1f);
        }

        // ---- bench wake-up (animations.bench_wake) -----------------------------------------------------------

        bool wasAtBench, waking;

        // Celeste's Player sprite OnFrameChange: footsteps, handholds and push dust on set frames
        // (visuals.footstep_frames / handhold_frames), with the area's surface (sheets/surfaces.json).
        static readonly Dictionary<string, HashSet<int>> footstepFrames = ParseFrames(Visuals.FootstepFrames), handholdFrames = ParseFrames(Visuals.HandholdFrames);

        static Dictionary<string, HashSet<int>> ParseFrames(string spec)
        {
            var d = new Dictionary<string, HashSet<int>>();
            foreach (string part in spec.Split(';'))
            {
                string[] kv = part.Split(':');
                var set = new HashSet<int>();
                foreach (string f in kv[1].Split(',')) set.Add(int.Parse(f, System.Globalization.CultureInfo.InvariantCulture));
                d[kv[0]] = set;
            }
            return d;
        }

        void OnFrame()
        {
            if (dying || anim == null) return;
            if (anim.Id == Animations.BenchWake.CelesteAnim && frameIndex == (int)Visuals.WakeSoundFrame) Sfx.Play(Sounds.CampfireStand.Id);
            if (!ctrl.Active) return;
            HashSet<int> frames;
            if (ctrl.OnGround && footstepFrames.TryGetValue(anim.Id, out frames) && frames.Contains(frameIndex))
            {
                Sfx.PlaySurface("footstep");
                if (anim.Id == Animations.Push.CelesteAnim)   // Dust.BurstFG behind her heel, no spread
                {
                    float u = Visuals.SpriteScale / Visuals.PxPerUnit;
                    Particles.Dust(body.transform.position + new Vector3(-ctrl.Facing * 5f * u, 1f * u, 0f), Mathf.Atan2(-0.5f, -ctrl.Facing), 1, 0f);
                }
            }
            else if (ctrl.State == MadelineController.StClimb && handholdFrames.TryGetValue(anim.Id, out frames) && frames.Contains(frameIndex))
                Sfx.PlaySurface("handhold");
        }

        // ---- death (Celeste.PlayerDeadBody + DeathEffect; sheets/visuals.json death_*) -----------------------

        bool dying, deathSounded;
        float deathT;
        Vector3 deathStart;
        int deathDir, deathFacing;
        Color deathColor;
        SpriteRenderer[] burst, burstOutline;

        bool UpdateDeath(HeroController h, float dt, Vector3 feet, int facing)
        {
            bool dead = h.cState.dead || h.cState.hazardDeath;
            if (!dead) { if (dying) EndDeath(); return false; }
            if (!dying)
            {
                dying = true; deathSounded = false; deathT = 0f;
                deathStart = feet; deathFacing = facing; deathDir = -facing; deathColor = hairColor;
                Play(Animations.DeadSide.CelesteAnim, true);
                Sfx.Play(Sounds.Predeath.Id);
                EnsureBurst();
            }
            root.SetActive(true);
            deathT += dt;
            float u = Visuals.SpriteScale / Visuals.PxPerUnit;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(deathT / Visuals.DeathBounceTime), 3f);   // Ease.CubeOut
            Vector3 pos = deathStart + new Vector3(deathDir * Visuals.DeathBounce * e * u, 0f, 0f);
            float scale = 1.5f - e * 0.5f;
            Advance(dt);
            CelesteFrame frame = anim.Frames[Mathf.Clamp(frameIndex, 0, anim.Frames.Count - 1)];
            body.sprite = frame.Sprite;
            body.transform.position = pos;
            body.transform.localScale = new Vector3(deathFacing * scale * Visuals.SpriteScale, scale * Visuals.SpriteScale, 1f);
            for (int i = 0; i < hair.Length; i++) { hair[i].enabled = false; for (int k = 0; k < 4; k++) hairOutline[i, k].enabled = false; }
            HideCat();

            float p = (deathT - Visuals.DeathEffectDelay) / Visuals.DeathEffectDuration;
            if (p >= 0f && !deathSounded) { deathSounded = true; Sfx.Play(Sounds.Death.Id); }
            body.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(p));
            DrawBurst(pos + new Vector3(0f, 6f * u, -Visuals.LayerDepthStep), deathColor, p);
            foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true)) sr.sortingOrder = 0;
            return true;
        }

        void EnsureBurst()
        {
            if (burst != null) return;
            burst = new SpriteRenderer[8]; burstOutline = new SpriteRenderer[32];
            for (int i = 0; i < 8; i++) { for (int k = 0; k < 4; k++) burstOutline[i * 4 + k] = MakeRenderer("DeathOutline" + i + "_" + k, 0); burst[i] = MakeRenderer("Death" + i, 0); }
        }

        // Celeste.DeathEffect.Draw(center, color, percent): eight hair-node circles flying out (0 -> 1) or,
        // for a respawn, back in (1 -> 0), blinking white every tenth.
        void DrawBurst(Vector3 center, Color baseColor, float p)
        {
            float u = Visuals.SpriteScale / Visuals.PxPerUnit;
            bool show = p >= 0f && p <= 1f;
            Color c = Math.Floor(p * 10f) % 2.0 == 0.0 ? baseColor : Color.white;
            float s = p < 0.5f ? 0.5f + p : CubeOut(1f - (p - 0.5f) * 2f);   // DeathEffect.Draw circle size
            for (int i = 0; i < 8; i++)
            {
                float ang = ((float)i / 8f + p * 0.25f) * Mathf.PI * 2f;
                Vector3 at = center + new Vector3(Mathf.Cos(ang), -Mathf.Sin(ang), 0f) * CubeOut(Mathf.Clamp01(p)) * Visuals.DeathEffectRadius * u;
                SetBurst(burst[i], at, c, s, show);
                for (int k = 0; k < 4; k++) SetBurst(burstOutline[i * 4 + k], at + new Vector3(OutlineOffsets[k].x * u, OutlineOffsets[k].y * u, Visuals.LayerDepthStep), Color.black, s, show);
            }
        }

        void HideBurst()
        {
            if (burst != null) { foreach (var b in burst) b.enabled = false; foreach (var b in burstOutline) b.enabled = false; }
        }

        // ---- respawn (Celeste.Player.IntroRespawnBegin; visuals.respawn_*) ----------------------------------

        bool respawning;
        float respawnT;

        // As the wipe opens: the revive sound, the death burst played backwards onto her, then she pops in
        // squashed. Returns true while she is still hidden.
        bool UpdateRespawn(Vector3 feet, float dt, int layer, int order)
        {
            if (!respawning) return false;
            body.enabled = false;
            for (int i = 0; i < hair.Length; i++) { hair[i].enabled = false; for (int k = 0; k < 4; k++) hairOutline[i, k].enabled = false; }
            HideCat();
            if (CelesteWipe.ScreenCovered) { HideBurst(); return true; }
            if (respawnT == 0f) Sfx.Play(Sounds.Revive.Id);
            respawnT += dt;
            float t = Mathf.Clamp01(respawnT / Visuals.RespawnTime);
            float u = Visuals.SpriteScale / Visuals.PxPerUnit;
            EnsureBurst();
            DrawBurst(feet + new Vector3(0f, Visuals.DashFxCenterPx * u, -Visuals.LayerDepthStep), Normal, 1f - t);   // introEase
            foreach (SpriteRenderer sr in burst) { sr.sortingLayerID = layer; sr.sortingOrder = order; }
            foreach (SpriteRenderer sr in burstOutline) { sr.sortingLayerID = layer; sr.sortingOrder = order; }
            if (t < 1f) return true;
            respawning = false;
            HideBurst();
            body.enabled = true;
            if (ctrl.Active) { ctrl.ScaleX = Visuals.RespawnSquashX; ctrl.ScaleY = Visuals.RespawnSquashY; }
            return false;
        }

        static float CubeOut(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 3f); }

        void SetBurst(SpriteRenderer sr, Vector3 at, Color c, float scale, bool show)
        {
            sr.enabled = show;
            sr.sprite = assets.HairNode;
            sr.color = c;
            sr.transform.position = at;
            sr.transform.localScale = new Vector3(scale * Visuals.SpriteScale, scale * Visuals.SpriteScale, 1f);
        }

        void EndDeath()
        {
            dying = false;
            HideBurst();
            body.color = Color.white;
            anim = null;
            respawning = true; respawnT = 0f;
        }

        bool wallSliding;
#if TESTHARNESS
        string lastBenchInfo;
#endif

        public void LateUpdate(HeroController h, float dt)
        {
            wallSliding = false;
            Draw(h, dt);
            // assist.invisible_motion: Celeste's variant, she can't be seen while she moves
            Vector3 p = h.transform.position;
            bool moving = dt > 0f && (p - lastHeroPos).sqrMagnitude > 0.0004f;
            lastHeroPos = p;
            Vector3 want = AssistMode.InvisibleMotion && moving ? Vector3.zero : Vector3.one;
            if (root != null && root.transform.localScale != want) root.transform.localScale = want;
            Sfx.Loop(wallSliding ? Sfx.SurfaceId("wallslide", Sfx.Surface) : glider.enabled ? Sounds.GliderMovement.Id : null);
        }

        // Hornet's collider as it looks this frame. Silksong interpolates her Rigidbody2D (HeroController sets
        // RigidbodyInterpolation2D.Interpolate), so her transform moves smoothly every frame while the collider
        // only moves on physics steps; drawing from the raw collider made Madeline stair-step at fast-fall speed.
        // The smoothing offset is never more than one physics step of her speed. A bigger gap means Silksong moved
        // her without physics (the bench spawn teleports her in and lifts her onto the seat), and adding it made her
        // float above the bench, so it is left out then.
        static Bounds DrawBounds(HeroController h)
        {
            Bounds b = Col(h).bounds;
            Rigidbody2D rb = Rb(h);
            if (rb == null || !rb.simulated || rb.interpolation == RigidbodyInterpolation2D.None) return b;
            Vector2 off = (Vector2)h.transform.position - rb.position;
            float step = rb.linearVelocity.magnitude * Time.fixedDeltaTime;
            if (off.magnitude <= step + 0.001f) b.center += (Vector3)off;
            return b;
        }

        void Draw(HeroController h, float dt)
        {
            glider.enabled = false;   // UpdateGlider shows it again while she glides; deaths and respawns return early
            MeshRenderer mr = HeroRenderer(h);
            if (mr != null) mr.forceRenderingOff = true;
            {
                Bounds db = DrawBounds(h);
                if (UpdateDeath(h, dt, new Vector3(db.center.x, db.min.y, h.transform.position.z), ctrl.Active ? ctrl.Facing : (h.cState.facingRight ? 1 : -1))) { ShowPico(false); return; }
            }
            bool visible = mr != null && mr.enabled && h.gameObject.activeInHierarchy;
            if (mr != null) mr.forceRenderingOff = true;   // hooks.hide_hornet
            root.SetActive(visible);
            if (!visible) return;

            int sortingLayer = mr.sortingLayerID, sortingBase = mr.sortingOrder;
            Bounds b = DrawBounds(h);
            Vector3 feet = new Vector3(b.center.x, b.min.y, h.transform.position.z);
            // visuals.wall_offset_px: on a wall her climb pose reaches further in than Hornet's cling, so the wall's
            // close-up decoration (Greymoor's log bundles) hid half of her; she is drawn this far out from the wall.
            // It eases in and out (visuals.wall_offset_speed), so a state that flickers at the top of a climb can't
            // make her jitter.
            float wallTarget = 0f;
            if (ctrl.Active && (ctrl.State == MadelineController.StClimb || ctrl.WallSlideDir != 0))
                wallTarget = -(ctrl.State == MadelineController.StClimb ? ctrl.Facing : ctrl.WallSlideDir) * Visuals.WallOffsetPx;
            wallOffset = Mathf.MoveTowards(wallOffset, wallTarget, Visuals.WallOffsetSpeed * dt);
            feet.x += wallOffset * Visuals.SpriteScale / Visuals.PxPerUnit;
            if (h.playerData.atBench)
            {
                // On the seat: exactly bench_seat_height above the floor below her, whether she sat down (hero on
                // the floor) or respawned (hero placed above the seat).
                // Solid floor only: on a spawn or respawn Hornet is lifted onto the seat, and a ray that also hits
                // triggers stops on the bench's own RestBench box instead of the floor.
                var floorFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = 33562880 };
                RaycastHit2D[] floorHits = new RaycastHit2D[1];
                RaycastHit2D floor = Physics2D.Raycast(new Vector2(b.center.x, b.min.y + 0.02f), Vector2.down, floorFilter, floorHits, 3f) > 0 ? floorHits[0] : default(RaycastHit2D);
                if (floor.collider != null) feet.y = floor.point.y + Visuals.BenchSeatHeight;
                else feet.y += Visuals.BenchSeatHeight;
#if TESTHARNESS
                string benchInfo = string.Format("[TEST] bench: hero bottom {0:F2}, floor {1} at {2:F2}, Madeline feet drawn at {3:F2}, transform y {4:F2}, rb y {5:F2}, anim {6}",
                    b.min.y, floor.collider != null ? floor.collider.name : "none", floor.collider != null ? floor.point.y : 0f, feet.y, h.transform.position.y, Rb(h).position.y, anim != null ? anim.Id : "none");
                if (benchInfo != lastBenchInfo) { Plugin.Log.LogInfo(benchInfo); lastBenchInfo = benchInfo; }
#endif
            }
            // Getting up from a bench: Celeste's campfire wakeUp, easing down from the seat over its stand-up frames.
            bool atBench = h.playerData.atBench;
            if (wasAtBench && !atBench && !respawning) { waking = true; Play(Animations.BenchWake.CelesteAnim, true); }
            wasAtBench = atBench;
            if (waking && anim != null && anim.Id == Animations.BenchWake.CelesteAnim)
            {
                float k = Mathf.Clamp01((frameIndex - Visuals.WakeStandFromFrame) / Math.Max(1f, anim.Frames.Count - 1 - Visuals.WakeStandFromFrame));
                feet.y += Visuals.BenchSeatHeight * (1f - k);
            }
            if (ctrl.Active && ctrl.Speed != Vector2.zero) idleTimer = 0f; else idleTimer += dt;   // Celeste resets it on any speed
            int facing = ctrl.Active ? ctrl.Facing : (h.cState.facingRight ? 1 : -1);
            if (UpdateRespawn(feet, dt, sortingLayer, sortingBase)) { ShowPico(false); return; }
            UpdateBind(h, feet);

            ChooseAnimation(h);
            Advance(dt);
            CelesteFrame frame = anim.Frames[Mathf.Clamp(frameIndex, 0, anim.Frames.Count - 1)];

            float vs = Visuals.SpriteScale;
            float sx = ctrl.Active ? ctrl.ScaleX : 1f, sy = ctrl.Active ? ctrl.ScaleY : 1f;
            body.sprite = frame.Sprite;
            body.transform.position = feet;
            body.transform.localScale = new Vector3(facing * sx * vs, sy * vs, 1f);
            bool flash = Mathf.Repeat(Time.time, Visuals.TiredFlashInterval * 2f) < Visuals.TiredFlashInterval;
            if (binding && anim.Id == Animations.BindHold.CelesteAnim) body.color = StarFly;   // Celeste: Sprite.Color = starFlyColor
            else if (ctrl.Active && ctrl.IsTired && flash) body.color = Tired;   // Celeste's red flash when out of stamina
            else if (boostT > 0f && ctrl.SkillDashing) { boostT -= dt; body.color = Color.Lerp(Hex(skin.BodyColor), new Color(1f, 0.35f, 0.35f, 1f), 0.6f); }   // Rune Rage: Celeste's red booster
            else { boostT = 0f; Color bc = Hex(skin.BodyColor); if (h.cState.invulnerable && flash) bc.a *= 0.5f; body.color = bc; }   // skins.body_color (the Ghost is see-through blue)
            // Player.Render: during startStarFly the white frame on top, tinted starFlyColor * (frame / frames).
            bool white = binding && anim.Id == Animations.BindStart.CelesteAnim && frameIndex < assets.StarFlyWhite.Count;
            starWhite.enabled = white;
            if (white)
            {
                starWhite.sprite = assets.StarFlyWhite[frameIndex];
                starWhite.transform.position = feet + new Vector3(0f, 0f, -Visuals.LayerDepthStep / 2f);
                starWhite.transform.localScale = body.transform.localScale;
                Color c = StarFly * ((float)frameIndex / anim.Frames.Count);
                starWhite.color = c;
            }

            UpdateGlider(frame, feet, dt);
            UpdateHairColor(dt);
            UpdateHair(frame, feet, facing, sy, vs, dt);
            float unit = vs / Visuals.PxPerUnit;
            for (int i = 0; i < hair.Length; i++)
            {
                Sprite s = i == 0 ? assets.Bangs[Mathf.Clamp(frame.HairFrame, 0, assets.Bangs.Count - 1)] : assets.HairNode;
                bool on = frame.HasHair && i < hairN;
                float scale = i == 0 ? 1f : (0.25f + (1f - (float)i / hairN) * 0.75f) * AssistMode.HairSize;
                var ls = new Vector3((i == 0 ? facing : scale) * Math.Abs(sx) * vs, scale * vs, 1f);
                hair[i].enabled = on;
                float step = Visuals.LayerDepthStep;
                Color nc = AssistMode.HairNodeColor(hairColor, i, hairN, ctrl.Dashes > 0 && !ctrl.DashLocked && ctrl.HairFlashTimer <= 0f && !binding);
                hair[i].sprite = s; hair[i].color = nc; hair[i].transform.position = new Vector3(nodes[i].x, nodes[i].y, feet.z + step); hair[i].transform.localScale = ls;
                for (int k = 0; k < 4; k++)
                {
                    SpriteRenderer o = hairOutline[i, k];
                    o.enabled = on; o.sprite = s; o.color = Color.black; o.transform.localScale = ls;
                    o.transform.position = new Vector3(nodes[i].x + OutlineOffsets[k].x * unit, nodes[i].y + OutlineOffsets[k].y * unit, feet.z + step * 2f);
                }
            }
            UpdateCat(frame, feet, facing, sx, vs);
            atBenchNow = h.playerData.atBench;
            UpdateFocus(feet, dt);
            UpdateKnight(frame, feet, facing, sx, vs);
            UpdateGolden(feet, facing, dt);
            bool pico = IsPico;
            ShowPico(pico);
            if (pico)
            {
                for (int i = 0; i < hair.Length; i++) { hair[i].enabled = false; for (int k = 0; k < 4; k++) hairOutline[i, k].enabled = false; }
                starWhite.enabled = false;
                DrawPico(h, feet, facing, sx, sy, vs, dt);
            }
            // Same sorting layer and order as Hornet: Silksong's depth (z) sorting decides what covers her, so
            // foreground scenery hides her and background stays behind. Within her: body, hair, outline, trail by depth.
            foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingLayerID = sortingLayer;
                sr.sortingOrder = sortingBase;
            }
            lastSortingLayer = sortingLayer; lastSortingOrder = sortingBase;
            dashFx.Update(dt);
            Particles.Update(sortingLayer, sortingBase, feet.z - Visuals.LayerDepthStep * 3f);
            // Celeste Player.Update: the wall-slide loop plays while the wallslide animation shows and she moves down.
            UpdateCrystals(h, dt);
            wallSliding = ctrl.Active && anim != null && anim.Id == Animations.WallSlide.CelesteAnim && ctrl.Speed.y > 0f;
            for (int i = trails.Count - 1; i >= 0; i--)
            {
                SpriteRenderer t = trails[i].Key;
                float left = trails[i].Value - dt;
                if (left <= 0f) { UnityEngine.Object.Destroy(t.gameObject); trails.RemoveAt(i); continue; }
                Color c = t.color; c.a = Visuals.DashTrailAlpha * left / 0.3f; t.color = c;
                trails[i] = new KeyValuePair<SpriteRenderer, float>(t, left);
            }
        }

        // Celeste.Player.UpdateSprite, limited to the states this port has (sheets/animations.json).
        void ChooseAnimation(HeroController h)
        {
            if (h.playerData.atBench) { Play(Animations.BenchSit.CelesteAnim); return; }   // Celeste's campfire sleep
            if (binding) return;   // startStarFly runs into starFly by itself (its goto)
            if (waking)
            {
                InputMove(out int wx, out int wy);
                bool moved = wx != 0 || (ctrl.Active && (ctrl.Speed != Vector2.zero || !ctrl.OnGround || ctrl.State != MadelineController.StNormal || ctrl.Ducking));
                if (!moved && anim != null && anim.Id == Animations.BenchWake.CelesteAnim) return;
                waking = false;
            }
            if (!ctrl.Active)
            {
                // hooks.cutscene_anims: Silksong is animating Hornet (sheets/cutscenes.json), so show the same moment.
                string clip = hornetAnimator != null && hornetAnimator.CurrentClip != null ? hornetAnimator.CurrentClip.name : "";
                foreach (CutsceneRow row in Cutscenes.All)
                    if (clip.StartsWith(row.HornetClip, StringComparison.Ordinal)) { Play(row.CelesteAnim); return; }
                // Held in the air with gravity off (silk hearts and the like): Celeste's float spin.
                if (!h.IsGravityApplied && !h.cState.onGround && h.controlReqlinquished) { Play(Animations.CutFloat.CelesteAnim); return; }
                Vector2 v = Rb(h).linearVelocity * Visuals.PxPerUnit;
                if (h.cState.onGround) Play(Math.Abs(v.x) > 25f ? Animations.RunFast.CelesteAnim : Animations.Idle.CelesteAnim);
                else Play(v.y > 0 ? Animations.JumpSlow.CelesteAnim : Animations.FallSlow.CelesteAnim);
                return;
            }
            Vector2 s = ctrl.Speed;
            if (ctrl.State == MadelineController.StDash)
            {
                // Celeste keeps Ducking through a duck jump; in the air she still shows the dash (user 2026-10-11)
                Play(ctrl.Ducking && ctrl.OnGround ? Animations.Duck.CelesteAnim : Animations.Dash.CelesteAnim);
                return;
            }
            if (ctrl.State == MadelineController.StClimb)
            {
                if (ctrl.LastClimbMove < 0) Play(Animations.ClimbUp.CelesteAnim);
                else Play(Animations.WallSlide.CelesteAnim);
                return;
            }
            if (ctrl.Ducking && ctrl.OnGround) { Play(Animations.Duck.CelesteAnim); return; }
            if (ctrl.OnGround)
            {
                fastJump = false;
                InputMove(out int mx, out int my);
                if (mx != 0 && PushingWall(h, mx)) Play(Animations.Push.CelesteAnim);
                else if (Math.Abs(s.x) <= 25f && mx == 0)
                {
                    if (my == -1) Play(Animations.LookUp.CelesteAnim);
                    else if (anim == null || !anim.Id.StartsWith("idle", StringComparison.Ordinal)) Play(Animations.Idle.CelesteAnim);   // an idle move plays out
                }
                else if (Math.Sign(s.x) == -mx && mx != 0) Play(Math.Abs(s.x) > Movement.RunMax ? Animations.Skid.CelesteAnim : Animations.Flip.CelesteAnim);
                else if (anim == null || !(anim.Id.StartsWith("run", StringComparison.Ordinal) || anim.Id == "flip"))
                    Play(Math.Abs(s.x) < 45f ? Animations.RunSlow.CelesteAnim : Animations.RunFast.CelesteAnim);
                return;
            }
            if (ctrl.Gliding) { Play(s.y < 0f ? Animations.GlideRise.CelesteAnim : Animations.GlideFall.CelesteAnim); return; }   // Celeste's carry poses
            if (ctrl.WallSlideDir != 0) { Play(Animations.WallSlide.CelesteAnim); return; }
            if (s.y < 0f)
            {
                if (fastJump || Math.Abs(s.x) > Movement.RunMax) { fastJump = true; Play(Animations.JumpFast.CelesteAnim); }
                else Play(Animations.JumpSlow.CelesteAnim);
            }
            else if (fastJump || s.y >= Movement.MaxFall) { fastJump = true; Play(Animations.FallFast.CelesteAnim); }
            else Play(Animations.FallSlow.CelesteAnim);
        }

        static void InputMove(out int mx, out int my)
        {
            // menus own the arrows/stick: navigating Settings or Records doesn't make her look up or turn (user 2026-10-13)
            Vector2 v = InputHandler.Instance != null && !AssistMode.IsOpen && !BossCard.RecordsOpen ? InputHandler.Instance.inputActions.MoveVector.Vector : Vector2.zero;
            mx = v.x > 0.3f ? 1 : v.x < -0.3f ? -1 : 0;
            my = v.y < -0.5f ? 1 : v.y > 0.5f ? -1 : 0;
        }

        static bool PushingWall(HeroController h, int dir)
        {
            Bounds b = Col(h).bounds;
            return Physics2D.OverlapBox((Vector2)b.center + new Vector2(dir / Visuals.PxPerUnit, 0f), (Vector2)b.size - new Vector2(0.04f, 0.04f), 0f, 33562880) != null;
        }

        void Advance(float dt)
        {
            if (anim.Delay <= 0f || anim.Frames.Count <= 1) return;
            frameTimer += dt;
            while (frameTimer >= anim.Delay)
            {
                frameTimer -= anim.Delay;
                frameIndex++;
                if (frameIndex >= anim.Frames.Count)
                {
                    if (anim.Loop) frameIndex = 0;
                    else if (anim.Id == Animations.Idle.CelesteAnim && TryIdleMove()) return;
                    else if (anim.Goto != null && assets.Anims.ContainsKey(anim.Goto)) { Play(anim.Goto, true); return; }
                    else
                    {
                        if (anim.Id == Animations.BenchWake.CelesteAnim) waking = false;   // stood up
                        frameIndex = anim.Frames.Count - 1;
                        continue;
                    }
                }
                OnFrame();
            }
        }

        // Celeste.Player.UpdateHair
        void UpdateHairColor(float dt)
        {
            if (hasColorSkin && !AssistMode.IsOpen && hairVersion != AssistMode.HairVersion) SetColors(colorSkin);   // applied once the settings close   // changed in the settings
            if (binding) hairColor = StarFly;   // Celeste: Hair.Color = Sprite.Color while she is the star
            else if (ctrl.HairFlashTimer > 0f) hairColor = Flash;
            else if (ctrl.Dashes == 0 || ctrl.DashLocked) hairColor = Color.Lerp(hairColor, Used, 6f * dt);   // no dash, or locked after an enemy hit
            else hairColor = ctrl.Dashes == 2 ? TwoDashes : Normal;
        }

        // Celeste.PlayerHair.AfterUpdate, in Unity units (Y up).
        void UpdateHair(CelesteFrame frame, Vector3 feet, int facing, float sy, float vs, float dt)
        {
            float u = vs / Visuals.PxPerUnit;
            wave += dt * 4f;
            Vector2 offset = new Vector2(frame.HairOffset.x * facing, -frame.HairOffset.y) * u;
            nodes[0] = (Vector2)feet + new Vector2(0f, -Visuals.HairHeadOffset * sy * u) + offset;
            if (!AssistMode.IsOpen && hairN != AssistMode.HairLength)
            {
                int was = hairN; hairN = AssistMode.HairLength;
                for (int i = was; i < hairN; i++) nodes[i] = nodes[Math.Max(0, was - 1)];
            }
            if (!started) { for (int i = 1; i < nodes.Length; i++) nodes[i] = nodes[0]; started = true; }
            Vector2 step = new Vector2(0f, -Visuals.HairStepPerSegment * u);
            Vector2 target = nodes[0] + new Vector2(-facing * Visuals.HairStepInFacing * 2f * u, 0f) + step;
            Vector2 prev = nodes[0];
            float maxLink = Visuals.HairMaxLink * u;
            for (int i = 1; i < hairN; i++)
            {
                float approach = (1f - (float)i / hairN * 0.5f) * Visuals.HairStepApproach * u;
                nodes[i] = Vector2.MoveTowards(nodes[i], target, approach * dt);
                if ((nodes[i] - prev).magnitude > maxLink) nodes[i] = prev + (nodes[i] - prev).normalized * maxLink;
                target = nodes[i] + new Vector2(-facing * Visuals.HairStepInFacing * u, 0f) + step;
                prev = nodes[i];
            }
        }

        // Memory Crystal (tool Revenge Crystal): when she is hurt, a crystal Madeline stays where she stood (sheets/visuals memory_crystal_*).
        readonly List<KeyValuePair<SpriteRenderer, float>> crystals = new List<KeyValuePair<SpriteRenderer, float>>();
        int lastHealth = -1;
        void UpdateCrystals(HeroController h, float dt)
        {
            int hp = h.playerData.health;
            if (lastHealth >= 0 && hp < lastHealth && root.activeSelf && body != null && body.sprite != null && MadelineController.ToolOn("Revenge Crystal"))
            {
                var go = new GameObject("Memory Crystal Madeline");
                go.transform.position = body.transform.position + new Vector3(0f, 0f, Visuals.LayerDepthStep * 2f);
                go.transform.localScale = body.transform.lossyScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = material;
                sr.sprite = body.sprite;
                sr.color = Hex(Visuals.MemoryCrystalColor);
                sr.sortingLayerID = lastSortingLayer; sr.sortingOrder = lastSortingOrder;
                crystals.Add(new KeyValuePair<SpriteRenderer, float>(sr, Visuals.MemoryCrystalLife));
            }
            lastHealth = hp;
            Color baseC = Hex(Visuals.MemoryCrystalColor);
            for (int i = crystals.Count - 1; i >= 0; i--)
            {
                SpriteRenderer sr = crystals[i].Key;
                float left = crystals[i].Value - dt;
                if (sr == null) { crystals.RemoveAt(i); continue; }
                if (left <= 0f) { UnityEngine.Object.Destroy(sr.gameObject); crystals.RemoveAt(i); continue; }
                // a slow shimmer, then it fades over the last half second
                Color c = Color.Lerp(baseC, Color.white, 0.25f + 0.25f * Mathf.Sin(Time.time * 6f));
                c.a = baseC.a * Mathf.Clamp01(left / 0.5f);
                sr.color = c;
                crystals[i] = new KeyValuePair<SpriteRenderer, float>(sr, left);
            }
        }

        Vector3 lastHeroPos;
        void SpawnTrail()
        {
            if (!AssistMode.DashTrail) return;   // assist.dash_trail
            if (body == null || body.sprite == null || !root.activeSelf) return;
            var go = new GameObject("Trail");
            go.transform.SetParent(root.transform, false);
            go.transform.position = body.transform.position + new Vector3(0f, 0f, Visuals.LayerDepthStep * 3f);
            go.transform.localScale = body.transform.localScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = material;
            sr.sprite = body.sprite;
            Color c = hairColor; c.a = Visuals.DashTrailAlpha; sr.color = c;
            if (skin.Style == "hornet")
            {
                // Hornet's silk: a pale trail and white thread puffs
                sr.color = new Color(0.95f, 0.95f, 1f, Visuals.DashTrailAlpha * 0.8f);
                Particles.DustTinted(body.transform.position + new Vector3(0f, 6f * Visuals.SpriteScale / Visuals.PxPerUnit, 0f), Mathf.PI / 2f * (UnityEngine.Random.value < 0.5f ? 1f : -1f), 3, new Color(1f, 1f, 1f, 0.8f), 4f);
            }
            else if (skin.Style == "knight")
            {
                // the Knight's shade dash: a dark trail and black puffs instead of Celeste's coloured one
                sr.color = new Color(0.04f, 0.04f, 0.06f, Visuals.DashTrailAlpha);
                Particles.DustTinted(body.transform.position + new Vector3(0f, 6f * Visuals.SpriteScale / Visuals.PxPerUnit, 0f), Mathf.PI / 2f * (UnityEngine.Random.value < 0.5f ? 1f : -1f), 4, new Color(0.05f, 0.05f, 0.08f, 0.9f), 4f);
            }
            trails.Add(new KeyValuePair<SpriteRenderer, float>(sr, 0.3f));
        }

        static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString("#" + hex, out c);
            return c;
        }
    }
}
