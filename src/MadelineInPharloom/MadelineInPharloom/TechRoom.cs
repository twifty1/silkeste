using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace MadelineInPharloom;

/// <summary>
/// The Tech Tutorial (user 2026-10-20): on a new file, after the Tutorial postcard, Silkeste asks whether the player
/// knows Celeste's tech. "No" (or "Yes" then "Try it") wipes Madeline into Farewell's own tutorial rooms, read from the
/// player's Celeste (f-00: wavedash and wall bounce), then a practice room testing the super, hyper and wavedash
/// on two enemies, with Celeste's ghost runs and the Tech Tips
/// bubble over them. The rooms are built far from the Silksong level; the camera follows her Celeste's way, spikes and
/// falls send her back to the last start point, and the far end of the last room wipes her back to where she was.
/// Settings → Game → Tech Tutorial plays it again.
/// </summary>
public class TechRoom : MonoBehaviour
{
	public static bool Active;

	public static TechRoom Instance;

	private static readonly AccessTools.FieldRef<HeroController, Collider2D> Col = AccessTools.FieldRefAccess<HeroController, Collider2D>("col2d");

	private static readonly string[] Levels = new string[1] { "lvl_f-00" };   // the combo room (f-01) is left out (user 2026-10-20)

	private static readonly Vector3 Origin = new Vector3(6000f, 6000f, 0f);

	private class Room
	{
		public string Name;

		public Rect Bounds;   // world
	}

	private struct Hazard
	{
		public Rect R;

		public Vector2 Against;   // the way she must be moving to be hurt (y down, like the controller)
	}

	public class Playback
	{
		public List<TechRoomData.PlaybackFrame> Frames;

		public Vector3 Origin;   // world, her feet at the first frame

		public float MinX;

		public float MaxX;

		public int Lesson;

		public float T;
	}

	public static readonly List<Playback> Playbacks = new List<Playback>();

	/// <summary>Is she where one of Celeste's ghost runs plays (the Tech Tips ghost stays away there)?</summary>
	public static bool DemoNear(float x)
	{
		if (!Active)
		{
			return false;
		}
		if (enemyHintOn)
		{
			return true;
		}
		foreach (Playback p in Playbacks)
		{
			if (x >= p.MinX - 1f && x <= p.MaxX + 1f)
			{
				return true;
			}
		}
		return false;
	}

	private readonly List<Room> rooms = new List<Room>();

	private readonly List<Hazard> hazards = new List<Hazard>();

	private readonly List<Collider2D> dreams = new List<Collider2D>();

	private readonly List<Rect> dreamRects = new List<Rect>();

	private readonly List<Rect> springs = new List<Rect>();

	private readonly List<Vector3> spawns = new List<Vector3>();

	private readonly List<SpriteRenderer> stars = new List<SpriteRenderer>();

	private SpriteRenderer sky;

	private GameObject root;

	private int spawn;

	private Vector3 finish;

	private Vector3 returnTo;

	private string returnScene;

	private Vector3 camPos;

	private bool camSnap;

	private float springCooldown;

	private int sortLayer;

	private int sortOrder;

	private float heroZ;

	private Material mat;

	private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

	// the wipe: covering, then the action, then uncovering
	private float wipe;

	private int wipeDir;

	private Action wipeAction;

	private bool relinquished;

	private void Awake()
	{
		Instance = this;
	}

	// ---- the offer ------------------------------------------------------------------------------------------------

	private enum Ask
	{
		None,
		Familiar,
		Sure,
		Leave
	}

	private Ask ask;

	private int choice;

	private float askT;

	private float flash;

	private bool pendingOffer;

	private float waitFor;

	private bool holding;

	private bool gaveUp;

	/// <summary>The Tech Tutorial is holding her still before its question (the Tutorial postcard may still open).</summary>
	public static bool HoldsControl => Instance != null && Instance.holding;

	private bool waitingToAsk;

	/// <summary>No moving or dashing while she's being asked, waiting to be, or a wipe is on (user 2026-10-20).</summary>
	public static bool InputLocked => Instance != null && (Instance.ask != Ask.None || Instance.wipeDir != 0 || Instance.waitingToAsk);

	private readonly Rect[] choiceRects = new Rect[2];

	/// <summary>The Tutorial postcard closed on a new file: ask once per save (not in Speedrunner mode).</summary>
	public static void OfferAfterIntro()
	{
		if (Instance == null || AssistMode.ModeId == "speedrun" || TechTips.Slot < 0 || TechTips.Flag("asked"))
		{
			return;
		}
		Instance.pendingOffer = true;
	}

	/// <summary>Settings → Tech Tutorial: in, or out when already in.</summary>
	public static void Request()
	{
		if (Instance == null)
		{
			return;
		}
		if (Active)
		{
			Instance.Leave(finished: false);
		}
		else
		{
			Instance.Enter();
		}
	}

	private void OpenAsk(Ask a)
	{
		if (a == Ask.Familiar)
		{
			PreloadCrawler();   // so a ground moss enemy is ready for the practice room
		}
		HeroController h = HeroController.UnsafeInstance;
		ask = a;
		choice = a == Ask.Familiar ? 1 : 0;
		askT = 0f;
		Sfx.Play(Sounds.CardIn.Id);
		if (h != null && !h.controlReqlinquished)
		{
			h.RelinquishControl();
			relinquished = true;
		}
	}

	private void CloseAsk()
	{
		ask = Ask.None;
		HeroController h = HeroController.UnsafeInstance;
		if (relinquished && h != null)
		{
			h.RegainControl();
		}
		relinquished = false;
	}

	private void Choose(int c)
	{
		Sfx.Play(Sounds.MenuButtonSelect.Id);
		if (ask == Ask.Leave)
		{
			CloseAsk();
			if (c == 1)
			{
				Leave(finished: false);
			}
			return;
		}
		if (ask == Ask.Familiar)
		{
			if (c == 0)
			{
				OpenAsk(Ask.Sure);   // "yes": are you sure?
				return;
			}
			TechTips.SetFlag("asked");
			CloseAsk();
			Enter();
			return;
		}
		TechTips.SetFlag("asked");
		CloseAsk();
		if (c == 0)
		{
			Enter();
		}
		else
		{
			Plugin.Log.LogInfo("Tech tutorial: skipped on file " + TechTips.Slot);
		}
	}

	private void DriveAsk(float dt)
	{
		askT += dt;
		flash += dt;
		if (askT < 0.5f)
		{
			return;
		}
		InputHandler ih = ManagerSingleton<InputHandler>.Instance;
		HeroActions a = ih != null ? ih.inputActions : null;
		Keyboard kb = Keyboard.current;
		bool left = a != null && (a.Left.WasPressed || a.Up.WasPressed);
		bool right = a != null && (a.Right.WasPressed || a.Down.WasPressed);
		if (left && choice != 0 || right && choice != 1)
		{
			choice = left ? 0 : 1;
			Sfx.Play(left ? Sounds.MenuRolloverUp.Id : Sounds.MenuRolloverDown.Id);
		}
		Mouse m = Mouse.current;
		if (m != null)
		{
			float k = Screen.height / 1080f;
			Vector2 p = m.position.ReadValue();
			Vector2 g = new Vector2(p.x, Screen.height - p.y) / k;
			for (int i = 0; i < 2; i++)
			{
				if (choiceRects[i].Contains(g))
				{
					if (m.delta.ReadValue().sqrMagnitude > 0.5f && choice != i)
					{
						choice = i;
						Sfx.Play(Sounds.MenuRolloverDown.Id);
					}
					if (m.leftButton.wasPressedThisFrame)
					{
						Choose(i);
						return;
					}
				}
			}
		}
		bool confirm = a != null && (a.Jump.WasPressed || a.MenuSubmit.WasPressed) || kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
		if (confirm)
		{
			Choose(choice);
		}
	}

	// ---- in and out -----------------------------------------------------------------------------------------------

	private void StartWipe(Action atCovered)
	{
		wipeDir = 1;
		wipe = 0f;
		wipeAction = atCovered;
	}

	private void Enter()
	{
		HeroController h = HeroController.UnsafeInstance;
		GameManager gm = GameManager.SilentInstance;
		if (Active || h == null || gm == null || Plugin.Atlas == null || Plugin.CelesteDir == null)
		{
			Plugin.Log.LogWarning("Tech tutorial: can't start here");
			return;
		}
		PreloadCrawler();
		StartWipe(delegate
		{
			try
			{
				Build(h);
			}
			catch (Exception e)
			{
				Plugin.Log.LogError("Tech tutorial: could not build the rooms: " + e);
				Cleanup();
				return;
			}
			returnTo = h.transform.position;
			returnScene = gm.sceneName;
			Active = true;
			spawn = 0;
			Place(h, spawns[0]);
			GameCameras gc = GameCameras.SilentInstance;
			if (gc != null && gc.cameraController != null)
			{
				gc.cameraController.enabled = false;
			}
			camSnap = true;
			Collider2D hc = Col(h);
			Plugin.Log.LogInfo("Tech tutorial: in (" + Levels.Length + " rooms, " + Playbacks.Count + " ghost runs, " + dreamLooks.Count + " dream blocks; her box " + (hc != null ? (hc.bounds.size.x * 8f).ToString("0.0") + " x " + (hc.bounds.size.y * 8f).ToString("0.0") + " px, Celeste's is 8 x 11" : "?") + ")");
		});
	}

