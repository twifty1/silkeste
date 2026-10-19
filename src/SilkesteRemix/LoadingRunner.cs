using System;
using System.Collections.Generic;
using HarmonyLib;
using MadelineInPharloom;
using MadelineInPharloom.Sheets;
using UnityEngine;
using UnityEngine.UI;

namespace SilkesteRemix;

public class LoadingRunner : MonoBehaviour
{
	public static readonly AssistRow Loading = new AssistRow
	{
		Id = "loading_runner",
		Section = "Video",
		Label = "Loading Screens",
		Values = "Madeline,Hornet",
		Default = "Madeline",
		Description = "Madeline: she runs and dashes across the bottom of loading screens. Hornet: Silksong's own corner icon."
	};

	public static readonly AssistRow TitleNap = new AssistRow
	{
		Id = "title_nap",
		Section = "Video",
		Label = "Title Screen Madeline",
		Values = "On,Off",
		Default = "On",
		Description = "Madeline naps in the corner of the title screen."
	};

	private const float Scale = 4f;

	private const float Speed = 300f;

	private const float DashEvery = 2.6f;

	private const float DashTime = 0.22f;

	private const float DashMult = 3.2f;

	private const float Baseline = 1016f;

	private static readonly Color Cream = new Color(0.97f, 0.94f, 0.86f);

	private static readonly Color Ink = new Color(0.035f, 0.04f, 0.065f);

	private static float loadAlpha;

	private static float loadSeen;

	private static float runX = -80f;

	private static float dashClock;

	private static float dashLeft;

	private static float flash;

	private static readonly List<Vector3> ghosts = new List<Vector3>();

	private static readonly List<Vector3> dust = new List<Vector3>();

	private static float dustIn;

	private static bool loggedError;

	private static CelesteMenus menus;

	private static Plugin plugin;

	private static readonly List<Vector3> embers = new List<Vector3>();

	private static float emberIn;

	private static bool Ready
	{
		get
		{
			if (Plugin.Ready && Plugin.PreviewAssets != null)
			{
				return AssistMode.Art != null;
			}
			return false;
		}
	}

	private static bool On(AssistRow r, string v)
	{
		try
		{
			return AssistMode.Value(r) == v;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public static void Patch(Harmony h)
	{
		h.Patch(AccessTools.Method(typeof(LoadingSpinner), "Update"), null, new HarmonyMethod(typeof(LoadingRunner), "SpinnerPost"));
		h.Patch(AccessTools.Method(typeof(StandaloneLoadingSpinner), "LateUpdate"), null, new HarmonyMethod(typeof(LoadingRunner), "StandalonePost"));
	}

	private static void Take(Image image, float fade)
	{
		if (Ready && On(Loading, "Madeline"))
		{
			if (fade > 0f)
			{
				loadAlpha = Math.Max(loadAlpha, fade);
				loadSeen = Time.unscaledTime;
			}
			if (image != null && image.enabled)
			{
				image.color = new Color(1f, 1f, 1f, 0f);
			}
		}
	}

	private static void SpinnerPost(LoadingSpinner __instance)
	{
		Take(__instance.image, __instance.fadeFactor);
	}

	private static void StandalonePost(StandaloneLoadingSpinner __instance)
	{
		Take(__instance.image, __instance.fadeFactor);
	}

	private void Update()
	{
		if (Time.unscaledTime - loadSeen > 0.1f)
		{
			loadAlpha = Mathf.MoveTowards(loadAlpha, 0f, Time.unscaledDeltaTime * 4f);
		}
	}

	private void OnGUI()
	{
		GUI.depth = -7;
		if (Event.current.type != EventType.Repaint)
		{
			return;
		}
		try
		{
			if (loadAlpha > 0.01f)
			{
				DrawRunner(loadAlpha);
			}
			else
			{
				DrawTitleNap();
			}
		}
		catch (Exception ex)
		{
			if (!loggedError)
			{
				loggedError = true;
				Plugin.Log.LogWarning("loading runner: " + ex.Message);
			}
		}
	}

	private static CelesteAssets Assets(out SkinRow skin)
	{
		skin = Skins.Madeline;
		if (plugin == null)
		{
			plugin = UnityEngine.Object.FindObjectOfType<Plugin>();
		}
		if (plugin == null)
		{
			return null;
		}
		if (!plugin.skins.TryGetValue(skin.Id, out var value))
		{
			plugin.EnsureSkin(skin);
			plugin.skins.TryGetValue(skin.Id, out value);
		}
		return value;
	}

	public static CelesteAssets DefaultMadeline(out SkinRow skin)
	{
		return Assets(out skin);
	}

	public static void DrawMadelineAt(CelesteAssets assets, SkinRow skin, CelesteFrame frame, Vector2 feet, float a, bool running)
	{
		DrawMadeline(assets, skin, frame, feet, Hex(skin.HairNormal), a, running);
	}

	private static Color Hex(string hex)
	{
		if (!ColorUtility.TryParseHtmlString("#" + hex, out var color))
		{
			return Color.white;
		}
		return color;
	}

	private static void Begin(out Matrix4x4 old, out float width)
	{
		float num = (float)Screen.height / 1080f;
		width = (float)Screen.width / num;
		old = GUI.matrix;
		GUI.depth = -7;
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num, num, 1f));
	}

