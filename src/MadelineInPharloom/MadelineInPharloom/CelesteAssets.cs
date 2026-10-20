using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

public class CelesteAssets
{
	internal struct SubTex
	{
		public int Page;

		public int X;

		public int Y;

		public int W;

		public int H;

		public int OffX;

		public int OffY;

		public int FullW;

		public int FullH;
	}

	internal class Page
	{
		public int W;

		public int H;

		public byte[] Rgba;
	}

	public class Prepared
	{
		internal readonly Dictionary<string, SubTex> Textures = new Dictionary<string, SubTex>();

		internal readonly List<Page> Pages = new List<Page>();

		internal XmlNode Sprites;
	}

	public readonly Dictionary<string, CelesteAnim> Anims = new Dictionary<string, CelesteAnim>();

	public Sprite HairNode;

	public readonly List<Sprite> Bangs = new List<Sprite>();

	public Vector2 Origin = new Vector2(16f, 32f);

	public List<Sprite> Slash = new List<Sprite>();

	public List<Sprite> Smoke = new List<Sprite>();

	public Sprite Feather;

	public readonly List<Sprite> GliderOpen = new List<Sprite>();

	public readonly List<Sprite> GliderLoop = new List<Sprite>();

	public readonly List<Sprite> StarFlyWhite = new List<Sprite>();

	public readonly Sprite[] PicoBody = new Sprite[8];

	public readonly Sprite[] PicoHair = new Sprite[8];

	public Sprite PicoCircle1;

	public Sprite PicoCircle2;

	public readonly List<Sprite> GoldIdle = new List<Sprite>();

	public readonly List<Sprite> GoldWings = new List<Sprite>();

	// the Companion (user 2026-10-19): Celeste's bird and a strawberry
	public readonly List<Sprite> BirdFly = new List<Sprite>();

	public readonly List<Sprite> BirdCrow = new List<Sprite>();

	public readonly List<Sprite> Berry = new List<Sprite>();

	/// <summary>Every floating Companion's frames by name, made once and shared by all skins (user 2026-10-19).</summary>
	public static Dictionary<string, List<Sprite>> Companions;

	// name, the frames' path, zero-padded numbers, how many frames
	private static readonly string[][] CompanionPaths = new string[10][]
	{
		new string[4] { "Strawberry", "collectables/strawberry/normal", "2", "7" },
		new string[4] { "Golden Berry", "collectables/goldberry/idle", "2", "6" },
		new string[4] { "Winged Berry", "collectables/strawberry/wings", "2", "9" },
		new string[4] { "Moon Berry", "collectables/moonBerry/normal", "2", "7" },
		new string[4] { "Crystal Heart", "collectables/heartGem/0/", "2", "14" },
		new string[4] { "Cassette", "collectables/cassette/idle", "2", "13" },
		new string[4] { "Key", "collectables/key/idle", "2", "14" },
		new string[4] { "Dash Crystal", "objects/refill/idle", "2", "5" },
		new string[4] { "Feather", "objects/flyFeather/idle", "2", "21" },
		new string[4] { "Jellyfish", "objects/glider/idle", "1", "10" }
	};

	private static void BuildCompanions(Prepared p)
	{
		Companions = new Dictionary<string, List<Sprite>>();
		foreach (string[] c in CompanionPaths)
		{
			List<Sprite> list = new List<Sprite>();
			int n = int.Parse(c[3], CultureInfo.InvariantCulture);
			for (int f = 0; f < n; f++)
			{
				if (p.Textures.TryGetValue(c[1] + (c[2] == "2" ? f.ToString("00") : f.ToString()), out SubTex st))
				{
					list.Add(MakeSprite(p.Pages[st.Page], st, new Vector2((float)st.FullW / 2f, (float)st.FullH / 2f)));
				}
			}
			if (list.Count > 0)
			{
				Companions[c[0]] = list;
			}
		}
		Plugin.Log.LogInfo("Companions: " + Companions.Count + " ready");
	}

	private const string SlashPath = "effects/slash/";

	private const string SmokePath = "particles/smoke";

	private const string FeatherPath = "particles/feather";