	private void Leave(bool finished)
	{
		if (!Active)
		{
			return;
		}
		if (finished)
		{
			Sfx.Play(Sounds.GoldenGet.Id);
		}
		StartWipe(delegate
		{
			HeroController h = HeroController.UnsafeInstance;
			Cleanup();
			if (h != null)
			{
				Place(h, returnTo, feet: false);
			}
			GameCameras gc = GameCameras.SilentInstance;
			if (gc != null && gc.cameraController != null)
			{
				gc.cameraController.PositionToHero(forceDirect: true);
			}
			Plugin.Log.LogInfo("Tech tutorial: out (" + (finished ? "finished" : "left") + ")");
		});
	}

	private void Cleanup()
	{
		Active = false;
		Playbacks.Clear();
		Plugin.Controller.InDream = false;
		GameCameras gc = GameCameras.SilentInstance;
		if (gc != null && gc.cameraController != null && !gc.cameraController.enabled)
		{
			gc.cameraController.enabled = true;
			// switching it off unhooked it from "the hero is in place" (it only hooks on once, at the start of a
			// game), so it stopped finding her in every room after (user 2026-10-20: the screen stayed put)
			HeroController hh = HeroController.UnsafeInstance;
			if (hh != null)
			{
				hh.heroInPosition -= gc.cameraController.PositionToHero;
				hh.heroInPosition += gc.cameraController.PositionToHero;
			}
		}
		if (root != null)
		{
			UnityEngine.Object.Destroy(root);
		}
		root = null;
		rooms.Clear();
		enemies.Clear();
		enemyHintOn = false;
		hazards.Clear();
		dreams.Clear();
		dreamRects.Clear();
		dreamLooks.Clear();
		springs.Clear();
		spawns.Clear();
		stars.Clear();
		sky = null;
	}

	private static void Place(HeroController h, Vector3 at, bool feet = true)
	{
		Vector3 p = at;
		if (feet)
		{
			Collider2D c = Col(h);
			float off = c != null ? h.transform.position.y - c.bounds.min.y : 0.7f;
			p = new Vector3(at.x, at.y + off + 0.02f, h.transform.position.z);
		}
		h.transform.position = p;
		Rigidbody2D rb = h.GetComponent<Rigidbody2D>();
		if (rb != null)
		{
			rb.velocity = Vector2.zero;
		}
		Plugin.Controller.Speed = Vector2.zero;
		Plugin.Controller.InDream = false;
	}

	// ---- building the rooms ---------------------------------------------------------------------------------------

	private Vector3 World(Vector2 levelPx, float px, float py, float z = 0f)
	{
		return Origin + new Vector3((levelPx.x + px) / 8f, -(levelPx.y + py) / 8f, heroZ + z);
	}

	private void Build(HeroController h)
	{
		Cleanup();
		string content = Path.Combine(Plugin.CelesteDir, "Content");
		TechRoomData.Element map = TechRoomData.ReadMap(Path.Combine(content, "Maps/LostLevels.bin"));
		Dictionary<char, TechRoomData.Tileset> fgSets = TechRoomData.ReadTilesets(Path.Combine(content, "Graphics/ForegroundTiles.xml"));
		Dictionary<char, TechRoomData.Tileset> bgSets = TechRoomData.ReadTilesets(Path.Combine(content, "Graphics/BackgroundTiles.xml"));
		heroZ = h.transform.position.z;
		MeshRenderer mr = h.GetComponent<MeshRenderer>();
		sortLayer = mr != null ? mr.sortingLayerID : 0;
		sortOrder = mr != null ? mr.sortingOrder : 0;
		if (mat == null)
		{
			mat = new Material(Shader.Find("Sprites/Default"));
		}
		root = new GameObject("SilkesteTechRoom");
		UnityEngine.Object.DontDestroyOnLoad(root);
		TechRoomData.Element first = TechRoomData.Level(map, Levels[0]);
		Vector2 basePx = new Vector2(first.F("x"), first.F("y"));
		foreach (string name in Levels)
		{
			TechRoomData.Element lv = TechRoomData.Level(map, name);
			if (lv == null)
			{
				throw new Exception("Farewell has no room " + name);
			}
			// user 2026-10-20: no wall bounce part; f-00 stops at the block its wall bounce starts from
			cutPx = name == "lvl_f-00" ? 680f : 99999f;
			BuildLevel(lv, new Vector2(lv.F("x"), lv.F("y")) - basePx, fgSets, bgSets, content);
			lastOff = new Vector2(lv.F("x"), lv.F("y")) - basePx;
			lastW = Math.Min(lv.F("width"), cutPx);
			lastExitY = exitY;
		}
		cutPx = 99999f;
		BuildSections(fgSets);
		BuildPractice(fgSets);
		// Celeste's level edges: no walking off the sides of the first and last rooms (user 2026-10-20: fell into
		// the void going left at the start)
		Rect firstR = rooms[0].Bounds;
		Rect lastR = rooms[rooms.Count - 1].Bounds;
		Box("Edge", new Vector3(firstR.xMin - 1f, firstR.yMax + 30f, heroZ), 1f, firstR.height + 30f);
		Box("Edge", new Vector3(lastR.xMax, lastR.yMax + 30f, heroZ), 1f, lastR.height + 30f);
		BuildSky();
		if (spawns.Count == 0)
		{
			throw new Exception("no start point");
		}
		finish = spawns[spawns.Count - 1];
		SpawnEnemies();
		if (enemies.Count == 0)
		{
			Plugin.Log.LogInfo("Tech tutorial: no enemy copy yet (one is kept the first time she's in a level with an early green enemy)");
		}
	}

	private void BuildLevel(TechRoomData.Element lv, Vector2 off, Dictionary<char, TechRoomData.Tileset> fgSets, Dictionary<char, TechRoomData.Tileset> bgSets, string content)
	{
		int wPx = (int)Math.Min(lv.F("width"), cutPx);
		int hPx = (int)lv.F("height");
		int w = wPx / 8;
		int hh = hPx / 8;
		Vector3 tl = World(off, 0f, 0f);
		rooms.Add(new Room { Name = lv.S("name"), Bounds = new Rect(tl.x, tl.y - hPx / 8f, wPx / 8f, hPx / 8f) });
		char[,] fg = TechRoomData.Grid(lv.Child("solids")?.S("innerText"), w, hh);
		char[,] bg = TechRoomData.Grid(lv.Child("bg")?.S("innerText"), w, hh);
		TechRoomData.Element ents = lv.Child("entities");
		int firstSpawn = spawns.Count;
		exitX = -1f;
		exitY = 0f;
		if (ents != null)
		{
			// floaty space blocks are just more solid tiles here
			foreach (TechRoomData.Element e in ents.Children)
			{
				if (e.Name == "floatySpaceBlock" && e.F("x") < cutPx)
				{
					char c = e.S("tiletype", "m")[0];
					for (int y = (int)e.F("y") / 8; y < (int)(e.F("y") + e.F("height")) / 8 && y < hh; y++)
					{
						for (int x = (int)e.F("x") / 8; x < (int)(e.F("x") + e.F("width")) / 8 && x < w; x++)
						{
							fg[x, y] = c;
						}
					}
				}
			}
		}
		TileLayer(bg, bgSets, off, w, hh, 0.02f, -20, "bg");
		TileLayer(fg, fgSets, off, w, hh, -0.02f, 20, "fg");
		Colliders(fg, off, w, hh);
		Decals(lv.Child("bgdecals"), off, 0.015f, -15);
		Decals(lv.Child("fgdecals"), off, -0.015f, 15);
		if (ents != null)
		{
			foreach (TechRoomData.Element e in ents.Children)
			{
				if (e.F("x") < cutPx && e.S("tutorial") != "superwalljump")
				{
					Entity(e, off, content);
				}
			}
		}
		TechRoomData.Element trig = lv.Child("triggers");
		if (trig != null)
		{
			foreach (TechRoomData.Element t in trig.Children)
			{
				if (t.Name == "changeRespawnTrigger" && t.Children.Count > 0 && t.F("x") < cutPx)
				{
					spawns.Add(World(off, t.Children[0].F("x"), t.Children[0].F("y")));
				}
			}
		}
		// this room's start points, left to right (the last of the last room is the finish)
		List<Vector3> mine = spawns.GetRange(firstSpawn, spawns.Count - firstSpawn);
		mine.Sort((Vector3 p, Vector3 q) => p.x.CompareTo(q.x));
		spawns.RemoveRange(firstSpawn, spawns.Count - firstSpawn);
		spawns.AddRange(mine);
	}

	private bool AtlasTex(string name, out CelesteAssets.SubTex t, out CelesteAssets.Page page)
	{
		page = null;
		CelesteAssets.Prepared atlas = Plugin.Atlas;
		if (atlas == null || !atlas.Textures.TryGetValue(name, out t))
		{
			t = default(CelesteAssets.SubTex);
			return false;
		}
		page = atlas.Pages[t.Page];
		return page != null;
	}

	/// <summary>A pixel of an atlas picture, in its full (untrimmed) frame, top-left origin.</summary>
	private static Color32 Px(CelesteAssets.SubTex t, CelesteAssets.Page page, int fx, int fy)
	{
		int j = fx - t.OffX;
		int i = fy - t.OffY;
		if (j < 0 || i < 0 || j >= t.W || i >= t.H)
		{
			return new Color32(0, 0, 0, 0);
		}
		int k = ((t.Y + i) * page.W + (t.X + j)) * 4;
		return new Color32(page.Rgba[k], page.Rgba[k + 1], page.Rgba[k + 2], page.Rgba[k + 3]);
	}

