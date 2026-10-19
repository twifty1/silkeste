using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;

namespace MadelineInPharloom;

public class GuiArt
{
	internal class Img
	{
		public int W;

		public int H;

		public int XOffset;

		public int YOffset;

		public int XAdvance;

		public byte[] Rgba;

		public Texture2D Tex;
	}

	internal Img Skull;

	internal Img X;

	internal Img Postcard;

	internal readonly Img[] Digits = new Img[10];

	internal readonly Dictionary<char, Img> Glyphs = new Dictionary<char, Img>();

	internal int LineHeight;

	internal bool TextboxDark = true;

	private static readonly string[] Extra = new string[37]
	{
		"collectables/goldberry", "collectables/strawberry", "collectables/skullGold", "collectables/dashes", "collectables/heartgem/0/spin00", "collectables/heartgem/1/spin00", "collectables/heartgem/2/spin00", "collectables/heartgem/2/spin01", "collectables/heartgem/2/spin02", "collectables/heartgem/2/spin03",
		"collectables/heartgem/2/spin04", "collectables/heartgem/2/spin05", "collectables/heartgem/2/spin06", "collectables/heartgem/2/spin07", "collectables/heartgem/2/spin08", "collectables/heartgem/2/spin09", "collectables/heartgem/2/spin10", "strawberryCountBG", "areaselect/card", "areaselect/card_golden",
		"areaselect/cardtop", "areaselect/cardtop_golden", "areaselect/title", "areaselect/accent", "assistmode", "assistmodestamp", "textboxbutton", "menu/start", "menu/options", "menu/exit",
		"menu/journal", "menu/credits", "menu/play", "adjustarrowleft", "adjustarrowright", "dotarrow", "dotarrow_outline"
	};

	internal readonly Dictionary<string, Img> Images = new Dictionary<string, Img>();

	internal Img Image(string name)
	{
		if (!Images.TryGetValue(name, out var value) || !(value.Tex != null))
		{
			return null;
		}
		return value;
	}

