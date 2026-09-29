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
                if (Files().Length == 0) Seed();
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

        static void Seed()
        {
            for (int i = 0; i < CourseLibrary.Sources.Length; i++)
                File.WriteAllText(Path.Combine(Dir, $"{i + 1}.course"),
                                  CourseLibrary.Sources[i].TrimStart('\n', '\r'));
            Debug.Log($"[courses] seeded {CourseLibrary.Sources.Length} courses into {Dir}");
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
