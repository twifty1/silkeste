using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

public class CelesteTextbox : MonoBehaviour
{
	public static bool Showing;

	private float hideFor;

	private static readonly AccessTools.FieldRef<DialogueBox, bool> Running = AccessTools.FieldRefAccess<DialogueBox, bool>("isDialogueRunning");

	private static readonly AccessTools.FieldRef<DialogueBox> Inst = AccessTools.StaticFieldRefAccess<DialogueBox>(AccessTools.Field(typeof(DialogueBox), "_instance"));

	private static readonly AccessTools.FieldRef<DialogueBox, bool> BoxOpen = AccessTools.FieldRefAccess<DialogueBox, bool>("isBoxOpen");

	private static readonly AccessTools.FieldRef<DialogueBox, bool> Waiting = AccessTools.FieldRefAccess<DialogueBox, bool>("waitingToAdvance");

	private static readonly AccessTools.FieldRef<DialogueBox, bool> Printing = AccessTools.FieldRefAccess<DialogueBox, bool>("isPrintingText");

	private static readonly FieldInfo MeshField = AccessTools.Field(typeof(DialogueBox), "textMesh");

	private bool open;

	private float ease;

	private string shown = "";

	private string lastFull = "";

	private bool waiting;

	private float arrowT;

	private bool loggedError;

	private bool logged;

	private bool loggedOpen;

	private const int HiddenLayer = 31;

	private bool checkedCams;

	private bool camSeesHidden;

	private int diagCount;

	private string loggedText = "";

	private float findIn;

	private DialogueBox found;

	private float dispCount;

	private float pauseT;

	private readonly List<float> revealAt = new List<float>();

	private string revealFor = "";

	private bool isPlayer;

	private string speaker = "";

	private static readonly AccessTools.FieldRef<DialogueBox, DialogueBox.DialogueLine> CurLine = AccessTools.FieldRefAccess<DialogueBox, DialogueBox.DialogueLine>("currentLine");

	private static readonly FieldInfo InstigatorField = AccessTools.Field(typeof(DialogueBox), "instigator");

	private float diagIn = 5f;

	private static readonly float[] Cols = new float[6] { 0f, 120f, 790f, 955f, 1623f, 1743f };

	private Color[] nameColors;

	private static readonly Color Rosary = new Color(1f, 0.78f, 0.72f);

	private static readonly Color Shard = new Color(0.62f, 0.86f, 0.92f);

	private static readonly Color Peak = new Color(1f, 0.86f, 0.25f);

	private static readonly Color Deep = new Color(0.72f, 0.5f, 1f);

	private static readonly Color Place = new Color(0.45f, 0.9f, 0.85f);

