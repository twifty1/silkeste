using System;
using System.Collections.Generic;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Celeste's dash feel (sheets/visuals.json dash_* / slash_*): the freeze in DashBegin, then
    /// DashCoroutine's directional shake and SlashFx. Freeze and shake go through Silksong's
    /// own camera shake (hooks.dash_freeze_shake), so its screen shake setting applies.
    /// </summary>
    public class DashFx
    {
        readonly Transform parent;
        readonly Material material;
        readonly List<Sprite> slashFrames;
        readonly List<Effect> effects = new List<Effect>();

        class Effect { public SpriteRenderer Slash; public Vector3 Velocity; public float T; }

        public DashFx(Transform parent, Material material, List<Sprite> slashFrames)
        {
            this.parent = parent;
            this.material = material;
            this.slashFrames = slashFrames;
        }

        /// <summary>Celeste.Freeze(0.05f): Silksong's freeze frames are 1/60 s each.</summary>
        public void Freeze(UnityEngine.Object source)
        {
            if (Time.timeScale <= Visuals.DashFreezeMinTimescale) return;
            Shake(new CelesteShake(Vector2.zero, 0f, (int)Visuals.DashFreezeFrames), source, true);
        }

        /// <summary>DashCoroutine: DirectionalShake(dir, 0.2) and SlashFx.Burst(Center, angle).</summary>
        public void Launch(UnityEngine.Object source, Vector3 center, Vector2 shakeDir, Vector2 slashDir, int sortingLayer, int sortingOrder)
        {
            Shake(new CelesteShake(Up(shakeDir).normalized, Visuals.DashShakeTime, 0), source, false);
            float u = Visuals.SpriteScale / Visuals.PxPerUnit;
            var e = new Effect();
            if (slashFrames.Count > 0)
            {
                var go = new GameObject("DashSlash");
                go.transform.SetParent(parent, false);
                go.transform.position = center + new Vector3(0f, 0f, -Visuals.LayerDepthStep * 2f);
                go.transform.localScale = new Vector3(Visuals.SpriteScale, Visuals.SpriteScale, 1f);
                float angle = Mathf.Atan2(slashDir.y, slashDir.x);   // Celeste, Y down
                // SlashFx leaves the sprite unrotated when the angle is pi (a dash straight left).
                if (Math.Abs(angle - Mathf.PI) > 0.01f) go.transform.rotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
                e.Slash = go.AddComponent<SpriteRenderer>();
                e.Slash.sharedMaterial = material;
                e.Slash.sprite = slashFrames[0];
                e.Slash.sortingLayerID = sortingLayer;
                e.Slash.sortingOrder = sortingOrder;
                e.Velocity = (Vector3)(Up(slashDir).normalized * Visuals.SlashDrift * u);
            }
            effects.Add(e);
            Update(0f);
        }

        public void Update(float dt)
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                Effect e = effects[i];
                e.T += dt;
                bool done = true;
                if (e.Slash != null)
                {
                    int frame = (int)(e.T / Visuals.SlashFrameTime);
                    done = frame >= slashFrames.Count;
                    if (!done)
                    {
                        e.Slash.sprite = slashFrames[frame];
                        e.Slash.transform.position += e.Velocity * dt;
                    }
                }
                if (done)
                {
                    if (e.Slash != null) UnityEngine.Object.Destroy(e.Slash.gameObject);
                    effects.RemoveAt(i);
                }
            }
        }

        static Vector2 Up(Vector2 celeste) { return new Vector2(celeste.x, -celeste.y); }

        static void Shake(ICameraShake shake, UnityEngine.Object source, bool freeze)
        {
            try
            {
                CameraManagerReference cam = global::GlobalSettings.Camera.MainCameraShakeManager;
                if (cam != null) cam.DoShake(shake, source, freeze, false, false);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Dash shake: " + e.Message); }
        }

        /// <summary>Level.DirectionalShake as a Silksong camera shake: ceil(timeLeft * 10) px against the
        /// direction, flipping every 0.04 s. Zero time with freeze frames is Celeste.Freeze.</summary>
        sealed class CelesteShake : ICameraShake
        {
            readonly Vector2 dir;
            readonly float time;
            readonly int freezeFrames;

            public CelesteShake(Vector2 dir, float time, int freezeFrames) { this.dir = dir; this.time = time; this.freezeFrames = freezeFrames; }

            public bool CanFinish { get { return true; } }
            public bool PersistThroughScenes { get { return false; } }
            public int FreezeFrames { get { return freezeFrames; } }
            public ICameraShakeVibration CameraShakeVibration { get { return null; } }
            public CameraShakeWorldForceIntensities WorldForceOnStart { get { return CameraShakeWorldForceIntensities.None; } }
            public float Magnitude { get { return Mathf.Ceil(time * Visuals.DashShakePxPerSecond) / Visuals.PxPerUnit * AssistMode.ShakeMult; } }

            public Vector2 GetOffset(float elapsed)
            {
                if (dir == Vector2.zero || elapsed >= time) return Vector2.zero;
                int tick = (int)(elapsed / Visuals.DashShakeInterval);
                float px = Mathf.Ceil((time - tick * Visuals.DashShakeInterval) * Visuals.DashShakePxPerSecond - 0.0001f);
                float sign = tick % 2 == 0 ? 1f : -1f;
                return -dir * sign * px / Visuals.PxPerUnit * AssistMode.ShakeMult;   // assist.screen_shake
            }

            public bool IsDone(float elapsed) { return elapsed >= time; }
        }
    }
}
