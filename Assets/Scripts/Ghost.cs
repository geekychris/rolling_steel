using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RollingSteel
{
    [System.Serializable]
    public class GhostData
    {
        public float hz = Ghost.Hz;
        public List<Vector3> p = new List<Vector3>();   // course space
    }

    /// The marble's path on your best run, recorded and played back beside you.
    ///
    /// Samples are stored in course space rather than world space, so a ghost
    /// stays valid regardless of how the course root is oriented.
    public static class Ghost
    {
        public const float Hz = 20f;

        static string PathFor(string course)
        {
            var safe = new StringBuilder();
            foreach (char c in course)
                safe.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_');
            return Path.Combine(Application.persistentDataPath, $"ghost-{safe}.json");
        }

        public static GhostData Load(string course)
        {
            try
            {
                string p = PathFor(course);
                if (!File.Exists(p)) return null;
                var d = JsonUtility.FromJson<GhostData>(File.ReadAllText(p));
                return d != null && d.p.Count > 1 ? d : null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ghost] could not read {course}: {e.Message}");
                return null;
            }
        }

        public static void Save(string course, List<Vector3> samples)
        {
            if (samples == null || samples.Count < 2) return;
            var d = new GhostData { hz = Hz };
            d.p.AddRange(samples);
            try { File.WriteAllText(PathFor(course), JsonUtility.ToJson(d)); }
            catch (IOException e) { Debug.LogWarning($"[ghost] could not write {course}: {e.Message}"); }
        }

        /// Where the ghost was at time t, in course space.
        public static bool Sample(GhostData d, float t, out Vector3 pos)
        {
            pos = Vector3.zero;
            if (d == null || d.p.Count < 2) return false;

            float f = t * d.hz;
            if (f >= d.p.Count - 1)
            {
                pos = d.p[d.p.Count - 1];
                return false;            // finished; stop drawing it
            }

            int i = Mathf.Max(0, Mathf.FloorToInt(f));
            pos = Vector3.Lerp(d.p[i], d.p[i + 1], f - i);
            return true;
        }
    }
}