	private static readonly Dictionary<string, Color> NameColor = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
	{
		{
			"Hornet",
			new Color(1f, 0.45f, 0.45f)
		},
		{
			"Madeline",
			new Color(1f, 0.45f, 0.45f)
		},
		{
			"Lace",
			new Color(0.95f, 0.95f, 1f)
		},
		{
			"Grand Mother Silk",
			new Color(0.95f, 0.55f, 0.85f)
		},
		{
			"Sherma",
			new Color(1f, 0.85f, 0.4f)
		},
		{
			"Shakra",
			new Color(1f, 0.6f, 0.3f)
		},
		{
			"Garmond",
			new Color(0.55f, 0.75f, 1f)
		},
		{
			"Zaza",
			new Color(0.6f, 0.95f, 0.75f)
		},
		{
			"Seamstress",
			new Color(0.85f, 0.65f, 1f)
		},
		{
			"Pinstress",
			new Color(0.4f, 0.85f, 1f)
		},
		{
			"Trobbio",
			new Color(1f, 0.7f, 0.85f)
		},
		{
			"Widow",
			new Color(0.75f, 0.5f, 0.95f)
		},
		{
			"Moss Mother",
			new Color(0.6f, 0.9f, 0.45f)
		},
		{
			"Bell Beast",
			new Color(1f, 0.75f, 0.45f)
		},
		{
			"Flick",
			new Color(1f, 0.8f, 0.5f)
		},
		{
			"Weaver",
			new Color(0.7f, 0.8f, 1f)
		},
		{
			"Weavers",
			new Color(0.7f, 0.8f, 1f)
		},
		{ "Rosaries", Rosary },
		{ "Rosary", Rosary },
		{ "Rosary String", Rosary },
		{ "Rosary Strings", Rosary },
		{ "Rosary Necklace", Rosary },
		{ "Shell Shards", Shard },
		{ "Shell Shard", Shard },
		{ "Citadel", Peak },
		{ "the Citadel", Peak },
		{ "the peak", Peak },
		{ "the summit", Peak },
		{ "the crown", Peak },
		{ "Pharloom's peak", Peak },
		{ "the top of the kingdom", Peak },
		{ "the top of Pharloom", Peak },
		{ "High Halls", Peak },
		{ "Choral Chambers", Peak },
		{
			"Pharloom",
			new Color(1f, 0.78f, 0.5f)
		},
		{ "the Abyss", Deep },
		{ "Abyss", Deep },
		{ "the Void", Deep },
		{
			"Hallownest",
			new Color(0.75f, 0.85f, 1f)
		},
		{ "Bellhart", Place },
		{ "Mosshome", Place },
		{ "Moss Grotto", Place },
		{ "Greymoor", Place },
		{ "Shellwood", Place },
		{ "Bilewater", Place },
		{ "Deep Docks", Place },
		{ "Far Fields", Place },
		{ "Hunter's March", Place },
		{ "Blasted Steps", Place },
		{ "Sinner's Road", Place },
		{ "the Marrow", Place },
		{ "Whiteward", Place },
		{ "Underworks", Place },
		{ "the Cradle", Place },
		{ "Wormways", Place },
		{ "Bone Bottom", Place },
		{ "Mount Fay", Place },
		{ "the Slab", Place },
		{ "Putrified Ducts", Place },
		{ "Weavenest", Place },
		{
			"Haunted Bell",
			new Color(1f, 0.75f, 0.45f)
		}
	};

	private static readonly Color[] Palette = new Color[6]
	{
		new Color(1f, 0.87f, 0.4f),
		new Color(0.45f, 0.85f, 1f),
		new Color(1f, 0.55f, 0.75f),
		new Color(0.6f, 0.95f, 0.55f),
		new Color(0.8f, 0.6f, 1f),
		new Color(1f, 0.65f, 0.35f)
	};

	private static readonly Dictionary<string, string> names = new Dictionary<string, string>();

	private static readonly FieldInfo TitleNpcs = AccessTools.Field(typeof(NpcDialogueTitle), "npcControls");

	private static readonly FieldInfo TitleList = AccessTools.Field(typeof(NpcDialogueTitle), "titles");

	private static readonly float[] Rows = new float[4] { 0f, 80f, 256f, 336f };

	private static Color? ink;

	private static GuiArt Art => SpeedrunTimer.Art;

	private static object Mesh(DialogueBox b)
	{
		if (!(MeshField != null))
		{
			return null;
		}
		return MeshField.GetValue(b);
	}

	private static object Prop(object o, string name)
	{
		PropertyInfo property = o.GetType().GetProperty(name);
		if (!(property != null))
		{
			return null;
		}
		return property.GetValue(o, null);
	}

	private void Awake()
	{
		Plugin.Log.LogInfo("Celeste text box: on");
	}