	private static void DrawRunner(float a)
	{
		SkinRow skin;
		CelesteAssets celesteAssets = Assets(out skin);
		if (celesteAssets == null)
		{
			return;
		}
		Begin(out var old, out var width);
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		dashClock += unscaledDeltaTime;
		if (dashLeft <= 0f && dashClock >= 2.6f)
		{
			dashClock = 0f;
			dashLeft = 0.22f;
		}
		bool flag = dashLeft > 0f;
		if (flag)
		{
			dashLeft -= unscaledDeltaTime;
			if (dashLeft <= 0f)
			{
				flash = 0.12f;
			}
		}
		if (flash > 0f)
		{
			flash -= unscaledDeltaTime;
		}
		runX += 300f * (flag ? 3.2f : 1f) * unscaledDeltaTime;
		if (runX > width + 80f)
		{
			runX = -80f;
		}
		CelesteAnim celesteAnim = Anim(celesteAssets, flag ? "dash" : "runFast");
		if (celesteAnim == null)
		{
			GUI.matrix = old;
			return;
		}
		CelesteFrame celesteFrame = celesteAnim.Frames[(int)(Time.unscaledTime / Math.Max(0.05f, celesteAnim.Delay)) % celesteAnim.Frames.Count];
		Vector2 feet = new Vector2(runX, 1016f);
		for (int num = ghosts.Count - 1; num >= 0; num--)
		{
			Vector3 value = ghosts[num];
			value.z += unscaledDeltaTime;
			ghosts[num] = value;
			if (value.z > 0.25f)
			{
				ghosts.RemoveAt(num);
			}
		}
		if (flag)
		{
			ghosts.Add(new Vector3(feet.x, feet.y, 0f));
		}
		Color color = Hex(skin.HairUsed);
		foreach (Vector3 ghost in ghosts)
		{
			Color c = color;
			c.a = a * 0.5f * (1f - ghost.z / 0.25f);
			AssistMode.DrawSprite(celesteFrame.Sprite, new Vector2(ghost.x, ghost.y), 4f, c);
		}
		if ((dustIn -= unscaledDeltaTime) <= 0f && !flag)
		{
			dustIn = 0.16f;
			dust.Add(new Vector3(feet.x - 16f, feet.y - 2f, 0f));
		}
		for (int num2 = dust.Count - 1; num2 >= 0; num2--)
		{
			Vector3 value2 = dust[num2];
			value2.z += unscaledDeltaTime;
			value2.y -= unscaledDeltaTime * 18f;
			dust[num2] = value2;
			if (value2.z > 0.4f || celesteAssets.Smoke.Count == 0)
			{
				dust.RemoveAt(num2);
			}
			else
			{
				AssistMode.DrawSprite(celesteAssets.Smoke[Math.Min(celesteAssets.Smoke.Count - 1, (int)(value2.z / 0.4f * (float)celesteAssets.Smoke.Count))], new Vector2(value2.x, value2.y), 2.4f, new Color(1f, 1f, 1f, a * 0.7f * (1f - value2.z / 0.4f)));
			}
		}
		Color hair = (flag ? color : ((flash > 0f) ? Color.white : Hex(skin.HairNormal)));
		DrawMadeline(celesteAssets, skin, celesteFrame, feet, hair, a, running: true);
		GuiArt art = AssistMode.Art;
		string text = new string('.', 1 + (int)(Time.unscaledTime * 2f) % 3);
		float x = width - 60f - art.Measure("Loading...") * 0.42f;
		art.DrawOutlined("Loading" + text, x, 1016f - (float)art.LineHeight * 0.42f - 8f, 0.42f, new Color(Cream.r, Cream.g, Cream.b, a * 0.9f), new Color(Ink.r, Ink.g, Ink.b, a * 0.9f), 2f);
		GUI.matrix = old;
		GUI.color = Color.white;
	}

