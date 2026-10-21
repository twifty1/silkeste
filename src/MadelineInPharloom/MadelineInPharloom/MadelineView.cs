using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

public partial class MadelineView
{
	private static readonly AccessTools.FieldRef<HeroController, MeshRenderer> HeroRenderer = AccessTools.FieldRefAccess<HeroController, MeshRenderer>("renderer");

	private static readonly AccessTools.FieldRef<HeroController, Collider2D> Col = AccessTools.FieldRefAccess<HeroController, Collider2D>("col2d");

	private static readonly AccessTools.FieldRef<HeroController, Rigidbody2D> Rb = AccessTools.FieldRefAccess<HeroController, Rigidbody2D>("rb2d");

	private CelesteAssets assets;

	private Color Normal;

	private Color Used;

	private Color TwoDashes;

	private readonly MadelineController ctrl;

	private readonly GameObject root;

	private readonly SpriteRenderer body;

	private readonly SpriteRenderer[] hair = new SpriteRenderer[8];

	private readonly SpriteRenderer[,] hairOutline = new SpriteRenderer[8, 4];

	private readonly Vector2[] nodes = new Vector2[8];

	private int hairN = 4;

	private readonly List<KeyValuePair<SpriteRenderer, float>> trails = new List<KeyValuePair<SpriteRenderer, float>>();

	private readonly Stack<SpriteRenderer> trailPool = new Stack<SpriteRenderer>();

	private readonly Material material;

	private CelesteAnim anim;

	private int frameIndex;

	private float frameTimer;

	private float wave;

	private Color hairColor;

	private bool fastJump;

	private bool started;

	private static readonly Color Flash = Hex("FFFFFF");

	private static readonly Color Tired = Hex("FF0000");

	private static readonly Vector2[] OutlineOffsets = new Vector2[4]
	{
		new Vector2(-1f, 0f),
		new Vector2(1f, 0f),
		new Vector2(0f, -1f),
		new Vector2(0f, 1f)
	};

	private SkinRow skin;

	private readonly DashFx dashFx;

	private int lastSortingLayer;

	private int lastSortingOrder;

	private float boostT;

	private SkinRow colorSkin;

	private bool hasColorSkin;

	private int hairVersion = -1;

	private readonly SpriteRenderer goldBerry;

	private Vector3 goldPos;

	private float goldAppear;

	private float goldFrameTime;

	private float goldFlyT = -1f;

	private float goldFlyVel;

	private bool goldShown;

	private readonly SpriteRenderer picoMask;

	private readonly SpriteRenderer[] picoCircles = new SpriteRenderer[5];

	private readonly Vector2[] picoNodes = new Vector2[5];

	private bool picoStarted;

	private float picoRun;

	private readonly SpriteRenderer catEars;

	private readonly SpriteRenderer catEarsInner;

	private bool earsSetting;

	public static Sprite EarSprite;

	public static Sprite EarInnerSprite;

	public static Sprite TailSprite;

	public static Sprite TailOutlineSprite;

	public static Sprite MaskSprite;

	public const int TailLength = 6;

	private readonly SpriteRenderer[] catTail = new SpriteRenderer[6];

	private readonly SpriteRenderer[] catTailOutline = new SpriteRenderer[6];

	private Sprite earSprite;

	private Sprite earInnerSprite;

	private Sprite tailSprite;

	private Sprite tailOutlineSprite;

	private static readonly string[] EarArt = new string[5] { ".O.......O.", "OHO.....OHO", "OPHO...OHPO", "OPHHO.OHHPO", "OHHHHHHHHHO" };

	private readonly SpriteRenderer knightMask;

	private readonly SpriteRenderer knightEyes;

	private bool atBenchNow;

	private float soulT;

	private Sprite maskSprite;

	private Sprite eyesSprite;

	private Sprite hornetMaskSprite;

	private Sprite needleSprite;

	private SpriteRenderer needle;

	public static Sprite HornetMaskSprite;

	private static readonly string[] HornetMaskArt = new string[18]
	{
		"....OOOOO...........", ".O...OWWWOO.........", ".OWO....OWWWOO......", ".OWWO...OWWWWO......", ".OWWO....OWWWWOO....", "..OWWOO....OWWWO....", "...OWWWWO...OWWWO...", "...OWWWWO...WWWWO...", "....OWWWWO..OWWWWO..", ".....OWWWWOOWWWWWWO.",
		"....OWWWWWWWWWWWWWO.", ".....OWWWWWWWWWWWWWO", ".....OWWWWWWWWWWWWWO", "......OOWWWWOOWWOOWO", ".......OOWWOOOOWOOWO", "........OWWWOOOWOWWO", ".........OWWOOWWWWO.", ".........OOOOOOOO..."
	};

	private static readonly string[] NeedleArt = new string[13]
	{
		"...........OOO", "..........ON.O", ".........ONOO.", "........ONO...", ".......ONO....", "......ONO.....", ".....ONO......", "....ONO.......", "...ONO........", "..ONO.........",
		".ONO..........", "ONO...........", "OO............"
	};

	private static readonly string[] MaskArt = new string[13]
	{
		"..OO.....OO..", ".OWWO...OWWO.", "OWWO.....OWWO", "OWO.......OWO", "OWO.OOOOO.OWO", "OWWOWWWWWOWWO", ".OWWWWWWWWWO.", ".OWWWOWWWOWO.", ".OWWOOOWOOOO.", ".OWWOOOWOOOO.",
		".OWWWOWWWOWO.", "..OWWWWWWWO..", "...OOOOOOO..."
	};

	private readonly Dictionary<Sprite, Vector2> opaqueTop = new Dictionary<Sprite, Vector2>();

	private readonly SpriteRenderer[] focusDots = new SpriteRenderer[14];

	private SpriteRenderer focusGlow;

	private SpriteRenderer focusBody;

	private Material silhouette;

	private Sprite glowSprite;

	private float focusT;

	private float focusFlash;

	private float idleTimer;

	private static readonly System.Random idleRng = new System.Random();

	private static readonly Color StarFly = Hex("FFD65C");

	private SpriteRenderer starWhite;

	private bool binding;

	private tk2dSpriteAnimator hornetAnimator;

	private float wallOffset;

	private SpriteRenderer glider;

	private float gliderT;

	private float gliderRot;

	private Vector2 gliderScale = Vector2.one;

	private bool wasAtBench;

	private bool waking;

	private static readonly Dictionary<string, HashSet<int>> footstepFrames = ParseFrames("runSlow:0,6;runFast:0,6;push:8,15;flip:4");

	private static readonly Dictionary<string, HashSet<int>> handholdFrames = ParseFrames("climbup:5");

	private bool dying;

	private bool deathSounded;

	private float deathT;

	private Vector3 deathStart;

	private int deathDir;

	private int deathFacing;

	private Color deathColor;

	private SpriteRenderer[] burst;

	private SpriteRenderer[] burstOutline;

	private bool respawning;

	private float respawnT;

	private bool wallSliding;

	private bool wasGrounded;

	private float lastVerticalSpeed;

	private readonly List<KeyValuePair<SpriteRenderer, float>> crystals = new List<KeyValuePair<SpriteRenderer, float>>();

	private int lastHealth = -1;

	private Vector3 lastHeroPos;

	private bool IsPico
	{
		get
		{
			if (skin.Style == "pico8")
			{
				return assets.PicoBody[1] != null;
			}
			return false;
		}
	}

	public string CurrentAnimId
	{
		get
		{
			if (anim == null)
			{
				return "none";
			}
			return anim.Id + "#" + frameIndex;
		}
	}

	public MadelineView(CelesteAssets assets, SkinRow skin, MadelineController ctrl)
	{
		this.skin = skin;
		SetColors(skin);
		this.assets = assets;
		this.ctrl = ctrl;
		material = new Material(Shader.Find("Sprites/Default"));
		root = new GameObject("Madeline");
		UnityEngine.Object.DontDestroyOnLoad(root);
		for (int i = 0; i < hair.Length; i++)
		{
			for (int j = 0; j < 4; j++)
			{
				hairOutline[i, j] = MakeRenderer("HairOutline" + i + "_" + j, -2);
			}
			hair[i] = MakeRenderer("Hair" + i, -1);
		}
		body = MakeRenderer("Body", 0);
		bakedHair = MakeRenderer("BakedHair", 0);
		bakedHair.enabled = false;
		glider = MakeRenderer("Glider", 0);
		glider.enabled = false;
		starWhite = MakeRenderer("StarFlyWhite", 0);
		starWhite.enabled = false;
		goldBerry = MakeRenderer("GoldenBerry", 0);
		goldBerry.enabled = false;
		BossCard.GoldenLost += OnGoldenLost;
		catEars = MakeRenderer("CatEars", 0);
		catEarsInner = MakeRenderer("CatEarsInner", 0);
		knightMask = MakeRenderer("KnightMask", 0);
		knightEyes = MakeRenderer("KnightEyes", 0);
		needle = MakeRenderer("HornetNeedle", 0);
		for (int k = 0; k < catTail.Length; k++)
		{
			catTailOutline[k] = MakeRenderer("CatTailOutline" + k, -3);
			catTail[k] = MakeRenderer("CatTail" + k, -2);
		}
		picoMask = MakeRenderer("PicoHair", 0);
		for (int l = 0; l < picoCircles.Length; l++)
		{
			picoCircles[l] = MakeRenderer("PicoHairCircle" + l, -1);
		}
		ShowPico(on: false);
		hairColor = Normal;
		try
		{
			BuildLookSprites();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("look sprites: " + ex.Message);
		}
		dashFx = new DashFx(root.transform, material, assets.Slash);
		Particles.Init(root.transform, material, assets.Smoke, assets.Feather);
		ctrl.Dashed += SpawnTrail;
		ctrl.SkillUsed += OnSkillUsed;
		ctrl.DashBegan += OnDashBegan;
		ctrl.DashLaunched += OnDashLaunched;
		ctrl.GlideStarted += OnGlideStarted;
		Play(Animations.Idle.CelesteAnim);
	}

