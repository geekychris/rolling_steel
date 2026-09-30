using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RollingSteel
{
    [System.Serializable]
    public class CourseRecord
    {
        public string name;
        public float bestTime;      // 0 means never finished
        public int bestFalls = -1;
    }

    [System.Serializable]
    public class ProgressData
    {
        public List<CourseRecord> courses = new List<CourseRecord>();
        public float bestRunTime;
        public int bestRunFalls = -1;
    }

    /// Best times, kept on disk between sessions.
    ///
    /// Records are keyed by course *name* rather than index, so reordering the
    /// courses or dropping a new one into the folder does not scramble anyone's
    /// times.
    public static class Progress
    {
        public const int None = 0, Bronze = 1, Silver = 2, Gold = 3;

        static ProgressData data = new ProgressData();
        static string path;

        public static void Load()
        {
            path = Path.Combine(Application.persistentDataPath, "progress.json");
            try
            {
                if (File.Exists(path))
                    data = JsonUtility.FromJson<ProgressData>(File.ReadAllText(path)) ?? new ProgressData();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[progress] could not read {path}: {e.Message}");
                data = new ProgressData();
            }
        }

        static void Save()
        {
            if (path == null) return;
            try { File.WriteAllText(path, JsonUtility.ToJson(data, true)); }
            catch (IOException e) { Debug.LogWarning($"[progress] could not write {path}: {e.Message}"); }
        }

        static CourseRecord For(string name, bool create)
        {
            foreach (var c in data.courses) if (c.name == name) return c;
            if (!create) return null;
            var rec = new CourseRecord { name = name };
            data.courses.Add(rec);
            return rec;
        }

        /// Best time for a course, or 0 if it has never been finished.
        public static float BestTime(string name) => For(name, false)?.bestTime ?? 0f;

        public static float BestRunTime => data.bestRunTime;
        public static int BestRunFalls => data.bestRunFalls;

        /// Record a finish. Returns true if it beat the previous best.
        public static bool SubmitCourse(string name, float time, int falls)
        {
            var rec = For(name, true);
            bool better = rec.bestTime <= 0f || time < rec.bestTime;
            if (better)
            {
                rec.bestTime = time;
                rec.bestFalls = falls;
                Save();
            }
            return better;
        }

        public static bool SubmitRun(float time, int falls)
        {
            bool better = data.bestRunTime <= 0f || time < data.bestRunTime;
            if (better)
            {
                data.bestRunTime = time;
                data.bestRunFalls = falls;
                Save();
            }
            return better;
        }

        public static int MedalFor(Level lvl, float time)
        {
            if (time <= 0f) return None;
            if (lvl.Gold > 0f && time <= lvl.Gold) return Gold;
            if (lvl.Silver > 0f && time <= lvl.Silver) return Silver;
            if (lvl.Bronze > 0f && time <= lvl.Bronze) return Bronze;
            return None;
        }

        public static string MedalName(int medal) =>
            medal == Gold ? "GOLD" : medal == Silver ? "SILVER" : medal == Bronze ? "BRONZE" : "";

        public static Color MedalColor(int medal) =>
            medal == Gold ? new Color(1f, 0.82f, 0.25f)
          : medal == Silver ? new Color(0.82f, 0.86f, 0.92f)
          : medal == Bronze ? new Color(0.85f, 0.55f, 0.30f)
          : new Color(1f, 1f, 1f, 0.35f);

        /// mm:ss.s, or a dash when there is no time yet.
        public static string Format(float t)
        {
            if (t <= 0f) return "--:--";
            int m = (int)(t / 60f);
            return m > 0 ? $"{m}:{t - m * 60f:00.0}" : $"{t:0.0}";
        }
    }
}