	public static GuiArt Prepare(string celesteDir)
	{
		GuiArt guiArt = new GuiArt();
		string atlasDir = Path.Combine(celesteDir, "Content/Graphics/Atlases");
		Dictionary<string, CelesteAssets.SubTex> dictionary = new Dictionary<string, CelesteAssets.SubTex>();
		List<string> pageNames = new List<string>();
		using (BinaryReader binaryReader = new BinaryReader(File.OpenRead(Path.Combine(atlasDir, "Gui.meta"))))
		{
			binaryReader.ReadInt32();
			binaryReader.ReadString();
			binaryReader.ReadInt32();
			int num = binaryReader.ReadInt16();
			for (int i = 0; i < num; i++)
			{
				pageNames.Add(binaryReader.ReadString());
				int num2 = binaryReader.ReadInt16();
				for (int j = 0; j < num2; j++)
				{
					string text = binaryReader.ReadString().Replace('\\', '/');
					CelesteAssets.SubTex value = new CelesteAssets.SubTex
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
					switch (text)
					{
					default:
						if (Array.IndexOf(Extra, text) < 0)
						{
							continue;
						}
						break;
					case "collectables/skullBlue":
					case "x":
					case "renogare64_0":
					case "postcard":
						break;
					}
					dictionary[text] = value;
				}
			}
		}
		string[] array = new string[4] { "collectables/skullBlue", "x", "renogare64_0", "postcard" };
		foreach (string text2 in array)
		{
			if (!dictionary.ContainsKey(text2))
			{
				throw new Exception("Gui atlas is missing " + text2);
			}
		}
		Dictionary<int, CelesteAssets.Page> decoded = new Dictionary<int, CelesteAssets.Page>();
		Func<CelesteAssets.SubTex, CelesteAssets.Page> func = delegate(CelesteAssets.SubTex t)
		{
			if (!decoded.TryGetValue(t.Page, out var value5))
			{
				value5 = (decoded[t.Page] = CelesteAssets.DecodeData(Path.Combine(atlasDir, pageNames[t.Page] + ".data")));
			}
			return value5;
		};
		guiArt.Skull = Cut(func(dictionary["collectables/skullBlue"]), dictionary["collectables/skullBlue"], 0, 0, dictionary["collectables/skullBlue"].FullW, dictionary["collectables/skullBlue"].FullH);
		guiArt.X = Cut(func(dictionary["x"]), dictionary["x"], 0, 0, dictionary["x"].FullW, dictionary["x"].FullH);
		guiArt.Postcard = Cut(func(dictionary["postcard"]), dictionary["postcard"], 0, 0, dictionary["postcard"].FullW, dictionary["postcard"].FullH);
		array = Extra;
		foreach (string text3 in array)
		{
			if (dictionary.TryGetValue(text3, out var value2))
			{
				guiArt.Images[text3] = Cut(func(value2), value2, 0, 0, value2.FullW, value2.FullH);
			}
			else
			{
				Plugin.Log.LogWarning("Gui atlas has no " + text3);
			}
		}
		try
		{
			CelesteAssets.Page page = CelesteAssets.DecodeData(Path.Combine(atlasDir, "Portraits/textbox/madeline.data"));
			Img img = new Img
			{
				W = page.W,
				H = page.H,
				Rgba = page.Rgba
			};
			guiArt.Images["textbox/default"] = img;
			int num3 = (img.H / 2 * img.W + img.W / 2) * 4;
			guiArt.TextboxDark = (float)(int)img.Rgba[num3] * 0.3f + (float)(int)img.Rgba[num3 + 1] * 0.59f + (float)(int)img.Rgba[num3 + 2] * 0.11f < 128f;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Celeste text box: " + ex.Message);
		}
		try
		{
			List<string> list = new List<string>();
			Dictionary<string, CelesteAssets.SubTex> dictionary2 = new Dictionary<string, CelesteAssets.SubTex>();
			string[] array2 = new string[12]
			{
				"card", "ticket", "portraitOverlay", "goldberry", "flag", "dot", "cardShine000", "cardShine001", "cardShine002", "cardShine003",
				"cardShine004", "cardShine005"
			};
			using (BinaryReader binaryReader2 = new BinaryReader(File.OpenRead(Path.Combine(atlasDir, "FileSelect.meta"))))
			{
				binaryReader2.ReadInt32();
				binaryReader2.ReadString();
				binaryReader2.ReadInt32();
				int num4 = binaryReader2.ReadInt16();
				for (int num5 = 0; num5 < num4; num5++)
				{
					list.Add(binaryReader2.ReadString());
					int num6 = binaryReader2.ReadInt16();
					for (int num7 = 0; num7 < num6; num7++)
					{
						string text4 = binaryReader2.ReadString().Replace('\\', '/');
						CelesteAssets.SubTex value3 = new CelesteAssets.SubTex
						{
							Page = num5,
							X = binaryReader2.ReadInt16(),
							Y = binaryReader2.ReadInt16(),
							W = binaryReader2.ReadInt16(),
							H = binaryReader2.ReadInt16()
						};
						value3.OffX = -binaryReader2.ReadInt16();
						value3.OffY = -binaryReader2.ReadInt16();
						value3.FullW = binaryReader2.ReadInt16();
						value3.FullH = binaryReader2.ReadInt16();
						if (Array.IndexOf(array2, text4) >= 0)
						{
							dictionary2[text4] = value3;
						}
					}
				}
			}
			Dictionary<int, CelesteAssets.Page> dictionary3 = new Dictionary<int, CelesteAssets.Page>();
			foreach (KeyValuePair<string, CelesteAssets.SubTex> item in dictionary2)
			{
				if (!dictionary3.TryGetValue(item.Value.Page, out var value4))
				{
					value4 = (dictionary3[item.Value.Page] = CelesteAssets.DecodeData(Path.Combine(atlasDir, list[item.Value.Page] + ".data")));
				}
				guiArt.Images["fileselect/" + item.Key] = Cut(value4, item.Value, 0, 0, item.Value.FullW, item.Value.FullH);
			}
			CelesteAssets.Page page3 = CelesteAssets.DecodeData(Path.Combine(atlasDir, "Portraits/madeline/normal00.data"));
			guiArt.Images["portrait/madeline"] = new Img
			{
				W = page3.W,
				H = page3.H,
				Rgba = page3.Rgba
			};
		}
		catch (Exception ex2)
		{
			Plugin.Log.LogWarning("Celeste file select art: " + ex2.Message);
		}
		try
		{
			// Celeste's journal (its paper page and icons), for the records screen.
			string[] wanted = new string[12] { "page", "edge", "time", "skullBlue", "skullGold", "clear", "fullclear", "strawberry", "heartgem0", "dot", "pageArrow", "beat" };
			List<string> jPages = new List<string>();
			Dictionary<string, CelesteAssets.SubTex> jTex = new Dictionary<string, CelesteAssets.SubTex>();
			using (BinaryReader br = new BinaryReader(File.OpenRead(Path.Combine(atlasDir, "Journal.meta"))))
			{
				br.ReadInt32();
				br.ReadString();
				br.ReadInt32();
				int pages = br.ReadInt16();
				for (int p = 0; p < pages; p++)
				{
					jPages.Add(br.ReadString());
					int count = br.ReadInt16();
					for (int q = 0; q < count; q++)
					{
						string name = br.ReadString().Replace('\\', '/');
						CelesteAssets.SubTex st = new CelesteAssets.SubTex
						{
							Page = p,
							X = br.ReadInt16(),
							Y = br.ReadInt16(),
							W = br.ReadInt16(),
							H = br.ReadInt16()
						};
						st.OffX = -br.ReadInt16();
						st.OffY = -br.ReadInt16();
						st.FullW = br.ReadInt16();
						st.FullH = br.ReadInt16();
						if (Array.IndexOf(wanted, name) >= 0)
						{
							jTex[name] = st;
						}
					}
				}
			}
			Dictionary<int, CelesteAssets.Page> jDecoded = new Dictionary<int, CelesteAssets.Page>();
			foreach (KeyValuePair<string, CelesteAssets.SubTex> item in jTex)
			{
				if (!jDecoded.TryGetValue(item.Value.Page, out var pg))
				{
					pg = (jDecoded[item.Value.Page] = CelesteAssets.DecodeData(Path.Combine(atlasDir, jPages[item.Value.Page] + ".data")));
				}
				guiArt.Images["journal/" + item.Key] = Cut(pg, item.Value, 0, 0, item.Value.FullW, item.Value.FullH);
			}
		}
		catch (Exception ex3)
		{
			Plugin.Log.LogWarning("Celeste journal art: " + ex3.Message);
		}
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.Load(Path.Combine(celesteDir, "Content/Dialog/Fonts/renogare64.fnt"));
		guiArt.LineHeight = int.Parse(xmlDocument.SelectSingleNode("/font/common").Attributes["lineHeight"].Value, CultureInfo.InvariantCulture);
		CelesteAssets.SubTex subTex = dictionary["renogare64_0"];
		foreach (XmlNode item2 in xmlDocument.SelectNodes("/font/chars/char"))
		{
			int num8 = Attr(item2, "id");
			if (num8 >= 32 && (num8 <= 126 || num8 >= 160) && num8 <= 383)
			{
				Img img2 = Cut(func(subTex), subTex, Attr(item2, "x"), Attr(item2, "y"), Attr(item2, "width"), Attr(item2, "height"));
				img2.XOffset = Attr(item2, "xoffset");
				img2.YOffset = Attr(item2, "yoffset");
				img2.XAdvance = Attr(item2, "xadvance");
				guiArt.Glyphs[(char)num8] = img2;
				if (num8 >= 48 && num8 <= 57)
				{
					guiArt.Digits[num8 - 48] = img2;
				}
			}
		}
		Img[] digits = guiArt.Digits;
		for (int k = 0; k < digits.Length; k++)
		{
			if (digits[k] == null)
			{
				throw new Exception("renogare64.fnt is missing a digit");
			}
		}
		return guiArt;
	}