	private Sprite AtlasSprite(string name, Vector2 pivot01)
	{
		string key = name + "@" + pivot01.x + "," + pivot01.y;
		if (spriteCache.TryGetValue(key, out Sprite s))
		{
			return s;
		}
		s = null;
		if (AtlasTex(name, out CelesteAssets.SubTex t, out CelesteAssets.Page page))
		{
			Texture2D tex = new Texture2D(t.FullW, t.FullH, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
			Color32[] px = new Color32[t.FullW * t.FullH];
			for (int y = 0; y < t.FullH; y++)
			{
				for (int x = 0; x < t.FullW; x++)
				{
					px[(t.FullH - 1 - y) * t.FullW + x] = Px(t, page, x, y);
				}
			}
			tex.SetPixels32(px);
			tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			s = Sprite.Create(tex, new Rect(0f, 0f, t.FullW, t.FullH), pivot01, 8f, 0u, SpriteMeshType.FullRect);
		}
		spriteCache[key] = s;
		return s;
	}

	private SpriteRenderer Renderer(string name, Sprite s, Vector3 at, int order)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(root.transform, worldPositionStays: false);
		go.transform.position = at;
		SpriteRenderer r = go.AddComponent<SpriteRenderer>();
		r.sharedMaterial = mat;
		r.sprite = s;
		r.sortingLayerID = sortLayer;
		r.sortingOrder = sortOrder + order;
		return r;
	}

