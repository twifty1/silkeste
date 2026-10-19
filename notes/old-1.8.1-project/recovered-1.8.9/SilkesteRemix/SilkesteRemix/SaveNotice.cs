using System;
using System.Collections.Generic;
using MadelineInPharloom;
using MadelineInPharloom.Sheets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilkesteRemix;

public class SaveNotice : MonoBehaviour
{
	public static readonly AssistRow Style = new AssistRow
	{
		Id = "save_notice",
		Section = "Controls",
		Label = "Startup Notice",
		Values = "Celeste,Silksong",
		Default = "Celeste",
		Description = "Celeste: the autosave notice at startup is a snowy night on the mountain with Madeline. Silksong: the original notice."
	};

	private const string Clip = "StartingQuote";

	private static readonly Color Cream = new Color(0.97f, 0.94f, 0.86f);

	private static readonly Color Muted = new Color(0.74f, 0.71f, 0.8f);

	private static readonly Color Gold = new Color(1f, 0.82f, 0.4f);

	private static readonly Color Ink = new Color(0.035f, 0.04f, 0.065f);

	private StartManager start;

	private float alpha;

	private Texture2D sky;

	private Texture2D mountainFar;

	private Texture2D mountainNear;

	private readonly List<Vector4> snow = new List<Vector4>();

	private readonly List<Vector3> stars = new List<Vector3>();

	private bool holding;

	private float heldFor;

	private static Texture2D disc;

	private void Update()
	{
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		string text = SceneManager.GetActiveScene().name;
		if (text == "Pre_Menu_Intro")
		{
			if (start == null)
			{
				start = UnityEngine.Object.FindObjectOfType<StartManager>();
			}
			Animator animator = ((start != null) ? start.startManagerAnimator : null);
			if (!(animator == null))
			{
				AnimatorClipInfo[] currentAnimatorClipInfo = animator.GetCurrentAnimatorClipInfo(0);
				if (currentAnimatorClipInfo.Length != 0 && currentAnimatorClipInfo[0].clip != null && currentAnimatorClipInfo[0].clip.name == "StartingQuote")
				{
					float normalizedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
					alpha = Mathf.Max(alpha, Mathf.Clamp01(normalizedTime / 0.08f));
					holding = true;
				}
				if (holding)
				{
					heldFor = 0f;
				}
			}
			return;
		}
		start = null;
		if (!holding)
		{
			alpha = 0f;
			return;
		}
		heldFor += unscaledDeltaTime;
		CelesteMenus celesteMenus = (CelesteMenus.Showing ? UnityEngine.Object.FindObjectOfType<CelesteMenus>() : null);
		if ((celesteMenus != null && celesteMenus.kind == "title" && celesteMenus.alpha >= 0.99f && !celesteMenus.holding) || heldFor > 20f || text != "Menu_Title")
		{
			alpha = Mathf.MoveTowards(alpha, 0f, unscaledDeltaTime / 0.6f);
			if (alpha <= 0f)
			{
				holding = false;
			}
		}
		else
		{
			alpha = 1f;
		}
	}

	private void OnGUI()
	{
		GUI.depth = -8;
		if (alpha <= 0.001f || Event.current.type != EventType.Repaint)
		{
			return;
		}
		try
		{
			if (AssistMode.Value(Style) != "Celeste")
			{
				return;
			}
		}
		catch (Exception)
		{
			return;
		}
		GuiArt art = AssistMode.Art;
		if (art == null)
		{
			return;
		}
		try
		{
			Draw(art, alpha);
		}
		catch (Exception ex2)
		{
			alpha = 0f;
			Plugin.Log.LogWarning("save notice: " + ex2.Message);
		}
	}