	private const string PicoPath = "pico8/atlas";

	private const string GoldPath = "collectables/goldberry/";

	private const string GliderPath = "objects/glider/";

	private static readonly Color32 PicoHairRed = new Color32(byte.MaxValue, 0, 77, byte.MaxValue);

	public static Prepared Prepare(string celesteDir)
	{
		Prepared prepared = new Prepared();
		string path = Path.Combine(celesteDir, "Content/Graphics/Atlases");
		using (BinaryReader binaryReader = new BinaryReader(File.OpenRead(Path.Combine(path, "Gameplay.meta"))))
		{
			binaryReader.ReadInt32();
			binaryReader.ReadString();
			binaryReader.ReadInt32();
			int num = binaryReader.ReadInt16();
			for (int i = 0; i < num; i++)
			{
				string text = binaryReader.ReadString();
				int num2 = binaryReader.ReadInt16();
				bool flag = false;
				for (int j = 0; j < num2; j++)
				{
					string text2 = binaryReader.ReadString().Replace('\\', '/');
					SubTex value = new SubTex
					{
						Page = i,
						X = binaryReader.ReadInt16(),
						Y = binaryReader.ReadInt16(),
						W = binaryReader.ReadInt16(),
						H = binaryReader.ReadInt16()
					};
					value.OffX = -binaryReader.ReadInt16();
					value.OffY = -binaryReader.ReadInt16();
					value.FullW = binaryReader.ReadInt16();
					value.FullH = binaryReader.ReadInt16();
					if (text2.StartsWith("characters/player", StringComparison.Ordinal) || text2.StartsWith("effects/slash/", StringComparison.Ordinal) || text2.StartsWith("particles/smoke", StringComparison.Ordinal) || text2 == "particles/feather" || text2 == "pico8/atlas" || text2.StartsWith("collectables/goldberry/", StringComparison.Ordinal) || text2.StartsWith("objects/glider/", StringComparison.Ordinal) || text2.StartsWith("characters/bird/", StringComparison.Ordinal) || text2.StartsWith("collectables/strawberry/", StringComparison.Ordinal) || text2.StartsWith("collectables/moonBerry/normal", StringComparison.Ordinal) || text2.StartsWith("collectables/heartGem/0/", StringComparison.Ordinal) || text2.StartsWith("collectables/cassette/idle", StringComparison.Ordinal) || text2.StartsWith("collectables/key/idle", StringComparison.Ordinal) || text2.StartsWith("objects/refill/idle", StringComparison.Ordinal) || text2.StartsWith("objects/flyFeather/idle", StringComparison.Ordinal) || text2.StartsWith("tilesets/", StringComparison.Ordinal) || text2.StartsWith("danger/", StringComparison.Ordinal) || text2.StartsWith("objects/", StringComparison.Ordinal) || text2.StartsWith("decals/", StringComparison.Ordinal) || text2.StartsWith("bgs/", StringComparison.Ordinal))
					{
						prepared.Textures[text2] = value;
						flag = true;
					}
				}
				prepared.Pages.Add(flag ? DecodeData(Path.Combine(path, text + ".data")) : null);
			}
		}
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.Load(Path.Combine(celesteDir, "Content/Graphics/Sprites.xml"));
		prepared.Sprites = xmlDocument.SelectSingleNode("/Sprites");
		if (prepared.Sprites == null || prepared.Sprites.SelectSingleNode("player") == null)
		{
			throw new Exception("Sprites.xml has no <player>");
		}
		return prepared;
	}

	public static List<Sprite> BuildSlash(Prepared p)
	{
		List<Sprite> list = new List<Sprite>();
		int num = 0;
		SubTex value;
		while (p.Textures.TryGetValue("effects/slash/" + num.ToString("00"), out value))
		{
			list.Add(MakeSprite(p.Pages[value.Page], value, new Vector2((float)value.FullW / 2f, (float)value.FullH / 2f)));
			num++;
		}
		return list;
	}

