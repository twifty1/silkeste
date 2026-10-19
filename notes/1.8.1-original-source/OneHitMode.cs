using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// assist.one_hit (user 2026-10-14): Madeline has one heart. The HUD shows a single mask (the others are shrunk
    /// away while it's on), and a hit sends her back to where she came into the room, as Celeste restarts a screen.
    /// </summary>
    public class OneHitMode : MonoBehaviour
    {
        static Vector3 entryPos; static bool entryRight, haveEntry;
        string scene;
        float scanIn;
        readonly Dictionary<Transform, Vector3> hidden = new Dictionary<Transform, Vector3>();
        static readonly Regex MaskName = new Regex(@"^Health (\d+)$");

        void Update()
        {
            var h = HeroController.SilentInstance;
            if (h != null)
            {
                // the spot she stands on once she has walked in from a door or transition
                string now = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                bool trans = h.cState.transitioning || !h.acceptingInput;
                if (now != scene) { scene = now; haveEntry = false; }
                if (!haveEntry && !trans && h.cState.onGround) { entryPos = h.transform.position; entryRight = h.cState.facingRight; haveEntry = true; }
            }
            if ((scanIn -= Time.unscaledDeltaTime) > 0f) return;
            scanIn = 0.5f;
            ShowOneMask(AssistMode.OneHit);
        }

        /// <summary>Before the one-hit death: the respawn goes to the room's entrance.</summary>
        public static void UseEntrance(HeroController h)
        {
            if (haveEntry && h != null) h.SetHazardRespawn(entryPos, entryRight);
        }

        // Silksong's masks are HUD objects "Health 1", "Health 2" ...: all but the first shrink away
        void ShowOneMask(bool on)
        {
            if (!on)
            {
                foreach (var kv in hidden) if (kv.Key != null) kv.Key.localScale = kv.Value;
                hidden.Clear();
                return;
            }
            var gc = GameCameras.SilentInstance;
            if (gc == null || gc.hudCamera == null) return;
            foreach (Transform t in gc.hudCamera.GetComponentsInChildren<Transform>(true))
            {
                var m = MaskName.Match(t.name);
                if (!m.Success || m.Groups[1].Value == "1" || hidden.ContainsKey(t)) continue;
                hidden[t] = t.localScale;
                t.localScale = Vector3.zero;
            }
        }
    }
}
