using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MadelineInPharloom;
using UnityEngine;

namespace SilkesteRemix;

internal static class TheoArt
{
	public static readonly int[] SitFrames = new int[1] { 182 };

	public static readonly int[] DozeFrames = new int[6] { 183, 184, 185, 186, 187, 188 };

	private static readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();

	private static Dictionary<int, (CelesteAssets.Page page, CelesteAssets.SubTex tex)> pending;

	private static int state;

	public static Sprite Frame(int n)
	{
		if (!sprites.TryGetValue(n, out var value))
		{
			return null;
		}
		return value;
	}

	public static bool Ready()
	{
		if (state == 2)
		{
			return true;
		}
		if (state == 0)
		{
			Start();
		}
		if (state == 1 && pending != null)
		{
			foreach (KeyValuePair<int, (CelesteAssets.Page, CelesteAssets.SubTex)> item2 in pending)
			{
				CelesteAssets.SubTex item = item2.Value.Item2;
				sprites[item2.Key] = CelesteAssets.MakeSprite(item2.Value.Item1, item, new Vector2((float)item.FullW / 2f, item.FullH), (Dictionary<int, Color32>)null);
			}
			pending = null;
			state = 2;
		}
		return state == 2;
	}

	private static void Start()
	{
		state = 1;
		string celeste;
		try
		{
			celeste = Plugin.FindCeleste(Path.Combine(AssistMode.DataDir ?? "", "settings.cfg"));
		}
		catch (Exception)
		{
			celeste = null;
		}
		if (string.IsNullOrEmpty(celeste))
		{
			state = 3;
			return;
		}
		Thread thread = new Thread((ThreadStart)delegate
		{
			try
			{
				pending = Load(celeste);
			}
			catch (Exception ex2)
			{
				state = 3;
				Plugin.Log.LogWarning("Theo art: " + ex2.Message);
			}
		});
		thread.IsBackground = true;
		thread.Start();
	}

	private static Dictionary<int, (CelesteAssets.Page, CelesteAssets.SubTex)> Load(string celeste)
	{
		HashSet<int> hashSet = new HashSet<int>(SitFrames);
		hashSet.UnionWith(DozeFrames);
		string path = Path.Combine(celeste, "Content/Graphics/Atlases");
		Dictionary<int, (int, CelesteAssets.SubTex)> dictionary = new Dictionary<int, (int, CelesteAssets.SubTex)>();
		List<string> list = new List<string>();
		using (BinaryReader binaryReader = new BinaryReader(File.OpenRead(Path.Combine(path, "Gameplay.meta"))))
		{
			binaryReader.ReadInt32();
			binaryReader.ReadString();
			binaryReader.ReadInt32();
			int num = binaryReader.ReadInt16();
			for (int i = 0; i < num; i++)
			{
				list.Add(binaryReader.ReadString());
				int num2 = binaryReader.ReadInt16();
				for (int j = 0; j < num2; j++)
				{
					string text = binaryReader.ReadString().Replace('\\', '/');
					CelesteAssets.SubTex item = new CelesteAssets.SubTex
					{
						Page = i,
						X = binaryReader.ReadInt16(),
						Y = binaryReader.ReadInt16(),
						W = binaryReader.ReadInt16(),
						H = binaryReader.ReadInt16()
					};
					item.OffX = -binaryReader.ReadInt16();
					item.OffY = -binaryReader.ReadInt16();
					item.FullW = binaryReader.ReadInt16();
					item.FullH = binaryReader.ReadInt16();
					if (text.StartsWith("characters/theo/theo", StringComparison.Ordinal) && int.TryParse(text.Substring("characters/theo/theo".Length), out var result) && hashSet.Contains(result))
					{
						dictionary[result] = (i, item);
					}
				}
			}
		}
		Dictionary<int, CelesteAssets.Page> dictionary2 = new Dictionary<int, CelesteAssets.Page>();
		Dictionary<int, (CelesteAssets.Page, CelesteAssets.SubTex)> dictionary3 = new Dictionary<int, (CelesteAssets.Page, CelesteAssets.SubTex)>();
		foreach (KeyValuePair<int, (int, CelesteAssets.SubTex)> item2 in dictionary)
		{
			if (!dictionary2.TryGetValue(item2.Value.Item1, out var value))
			{
				value = (dictionary2[item2.Value.Item1] = CelesteAssets.DecodeData(Path.Combine(path, list[item2.Value.Item1] + ".data")));
			}
			dictionary3[item2.Key] = (value, item2.Value.Item2);
		}
		return dictionary3;
	}
}
