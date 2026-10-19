using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MadelineInPharloom;

public class OneHitMode : MonoBehaviour
{
	private static Vector3 entryPos;

	private static bool entryRight;

	private static bool haveEntry;

	private string scene;

	private float scanIn;

	private int rememberedSlot = -1;

	private readonly Dictionary<Transform, Vector3> hidden = new Dictionary<Transform, Vector3>();

	private static readonly Regex MaskName = new Regex("^Health (\\d+)$");

	private void Update()
	{
		HeroController silentInstance = HeroController.UnsafeInstance;
		if (silentInstance != null)
		{
			// the title's Continue loads the save played last
			GameManager gmL = GameManager.SilentInstance;
			if (gmL != null && gmL.profileID > 0 && gmL.profileID != rememberedSlot)
			{
				rememberedSlot = gmL.profileID;
				CelesteFileSelect.RememberSlot(rememberedSlot);
			}
			string text = SceneManager.GetActiveScene().name;
			bool flag = silentInstance.cState.transitioning || !silentInstance.acceptingInput;
			if (text != scene)
			{
				scene = text;
				haveEntry = false;
			}
			if (!haveEntry && !flag && silentInstance.cState.onGround)
			{
				entryPos = silentInstance.transform.position;
				entryRight = silentInstance.cState.facingRight;
				haveEntry = true;
			}
		}
		if (!((scanIn -= Time.unscaledDeltaTime) > 0f))
		{
			scanIn = 0.5f;
			ShowOneMask(AssistMode.OneHit);
		}
	}

	public static void UseEntrance(HeroController h)
	{
		if (haveEntry && h != null)
		{
			h.SetHazardRespawn(entryPos, entryRight);
		}
	}

	private void ShowOneMask(bool on)
	{
		if (!on)
		{
			foreach (KeyValuePair<Transform, Vector3> item in hidden)
			{
				if (item.Key != null)
				{
					item.Key.localScale = item.Value;
				}
			}
			hidden.Clear();
			return;
		}
		GameCameras silentInstance = GameCameras.SilentInstance;
		if (silentInstance == null || silentInstance.hudCamera == null)
		{
			return;
		}
		Transform[] componentsInChildren = silentInstance.hudCamera.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			Match match = MaskName.Match(transform.name);
			if (match.Success && !(match.Groups[1].Value == "1") && !hidden.ContainsKey(transform))
			{
				hidden[transform] = transform.localScale;
				transform.localScale = Vector3.zero;
			}
		}
	}
}