	private void LateUpdate()
	{
		if ((diagIn -= Time.unscaledDeltaTime) <= 0f && diagCount < 6)
		{
			diagIn = 5f;
			diagCount++;
			DialogueBox dialogueBox = null;
			try
			{
				dialogueBox = Inst();
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Celeste text box: instance read failed: " + ex.Message);
			}
			Plugin.Log.LogInfo("Celeste text box check: art " + ((Art == null) ? "null" : ((Art.Image("textbox/default") == null) ? "no box" : "ok")) + ", instance " + ((dialogueBox != null) ? (dialogueBox.name + " open " + BoxOpen(dialogueBox)) : "null") + ", boxes in scene " + UnityEngine.Object.FindObjectsByType<DialogueBox>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
		}
		try
		{
			Tick();
		}
		catch (Exception ex2)
		{
			if (!loggedError)
			{
				loggedError = true;
				Plugin.Log.LogError("CelesteTextbox: " + ex2);
			}
		}
	}

	private void Tick()
	{
		DialogueBox dialogueBox = Inst();
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		if ((dialogueBox == null || !BoxOpen(dialogueBox)) && (findIn -= unscaledDeltaTime) <= 0f)
		{
			findIn = 1f;
			DialogueBox[] array = UnityEngine.Object.FindObjectsByType<DialogueBox>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			foreach (DialogueBox instance in array)
			{
				if (BoxOpen(instance))
				{
					found = instance;
					break;
				}
			}
		}
		if (found != null && BoxOpen(found))
		{
			dialogueBox = found;
		}
		if (!logged && Art != null && HeroController.UnsafeInstance != null)
		{
			logged = true;
			Plugin.Log.LogInfo("Celeste text box: art " + ((Art != null && Art.Image("textbox/default") != null) ? "ready" : "missing") + ", dialogue box " + ((dialogueBox != null) ? "found" : "not found yet"));
		}
		if (dialogueBox != null && BoxOpen(dialogueBox) && !loggedOpen)
		{
			loggedOpen = true;
			Plugin.Log.LogInfo("Celeste text box: a conversation opened (" + dialogueBox.name + ")");
		}
		if (dialogueBox == null || Art == null || Art.Image("textbox/default") == null)
		{
			open = false;
			ease = Mathf.MoveTowards(ease, 0f, unscaledDeltaTime / 0.15f);
			hideFor = ((ease > 0f) ? 0.5f : Math.Max(0f, hideFor - unscaledDeltaTime));
			Showing = hideFor > 0f;
			return;
		}
		Transform[] componentsInChildren = dialogueBox.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (transform.gameObject.layer != 31)
			{
				transform.gameObject.layer = 31;
			}
		}
		if (!checkedCams)
		{
			checkedCams = true;
			Camera[] allCameras = Camera.allCameras;
			foreach (Camera camera in allCameras)
			{
				if ((camera.cullingMask & int.MinValue) != 0)
				{
					camSeesHidden = true;
					Plugin.Log.LogInfo("Celeste text box: camera " + camera.name + " draws layer " + 31 + "; the box is moved away instead");
				}
			}
		}
		if (camSeesHidden)
		{
			dialogueBox.transform.localPosition = new Vector3(dialogueBox.transform.localPosition.x, 5000f, dialogueBox.transform.localPosition.z);
		}
		bool flag = open;
		open = BoxOpen(dialogueBox);
		if (open && !flag)
		{
			ease = 0f;
			Sfx.Play(Sounds.MenuRolloverDown.Id, 0.5f);
		}
		ease = (open ? Mathf.MoveTowards(ease, 1f, unscaledDeltaTime / 0.2f) : Mathf.MoveTowards(ease, 0f, unscaledDeltaTime / 0.15f));
		bool flag2 = Running(dialogueBox);
		if (ease > 0f || flag2)
		{
			hideFor = 0.5f;
		}
		else
		{
			hideFor = Math.Max(0f, hideFor - unscaledDeltaTime);
		}
		Showing = hideFor > 0f;
		waiting = open && Waiting(dialogueBox) && !Printing(dialogueBox) && dispCount >= (float)shown.Length;
		arrowT = (waiting ? (arrowT + unscaledDeltaTime) : 0f);
		if (!open)
		{
			return;
		}
		object obj = Mesh(dialogueBox);
		if (obj == null)
		{
			return;
		}
		int length = shown.Length;
		shown = VisibleText(obj, out lastFull);
		if (lastFull != revealFor)
		{
			revealFor = lastFull;
			dispCount = 0f;
			pauseT = 0f;
			revealAt.Clear();
			nameColors = NameColors(lastFull);
		}
		int length2 = shown.Length;
		if (length2 - length <= 8 && length2 >= lastFull.Length)
		{
			Printing(dialogueBox);
		}
		if (length2 - length > 8)
		{
			dispCount = length2;
		}
		if (pauseT > 0f)
		{
			pauseT -= unscaledDeltaTime;
		}
		else if (dispCount < (float)length2)
		{
			float num = Math.Min(length2, dispCount + unscaledDeltaTime * (((float)length2 - dispCount > 6f) ? 140f : 70f));
			for (int j = (int)dispCount; j < (int)num && j < lastFull.Length; j++)
			{
				char c = lastFull[j];
				bool flag3 = j + 1 >= lastFull.Length || lastFull[j + 1] == ' ' || lastFull[j + 1] == '\n';
				if (flag3 && (c == '.' || c == '!' || c == '?'))
				{
					num = j + 1;
					pauseT = 0.22f;
					break;
				}
				if (flag3 && (c == ',' || c == ';' || c == ':'))
				{
					num = j + 1;
					pauseT = 0.1f;
					break;
				}
			}
			dispCount = num;
		}
		while (revealAt.Count < (int)dispCount)
		{
			revealAt.Add(Time.unscaledTime);
		}
		try
		{
			DialogueBox.DialogueLine dialogueLine = CurLine(dialogueBox);
			isPlayer = dialogueLine.IsPlayer;
		}
		catch (Exception)
		{
			isPlayer = false;
		}
		string speakerEvent = "";
		try
		{
			speakerEvent = CurLine(dialogueBox).Event;
		}
		catch (Exception)
		{
		}
		speaker = (isPlayer ? "Madeline" : SpeakerName((InstigatorField != null) ? (InstigatorField.GetValue(dialogueBox) as Component) : null, speakerEvent));
		if (lastFull != loggedText && lastFull.Length > 0)
		{
			loggedText = lastFull;
			Plugin.Log.LogInfo("Celeste text box line: " + ((lastFull.Length > 60) ? (lastFull.Substring(0, 60) + "...") : lastFull));
		}
	}

