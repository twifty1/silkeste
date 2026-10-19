using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Records' photo album (user 2026-10-16): Theo's camera pictures from Pictures/Silkeste, newest first. Only the
    /// picture on screen and its neighbours are loaded, so a big folder costs nothing.
    /// </summary>
    public static class Album
    {
        public static readonly List<string> Photos = new List<string>();
        static readonly Dictionary<string, Texture2D> loaded = new Dictionary<string, Texture2D>();
        static float listedAt = -10f;
        // ImageConversion.LoadImage(Texture2D, byte[], bool), found by reflection: Unity 6 also has a Span overload
        // this compiler can't see, which makes the plain call fail to build
        static readonly System.Reflection.MethodInfo LoadImage = typeof(ImageConversion).GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });

        static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Silkeste"); } }

        /// <summary>Lists the folder again, at most every two seconds.</summary>
        public static void Refresh()
        {
            if (Time.unscaledTime - listedAt < 2f) return;
            listedAt = Time.unscaledTime;
            Photos.Clear();
            try
            {
                if (!Directory.Exists(Dir)) return;
                var files = new List<FileInfo>();
                foreach (string f in Directory.GetFiles(Dir, "*.png")) files.Add(new FileInfo(f));
                files.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                foreach (FileInfo f in files) Photos.Add(f.FullName);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Photo album: " + e.Message); }
        }

        /// <summary>The picture at i, loaded on first look; pictures far from i are let go.</summary>
        public static Texture2D Get(int i)
        {
            if (i < 0 || i >= Photos.Count) return null;
            var keep = new HashSet<string>();
            for (int d = -3; d <= 3; d++) if (i + d >= 0 && i + d < Photos.Count) keep.Add(Photos[i + d]);
            foreach (string k in new List<string>(loaded.Keys))
                if (!keep.Contains(k)) { if (loaded[k] != null) UnityEngine.Object.Destroy(loaded[k]); loaded.Remove(k); }
            string path = Photos[i];
            Texture2D tex;
            if (loaded.TryGetValue(path, out tex)) return tex;
            try
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                byte[] data = File.ReadAllBytes(path);
                if (!(bool)LoadImage.Invoke(null, new object[] { tex, data, true })) { UnityEngine.Object.Destroy(tex); tex = null; }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Photo album: " + e.Message); tex = null; }
            loaded[path] = tex;
            return tex;
        }

        /// <summary>The picture's day and time, from its file.</summary>
        /// <summary>What a photo shows, from its file name: (boss, area); either may be null.</summary>
        public static void Tag(int i, out string boss, out string area)
        {
            boss = area = null;
            try
            {
                string[] p = Path.GetFileNameWithoutExtension(Photos[i]).Split('_');
                if (p.Length >= 4)
                {
                    string t = p[1];
                    if (t.StartsWith("A-", StringComparison.Ordinal)) area = Spaced(t.Substring(2));
                    else boss = Spaced(t);
                }
            }
            catch (Exception) { }
        }
        static string Spaced(string s)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1])) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }
        public static string Name(int i)
        {
            try { return File.GetLastWriteTime(Photos[i]).ToString("d MMM yyyy, HH:mm"); }
            catch (Exception) { return "Photo"; }
        }
    }
}