	private void TileLayer(char[,] g, Dictionary<char, TechRoomData.Tileset> sets, Vector2 off, int w, int h, float z, int order, string name)
	{
		Texture2D tex = new Texture2D(w * 8, h * 8, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
		Color32[] px = new Color32[w * 8 * h * 8];
		Dictionary<char, KeyValuePair<CelesteAssets.SubTex, CelesteAssets.Page>> sheets = new Dictionary<char, KeyValuePair<CelesteAssets.SubTex, CelesteAssets.Page>>();
		bool any = false;
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				char c = g[x, y];
				if (c == '0' || !sets.TryGetValue(c, out TechRoomData.Tileset ts))
				{
					continue;
				}
				if (!sheets.TryGetValue(c, out KeyValuePair<CelesteAssets.SubTex, CelesteAssets.Page> sheet))
				{
					AtlasTex("tilesets/" + ts.Path, out CelesteAssets.SubTex st, out CelesteAssets.Page pg);
					sheet = sheets[c] = new KeyValuePair<CelesteAssets.SubTex, CelesteAssets.Page>(st, pg);
				}
				if (sheet.Value == null)
				{
					continue;
				}
				Vector2Int? pick = TechRoomData.Pick(g, x, y, ts);
				if (!pick.HasValue)
				{
					continue;
				}
				for (int j = 0; j < 8; j++)
				{
					for (int i = 0; i < 8; i++)
					{
						Color32 c32 = Px(sheet.Key, sheet.Value, pick.Value.x * 8 + i, pick.Value.y * 8 + j);
						int ty = h * 8 - 1 - (y * 8 + j);
						px[ty * w * 8 + x * 8 + i] = c32;
					}
				}
				any = true;
			}
		}
		if (!any)
		{
			return;
		}
		tex.SetPixels32(px);
		tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		Sprite s = Sprite.Create(tex, new Rect(0f, 0f, w * 8, h * 8), new Vector2(0f, 1f), 8f, 0u, SpriteMeshType.FullRect);
		Renderer("Tiles " + name, s, World(off, 0f, 0f, z), order);
	}

	/// <summary>Solid tiles become box colliders on Silksong's terrain layer, merged into rectangles.</summary>
	private void Colliders(char[,] g, Vector2 off, int w, int h)
	{
		bool[,] used = new bool[w, h];
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				if (used[x, y] || g[x, y] == '0')
				{
					continue;
				}
				int x2 = x;
				while (x2 + 1 < w && g[x2 + 1, y] != '0' && !used[x2 + 1, y])
				{
					x2++;
				}
				int y2 = y;
				bool grow = true;
				while (grow && y2 + 1 < h)
				{
					for (int i = x; i <= x2; i++)
					{
						if (g[i, y2 + 1] == '0' || used[i, y2 + 1])
						{
							grow = false;
							break;
						}
					}
					if (grow)
					{
						y2++;
					}
				}
				for (int j = y; j <= y2; j++)
				{
					for (int i = x; i <= x2; i++)
					{
						used[i, j] = true;
					}
				}
				Box("Solid", World(off, x * 8, y * 8), (x2 - x + 1), (y2 - y + 1));
			}
		}
	}

	private BoxCollider2D Box(string name, Vector3 topLeft, float wUnits, float hUnits)
	{
		GameObject go = new GameObject(name);
		go.layer = 8;
		go.transform.SetParent(root.transform, worldPositionStays: false);
		go.transform.position = new Vector3(topLeft.x + wUnits / 2f, topLeft.y - hUnits / 2f, heroZ);
		BoxCollider2D b = go.AddComponent<BoxCollider2D>();
		b.size = new Vector2(wUnits, hUnits);
		return b;
	}

	private void Decals(TechRoomData.Element decals, Vector2 off, float z, int order)
	{
		if (decals == null)
		{
			return;
		}
		foreach (TechRoomData.Element d in decals.Children)
		{
			if (d.F("x") >= cutPx)
			{
				continue;
			}
			string tex = d.S("texture").Replace('\\', '/');
			if (tex.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
			{
				tex = tex.Substring(0, tex.Length - 4);
			}
			Sprite s = AtlasSprite("decals/" + tex, new Vector2(0.5f, 0.5f));
			if (s == null)
			{
				s = AtlasSprite("decals/" + tex + "00", new Vector2(0.5f, 0.5f));   // an animated decal's first frame
			}
			if (s == null)
			{
				continue;
			}
			SpriteRenderer r = Renderer("Decal", s, World(off, d.F("x"), d.F("y"), z), order);
			r.transform.localScale = new Vector3(d.F("scaleX", 1f), d.F("scaleY", 1f), 1f);
		}
	}

	private void Entity(TechRoomData.Element e, Vector2 off, string content)
	{
		float x = e.F("x");
		float y = e.F("y");
		float w = e.F("width", 8f);
		float h = e.F("height", 8f);
		switch (e.Name)
		{
		case "player":
			spawns.Add(World(off, x, y));
			if (x >= exitX)
			{
				exitX = x;   // the rightmost start point is where the room is left
				exitY = y;
			}
			break;
		case "spikesUp":
			Spikes(off, x, y - 3f, w, 3f, new Vector2(0f, 1f), "up", w / 8f, 0f);
			break;
		case "spikesDown":
			Spikes(off, x, y, w, 3f, new Vector2(0f, -1f), "down", w / 8f, 0f);
			break;
		case "spikesLeft":
			Spikes(off, x - 3f, y, 3f, h, new Vector2(1f, 0f), "left", 0f, h / 8f);
			break;
		case "spikesRight":
			Spikes(off, x, y, 3f, h, new Vector2(-1f, 0f), "right", 0f, h / 8f);
			break;
		case "dreamBlock":
			DreamBlock(off, x, y, w, h);
			break;
		case "jumpThru":
		{
			Box("JumpThru", World(off, x, y), w / 8f, 0.5f);
			Sprite s = AtlasSprite("objects/jumpthru/wood", new Vector2(0f, 1f));
			for (int i = 0; i < Math.Max(1, (int)(w / 24f)); i++)
			{
				if (s != null)
				{
					Renderer("JumpThru", s, World(off, x + i * 24f, y, -0.01f), 10);
				}
			}
			break;
		}
		case "wallSpringLeft":
		case "wallSpringRight":
		{
			// on a wall to its left it kicks her right (and the other way round)
			bool left = e.Name == "wallSpringLeft";
			Vector3 a = World(off, left ? x : x - 6f, y - 8f);
			springs.Add(new Rect(a.x, a.y - 2f, 6f / 8f, 2f));
			Sprite s = AtlasSprite("objects/spring/00", new Vector2(0.5f, 0f));
			if (s != null)
			{
				SpriteRenderer r = Renderer("Spring", s, World(off, x, y, -0.01f), 12);
				r.transform.rotation = Quaternion.Euler(0f, 0f, left ? -90f : 90f);
			}
			break;
		}
		case "playbackTutorial":
		{
			string tut = e.S("tutorial");
			string file = Path.Combine(content, "Tutorials/" + tut + ".bin");
			if (!File.Exists(file))
			{
				break;
			}
			Playback pb = new Playback { Frames = TechRoomData.ReadPlayback(file), Origin = World(off, x, y), Lesson = tut == "wavedash" ? 2 : tut == "superwalljump" ? 3 : -1 };
			if (pb.Frames.Count == 0)
			{
				break;
			}
			pb.MinX = e.Children.Count > 0 ? World(off, e.Children[0].F("x"), 0f).x : pb.Origin.x - 10f;
			pb.MaxX = e.Children.Count > 1 ? World(off, e.Children[1].F("x"), 0f).x : pb.Origin.x + 30f;
			Playbacks.Add(pb);
			break;
		}
		}
	}

	private void Spikes(Vector2 off, float x, float y, float w, float h, Vector2 against, string dir, float countX, float countY)
	{
		Vector3 a = World(off, x, y);
		hazards.Add(new Hazard { R = new Rect(a.x, a.y - h / 8f, w / 8f, h / 8f), Against = against });
		Sprite s = AtlasSprite("danger/spikes/default_" + dir + "00", dir == "up" ? new Vector2(0.5f, 0f) : dir == "down" ? new Vector2(0.5f, 1f) : dir == "left" ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f));
		if (s == null)
		{
			return;
		}
		int n = Math.Max(1, (int)Math.Round(Math.Max(countX, countY)));
		for (int i = 0; i < n; i++)
		{
			Vector3 at;
			switch (dir)
			{
			case "up":
				at = World(off, x + 4f + i * 8f, y + 3f + 1f, -0.01f);
				break;
			case "down":
				at = World(off, x + 4f + i * 8f, y - 1f, -0.01f);
				break;
			case "left":
				at = World(off, x + 3f + 1f, y + 4f + i * 8f, -0.01f);
				break;
			default:
				at = World(off, x - 1f, y + 4f + i * 8f, -0.01f);
				break;
			}
			Renderer("Spike", s, at, 12);
		}
	}

	private void DreamBlock(Vector2 off, float x, float y, float w, float h)
	{
		BoxCollider2D b = Box("DreamBlock", World(off, x, y), w / 8f, h / 8f);
		dreams.Add(b);
		Vector3 a = World(off, x, y);
		dreamRects.Add(new Rect(a.x, a.y - h / 8f, w / 8f, h / 8f));
		DreamLook d = new DreamLook((int)w, (int)h, new Vector2(off.x + x, off.y + y), (int)(x * 31f + y * 7f));
		d.R = Renderer("DreamBlock", d.Sprite, World(off, x - DreamLook.Margin, y - DreamLook.Margin, -0.005f), 8);
		dreamLooks.Add(d);
	}

	private readonly List<DreamLook> dreamLooks = new List<DreamLook>();

	private Color32[] dreamParticles;

	private int dreamParticlesW;

	/// <summary>
	/// Celeste's dream block, drawn the way DreamBlock.Render does it (redrawn every frame): black, three layers of
	/// coloured particles from objects/dreamblock/particles that drift with the camera, and a white edge that wobbles,
	/// with white 2x2 corners.
	/// </summary>
	private class DreamLook
	{
		public const int Margin = 4;

		public readonly int W;

		public readonly int H;

		public readonly Vector2 Px;   // top-left, Celeste pixels in the tutorial's space

		public readonly Texture2D Tex;

		public readonly Sprite Sprite;

		public readonly Color32[] Buf;

		public SpriteRenderer R;

		public readonly Vector2[] PPos;

		public readonly int[] PLayer;

		public readonly float[] PTime;

		public readonly Color32[] PCol;

		public float AnimTimer;

		public float WobbleFrom;

		public float WobbleTo;

		public float WobbleEase;

		public readonly System.Random Rnd;

		public DreamLook(int w, int h, Vector2 px, int seed)
		{
			W = w;
			H = h;
			Px = px;
			Rnd = new System.Random(seed);
			int tw = w + Margin * 2;
			int th = h + Margin * 2;
			Tex = new Texture2D(tw, th, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
			Buf = new Color32[tw * th];
			Sprite = Sprite.Create(Tex, new Rect(0f, 0f, tw, th), new Vector2(0f, 1f), 8f, 0u, SpriteMeshType.FullRect);
			int n = (int)(w / 8.0 * (h / 8.0) * 0.7);
			PPos = new Vector2[n];
			PLayer = new int[n];
			PTime = new float[n];
			PCol = new Color32[n];
			int[] layers = new int[6] { 0, 1, 1, 2, 2, 2 };
			string[][] cols = new string[3][]
			{
				new string[3] { "FFEF11", "FF00D0", "08a310" },
				new string[3] { "5fcde4", "7fb25e", "E0564C" },
				new string[3] { "5b6ee1", "CC3B3B", "7daa64" }
			};
			for (int i = 0; i < n; i++)
			{
				PPos[i] = new Vector2((float)Rnd.NextDouble() * w, (float)Rnd.NextDouble() * h);
				PLayer[i] = layers[Rnd.Next(6)];
				PTime[i] = (float)Rnd.NextDouble();
				ColorUtility.TryParseHtmlString("#" + cols[PLayer[i]][Rnd.Next(3)], out Color c);
				PCol[i] = c;
			}
			WobbleTo = (float)Rnd.NextDouble() * Mathf.PI * 2f;
		}
	}

	/// <summary>Redraws the dream blocks for this frame; cam is the view's top-left in Celeste pixels.</summary>
	private void UpdateDreams(Vector2 cam, float dt)
	{
		if (dreamLooks.Count == 0)
		{
			return;
		}
		if (dreamParticles == null && AtlasTex("objects/dreamblock/particles", out CelesteAssets.SubTex st, out CelesteAssets.Page pg))
		{
			dreamParticlesW = st.FullW;
			dreamParticles = new Color32[st.FullW * st.FullH];
			for (int y = 0; y < st.FullH; y++)
			{
				for (int x = 0; x < st.FullW; x++)
				{
					dreamParticles[y * st.FullW + x] = Px(st, pg, x, y);
				}
			}
		}
		Color32 white = new Color32(255, 255, 255, 255);
		Color32 black = new Color32(0, 0, 0, 255);
		foreach (DreamLook d in dreamLooks)
		{
			d.AnimTimer += 6f * dt;
			d.WobbleEase += dt * 2f;
			if (d.WobbleEase > 1f)
			{
				d.WobbleEase = 0f;
				d.WobbleFrom = d.WobbleTo;
				d.WobbleTo = (float)d.Rnd.NextDouble() * Mathf.PI * 2f;
			}
			int tw = d.W + DreamLook.Margin * 2;
			int th = d.H + DreamLook.Margin * 2;
			Color32[] buf = d.Buf;
			Array.Clear(buf, 0, buf.Length);
			void Put(int x, int y, Color32 c)
			{
				// x, y: Celeste pixels from the block's top-left
				int bx = x + DreamLook.Margin;
				int by = y + DreamLook.Margin;
				if (bx >= 0 && by >= 0 && bx < tw && by < th)
				{
					buf[(th - 1 - by) * tw + bx] = c;
				}
			}
			for (int y = 0; y < d.H; y++)
			{
				for (int x = 0; x < d.W; x++)
				{
					Put(x, y, black);
				}
			}
			// the particles: three layers drifting with the camera, wrapped back inside
			if (dreamParticles != null)
			{
				for (int i = 0; i < d.PPos.Length; i++)
				{
					int layer = d.PLayer[i];
					Vector2 p = d.Px + d.PPos[i] + cam * (0.3f + 0.25f * layer);
					p.x = d.Px.x + Mathf.Repeat(p.x - d.Px.x, d.W);
					p.y = d.Px.y + Mathf.Repeat(p.y - d.Px.y, d.H);
					int frame;
					switch (layer)
					{
					case 0:
						frame = 3 - (int)((d.PTime[i] * 4f + d.AnimTimer) % 4f);
						break;
					case 1:
						frame = 1 + (int)((d.PTime[i] * 2f + d.AnimTimer) % 2f);
						break;
					default:
						frame = 2;
						break;
					}
					// the four frames are the sheet's (14,0), (7,0), (0,0), (7,0)
					int sx = frame == 0 ? 14 : frame == 2 ? 0 : 7;
					Vector2 local = p - d.Px;
					if (local.x < 2f || local.y < 2f || local.x >= d.W - 2f || local.y >= d.H - 2f)
					{
						continue;
					}
					int ox = Mathf.FloorToInt(local.x - 3.5f);
					int oy = Mathf.FloorToInt(local.y - 3.5f);
					Color32 tint = d.PCol[i];
					for (int yy = 0; yy < 7; yy++)
					{
						for (int xx = 0; xx < 7; xx++)
						{
							Color32 t = dreamParticles[yy * dreamParticlesW + sx + xx];
							if (t.a > 0)
							{
								Put(ox + xx, oy + yy, new Color32((byte)(t.r * tint.r / 255), (byte)(t.g * tint.g / 255), (byte)(t.b * tint.b / 255), 255));
							}
						}
					}
				}
			}
			// the wobbling edge: a white line with two black ones inside it
			Wobble(d, Put, new Vector2(0f, 0f), new Vector2(d.W, 0f), 0f, white, black);
			Wobble(d, Put, new Vector2(d.W, 0f), new Vector2(d.W, d.H), 0.7f, white, black);
			Wobble(d, Put, new Vector2(d.W, d.H), new Vector2(0f, d.H), 1.5f, white, black);
			Wobble(d, Put, new Vector2(0f, d.H), new Vector2(0f, 0f), 2.5f, white, black);
			foreach (Vector2Int c in new Vector2Int[4] { new Vector2Int(0, 0), new Vector2Int(d.W - 2, 0), new Vector2Int(0, d.H - 2), new Vector2Int(d.W - 2, d.H - 2) })
			{
				Put(c.x, c.y, white);
				Put(c.x + 1, c.y, white);
				Put(c.x, c.y + 1, white);
				Put(c.x + 1, c.y + 1, white);
			}
			d.Tex.SetPixels32(buf);
			d.Tex.Apply(updateMipmaps: false);
		}
	}

	private static float LineAmplitude(float seed, float index)
	{
		return (float)(Math.Sin(seed + index / 16f + Math.Sin(seed * 2f + index / 32f) * 6.2831854820251465) + 1.0) * 1.5f;
	}

	private static void Wobble(DreamLook d, Action<int, int, Color32> put, Vector2 from, Vector2 to, float offset, Color32 line, Color32 back)
	{
		float len = (to - from).magnitude;
		Vector2 dir = (to - from) / len;
		Vector2 n = new Vector2(dir.y, -dir.x);
		float last = 0f;
		const int step = 16;
		for (int i = 2; i < len - 2f; i += step)
		{
			float amp = Mathf.Lerp(LineAmplitude(d.WobbleFrom + offset, i), LineAmplitude(d.WobbleTo + offset, i), d.WobbleEase);
			if (i + step >= len)
			{
				amp = 0f;
			}
			float seg = Math.Min(step, len - 2f - i);
			Vector2 a = from + dir * i + n * last;
			Vector2 b = from + dir * (i + seg) + n * amp;
			Line(put, a - n, b - n, back);
			Line(put, a - n * 2f, b - n * 2f, back);
			Line(put, a, b, line);
			last = amp;
		}
	}

	private static void Line(Action<int, int, Color32> put, Vector2 a, Vector2 b, Color32 c)
	{
		// Monocle's Draw.Line is a 1-pixel line; this steps along the longer axis
		Vector2 d = b - a;
		int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Math.Abs(d.x), Math.Abs(d.y))));
		for (int i = 0; i < steps; i++)
		{
			Vector2 p = a + d * (i / (float)steps);
			put(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), c);
		}
	}

	private void BuildSky()
	{
		Texture2D white = Texture2D.whiteTexture;
		Sprite px = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);
		sky = Renderer("Sky", px, Origin + new Vector3(0f, 0f, heroZ + 60f), -400);
		sky.color = new Color(0.07f, 0.05f, 0.14f, 1f);
		sky.transform.localScale = new Vector3(600f, 400f, 1f);
		// stars at depth: the perspective camera gives them parallax
		string[] names = new string[6] { "bgs/02/stars/a00", "bgs/02/stars/a01", "bgs/02/stars/a02", "bgs/02/stars/b00", "bgs/02/stars/c00", "bgs/02/stars/c01" };
		System.Random rnd = new System.Random(1);
		Rect area = rooms[0].Bounds;
		foreach (Room r in rooms)
		{
			area = Rect.MinMaxRect(Math.Min(area.xMin, r.Bounds.xMin), Math.Min(area.yMin, r.Bounds.yMin), Math.Max(area.xMax, r.Bounds.xMax), Math.Max(area.yMax, r.Bounds.yMax));
		}
		for (int i = 0; i < 220; i++)
		{
			Sprite s = AtlasSprite(names[rnd.Next(names.Length)], new Vector2(0.5f, 0.5f));
			if (s == null)
			{
				continue;
			}
			float depth = 6f + (float)rnd.NextDouble() * 30f;
			float grow = 1f + depth / 20f;
			Vector3 at = new Vector3(area.center.x + ((float)rnd.NextDouble() - 0.5f) * (area.width + 40f) * grow, area.center.y + ((float)rnd.NextDouble() - 0.5f) * (area.height + 30f) * grow, heroZ + depth);
			SpriteRenderer sr = Renderer("Star", s, at, -300);
			sr.color = new Color(0.75f, 0.8f, 1f, 0.35f + (float)rnd.NextDouble() * 0.5f);
			stars.Add(sr);
		}
	}

	// ---- playing --------------------------------------------------------------------------------------------------

	private void Update()
	{
		float dt = Time.unscaledDeltaTime;
		HeroController h = HeroController.UnsafeInstance;
		GameManager gm = GameManager.SilentInstance;
		if (gm != null && h != null)
		{
			ScanForEnemy(gm, dt);
		}
		if (Active && (h == null || gm == null || gm.sceneName != returnScene))
		{
			Plugin.Log.LogInfo("Tech tutorial: the scene changed, rooms put away");
			Cleanup();
		}
		// every file that hasn't been asked yet is asked (user 2026-10-20: new or deleted files always get it), and
		// she can't move until it's answered
		// from the moment a file that hasn't been asked is in control, she's held still (Silksong's own no-control
		// state, so no running or dashing at all) through the Tutorial postcard until the question is answered
		bool pre = !Active && wipeDir == 0 && h != null && gm != null && gm.GameState == GlobalEnums.GameState.PLAYING && TechTips.Slot >= 0 && !TechTips.Flag("asked") && !h.cState.transitioning && !h.cState.dead && !h.cState.hazardDeath && !h.playerData.atBench && !gaveUp;
		if (pre)
		{
			if (!h.controlReqlinquished && h.acceptingInput)
			{
				h.RelinquishControl();
				holding = true;
			}
		}
		else if (holding && ask == Ask.None)
		{
			// asked (or Silksong took over for a transition or a death: it gives control back itself)
			if (h != null && h.controlReqlinquished && !h.cState.transitioning && !h.cState.dead && !h.cState.hazardDeath && gm != null && gm.GameState == GlobalEnums.GameState.PLAYING)
			{
				h.RegainControl();
			}
			holding = false;
		}
		if (h == null)
		{
			holding = false;
			gaveUp = false;
		}
		bool due = !Active && ask == Ask.None && wipeDir == 0 && h != null && gm != null && gm.GameState == GlobalEnums.GameState.PLAYING && !gm.isPaused && TechTips.Slot >= 0 && !TechTips.Flag("asked") && !h.cState.transitioning && !h.cState.dead && !h.playerData.atBench && PostcardView.TutorialDone && !AssistMode.IsOpen && !StartingAbilities.IsOpen;
		waitFor = due ? waitFor + dt : 0f;
		waitingToAsk = due && waitFor < 12f;
		if (due && waitFor >= 12f)
		{
			gaveUp = true;   // something kept the question from opening: never leave her stuck
			Plugin.Log.LogWarning("Tech tutorial: the question couldn't open, she can move");
		}
		if (due && (!h.controlReqlinquished || holding) && waitFor > 0.3f)
		{
			pendingOffer = false;
			waitingToAsk = false;
			OpenAsk(Ask.Familiar);
		}
		if (pendingLeave && Active && ask == Ask.None && wipeDir == 0 && gm != null && !gm.isPaused)
		{
			pendingLeave = false;
			OpenAsk(Ask.Leave);
		}
		if (!Active)
		{
			pendingLeave = false;
		}
		if (ask != Ask.None)
		{
			DriveAsk(dt);
		}
		if (wipeDir != 0)
		{
			wipe += dt / 0.35f * (wipeDir > 0 ? 1f : 1f);
			if (wipeDir > 0 && wipe >= 1f)
			{
				Action a = wipeAction;
				wipeAction = null;
				wipe = 0f;
				wipeDir = -1;
				try
				{
					a?.Invoke();
				}
				catch (Exception e)
				{
					Plugin.Log.LogError("Tech tutorial: " + e);
				}
			}
			else if (wipeDir < 0 && wipe >= 1f)
			{
				wipeDir = 0;
				wipe = 0f;
			}
		}
		if (!Active || h == null)
		{
			return;
		}
		MadelineController c = Plugin.Controller;
		Collider2D col = Col(h);
		if (col == null)
		{
			return;
		}
		Bounds b = col.bounds;
		Rect me = new Rect(b.min.x + 0.05f, b.min.y + 0.05f, b.size.x - 0.1f, b.size.y - 0.1f);
		// dream blocks let a dash through
		bool inside = false;
		foreach (Rect r in dreamRects)
		{
			inside |= r.Overlaps(me);
		}
		bool pass = c.State == 2 || c.InDream;
		foreach (Collider2D d in dreams)
		{
			if (d != null)
			{
				Physics2D.IgnoreCollision(col, d, pass);
			}
		}
		c.DreamTick(inside);
		if (wipeDir != 0)
		{
			return;
		}
		WatchEnemies(b);
		WatchSections(b);
		// start points touched become where she comes back
		for (int i = 0; i < spawns.Count; i++)
		{
			if (i != spawn && Mathf.Abs(spawns[i].x - b.center.x) < 1.5f && Mathf.Abs(spawns[i].y - b.min.y) < 2f)
			{
				if (i > spawn)
				{
					spawn = i;
				}
			}
		}
		foreach (Hazard z in hazards)
		{
			// Celeste's rule: spikes hurt unless she's moving away from them
			if (z.R.Overlaps(me) && Vector2.Dot(c.Speed, z.Against) >= 0f)
			{
				Die();
				return;
			}
		}
		if (RoomAt(b.center) == null)
		{
			// outside every room: fine above one (Celeste lets her go over the top), a fall anywhere else
			bool above = false;
			foreach (Room r in rooms)
			{
				above |= b.center.x >= r.Bounds.xMin && b.center.x <= r.Bounds.xMax && b.center.y > r.Bounds.yMax;
			}
			if (!above)
			{
				Die();
				return;
			}
		}
		springCooldown -= dt;
		foreach (Rect s in springs)
		{
			if (springCooldown <= 0f && s.Overlaps(me))
			{
				springCooldown = 0.2f;
				c.Bounce(new Vector2(240f, -140f));
				Sfx.Play(Sounds.JumpSuperwall.Id);
			}
		}
	}

	private void Die()
	{
		Sfx.Play(Sounds.Death.Id);
		StartWipe(delegate
		{
			HeroController h = HeroController.UnsafeInstance;
			if (h != null && Active)
			{
				Place(h, spawns[Mathf.Clamp(spawn, 0, spawns.Count - 1)]);
				camSnap = true;
				if (enemies.Count == 0 || enemies.Exists((HealthManager e) => e == null || e.GetIsDead()))
				{
					SpawnEnemies();
				}
			}
		});
	}

	private Room RoomAt(Vector2 p)
	{
		foreach (Room r in rooms)
		{
			if (r.Bounds.Contains(p))
			{
				return r;
			}
		}
		return null;
	}

	private void LateUpdate()
	{
		if (!Active)
		{
			return;
		}
		HeroController h = HeroController.UnsafeInstance;
		GameCameras gc = GameCameras.SilentInstance;
		if (h == null || gc == null || gc.cameraController == null || gc.mainCamera == null)
		{
			return;
		}
		Camera cam = gc.mainCamera;
		Transform ct = gc.cameraController.transform;
		Vector3 hp = h.transform.position;
		float dist = Math.Abs(cam.transform.position.z - heroZ);
		float halfH = cam.orthographic ? cam.orthographicSize : dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
		float halfW = halfH * cam.aspect;
		Room room = RoomAt(hp);
		Vector2 want = new Vector2(hp.x, hp.y + 0.5f);
		if (room != null)
		{
			Rect r = room.Bounds;
			want.x = r.width <= halfW * 2f ? r.center.x : Mathf.Clamp(want.x, r.xMin + halfW, r.xMax - halfW);
			want.y = r.height <= halfH * 2f ? r.center.y : Mathf.Clamp(want.y, r.yMin + halfH, r.yMax - halfH);
		}
		if (camSnap)
		{
			camPos = want;
			camSnap = false;
		}
		else
		{
			camPos = Vector2.Lerp(camPos, want, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 7f));
		}
		Vector3 lens = cam.transform.position - ct.position;
		ct.position = new Vector3(camPos.x - lens.x, camPos.y - lens.y, ct.position.z);
		if (sky != null)
		{
			sky.transform.position = new Vector3(camPos.x, camPos.y, sky.transform.position.z);
		}
		UpdateDreams(new Vector2((camPos.x - halfW - Origin.x) * 8f, (Origin.y - (camPos.y + halfH)) * 8f), Time.deltaTime);
		float t = Time.unscaledTime;
		for (int i = 0; i < stars.Count; i++)
		{
			Color sc = stars[i].color;
			sc.a = 0.45f + 0.35f * Mathf.Sin(t * (0.8f + i % 7 * 0.3f) + i);
			stars[i].color = sc;
		}
	}


	// ---- the practice room's enemies (user 2026-10-20) --------------------------------------------------------------
	// Silksong's enemies live in their levels. The ground moss one (MossBone Crawler) is first met in Tut_01b, so when
	// none has been seen yet, Tut_01b is loaded in the background, one crawler is copied (hidden) and the rest of that
	// level is switched off and unloaded again. Two copies stand on the practice floor: they take hits but never die.

	private static readonly string[] EnemyKeys = new string[7] { "mossgrub", "moss grub", "moss_grub", "mossbone", "moss bone", "mosscreep", "moss creep" };

	private static GameObject enemySource;

	private static int enemyScore;

	private string scannedScene;

	private float sceneFor;

	private readonly List<HealthManager> enemies = new List<HealthManager>();

	private readonly List<Vector3> enemyAt = new List<Vector3>();

	private static readonly float[] EnemyPx = new float[2] { 232f, 296f };

	private const float EnemyFloorPx = 136f;

	// the practice room: built after Farewell's room out of its tiles, a long floor with the two enemies, entered from
	// the left at that room's exit; its right end leaves the tutorial
	private const float PracticeW = 480f;

	private const float PracticeH = 184f;

	private Vector2 lastOff;

	private float cutPx = 99999f;

	private float exitX;

	private float exitY;

	private float lastExitY;

	private float lastW;

	private Vector2 practiceOff;

	private Vector3 stationStart;

	private float exitAt;

	private Vector3 exitSign;

	private static readonly string[] PracticeIds = new string[3] { "super", "hyper", "wavedash" };

	private readonly HashSet<string> practiced = new HashSet<string>();

	private float lastEnemyHit = -10f;

	private float lastCredit = -10f;

	/// <summary>The moves done on the practice enemies so far, for the bubble.</summary>
	public static string PracticedList
	{
		get
		{
			if (Instance == null)
			{
				return "";
			}
			List<string> l = new List<string>();
			foreach (string id in PracticeIds)
			{
				if (Instance.practiced.Contains(id))
				{
					l.Add(TechTips.Lessons[Array.FindIndex(TechTips.Lessons, (TechTips.Lesson x) => x.Id == id)].Name);
				}
			}
			return string.Join(", ", l.ToArray());
		}
	}

	private void BuildPractice(Dictionary<char, TechRoomData.Tileset> fgSets)
	{
		// the last Farewell room is left by its right edge, standing where its rightmost start point is
		practiceOff = new Vector2(lastOff.x + lastW, lastOff.y + lastExitY - EnemyFloorPx);
		int w = (int)PracticeW / 8;
		int h = (int)PracticeH / 8;
		char[,] g = new char[w, h];
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				bool floor = y >= (int)EnemyFloorPx / 8;
				bool wall = x >= w - 2;
				bool roof = y < 2 && x > 3;
				g[x, y] = floor || wall || roof ? 'm' : '0';
			}
		}
		Vector3 tl = World(practiceOff, 0f, 0f);
		rooms.Add(new Room { Name = "practice", Bounds = new Rect(tl.x, tl.y - PracticeH / 8f, PracticeW / 8f, PracticeH / 8f) });
		TileLayer(g, fgSets, practiceOff, w, h, -0.02f, 20, "practice");
		Colliders(g, practiceOff, w, h);
		stationStart = World(practiceOff, 48f, EnemyFloorPx);
		spawns.Add(stationStart);
		exitAt = World(practiceOff, PracticeW - 48f, 0f).x;
		exitSign = World(practiceOff, PracticeW - 36f, EnemyFloorPx - 20f);
		practiced.Clear();
	}

	// ---- the super and hyper gaps (user 2026-10-20: instead of the wall bounce) ------------------------------------
	// a gap a run-jump can't cross but a super can (96 px; a super goes ~126), then a gap under a low ceiling (24 px
	// over the floor) down to a lower floor (24 px), 120 px wide: a hyper goes ~164, a super only ~107 under it

	private const float SecW = 760f;

	private const float SecFloor = 136f;

	private Vector2 secOff;

	private bool secFromA;

	private bool secFromB;

	private readonly HashSet<string> secDone = new HashSet<string>();

	private void BuildSections(Dictionary<char, TechRoomData.Tileset> fgSets)
	{
		secOff = new Vector2(lastOff.x + lastW, lastOff.y + lastExitY - SecFloor);
		int w = (int)SecW / 8;
		int h = (int)PracticeH / 8;
		char[,] g = new char[w, h];
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				int px = x * 8;
				int py = y * 8;
				bool a = px < 200 && py >= SecFloor;
				bool b = px >= 296 && px < 480 && py >= SecFloor;
				bool ceiling = px >= 400 && px < 640 && py >= 88 && py < 112;
				bool c = px >= 600 && py >= SecFloor + 24f;
				g[x, y] = a || b || ceiling || c ? 'm' : '0';
			}
		}
		Vector3 tl = World(secOff, 0f, 0f);
		rooms.Add(new Room { Name = "sections", Bounds = new Rect(tl.x, tl.y - PracticeH / 8f, SecW / 8f, PracticeH / 8f) });
		TileLayer(g, fgSets, secOff, w, h, -0.02f, 20, "sections");
		Colliders(g, secOff, w, h);
		spawns.Add(World(secOff, 40f, SecFloor));
		spawns.Add(World(secOff, 456f, SecFloor));
		spawns.Add(World(secOff, SecW - 16f, SecFloor + 24f));
		lastOff = secOff;
		lastW = SecW;
		lastExitY = SecFloor + 24f;
		secDone.Clear();
	}

	public const float HyperDropPx = 24f;

	/// <summary>Where the gap's ghost stands (world, feet): before the edge, so its move lands on the far side.</summary>
	public static Vector3 SectionGhostAt(int lesson)
	{
		if (Instance == null)
		{
			return Vector3.zero;
		}
		return Instance.World(Instance.secOff, lesson == 0 ? 176f : 456f, SecFloor);
	}

	/// <summary>Past the tutorial's start floor (at or after Farewell's wavedash run).</summary>
	public static bool PastStart(float worldX)
	{
		return Active && Playbacks.Count > 0 && worldX >= Playbacks[0].MinX - 1f;
	}

	/// <summary>The move the gap she's at is for (-1: not at one).</summary>
	public static int SectionLesson(float worldX)
	{
		if (Instance == null || !Active)
		{
			return -1;
		}
		Room r = Instance.rooms.Find((Room q) => q.Name == "sections");
		if (r == null || worldX < r.Bounds.xMin || worldX > r.Bounds.xMax)
		{
			return -1;
		}
		float local = (worldX - r.Bounds.xMin) * 8f;
		return local < 440f ? 0 : 1;
	}

	private void WatchSections(Bounds b)
	{
		Room r = rooms.Find((Room q) => q.Name == "sections");
		if (r == null || !r.Bounds.Contains(b.center) || !Plugin.Controller.OnGround)
		{
			return;
		}
		float local = (b.center.x - r.Bounds.xMin) * 8f;
		if (local < 200f)
		{
			secFromA = true;
		}
		else if (local >= 304f && local < 480f)
		{
			if (secFromA)
			{
				secFromA = false;
				Crossed("super");
			}
			secFromB = true;
		}
		else if (local >= 608f && secFromB)
		{
			secFromB = false;
			Crossed("hyper");
		}
	}

	private void Crossed(string id)
	{
		string name = TechTips.Lessons[Array.FindIndex(TechTips.Lessons, (TechTips.Lesson x) => x.Id == id)].Name;
		bool used = TechTips.LastTechId == id && Time.unscaledTime - TechTips.LastTechAt < 2f;
		if (!used)
		{
			TechTips.Note("You made it! Now try it with a " + name.ToLowerInvariant() + ".", good: true);
			return;
		}
		if (secDone.Add(id))
		{
			TechTips.DoneAt = Time.unscaledTime;
			TechTips.DoneName = name + "!";
			TechTips.DoneOver = TechTips.Head;
			Sfx.Play(Sounds.FeatherGet.Id);
		}
		else
		{
			Sfx.Play(Sounds.Refill.Id);
		}
		TechTips.Note(id == "super" ? "Super! Next: the hyper, under the low ceiling." : "Hyper! On to the practice room.", good: true);
	}

	private static bool enemyHintOn;

	/// <summary>She's in the practice room (the bubble says what to do there).</summary>
	public static bool EnemyHint => Active && enemyHintOn;

	/// <summary>The practice enemies: hits land, but they never die (user 2026-10-20).</summary>
	public static bool IsTutorialEnemy(HealthManager hm) => Active && Instance != null && hm != null && Instance.enemies.Contains(hm);

	public static void OnEnemyHit(HealthManager hm)
	{
		if (Instance != null)
		{
			Instance.lastEnemyHit = Time.unscaledTime;
			hm.hp = Math.Max(hm.hp, 9999);
		}
	}

	private static int ScoreOf(string name)
	{
		string n = name.ToLowerInvariant();
		if (n.Contains("mother") || n.Contains("boss") || Array.Find(EnemyKeys, (string k) => n.Contains(k)) == null)
		{
			return 0;
		}
		if (n.Contains("cocoon"))
		{
			return 0;   // not an enemy to practise on
		}
		if (n.Contains("crawler") || n.Contains("grub") || n.Contains("creep"))
		{
			return 4;   // the regular ground moss one (user 2026-10-20), frozen in place
		}
		return n.Contains("fly") ? 1 : 2;
	}

	private static void Keep(GameObject src, string from)
	{
		bool was = src.activeSelf;
		src.SetActive(false);
		GameObject c = UnityEngine.Object.Instantiate(src);
		src.SetActive(was);
		c.name = "SilkesteTutorialEnemy (" + src.name + ")";
		// a copy mustn't touch the save: no persistent "this one is dead" flags
		foreach (Component comp in c.GetComponentsInChildren<Component>(includeInactive: true))
		{
			if (comp != null && comp.GetType().Name.StartsWith("Persistent", StringComparison.Ordinal))
			{
				UnityEngine.Object.DestroyImmediate(comp);
			}
		}
		UnityEngine.Object.DontDestroyOnLoad(c);
		if (enemySource != null)
		{
			UnityEngine.Object.Destroy(enemySource);
		}
		enemySource = c;
		enemyScore = ScoreOf(src.name);
		Plugin.Log.LogInfo("Tech tutorial: kept a copy of " + src.name + " from " + from);
		if (Active && Instance != null)
		{
			Instance.SpawnEnemies();
		}
	}

	private void ScanForEnemy(GameManager gm, float dt)
	{
		if (Active || gm.GameState != GlobalEnums.GameState.PLAYING || gm.sceneName == scannedScene)
		{
			sceneFor = 0f;
			return;
		}
		sceneFor += dt;
		if (sceneFor < 1.5f)
		{
			return;
		}
		scannedScene = gm.sceneName;
		HealthManager[] all = UnityEngine.Object.FindObjectsOfType<HealthManager>();
		if (MadelineTouches.DevOn && all.Length > 0)
		{
			HashSet<string> names = new HashSet<string>();
			foreach (HealthManager hm in all)
			{
				names.Add(hm.gameObject.name);
			}
			Plugin.Log.LogInfo("Tech tutorial: enemies in " + gm.sceneName + ": " + string.Join(", ", new List<string>(names).ToArray()));
		}
		HealthManager best = null;
		int bestScore = enemyScore;
		foreach (HealthManager hm in all)
		{
			int s = hm.GetIsDead() ? 0 : ScoreOf(hm.gameObject.name);
			if (s > bestScore)
			{
				best = hm;
				bestScore = s;
			}
		}
		if (best != null)
		{
			try
			{
				Keep(best.gameObject, gm.sceneName);
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning("Tech tutorial: couldn't copy " + best.gameObject.name + ": " + e.Message);
			}
		}
	}

	private static bool preloading;

	private const string PreloadScene = "Tut_02";   // MossBone Crawlers

	private static AsyncOperationHandle<SceneInstance> preHandle;

	/// <summary>Loads Tut_01b in the background for one MossBone Crawler, when none has been seen yet.</summary>
	public static void PreloadCrawler()
	{
		GameManager gm = GameManager.SilentInstance;
		if (preloading || enemyScore >= 5 || gm == null || gm.sceneName == PreloadScene)
		{
			return;
		}
		preloading = true;
		try
		{
			SceneManager.sceneLoaded += OnPreloaded;
			preHandle = Addressables.LoadSceneAsync("Scenes/" + PreloadScene, LoadSceneMode.Additive);
			preHandle.Completed += delegate (AsyncOperationHandle<SceneInstance> h)
			{
				SceneManager.sceneLoaded -= OnPreloaded;
				if (h.Status == AsyncOperationStatus.Succeeded)
				{
					Addressables.UnloadSceneAsync(h);
				}
				preloading = false;
			};
			Plugin.Log.LogInfo("Tech tutorial: loading " + PreloadScene + " in the background for a MossBone Crawler");
		}
		catch (Exception e)
		{
			SceneManager.sceneLoaded -= OnPreloaded;
			preloading = false;
			Plugin.Log.LogWarning("Tech tutorial: background load: " + e.Message);
		}
	}

	private static void OnPreloaded(Scene s, LoadSceneMode mode)
	{
		if (s.name != PreloadScene)
		{
			return;
		}
		SceneManager.sceneLoaded -= OnPreloaded;
		GameObject[] roots = s.GetRootGameObjects();
		try
		{
			foreach (GameObject r in roots)
			{
				foreach (HealthManager hm in r.GetComponentsInChildren<HealthManager>(includeInactive: true))
				{
					// a crawler on a floor (not turned onto a wall or the ceiling) is the one to copy
					bool upright = Mathf.Abs(Mathf.DeltaAngle(hm.transform.eulerAngles.z, 0f)) < 10f && hm.transform.lossyScale.y > 0f;
					int sc = ScoreOf(hm.gameObject.name) + (upright ? 1 : 0);
					if (sc > enemyScore)
					{
						Keep(hm.gameObject, PreloadScene + " (background)");
						enemyScore = sc;
					}
				}
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Tech tutorial: background copy: " + e.Message);
		}
		// nothing of that level may run: off before its Start, then it's unloaded
		foreach (GameObject r in roots)
		{
			r.SetActive(false);
		}
	}

	private void SpawnEnemies()
	{
		foreach (HealthManager hm in enemies)
		{
			if (hm != null)
			{
				UnityEngine.Object.Destroy(hm.gameObject);
			}
		}
		enemies.Clear();
		enemyAt.Clear();
		if (enemySource == null || root == null)
		{
			return;
		}
		foreach (float px in EnemyPx)
		{
			try
			{
				GameObject e = UnityEngine.Object.Instantiate(enemySource, root.transform);
				e.name = "TutorialEnemy";
				foreach (PlayMakerFSM f in e.GetComponentsInChildren<PlayMakerFSM>(includeInactive: true))
				{
					f.enabled = false;   // it stands still
				}
				foreach (MonoBehaviour mb in e.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
				{
					string tn = mb.GetType().Name;
					if (tn.Contains("Crawl") || tn.Contains("Walk") || tn.Contains("Patrol") || tn.Contains("Climb") || tn.Contains("Wander"))
					{
						mb.enabled = false;
					}
				}
				Rigidbody2D rb = e.GetComponent<Rigidbody2D>();
				if (rb != null)
				{
					rb.isKinematic = true;
					rb.velocity = Vector2.zero;
				}
				Vector3 at = World(practiceOff, px, EnemyFloorPx);
				e.transform.position = new Vector3(at.x, at.y, heroZ);
				// the copy may come from a wall or a ceiling: stand it upright on the floor (user 2026-10-20)
				e.transform.rotation = Quaternion.identity;
				Vector3 sc = e.transform.localScale;
				e.transform.localScale = new Vector3(Math.Abs(sc.x), Math.Abs(sc.y), sc.z);
				e.SetActive(true);
				Collider2D c = e.GetComponent<Collider2D>();
				if (c != null)
				{
					float lift = enemyScore <= 1 ? 0.6f : 0f;   // a flying one hovers a little
					e.transform.position += new Vector3(at.x - c.bounds.center.x, at.y - c.bounds.min.y + lift, 0f);
				}
				HealthManager hm = e.GetComponent<HealthManager>();
				if (hm != null)
				{
					hm.hp = 9999;
					enemies.Add(hm);
					enemyAt.Add(e.transform.position);
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Tech tutorial: enemy: " + ex.Message);
			}
		}
	}

	private void WatchEnemies(Bounds b)
	{
		Room here = RoomAt(b.center);
		enemyHintOn = here != null && here.Name == "practice";
		for (int i = 0; i < enemies.Count; i++)
		{
			HealthManager hm = enemies[i];
			if (hm != null)
			{
				hm.hp = Math.Max(hm.hp, 9999);
				hm.transform.position = enemyAt[i];   // they don't move (user 2026-10-20)
				Rigidbody2D rb = hm.GetComponent<Rigidbody2D>();
				if (rb != null)
				{
					rb.velocity = Vector2.zero;
				}
			}
		}
		if (!enemyHintOn || wipeDir != 0)
		{
			return;
		}
		// a move done on an enemy: its dash hit one, and the jump came right before or after
		float now = Time.unscaledTime;
		if (TechTips.LastTechAt > lastCredit && Math.Abs(TechTips.LastTechAt - lastEnemyHit) < 0.6f && now - TechTips.LastTechAt < 1f && Array.IndexOf(PracticeIds, TechTips.LastTechId) >= 0)
		{
			lastCredit = TechTips.LastTechAt;
			string id = TechTips.LastTechId;
			string name = TechTips.Lessons[Array.FindIndex(TechTips.Lessons, (TechTips.Lesson x) => x.Id == id)].Name;
			if (practiced.Add(id))
			{
				TechTips.DoneAt = now;
				TechTips.DoneName = name + "!";
				TechTips.DoneOver = TechTips.Head;
				Sfx.Play(Sounds.FeatherGet.Id);
				TechTips.Note(practiced.Count >= PracticeIds.Length ? "All three! Keep going, or go right to leave." : "Nice " + name.ToLowerInvariant() + "!", good: true);
			}
			else
			{
				Sfx.Play(Sounds.Refill.Id);
				TechTips.Note("Nice " + name.ToLowerInvariant() + "!", good: true);
			}
		}
		// all the way right leaves the tutorial
		if (b.center.x > exitAt)
		{
			Leave(finished: true);
		}
	}

	/// <summary>Hurt in the Tech Tutorial: no masks lost, just back to the last start point (like Celeste).</summary>
	public static void Hurt()
	{
		if (Instance != null && Active && Instance.wipeDir == 0)
		{
			if (enemyHintOn)
			{
				// the practice room: no going back, just a reminder (user 2026-10-20)
				if (Time.unscaledTime - TechTips.NoteAt > 1f)
				{
					TechTips.Note("Ouch! Dash through them instead: nothing can hurt her while she dashes.", good: false);
				}
				return;
			}
			Instance.Die();
		}
	}

	// ---- leaving midway (user 2026-10-20: from the pause menu, with a warning) ----------------------------------------

	private bool pendingLeave;

	/// <summary>The pause menu's Leave Tutorial: unpause, then ask (with what she hasn't learned yet).</summary>
	public static void AskLeave()
	{
		if (Instance == null || !Active)
		{
			return;
		}
		Instance.pendingLeave = true;
		try
		{
			UIManager.instance.TogglePauseGame();
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Tech tutorial: unpause: " + e.Message);
		}
	}

	private static string NotLearned()
	{
		List<string> l = new List<string>();
		for (int i = 0; i < TechTips.Lessons.Length; i++)
		{
			if (!TechTips.Done(i) && Array.IndexOf(PracticeIds, TechTips.Lessons[i].Id) >= 0)
			{
				l.Add(TechTips.Lessons[i].Name);
			}
		}
		return string.Join(", ", l.ToArray());
	}

	private void DrawExitSign(GuiArt art)
	{
		Camera cam = Camera.main;
		if (cam == null || !enemyHintOn)
		{
			return;
		}
		float k = Screen.height / 1080f;
		Vector3 sp = cam.WorldToScreenPoint(exitSign);
		if (sp.z < 0f)
		{
			return;
		}
		Vector2 c = new Vector2(sp.x, Screen.height - sp.y) / k;
		float bob = Mathf.Sin(Time.unscaledTime * 4f) * 6f;
		string t = "EXIT >";
		float s = 0.6f;
		float w = art.Measure(t) * s;
		art.DrawOutlined(t, c.x - w / 2f + bob, c.y - art.LineHeight * s, s, new Color(1f, 0.85f, 0.35f, 1f), Color.black, 3f);
		string t2 = "leaves the tutorial";
		float s2 = 0.38f;
		art.DrawOutlined(t2, c.x - art.Measure(t2) * s2 / 2f, c.y + 6f, s2, new Color(1f, 1f, 1f, 0.9f), Color.black, 2f);
	}

	// ---- drawing the prompt and the wipe --------------------------------------------------------------------------

	private void OnGUI()
	{
		if (Event.current.type != EventType.Repaint)
		{
			return;
		}
		float k = Screen.height / 1080f;
		float sw = Screen.width / k;
		Matrix4x4 old = GUI.matrix;
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(k, k, 1f));
		GUI.depth = -2;
		if (Active && AssistMode.Art != null && wipeDir == 0)
		{
			DrawExitSign(AssistMode.Art);
		}
		if (ask != Ask.None)
		{
			DrawAsk(sw);
		}
		if (wipeDir != 0)
		{
			// Celeste's slanted wipe: bars sweep across, then away
			float p = Mathf.Clamp01(wipe);
			float cover = wipeDir > 0 ? p : 1f - p;
			GUI.color = Color.black;
			int bars = 12;
			float barH = 1080f / bars + 2f;
			for (int i = 0; i < bars; i++)
			{
				float lag = i / (float)bars * 0.3f;
				float f = Mathf.Clamp01((cover - lag) / 0.7f);
				float wd = (sw + 200f) * f;
				float x0 = wipeDir > 0 ? -100f : sw + 100f - wd;
				GUI.DrawTexture(new Rect(x0 + (wipeDir > 0 ? 0f : 0f), i * (1080f / bars), wd, barH), Texture2D.whiteTexture);
			}
		}
		GUI.matrix = old;
		GUI.color = Color.white;
	}

	private void DrawAsk(float sw)
	{
		GuiArt art = AssistMode.Art;
		if (art == null)
		{
			return;
		}
		float e = Mathf.Clamp01(askT / 0.25f);
		GUI.color = new Color(0f, 0f, 0f, 0.45f * e);
		GUI.DrawTexture(new Rect(0f, 0f, sw, 1080f), Texture2D.whiteTexture);
		string nl = NotLearned();
		string q = ask == Ask.Familiar ? "Do you know Celeste's tech?\nSupers, hypers and wavedashes." : ask == Ask.Sure ? "Are you sure?\nSilkeste is built around these moves, and the tutorial teaches them in a few minutes. It's recommended, even if you've played Celeste." : nl.Length > 0 ? "Leave the tutorial?\nYou haven't learned yet: " + nl + ". Without them a lot of Silkeste is much harder. You can come back any time: Settings > Game > Tech Tutorial." : "Leave the tutorial?\nYou can come back any time: Settings > Game > Tech Tutorial.";
		string[] opts = ask == Ask.Familiar ? new string[2] { "Yes", "No" } : ask == Ask.Sure ? new string[2] { "Try it", "Skip" } : new string[2] { "Keep going", "Leave" };
		float ts = 0.56f;
		List<string> lines = new List<string>();
		foreach (string para in q.Split('\n'))
		{
			lines.AddRange(art.Wrap(para, 880f / ts));
		}
		float boxH = Math.Max(150f, 70f + lines.Count * art.LineHeight * ts);
		float y0 = 300f + (1f - e) * 30f;
		Rect box = new Rect(sw / 2f - 500f, y0, 1000f, boxH);
		GuiArt.Img tb = art.Image("textbox/default");
		GUI.color = new Color(1f, 1f, 1f, e);
		if (tb != null)
		{
			CelesteTextbox.Slices9(tb, box, 0.82f);
		}
		else
		{
			GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.9f * e);
			GUI.DrawTexture(box, Texture2D.whiteTexture);
		}
		float ty = y0 + (boxH - lines.Count * art.LineHeight * ts) / 2f;
		foreach (string l in lines)
		{
			float lw = art.Measure(l) * ts;
			art.DrawLine(l, sw / 2f - lw / 2f, ty, ts, new Color(0.94f, 0.92f, 0.95f, e));
			ty += art.LineHeight * ts;
		}
		float cy = y0 + boxH + 34f;
		float cs = 0.75f;
		Color hot = flash % 0.2f < 0.1f ? new Color(0.52f, 1f, 0.33f, e) : new Color(0.99f, 1f, 0.35f, e);
		for (int i = 0; i < 2; i++)
		{
			float cx = sw / 2f + (i == 0 ? -220f : 220f);
			float lw = art.Measure(opts[i]) * cs;
			bool sel = i == choice;
			float bob = sel ? -4f - Mathf.Abs(Mathf.Sin(flash * 6f)) * 3f : 0f;
			choiceRects[i] = new Rect(cx - lw / 2f - 50f, cy - 10f, lw + 100f, art.LineHeight * cs + 20f);
			Color c = sel ? hot : new Color(0.85f, 0.85f, 0.85f, e);
			if (sel)
			{
				art.DrawOutlined(">", cx - lw / 2f - 40f, cy + bob, cs, c, new Color(0f, 0f, 0f, e), 2f);
				art.DrawOutlined("<", cx + lw / 2f + 16f, cy + bob, cs, c, new Color(0f, 0f, 0f, e), 2f);
			}
			art.DrawOutlined(opts[i], cx - lw / 2f, cy + bob, cs, c, new Color(0f, 0f, 0f, e), 2f);
		}
		if (ask == Ask.Sure)
		{
			string hint = "Recommended: Try it";
			float hs = 0.42f;
			art.DrawOutlined(hint, sw / 2f - art.Measure(hint) * hs / 2f, cy + art.LineHeight * cs + 22f, hs, new Color(0.7f, 0.85f, 1f, e), new Color(0f, 0f, 0f, e), 2f);
		}
	}
}
