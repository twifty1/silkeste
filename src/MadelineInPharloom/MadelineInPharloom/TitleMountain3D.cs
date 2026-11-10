using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// The title and file screens' mountain is Celeste's own 3D mountain (user 2026-11-10), read from the player's
/// Celeste like Madeline is: Overworld/mountain.obj and buildings.obj with the Mountain atlas's terrain, buildings and
/// skybox textures for one time of day. Two small cameras far from the game's world draw the skybox, then the mountain,
/// into one image the menus draw behind them; they only render while that image was asked for in the last half
/// second, so it costs nothing in game. If anything can't be read, the painted mountain stays (CelesteFileSelect).
/// </summary>
public class TitleMountain3D : MonoBehaviour
{
	private static TitleMountain3D instance;

	private static float wantedAt = -10f;

	// far from Pharloom and from each other, so no game camera and neither of ours sees the other's things
	private static readonly Vector3 MountainHome = new Vector3(60000f, 60000f, 0f);

	private static readonly Vector3 SkyHome = new Vector3(-60000f, 60000f, 0f);

	private class Parsed
	{
		public Vector3[] Pos;

		public Vector2[] Uv;

		public int[] Tris;
	}

	private Parsed mountain;

	private Parsed buildings;

	private CelesteAssets.Page terrainTex;

	private CelesteAssets.Page buildingTex;

	private CelesteAssets.Page skyTex;

	private CelesteAssets.Page fogTex;

	private Color fogColor;

	private Material fogMat;

	private volatile bool loaded;

	private volatile bool failed;

	private bool built;

	private Camera mountainCam;

	private Camera skyCam;

	private RenderTexture rt;

	private float orbit = MathF.PI / 2f;   // Celeste's title view: from (0, 12, 24) at its centre

	/// <summary>Celeste's mountain as it is this frame, or null (not read yet, or couldn't be): draw the painted one then.</summary>
	public static Texture Frame()
	{
		wantedAt = Time.unscaledTime;
		if (instance == null || !instance.built || instance.rt == null || !instance.rt.IsCreated())
		{
			return null;
		}
		return instance.rt;
	}

	private void Start()
	{
		instance = this;
		string celeste = Plugin.CelesteDir;
		if (string.IsNullOrEmpty(celeste))
		{
			failed = true;
			return;
		}
		int state = StateFor(TitleScene.Name);
		// Celeste's fog colours for its three looks (MountainModel)
		fogColor = state == 2 ? new Color32(0x28, 0x1A, 0x35, 0xFF) : state == 1 ? new Color32(0x13, 0x20, 0x3E, 0xFF) : new Color32(0x01, 0x08, 0x17, 0xFF);
		Thread t = new Thread((ThreadStart)delegate
		{
			try
			{
				System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
				string over = Path.Combine(celeste, "Content", "Overworld");
				string atlas = Path.Combine(celeste, "Content", "Graphics", "Atlases", "Mountain");
				mountain = ReadObj(Path.Combine(over, "mountain.obj"));
				buildings = ReadObj(Path.Combine(over, "buildings.obj"));
				terrainTex = CelesteAssets.DecodeData(Path.Combine(atlas, "mountain_" + state + ".data"));
				buildingTex = CelesteAssets.DecodeData(Path.Combine(atlas, "buildings_" + state + ".data"));
				skyTex = CelesteAssets.DecodeData(Path.Combine(atlas, "skybox_" + state + ".data"));
				fogTex = CelesteAssets.DecodeData(Path.Combine(atlas, "fog.data"));
				loaded = true;
				Plugin.Log.LogInfo("Title mountain: Celeste's read in " + sw.ElapsedMilliseconds + " ms (" + mountain.Tris.Length / 3 + " + " + buildings.Tris.Length / 3 + " triangles, look " + state + ")");
			}
			catch (Exception e)
			{
				failed = true;
				Plugin.Log.LogWarning("Title mountain: couldn't read Celeste's, the painted one stays: " + e.Message);
			}
		});
		t.IsBackground = true;
		t.Start();
	}

	// Celeste's three looks (seen in its skybox textures): 0 night, 1 twilight, 2 day
	private static int StateFor(string name)
	{
		switch (name)
		{
		case "sunset":
		case "dawn":
			return 1;
		case "day":
			return 2;
		default:
			return 0;
		}
	}

