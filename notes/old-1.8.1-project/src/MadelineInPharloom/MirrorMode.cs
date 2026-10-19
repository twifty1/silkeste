using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// assist.mirror_mode, Celeste's variant (user 2026-10-14): the game is shown flipped left to right. The finished
    /// picture of Silksong's main camera is mirrored as a whole (flipping each camera on its own made the background
    /// layers drift the wrong way, user 2026-10-14); the HUD camera draws after it, so the HUD and menus stay readable.
    /// MadelineController flips left and right on the controls to match.
    /// </summary>
    public class MirrorMode : MonoBehaviour
    {
        Camera attachedTo;

        // the "Speak" / "Listen" prompts float over NPCs on the HUD camera, placed from where the NPC is on screen:
        // with the picture mirrored they must sit on the mirrored side too (user 2026-10-14)
        void Awake()
        {
            try
            {
                new HarmonyLib.Harmony("silkeste.mirror").Patch(HarmonyLib.AccessTools.Method(typeof(PromptMarker), "UpdatePosition"),
                    postfix: new HarmonyLib.HarmonyMethod(typeof(MirrorMode), nameof(PromptPostfix)));
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("Mirror prompts: " + e.Message); }
        }
        static readonly System.Reflection.FieldInfo FollowF = HarmonyLib.AccessTools.Field(typeof(PromptMarker), "followTransform"),
            OffsetF = HarmonyLib.AccessTools.Field(typeof(PromptMarker), "followOffset"),
            MainF = HarmonyLib.AccessTools.Field(typeof(PromptMarker), "mainCam"), HudF = HarmonyLib.AccessTools.Field(typeof(PromptMarker), "hudCam");
        static void PromptPostfix(PromptMarker __instance)
        {
            if (!MirrorBlit.Active) return;
            var follow = FollowF != null ? FollowF.GetValue(__instance) as Transform : null;
            var main = MainF != null ? MainF.GetValue(__instance) as Camera : null;
            var hud = HudF != null ? HudF.GetValue(__instance) as Camera : null;
            if (follow == null || main == null || hud == null) return;
            Vector3 off = OffsetF != null ? (Vector3)OffsetF.GetValue(__instance) : Vector3.zero;
            Vector3 vp = main.WorldToViewportPoint(follow.position + off);
            vp.x = 1f - vp.x;
            __instance.transform.position = hud.ViewportToWorldPoint(vp);
        }

        void Update()
        {
            var gc = GameCameras.SilentInstance;
            Camera main = gc != null ? gc.mainCamera : null;
            if (main == null || main == attachedTo) return;
            if (main.GetComponent<MirrorBlit>() == null) main.gameObject.AddComponent<MirrorBlit>();
            attachedTo = main;
        }
    }

    public class MirrorBlit : MonoBehaviour
    {
        static readonly Vector2 FlipScale = new Vector2(-1f, 1f), FlipOffset = new Vector2(1f, 0f);

        public static bool Active { get { var gm = GameManager.SilentInstance; return AssistMode.MirrorMode && gm != null && !gm.IsMenuScene(); } }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (Active) Graphics.Blit(src, dst, FlipScale, FlipOffset);
            else Graphics.Blit(src, dst);
        }
    }
}