	private void Draw(GuiArt art, float a)
	{
		float num = (float)Screen.height / 1080f;
		float num2 = (float)Screen.width / num;
		Matrix4x4 matrix = GUI.matrix;
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num, num, 1f));
		float unscaledTime = Time.unscaledTime;
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		Build(num2);
		GUI.color = new Color(1f, 1f, 1f, a);
		GUI.DrawTexture(new Rect(0f, 0f, num2, 1080f), sky);
		foreach (Vector3 star in stars)
		{
			float num3 = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(unscaledTime * 1.3f + star.z));
			GUI.color = new Color(1f, 1f, 1f, a * num3);
			GUI.DrawTexture(new Rect(star.x * num2, star.y, 3f, 3f), Texture2D.whiteTexture);
		}
		GUI.color = new Color(0.85f, 0.8f, 1f, 0.22f * a);
		GUI.DrawTexture(new Rect(num2 * 0.78f - 160f, 60f, 320f, 320f), CelesteMenus.MenuFx.Glow);
		GUI.color = new Color(0.95f, 0.93f, 1f, 0.9f * a);
		GUI.DrawTexture(new Rect(num2 * 0.78f - 36f, 184f, 72f, 72f), Disc());
		float num4 = unscaledTime * 6f % num2;
		GUI.color = new Color(0.2f, 0.16f, 0.36f, a);
		GUI.DrawTexture(new Rect(0f - num4, 560f, num2, 520f), mountainFar);
		GUI.DrawTexture(new Rect(num2 - num4, 560f, num2, 520f), mountainFar);
		GUI.color = new Color(0.1f, 0.08f, 0.2f, a);
		GUI.DrawTexture(new Rect(0f, 760f, num2, 320f), mountainNear);
		while (snow.Count < 140)
		{
			snow.Add(new Vector4(UnityEngine.Random.value * num2, UnityEngine.Random.value * 1080f, 40f + UnityEngine.Random.value * 90f, 2f + Mathf.Floor(UnityEngine.Random.value * 3f)));
		}
		for (int i = 0; i < snow.Count; i++)
		{
			Vector4 value = snow[i];
			value.x -= value.z * 1.6f * unscaledDeltaTime;
			value.y += value.z * unscaledDeltaTime + Mathf.Sin(unscaledTime * 2f + (float)i) * 10f * unscaledDeltaTime;
			if (value.x < -10f || value.y > 1090f)
			{
				value.x = num2 + UnityEngine.Random.value * 200f;
				value.y = UnityEngine.Random.value * 1080f - 200f;
			}
			snow[i] = value;
			GUI.color = new Color(1f, 1f, 1f, a * (0.35f + value.w * 0.15f));
			GUI.DrawTexture(new Rect(value.x, value.y, value.w, value.w), Texture2D.whiteTexture);
		}
		float num5 = num2 / 2f;
		string line = "AUTOSAVE";
		float num6 = art.Measure(line) * 0.9f;
		float num7 = 330f;
		art.DrawOutlined(line, num5 - num6 / 2f, num7, 0.9f, new Color(Cream.r, Cream.g, Cream.b, a), new Color(Ink.r, Ink.g, Ink.b, a), 3f);
		float num8 = num7 + (float)art.LineHeight * 0.9f / 2f;
		GUI.color = new Color(Cream.r, Cream.g, Cream.b, 0.55f * a);
		GUI.DrawTexture(new Rect(num5 - num6 / 2f - 300f, num8 - 1f, 250f, 2f), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(num5 + num6 / 2f + 50f, num8 - 1f, 250f, 2f), Texture2D.whiteTexture);
		GUI.color = new Color(Gold.r, Gold.g, Gold.b, a);
		Diamond(num5 - num6 / 2f - 28f, num8, 8f);
		Diamond(num5 + num6 / 2f + 28f, num8, 8f);
		Centered(art, "This game saves automatically at certain points.", num5, 470f, 0.5f, Cream, a);
		Centered(art, "Please don't close it while the save icon is showing.", num5, 530f, 0.42f, Muted, a);
		DrawRunner(num5, RidgeTop(mountainNear, num5 / num2, 760f, 320f) + 2f, a, unscaledTime);
		GUI.matrix = matrix;
		GUI.color = Color.white;
	}

	private static float RidgeTop(Texture2D ridge, float u, float y0, float height)
	{
		int x = Mathf.Clamp((int)(u * (float)ridge.width), 0, ridge.width - 1);
		int num = 0;
		for (int num2 = ridge.height - 1; num2 >= 0; num2--)
		{
			if (ridge.GetPixel(x, num2).a > 0.5f)
			{
				num = num2 + 1;
				break;
			}
		}
		return y0 + height * (1f - (float)num / (float)ridge.height);
	}

	private static void Centered(GuiArt art, string s, float cx, float y, float scale, Color c, float a)
	{
		art.DrawOutlined(s, cx - art.Measure(s) * scale / 2f, y, scale, new Color(c.r, c.g, c.b, a), new Color(Ink.r, Ink.g, Ink.b, a * 0.9f), 2f);
	}

	private static void DrawRunner(float x, float feetY, float a, float t)
	{
		if (Plugin.Ready)
		{
			SkinRow skin;
			CelesteAssets celesteAssets = LoadingRunner.DefaultMadeline(out skin);
			if (celesteAssets != null && celesteAssets.Anims.TryGetValue("runSlow", out var value) && value.Frames.Count != 0)
			{
				CelesteFrame frame = value.Frames[(int)(t / Math.Max(0.05f, value.Delay)) % value.Frames.Count];
				LoadingRunner.DrawMadelineAt(celesteAssets, skin, frame, new Vector2(x, feetY), a, running: true);
			}
		}
	}

	private static void Diamond(float cx, float cy, float r)
	{
		Matrix4x4 matrix = GUI.matrix;
		GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy) * ((float)Screen.height / 1080f));
		GUI.DrawTexture(new Rect(cx - r / 1.414f, cy - r / 1.414f, r * 1.414f, r * 1.414f), Texture2D.whiteTexture);
		GUI.matrix = matrix;
	}

	private static Texture2D Disc()
	{
		if (disc != null)
		{
			return disc;
		}
		disc = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp
		};
		for (int i = 0; i < 64; i++)
		{
			for (int j = 0; j < 64; j++)
			{
				float num = (float)j + 0.5f - 32f;
				float num2 = (float)i + 0.5f - 32f;
				disc.SetPixel(j, i, new Color(1f, 1f, 1f, Mathf.Clamp01(32f - Mathf.Sqrt(num * num + num2 * num2))));
			}
		}
		disc.Apply();
		return disc;
	}

	private void Build(float w)
	{
		if (!(sky != null))
		{
			sky = new Texture2D(1, 256, TextureFormat.RGBA32, mipChain: false)
			{
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear
			};
			Color b = new Color(0.06f, 0.04f, 0.14f);
			Color a = new Color(0.3f, 0.2f, 0.45f);
			for (int i = 0; i < 256; i++)
			{
				sky.SetPixel(0, i, Color.Lerp(a, b, (float)i / 255f));
			}
			sky.Apply();
			mountainFar = Ridge(512, 256, 7, 0.35f, 1);
			mountainNear = Ridge(512, 160, 4, 0.55f, 2);
			System.Random random = new System.Random(7);
			for (int j = 0; j < 110; j++)
			{
				stars.Add(new Vector3((float)random.NextDouble(), (float)random.NextDouble() * 620f, (float)random.NextDouble() * 6f));
			}
		}
	}

	private static Texture2D Ridge(int w, int h, int peaks, float rough, int seed)
	{
		System.Random random = new System.Random(seed);
		float[] array = new float[peaks + 1];
		for (int i = 0; i < peaks; i++)
		{
			array[i] = 0.45f + (float)random.NextDouble() * 0.5f;
		}
		array[peaks] = array[0];
		Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Point
		};
		Color[] array2 = new Color[w * h];
		for (int j = 0; j < w; j++)
		{
			float num = (float)j / (float)w * (float)peaks;
			int num2 = (int)num;
			float num3 = num - (float)num2;
			int num4 = Mathf.Clamp((int)((Mathf.Lerp(array[num2], array[num2 + 1], num3) - Mathf.Abs(num3 - 0.5f) * rough) * (float)h), 0, h);
			for (int k = 0; k < h; k++)
			{
				array2[k * w + j] = ((k < num4) ? Color.white : Color.clear);
			}
		}
		texture2D.SetPixels(array2);
		texture2D.Apply();
		return texture2D;
	}
}