	private void OnDashBegan()
	{
		dashFx.Freeze(root);
	}

	private void OnSkillUsed(string tool)
	{
		if (root.activeSelf && !(body == null))
		{
			Vector3 world = body.transform.position + new Vector3(0f, 0.9f, 0f);
			Color tint = ((tool == "Silk Bomb") ? new Color(1f, 0.25f, 0.25f, 0.95f) : new Color(1f, 1f, 1f, 0.9f));
			for (int i = 0; i < 8; i++)
			{
				Particles.DustTinted(world, (float)i * MathF.PI / 4f, 2, tint, 3f);
			}
			if (tool == "Silk Bomb")
			{
				boostT = 0.6f;
			}
		}
	}

	private void OnDashLaunched(Vector2 shakeDir, Vector2 slashDir)
	{
		if (root.activeSelf)
		{
			Vector3 center = body.transform.position + new Vector3(0f, 99f / 160f, 0f);
			dashFx.Launch(root, center, shakeDir, slashDir, lastSortingLayer, lastSortingOrder);
		}
	}

	private void SetColors(SkinRow skin)
	{
		colorSkin = skin;
		hasColorSkin = true;
		hairVersion = AssistMode.HairVersion;
		float glowCompensation = skin.GlowCompensation;
		Normal = UnderGlow(Hex(AssistMode.HairHex(AssistOptions.HairOne, skin.HairNormal)), glowCompensation);
		Used = UnderGlow(Hex(AssistMode.HairHex(AssistOptions.HairZero, skin.HairUsed)), glowCompensation);
		TwoDashes = UnderGlow(Hex(AssistMode.HairHex(AssistOptions.HairTwo, skin.HairTwoDashes)), glowCompensation);
	}

	private static Color UnderGlow(Color c, float k)
	{
		Func<float, float> func = (float v) => Mathf.Clamp01(1f - (1f - v) / (1f - k));
		return new Color(func(c.r), func(c.g), func(c.b), c.a);
	}

	public void SetSkin(CelesteAssets next, SkinRow skin)
	{
		string id = ((anim != null) ? anim.Id : Animations.Idle.CelesteAnim);
		assets = next;
		this.skin = skin;
		SetColors(skin);
		hairColor = Normal;
		anim = null;
		Play(id, restart: true);
		picoStarted = false;
	}

	private void OnGoldenLost()
	{
		if (goldShown)
		{
			goldFlyT = 0f;
			goldFlyVel = 0f;
			Sfx.Play(Sounds.GoldenLaugh.Id);
		}
	}

	private void UpdateGolden(Vector3 feet, int facing, float dt)
	{
		float num = 0.1125f;
		if (goldFlyT >= 0f)
		{
			goldFlyT += dt;
			if (goldFlyT > 0.35f)
			{
				if (goldFlyVel == 0f)
				{
					Sfx.Play(Sounds.GoldenFlyaway.Id);
				}
				goldFlyVel += 500f * dt;
				goldPos.y += goldFlyVel * num * dt;
			}
			goldFrameTime += dt;
			if (assets.GoldWings.Count > 0)
			{
				goldBerry.sprite = assets.GoldWings[(int)(goldFrameTime / 0.08f) % Math.Min(9, assets.GoldWings.Count)];
			}
			float num2 = Mathf.Clamp01(1f - (goldFlyT - 0.35f - 0.6f) / 0.6f);
			goldBerry.color = new Color(1f, 1f, 1f, num2);
			goldBerry.transform.position = goldPos;
			if (num2 <= 0f)
			{
				goldFlyT = -1f;
				goldShown = false;
				goldBerry.enabled = false;
			}
			return;
		}
		int num3;
		if (BossCard.GoldenRun && assets.GoldIdle.Count > 0 && ctrl.Active)
		{
			num3 = ((BossCard.FightTime < 2.8f) ? 1 : 0);
			if (num3 != 0 && !goldShown)
			{
				goldShown = true;
				goldAppear = 0f;
				goldPos = feet + new Vector3(0f, 22f * num, 0f);
			}
		}
		else
		{
			num3 = 0;
		}
		if (num3 == 0 && goldShown)
		{
			goldShown = false;
		}
		goldBerry.enabled = goldShown;
		if (goldShown)
		{
			goldAppear = Math.Min(1f, goldAppear + dt / 0.3f);
			Vector3 b = feet + new Vector3((float)(-facing) * 14f * num, (22f + Mathf.Sin(Time.time * 4f) * 1.5f) * num, -0.00059999997f);
			goldPos = Vector3.Lerp(goldPos, b, 1f - Mathf.Exp(-6f * dt));
			goldFrameTime += dt;
			int num4 = (int)(goldFrameTime / 0.08f) % 32;
			int val = ((num4 < 24) ? (num4 % 6) : (num4 - 24));
			goldBerry.sprite = assets.GoldIdle[Math.Min(val, assets.GoldIdle.Count - 1)];
			goldBerry.transform.position = goldPos;
			float num5 = 0.9f * (0.6f + 0.4f * goldAppear);
			goldBerry.transform.localScale = new Vector3(num5, num5, 1f);
			float num6 = Mathf.Clamp01((BossCard.FightTime - 2.5f) / 0.3f);
			if (num6 > 0f)
			{
				goldPos += new Vector3(0f, 400f * num * dt, 0f);
				goldBerry.transform.position = goldPos;
			}
			goldBerry.color = new Color(1f, 1f, 1f, goldAppear * (1f - num6));
		}
	}

	private void ShowPico(bool on)
	{
		if (picoMask.enabled != on)
		{
			picoMask.enabled = on;
		}
		SpriteRenderer[] array = picoCircles;
		foreach (SpriteRenderer spriteRenderer in array)
		{
			if (spriteRenderer.enabled != on)
			{
				spriteRenderer.enabled = on;
			}
		}
	}

	private int PicoSprite(HeroController h, float dt)
	{
		if (!ctrl.Active)
		{
			if (!h.playerData.atBench && (anim == null || (!(anim.Id == "sleep") && !(anim.Id == "asleep"))))
			{
				return 1;
			}
			return 6;
		}
		if (ctrl.State == 1 || ctrl.WallSlideDir != 0)
		{
			return 5;
		}
		if (!ctrl.OnGround)
		{
			return 3;
		}
		if (ctrl.Ducking)
		{
			return 6;
		}
		if (anim != null && anim.Id == Animations.LookUp.CelesteAnim)
		{
			return 7;
		}
		if (Math.Abs(ctrl.Speed.x) > 1f)
		{
			picoRun += dt * 7.5f;
			return 1 + (int)picoRun % 4;
		}
		picoRun = 0f;
		return 1;
	}

	private void DrawPico(HeroController h, Vector3 feet, int facing, float sx, float sy, float vs, float dt)
	{
		int num = PicoSprite(h, dt);
		body.sprite = assets.PicoBody[num];
		body.transform.localScale = new Vector3((float)facing * sx * vs, sy * vs, 1f);
		float num2 = 0.0004f;
		picoMask.sprite = assets.PicoHair[num];
		picoMask.color = hairColor;
		picoMask.transform.position = feet + new Vector3(0f, 0f, (0f - num2) / 2f);
		picoMask.transform.localScale = body.transform.localScale;
		float num3 = 2f * vs / 8f;
		bool flag = ctrl.Active && (ctrl.Ducking || num == 6);
		Vector2 vector = new Vector2(feet.x + ((facing > 0) ? 0f : (-2f)) * num3 * Math.Abs(sx), feet.y + (8f - (flag ? 4f : 3f)) * num3 * sy);
		if (!picoStarted)
		{
			for (int i = 0; i < picoNodes.Length; i++)
			{
				picoNodes[i] = vector;
			}
			picoStarted = true;
		}
		float num4 = 1f - Mathf.Pow(0.3333333f, dt * 30f);
		for (int j = 0; j < picoNodes.Length; j++)
		{
			Vector2 vector2 = vector + new Vector2(0f, -0.5f * num3);
			picoNodes[j] += (vector2 - picoNodes[j]) * num4;
			SpriteRenderer obj = picoCircles[j];
			obj.sprite = ((j < 2) ? assets.PicoCircle2 : assets.PicoCircle1);
			obj.color = hairColor;
			obj.transform.position = new Vector3(picoNodes[j].x, picoNodes[j].y, feet.z + num2);
			obj.transform.localScale = new Vector3(vs, vs, 1f);
			vector = picoNodes[j];
		}
	}

	private Sprite PixelSprite(string[] art, Func<char, Color> color, Vector2 pivot)
	{
		int length = art[0].Length;
		int num = art.Length;
		Texture2D texture2D = new Texture2D(length, num, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Point,
			wrapMode = TextureWrapMode.Clamp
		};
		for (int i = 0; i < num; i++)
		{
			for (int j = 0; j < length; j++)
			{
				texture2D.SetPixel(j, num - 1 - i, color(art[i][j]));
			}
		}
		texture2D.Apply();
		float pixelsPerUnit = ((assets.HairNode != null) ? assets.HairNode.pixelsPerUnit : 100f);
		return Sprite.Create(texture2D, new Rect(0f, 0f, length, num), pivot, pixelsPerUnit);
	}

