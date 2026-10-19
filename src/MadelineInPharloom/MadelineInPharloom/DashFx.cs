using System;
using System.Collections.Generic;
using GlobalSettings;
using UnityEngine;

namespace MadelineInPharloom;

public class DashFx
{
	private class Effect
	{
		public SpriteRenderer Slash;

		public Vector3 Velocity;

		public float T;
	}

	private sealed class CelesteShake : ICameraShake
	{
		private readonly Vector2 dir;

		private readonly float time;

		private readonly int freezeFrames;

		public bool CanFinish => true;

		public bool PersistThroughScenes => false;

		public int FreezeFrames => freezeFrames;

		public ICameraShakeVibration CameraShakeVibration => null;

		public CameraShakeWorldForceIntensities WorldForceOnStart => CameraShakeWorldForceIntensities.None;

		public float Magnitude => Mathf.Ceil(time * 10f) / 8f * AssistMode.ShakeMult;

		public CelesteShake(Vector2 dir, float time, int freezeFrames)
		{
			this.dir = dir;
			this.time = time;
			this.freezeFrames = freezeFrames;
		}

		public Vector2 GetOffset(float elapsed)
		{
			if (dir == Vector2.zero || elapsed >= time)
			{
				return Vector2.zero;
			}
			int num = (int)(elapsed / 0.04f);
			float num2 = Mathf.Ceil((time - (float)num * 0.04f) * 10f - 0.0001f);
			float num3 = ((num % 2 == 0) ? 1f : (-1f));
			return -dir * num3 * num2 / 8f * AssistMode.ShakeMult;
		}

		public bool IsDone(float elapsed)
		{
			return elapsed >= time;
		}
	}

	private readonly Transform parent;

	private readonly Material material;

	private readonly List<Sprite> slashFrames;

	private readonly List<Effect> effects = new List<Effect>();

	public DashFx(Transform parent, Material material, List<Sprite> slashFrames)
	{
		this.parent = parent;
		this.material = material;
		this.slashFrames = slashFrames;
	}

	public void Freeze(UnityEngine.Object source)
	{
		if (!(Time.timeScale <= 0.25f))
		{
			Shake(new CelesteShake(Vector2.zero, 0f, 3), source, freeze: true);
		}
	}

	public void Launch(UnityEngine.Object source, Vector3 center, Vector2 shakeDir, Vector2 slashDir, int sortingLayer, int sortingOrder)
	{
		Shake(new CelesteShake(Up(shakeDir).normalized, 0.2f, 0), source, freeze: false);
		float num = 0.1125f;
		Effect effect = new Effect();
		if (slashFrames.Count > 0)
		{
			GameObject gameObject = new GameObject("DashSlash");
			gameObject.transform.SetParent(parent, worldPositionStays: false);
			gameObject.transform.position = center + new Vector3(0f, 0f, -0.0008f);
			gameObject.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
			float num2 = Mathf.Atan2(slashDir.y, slashDir.x);
			if (Math.Abs(num2 - MathF.PI) > 0.01f)
			{
				gameObject.transform.rotation = Quaternion.Euler(0f, 0f, (0f - num2) * 57.29578f);
			}
			effect.Slash = gameObject.AddComponent<SpriteRenderer>();
			effect.Slash.sharedMaterial = material;
			effect.Slash.sprite = slashFrames[0];
			effect.Slash.sortingLayerID = sortingLayer;
			effect.Slash.sortingOrder = sortingOrder;
			effect.Velocity = Up(slashDir).normalized * 8f * num;
		}
		effects.Add(effect);
		Update(0f);
	}

	public void Update(float dt)
	{
		for (int num = effects.Count - 1; num >= 0; num--)
		{
			Effect effect = effects[num];
			effect.T += dt;
			bool flag = true;
			if (effect.Slash != null)
			{
				int num2 = (int)(effect.T / 0.1f);
				flag = num2 >= slashFrames.Count;
				if (!flag)
				{
					effect.Slash.sprite = slashFrames[num2];
					effect.Slash.transform.position += effect.Velocity * dt;
				}
			}
			if (flag)
			{
				if (effect.Slash != null)
				{
					UnityEngine.Object.Destroy(effect.Slash.gameObject);
				}
				effects.RemoveAt(num);
			}
		}
	}

	private static Vector2 Up(Vector2 celeste)
	{
		return new Vector2(celeste.x, 0f - celeste.y);
	}

	private static void Shake(ICameraShake shake, UnityEngine.Object source, bool freeze)
	{
		try
		{
			CameraManagerReference mainCameraShakeManager = GlobalSettings.Camera.MainCameraShakeManager;
			if (mainCameraShakeManager != null)
			{
				mainCameraShakeManager.DoShake(shake, source, freeze, vibrate: false, sendWorldForce: false);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Dash shake: " + ex.Message);
		}
	}
}