	private static CelesteAnim Anim(CelesteAssets assets, string id)
	{
		if (assets.Anims.TryGetValue(id, out var value) && value.Frames.Count > 0)
		{
			return value;
		}
		if (!assets.Anims.TryGetValue(Animations.Idle.CelesteAnim, out value) || value.Frames.Count <= 0)
		{
			return null;
		}
		return value;
	}

	private static void DrawMadeline(CelesteAssets assets, SkinRow skin, CelesteFrame frame, Vector2 feet, Color hair, float a, bool running)
	{
		hair.a = a;
		Color c = new Color(0f, 0f, 0f, a);
		Color c2 = Hex(skin.BodyColor);
		c2.a *= a;
		if (frame.HasHair && !MadelineView.Masked(skin.Style) && assets.HairNode != null && assets.Bangs.Count > 0)
		{
			Vector2[] array = new Vector2[4]
			{
				feet + new Vector2(frame.HairOffset.x, -9f + frame.HairOffset.y) * 4f,
				default(Vector2),
				default(Vector2),
				default(Vector2)
			};
			for (int i = 1; i < 4; i++)
			{
				array[i] = array[i - 1] + new Vector2(running ? (-1.6f) : (-0.5f), running ? (0.4f + Mathf.Sin(Time.unscaledTime * 10f + (float)i) * 0.35f) : 2f) * 4f;
			}
			for (int j = 0; j < 2; j++)
			{
				for (int num = 3; num >= 0; num--)
				{
					Sprite sp = ((num == 0) ? assets.Bangs[Mathf.Clamp(frame.HairFrame, 0, assets.Bangs.Count - 1)] : assets.HairNode);
					float scale = 4f * ((num == 0) ? 1f : (0.25f + (1f - (float)num / 4f) * 0.75f));
					if (j == 0)
					{
						Vector2[] array2 = new Vector2[4]
						{
							Vector2.left,
							Vector2.right,
							Vector2.up,
							Vector2.down
						};
						foreach (Vector2 vector in array2)
						{
							AssistMode.DrawSprite(sp, array[num] + vector * 4f, scale, c);
						}
					}
					else
					{
						AssistMode.DrawSprite(sp, array[num], scale, hair);
					}
				}
			}
		}
		AssistMode.DrawSprite(frame.Sprite, feet, 4f, c2);
	}

