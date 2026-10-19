using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace MadelineInPharloom;

public class DeathCounter : MonoBehaviour
{
	public static GuiArt Art;

	public static string DataDir;

	private readonly Dictionary<int, int> counts = new Dictionary<int, int>();

	private bool loaded;

	private bool wasDead;

	// deaths in the room you're in (this session): Celeste's room count, shown under the total (user 2026-10-17)
	private static readonly Dictionary<string, int> roomDeaths = new Dictionary<string, int>();

	private bool wasHazard;

	private string lastRoom;

	/// <summary>Deaths (and hazard falls) in the current room this session.</summary>
	public static int RoomDeaths
	{
		get
		{
			string r = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
			return (r != null && roomDeaths.TryGetValue(r, out int n)) ? n : 0;
		}
	}

	private float showTimer;

	private float alpha;

	private float wiggleCounter;

	private float sineCounter;

	private float flashTimer;

	private string SaveFile => Path.Combine(DataDir, "deaths.cfg");

	private void Update()
	{
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		HeroController silentInstance = HeroController.UnsafeInstance;
		GameManager silentInstance2 = GameManager.SilentInstance;
		bool flag = false;
		if (Plugin.Ready && silentInstance != null && silentInstance2 != null)
		{
			Load();
			bool dead = silentInstance.cState.dead;
			bool hazard = silentInstance.cState.hazardDeath;
			string room = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
			if ((dead && !wasDead) || (hazard && !wasHazard))
			{
				roomDeaths.TryGetValue(room ?? "", out int rn);
				roomDeaths[room ?? ""] = rn + 1;
				lastRoom = room;
			}
			if (hazard && !wasHazard && !dead)
			{
				showTimer = 2.5f;   // a fall back to the last safe spot shows the room count too
			}
			wasHazard = hazard;
			if (dead && !wasDead)
			{
				Add(silentInstance2.profileID);
			}
			if (!dead && wasDead)
			{
				showTimer = 2.5f;
				wiggleCounter = 1f;
				sineCounter = 0f;
				flashTimer = 0.5f;
			}
			wasDead = dead;
			if (!silentInstance2.isPaused)
			{
				showTimer = Math.Max(0f, showTimer - unscaledDeltaTime);
			}
			flag = !dead && !silentInstance2.isPaused && (showTimer > 0f || silentInstance.playerData.atBench);
		}
		alpha = Mathf.MoveTowards(alpha, flag ? 1f : 0f, unscaledDeltaTime / 0.25f);
		if (wiggleCounter > 0f)
		{
			wiggleCounter = Math.Max(0f, wiggleCounter - unscaledDeltaTime / 0.5f);
			sineCounter += MathF.PI * 6f * unscaledDeltaTime;
		}
		if (flashTimer > 0f)
		{
			flashTimer -= unscaledDeltaTime;
		}
	}

	private void Load()
	{
		if (loaded)
		{
			return;
		}
		loaded = true;
		try
		{
			if (!File.Exists(SaveFile))
			{
				return;
			}
			string[] array = File.ReadAllLines(SaveFile);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(new char[1] { '=' });
				if (array2.Length == 2 && int.TryParse(array2[0], out var result) && int.TryParse(array2[1], out var result2))
				{
					counts[result] = result2;
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Death counter: " + ex.Message);
		}
	}

	private void Add(int slot)
	{
		counts.TryGetValue(slot, out var value);
		counts[slot] = value + 1;
		try
		{
			List<string> list = new List<string>();
			foreach (KeyValuePair<int, int> count in counts)
			{
				list.Add(count.Key + "=" + count.Value);
			}
			Directory.CreateDirectory(DataDir);
			File.WriteAllLines(SaveFile, list.ToArray());
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Death counter: " + ex.Message);
		}
	}

	private void OnGUI()
	{
		if (TheoCamera.Hiding || TheoCamera.HidingHud || alpha <= 0f || Art == null || Art.Skull.Tex == null || Event.current.type != EventType.Repaint)
		{
			return;
		}
		GameManager silentInstance = GameManager.SilentInstance;
		if (silentInstance == null)
		{
			return;
		}
		counts.TryGetValue(silentInstance.profileID, out var value);
		string text = value.ToString(CultureInfo.InvariantCulture);
		GUI.depth = 1;
		float num = (float)Screen.height / 1080f;
		float num2 = 0f;
		string text2 = text;
		foreach (char c in text2)
		{
			num2 += (float)Art.Digits[c - 48].XAdvance;
		}
		float num3 = 62f + (float)Art.X.W + 2f + num2;
		Vector2 vector = new Vector2((float)Screen.width / num - 48f - num3, 48f + (float)Art.Skull.H / 2f);
		float num4 = (float)Math.Sin(sineCounter) * wiggleCounter;
		Color c2 = ((flashTimer > 0f && Time.unscaledTime % 0.1f < 0.05f) ? Hex("FF5E76") : Color.white);
		c2.a = alpha;
		Color c3 = new Color(0f, 0f, 0f, alpha);
		Centered(Art.Skull, vector + new Vector2(30f, 0f), new Color(1f, 1f, 1f, alpha), num);
		Centered(Art.X, vector + new Vector2(62f + (float)Art.X.W / 2f, 2f), c2, num);
		Vector2 vector2 = vector + new Vector2(num3 - num2 / 2f, (0f - num4) * 18f);
		float num5 = 2f;
		for (int j = -1; j <= 1; j++)
		{
			for (int k = -1; k <= 1; k++)
			{
				if (j != 0 || k != 0)
				{
					Text(text, vector2 + new Vector2((float)j * num5, (float)k * num5), num2, c3, num);
				}
			}
		}
		Text(text, vector2, num2, c2, num);
		int here = RoomDeaths;
		if (here > 0 && Art != null)
		{
			// this room's count under the total, small
			string rt = "this room  " + here;
			float rs = 0.42f;
			float rx = ((float)Screen.width / num - 48f - Art.Measure(rt) * rs) * num;
			GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num, num, 1f));
			Art.DrawOutlined(rt, (float)Screen.width / num - 48f - Art.Measure(rt) * rs, 48f + (float)Art.Skull.H + 14f, rs, new Color(1f, 1f, 1f, alpha), new Color(0f, 0f, 0f, alpha), 2f);
			GUI.matrix = Matrix4x4.identity;
		}
		GUI.color = Color.white;
	}

	private static void Text(string text, Vector2 center, float width, Color c, float s)
	{
		float num = center.x - width / 2f;
		float num2 = center.y - (float)Art.LineHeight / 2f;
		GUI.color = c;
		foreach (char c2 in text)
		{
			GuiArt.Img img = Art.Digits[c2 - 48];
			GUI.DrawTexture(new Rect((num + (float)img.XOffset) * s, (num2 + (float)img.YOffset) * s, (float)img.W * s, (float)img.H * s), img.Tex);
			num += (float)img.XAdvance;
		}
	}

	private static void Centered(GuiArt.Img img, Vector2 center, Color c, float s)
	{
		GUI.color = c;
		GUI.DrawTexture(new Rect((center.x - (float)img.W / 2f) * s, (center.y - (float)img.H / 2f) * s, (float)img.W * s, (float)img.H * s), img.Tex);
	}

	private static Color Hex(string hex)
	{
		ColorUtility.TryParseHtmlString("#" + hex, out var color);
		return color;
	}
}