	internal float Measure(string line)
	{
		float num = 0f;
		foreach (char key in line)
		{
			if (Glyphs.TryGetValue(key, out var value))
			{
				num += (float)value.XAdvance;
			}
		}
		return num;
	}

	internal void DrawLine(string line, float x, float top, float scale, Color c)
	{
		GUI.color = c;
		foreach (char key in line)
		{
			if (Glyphs.TryGetValue(key, out var value))
			{
				if (value.Tex != null)
				{
					GUI.DrawTexture(new Rect(x + (float)value.XOffset * scale, top + (float)value.YOffset * scale, (float)value.W * scale, (float)value.H * scale), value.Tex);
				}
				x += (float)value.XAdvance * scale;
			}
		}
	}

	internal void DrawOutlined(string line, float x, float top, float scale, Color c, Color outline, float stroke)
	{
		if (outline.a > 0f)
		{
			DrawLine(line, x - stroke, top, scale, outline);
			DrawLine(line, x + stroke, top, scale, outline);
			DrawLine(line, x, top - stroke, scale, outline);
			DrawLine(line, x, top + stroke, scale, outline);
		}
		DrawLine(line, x, top, scale, c);
	}

	internal static void Draw(Img img, Rect r, Color c)
	{
		if (img != null && !(img.Tex == null))
		{
			GUI.color = c;
			GUI.DrawTexture(r, img.Tex);
		}
	}