	public static bool Masked(string style)
	{
		if (!(style == "knight"))
		{
			return style == "hornet";
		}
		return true;
	}

	public static Sprite MaskFor(string style)
	{
		if (!(style == "hornet"))
		{
			return MaskSprite;
		}
		return HornetMaskSprite;
	}

	private void UpdateKnight(CelesteFrame frame, Vector3 feet, int facing, float sx, float vs)
	{
		bool flag = Masked(skin.Style) && root.activeSelf;
		if (Masked(skin.Style))
		{
			for (int i = 0; i < hair.Length; i++)
			{
				hair[i].enabled = false;
				for (int j = 0; j < 4; j++)
				{
					hairOutline[i, j].enabled = false;
				}
			}
		}
		knightMask.enabled = flag && body.enabled && body.sprite != null;
		knightEyes.enabled = false;
		if (!knightMask.enabled)
		{
			needle.enabled = false;
			return;
		}
		BuildLookSprites();
		float num = vs / 8f;
		float num2 = 0.0004f;
		Vector3 localScale = new Vector3((float)facing * Math.Abs(sx) * vs, vs, 1f);
		Vector3 vector = ((frame.HasHair && !atBenchNow) ? new Vector3(nodes[0].x + (float)facing * 0.5f * num, nodes[0].y + 2f * num, feet.z - num2 * 0.5f) : new Vector3(feet.x + (float)facing * 0.5f * num, feet.y + 12f * num, feet.z - num2 * 0.5f));
		if (skin.Style == "knight" && (ctrl.Dashes == 0 || ctrl.DashLocked) && ctrl.Active && (soulT -= Time.deltaTime) <= 0f)
		{
			soulT = 0.12f;
			Particles.DustTinted(feet + new Vector3(UnityEngine.Random.Range(-4f, 4f) * num, UnityEngine.Random.Range(2f, 12f) * num, 0f), -MathF.PI / 2f, 1, new Color(1f, 1f, 1f, 0.75f), 1f);
		}
		needle.enabled = skin.Style == "hornet" && !atBenchNow && needleSprite != null;
		if (needle.enabled)
		{
			needle.sprite = needleSprite;
			needle.color = Color.white;
			needle.transform.position = new Vector3(feet.x - (float)facing * 1f * num, feet.y + 9f * num, feet.z + num2 * 3f);
			needle.transform.localScale = new Vector3((float)(-facing) * Math.Abs(sx) * vs, vs, 1f);
		}
		knightMask.sprite = ((skin.Style == "hornet") ? hornetMaskSprite : maskSprite);
		knightMask.color = Color.white;
		knightMask.transform.position = ((skin.Style == "hornet") ? (vector + new Vector3(0f, -8f * num, 0f)) : vector);
		knightMask.transform.localScale = localScale;
		knightEyes.sprite = eyesSprite;
		knightEyes.color = hairColor;
		knightEyes.transform.position = vector - new Vector3(0f, 0f, num2 * 0.1f);
		knightEyes.transform.localScale = localScale;
	}

	private Vector3 OpaqueTop(SpriteRenderer sr)
	{
		Sprite sprite = sr.sprite;
		if (!opaqueTop.TryGetValue(sprite, out var value))
		{
			value = new Vector2(0f, sprite.bounds.max.y);
			try
			{
				Rect textureRect = sprite.textureRect;
				Color[] pixels = sprite.texture.GetPixels((int)textureRect.x, (int)textureRect.y, (int)textureRect.width, (int)textureRect.height);
				int num = (int)textureRect.width;
				int num2 = (int)textureRect.height - 1;
				while (num2 >= 0 && value.x == 0f)
				{
					int num3 = -1;
					int num4 = -1;
					for (int i = 0; i < num; i++)
					{
						if (pixels[num2 * num + i].a > 0.5f)
						{
							if (num3 < 0)
							{
								num3 = i;
							}
							num4 = i;
						}
					}
					if (num3 >= 0)
					{
						value = new Vector2(((float)(num3 + num4 + 1) / 2f - sprite.pivot.x) / sprite.pixelsPerUnit + 0.0001f, ((float)(num2 + 1) - sprite.pivot.y) / sprite.pixelsPerUnit);
						break;
					}
					num2--;
				}
			}
			catch (Exception)
			{
			}
			opaqueTop[sprite] = value;
		}
		return sr.transform.TransformPoint(value);
	}