	private static Parsed ReadObj(string file)
	{
		List<Vector3> v = new List<Vector3>();
		List<Vector2> vt = new List<Vector2>();
		List<Vector3> pos = new List<Vector3>();
		List<Vector2> uv = new List<Vector2>();
		foreach (string line in File.ReadLines(file))
		{
			if (line.Length < 2)
			{
				continue;
			}
			string[] p = line.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (p.Length == 0)
			{
				continue;
			}
			if (p[0] == "v" && p.Length >= 4)
			{
				// Celeste's world is right-handed, Unity's left-handed: z turns round (and the triangles below with it)
				v.Add(new Vector3(F(p[1]), F(p[2]), -F(p[3])));
			}
			else if (p[0] == "vt" && p.Length >= 3)
			{
				vt.Add(new Vector2(F(p[1]), F(p[2])));
			}
			else if (p[0] == "f" && p.Length >= 4)
			{
				for (int m = 1; m <= 3; m++)
				{
					string[] q = p[m].Split('/');
					pos.Add(v[int.Parse(q[0], CultureInfo.InvariantCulture) - 1]);
					uv.Add((q.Length > 1 && q[1].Length > 0) ? vt[int.Parse(q[1], CultureInfo.InvariantCulture) - 1] : Vector2.zero);
				}
			}
		}
		int[] tris = new int[pos.Count];
		for (int i = 0; i + 2 < tris.Length; i += 3)
		{
			// Celeste culls clockwise faces (its fronts are counter-clockwise); with z turned round they come out
			// clockwise, Unity's front, so the order stays as it is
			tris[i] = i;
			tris[i + 1] = i + 1;
			tris[i + 2] = i + 2;
		}
		return new Parsed
		{
			Pos = pos.ToArray(),
			Uv = uv.ToArray(),
			Tris = tris
		};
	}

	private static float F(string s)
	{
		return float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
	}

