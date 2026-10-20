using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Reads the Tech Tutorial's rooms from the player's own Celeste (user 2026-10-20: "copy the tutorial in chapter 9"):
/// Farewell's map (Maps/LostLevels.bin, Celeste's BinaryPacker format), its tile rules (ForegroundTiles.xml /
/// BackgroundTiles.xml) and the ghost runs Celeste plays in it (Tutorials/*.bin). Nothing of Celeste ships with Silkeste.
/// </summary>
public static class TechRoomData
{
	public class Element
	{
		public string Name;

		public readonly Dictionary<string, object> Attr = new Dictionary<string, object>();

		public readonly List<Element> Children = new List<Element>();

		public float F(string k, float fallback = 0f)
		{
			if (!Attr.TryGetValue(k, out object v))
			{
				return fallback;
			}
			switch (v)
			{
			case int i:
				return i;
			case short s:
				return s;
			case byte b:
				return b;
			case float f:
				return f;
			default:
				return fallback;
			}
		}

		public string S(string k, string fallback = "")
		{
			return Attr.TryGetValue(k, out object v) && v != null ? v.ToString() : fallback;
		}

		public Element Child(string name) => Children.Find((Element c) => c.Name == name);
	}

	public static Element ReadMap(string path)
	{
		using (BinaryReader r = new BinaryReader(File.OpenRead(path)))
		{
			r.ReadString();   // "CELESTE MAP"
			r.ReadString();   // package
			int n = r.ReadInt16();
			string[] lookup = new string[n];
			for (int i = 0; i < n; i++)
			{
				lookup[i] = r.ReadString();
			}
			return ReadElement(r, lookup);
		}
	}

	private static Element ReadElement(BinaryReader r, string[] lookup)
	{
		Element e = new Element { Name = lookup[r.ReadInt16()] };
		int attrs = r.ReadByte();
		for (int i = 0; i < attrs; i++)
		{
			string key = lookup[r.ReadInt16()];
			object v;
			switch (r.ReadByte())
			{
			case 0:
				v = r.ReadBoolean();
				break;
			case 1:
				v = (int)r.ReadByte();
				break;
			case 2:
				v = (int)r.ReadInt16();
				break;
			case 3:
				v = r.ReadInt32();
				break;
			case 4:
				v = r.ReadSingle();
				break;
			case 5:
				v = lookup[r.ReadInt16()];
				break;
			case 6:
				v = r.ReadString();
				break;
			case 7:
			{
				// run-length text: (count, char) pairs
				int len = r.ReadInt16();
				byte[] b = r.ReadBytes(len);
				StringBuilder sb = new StringBuilder();
				for (int j = 0; j + 1 < b.Length; j += 2)
				{
					sb.Append((char)b[j + 1], b[j]);
				}
				v = sb.ToString();
				break;
			}
			default:
				v = null;
				break;
			}
			e.Attr[key] = v;
		}
		int children = r.ReadInt16();
		for (int i = 0; i < children; i++)
		{
			e.Children.Add(ReadElement(r, lookup));
		}
		return e;
	}

	public static Element Level(Element map, string name)
	{
		Element levels = map.Child("levels");
		return levels?.Children.Find((Element l) => l.S("name") == name);
	}

	// ---- tiles -----------------------------------------------------------------------------------------------------

	public class Tileset
	{
		public char Id;

		public string Path;

		public string Ignores = "";

		public readonly List<KeyValuePair<string, List<Vector2Int>>> Rules = new List<KeyValuePair<string, List<Vector2Int>>>();

		public List<Vector2Int> Padding = new List<Vector2Int>();

		public List<Vector2Int> Center = new List<Vector2Int>();
	}

