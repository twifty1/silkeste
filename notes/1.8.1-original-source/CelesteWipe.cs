using GlobalEnums;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Celeste's death transition (Chapter 1's AngledWipe) in place of Silksong's fade: after Madeline's death
    /// burst the screen is covered by six slanted black bands, Silksong reloads and respawns underneath, and the
    /// wipe opens again once the hero is back. Drawn in OnGUI so it sits above everything, on unscaled time.
    /// </summary>
    public class CelesteWipe : MonoBehaviour
    {
        enum Phase { Idle, Waiting, Covering, Covered, Revealing }
        Phase phase;
        float clock, percent, aliveFor;
        bool hazard;
        Material mat;

        void Update()
        {
            HeroController h = HeroController.SilentInstance;
            GameManager gm = GameManager.instance;
            bool dying = h != null && (h.cState.dead || h.cState.hazardDeath);
            float dt = Time.unscaledDeltaTime;
            switch (phase)
            {
                case Phase.Idle:
                    if (dying && Plugin.Ready) { phase = Phase.Waiting; clock = 0f; hazard = h.cState.hazardDeath && !h.cState.dead; }
                    break;
                case Phase.Waiting:
                    clock += dt;
                    if (clock >= (hazard ? Visuals.HazardWipeStart : Visuals.WipeStart)) { phase = Phase.Covering; percent = 0f; }
                    break;
                case Phase.Covering:
                    percent = Mathf.MoveTowards(percent, 1f, dt / Visuals.WipeDuration);
                    if (percent >= 1f) { phase = Phase.Covered; aliveFor = 0f; }
                    break;
                case Phase.Covered:
                    bool alive = h != null && !dying && gm != null && gm.GameState == GameState.PLAYING && !h.cState.transitioning;
                    aliveFor = alive ? aliveFor + dt : 0f;
                    if (aliveFor >= Visuals.WipeRevealDelay) { phase = Phase.Revealing; percent = 0f; }
                    break;
                case Phase.Revealing:
                    percent = Mathf.MoveTowards(percent, 1f, dt / Visuals.WipeDuration);
                    if (percent >= 1f) phase = Phase.Idle;
                    break;
            }
            ScreenCovered = phase == Phase.Covering || phase == Phase.Covered;
        }

        /// <summary>The wipe is closing or closed; Celeste's respawn starts as it opens again.</summary>
        public static bool ScreenCovered { get; private set; }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (phase != Phase.Covering && phase != Phase.Covered && phase != Phase.Revealing) return;
            if (mat == null)
            {
                Shader s = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
                mat = new Material(s);
                mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }
            bool wipeIn = phase == Phase.Revealing;
            float p = phase == Phase.Covered ? 1f : percent;
            float sx = Screen.width / 1920f, sy = Screen.height / 1080f;
            int rows = (int)Visuals.WipeRows;
            float rowH = 1100f / rows, slant = Visuals.WipeAngle;

            mat.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.TRIANGLES);
            GL.Color(Color.black);
            for (int i = 0; i < rows; i++)
            {
                // Celeste.AngledWipe.Render, in 1920x1080 with Y down.
                float x0 = -slant, y0 = -10f + i * rowH;
                float frac = (float)i / rows;
                float start = (wipeIn ? 1f - frac : frac) * 0.3f;
                float fill = p > start ? Mathf.Min(1f, (p - start) / 0.7f) : 0f;
                if (wipeIn) fill = 1f - fill;
                float w = (1920f + slant) * fill;
                Vector2[] v =
                {
                    new Vector2(x0, y0), new Vector2(x0 + w, y0), new Vector2(x0, y0 + rowH),
                    new Vector2(x0 + w, y0), new Vector2(x0 + w + slant, y0 + rowH), new Vector2(x0, y0 + rowH),
                };
                foreach (Vector2 c in v)
                {
                    float x = wipeIn ? 1920f - c.x : c.x, y = wipeIn ? 1080f - c.y : c.y;
                    GL.Vertex3(x * sx, Screen.height - y * sy, 0f);
                }
            }
            GL.End();
            GL.PopMatrix();
        }

        /// <summary>hooks.delay_hazard_fade / delay_death_fade: when Silksong starts its own fade and respawn,
        /// part-way through the cover wipe (visuals.silksong_fade_at), so the whole death is Celeste-length.</summary>
        public static float FadeStart { get { return Visuals.WipeStart + Visuals.WipeDuration * Visuals.SilksongFadeAt; } }
        public static float HazardFadeStart { get { return Visuals.HazardWipeStart + Visuals.WipeDuration * Visuals.SilksongFadeAt; } }
    }
}
