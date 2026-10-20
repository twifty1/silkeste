using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GlobalEnums;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

public class PostcardView : MonoBehaviour
{
	private enum Phase
	{
		None,
		In,
		Shown,
		Out
	}

	public static GuiArt Art;

	private Phase phase;

	private PostcardRow card;

	private List<string> lines;

	private float t;

	private float shownFor;

	private float controlFor;

	private float textScale = 0.7f;

	private readonly List<PostcardRow> queue = new List<PostcardRow>();

	private bool relinquished;

	private bool introShown;

	public static string DataDir;

	public static bool TutorialDone;

	private int baselineSlot = -1;

	private readonly Dictionary<string, bool> had = new Dictionary<string, bool>();

	private static string TutorialFile => Path.Combine(DataDir ?? "", "tutorial.cfg");

	private static bool TutorialDue(int slot, float playTime)
	{
		Dictionary<int, float> dictionary = new Dictionary<int, float>();
		try
		{
			if (DataDir != null && File.Exists(TutorialFile))
			{
				string[] array = File.ReadAllLines(TutorialFile);
				for (int i = 0; i < array.Length; i++)
				{
					string[] array2 = array[i].Split(new char[1] { '=' });
					if (array2.Length == 2 && int.TryParse(array2[0], out var result) && float.TryParse(array2[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
					{
						dictionary[result] = result2;
					}
				}
			}
			if (playTime >= 300f && dictionary.TryGetValue(slot, out var value) && playTime + 5f >= value)
			{
				return false;
			}
			dictionary[slot] = playTime;
			List<string> list = new List<string>();
			foreach (KeyValuePair<int, float> item in dictionary)
			{
				list.Add(item.Key + "=" + item.Value.ToString("R", CultureInfo.InvariantCulture));
			}
			Directory.CreateDirectory(DataDir);
			File.WriteAllLines(TutorialFile, list.ToArray());
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Tutorial: " + ex.Message);
		}
		return true;
	}

	private void Update()
	{
		if (!Plugin.Ready || Art == null || Art.Postcard == null || Art.Postcard.Tex == null)
		{
			return;
		}
		HeroController silentInstance = HeroController.UnsafeInstance;
		GameManager silentInstance2 = GameManager.SilentInstance;
		if (silentInstance == null || silentInstance2 == null)
		{
			Hide(silentInstance);
			introShown = false;
			TutorialDone = false;
			return;
		}
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		bool flag = silentInstance2.GameState == GameState.PLAYING && !silentInstance2.isPaused && !silentInstance.cState.transitioning && !silentInstance.cState.dead && !silentInstance.cState.hazardDeath;
		bool flag2 = flag && (!silentInstance.controlReqlinquished || TechRoom.HoldsControl);
		controlFor = (flag2 ? (controlFor + unscaledDeltaTime) : 0f);
		WatchUnlocks(silentInstance2);
		if (!introShown && flag2 && !silentInstance.playerData.atBench)
		{
			introShown = true;
			if (TutorialDue(silentInstance2.profileID, silentInstance2.PlayTime))
			{
				Enqueue(Postcards.Intro, front: false);
			}
			else
			{
				TutorialDone = true;
			}
		}
		Keyboard current = Keyboard.current;
		if (false && flag)   // the Tutorial key is gone (user 2026-10-19); the card stays in Records
		{
			if (phase == Phase.Shown && card.Id == Postcards.Help.Id)
			{
				Close();
			}
			else if (phase == Phase.None)
			{
				Enqueue(Postcards.Help, front: true);
			}
		}
		if (phase == Phase.None && queue.Count > 0 && flag2 && (queue[0].Id == Postcards.Help.Id || controlFor >= 0.5f))
		{
			Open(silentInstance);
		}
		switch (phase)
		{
		case Phase.In:
			t += unscaledDeltaTime / 1.25f;
			if (t >= 1f)
			{
				phase = Phase.Shown;
				shownFor = 0f;
			}
			break;
		case Phase.Shown:
			shownFor += unscaledDeltaTime;
			if (shownFor >= 0.75f && ConfirmPressed(current))
			{
				Close();
			}
			break;
		case Phase.Out:
			t += unscaledDeltaTime / 1f;
			if (t >= 1f)
			{
				if (card.Id == Postcards.Intro.Id)
				{
					TutorialDone = true;
					TechRoom.OfferAfterIntro();   // then: do you know Celeste's tech? (user 2026-10-20)
				}
				phase = Phase.None;
				card = default(PostcardRow);
				Release(silentInstance);
			}
			break;
		}
	}

	private void Enqueue(PostcardRow row, bool front)
	{
		foreach (PostcardRow item in queue)
		{
			if (item.Id == row.Id)
			{
				return;
			}
		}
		if (front)
		{
			queue.Insert(0, row);
		}
		else
		{
			queue.Add(row);
		}
	}

	private void WatchUnlocks(GameManager gm)
	{
		if (PlayerData.instance == null || TechRoom.Active)
		{
			return;   // the Tech Tutorial lends climbing for a while: no Cling Grip card for that
		}
		bool flag = gm.profileID != baselineSlot;
		if (flag)
		{
			introShown = false;
			TutorialDone = false;
		}
		baselineSlot = gm.profileID;
		PostcardRow[] all = Postcards.All;
		for (int i = 0; i < all.Length; i++)
		{
			PostcardRow row = all[i];
			if (!(row.Unlock == "none"))
			{
				bool flag2 = MadelineController.Unlocked(UnlockRowFor(row.Unlock));
				if (!flag && had.TryGetValue(row.Id, out var value) && !value && flag2 && !StartingAbilities.WasGiven(UnlockRowFor(row.Unlock).PlayerData))
				{
					Enqueue(row, front: false);
				}
				had[row.Id] = flag2;
			}
		}
	}

	private static UnlockRow UnlockRowFor(string id)
	{
		UnlockRow[] all = Unlocks.All;
		for (int i = 0; i < all.Length; i++)
		{
			UnlockRow result = all[i];
			if (result.Id == id)
			{
				return result;
			}
		}
		throw new Exception("postcard unlock '" + id + "' is not in sheets/unlocks.json");
	}

        // ---- Records' postcard album (user 2026-10-16) -----------------------------------------------------------
        static string SeenFile { get { return Path.Combine(DataDir ?? "", "postcards_seen.cfg"); } }
        static HashSet<string> seenIds;
        static void LoadSeen()
        {
            if (seenIds != null) return;
            seenIds = new HashSet<string>();
            try { if (File.Exists(SeenFile)) foreach (string l in File.ReadAllLines(SeenFile)) if (l.Trim().Length > 0) seenIds.Add(l.Trim()); } catch (Exception) { }
        }
        static void MarkSeen(string id)
        {
            LoadSeen();
            if (!seenIds.Add(id)) return;
            try { File.WriteAllLines(SeenFile, new List<string>(seenIds).ToArray()); } catch (Exception) { }
            Achievements.Best("postman", Album().Count);
        }
        /// <summary>The cards for the album: the Tutorial always, and every unlock card seen (or already earned).</summary>
        public static List<PostcardRow> Album()
        {
            LoadSeen();
            var list = new List<PostcardRow>();
            foreach (PostcardRow row in Postcards.All)
            {
                bool owned = false;
                if (row.Unlock != "none") { try { owned = PlayerData.instance != null && MadelineController.Unlocked(UnlockRowFor(row.Unlock)); } catch (Exception) { } }
                if (row.Id == Postcards.Help.Id) continue;   // the same card as the Tutorial: listed once
                if (row.Id == Postcards.Intro.Id || seenIds.Contains(row.Id) || owned) list.Add(row);
            }
            return list;
        }
        static readonly Dictionary<string, KeyValuePair<List<string>, float>> albumLines = new Dictionary<string, KeyValuePair<List<string>, float>>();
        /// <summary>A postcard as it shows in the game, centred at c (GUI units), scaled by k.</summary>
        public static void DrawAlbumCard(PostcardRow row, Vector2 c, float k, float alpha)
        {
            if (Art == null || Art.Postcard == null || Art.Postcard.Tex == null) return;
            KeyValuePair<List<string>, float> wrapped;
            if (!albumLines.TryGetValue(row.Id, out wrapped))
            {
                float ts = Visuals.PostcardTextScale; List<string> ls = null;
                for (int i = 0; i < 12; i++)
                {
                    ls = Art.Wrap(Fill(row.Body), (Art.Postcard.W - Visuals.PostcardTextMargin) / ts);
                    if (ls.Count * Art.LineHeight * ts <= Visuals.PostcardTextMaxHeight) break;
                    ts *= 0.95f;
                }
                albumLines[row.Id] = wrapped = new KeyValuePair<List<string>, float>(ls, ts);
            }
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = old * Matrix4x4.TRS(new Vector3(c.x, c.y, 0f), Quaternion.Euler(0f, 0f, Visuals.PostcardTurnTo * Mathf.Rad2Deg), new Vector3(k, k, 1f));
            float w = Art.Postcard.W, h = Art.Postcard.H;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(-w / 2f, -h / 2f, w, h), Art.Postcard.Tex);
            Art.DrawLine(row.Title, -w / 2f + Visuals.PostcardTitleX, -h / 2f + Visuals.PostcardTitleY, Visuals.PostcardTitleScale, new Color(0f, 0f, 0f, Visuals.PostcardTitleAlpha * alpha));
            float sc = wrapped.Value, lineH = Art.LineHeight * sc, top = Visuals.PostcardTextOffsetY - wrapped.Key.Count * lineH / 2f, block = 0f;
            var ink = new Color(0f, 0f, 0f, Visuals.PostcardTextAlpha * alpha);
            foreach (string line in wrapped.Key) block = Math.Max(block, Art.Measure(line));
            foreach (string line in wrapped.Key)
            {
                float x = Visuals.PostcardLeftAligned > 0f ? -block * sc / 2f : -Art.Measure(line) * sc / 2f;
                Art.DrawLine(line, x, top, sc, ink);
                top += lineH;
            }
            GUI.matrix = old;
            GUI.color = Color.white;
        }

	private void Open(HeroController h)
	{
		card = queue[0];
		queue.RemoveAt(0);
		MarkSeen(card.Id);
		textScale = 0.7f;
		for (int i = 0; i < 12; i++)
		{
			lines = Art.Wrap(Fill(card.Body), ((float)Art.Postcard.W - 120f) / textScale);
			if ((float)(lines.Count * Art.LineHeight) * textScale <= 400f)
			{
				break;
			}
			textScale *= 0.95f;
		}
		phase = Phase.In;
		t = 0f;
		Sfx.Play(Sounds.PostcardIn.Id);
		if (!h.controlReqlinquished)
		{
			h.RelinquishControl();
			relinquished = true;
		}
	}

	private void Close()
	{
		Sfx.Play(Sounds.ButtonLowkey.Id);
		Sfx.Play(Sounds.PostcardOut.Id);
		phase = Phase.Out;
		t = 0f;
	}

	private void Release(HeroController h)
	{
		if (relinquished && h != null)
		{
			h.RegainControl();
		}
		relinquished = false;
	}

	private void Hide(HeroController h)
	{
		if (phase != Phase.None)
		{
			phase = Phase.None;
			Release(h);
		}
	}

	private static bool ConfirmPressed(Keyboard kb)
	{
		InputHandler instance = ManagerSingleton<InputHandler>.Instance;
		if (instance != null && instance.inputActions != null && instance.inputActions.Jump.WasPressed)
		{
			return true;
		}
		if (kb != null)
		{
			if (!kb.enterKey.wasPressedThisFrame)
			{
				return kb.spaceKey.wasPressedThisFrame;
			}
			return true;
		}
		return false;
	}

	private static string Fill(string body)
	{
		UnlockRow[] all = Unlocks.All;
		for (int i = 0; i < all.Length; i++)
		{
			UnlockRow row = all[i];
			body = body.Replace("{unlock:" + row.Id + "}", MadelineController.Unlocked(row) ? "Unlocked" : ("find " + row.SilksongUnlock));
		}
		AssistRow[] all2 = AssistOptions.All;
		for (int i = 0; i < all2.Length; i++)
		{
			AssistRow row2 = all2[i];
			if (row2.Section == "Controls")
			{
				body = body.Replace("{key:" + row2.Id + "}", AssistMode.KeyName(row2));
			}
		}
		return body;
	}

	private void OnGUI()
	{
		if (TheoCamera.Hiding || phase == Phase.None || Art == null || Event.current.type != EventType.Repaint)
		{
			return;
		}
		float num = (float)Screen.height / 1080f;
		Vector2 vector = new Vector2((float)Screen.width / num / 2f, 540f);
		float num2 = Mathf.Clamp01(t);
		Vector2 vector2;
		float num4;
		float num5;
		if (phase == Phase.In)
		{
			float num3 = 1f - (1f - num2) * (1f - num2) * (1f - num2);
			vector2 = vector + new Vector2(0f, 200f) * (1f - num3);
			num4 = num3;
			num5 = -0.1f + 0.15f * num3;
		}
		else if (phase == Phase.Out)
		{
			float num3 = num2 * num2 * num2;
			vector2 = vector - new Vector2(0f, 200f) * num3;
			num4 = 1f - num3;
			num5 = 0.05f + 0.1f * num3;
		}
		else
		{
			vector2 = vector;
			num4 = 1f;
			num5 = 0.05f;
		}
		Matrix4x4 matrix = GUI.matrix;
		GUI.depth = -1;
		GUI.matrix = Matrix4x4.TRS(new Vector3(vector2.x * num, vector2.y * num, 0f), Quaternion.Euler(0f, 0f, num5 * 57.29578f), new Vector3(num, num, 1f));
		float num6 = Art.Postcard.W;
		float num7 = Art.Postcard.H;
		GUI.color = new Color(1f, 1f, 1f, num4);
		GUI.DrawTexture(new Rect((0f - num6) / 2f, (0f - num7) / 2f, num6, num7), Art.Postcard.Tex);
		Art.DrawLine(card.Title, (0f - num6) / 2f + 115f, (0f - num7) / 2f + 30f, 0.9f, new Color(0f, 0f, 0f, 0.7f * num4));
		float num8 = textScale;
		float num9 = (float)Art.LineHeight * num8;
		float num10 = 40f - (float)lines.Count * num9 / 2f;
		Color c = new Color(0f, 0f, 0f, 0.6f * num4);
		float num11 = 0f;
		foreach (string line in lines)
		{
			num11 = Math.Max(num11, Art.Measure(line));
		}
		foreach (string line2 in lines)
		{
			float x = (0f - num11) * num8 / 2f;
			Art.DrawLine(line2, x, num10, num8, c);
			num10 += num9;
		}
		GUI.matrix = matrix;
		if (phase == Phase.Shown && shownFor > 0.75f)
		{
			float num12 = Mathf.Clamp01((shownFor - 0.75f) * 2f);
			float num13 = 1f - (1f - num12) * (1f - num12) * (1f - num12);
			float num14 = 0.6f;
			float num15 = (float)Screen.width / num - 120f - Art.Measure("Jump to continue") * num14;
			float num16 = 980f - 20f * num13 - (float)Art.LineHeight * num14 / 2f;
			GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num, num, 1f));
			for (int i = -1; i <= 1; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					if (i != 0 || j != 0)
					{
						Art.DrawLine("Jump to continue", num15 + (float)i * 2f, num16 + (float)j * 2f, num14, new Color(0f, 0f, 0f, num13));
					}
				}
			}
			Art.DrawLine("Jump to continue", num15, num16, num14, new Color(1f, 1f, 1f, num13));
			GUI.matrix = matrix;
		}
		GUI.color = Color.white;
	}
}
