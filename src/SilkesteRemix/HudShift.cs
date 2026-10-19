using System;
using UnityEngine;

namespace SilkesteRemix;

public class HudShift : MonoBehaviour
{
	public static float PanelPixels;

	private static float shown;

	private CurrencyCounterStack[] stacks = new CurrencyCounterStack[0];

	private Vector3[] applied = new Vector3[0];

	private Vector3[] basePos = new Vector3[0];

	private float findIn;

	private void LateUpdate()
	{
		try
		{
			Shift();
		}
		catch (Exception)
		{
		}
	}

	private void Shift()
	{
		if ((findIn -= Time.unscaledDeltaTime) <= 0f || Array.Exists(stacks, (CurrencyCounterStack s) => s == null))
		{
			findIn = 2f;
			CurrencyCounterStack[] array = UnityEngine.Object.FindObjectsOfType<CurrencyCounterStack>();
			if (array.Length != stacks.Length || !Array.TrueForAll(array, (CurrencyCounterStack f) => Array.IndexOf(stacks, f) >= 0))
			{
				stacks = array;
				applied = new Vector3[array.Length];
				basePos = new Vector3[array.Length];
				for (int num = 0; num < array.Length; num++)
				{
					basePos[num] = (applied[num] = array[num].transform.position);
				}
			}
		}
		shown = Mathf.MoveTowards(shown, PanelPixels, Time.unscaledDeltaTime * 1200f);
		PanelPixels = 0f;
		GameCameras instance = GameCameras.instance;
		Camera camera = ((instance != null) ? instance.hudCamera : null);
		if (camera == null)
		{
			return;
		}
		float num2 = (camera.orthographic ? (camera.orthographicSize * 2f / 1080f) : 0.01f);
		for (int num3 = 0; num3 < stacks.Length; num3++)
		{
			Transform transform = stacks[num3].transform;
			if (stacks[num3].wasVisible)
			{
				if ((transform.position - applied[num3]).sqrMagnitude > 1E-06f)
				{
					basePos[num3] = transform.position;
				}
				Vector3 vector = (transform.position = basePos[num3] - new Vector3(0f, shown * num2, 0f));
				applied[num3] = vector;
			}
		}
	}
}