	public static Dictionary<char, Tileset> ReadTilesets(string xmlPath)
	{
		Dictionary<char, Tileset> sets = new Dictionary<char, Tileset>();
		XmlDocument doc = new XmlDocument();
		doc.Load(xmlPath);
		foreach (XmlNode node in doc.SelectNodes("/Data/Tileset"))
		{
			string id = node.Attributes["id"]?.Value;
			if (string.IsNullOrEmpty(id))
			{
				continue;
			}
			Tileset t = new Tileset { Id = id[0], Path = node.Attributes["path"]?.Value ?? "", Ignores = node.Attributes["ignores"]?.Value ?? "" };
			string copy = node.Attributes["copy"]?.Value;
			if (copy != null && sets.TryGetValue(copy[0], out Tileset from))
			{
				t.Rules.AddRange(from.Rules);
				t.Padding = from.Padding;
				t.Center = from.Center;
			}
			foreach (XmlNode set in node.SelectNodes("set"))
			{
				string mask = set.Attributes["mask"]?.Value ?? "";
				List<Vector2Int> tiles = new List<Vector2Int>();
				foreach (string pair in (set.Attributes["tiles"]?.Value ?? "").Split(';'))
				{
					string[] xy = pair.Split(',');
					if (xy.Length == 2 && int.TryParse(xy[0], out int x) && int.TryParse(xy[1], out int y))
					{
						tiles.Add(new Vector2Int(x, y));
					}
				}
				if (mask == "padding")
				{
					t.Padding = tiles;
				}
				else if (mask == "center")
				{
					t.Center = tiles;
				}
				else
				{
					t.Rules.Add(new KeyValuePair<string, List<Vector2Int>>(mask.Replace("-", ""), tiles));
				}
			}
			sets[t.Id] = t;
		}
		return sets;
	}

	public static char[,] Grid(string text, int w, int h)
	{
		char[,] g = new char[w, h];
		string[] rows = (text ?? "").Replace("\r", "").Split('\n');
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				g[x, y] = y < rows.Length && x < rows[y].Length ? rows[y][x] : '0';
			}
		}
		return g;
	}

	/// <summary>Celeste's autotiler: each tile picks its picture from which of its 8 neighbours are solid.</summary>
	public static Vector2Int? Pick(char[,] g, int x, int y, Tileset t)
	{
		int w = g.GetLength(0);
		int h = g.GetLength(1);
		bool Solid(int ix, int iy)
		{
			if (ix < 0 || iy < 0 || ix >= w || iy >= h)
			{
				return true;   // past the room's edge counts as solid
			}
			char c = g[ix, iy];
			if (c == '0' || c == ' ')
			{
				return false;
			}
			if (t.Ignores == "*" ? c != t.Id : t.Ignores.IndexOf(c) >= 0)
			{
				return false;
			}
			return true;
		}
		bool[] n = new bool[9];
		for (int j = 0; j < 3; j++)
		{
			for (int i = 0; i < 3; i++)
			{
				n[j * 3 + i] = Solid(x + i - 1, y + j - 1);
			}
		}
		int hash = (x * 73856093) ^ (y * 19349663);
		List<Vector2Int> Choose(List<Vector2Int> l) => l;
		foreach (KeyValuePair<string, List<Vector2Int>> rule in t.Rules)
		{
			string m = rule.Key;
			if (m.Length != 9 || rule.Value.Count == 0)
			{
				continue;
			}
			bool ok = true;
			for (int k = 0; k < 9 && ok; k++)
			{
				ok = m[k] == 'x' || (m[k] == '1') == n[k];
			}
			if (ok)
			{
				List<Vector2Int> l = Choose(rule.Value);
				return l[Math.Abs(hash) % l.Count];
			}
		}
		// surrounded: padding near an edge, else the plain centre
		bool nearEdge = !Solid(x - 2, y) || !Solid(x + 2, y) || !Solid(x, y - 2) || !Solid(x, y + 2);
		List<Vector2Int> fill = nearEdge && t.Padding.Count > 0 ? t.Padding : t.Center;
		if (fill.Count == 0)
		{
			return null;
		}
		return fill[Math.Abs(hash) % fill.Count];
	}

	// ---- ghost runs ------------------------------------------------------------------------------------------------

	public struct PlaybackFrame
	{
		public Vector2 P;   // Celeste pixels from the start, y down

		public float T;

		public string Anim;

		public int Facing;

		public Color32 Hair;
	}

	public static List<PlaybackFrame> ReadPlayback(string path)
	{
		List<PlaybackFrame> list = new List<PlaybackFrame>();
		using (BinaryReader r = new BinaryReader(File.OpenRead(path)))
		{
			if (r.ReadString() != "TIMELINE")
			{
				return list;
			}
			int version = r.ReadInt32();
			int count = r.ReadInt32();
			for (int i = 0; i < count; i++)
			{
				PlaybackFrame f = new PlaybackFrame();
				f.P = new Vector2(r.ReadSingle(), r.ReadSingle());
				f.T = r.ReadSingle();
				f.Anim = r.ReadString();
				f.Facing = r.ReadInt32();
				r.ReadBoolean();
				f.Hair = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), byte.MaxValue);
				r.ReadInt32();
				if (version >= 2)
				{
					r.ReadSingle();
					r.ReadSingle();
					r.ReadSingle();
					r.ReadSingle();
				}
				list.Add(f);
			}
		}
		return list;
	}
}
