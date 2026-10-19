using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MadelineInPharloom;

public class MirrorMode : MonoBehaviour
{
	private Camera attachedTo;

	private static readonly FieldInfo FollowF = AccessTools.Field(typeof(PromptMarker), "followTransform");

	private static readonly FieldInfo OffsetF = AccessTools.Field(typeof(PromptMarker), "followOffset");

	private static readonly FieldInfo MainF = AccessTools.Field(typeof(PromptMarker), "mainCam");

	private static readonly FieldInfo HudF = AccessTools.Field(typeof(PromptMarker), "hudCam");

	private void Awake()
	{
		try
		{
			new Harmony("silkeste.mirror").Patch(AccessTools.Method(typeof(PromptMarker), "UpdatePosition"), null, new HarmonyMethod(typeof(MirrorMode), "PromptPostfix"));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Mirror prompts: " + ex.Message);
		}
	}

	private static void PromptPostfix(PromptMarker __instance)
	{
		if (MirrorBlit.Active)
		{
			Transform transform = ((FollowF != null) ? (FollowF.GetValue(__instance) as Transform) : null);
			Camera camera = ((MainF != null) ? (MainF.GetValue(__instance) as Camera) : null);
			Camera camera2 = ((HudF != null) ? (HudF.GetValue(__instance) as Camera) : null);
			if (!(transform == null) && !(camera == null) && !(camera2 == null))
			{
				Vector3 vector = ((OffsetF != null) ? ((Vector3)OffsetF.GetValue(__instance)) : Vector3.zero);
				Vector3 position = camera.WorldToViewportPoint(transform.position + vector);
				position.x = 1f - position.x;
				__instance.transform.position = camera2.ViewportToWorldPoint(position);
			}
		}
	}

	private void Update()
	{
		GameCameras silentInstance = GameCameras.SilentInstance;
		Camera camera = ((silentInstance != null) ? silentInstance.mainCamera : null);
		if (!(camera == null) && !(camera == attachedTo))
		{
			if (camera.GetComponent<MirrorBlit>() == null)
			{
				camera.gameObject.AddComponent<MirrorBlit>();
			}
			attachedTo = camera;
		}
	}
}