	internal static Page DecodeData(string file)
	{
		byte[] array = File.ReadAllBytes(file);
		int num = BitConverter.ToInt32(array, 0);
		int num2 = BitConverter.ToInt32(array, 4);
		bool flag = array[8] == 1;
		byte[] array2 = new byte[num * num2 * 4];
		int num3 = 9;
		int i = 0;
		int num5;
		for (int num4 = array2.Length; i < num4; i += num5)
		{
			if (num3 >= array.Length)
			{
				break;
			}
			num5 = array[num3] * 4;
			byte b;
			byte b2;
			byte b3;
			byte b4;
			if (flag)
			{
				b = array[num3 + 1];
				if (b > 0)
				{
					b2 = array[num3 + 4];
					b3 = array[num3 + 3];
					b4 = array[num3 + 2];
					num3 += 5;
				}
				else
				{
					b2 = (b3 = (b4 = 0));
					num3 += 2;
				}
			}
			else
			{
				b2 = array[num3 + 3];
				b3 = array[num3 + 2];
				b4 = array[num3 + 1];
				b = byte.MaxValue;
				num3 += 4;
			}
			if (b > 0 && b < byte.MaxValue)
			{
				b2 = (byte)Math.Min(255, b2 * 255 / b);
				b3 = (byte)Math.Min(255, b3 * 255 / b);
				b4 = (byte)Math.Min(255, b4 * 255 / b);
			}
			int j = i;
			for (int num6 = Math.Min(i + num5, num4); j < num6; j += 4)
			{
				array2[j] = b2;
				array2[j + 1] = b3;
				array2[j + 2] = b4;
				array2[j + 3] = b;
			}
		}
		return new Page
		{
			W = num,
			H = num2,
			Rgba = array2
		};
	}