	// Celeste's pixels go in top row first, as its UVs expect (v down), so neither needs turning over
	private static Texture2D Tex(CelesteAssets.Page p, bool mips)
	{
		Texture2D t = new Texture2D(p.W, p.H, TextureFormat.RGBA32, mips)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp,
			anisoLevel = 4
		};
		t.SetPixelData(p.Rgba, 0);
		t.Apply(updateMipmaps: mips, makeNoLongerReadable: true);
		return t;
	}

	private static GameObject MeshObject(string name, Transform parent, Parsed d, Material mat)
	{
		Mesh mesh = new Mesh
		{
			indexFormat = (d.Pos.Length > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16
		};
		mesh.vertices = d.Pos;
		mesh.uv = d.Uv;
		mesh.triangles = d.Tris;
		mesh.RecalculateBounds();
		GameObject go = new GameObject(name);
		go.transform.SetParent(parent, worldPositionStays: false);
		go.AddComponent<MeshFilter>().sharedMesh = mesh;
		MeshRenderer r = go.AddComponent<MeshRenderer>();
		r.sharedMaterial = mat;
		r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		r.receiveShadows = false;
		return go;
	}

	private void Build()
	{
		Shader unlit = Shader.Find("Unlit/Texture");
		if (unlit == null)
		{
			failed = true;
			Plugin.Log.LogWarning("Title mountain: no unlit shader, the painted one stays");
			return;
		}
		Transform root = new GameObject("SilkesteCelesteMountain").transform;
		UnityEngine.Object.DontDestroyOnLoad(root.gameObject);
		root.position = MountainHome;
		MeshObject("Terrain", root, mountain, new Material(unlit) { mainTexture = Tex(terrainTex, mips: true) });
		MeshObject("Buildings", root, buildings, new Material(unlit) { mainTexture = Tex(buildingTex, mips: true) });
		Transform sky = new GameObject("SilkesteCelesteSky").transform;
		UnityEngine.Object.DontDestroyOnLoad(sky.gameObject);
		sky.position = SkyHome;
		MeshObject("Skybox", sky, SkyBox(skyTex.W, skyTex.H), new Material(unlit) { mainTexture = Tex(skyTex, mips: false) });
		// Celeste's fog: a slowly turning band of its fog texture round the mountain (Ring(6, -1, 20), tinted with the
		// look's fog colour) in front of the sky
		Shader sprite = Shader.Find("Sprites/Default");
		if (sprite != null && fogTex != null)
		{
			Texture2D ft = Tex(fogTex, mips: false);
			ft.wrapMode = TextureWrapMode.Repeat;
			fogMat = new Material(sprite) { mainTexture = ft };
			GameObject ring = MeshObject("Fog", root, FogRing(6f, -1f, 20f, 24), fogMat);
			Color top = Color.Lerp(fogColor, Color.white, 0.55f);
			ring.GetComponent<MeshFilter>().sharedMesh.colors = RingColors(24, top);
		}
		mountain = buildings = null;
		terrainTex = buildingTex = skyTex = fogTex = null;
		skyCam = MakeCam("SilkesteSkyCam", CameraClearFlags.SolidColor, 0f);
		mountainCam = MakeCam("SilkesteMountainCam", CameraClearFlags.Depth, 1f);
		built = true;
	}

	private static Camera MakeCam(string name, CameraClearFlags clear, float depth)
	{
		GameObject go = new GameObject(name);
		UnityEngine.Object.DontDestroyOnLoad(go);
		Camera c = go.AddComponent<Camera>();
		c.enabled = false;   // drawn by hand (Render) only when the menus want the picture
		c.clearFlags = clear;
		c.backgroundColor = Color.black;
		c.fieldOfView = 45f;   // Celeste's pi/4
		c.nearClipPlane = 0.25f;
		c.farClipPlane = 60f;
		c.depth = depth;
		c.allowHDR = false;
		c.allowMSAA = true;
		c.useOcclusionCulling = false;
		return c;
	}

	// Celeste's Skybox: a 25-unit box with 5 faces cut from one 820-pixel strip (top, then the sides). Both windings,
	// since it's seen from inside and the handedness turned round.
	private static Parsed SkyBox(int w, int h)
	{
		const float s = 25f;
		Vector3 a = X(-s, s, -s), b = X(s, s, -s), c = X(s, s, s), d = X(-s, s, s);
		Vector3 e = X(-s, -s, -s), f = X(s, -s, -s), g = X(s, -s, s), k = X(-s, -s, s);
		List<Vector3> pos = new List<Vector3>();
		List<Vector2> uv = new List<Vector2>();
		List<int> tris = new List<int>();
		void Face(int px, int pw, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
		{
			float u0 = (px + 1f) / w, u1 = (px + pw - 1f) / w, v0 = 1f / h, v1 = (820f - 1f) / h;
			int i = pos.Count;
			pos.AddRange(new Vector3[4] { p0, p1, p2, p3 });
			uv.AddRange(new Vector2[4]
			{
				new Vector2(u0, v0),
				new Vector2(u1, v0),
				new Vector2(u1, v1),
				new Vector2(u0, v1)
			});
			tris.AddRange(new int[12] { i, i + 1, i + 2, i, i + 2, i + 3, i, i + 2, i + 1, i, i + 3, i + 2 });
		}
		Face(0, 820, a, b, c, d);
		Face(2460, 820, b, a, e, f);
		Face(820, 820, d, c, g, k);
		Face(3280, 819, c, b, f, g);
		Face(1640, 820, a, d, k, e);
		return new Parsed
		{
			Pos = pos.ToArray(),
			Uv = uv.ToArray(),
			Tris = tris.ToArray()
		};
	}

	private static Parsed FogRing(float top, float bottom, float distance, int steps)
	{
		List<Vector3> pos = new List<Vector3>();
		List<Vector2> uv = new List<Vector2>();
		List<int> tris = new List<int>();
		for (int i = 0; i < steps; i++)
		{
			float u0 = (float)(i - 1) / steps;
			float u1 = (float)i / steps;
			Vector3 a = X(Mathf.Cos(u0 * MathF.PI * 2f) * distance, top, Mathf.Sin(u0 * MathF.PI * 2f) * distance);
			Vector3 b = X(Mathf.Cos(u1 * MathF.PI * 2f) * distance, top, Mathf.Sin(u1 * MathF.PI * 2f) * distance);
			Vector3 c = new Vector3(b.x, bottom, b.z);
			Vector3 d = new Vector3(a.x, bottom, a.z);
			int n = pos.Count;
			pos.AddRange(new Vector3[4] { a, b, c, d });
			uv.AddRange(new Vector2[4]
			{
				new Vector2(u0, 0.01f),
				new Vector2(u1, 0.01f),
				new Vector2(u1, 1f),
				new Vector2(u0, 1f)
			});
			tris.AddRange(new int[6] { n, n + 1, n + 2, n, n + 2, n + 3 });
		}
		return new Parsed
		{
			Pos = pos.ToArray(),
			Uv = uv.ToArray(),
			Tris = tris.ToArray()
		};
	}

	private static Color[] RingColors(int steps, Color c)
	{
		Color[] cs = new Color[steps * 4];
		for (int i = 0; i < cs.Length; i++)
		{
			cs[i] = c;
		}
		return cs;
	}

	private static Vector3 X(float x, float y, float z)
	{
		return new Vector3(x, y, -z);
	}

	private void Update()
	{
		if (failed)
		{
			return;
		}
		if (loaded && !built)
		{
			try
			{
				Build();
			}
			catch (Exception e)
			{
				failed = true;
				Plugin.Log.LogWarning("Title mountain: " + e.Message);
			}
		}
		if (!built)
		{
			return;
		}
		bool wanted = Time.unscaledTime - wantedAt < 0.5f;
		if (!wanted)
		{
			if (rt != null && Time.unscaledTime - wantedAt > 5f)
			{
				rt.Release();   // in game: the picture's memory back
				UnityEngine.Object.Destroy(rt);
				rt = null;
			}
			return;
		}
		int w = Math.Min(Screen.width, 1920);
		int h = Math.Max(1, Mathf.RoundToInt(w * (float)Screen.height / Math.Max(1, Screen.width)));
		if (rt == null || rt.width != w || rt.height != h)
		{
			if (rt != null)
			{
				rt.Release();
				UnityEngine.Object.Destroy(rt);
			}
			rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
			{
				antiAliasing = 2,
				name = "SilkesteMountain"
			};
			rt.Create();
		}
		// Celeste's main-menu camera: low, looking up at the peak, slowly going round (MountainRenderer)
		orbit -= Time.unscaledDeltaTime * 0.1f;
		Vector3 eye = X(Mathf.Cos(orbit) * 15f, 3f, Mathf.Sin(orbit) * 15f);
		if (fogMat != null)
		{
			fogMat.mainTextureOffset = new Vector2(fogMat.mainTextureOffset.x - Time.unscaledDeltaTime * 0.01f, 0f);
		}
		Vector3 look = X(0f, 7f, 0f);
		Quaternion turn = Quaternion.LookRotation(look - eye, Vector3.up);
		mountainCam.transform.SetPositionAndRotation(MountainHome + eye, turn);
		// the skybox sits round the camera, lowered as it rises (Celeste: 5 - camera height x 1.1)
		skyCam.transform.SetPositionAndRotation(SkyHome - new Vector3(0f, 5f - eye.y * 1.1f, 0f), turn);
		mountainCam.aspect = skyCam.aspect = (float)w / h;
		skyCam.targetTexture = mountainCam.targetTexture = rt;
		skyCam.Render();
		// the far side of the mountain fades into the look's fog colour, as Celeste's mountain shader does
		bool fog = RenderSettings.fog;
		FogMode mode = RenderSettings.fogMode;
		Color fogCol = RenderSettings.fogColor;
		float start = RenderSettings.fogStartDistance;
		float end = RenderSettings.fogEndDistance;
		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.Linear;
		RenderSettings.fogColor = fogColor;
		RenderSettings.fogStartDistance = 10f;
		RenderSettings.fogEndDistance = 55f;
		mountainCam.Render();
		RenderSettings.fog = fog;
		RenderSettings.fogMode = mode;
		RenderSettings.fogColor = fogCol;
		RenderSettings.fogStartDistance = start;
		RenderSettings.fogEndDistance = end;
	}

	private void OnApplicationQuit()
	{
		if (rt != null)
		{
			rt.Release();
		}
	}
}