	private void UpdateFocus(Vector3 feet, float dt)
	{
		if (focusGlow == null)
		{
			focusGlow = MakeRenderer("FocusGlow", 0);
			focusBody = MakeRenderer("FocusWhite", 0);
			Shader shader = Shader.Find("GUI/Text Shader");
			silhouette = ((shader != null) ? new Material(shader) : material);
			focusBody.sharedMaterial = silhouette;
			for (int i = 0; i < focusDots.Length; i++)
			{
				focusDots[i] = MakeRenderer("FocusSoul" + i, 0);
			}
			string[] array = new string[17];
			for (int j = 0; j < 17; j++)
			{
				char[] array2 = new char[17];
				for (int k = 0; k < 17; k++)
				{
					float num = k - 8;
					float num2 = j - 8;
					array2[k] = ((num * num + num2 * num2 <= 64f) ? ((num * num + num2 * num2 <= 30f) ? 'C' : 'E') : '.');
				}
				array[j] = new string(array2);
			}
			glowSprite = PixelSprite(array, (char ch) => ch switch
			{
				'E' => new Color(1f, 1f, 1f, 0.5f), 
				'C' => new Color(1f, 1f, 1f, 1f), 
				_ => Color.clear, 
			}, new Vector2(0.5f, 0.5f));
			if (tailSprite == null)
			{
				string[] art = new string[4] { ".HH.", "HHHH", "HHHH", ".HH." };
				tailSprite = PixelSprite(art, (char ch) => (ch != 'H') ? Color.clear : Color.white, new Vector2(0.5f, 0.5f));
			}
		}
		bool flag = binding && Masked(skin.Style) && root.activeSelf;
		float num3 = 0.9f;
		float num4 = num3 / 8f;
		float num5 = 0.0004f;
		Vector3 vector = feet + new Vector3(0f, 8f * num4, (0f - num5) * 2f);
		if (flag)
		{
			focusT += dt;
		}
		if (focusFlash > 0f)
		{
			focusFlash = Mathf.Max(0f, focusFlash - dt / 0.35f);
		}
		for (int num6 = 0; num6 < focusDots.Length; num6++)
		{
			SpriteRenderer spriteRenderer = focusDots[num6];
			spriteRenderer.enabled = flag;
			if (flag)
			{
				float num7 = Mathf.Repeat(focusT * 1.3f + (float)num6 / (float)focusDots.Length, 1f);
				float f = (float)num6 * 2.39996f + focusT * 0.8f;
				float num8 = (1f - num7) * 26f * num4;
				spriteRenderer.sprite = tailSprite;
				spriteRenderer.transform.position = vector + new Vector3(Mathf.Cos(f) * num8, Mathf.Sin(f) * num8, 0f);
				float num9 = num3 * 0.5f * (0.6f + num7 * 0.6f);
				spriteRenderer.transform.localScale = new Vector3(num9, num9, 1f);
				spriteRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Sin(num7 * MathF.PI) * 1.5f));
			}
		}
		float num10 = (flag ? (Mathf.Clamp01(focusT / 0.8f) * 0.75f) : focusFlash);
		focusBody.enabled = num10 > 0.01f && body.enabled && body.sprite != null;
		if (focusBody.enabled)
		{
			focusBody.sprite = body.sprite;
			focusBody.transform.position = body.transform.position - new Vector3(0f, 0f, num5 * 0.3f);
			focusBody.transform.localScale = body.transform.localScale;
			focusBody.color = new Color(1f, 1f, 1f, num10);
		}
		focusGlow.enabled = flag || focusFlash > 0f;
		if (focusGlow.enabled)
		{
			focusGlow.sprite = glowSprite;
			focusGlow.transform.position = vector + new Vector3(0f, 0f, num5);
			float num11 = (flag ? Mathf.Clamp01(focusT / 1f) : (1f + (1f - focusFlash) * 1.2f));
			float num12 = num3 * (0.6f + num11 * 0.9f) * (1f + 0.05f * Mathf.Sin(focusT * 12f));
			focusGlow.transform.localScale = new Vector3(num12, num12, 1f);
			focusGlow.color = new Color(1f, 1f, 1f, flag ? (0.3f + 0.25f * num11) : (0.85f * focusFlash));
		}
	}

	private void BuildLookSprites()
	{
		if (earSprite == null)
		{
			earSprite = PixelSprite(EarArt, (char ch) => ch switch
			{
				'H' => Color.white, 
				'O' => Color.black, 
				_ => Color.clear, 
			}, new Vector2(0.5f, 0f));
			earInnerSprite = PixelSprite(EarArt, (char ch) => (ch != 'P') ? Color.clear : new Color(1f, 0.62f, 0.72f), new Vector2(0.5f, 0f));
			string[] art = new string[4] { ".HH.", "HHHH", "HHHH", ".HH." };
			tailSprite = PixelSprite(art, (char ch) => (ch != 'H') ? Color.clear : Color.white, new Vector2(0.5f, 0.5f));
			string[] art2 = new string[6] { ".OOOO.", "OOOOOO", "OOOOOO", "OOOOOO", "OOOOOO", ".OOOO." };
			tailOutlineSprite = PixelSprite(art2, (char ch) => (ch != 'O') ? Color.clear : Color.black, new Vector2(0.5f, 0.5f));
			EarSprite = earSprite;
			EarInnerSprite = earInnerSprite;
			TailSprite = tailSprite;
			TailOutlineSprite = tailOutlineSprite;
		}
		if (!(maskSprite == null))
		{
			return;
		}
		maskSprite = PixelSprite(MaskArt, delegate(char ch)
		{
			switch (ch)
			{
			default:
				return Color.clear;
			case 'W':
				return new Color(0.97f, 0.97f, 0.97f);
			case 'E':
			case 'O':
				return Color.black;
			}
		}, new Vector2(0.5f, 0.5f));
		MaskSprite = maskSprite;
		Color navy = new Color(0.04f, 0.15f, 0.25f);
		hornetMaskSprite = PixelSprite(HornetMaskArt, delegate(char ch)
		{
			switch (ch)
			{
			default:
				return Color.clear;
			case 'W':
				return new Color(1f, 0.94f, 0.86f);
			case 'E':
			case 'O':
				return navy;
			}
		}, new Vector2(0.67f, 0f));
		needleSprite = PixelSprite(NeedleArt, (char ch) => ch switch
		{
			'N' => new Color(0.36f, 0.31f, 0.45f), 
			'O' => navy, 
			_ => Color.clear, 
		}, new Vector2(0.5f, 0.5f));
		HornetMaskSprite = hornetMaskSprite;
		eyesSprite = PixelSprite(MaskArt, (char ch) => (ch != 'E') ? Color.clear : Color.white, new Vector2(0.5f, 0.5f));
	}

	private void HideCat()
	{
		SpriteRenderer spriteRenderer = knightMask;
		bool enabled = (knightEyes.enabled = false);
		spriteRenderer.enabled = enabled;
		if (needle != null)
		{
			needle.enabled = false;
		}
		SpriteRenderer spriteRenderer2 = catEars;
		enabled = (catEarsInner.enabled = false);
		spriteRenderer2.enabled = enabled;
		for (int i = 0; i < catTail.Length; i++)
		{
			SpriteRenderer obj = catTail[i];
			enabled = (catTailOutline[i].enabled = false);
			obj.enabled = enabled;
		}
	}

	private void UpdateCat(CelesteFrame frame, Vector3 feet, int facing, float sx, float vs)
	{
		if (!AssistMode.IsOpen)
		{
			earsSetting = AssistMode.CatEars;
		}
		bool flag = (skin.Style == "cat" || (earsSetting && skin.Style == "celeste" && skin.CelesteSprite == "player")) && frame.HasHair && root.activeSelf;
		SpriteRenderer spriteRenderer = catEars;
		bool enabled = (catEarsInner.enabled = flag);
		spriteRenderer.enabled = enabled;
		for (int i = 0; i < catTail.Length; i++)
		{
			SpriteRenderer obj = catTail[i];
			enabled = (catTailOutline[i].enabled = flag);
			obj.enabled = enabled;
		}
		if (flag)
		{
			BuildLookSprites();
			float num = vs / 8f;
			float num2 = 0.0004f;
			Vector3 localScale = new Vector3((float)facing * Math.Abs(sx) * vs, vs, 1f);
			Vector3 vector = new Vector3(nodes[0].x - (float)facing * 0.5f * num, nodes[0].y + 2f * num, feet.z + num2 * 2.5f);
			catEars.sprite = earSprite;
			catEars.color = hairColor;
			catEars.transform.position = vector;
			catEars.transform.localScale = localScale;
			catEarsInner.sprite = earInnerSprite;
			catEarsInner.color = Color.white;
			catEarsInner.transform.position = vector - new Vector3(0f, 0f, num2 * 0.1f);
			catEarsInner.transform.localScale = localScale;
			float time = Time.time;
			Vector3 vector2 = feet + new Vector3((float)(-facing) * 3f * num, 3.5f * num, num2 * 3.5f);
			for (int j = 0; j < catTail.Length; j++)
			{
				float num3 = (float)j / (float)(catTail.Length - 1);
				float num4 = Mathf.Sin(time * 3f - (float)j * 0.7f) * num3 * 2.2f;
				Vector3 vector3 = vector2 + new Vector3((float)(-facing) * ((float)j * 1.3f + num4 * 0.3f) * num, ((float)(j * j) * 0.22f + num4 * 0.7f) * num, 0f);
				float num5 = vs * 0.6f * (1f - num3 * 0.3f);
				catTail[j].sprite = tailSprite;
				catTail[j].color = hairColor;
				catTail[j].transform.position = vector3;
				catTail[j].transform.localScale = new Vector3(num5, num5, 1f);
				catTailOutline[j].sprite = tailOutlineSprite;
				catTailOutline[j].color = Color.white;
				catTailOutline[j].transform.position = vector3 + new Vector3(0f, 0f, num2 * 0.5f);
				catTailOutline[j].transform.localScale = new Vector3(num5, num5, 1f);
			}
		}
	}

	private SpriteRenderer MakeRenderer(string name, int order)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.transform.SetParent(root.transform, worldPositionStays: false);
		SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
		spriteRenderer.sharedMaterial = material;
		spriteRenderer.sortingOrder = order;
		return spriteRenderer;
	}

	public void Hide()
	{
		if (root.activeSelf)
		{
			root.SetActive(value: false);
		}
		foreach (KeyValuePair<SpriteRenderer, float> trail in trails)
		{
			if (trail.Key != null)
			{
				trail.Key.enabled = false;
			}
		}
	}

	public void Destroy()
	{
		ctrl.Dashed -= SpawnTrail;
		ctrl.DashBegan -= OnDashBegan;
		ctrl.DashLaunched -= OnDashLaunched;
		ctrl.GlideStarted -= OnGlideStarted;
		BossCard.GoldenLost -= OnGoldenLost;
		UnityEngine.Object.Destroy(root);
	}

	private bool soarSpin;

	private static float cheerUntil;

	private static float startleUntil;

	/// <summary>A boss just appeared: her hair flashes and, if she's standing still, she shivers for a moment.</summary>
	public static void Startle()
	{
		startleUntil = Time.unscaledTime + 1f;
		Plugin.Controller.HairFlashTimer = 0.2f;
	}

	/// <summary>A boss just fell: her hair flashes and, standing still, she does a little spin.</summary>
	public static void Cheer()
	{
		cheerUntil = Time.unscaledTime + 0.9f;
		Plugin.Controller.HairFlashTimer = 0.2f;
	}

	private float needolinFor;

	private void Play(string id, bool restart = false)
	{
		if (assets.Anims.TryGetValue(id, out var value) && (value != anim || restart))
		{
			if (anim != null && (anim.Id == Animations.IdleB.CelesteAnim || anim.Id == Animations.IdleC.CelesteAnim) && !id.StartsWith("idle", StringComparison.Ordinal))
			{
				Sfx.StopIdle();
			}
			anim = value;
			frameIndex = 0;
			frameTimer = 0f;
			OnFrame();
		}
	}

	private bool TryIdleMove()
	{
		if (!ctrl.Active || dying || waking || idleTimer <= 3f || idleRng.NextDouble() >= 0.20000000298023224)
		{
			return false;
		}
		List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
		int num = 0;
		string[] array = skin.IdleMoves.Split(new char[1] { ',' });
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(new char[1] { ':' });
			int num2 = int.Parse(array2[1], CultureInfo.InvariantCulture);
			list.Add(new KeyValuePair<string, int>(array2[0], num2));
			num += num2;
		}
		int num3 = idleRng.Next(num);
		string key = list[list.Count - 1].Key;
		foreach (KeyValuePair<string, int> item in list)
		{
			if (num3 < item.Value)
			{
				key = item.Key;
				break;
			}
			num3 -= item.Value;
		}
		if (!assets.Anims.ContainsKey(key))
		{
			return false;
		}
		Play(key, restart: true);
		array = skin.IdleSounds.Split(new char[1] { ';' });
		for (int i = 0; i < array.Length; i++)
		{
			string[] array3 = array[i].Split(new char[1] { ':' });
			if (array3[0] == key)
			{
				Sfx.PlayIdle(array3[1]);
				if (array3[1] == "idle_sneeze")
				{
					Achievements.Sneezed();
				}
			}
		}
		return true;
	}

	private void UpdateBind(HeroController h, Vector3 feet)
	{
		if (hornetAnimator == null || hornetAnimator.gameObject != h.gameObject)
		{
			hornetAnimator = h.GetComponent<tk2dSpriteAnimator>();
		}
		string obj = ((hornetAnimator != null && hornetAnimator.CurrentClip != null) ? hornetAnimator.CurrentClip.name : "");
		bool flag = obj.StartsWith("BindCharge", StringComparison.Ordinal);
		bool flag2 = obj.StartsWith("BindBurst", StringComparison.Ordinal);
		if (flag && !binding)
		{
			binding = true;
			waking = false;
			if (Masked(skin.Style))
			{
				Play(Animations.Idle.CelesteAnim, restart: true);
				focusT = 0f;
			}
			else
			{
				Play(Animations.BindStart.CelesteAnim, restart: true);
				Sfx.Play(Sounds.FeatherGet.Id);
			}
		}
		else if (flag2 && binding)
		{
			binding = false;
			float num = 0.1125f;
			if (Masked(skin.Style))
			{
				focusFlash = 1f;
			}
			else
			{
				Particles.Feathers(feet + new Vector3(0f, 5.5f * num, 0f), 10, 6f);
				Sfx.Play(Sounds.FeatherStateEnd.Id);
			}
			ctrl.HairFlashTimer = 0.12f;
			anim = null;
		}
		else if (!flag && !flag2 && binding)
		{
			binding = false;
			anim = null;
		}
	}

	private void OnGlideStarted()
	{
		gliderT = 0f;
		gliderScale = new Vector2(1.5f, 0.6f);
		Sfx.Play(Sounds.GliderEngage.Id);
	}

	private void UpdateGlider(CelesteFrame frame, Vector3 feet, float dt)
	{
		bool flag = ctrl.Active && ctrl.Gliding && assets.GliderOpen.Count + assets.GliderLoop.Count > 0;
		glider.enabled = flag;
		if (flag)
		{
			gliderT += dt;
			int num = (int)(gliderT / 0.06f);
			glider.sprite = ((num < assets.GliderOpen.Count || assets.GliderLoop.Count == 0) ? assets.GliderOpen[Math.Min(num, assets.GliderOpen.Count - 1)] : assets.GliderLoop[(num - assets.GliderOpen.Count) % assets.GliderLoop.Count]);
			float target = Mathf.Clamp(ctrl.Speed.x / 300f, -1f, 1f) * (-MathF.PI / 3f);
			gliderRot = Mathf.MoveTowards(gliderRot, target, MathF.PI * dt);
			gliderScale = Vector2.MoveTowards(gliderScale, Vector2.one, 2f * dt);
			float num2 = 0.1125f;
			glider.transform.position = feet + new Vector3(0f, (0f - (-12f + (float)frame.CarryY)) * num2, -0.00059999997f);
			glider.transform.rotation = Quaternion.Euler(0f, 0f, (0f - gliderRot) * 57.29578f);
			glider.transform.localScale = new Vector3(gliderScale.x * 0.9f, gliderScale.y * 0.9f, 1f);
		}
	}

	private static Dictionary<string, HashSet<int>> ParseFrames(string spec)
	{
		Dictionary<string, HashSet<int>> dictionary = new Dictionary<string, HashSet<int>>();
		string[] array = spec.Split(new char[1] { ';' });
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(new char[1] { ':' });
			HashSet<int> hashSet = new HashSet<int>();
			string[] array3 = array2[1].Split(new char[1] { ',' });
			foreach (string s in array3)
			{
				hashSet.Add(int.Parse(s, CultureInfo.InvariantCulture));
			}
			dictionary[array2[0]] = hashSet;
		}
		return dictionary;
	}

	private void OnFrame()
	{
		if (dying || anim == null)
		{
			return;
		}
		if (anim.Id == Animations.BenchWake.CelesteAnim && frameIndex == 19)
		{
			Sfx.Play(Sounds.CampfireStand.Id);
		}
		if (!ctrl.Active)
		{
			return;
		}
		if (ctrl.OnGround && footstepFrames.TryGetValue(anim.Id, out var value) && value.Contains(frameIndex))
		{
			Sfx.PlaySurface("footstep");
			if (anim.Id == Animations.Push.CelesteAnim)
			{
				float num = 0.1125f;
				Particles.Dust(body.transform.position + new Vector3((float)(-ctrl.Facing) * 5f * num, 1f * num, 0f), Mathf.Atan2(-0.5f, -ctrl.Facing), 1, 0f);
			}
		}
		else if (ctrl.State == 1 && handholdFrames.TryGetValue(anim.Id, out value) && value.Contains(frameIndex))
		{
			Sfx.PlaySurface("handhold");
		}
	}

	private bool UpdateDeath(HeroController h, float dt, Vector3 feet, int facing)
	{
		if (!h.cState.dead && !h.cState.hazardDeath)
		{
			if (dying)
			{
				EndDeath();
			}
			return false;
		}
		if (!dying)
		{
			dying = true;
			deathSounded = false;
			deathT = 0f;
			deathStart = feet;
			deathFacing = facing;
			deathDir = -facing;
			deathColor = hairColor;
			Play(Animations.DeadSide.CelesteAnim, restart: true);
			Sfx.Play(Sounds.Predeath.Id);
			EnsureBurst();
		}
		root.SetActive(value: true);
		deathT += dt;
		float num = 0.1125f;
		float num2 = 1f - Mathf.Pow(1f - Mathf.Clamp01(deathT / 0.5f), 3f);
		Vector3 vector = deathStart + new Vector3((float)deathDir * 24f * num2 * num, 0f, 0f);
		float num3 = 1.5f - num2 * 0.5f;
		Advance(dt);
		CelesteFrame celesteFrame = anim.Frames[Mathf.Clamp(frameIndex, 0, anim.Frames.Count - 1)];
		body.sprite = celesteFrame.Sprite;
		bakedHair.enabled = false;
		HideLooks();
		body.transform.position = vector;
		body.transform.localScale = new Vector3((float)deathFacing * num3 * 0.9f, num3 * 0.9f, 1f);
		for (int i = 0; i < hair.Length; i++)
		{
			hair[i].enabled = false;
			for (int j = 0; j < 4; j++)
			{
				hairOutline[i, j].enabled = false;
			}
		}
		HideCat();
		float num4 = (deathT - 0.375f) / 0.834f;
		if (num4 >= 0f && !deathSounded)
		{
			deathSounded = true;
			Sfx.Play(Sounds.Death.Id);
		}
		body.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(num4));
		DrawBurst(vector + new Vector3(0f, 6f * num, -0.0004f), deathColor, num4);
		SpriteRenderer[] componentsInChildren = root.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
		for (int k = 0; k < componentsInChildren.Length; k++)
		{
			componentsInChildren[k].sortingOrder = 0;
		}
		return true;
	}

	private void EnsureBurst()
	{
		if (burst != null)
		{
			return;
		}
		burst = new SpriteRenderer[8];
		burstOutline = new SpriteRenderer[32];
		for (int i = 0; i < 8; i++)
		{
			for (int j = 0; j < 4; j++)
			{
				burstOutline[i * 4 + j] = MakeRenderer("DeathOutline" + i + "_" + j, 0);
			}
			burst[i] = MakeRenderer("Death" + i, 0);
		}
	}

	private void DrawBurst(Vector3 center, Color baseColor, float p)
	{
		float num = 0.1125f;
		bool show = p >= 0f && p <= 1f;
		Color c = ((Math.Floor(p * 10f) % 2.0 == 0.0) ? baseColor : Color.white);
		float scale = ((p < 0.5f) ? (0.5f + p) : CubeOut(1f - (p - 0.5f) * 2f));
		for (int i = 0; i < 8; i++)
		{
			float f = ((float)i / 8f + p * 0.25f) * MathF.PI * 2f;
			Vector3 vector = center + new Vector3(Mathf.Cos(f), 0f - Mathf.Sin(f), 0f) * CubeOut(Mathf.Clamp01(p)) * 24f * num;
			SetBurst(burst[i], vector, c, scale, show);
			for (int j = 0; j < 4; j++)
			{
				SetBurst(burstOutline[i * 4 + j], vector + new Vector3(OutlineOffsets[j].x * num, OutlineOffsets[j].y * num, 0.0004f), Color.black, scale, show);
			}
		}
	}

	private void HideBurst()
	{
		if (burst != null)
		{
			SpriteRenderer[] array = burst;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].enabled = false;
			}
			array = burstOutline;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].enabled = false;
			}
		}
	}

	private bool UpdateRespawn(Vector3 feet, float dt, int layer, int order)
	{
		if (!respawning)
		{
			return false;
		}
		body.enabled = false;
		for (int i = 0; i < hair.Length; i++)
		{
			hair[i].enabled = false;
			for (int j = 0; j < 4; j++)
			{
				hairOutline[i, j].enabled = false;
			}
		}
		HideCat();
		if (CelesteWipe.ScreenCovered)
		{
			HideBurst();
			return true;
		}
		if (respawnT == 0f)
		{
			Sfx.Play(Sounds.Revive.Id);
		}
		respawnT += dt;
		float num = Mathf.Clamp01(respawnT / 0.6f);
		float num2 = 0.1125f;
		EnsureBurst();
		DrawBurst(feet + new Vector3(0f, 5.5f * num2, -0.0004f), Normal, 1f - num);
		SpriteRenderer[] array = burst;
		foreach (SpriteRenderer obj in array)
		{
			obj.sortingLayerID = layer;
			obj.sortingOrder = order;
		}
		array = burstOutline;
		foreach (SpriteRenderer obj2 in array)
		{
			obj2.sortingLayerID = layer;
			obj2.sortingOrder = order;
		}
		if (num < 1f)
		{
			return true;
		}
		respawning = false;
		HideBurst();
		body.enabled = true;
		if (ctrl.Active)
		{
			ctrl.ScaleX = 1.5f;
			ctrl.ScaleY = 0.5f;
		}
		return false;
	}

	private static float CubeOut(float t)
	{
		t = Mathf.Clamp01(t);
		return 1f - Mathf.Pow(1f - t, 3f);
	}

	private void SetBurst(SpriteRenderer sr, Vector3 at, Color c, float scale, bool show)
	{
		sr.enabled = show;
		sr.sprite = assets.HairNode;
		sr.color = c;
		sr.transform.position = at;
		sr.transform.localScale = new Vector3(scale * 0.9f, scale * 0.9f, 1f);
	}

	private void EndDeath()
	{
		dying = false;
		HideBurst();
		body.color = Color.white;
		anim = null;
		respawning = true;
		respawnT = 0f;
	}

	public void LateUpdate(HeroController h, float dt)
	{
		wallSliding = false;
		Draw(h, dt);
		bool grounded = ctrl.Active && ctrl.OnGround;
		if (AssistMode.LandingEffects && grounded && !wasGrounded && lastVerticalSpeed < -35f && body != null && body.sprite != null && root.activeSelf)
		{
			float particleScale = Mathf.Max(0.25f, body.transform.lossyScale.y);
			Particles.DustTinted(body.transform.position + new Vector3(0f, 0.8f * particleScale, 0f), -MathF.PI / 2f, 3, new Color(1f, 1f, 1f, 0.72f), 2.5f);
		}
		wasGrounded = grounded;
		lastVerticalSpeed = ctrl.Active ? ctrl.Speed.y : 0f;
		Vector3 position = h.transform.position;
		bool flag = dt > 0f && (position - lastHeroPos).sqrMagnitude > 0.0004f;
		lastHeroPos = position;
		Vector3 vector = ((AssistMode.InvisibleMotion && flag) ? Vector3.zero : Vector3.one);
		if (root != null && root.transform.localScale != vector)
		{
			root.transform.localScale = vector;
		}
		Sfx.Loop(wallSliding ? Sfx.SurfaceId("wallslide", Sfx.Surface) : (glider.enabled ? Sounds.GliderMovement.Id : null));
	}

	private static Bounds DrawBounds(HeroController h)
	{
		Bounds bounds = Col(h).bounds;
		Rigidbody2D rigidbody2D = Rb(h);
		if (rigidbody2D == null || !rigidbody2D.simulated || rigidbody2D.interpolation == RigidbodyInterpolation2D.None)
		{
			return bounds;
		}
		Vector2 vector = (Vector2)h.transform.position - rigidbody2D.position;
		float num = rigidbody2D.linearVelocity.magnitude * Time.fixedDeltaTime;
		if (vector.magnitude <= num + 0.001f)
		{
			bounds.center += (Vector3)vector;
		}
		return bounds;
	}

	private void Draw(HeroController h, float dt)
	{
		glider.enabled = false;
		MeshRenderer meshRenderer = HeroRenderer(h);
		if (meshRenderer != null)
		{
			meshRenderer.forceRenderingOff = true;
		}
		Bounds bounds = DrawBounds(h);
		if (UpdateDeath(h, dt, new Vector3(bounds.center.x, bounds.min.y, h.transform.position.z), ctrl.Active ? ctrl.Facing : (h.cState.facingRight ? 1 : (-1))))
		{
			ShowPico(on: false);
			return;
		}
		bool flag = meshRenderer != null && meshRenderer.enabled && h.gameObject.activeInHierarchy;
		if (meshRenderer != null)
		{
			meshRenderer.forceRenderingOff = true;
		}
		root.SetActive(flag);
		if (!flag)
		{
			return;
		}
		int sortingLayerID = meshRenderer.sortingLayerID;
		int sortingOrder = meshRenderer.sortingOrder;
		Bounds bounds2 = DrawBounds(h);
		Vector3 vector = new Vector3(bounds2.center.x, bounds2.min.y, h.transform.position.z);
		float target = 0f;
		if (ctrl.Active && (ctrl.State == 1 || ctrl.WallSlideDir != 0))
		{
			target = (float)(-((ctrl.State == 1) ? ctrl.Facing : ctrl.WallSlideDir)) * 2f;
		}
		wallOffset = Mathf.MoveTowards(wallOffset, target, 16f * dt);
		vector.x += wallOffset * 0.9f / 8f;
		if (h.playerData.atBench)
		{
			ContactFilter2D contactFilter = new ContactFilter2D
			{
				useTriggers = false,
				useLayerMask = true,
				layerMask = 33562880
			};
			RaycastHit2D[] array = new RaycastHit2D[1];
			RaycastHit2D raycastHit2D = ((Physics2D.Raycast(new Vector2(bounds2.center.x, bounds2.min.y + 0.02f), Vector2.down, contactFilter, array, 3f) > 0) ? array[0] : default(RaycastHit2D));
			if (raycastHit2D.collider != null)
			{
				vector.y = raycastHit2D.point.y + 0.85f;
			}
			else
			{
				vector.y += 0.85f;
			}
		}
		bool atBench = h.playerData.atBench;
		if (wasAtBench && !atBench && !respawning)
		{
			waking = true;
			Play(Animations.BenchWake.CelesteAnim, restart: true);
		}
		wasAtBench = atBench;
		if (waking && anim != null && anim.Id == Animations.BenchWake.CelesteAnim)
		{
			float num = Mathf.Clamp01(((float)frameIndex - 15f) / Math.Max(1f, (float)(anim.Frames.Count - 1) - 15f));
			vector.y += 0.85f * (1f - num);
		}
		if (ctrl.Active && ctrl.Speed != Vector2.zero)
		{
			idleTimer = 0f;
		}
		else
		{
			idleTimer += dt;
		}
		int num2 = (ctrl.Active ? ctrl.Facing : (h.cState.facingRight ? 1 : (-1)));
		if (UpdateRespawn(vector, dt, sortingLayerID, sortingOrder))
		{
			ShowPico(on: false);
			return;
		}
		UpdateBind(h, vector);
		ChooseAnimation(h);
		Advance(dt);
		CelesteFrame celesteFrame = anim.Frames[Mathf.Clamp(frameIndex, 0, anim.Frames.Count - 1)];
		float num3 = 0.9f;
		float num4 = (ctrl.Active ? ctrl.ScaleX : 1f);
		float num5 = (ctrl.Active ? ctrl.ScaleY : 1f);
		body.sprite = celesteFrame.Sprite;
		body.transform.position = vector;
		body.transform.localScale = new Vector3((float)num2 * num4 * num3, num5 * num3, 1f);
		bool flag2 = Mathf.Repeat(Time.time, 0.1f) < 0.05f;
		if (binding && anim.Id == Animations.BindHold.CelesteAnim)
		{
			body.color = StarFly;
		}
		else if (ctrl.Active && ctrl.IsTired && flag2)
		{
			body.color = Tired;
		}
		else if (boostT > 0f && ctrl.SkillDashing)
		{
			boostT -= dt;
			body.color = Color.Lerp(Hex(skin.BodyColor), new Color(1f, 0.35f, 0.35f, 1f), 0.6f);
		}
		else
		{
			boostT = 0f;
			Color color = Hex(skin.BodyColor);
			if (h.cState.invulnerable && flag2)
			{
				color.a *= 0.5f;
			}
			body.color = color;
		}
		bool flag3 = binding && anim.Id == Animations.BindStart.CelesteAnim && frameIndex < assets.StarFlyWhite.Count;
		starWhite.enabled = flag3;
		if (flag3)
		{
			starWhite.sprite = assets.StarFlyWhite[frameIndex];
			starWhite.transform.position = vector + new Vector3(0f, 0f, -0.0002f);
			starWhite.transform.localScale = body.transform.localScale;
			Color color2 = StarFly * ((float)frameIndex / (float)anim.Frames.Count);
			starWhite.color = color2;
		}
		UpdateGlider(celesteFrame, vector, dt);
		UpdateHairColor(dt);
		UpdateBakedHair(celesteFrame, vector);
		UpdateHair(celesteFrame, vector, num2, num5, num3, dt);
		float num6 = num3 / 8f;
		for (int i = 0; i < hair.Length; i++)
		{
			Sprite sprite = ((i == 0) ? assets.Bangs[Mathf.Clamp(celesteFrame.HairFrame, 0, assets.Bangs.Count - 1)] : assets.HairNode);
			bool enabled = celesteFrame.HasHair && i < hairN;
			float num7 = ((i == 0) ? 1f : ((0.25f + (1f - (float)i / (float)hairN) * 0.75f) * AssistMode.HairSize));
			Vector3 localScale = new Vector3(((i == 0) ? ((float)num2) : num7) * Math.Abs(num4) * num3, num7 * num3, 1f);
			hair[i].enabled = enabled;
			float num8 = 0.0004f;
			Color color3 = AssistMode.HairNodeColor(hairColor, i, hairN, (!ctrl.Active || (ctrl.Dashes > 0 && !ctrl.DashLocked)) && ctrl.HairFlashTimer <= 0f && !binding);
			hair[i].sprite = sprite;
			hair[i].color = color3;
			hair[i].transform.position = new Vector3(nodes[i].x, nodes[i].y, vector.z + num8);
			hair[i].transform.localScale = localScale;
			for (int j = 0; j < 4; j++)
			{
				SpriteRenderer spriteRenderer = hairOutline[i, j];
				spriteRenderer.enabled = enabled;
				spriteRenderer.sprite = sprite;
				spriteRenderer.color = Color.black;
				spriteRenderer.transform.localScale = localScale;
				spriteRenderer.transform.position = new Vector3(nodes[i].x + OutlineOffsets[j].x * num6, nodes[i].y + OutlineOffsets[j].y * num6, vector.z + num8 * 2f);
			}
		}
		UpdateCat(celesteFrame, vector, num2, num4, num3);
		UpdateHeadwear(celesteFrame, vector, num2, num4, num3);
		UpdateCompanion(h, vector, num2, num3, dt);
		UpdateAuraAndSteps(h, vector, num2, num3, dt);
		UpdateTechGhost(h, vector, num3, dt);
		UpdateRoomGhosts(vector, num3, dt);
		atBenchNow = h.playerData.atBench;
		UpdateFocus(vector, dt);
		UpdateKnight(celesteFrame, vector, num2, num4, num3);
		UpdateGolden(vector, num2, dt);
		bool isPico = IsPico;
		ShowPico(isPico);
		if (isPico)
		{
			for (int k = 0; k < hair.Length; k++)
			{
				hair[k].enabled = false;
				for (int l = 0; l < 4; l++)
				{
					hairOutline[k, l].enabled = false;
				}
			}
			starWhite.enabled = false;
			DrawPico(h, vector, num2, num4, num5, num3, dt);
		}
		SpriteRenderer[] componentsInChildren = root.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
		foreach (SpriteRenderer obj in componentsInChildren)
		{
			obj.sortingLayerID = sortingLayerID;
			obj.sortingOrder = sortingOrder;
		}
		lastSortingLayer = sortingLayerID;
		lastSortingOrder = sortingOrder;
		dashFx.Update(dt);
		Particles.Update(sortingLayerID, sortingOrder, vector.z - 0.0011999999f);
		UpdateCrystals(h, dt);
		wallSliding = ctrl.Active && anim != null && anim.Id == Animations.WallSlide.CelesteAnim && ctrl.Speed.y > 0f;
		for (int num9 = trails.Count - 1; num9 >= 0; num9--)
		{
			SpriteRenderer key = trails[num9].Key;
			float num10 = trails[num9].Value - dt;
			if (num10 <= 0f)
			{
				key.gameObject.SetActive(value: false);
				trailPool.Push(key);
				trails.RemoveAt(num9);
			}
			else
			{
				Color color4 = key.color;
				color4.a = 0.6f * num10 / 0.3f;
				key.color = color4;
				trails[num9] = new KeyValuePair<SpriteRenderer, float>(key, num10);
			}
		}
	}

	private readonly HashSet<string> unmapped = new HashSet<string>();

	private void ChooseAnimation(HeroController h)
	{
		if (h.playerData.atBench)
		{
			Play(Animations.BenchSit.CelesteAnim);
		}
		else
		{
			if (binding)
			{
				return;
			}
			if (waking)
			{
				InputMove(out var mx, out var _);
				if (mx == 0 && (!ctrl.Active || (!(ctrl.Speed != Vector2.zero) && ctrl.OnGround && ctrl.State == 0 && !ctrl.Ducking)) && anim != null && anim.Id == Animations.BenchWake.CelesteAnim)
				{
					return;
				}
				waking = false;
			}
			if (!ctrl.Active)
			{
				string text = ((hornetAnimator != null && hornetAnimator.CurrentClip != null) ? hornetAnimator.CurrentClip.name : "");
				// Hornet's own Silk Soar, played by Madeline (user 2026-10-17): crouch while it charges, look up as the
				// needle flies, spin on the way up, fall out of it when it's cancelled
				bool wasSpin = soarSpin;
				soarSpin = text.StartsWith("Super Jump Loop", StringComparison.Ordinal) && !text.StartsWith("Super Jump Loop Cancel", StringComparison.Ordinal);
				if (soarSpin && !wasSpin)
				{
					Achievements.Add("sky_high");
				}
				if (text.StartsWith("Needolin", StringComparison.Ordinal))
				{
					needolinFor += Time.deltaTime;
					if (needolinFor >= 1f)
					{
						needolinFor -= 1f;
						Achievements.Add("busker");
					}
				}
				if (text.StartsWith("Harpoon", StringComparison.Ordinal))
				{
					// the Clawline pull: Celeste's dash, with its afterimages (user 2026-10-21)
					Play(Animations.Dash.CelesteAnim);
					if ((harpoonTrail -= Time.deltaTime) <= 0f)
					{
						harpoonTrail = 0.06f;
						SpawnTrail();
					}
					return;
				}
				if (soarSpin)
				{
					Play(Animations.CutFloat.CelesteAnim);   // Celeste's own "spin": Madeline turning left to right
					return;
				}
				if (text.StartsWith("Super Jump Antic", StringComparison.Ordinal))
				{
					Play(Animations.Duck.CelesteAnim);
					return;
				}
				if (text.StartsWith("Super Jump Throw", StringComparison.Ordinal))
				{
					Play(Animations.LookUp.CelesteAnim);
					return;
				}
				if (text.StartsWith("Super Jump", StringComparison.Ordinal))
				{
					Play(Animations.FallFast.CelesteAnim);
					return;
				}
				CutsceneRow[] all = Cutscenes.All;
				for (int i = 0; i < all.Length; i++)
				{
					CutsceneRow cutsceneRow = all[i];
					if (text.StartsWith(cutsceneRow.HornetClip, StringComparison.Ordinal))
					{
						Play(cutsceneRow.CelesteAnim);
						return;
					}
				}
				if (!h.IsGravityApplied && !h.cState.onGround && h.controlReqlinquished)
				{
					Play(Animations.CutFloat.CelesteAnim);
					return;
				}
				if (MadelineTouches.DevOn && text.Length > 0 && unmapped.Add(text))
				{
					Plugin.Log.LogInfo("Cutscene clip with no Celeste animation yet: " + text + (h.cState.onGround ? " (ground)" : " (air)"));
				}
				Vector2 vector = Rb(h).linearVelocity * 8f;
				if (h.cState.onGround)
				{
					// a cutscene walk gets Celeste's walk, not a run on the spot (user 2026-10-19: more animations)
					float vx = Math.Abs(vector.x);
					Play((vx > 55f) ? Animations.RunFast.CelesteAnim : ((vx > 4f && assets.Anims.ContainsKey("walk")) ? "walk" : Animations.Idle.CelesteAnim));
				}
				else
				{
					Play((vector.y > 0f) ? Animations.JumpSlow.CelesteAnim : Animations.FallSlow.CelesteAnim);
				}
				return;
			}
			Vector2 speed = ctrl.Speed;
			if (ctrl.State == 2)
			{
				Play((ctrl.Ducking && ctrl.OnGround) ? Animations.Duck.CelesteAnim : Animations.Dash.CelesteAnim);
			}
			else if (ctrl.State == 1)
			{
				if (ctrl.LastClimbMove < 0)
				{
					Play(Animations.ClimbUp.CelesteAnim);
				}
				else
				{
					Play(Animations.WallSlide.CelesteAnim);
				}
			}
			else if (ctrl.Ducking && ctrl.OnGround)
			{
				Play(Animations.Duck.CelesteAnim);
			}
			else if (ctrl.OnGround)
			{
				if (EmotePlaying(speed))
				{
					return;
				}
				fastJump = false;
				InputMove(out var mx2, out var my2);
				if (mx2 != 0 && PushingWall(h, mx2))
				{
					Play(Animations.Push.CelesteAnim);
				}
				else if (Math.Abs(speed.x) <= 25f && mx2 == 0)
				{
					// her reactions while standing still (user 2026-10-18): a happy spin after a boss falls, a shiver
					// when one appears, panting on her last mask
					if (Time.unscaledTime < cheerUntil && assets.Anims.ContainsKey("spin"))
					{
						Play("spin");
					}
					else if (Time.unscaledTime < startleUntil && assets.Anims.ContainsKey("shaking"))
					{
						Play("shaking");
					}
					else if (my2 == -1)
					{
						Play(Animations.LookUp.CelesteAnim);
					}
					else if (h.playerData.health == 1 && h.playerData.maxHealth > 1 && assets.Anims.ContainsKey("tired"))
					{
						Play("tired");
					}
					else if (anim == null || !anim.Id.StartsWith("idle", StringComparison.Ordinal))
					{
						Play(Animations.Idle.CelesteAnim);
					}
				}
				else if (Math.Sign(speed.x) == -mx2 && mx2 != 0)
				{
					Play((Math.Abs(speed.x) > 90f) ? Animations.Skid.CelesteAnim : Animations.Flip.CelesteAnim);
				}
				else if (anim == null || (!anim.Id.StartsWith("run", StringComparison.Ordinal) && !(anim.Id == "flip")))
				{
					Play((Math.Abs(speed.x) < 45f) ? Animations.RunSlow.CelesteAnim : Animations.RunFast.CelesteAnim);
				}
			}
			else if (ctrl.Gliding)
			{
				Play((speed.y < 0f) ? Animations.GlideRise.CelesteAnim : Animations.GlideFall.CelesteAnim);
			}
			else if (ctrl.WallSlideDir != 0)
			{
				Play(Animations.WallSlide.CelesteAnim);
			}
			else if (speed.y < 0f)
			{
				if (fastJump || Math.Abs(speed.x) > 90f)
				{
					fastJump = true;
					Play(Animations.JumpFast.CelesteAnim);
				}
				else
				{
					Play(Animations.JumpSlow.CelesteAnim);
				}
			}
			else if (fastJump || speed.y >= 160f)
			{
				fastJump = true;
				Play(Animations.FallFast.CelesteAnim);
			}
			else
			{
				Play(Animations.FallSlow.CelesteAnim);
			}
		}
	}

	private static void InputMove(out int mx, out int my)
	{
		Vector2 vector = ((ManagerSingleton<InputHandler>.Instance != null && !AssistMode.IsOpen && !BossCard.RecordsOpen) ? ManagerSingleton<InputHandler>.Instance.inputActions.MoveVector.Vector : Vector2.zero);
		mx = ((vector.x > 0.3f) ? 1 : ((vector.x < -0.3f) ? (-1) : 0));
		my = ((vector.y < -0.5f) ? 1 : ((vector.y > 0.5f) ? (-1) : 0));
	}

	private static bool PushingWall(HeroController h, int dir)
	{
		Bounds bounds = Col(h).bounds;
		return Physics2D.OverlapBox((Vector2)bounds.center + new Vector2((float)dir / 8f, 0f), (Vector2)bounds.size - new Vector2(0.04f, 0.04f), 0f, 33562880) != null;
	}

	private void Advance(float dt)
	{
		if (anim.Delay <= 0f || anim.Frames.Count <= 1)
		{
			return;
		}
		frameTimer += dt;
		while (frameTimer >= anim.Delay)
		{
			frameTimer -= anim.Delay;
			frameIndex++;
			if (frameIndex >= anim.Frames.Count)
			{
				if (!anim.Loop)
				{
					if (anim.Id == Animations.Idle.CelesteAnim && TryIdleMove())
					{
						break;
					}
					if (anim.Goto != null && assets.Anims.ContainsKey(anim.Goto))
					{
						Play(anim.Goto, restart: true);
						break;
					}
					if (anim.Id == Animations.BenchWake.CelesteAnim)
					{
						waking = false;
					}
					frameIndex = anim.Frames.Count - 1;
					continue;
				}
				frameIndex = 0;
			}
			OnFrame();
		}
	}

	private SpriteRenderer bakedHair;

	/// <summary>A frame with hair painted in gets that hair redrawn on top in the live hair colour (skin, custom colour,
	/// dashes), so sleeping or sitting Madeline matches the rest of the time.</summary>
	private void UpdateBakedHair(CelesteFrame frame, Vector3 at)
	{
		bool show = frame.HairMask != null && body.enabled && !binding && !IsPico;
		bakedHair.enabled = show;
		if (show)
		{
			bakedHair.sprite = frame.HairMask;
			bakedHair.transform.position = at + new Vector3(0f, 0f, -0.0001f);
			bakedHair.transform.localScale = body.transform.localScale;
			Color c = hairColor;
			c.a = body.color.a;
			bakedHair.color = c;
		}
	}

	private void UpdateHairColor(float dt)
	{
		if (hasColorSkin && !AssistMode.IsOpen && hairVersion != AssistMode.HairVersion)
		{
			SetColors(colorSkin);
		}
		if (binding)
		{
			hairColor = StarFly;
		}
		else if (ctrl.HairFlashTimer > 0f)
		{
			hairColor = Flash;
		}
		else if (!ctrl.Active)
		{
			hairColor = (ctrl.MaxDashes == 2) ? TwoDashes : Normal;   // out of her control: the most dashes she has (user 2026-10-19)
		}
		else if (ctrl.Dashes == 0 || ctrl.DashLocked)
		{
			hairColor = Color.Lerp(hairColor, Used, 6f * dt);
		}
		else
		{
			hairColor = ((ctrl.Dashes == 2 && ctrl.MaxDashes == 2) ? TwoDashes : Normal);   // no pink with infinite dashes
		}
	}

	private void UpdateHair(CelesteFrame frame, Vector3 feet, int facing, float sy, float vs, float dt)
	{
		float num = vs / 8f;
		wave += dt * 4f;
		Vector2 vector = new Vector2(frame.HairOffset.x * (float)facing, 0f - frame.HairOffset.y) * num;
		nodes[0] = (Vector2)feet + new Vector2(0f, 9f * sy * num) + vector;
		if (!AssistMode.IsOpen && hairN != AssistMode.HairLength)
		{
			int num2 = hairN;
			hairN = AssistMode.HairLength;
			for (int i = num2; i < hairN; i++)
			{
				nodes[i] = nodes[Math.Max(0, num2 - 1)];
			}
		}
		if (!started)
		{
			for (int j = 1; j < nodes.Length; j++)
			{
				nodes[j] = nodes[0];
			}
			started = true;
		}
		Vector2 vector2 = new Vector2(Mathf.Clamp(-ctrl.Speed.x * 0.012f, -2f, 2f), -2f - Mathf.Clamp(ctrl.Speed.y * 0.008f, -1.5f, 1.5f)) * num;
		Vector2 target = nodes[0] + new Vector2((float)(-facing) * 0.5f * 2f * num, 0f) + vector2;
		Vector2 vector3 = nodes[0];
		float num3 = 3f * num;
		for (int k = 1; k < hairN; k++)
		{
			float num4 = (1f - (float)k / (float)hairN * 0.5f) * 64f * num;
			nodes[k] = Vector2.MoveTowards(nodes[k], target, num4 * dt);
			Vector2 delta = nodes[k] - vector3;
			if (delta.sqrMagnitude > num3 * num3)
			{
				nodes[k] = vector3 + delta.normalized * num3;
			}
			target = nodes[k] + new Vector2((float)(-facing) * 0.5f * num, 0f) + vector2;
			vector3 = nodes[k];
		}
	}

	private void UpdateCrystals(HeroController h, float dt)
	{
		int health = h.playerData.health;
		if (lastHealth >= 0 && health < lastHealth && root.activeSelf && body != null && body.sprite != null && MadelineController.ToolOn("Revenge Crystal"))
		{
			GameObject gameObject = new GameObject("Memory Crystal Madeline");
			gameObject.transform.position = body.transform.position + new Vector3(0f, 0f, 0.0008f);
			gameObject.transform.localScale = body.transform.lossyScale;
			SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
			spriteRenderer.sharedMaterial = material;
			spriteRenderer.sprite = body.sprite;
			spriteRenderer.color = Hex("9FE8FFD8");
			spriteRenderer.sortingLayerID = lastSortingLayer;
			spriteRenderer.sortingOrder = lastSortingOrder;
			crystals.Add(new KeyValuePair<SpriteRenderer, float>(spriteRenderer, 2.5f));
		}
		lastHealth = health;
		Color a = Hex("9FE8FFD8");
		for (int num = crystals.Count - 1; num >= 0; num--)
		{
			SpriteRenderer key = crystals[num].Key;
			float num2 = crystals[num].Value - dt;
			if (key == null)
			{
				crystals.RemoveAt(num);
			}
			else if (num2 <= 0f)
			{
				UnityEngine.Object.Destroy(key.gameObject);
				crystals.RemoveAt(num);
			}
			else
			{
				Color color = Color.Lerp(a, Color.white, 0.25f + 0.25f * Mathf.Sin(Time.time * 6f));
				color.a = a.a * Mathf.Clamp01(num2 / 0.5f);
				key.color = color;
				crystals[num] = new KeyValuePair<SpriteRenderer, float>(key, num2);
			}
		}
	}

	private float harpoonTrail;

	private void SpawnTrail()
	{
		if (AssistMode.DashTrail && !(body == null) && !(body.sprite == null) && root.activeSelf)
		{
			GameObject gameObject;
			SpriteRenderer spriteRenderer;
			if (trailPool.Count > 0)
			{
				spriteRenderer = trailPool.Pop();
				gameObject = spriteRenderer.gameObject;
				gameObject.SetActive(value: true);
			}
			else
			{
				gameObject = new GameObject("Trail");
				gameObject.transform.SetParent(root.transform, worldPositionStays: false);
				spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
				spriteRenderer.sharedMaterial = material;
			}
			gameObject.transform.position = body.transform.position + new Vector3(0f, 0f, 0.0011999999f);
			gameObject.transform.localScale = body.transform.localScale;
			spriteRenderer.sprite = body.sprite;
			spriteRenderer.sortingLayerID = lastSortingLayer;
			spriteRenderer.sortingOrder = lastSortingOrder - 1;
			// Trail Shape (user 2026-10-19): hearts, stars or feathers instead of the afterimage
			Sprite shape = (skin.Style == "celeste" || skin.Style == "cat") ? TrailShapeSprite() : null;
			if (shape != null)
			{
				float ts = Math.Abs(body.transform.localScale.x);
				float tpx = ts / 8f;
				spriteRenderer.sprite = shape;
				gameObject.transform.position = body.transform.position + new Vector3((UnityEngine.Random.value * 8f - 4f) * tpx, (6f + UnityEngine.Random.value * 8f - 4f) * tpx, 0.0012f);
				gameObject.transform.localScale = new Vector3(ts, ts, 1f) * (shape == assets.Feather ? 1.5f : 1f);
			}
			Color color = hairColor;
			color.a = 0.6f;
			spriteRenderer.color = color;
			if (skin.Style == "hornet")
			{
				spriteRenderer.color = new Color(0.95f, 0.95f, 1f, 0.48000002f);
				Particles.DustTinted(body.transform.position + new Vector3(0f, 0.67499995f, 0f), MathF.PI / 2f * ((UnityEngine.Random.value < 0.5f) ? 1f : (-1f)), 3, new Color(1f, 1f, 1f, 0.8f));
			}
			else if (skin.Style == "knight")
			{
				spriteRenderer.color = new Color(0.04f, 0.04f, 0.06f, 0.6f);
				Particles.DustTinted(body.transform.position + new Vector3(0f, 0.67499995f, 0f), MathF.PI / 2f * ((UnityEngine.Random.value < 0.5f) ? 1f : (-1f)), 4, new Color(0.05f, 0.05f, 0.08f, 0.9f));
			}
			trails.Add(new KeyValuePair<SpriteRenderer, float>(spriteRenderer, 0.3f));
		}
	}

	private static Color Hex(string hex)
	{
		ColorUtility.TryParseHtmlString("#" + hex, out var color);
		return color;
	}
}