	public static CelesteAssets Build(Prepared p, string spriteId, string palette = "none", string eraseBackpack = null, string fallbackPalette = "none", string bakedHair = null)
	{
		CelesteAssets celesteAssets = new CelesteAssets();
		Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
		Dictionary<string, Sprite> masks = new Dictionary<string, Sprite>();
		Dictionary<int, Color32> ownMap = ParsePalette(palette);
		Dictionary<int, Color32> fallbackMap = ParsePalette(fallbackPalette);
		if (ownMap != null && bakedHair != null && !ownMap.ContainsKey(11285042))
		{
			int num = int.Parse(bakedHair, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			Color32 value = new Color32((byte)(num >> 16), (byte)(num >> 8), (byte)num, byte.MaxValue);
			ownMap[11285042] = value;
			ownMap[5904922] = new Color32((byte)((float)(int)value.r * 0.52f), (byte)((float)(int)value.g * 0.52f), (byte)((float)(int)value.b * 0.52f), byte.MaxValue);
		}
		XmlNode xmlNode = p.Sprites.SelectSingleNode(spriteId);
		string copyRoot = ((xmlNode != null && Attr(xmlNode, "copy", null) != null) ? Attr(p.Sprites.SelectSingleNode(Attr(xmlNode, "copy", null)), "path", null) : null);
		Dictionary<int, Color32> eraseMap = ((eraseBackpack != null) ? (ParsePalette(eraseBackpack) ?? new Dictionary<int, Color32>()) : null);
		string ownPath = Attr(p.Sprites.SelectSingleNode(spriteId), "path", "");
		Func<string, Vector2, Sprite> func = delegate(string name, Vector2 origin)
		{
			if (cache.TryGetValue(name, out var value13))
			{
				return value13;
			}
			if (!p.Textures.TryGetValue(name, out var value14))
			{
				return (Sprite)null;
			}
			Dictionary<int, Color32> map = ((fallbackMap != null && copyRoot != null && name.StartsWith(copyRoot, StringComparison.Ordinal) && !name.Contains("/hair") && !name.Contains("/bangs")) ? fallbackMap : ownMap);
			string text7 = (name.StartsWith(ownPath, StringComparison.Ordinal) ? name.Substring(ownPath.Length) : null);
			if (eraseMap != null && text7 != null && p.Textures.TryGetValue("characters/player/" + text7, out var value15) && p.Textures.TryGetValue("characters/player_no_backpack/" + text7, out var value16) && value15.FullW == value14.FullW && value15.FullH == value14.FullH && value16.FullW == value14.FullW && value16.FullH == value14.FullH)
			{
				Color32[] array7 = Full(p.Pages[value14.Page], value14);
				Color32[] array8 = Full(p.Pages[value15.Page], value15);
				Color32[] array9 = Full(p.Pages[value16.Page], value16);
				for (int i = 0; i < array7.Length; i++)
				{
					if (!Same(array8[i], array9[i]))
					{
						Color32 color = array9[i];
						if (color.a > 0 && eraseMap.TryGetValue((color.r << 16) | (color.g << 8) | color.b, out var value17))
						{
							value17.a = color.a;
							color = value17;
						}
						array7[i] = color;
					}
				}
				value13 = FromPixels(value14.FullW, value14.FullH, array7, origin);
			}
			else
			{
				value13 = MakeSprite(p.Pages[value14.Page], value14, origin, map);
			}
			value13.name = name;
			cache[name] = value13;
			return value13;
		};
		XmlNode xmlNode2 = p.Sprites.SelectSingleNode(spriteId);
		if (xmlNode2 == null)
		{
			throw new Exception("Sprites.xml has no <" + spriteId + ">");
		}
		string text = Attr(xmlNode2, "path", "");
		string text2 = Attr(xmlNode2, "copy", null);
		XmlNode xmlNode3 = ((text2 != null) ? p.Sprites.SelectSingleNode(text2) : xmlNode2);
		XmlNode xmlNode4 = xmlNode3.SelectSingleNode("Origin");
		if (xmlNode4 != null)
		{
			celesteAssets.Origin = new Vector2(F(Attr(xmlNode4, "x", "16")), F(Attr(xmlNode4, "y", "32")));
		}
		Dictionary<string, string[]> dictionary = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
		foreach (XmlNode item in xmlNode3.SelectNodes("Metadata/Frames"))
		{
			dictionary[Attr(item, "path", "")] = Attr(item, "hair", "").Split(new char[1] { '|' });
		}
		Dictionary<string, string[]> dictionary2 = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
		foreach (XmlNode item2 in xmlNode3.SelectNodes("Metadata/Frames"))
		{
			string text3 = Attr(item2, "carry", null);
			if (text3 != null)
			{
				dictionary2[Attr(item2, "path", "")] = text3.Split(new char[1] { ',' });
			}
		}
		foreach (XmlNode childNode in xmlNode3.ChildNodes)
		{
			if (childNode.NodeType != XmlNodeType.Element || (childNode.Name != "Anim" && childNode.Name != "Loop"))
			{
				continue;
			}
			string text4 = Attr(childNode, "path", "");
			List<string> list = new List<string>();
			string[] array = new string[2]
			{
				text,
				(text2 != null) ? Attr(xmlNode3, "path", text) : text
			};
			foreach (string text5 in array)
			{
				int num3 = 0;
				while (p.Textures.ContainsKey(text5 + text4 + num3.ToString("00")))
				{
					list.Add(text5 + text4 + num3.ToString("00"));
					num3++;
				}
				if (list.Count == 0 && p.Textures.ContainsKey(text5 + text4))
				{
					list.Add(text5 + text4);
				}
				if (list.Count > 0)
				{
					break;
				}
			}
			if (list.Count == 0)
			{
				continue;
			}
			CelesteAnim celesteAnim = new CelesteAnim
			{
				Id = Attr(childNode, "id", ""),
				Delay = F(Attr(childNode, "delay", "0")),
				Loop = (childNode.Name == "Loop"),
				Goto = Attr(childNode, "goto", null)
			};
			dictionary.TryGetValue(text4.TrimEnd(new char[1] { '/' }), out var value2);
			dictionary2.TryGetValue(text4.TrimEnd(new char[1] { '/' }), out var value3);
			foreach (int item3 in ParseFrames(Attr(childNode, "frames", null), list.Count))
			{
				CelesteFrame celesteFrame = new CelesteFrame
				{
					Sprite = func(list[item3], celesteAssets.Origin),
					HasHair = false
				};
				if (value3 != null && item3 < value3.Length)
				{
					celesteFrame.CarryY = int.Parse(value3[item3].Trim(), CultureInfo.InvariantCulture);
				}
				// a frame past the end of the hair list keeps the last entry (Celeste's faint lists 10 for 11 frames, so
				// the lying-down frame lost her head, which is drawn with the hair; user 2026-10-19)
				if (value2 != null && value2.Length > 0)
				{
					string text6 = value2[Math.Min(item3, value2.Length - 1)].Trim();
					if (!text6.Equals("x", StringComparison.OrdinalIgnoreCase) && text6.Length > 0)
					{
						celesteFrame.HasHair = true;
						string[] array2 = text6.Split(new char[1] { ':' });
						string[] array3 = array2[0].Split(new char[1] { ',' });
						celesteFrame.HairOffset = new Vector2(F(array3[0]), F(array3[1]));
						celesteFrame.HairFrame = ((array2.Length > 1) ? int.Parse(array2[1].Trim(), CultureInfo.InvariantCulture) : 0);
					}
				}
				if (!celesteFrame.HasHair)
				{
					celesteFrame.HairMask = HairMask(p, list[item3], celesteAssets.Origin, masks);
				}
				celesteAnim.Frames.Add(celesteFrame);
			}
			celesteAssets.Anims[celesteAnim.Id] = celesteAnim;
		}
		if (spriteId == "player_no_backpack")
		{
			string value4 = Attr(xmlNode3, "path", "characters/player/");
			string[] array = "sleep=sitDown:last;asleep=sitDown:last;halfWakeUp=sitDown:last;wakeUp=sitDown:reverse;tired=idle;tiredStill=idle;fall=fallSlow;bigFall=fallSlow;bigFallRecover=duck;fallPose=fallSlow;faint=duck:last;fainted=duck:last;hug=idle;slide=duck;edgeBack=edge".Split(new char[1] { ';' });
			for (int num2 = 0; num2 < array.Length; num2++)
			{
				string[] array4 = array[num2].Split(new char[1] { '=' });
				string[] array5 = array4[1].Split(new char[1] { ':' });
				if (celesteAssets.Anims.TryGetValue(array4[0], out var value5) && celesteAssets.Anims.TryGetValue(array5[0], out var value6) && value6.Frames.Count != 0 && (value5.Frames.Count <= 0 || !(value5.Frames[0].Sprite != null) || value5.Frames[0].Sprite.name.StartsWith(value4, StringComparison.Ordinal)))
				{
					List<CelesteFrame> list2 = new List<CelesteFrame>(value6.Frames);
					if (array5.Length > 1 && array5[1] == "last")
					{
						list2 = new List<CelesteFrame> { value6.Frames[value6.Frames.Count - 1] };
					}
					else if (array5.Length > 1 && array5[1] == "reverse")
					{
						list2.Reverse();
					}
					value5.Frames = list2;
				}
			}
		}
		celesteAssets.HairNode = func("characters/player/hair00", new Vector2(5f, 5f));
		int num4 = 0;
		while (p.Textures.ContainsKey("characters/player/bangs" + num4.ToString("00")))
		{
			celesteAssets.Bangs.Add(func("characters/player/bangs" + num4.ToString("00"), new Vector2(5f, 5f)));
			num4++;
		}
		AnimRow[] all = Animations.All;
		for (int num2 = 0; num2 < all.Length; num2++)
		{
			AnimRow animRow = all[num2];
			if (!celesteAssets.Anims.ContainsKey(animRow.CelesteAnim))
			{
				throw new Exception("Celeste animation missing: " + animRow.CelesteAnim);
			}
		}
		celesteAssets.Slash = BuildSlash(p);
		int num5 = 0;
		SubTex value7;
		while (p.Textures.TryGetValue("particles/smoke" + num5, out value7))
		{
			celesteAssets.Smoke.Add(MakeSprite(p.Pages[value7.Page], value7, new Vector2((float)value7.FullW / 2f, (float)value7.FullH / 2f)));
			num5++;
		}
		KeyValuePair<string, List<Sprite>>[] array6 = new KeyValuePair<string, List<Sprite>>[2]
		{
			new KeyValuePair<string, List<Sprite>>("fall", celesteAssets.GliderOpen),
			new KeyValuePair<string, List<Sprite>>("fallLoop", celesteAssets.GliderLoop)
		};
		for (int num2 = 0; num2 < array6.Length; num2++)
		{
			KeyValuePair<string, List<Sprite>> keyValuePair = array6[num2];
			int num6 = 0;
			SubTex value8;
			while (p.Textures.TryGetValue("objects/glider/" + keyValuePair.Key + num6, out value8))
			{
				keyValuePair.Value.Add(MakeSprite(p.Pages[value8.Page], value8, new Vector2((float)value8.FullW / 2f, (float)value8.FullH * 0.58f)));
				num6++;
			}
		}
		if (p.Textures.TryGetValue("particles/feather", out var value9))
		{
			celesteAssets.Feather = MakeSprite(p.Pages[value9.Page], value9, new Vector2((float)value9.FullW / 2f, (float)value9.FullH / 2f));
		}
		int num7 = 0;
		SubTex value10;
		while (p.Textures.TryGetValue(text + "startStarFlyWhite" + num7.ToString("00"), out value10))
		{
			celesteAssets.StarFlyWhite.Add(func(text + "startStarFlyWhite" + num7.ToString("00"), celesteAssets.Origin));
			num7++;
		}
		array6 = new KeyValuePair<string, List<Sprite>>[2]
		{
			new KeyValuePair<string, List<Sprite>>("idle", celesteAssets.GoldIdle),
			new KeyValuePair<string, List<Sprite>>("wings", celesteAssets.GoldWings)
		};
		for (int num2 = 0; num2 < array6.Length; num2++)
		{
			KeyValuePair<string, List<Sprite>> keyValuePair2 = array6[num2];
			int num8 = 0;
			SubTex value11;
			while (p.Textures.TryGetValue("collectables/goldberry/" + keyValuePair2.Key + num8.ToString("00"), out value11))
			{
				keyValuePair2.Value.Add(MakeSprite(p.Pages[value11.Page], value11, new Vector2((float)value11.FullW / 2f, (float)value11.FullH / 2f)));
				num8++;
			}
		}
		if (p.Textures.TryGetValue("pico8/atlas", out var value12))
		{
			BuildPico(celesteAssets, p.Pages[value12.Page], value12);
		}
		if (Companions == null)
		{
			BuildCompanions(p);
		}
		// the Companion's frames: the bird stands on its feet (Celeste's origin 16,24), the berry floats from its middle
		foreach (KeyValuePair<string, List<Sprite>> kv in new KeyValuePair<string, List<Sprite>>[3]
		{
			new KeyValuePair<string, List<Sprite>>("characters/bird/fly", celesteAssets.BirdFly),
			new KeyValuePair<string, List<Sprite>>("characters/bird/crow", celesteAssets.BirdCrow),
			new KeyValuePair<string, List<Sprite>>("collectables/strawberry/normal", celesteAssets.Berry)
		})
		{
			int f = 0;
			while (p.Textures.TryGetValue(kv.Key + f.ToString("00"), out SubTex st))
			{
				Vector2 o = kv.Key.Contains("bird") ? new Vector2(16f, 24f) : new Vector2((float)st.FullW / 2f, (float)st.FullH / 2f);
				kv.Value.Add(MakeSprite(p.Pages[st.Page], st, o));
				f++;
			}
		}
		return celesteAssets;
	}

	private static void BuildPico(CelesteAssets a, Page page, SubTex t)
	{
		float ppu = 4f;
		for (int i = 0; i < 8; i++)
		{
			Color32[] array = new Color32[64];
			Color32[] array2 = new Color32[64];
			for (int j = 0; j < 8; j++)
			{
				for (int k = 0; k < 8; k++)
				{
					int num = i * 8 + k - t.OffX;
					int num2 = j - t.OffY;
					if (num < 0 || num2 < 0 || num >= t.W || num2 >= t.H)
					{
						continue;
					}
					int num3 = ((t.Y + num2) * page.W + (t.X + num)) * 4;
					Color32 color = new Color32(page.Rgba[num3], page.Rgba[num3 + 1], page.Rgba[num3 + 2], page.Rgba[num3 + 3]);
					if (color.a != 0)
					{
						int num4 = (7 - j) * 8 + k;
						if (Math.Abs(color.r - PicoHairRed.r) < 24 && Math.Abs(color.g - PicoHairRed.g) < 24 && Math.Abs(color.b - PicoHairRed.b) < 24)
						{
							array2[num4] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
						}
						else
						{
							array[num4] = color;
						}
					}
				}
			}
			a.PicoBody[i] = Pixels(8, 8, array, new Vector2(0.5f, 0f), ppu);
			a.PicoHair[i] = Pixels(8, 8, array2, new Vector2(0.5f, 0f), ppu);
		}
		a.PicoCircle1 = Pixels(3, 3, Shape(3, "010111010"), new Vector2(0.5f, 0.5f), ppu);
		a.PicoCircle2 = Pixels(5, 5, Shape(5, "0111011111111111111101110"), new Vector2(0.5f, 0.5f), ppu);
	}

	private static Color32[] Shape(int size, string bits)
	{
		Color32[] array = new Color32[size * size];
		for (int i = 0; i < bits.Length; i++)
		{
			if (bits[i] == '1')
			{
				array[i] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
			}
		}
		return array;
	}

	private static Sprite Pixels(int w, int h, Color32[] px, Vector2 pivot, float ppu)
	{
		Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
		texture2D.filterMode = FilterMode.Point;
		texture2D.wrapMode = TextureWrapMode.Clamp;
		texture2D.SetPixels32(px);
		texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		return Sprite.Create(texture2D, new Rect(0f, 0f, w, h), pivot, ppu, 0u, SpriteMeshType.FullRect);
	}

	/// <summary>The hair painted into a frame (Madeline's red and its dark shade), as white and grey on clear, or null
	/// when the frame has none (user 2026-10-19: baked hair ignored the skin's colours and the second dash).</summary>
	private static Sprite HairMask(Prepared p, string name, Vector2 origin, Dictionary<string, Sprite> masks)
	{
		if (name == null)
		{
			return null;
		}
		if (masks.TryGetValue(name, out Sprite done))
		{
			return done;
		}
		Sprite result = null;
		if (p.Textures.TryGetValue(name, out SubTex t) && t.Page < p.Pages.Count && p.Pages[t.Page] != null)
		{
			Color32[] px = Full(p.Pages[t.Page], t);
			bool any = false;
			for (int i = 0; i < px.Length; i++)
			{
				Color32 c = px[i];
				int rgb = (c.r << 16) | (c.g << 8) | c.b;
				if (c.a > 0 && rgb == 0xAC3232)
				{
					px[i] = new Color32(255, 255, 255, c.a);
					any = true;
				}
				else if (c.a > 0 && rgb == 0x5A1A1A)
				{
					px[i] = new Color32(133, 133, 133, c.a);
					any = true;
				}
				else
				{
					px[i] = new Color32(0, 0, 0, 0);
				}
			}
			if (any)
			{
				result = FromPixels(t.FullW, t.FullH, px, origin);
				result.name = name + "#hair";
			}
		}
		masks[name] = result;
		return result;
	}

	private static Color32[] Full(Page page, SubTex t)
	{
		Color32[] array = new Color32[t.FullW * t.FullH];
		for (int i = 0; i < t.H; i++)
		{
			for (int j = 0; j < t.W; j++)
			{
				int num = t.OffX + j;
				int num2 = t.OffY + i;
				if (num >= 0 && num2 >= 0 && num < t.FullW && num2 < t.FullH)
				{
					int num3 = ((t.Y + i) * page.W + (t.X + j)) * 4;
					array[(t.FullH - 1 - num2) * t.FullW + num] = new Color32(page.Rgba[num3], page.Rgba[num3 + 1], page.Rgba[num3 + 2], page.Rgba[num3 + 3]);
				}
			}
		}
		return array;
	}

	private static bool Same(Color32 a, Color32 b)
	{
		if (a.a == b.a)
		{
			if (a.a != 0)
			{
				if (a.r == b.r && a.g == b.g)
				{
					return a.b == b.b;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private static Sprite FromPixels(int w, int h, Color32[] px, Vector2 originPx)
	{
		Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
		texture2D.filterMode = FilterMode.Point;
		texture2D.wrapMode = TextureWrapMode.Clamp;
		texture2D.SetPixels32(px);
		texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		return Sprite.Create(texture2D, new Rect(0f, 0f, w, h), new Vector2(originPx.x / (float)w, 1f - originPx.y / (float)h), 8f, 0u, SpriteMeshType.FullRect);
	}

	private static Dictionary<int, Color32> ParsePalette(string palette)
	{
		if (string.IsNullOrEmpty(palette) || palette == "none")
		{
			return null;
		}
		Dictionary<int, Color32> dictionary = new Dictionary<int, Color32>();
		string[] array = palette.Split(new char[1] { ';' });
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(new char[1] { '>' });
			int key = int.Parse(array2[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			int num = int.Parse(array2[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			dictionary[key] = new Color32((byte)(num >> 16), (byte)(num >> 8), (byte)num, byte.MaxValue);
		}
		return dictionary;
	}

	private static Sprite MakeSprite(Page page, SubTex t, Vector2 originPx, Dictionary<int, Color32> map = null)
	{
		Texture2D texture2D = new Texture2D(t.FullW, t.FullH, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Point,
			wrapMode = TextureWrapMode.Clamp
		};
		Color32[] array = new Color32[t.FullW * t.FullH];
		for (int i = 0; i < t.H; i++)
		{
			for (int j = 0; j < t.W; j++)
			{
				int num = ((t.Y + i) * page.W + (t.X + j)) * 4;
				int num2 = t.OffX + j;
				int num3 = t.OffY + i;
				if (num2 >= 0 && num3 >= 0 && num2 < t.FullW && num3 < t.FullH)
				{
					Color32 color = new Color32(page.Rgba[num], page.Rgba[num + 1], page.Rgba[num + 2], page.Rgba[num + 3]);
					if (map != null && color.a > 0 && map.TryGetValue((color.r << 16) | (color.g << 8) | color.b, out var value))
					{
						value.a = color.a;
						color = value;
					}
					array[(t.FullH - 1 - num3) * t.FullW + num2] = color;
				}
			}
		}
		texture2D.SetPixels32(array);
		texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		return Sprite.Create(pivot: new Vector2(originPx.x / (float)t.FullW, 1f - originPx.y / (float)t.FullH), texture: texture2D, rect: new Rect(0f, 0f, t.FullW, t.FullH), pixelsPerUnit: 8f, extrude: 0u, meshType: SpriteMeshType.FullRect);
	}

	private static IEnumerable<int> ParseFrames(string spec, int count)
	{
		if (string.IsNullOrEmpty(spec))
		{
			for (int i = 0; i < count; i++)
			{
				yield return i;
			}
			yield break;
		}
		string[] array = spec.Split(new char[1] { ',' });
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			int times = 1;
			int num = text.IndexOf('*');
			if (num >= 0)
			{
				times = int.Parse(text.Substring(num + 1), CultureInfo.InvariantCulture);
				text = text.Substring(0, num);
			}
			int num2 = text.IndexOf('-');
			int from = int.Parse((num2 >= 0) ? text.Substring(0, num2) : text, CultureInfo.InvariantCulture);
			int to = ((num2 >= 0) ? int.Parse(text.Substring(num2 + 1), CultureInfo.InvariantCulture) : from);
			for (int t = 0; t < times; t++)
			{
				for (int j = from; j <= to; j++)
				{
					if (j < count)
					{
						yield return j;
					}
				}
			}
		}
	}

	private static string Attr(XmlNode n, string name, string fallback)
	{
		XmlAttribute xmlAttribute = ((n.Attributes == null) ? null : n.Attributes[name]);
		if (xmlAttribute != null)
		{
			return xmlAttribute.Value;
		}
		return fallback;
	}

	private static float F(string s)
	{
		return float.Parse(s.Trim(), CultureInfo.InvariantCulture);
	}
}
