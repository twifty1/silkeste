using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadelineInPharloom;

public static class Particles
{
	private class Puff
	{
		public SpriteRenderer Sr;

		public Vector3 Velocity;

		public float Life;

		public float StartLife;

		public float StartSize;

		public float Spin;

		public bool Feather;
	}

	private static Transform parent;

	private static Material material;

	private static List<Sprite> smoke = new List<Sprite>();

	private static Sprite feather;

	private static readonly List<Puff> live = new List<Puff>();

	private static readonly List<Puff> spare = new List<Puff>();

	private static readonly System.Random rng = new System.Random();

	private static readonly Color FeatherColor = Hex("F2EB6D");

	private static readonly Color FeatherColor2 = Hex("FFF20F");

	private static Color dustTint = Color.white;

	private static float U => 0.1125f;

	public static void Init(Transform root, Material mat, List<Sprite> smokeSprites, Sprite featherSprite)
	{
		parent = root;
		material = mat;
		smoke = smokeSprites;
		feather = featherSprite;
		live.Clear();
		spare.Clear();
	}

	public static void Dust(Vector3 world, float direction, int count, float range = 4f)
	{
		if (!(parent == null) && smoke.Count != 0)
		{
			float num = Math.Abs(Mathf.Cos(direction - MathF.PI / 2f) * range);
			float num2 = Math.Abs(Mathf.Sin(direction - MathF.PI / 2f) * range);
			for (int i = 0; i < count; i++)
			{
				Puff puff = Take(isFeather: false);
				float f = direction - 0.25f + (float)rng.NextDouble() * 0.5f;
				puff.Velocity = new Vector3(Mathf.Cos(f), 0f - Mathf.Sin(f), 0f) * Rand(5f, 15f) * U;
				puff.StartLife = (puff.Life = Rand(0.3f, 0.5f));
				puff.StartSize = 0.59999996f + (float)rng.NextDouble() * 0.2f;
				puff.Spin = 0f;
				puff.Sr.sprite = smoke[rng.Next(smoke.Count)];
				puff.Sr.color = dustTint;
				Place(puff, world + new Vector3(Rand(0f - num, num), 0f - Rand(0f - num2, num2), 0f) * U);
			}
		}
	}

	public static void DustTinted(Vector3 world, float direction, int count, Color tint, float range = 4f)
	{
		dustTint = tint;
		try
		{
			Dust(world, direction, count, range);
		}
		finally
		{
			dustTint = Color.white;
		}
	}

	public static void Feathers(Vector3 world, int count, float range)
	{
		if (!(parent == null) && !(feather == null))
		{
			for (int i = 0; i < count; i++)
			{
				Puff puff = Take(isFeather: true);
				float f = (float)rng.NextDouble() * MathF.PI * 2f;
				puff.Velocity = new Vector3(Mathf.Cos(f), 0f - Mathf.Sin(f), 0f) * Rand(60f, 70f) * U;
				puff.StartLife = (puff.Life = Rand(0.6f, 0.9f));
				puff.StartSize = 1f;
				puff.Spin = Rand(MathF.PI / 6f, 1.3962634f) * ((rng.Next(2) == 0) ? 1f : (-1f));
				puff.Sr.sprite = feather;
				Place(puff, world + new Vector3(Rand(0f - range, range), Rand(0f - range, range), 0f) * U);
			}
		}
	}

	private static Puff Take(bool isFeather)
	{
		Puff puff;
		if (spare.Count > 0)
		{
			puff = spare[spare.Count - 1];
			spare.RemoveAt(spare.Count - 1);
		}
		else
		{
			GameObject gameObject = new GameObject("Particle");
			gameObject.transform.SetParent(parent, worldPositionStays: false);
			SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
			spriteRenderer.sharedMaterial = material;
			puff = new Puff
			{
				Sr = spriteRenderer
			};
		}
		puff.Feather = isFeather;
		return puff;
	}

	private static void Place(Puff p, Vector3 at)
	{
		p.Sr.transform.position = at;
		p.Sr.transform.rotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 360f);
		p.Sr.gameObject.SetActive(value: true);
		Look(p);
		live.Add(p);
	}

	public static void Update(int sortingLayer, int sortingOrder, float z)
	{
		float num = ((Time.timeScale <= 0f) ? 0f : Time.unscaledDeltaTime);
		for (int num2 = live.Count - 1; num2 >= 0; num2--)
		{
			Puff puff = live[num2];
			if (puff.Sr == null)
			{
				live.RemoveAt(num2);
			}
			else
			{
				puff.Life -= num;
				if (puff.Life <= 0f)
				{
					puff.Sr.gameObject.SetActive(value: false);
					live.RemoveAt(num2);
					spare.Add(puff);
				}
				else
				{
					Vector3 vector = puff.Sr.transform.position + puff.Velocity * num;
					if (puff.Feather)
					{
						puff.Velocity *= Mathf.Pow(0.1f, num);
					}
					else
					{
						puff.Velocity += new Vector3(0f, -4f * U, 0f) * num;
					}
					puff.Sr.transform.position = new Vector3(vector.x, vector.y, z);
					if (puff.Spin != 0f)
					{
						puff.Sr.transform.Rotate(0f, 0f, (0f - puff.Spin) * 57.29578f * num);
					}
					puff.Sr.sortingLayerID = sortingLayer;
					puff.Sr.sortingOrder = sortingOrder;
					Look(puff);
				}
			}
		}
	}

	private static void Look(Puff p)
	{
		float num = Mathf.Clamp01(p.Life / p.StartLife);
		if (p.Feather)
		{
			Color color = ((p.Life % 0.2f > 0.1f) ? FeatherColor : FeatherColor2);
			color.a = Math.Min(1f, num / 0.25f);
			p.Sr.color = color;
			p.Sr.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
		}
		else
		{
			float num2 = p.StartSize * (1f - (1f - num) * (1f - num) * (1f - num)) * 0.9f;
			p.Sr.transform.localScale = new Vector3(num2, num2, 1f);
		}
	}

	private static float Rand(float a, float b)
	{
		return a + (float)rng.NextDouble() * (b - a);
	}

	private static Color Hex(string hex)
	{
		ColorUtility.TryParseHtmlString("#" + hex, out var color);
		return color;
	}
}
