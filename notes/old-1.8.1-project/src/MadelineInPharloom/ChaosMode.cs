using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// assist.chaos_mode (user 2026-10-17): every room rolls one random Variant, never the same twice in a row, and a
    /// banner names it as she walks in. AssistMode's Variant getters ask Is() on top of their own settings.
    /// </summary>
    public class ChaosMode : MonoBehaviour
    {
        static readonly string[] Picks = { "Ice", "Low Gravity", "High Gravity", "Wind Left", "Wind Right", "Mirror Mode", "Super Dashing", "Fast Madeline", "Bouncy" };
        static string pick;
        string scene;
        float bannerT = -1f;

        public static bool Is(string name) { return pick != null && pick == name; }

        void Update()
        {
            if (!AssistMode.Chaos) { pick = null; scene = null; return; }
            var h = HeroController.SilentInstance;
            var gm = GameManager.SilentInstance;
            if (h == null || gm == null || gm.IsMenuScene()) { pick = null; return; }
            string now = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (now == scene) { if (bannerT >= 0f && (bannerT += Time.unscaledDeltaTime) > 2.6f) bannerT = -1f; return; }
            scene = now;
            string was = pick;
            do pick = Picks[Random.Range(0, Picks.Length)]; while (pick == was && Picks.Length > 1);
            bannerT = 0f;
            Sfx.Play(MadelineInPharloom.Sheets.Sounds.MenuToggleOn.Id);
        }

        void OnGUI()
        {
            if (bannerT < 0f || pick == null || Event.current.type != EventType.Repaint || SpeedrunTimer.Art == null || TheoCamera.Hiding) return;
            var art = SpeedrunTimer.Art;
            float s = Screen.height / 1080f, sw = Screen.width / s;
            float a = Mathf.Clamp01(bannerT / 0.25f) * Mathf.Clamp01((2.6f - bannerT) / 0.4f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            string head = "CHAOS", line = pick;
            float y = 150f - (1f - a) * 30f;
            Color hue = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.5f, 1f), 0.6f, 1f); hue.a = a;
            art.DrawOutlined(head, sw / 2f - art.Measure(head) * 0.5f / 2f, y, 0.5f, hue, new Color(0f, 0f, 0f, 0.8f * a), 2f);
            art.DrawOutlined(line, sw / 2f - art.Measure(line) * 1f / 2f, y + art.LineHeight * 0.5f, 1f, new Color(1f, 1f, 1f, a), new Color(0.08f, 0.06f, 0.16f, 0.9f * a), 4f);
            GUI.matrix = old;
            GUI.color = Color.white;
        }
    }
}
