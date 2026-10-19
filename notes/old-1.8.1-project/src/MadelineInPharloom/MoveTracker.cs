using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>How many of each Celeste move she has done this session (counted where MadelineController gives the speed).</summary>
    public static class MoveStats
    {
        public static int Dashes, Supers, Hypers, Wavedashes, Ultras, WallBounces, WallJumps, Dodges;
    }

#if TESTHARNESS
    /// <summary>Test build only (user 2026-10-13): a move counter on the right side of the screen. A count flashes gold when it goes up.</summary>
    public class MoveTracker : MonoBehaviour
    {
        static readonly string[] Names = { "Dashes", "Supers", "Hypers", "Wavedashes", "Ultras", "Wall bounces", "Wall jumps", "Dodge streak" };
        readonly int[] last = new int[8];
        readonly float[] flash = new float[8];

        static int[] Now() { return new[] { MoveStats.Dashes, MoveStats.Supers, MoveStats.Hypers, MoveStats.Wavedashes, MoveStats.Ultras, MoveStats.WallBounces, MoveStats.WallJumps, MoveStats.Dodges }; }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || TheoCamera.Hiding || AssistMode.IsOpen || BossCard.RecordsOpen) return;
            GuiArt art = SpeedrunTimer.Art;
            GameManager gm = GameManager.SilentInstance;
            if (art == null || gm == null || gm.IsMenuScene() || HeroController.SilentInstance == null) return;
            int[] now = Now();
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < now.Length; i++) { if (now[i] != last[i]) { flash[i] = 1f; last[i] = now[i]; } flash[i] = Mathf.Max(0f, flash[i] - dt * 1.5f); }

            float s = Screen.height / 1080f, sw = Screen.width / s;
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            float ts = 0.45f, lh = art.LineHeight * ts + 6f, w = 300f, x = sw - w - 24f, y = 300f;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(x - 14f, y - 10f, w + 28f, lh * (Names.Length + 1) + 16f), Texture2D.whiteTexture);
            var ink = new Color(0f, 0f, 0f, 1f);
            art.DrawOutlined("MOVES", x, y, ts, new Color(0.7f, 0.7f, 0.7f, 1f), ink, 2f);
            y += lh;
            for (int i = 0; i < Names.Length; i++)
            {
                Color c = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.3f), flash[i]);
                art.DrawOutlined(Names[i], x, y, ts, c, ink, 2f);
                string v = now[i].ToString();
                art.DrawOutlined(v, x + w - art.Measure(v) * ts, y, ts, c, ink, 2f);
                y += lh;
            }
            GUI.matrix = old;
            GUI.color = Color.white;
        }
    }
#endif
}