	private static string VisibleText(object tm, out string full)
	{
		full = Regex.Replace((Prop(tm, "text") as string) ?? "", "<[^>]*>", "");
		full = full.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"')
			.Replace('”', '"')
			.Replace('—', '-')
			.Replace('–', '-')
			.Replace("…", "...");
		object obj = Prop(tm, "maxVisibleCharacters");
		int num = ((obj is int) ? ((int)obj) : int.MaxValue);
		int length = ((num == int.MaxValue) ? full.Length : Mathf.Clamp(num, 0, full.Length));
		return full.Substring(0, length);
	}

	private void OnGUI()
	{
		if (Event.current.type != EventType.Repaint || ease <= 0f || Art == null || TheoCamera.Hiding)
		{
			return;
		}
		GuiArt.Img img = Art.Image("textbox/default");
		if (img == null)
		{
			return;
		}
		float num = (float)Screen.height / 1080f;
		float num2 = (float)Screen.width / num;
		Matrix4x4 matrix = GUI.matrix;
		GUI.depth = -1;
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num, num, 1f));
		float num3 = Math.Max(1, Art.Wrap(lastFull, 1719.9999f).Count);
		float num4 = Mathf.Clamp(64f + num3 * (float)Art.LineHeight * 0.6f, 150f, 275.52f);
		float num5 = (open ? BackOut(ease) : (ease * ease * ease));
		float num6 = num4 * num5;
		float num7 = Mathf.Clamp01(ease * 1.5f);
		float num8 = num2 / 2f - 600f;
		float num9 = 40f + (num4 - num6) / 2f;
		Color color = (isPlayer ? new Color(1f, 0.86f, 0.86f) : Color.white);
		Rect r = new Rect(num8, num9, 1200f, Math.Max(1f, num6));
		GUI.color = new Color(color.r, color.g, color.b, num7 * 0.55f);
		Slices9(img, r, 0.82f);
		GUI.color = new Color(color.r, color.g, color.b, num7);
		Slices9(img, r, 0.82f);
		DrawNameTab(num8, num9, 1200f, num7 * Mathf.Clamp01((ease - 0.5f) / 0.5f));
		float num10 = Mathf.Clamp01((ease - 0.6f) / 0.4f) * num7;
		if (num10 > 0f && num6 > 40f)
		{
			Color color2 = TextColor(img);
			color2.a = num10;
			float num11 = 64f;
			float num12 = 1200f - num11 * 2f - 40f;
			float num13 = 0.6f;
			List<string> list = Art.Wrap(lastFull, num12 / num13);
			while (num13 > 0.4f && (float)(list.Count * Art.LineHeight) * num13 > num4 - 56f)
			{
				num13 -= 0.05f;
				list = Art.Wrap(lastFull, num12 / num13);
			}
			float unscaledTime = Time.unscaledTime;
			float num14 = 40f + (num4 - (float)(list.Count * Art.LineHeight) * num13) / 2f;
			int num15 = 0;
			foreach (string item in list)
			{
				float num16 = num8 + num11;
				string[] array = item.Split(new char[1] { ' ' });
				int num17 = 0;
				while (true)
				{
					if (num17 < array.Length)
					{
						string text = array[num17];
						string text2 = text.Trim('!', '?', '.', ',', '"', '\'');
						bool flag = text2.Length >= 2 && text2.ToUpperInvariant() == text2 && text2.ToLowerInvariant() != text2;
						bool flag2 = text.EndsWith("!");
						int num18 = 0;
						while (true)
						{
							if (num18 < text.Length)
							{
								if (num15 >= revealAt.Count)
								{
									goto end_IL_0535;
								}
								float num19 = Mathf.Clamp01((unscaledTime - revealAt[num15]) / 0.12f);
								float num20 = (1f - num19) * (1f - num19);
								float num21 = 0f;
								float num22 = (0f - num20) * 10f * num13;
								if (flag)
								{
									num21 += (Mathf.PerlinNoise(unscaledTime * 30f, num15) - 0.5f) * 3f;
									num22 += (Mathf.PerlinNoise(num15, unscaledTime * 30f) - 0.5f) * 3f;
								}
								if (flag2)
								{
									num22 += Mathf.Sin(unscaledTime * 8f + (float)num15 * 0.7f) * 3f;
								}
								Color c = ((nameColors != null && num15 < nameColors.Length && nameColors[num15].a > 0f) ? nameColors[num15] : color2);
								c.a = color2.a * num19;
								num16 = DrawChar(text[num18], num16, num14, num13, num21, num22, c);
								num18++;
								num15++;
								continue;
							}
							num15++;
							num16 += (Art.Glyphs.TryGetValue(' ', out var value) ? ((float)value.XAdvance * num13) : (16f * num13));
							num17++;
							break;
						}
						continue;
					}
					num14 += (float)Art.LineHeight * num13;
					break;
				}
				continue;
				end_IL_0535:
				break;
			}
		}
		GuiArt.Img img2 = Art.Image("textboxbutton");
		if (img2 != null && arrowT > 0f && ease >= 1f)
		{
			float a = Mathf.Clamp01(arrowT / 0.15f);
			float num23 = Mathf.Sin(arrowT * 8f) * 4f;
			float num24 = img2.W;
			float num25 = img2.H;
			Color color3 = TextColor(img);
			color3.a = a;
			GuiArt.Draw(img2, new Rect(num8 + 1200f - 52f - num24 * 0.4f, 40f + num4 - 40f - num25 * 0.4f + num23, num24 * 0.8f, num25 * 0.8f), new Color(1f, 1f, 1f, a));
		}
		GUI.matrix = matrix;
		GUI.color = Color.white;
	}

	internal static void Slices(GuiArt.Img img, Rect r, float scale)
	{
		float num = (Cols[1] - Cols[0] + (Cols[3] - Cols[2]) + (Cols[5] - Cols[4])) * scale;
		float num2 = Math.Max(0f, (r.width - num) / 2f);
		float[] array = new float[5]
		{
			(Cols[1] - Cols[0]) * scale,
			num2,
			(Cols[3] - Cols[2]) * scale,
			num2,
			(Cols[5] - Cols[4]) * scale
		};
		float num3 = r.x;
		for (int i = 0; i < 5; i++)
		{
			float num4 = (Cols[i] + ((i > 0) ? 1f : 0f)) / (float)img.W;
			float num5 = (Cols[i + 1] - ((i < 4) ? 1f : 0f)) / (float)img.W;
			GUI.DrawTextureWithTexCoords(texCoords: new Rect(num4, 0f, num5 - num4, 1f), position: new Rect(num3 - ((i > 0) ? 0.5f : 0f), r.y, array[i] + ((i > 0 && i < 4) ? 1f : 0.5f), r.height), image: img.Tex);
			num3 += array[i];
		}
	}

	private float DrawChar(char ch, float x, float top, float scale, float ox, float oy, Color c)
	{
		if (!Art.Glyphs.TryGetValue(ch, out var value))
		{
			return x;
		}
		if (value.Tex != null && c.a > 0f)
		{
			GUI.color = c;
			GUI.DrawTexture(new Rect(x + ox + (float)value.XOffset * scale, top + oy + (float)value.YOffset * scale, (float)value.W * scale, (float)value.H * scale), value.Tex);
		}
		return x + (float)value.XAdvance * scale;
	}

	private void DrawNameTab(float bx, float by, float bw, float a)
	{
		if (!(a <= 0f) && !string.IsNullOrEmpty(speaker))
		{
			float num = 0.42f;
			float num2 = Art.Measure(speaker) * num + 40f;
			float num3 = (float)Art.LineHeight * num + 14f;
			float num4 = (isPlayer ? (bx + bw - 150f - num2) : (bx + 150f));
			float num5 = by - num3 * 0.35f;
			Rect position = new Rect(num4, num5, num2, num3);
			GUI.color = new Color(0.84f, 0.78f, 0.82f, a);
			GUI.DrawTexture(new Rect(position.x - 2f, position.y - 2f, position.width + 4f, position.height + 4f), Texture2D.whiteTexture);
			GUI.color = (isPlayer ? new Color(0.36f, 0.16f, 0.2f, a) : new Color(0.19f, 0.13f, 0.17f, a));
			GUI.DrawTexture(position, Texture2D.whiteTexture);
			Color c = ColorFor(speaker);
			c.a = a;
			Art.DrawLine(speaker, num4 + 20f, num5 + 7f, num, c);
		}
	}

	private static Color ColorFor(string name)
	{
		if (NameColor.TryGetValue(name, out var value))
		{
			return value;
		}
		int num = 0;
		string text = name.ToLowerInvariant();
		foreach (char c in text)
		{
			num = num * 31 + c;
		}
		value = Palette[(num & 0x7FFFFFFF) % Palette.Length];
		NameColor[name] = value;
		return value;
	}

	internal static Color[] NameColorsFor(string text)
	{
		return NameColors(text);
	}

	internal static Color ColorOf(string name, Color fallback)
	{
		if (string.IsNullOrEmpty(name) || !NameColor.TryGetValue(name, out var value))
		{
			return fallback;
		}
		return value;
	}

	private static Color[] NameColors(string text)
	{
		Color[] array = new Color[text.Length];
		List<string> list = new List<string>(NameColor.Keys);
		list.Sort((string a, string b) => b.Length.CompareTo(a.Length));
		foreach (string item in list)
		{
			int startIndex = 0;
			int num;
			while ((num = text.IndexOf(item, startIndex, StringComparison.OrdinalIgnoreCase)) >= 0)
			{
				bool num2 = num == 0 || !char.IsLetter(text[num - 1]);
				int num3 = num + item.Length;
				bool flag = num3 >= text.Length || !char.IsLetter(text[num3]) || (text[num3] == 's' && (num3 + 1 >= text.Length || !char.IsLetter(text[num3 + 1])));
				if (num2 && flag && array[num].a == 0f)
				{
					int num4 = ((num3 < text.Length && text[num3] == 's' && (num3 + 1 >= text.Length || !char.IsLetter(text[num3 + 1]))) ? (num3 + 1) : num3);
					for (int num5 = num; num5 < num4; num5++)
					{
						array[num5] = NameColor[item];
					}
				}
				startIndex = num + 1;
			}
		}
		return array;
	}

	private static string SpeakerName(Component npc, string speakerEvent)
	{
		if (npc == null)
		{
			return "";
		}
		string key = npc.GetInstanceID() + "|" + speakerEvent;
		if (names.TryGetValue(key, out var value))
		{
			return value;
		}
		value = "";
		try
		{
			NpcDialogueTitle[] array = UnityEngine.Object.FindObjectsByType<NpcDialogueTitle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (NpcDialogueTitle npcDialogueTitle in array)
			{
				Array array2 = ((TitleNpcs != null) ? (TitleNpcs.GetValue(npcDialogueTitle) as Array) : null);
				bool flag = npcDialogueTitle.gameObject == npc.gameObject;
				if (array2 != null)
				{
					foreach (object item in array2)
					{
						if (item is Component component && component == npc)
						{
							flag = true;
						}
					}
				}
				if (!flag)
				{
					continue;
				}
				Array array3 = ((TitleList != null) ? (TitleList.GetValue(npcDialogueTitle) as Array) : null);
				if (array3 == null)
				{
					continue;
				}
				string text = null;
				foreach (object item2 in array3)
				{
					if (item2 != null)
					{
						FieldInfo field = item2.GetType().GetField("Title");
						FieldInfo field2 = item2.GetType().GetField("SpeakerEvent");
						string text2 = ((field != null) ? (field.GetValue(item2) as string) : null);
						string text3 = ((field2 != null) ? (field2.GetValue(item2) as string) : null);
						if (text == null || text3 == speakerEvent)
						{
							text = text2;
						}
					}
				}
				if (!string.IsNullOrEmpty(text))
				{
					string text4 = BossCard.Lang(text + "_MAIN") ?? BossCard.Lang(text);
					if (!string.IsNullOrEmpty(text4))
					{
						value = BossCard.Clean(text4).Trim();
						break;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Celeste text box speaker: " + ex.Message);
		}
		if (value.Length > 0)
		{
			value = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
		}
		names[key] = value;
		if (value.Length > 0)
		{
			ColorFor(value);
		}
		Plugin.Log.LogInfo("Celeste text box speaker: " + npc.gameObject.name + " -> \"" + value + "\"");
		return value;
	}

	internal static void Slices9(GuiArt.Img img, Rect r, float scale)
	{
		float num = (Cols[1] - Cols[0] + (Cols[3] - Cols[2]) + (Cols[5] - Cols[4])) * scale;
		float num2 = Math.Max(0f, (r.width - num) / 2f);
		float[] array = new float[5]
		{
			(Cols[1] - Cols[0]) * scale,
			num2,
			(Cols[3] - Cols[2]) * scale,
			num2,
			(Cols[5] - Cols[4]) * scale
		};
		float num3 = Math.Min((Rows[1] - Rows[0]) * scale, r.height / 2f);
		float num4 = Math.Min((Rows[3] - Rows[2]) * scale, r.height / 2f);
		float[] array2 = new float[3]
		{
			num3,
			Math.Max(0f, r.height - num3 - num4),
			num4
		};
		float num5 = r.y;
		for (int i = 0; i < 3; i++)
		{
			float num6 = 1f - (Rows[i] + ((i > 0) ? 1f : 0f)) / (float)img.H;
			float num7 = 1f - (Rows[i + 1] - ((i < 2) ? 1f : 0f)) / (float)img.H;
			float num8 = r.x;
			for (int j = 0; j < 5; j++)
			{
				float num9 = (Cols[j] + ((j > 0) ? 1f : 0f)) / (float)img.W;
				float num10 = (Cols[j + 1] - ((j < 4) ? 1f : 0f)) / (float)img.W;
				if (array2[i] > 0f)
				{
					GUI.DrawTextureWithTexCoords(new Rect(num8 - ((j > 0) ? 0.5f : 0f), num5 - ((i > 0) ? 0.5f : 0f), array[j] + ((j > 0 && j < 4) ? 1f : 0.5f), array2[i] + ((i == 1) ? 1f : 0.5f)), img.Tex, new Rect(num9, num7, num10 - num9, num6 - num7));
				}
				num8 += array[j];
			}
			num5 += array2[i];
		}
	}

	private static float BackOut(float t)
	{
		t = Mathf.Clamp01(t) - 1f;
		return t * t * (2.2f * t + 1.2f) + 1f;
	}

	private static void NineSlice(GuiArt.Img img, Rect r, Color c)
	{
		float num = (float)Mathf.Min(img.W, img.H) / 3f;
		float num2 = num / (float)img.W;
		float num3 = num / (float)img.H;
		float num4 = Mathf.Min(num, r.width / 2f);
		float num5 = Mathf.Min(num, r.height / 2f);
		GUI.color = c;
		float[] array = new float[4]
		{
			r.x,
			r.x + num4,
			r.xMax - num4,
			r.xMax
		};
		float[] array2 = new float[4]
		{
			r.y,
			r.y + num5,
			r.yMax - num5,
			r.yMax
		};
		float[] array3 = new float[4]
		{
			0f,
			num2,
			1f - num2,
			1f
		};
		float[] array4 = new float[4]
		{
			1f,
			1f - num3,
			num3,
			0f
		};
		for (int i = 0; i < 3; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				Rect position = new Rect(array[i], array2[j], array[i + 1] - array[i], array2[j + 1] - array2[j]);
				if (!(position.width <= 0f) && !(position.height <= 0f))
				{
					GUI.DrawTextureWithTexCoords(texCoords: new Rect(array3[i], array4[j + 1], array3[i + 1] - array3[i], array4[j] - array4[j + 1]), position: position, image: img.Tex);
				}
			}
		}
	}

	private static Color TextColor(GuiArt.Img img)
	{
		if (ink.HasValue)
		{
			return ink.Value;
		}
		ink = (Art.TextboxDark ? new Color(0.93f, 0.92f, 0.94f) : new Color(0.13f, 0.11f, 0.12f));
		return ink.Value;
	}
}