	private static void DrawTitleNap()
	{
		if (!Ready || !On(TitleNap, "On"))
		{
			return;
		}
		if (menus == null)
		{
			menus = UnityEngine.Object.FindObjectOfType<CelesteMenus>();
		}
		GameManager silentInstance = GameManager.SilentInstance;
		if (silentInstance == null || !silentInstance.IsMenuScene() || menus == null || !CelesteMenus.Showing || menus.kind != "title" || menus.holding || menus.alpha < 0.99f || CelesteFileSelect.Showing || CelesteMenus.OverlayOpen || AssistMode.IsOpen || BossCard.RecordsOpen)
		{
			return;
		}
		SkinRow skin;
		CelesteAssets celesteAssets = Assets(out skin);
		if (celesteAssets == null)
		{
			return;
		}
		// they sleep at night; at dawn Madeline is up first, sitting by the fire; by day and at sunset both are awake,
		// Madeline standing about and Theo sitting up (follows the title's sky, user 2026-10-17)
		string light = TitleScene.Name;
		bool madelineUp = light != "night";
		bool theoUp = light == "day" || light == "sunset";
		CelesteAnim celesteAnim;
		if (!madelineUp)
		{
			celesteAnim = Anim(celesteAssets, "asleep") ?? Anim(celesteAssets, "sleep");
		}
		else if (light == "dawn")
		{
			celesteAnim = Anim(celesteAssets, "sitDown") ?? Anim(celesteAssets, "idle");
		}
		else
		{
			// standing by the fire, now and then one of her idle bits
			float cycle = Mathf.Repeat(Time.unscaledTime, 9f);
			string[] idles = new string[3] { "idleA", "idleB", "idleC" };
			string pick = (cycle > 6f) ? idles[(int)(Time.unscaledTime / 9f) % 3] : "idle";
			celesteAnim = Anim(celesteAssets, pick) ?? Anim(celesteAssets, "idle");
		}
		if (celesteAnim == null)
		{
			return;
		}
		Begin(out var old, out var width);
		float unscaledTime = Time.unscaledTime;
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		float num = 1050f;
		float num2 = width - 360f;
		float num3 = num2 - 96f;
		GUI.color = new Color(Ink.r, Ink.g, Ink.b, 0.55f);
		GUI.DrawTexture(new Rect(num3 - 160f, num, 320f, 34f), CelesteMenus.MenuFx.FadeBoth);
		float num4 = 0.85f + 0.1f * Mathf.Sin(unscaledTime * 13f) + 0.05f * Mathf.Sin(unscaledTime * 29f);
		GUI.color = new Color(1f, 0.55f, 0.18f, 0.32f * num4);
		float num5 = 280f * num4;
		GUI.DrawTexture(new Rect(num3 - num5 / 2f, num - num5 / 2f - 24f, num5, num5), CelesteMenus.MenuFx.Glow);
		if (TitleScene.Name == "night")
		{
			// her sleeping bag, only while she's asleep in it
			Px(num2 - 48f, num - 4f, 96f, 4f, new Color(0.45f, 0.16f, 0.18f));
			Px(num2 - 48f, num - 8f, 24f, 4f, new Color(0.62f, 0.26f, 0.26f));
		}
		Px(num3 - 32f, num - 8f, 64f, 8f, new Color(0.36f, 0.2f, 0.12f));
		Px(num3 - 24f, num - 12f, 48f, 4f, new Color(0.48f, 0.28f, 0.16f));
		Px(num3 - 32f, num - 8f, 8f, 8f, new Color(0.62f, 0.42f, 0.26f));
		Px(num3 + 24f, num - 8f, 8f, 8f, new Color(0.62f, 0.42f, 0.26f));
		Flame(num3, num - 12f, 7f, 9f, new Color(0.93f, 0.33f, 0.12f), unscaledTime, 0f);
		Flame(num3, num - 12f, 5f, 7f, new Color(1f, 0.6f, 0.15f), unscaledTime, 1.7f);
		Flame(num3, num - 12f, 3f, 4f, new Color(1f, 0.9f, 0.45f), unscaledTime, 3.1f);
		if ((emberIn -= unscaledDeltaTime) <= 0f)
		{
			emberIn = 0.18f + UnityEngine.Random.value * 0.2f;
			embers.Add(new Vector3(num3 + (UnityEngine.Random.value - 0.5f) * 6f * 4f, num - 32f, 0f));
		}
		for (int num6 = embers.Count - 1; num6 >= 0; num6--)
		{
			Vector3 value = embers[num6];
			value.z += unscaledDeltaTime;
			value.y -= unscaledDeltaTime * 40f;
			value.x += Mathf.Sin(value.z * 4f + (float)num6) * unscaledDeltaTime * 14f;
			embers[num6] = value;
			if (value.z > 1.6f)
			{
				embers.RemoveAt(num6);
			}
			else
			{
				Px(Mathf.Round(value.x / 4f) * 4f, Mathf.Round(value.y / 4f) * 4f, 4f, 4f, new Color(1f, 0.7f, 0.3f, 1f - value.z / 1.6f));
			}
		}
		GuiArt art = AssistMode.Art;
		if (TheoArt.Ready())
		{
			Vector2 pivotAt = new Vector2(num3 - 80f, num);
			Sprite sprite = TheoArt.Frame(theoUp ? TheoArt.SitFrames[0] : TheoArt.DozeFrames[TheoArt.DozeFrames.Length - 1]);
			if (sprite != null)
			{
				AssistMode.DrawSprite(sprite, pivotAt, 4f, Color.white);
				if (!theoUp)
				{
					Zs(art, pivotAt.x - 10f, pivotAt.y - 100f, -1f, unscaledTime + 0.5f);
				}
			}
		}
		int fi = (light == "dawn") ? (celesteAnim.Frames.Count - 1) : ((int)(unscaledTime / Math.Max(0.05f, celesteAnim.Delay)) % celesteAnim.Frames.Count);   // sitting stays sat
		CelesteFrame frame = celesteAnim.Frames[fi];
		Vector2 vector = new Vector2(num2, num - 4f);
		Matrix4x4 matrix = GUI.matrix;
		GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), vector * ((float)Screen.height / 1080f));
		DrawMadeline(celesteAssets, skin, frame, vector, Hex(skin.HairNormal), 1f, running: false);
		GUI.matrix = matrix;
		if (!madelineUp)
		{
			Zs(art, vector.x - 30f, vector.y - 60f, -1f, unscaledTime);
		}
		GUI.matrix = old;
		GUI.color = Color.white;
	}

	private static void Zs(GuiArt art, float x, float y, float dir, float t)
	{
		for (int i = 0; i < 3; i++)
		{
			float num = (t * 0.35f + (float)i / 3f) % 1f;
			Color c = new Color(Cream.r, Cream.g, Cream.b, Mathf.Sin(num * MathF.PI) * 0.9f);
			art.DrawOutlined("z", x + dir * num * 40f + Mathf.Sin(num * 6f) * 8f, y - num * 90f, 0.4f + num * 0.3f, c, new Color(Ink.r, Ink.g, Ink.b, c.a), 2f);
		}
	}

	private static void Px(float x, float y, float w, float h, Color c)
	{
		GUI.color = c;
		GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
	}

	private static void Flame(float cx, float baseY, float halfW, float height, Color c, float t, float phase)
	{
		for (int i = -(int)halfW; i <= (int)halfW; i++)
		{
			float num = 1f - (float)Mathf.Abs(i) / (halfW + 1f);
			float num2 = Mathf.Round(height * num * (0.75f + 0.25f * Mathf.Sin(t * 9f + (float)i * 1.3f + phase)));
			if (!(num2 <= 0f))
			{
				Px(cx + (float)i * 4f - 2f, baseY - num2 * 4f, 4f, num2 * 4f, c);
			}
		}
	}
}
