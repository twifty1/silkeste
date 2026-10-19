using System;
using System.Collections;
using System.IO;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Theo's camera (assist.key_camera, user 2026-10-12): a photo of the game without its HUD or Silkeste's overlays,
    /// a white flash, the picture saved to Pictures/Silkeste, and a polaroid of it in the corner for a moment.
    /// </summary>
    public class TheoCamera : MonoBehaviour
    {
        /// <summary>While a photo is taken, every overlay stays out of the picture.</summary>
        public static bool Hiding;
        /// <summary>A boss photo: the card stays, the timer and counters step out.</summary>
        public static bool HidingHud;
        static GuiArt Art { get { return SpeedrunTimer.Art; } }

        bool busy;
        float flash, showT = -1f;
        Texture2D shot;
        bool loggedError;
        public static TheoCamera Instance;
        void Awake() { Instance = this; }
#if TESTHARNESS
        public void TestTake() { if (!busy) StartCoroutine(Take(false, null)); }
#endif
        bool showAfterCard;   // an automatic boss photo: its polaroid waits until the card is closed

        /// <summary>A boss falls: a photo of the arena with its card on screen (user 2026-10-12), kept quietly.</summary>
        public static void BossPhoto(string boss)
        {
            if (Instance != null && !Instance.busy) Instance.StartCoroutine(Instance.Take(true, boss));
        }
        void Update()
        {
            try { Tick(); }
            catch (Exception e) { if (!loggedError) { loggedError = true; Plugin.Log.LogError("TheoCamera: " + e); } }
        }

        void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            if (flash > 0f) flash = Mathf.Max(0f, flash - dt * 2.5f);
            if (showT >= 0f && (showT += dt) > 3.2f) showT = -1f;
            if (showAfterCard && !BossCard.CardShowing) { showAfterCard = false; showT = 0f; }
            GameManager gm = GameManager.SilentInstance;
            if (!Plugin.Ready || busy || gm == null || HeroController.SilentInstance == null || gm.IsMenuScene()) return;
            // the Camera key, or L3 (left stick click) on a controller (user 2026-10-12)
            bool l3 = false;
            try { var dev = InControl.InputManager.ActiveDevice; l3 = dev != null && dev.LeftStickButton.WasPressed; } catch (Exception) { }
            if ((AssistMode.KeyPressed(AssistOptions.KeyCamera) || l3) && !AssistMode.IsOpen && !BossCard.RecordsOpen && !gm.isPaused)
                StartCoroutine(Take(false, null));
        }

        IEnumerator Take(bool withCard, string boss)
        {
            busy = true;
            Hiding = !withCard;   // a boss photo keeps its card in the picture
            HidingHud = true;
            Camera hud = GameCameras.instance != null ? GameCameras.instance.hudCamera : null;
            bool hudWas = hud != null && hud.enabled;
            if (hud != null) hud.enabled = false;
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try { tex = ScreenCapture.CaptureScreenshotAsTexture(); }
            catch (Exception e) { Plugin.Log.LogWarning("Photo: " + e.Message); }
            if (hud != null) hud.enabled = hudWas;
            Hiding = false; HidingHud = false;
            if (tex != null)
            {
                try
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Silkeste");
                    Directory.CreateDirectory(dir);
                    string tag = string.IsNullOrEmpty(boss) ? "" : "_" + new string(Array.FindAll(boss.ToCharArray(), ch => char.IsLetterOrDigit(ch)));
                    string file = Path.Combine(dir, "Silkeste" + tag + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                    File.WriteAllBytes(file, tex.EncodeToPNG());
                    Plugin.Log.LogInfo("Photo saved: " + file);
                }
                catch (Exception e) { Plugin.Log.LogWarning("Photo: " + e.Message); }
                if (shot != null) Destroy(shot);
                shot = tex;
                if (withCard) showAfterCard = true;   // no flash over the card: the polaroid shows once it is closed
                else { flash = 1f; showT = 0f; Sfx.Play(Sounds.MenuButtonSelect.Id); Achievements.Photo(); }
            }
            busy = false;
        }

        // the flash, then the photo as a polaroid sliding in at the bottom right, tilted, with Theo's caption
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || Hiding) return;
            float s = Screen.height / 1080f, sw = Screen.width / s;
            Matrix4x4 old = GUI.matrix;
            GUI.depth = -3;
            if (flash > 0f)
            {
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
                GUI.color = new Color(1f, 1f, 1f, flash * 0.9f);
                GUI.DrawTexture(new Rect(0f, 0f, sw, 1080f), Texture2D.whiteTexture);
            }
            if (showT >= 0f && shot != null)
            {
                float k = showT < 0.35f ? showT / 0.35f : showT > 2.7f ? 1f - (showT - 2.7f) / 0.5f : 1f;
                k = Mathf.Clamp01(k);
                float ease = 1f - (1f - k) * (1f - k) * (1f - k);
                float pw = 420f, ph = pw * shot.height / shot.width, border = 18f, bottom = 70f;
                float fw = pw + border * 2f, fh = ph + border + 70f;
                Vector2 c = new Vector2(sw - 60f - fw / 2f, 1080f - 40f - fh / 2f + (1f - ease) * 300f);
                GUI.matrix = Matrix4x4.TRS(new Vector3(c.x * s, c.y * s, 0f), Quaternion.Euler(0f, 0f, -4f), new Vector3(s, s, 1f));
                GUI.color = new Color(0f, 0f, 0f, 0.35f * ease);
                GUI.DrawTexture(new Rect(-fw / 2f + 8f, -fh / 2f + 10f, fw, fh), Texture2D.whiteTexture);
                GUI.color = new Color(0.97f, 0.96f, 0.92f, ease);
                GUI.DrawTexture(new Rect(-fw / 2f, -fh / 2f, fw, fh), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, ease);
                GUI.DrawTexture(new Rect(-fw / 2f + border, -fh / 2f + border, pw, ph), shot);
                if (Art != null)
                {
                    const string caption = "Saved to Pictures/Silkeste";
                    float cs = 0.5f;
                    Art.DrawLine(caption, -Art.Measure(caption) * cs / 2f, fh / 2f - bottom / 2f - Art.LineHeight * cs / 2f, cs, new Color(0.2f, 0.16f, 0.14f, ease));
                }
            }
            GUI.matrix = old;
            GUI.color = Color.white;
        }
    }
}