	internal List<string> Wrap(string text, float maxWidth)
	{
		List<string> list = new List<string>();
		string[] array = text.Split(new char[1] { '\n' });
		foreach (string obj in array)
		{
			string text2 = "";
			string[] array2 = obj.Split(new char[1] { ' ' });
			foreach (string text3 in array2)
			{
				string text4 = ((text2.Length == 0) ? text3 : (text2 + " " + text3));
				if (text2.Length > 0 && Measure(text4) > maxWidth)
				{
					list.Add(text2);
					text2 = text3;
				}
				else
				{
					text2 = text4;
				}
			}
			list.Add(text2);
		}
		return list;
	}

	private static int Attr(XmlNode n, string name)
	{
		return int.Parse(n.Attributes[name].Value, CultureInfo.InvariantCulture);
	}

	private static Img Cut(CelesteAssets.Page page, CelesteAssets.SubTex t, int fx, int fy, int w, int h)
	{
		Img img = new Img
		{
			W = w,
			H = h,
			Rgba = new byte[w * h * 4]
		};
		for (int i = 0; i < h; i++)
		{
			for (int j = 0; j < w; j++)
			{
				int num = fx + j - t.OffX;
				int num2 = fy + i - t.OffY;
				if (num >= 0 && num2 >= 0 && num < t.W && num2 < t.H)
				{
					Array.Copy(page.Rgba, ((t.Y + num2) * page.W + (t.X + num)) * 4, img.Rgba, (i * w + j) * 4, 4);
				}
			}
		}
		return img;
	}

	public void Build()
	{
		List<Img> list = new List<Img>(Glyphs.Values);
		list.Add(Skull);
		list.Add(X);
		list.Add(Postcard);
		list.AddRange(Images.Values);
		foreach (Img item in list)
		{
			if (item.W <= 0 || item.H <= 0)
			{
				item.Rgba = null;
				continue;
			}
			Texture2D texture2D = new Texture2D(item.W, item.H, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp
			};
			Color32[] array = new Color32[item.W * item.H];
			for (int i = 0; i < item.H; i++)
			{
				for (int j = 0; j < item.W; j++)
				{
					int num = (i * item.W + j) * 4;
					array[(item.H - 1 - i) * item.W + j] = new Color32(item.Rgba[num], item.Rgba[num + 1], item.Rgba[num + 2], item.Rgba[num + 3]);
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			item.Tex = texture2D;
			if (!Glyphs.ContainsValue(item))
			{
				item.Rgba = null;
			}
		}
	}
}
