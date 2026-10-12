using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RollingSteel
{
    /// Where course files live, and how they get there.
    ///
    /// The built-in courses are seeded to disk on first run, so there is always
    /// something to edit and the editor always has somewhere to save. A course
    /// that fails to parse falls back to its built-in rather than taking the
    /// game down with it.
    public static class CourseStore
    {
        public static string Dir { get; private set; }

        public static void Init(string overrideDir)
        {
            Dir = string.IsNullOrEmpty(overrideDir)
                ? Path.Combine(Application.persistentDataPath, "Courses")
                : overrideDir;

            try
            {
                Directory.CreateDirectory(Dir);
                SeedMissing();
                RenameOld();
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[courses] cannot use {Dir}: {e.Message}");
                Dir = null;
            }
        }

        static string[] Files()
        {
            if (string.IsNullOrEmpty(Dir) || !Directory.Exists(Dir)) return new string[0];
            var files = Directory.GetFiles(Dir, "*.course");
            System.Array.Sort(files, System.StringComparer.OrdinalIgnoreCase);
            return files;
        }

        /// Write out any built-in course that has no file yet. Gap-filling rather
        /// than all-or-nothing, so a build that adds courses adds their files to an
        /// existing installation without touching what is already there.
        static void SeedMissing()
        {
            int written = 0;
            for (int i = 0; i < CourseLibrary.Sources.Length; i++)
            {
                string path = Path.Combine(Dir, $"{i + 1}.course");
                if (File.Exists(path)) continue;
                File.WriteAllText(path, CourseLibrary.Sources[i].TrimStart('\n', '\r'));
                written++;
            }
            if (written > 0) Debug.Log($"[courses] seeded {written} course file(s) into {Dir}");
        }

        /// Built-in courses that have been renamed: old name, new name, and the
        /// scenery seed the old name hashed to. Saves keyed by name (best times,
        /// ghosts, seeded course files) are carried over on first run.
        public static readonly (string from, string to, int seed)[] Renamed =
        {
            ("PRACTICE", "WARM-UP", 1100716507), ("BEGINNER", "FIRST DROP", 1770843060),
            ("INTERMEDIATE", "SWITCHBACK", 1279180289), ("AERIAL", "SKYWAY", -835804624),
            ("SILLY", "TOPSY-TURVY", -212541437), ("ULTIMATE", "THE GAUNTLET", -1679118255),
        };

        /// Course files seeded by an older build carry the old names. Swap in the
        /// new name, plus a `seed` line so the scenery stays as it was; nothing
        /// else in the file is touched.
        static void RenameOld()
        {
            foreach (var file in Files())
            {
                try
                {
                    var lines = new List<string>(File.ReadAllLines(file));
                    for (int i = 0; i < lines.Count; i++)
                    {
                        string t = lines[i].Trim();
                        if (!t.StartsWith("name ")) continue;
                        foreach (var r in Renamed)
                            if (t == "name " + r.from)
                            {
                                lines[i] = "name " + r.to;
                                if (!lines.Exists(l => l.Trim().StartsWith("seed ")))
                                    lines.Insert(i + 1, "seed " + r.seed);
                                File.WriteAllLines(file, lines);
                                Debug.Log($"[courses] renamed {r.from} -> {r.to} in {file}");
                                break;
                            }
                        break;
                    }
                }
                catch (IOException e)
                {
                    Debug.LogWarning($"[courses] cannot update {file}: {e.Message}");
                }
            }
        }

        public static string PathFor(int index)
        {
            var files = Files();
            if (index >= 0 && index < files.Length) return files[index];
            return string.IsNullOrEmpty(Dir) ? null : Path.Combine(Dir, $"{index + 1}.course");
        }

        public static string SourceFor(int index)
        {
            string path = PathFor(index);
            try
            {
                if (path != null && File.Exists(path)) return File.ReadAllText(path);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[courses] cannot read {path}: {e.Message}");
            }
            return index < CourseLibrary.Sources.Length ? CourseLibrary.Sources[index] : null;
        }

        public static bool Save(int index, string source)
        {
            string path = PathFor(index);
            if (path == null) return false;
            try
            {
                File.WriteAllText(path, source);
                return true;
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[courses] cannot write {path}: {e.Message}");
                return false;
            }
        }

        /// Every course, from disk where possible, built-in where not.
        public static List<Level> LoadLevels()
        {
            var levels = new List<Level>();
            int count = Mathf.Max(Files().Length, CourseLibrary.Sources.Length);

            for (int i = 0; i < count; i++)
            {
                string src = SourceFor(i);
                if (string.IsNullOrEmpty(src)) continue;
                try
                {
                    levels.Add(CourseScript.Build(src));
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[courses] course {i + 1} failed to build ({e.Message}); using built-in");
                    if (i < CourseLibrary.Sources.Length)
                        levels.Add(CourseScript.Build(CourseLibrary.Sources[i]));
                }
            }

            if (levels.Count == 0) levels = CourseLibrary.All();
            return levels;
        }
    }
}
