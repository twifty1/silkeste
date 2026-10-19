using System;
using System.Collections.Generic;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Celeste's particles that Madeline makes: Dust.Burst with ParticleTypes.Dust (sheets/visuals.json dust_*),
    /// smoke puffs that drift out, fall a little and shrink away; and FlyFeather.P_Collect (feather_*), gold
    /// feathers that shoot out, hang, spin, blink and fade. Directions are Celeste angles (radians, Y down).
    /// </summary>
    public static class Particles
    {
        class Puff
        {
            public SpriteRenderer Sr; public Vector3 Velocity; public float Life, StartLife, StartSize, Spin;
            public bool Feather;
        }

        static Transform parent;
        static Material material;
        static List<Sprite> smoke = new List<Sprite>();
        static Sprite feather;
        static readonly List<Puff> live = new List<Puff>();
        static readonly List<Puff> spare = new List<Puff>();
        static readonly System.Random rng = new System.Random();
        static readonly Color FeatherColor = Hex(Visuals.FeatherColor), FeatherColor2 = Hex(Visuals.FeatherColor2);

        static float U { get { return Visuals.SpriteScale / Visuals.PxPerUnit; } }

        public static void Init(Transform root, Material mat, List<Sprite> smokeSprites, Sprite featherSprite)
        {
            parent = root; material = mat; smoke = smokeSprites; feather = featherSprite;
            live.Clear(); spare.Clear();
        }

        /// <summary>Dust.Burst(position, direction, count): puffs spread around position across the direction.</summary>
        public static void Dust(Vector3 world, float direction, int count, float range = Visuals.DustBurstRange)
        {
            if (parent == null || smoke.Count == 0) return;
            // Calc.AngleToVector(direction - pi/2, range) with both components made positive.
            float jx = Math.Abs(Mathf.Cos(direction - Mathf.PI / 2f) * range), jy = Math.Abs(Mathf.Sin(direction - Mathf.PI / 2f) * range);
            for (int i = 0; i < count; i++)
            {
                Puff p = Take(false);
                float a = direction - Visuals.DustDirectionRange / 2f + (float)rng.NextDouble() * Visuals.DustDirectionRange;
                p.Velocity = new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f) * Rand(Visuals.DustSpeedMin, Visuals.DustSpeedMax) * U;
                p.StartLife = p.Life = Rand(Visuals.DustLifeMin, Visuals.DustLifeMax);
                p.StartSize = Visuals.DustSize - Visuals.DustSizeRange / 2f + (float)rng.NextDouble() * Visuals.DustSizeRange;
                p.Spin = 0f;
                p.Sr.sprite = smoke[rng.Next(smoke.Count)];
                p.Sr.color = dustTint;
                Place(p, world + new Vector3(Rand(-jx, jx), -Rand(-jy, jy), 0f) * U);
            }
        }

        static Color dustTint = Color.white;
        /// <summary>Dust puffs in a colour: the Knight's shade dash (dark) and his soul wisps (white).</summary>
        public static void DustTinted(Vector3 world, float direction, int count, Color tint, float range = Visuals.DustBurstRange)
        {
            dustTint = tint;
            try { Dust(world, direction, count, range); } finally { dustTint = Color.white; }
        }

        /// <summary>ParticlesFG.Emit(FlyFeather.P_Collect, count, position, Vector2.One * range): all directions.</summary>
        public static void Feathers(Vector3 world, int count, float range)
        {
            if (parent == null || feather == null) return;
            for (int i = 0; i < count; i++)
            {
                Puff p = Take(true);
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                p.Velocity = new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f) * Rand(Visuals.FeatherSpeedMin, Visuals.FeatherSpeedMax) * U;
                p.StartLife = p.Life = Rand(Visuals.FeatherLifeMin, Visuals.FeatherLifeMax);
                p.StartSize = 1f;
                p.Spin = Rand(Visuals.FeatherSpinMin, Visuals.FeatherSpinMax) * (rng.Next(2) == 0 ? 1f : -1f);   // SpinFlippedChance
                p.Sr.sprite = feather;
                Place(p, world + new Vector3(Rand(-range, range), Rand(-range, range), 0f) * U);
            }
        }

        static Puff Take(bool isFeather)
        {
            Puff p;
            if (spare.Count > 0) { p = spare[spare.Count - 1]; spare.RemoveAt(spare.Count - 1); }
            else
            {
                var go = new GameObject("Particle");
                go.transform.SetParent(parent, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = material;
                p = new Puff { Sr = sr };
            }
            p.Feather = isFeather;
            return p;
        }

        static void Place(Puff p, Vector3 at)
        {
            p.Sr.transform.position = at;
            p.Sr.transform.rotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 360f);   // RotationModes.Random
            p.Sr.gameObject.SetActive(true);
            Look(p);
            live.Add(p);
        }

        /// <summary>Main thread, once a frame: Celeste's Particle.Update (UseActualDeltaTime: unscaled, but still while paused).</summary>
        public static void Update(int sortingLayer, int sortingOrder, float z)
        {
            float dt = Time.timeScale <= 0f ? 0f : Time.unscaledDeltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Puff p = live[i];
                if (p.Sr == null) { live.RemoveAt(i); continue; }
                p.Life -= dt;
                if (p.Life <= 0f) { p.Sr.gameObject.SetActive(false); live.RemoveAt(i); spare.Add(p); continue; }
                Vector3 pos = p.Sr.transform.position + p.Velocity * dt;
                if (p.Feather) p.Velocity *= Mathf.Pow(Visuals.FeatherSpeedMultiplier, dt);   // SpeedMultiplier
                else p.Velocity += new Vector3(0f, -Visuals.DustGravity * U, 0f) * dt;
                p.Sr.transform.position = new Vector3(pos.x, pos.y, z);
                if (p.Spin != 0f) p.Sr.transform.Rotate(0f, 0f, -p.Spin * Mathf.Rad2Deg * dt);
                p.Sr.sortingLayerID = sortingLayer;
                p.Sr.sortingOrder = sortingOrder;
                Look(p);
            }
        }

        static void Look(Puff p)
        {
            float t = Mathf.Clamp01(p.Life / p.StartLife);
            if (p.Feather)
            {
                // ColorModes.Blink (BetweenInterval(Life, 0.1)) and FadeModes.Late (fade over the last quarter).
                Color c = p.Life % 0.2f > 0.1f ? FeatherColor : FeatherColor2;
                c.a = Math.Min(1f, t / 0.25f);
                p.Sr.color = c;
                p.Sr.transform.localScale = new Vector3(Visuals.SpriteScale, Visuals.SpriteScale, 1f);
                return;
            }
            // ScaleOut: Size = StartSize * Ease.CubeOut(Life / StartLife).
            float s = p.StartSize * (1f - (1f - t) * (1f - t) * (1f - t)) * Visuals.SpriteScale;
            p.Sr.transform.localScale = new Vector3(s, s, 1f);
        }

        static float Rand(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString("#" + hex, out c);
            return c;
        }
    }
}
