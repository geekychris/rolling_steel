using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace RollingSteel
{
    /// One line of a course file: a verb, some named numbers, some flags.
    public class Cmd
    {
        public string Verb = "";
        public readonly List<string> Keys = new List<string>();     // ordered, for the editor
        public readonly List<float> Values = new List<float>();
        public readonly List<string> Flags = new List<string>();
        public string Text = "";                                    // for `name`
        public string Comment = "";

        public float Get(string key, float fallback)
        {
            int i = Keys.IndexOf(key);
            return i < 0 ? fallback : Values[i];
        }

        public bool Has(string flag) => Flags.Contains(flag);

        public void Set(string key, float v)
        {
            int i = Keys.IndexOf(key);
            if (i < 0) { Keys.Add(key); Values.Add(v); }
            else Values[i] = v;
        }

        public void Toggle(string flag)
        {
            if (!Flags.Remove(flag)) Flags.Add(flag);
        }

        public Cmd Clone()
        {
            var c = new Cmd { Verb = Verb, Text = Text, Comment = Comment };
            c.Keys.AddRange(Keys);
            c.Values.AddRange(Values);
            c.Flags.AddRange(Flags);
            return c;
        }

        static readonly string[] HeaderVerbs = { "time", "width", "decor", "music" };
        public bool IsHeader => System.Array.IndexOf(HeaderVerbs, Verb) >= 0;

        public override string ToString()
        {
            var sb = new StringBuilder(Verb);
            if (Verb == "name") return "name " + Text;

            // headers read better bare: `time 75`, not `time v=75`
            if (IsHeader && Keys.Count == 1 && Keys[0] == "v")
                return Verb + " " + Values[0].ToString("0.###", CultureInfo.InvariantCulture);
            for (int i = 0; i < Keys.Count; i++)
                sb.Append(' ').Append(Keys[i]).Append('=')
                  .Append(Values[i].ToString("0.###", CultureInfo.InvariantCulture));
            foreach (var f in Flags) sb.Append(' ').Append(f);
            return sb.ToString();
        }
    }

    /// Courses as plain text. The verbs map one-to-one onto CourseBuilder, so a
    /// course is data rather than code - which is what makes the in-game editor
    /// possible, and what lets anyone drop a .course file next to the game.
    public static class CourseScript
    {
        /// Verbs the editor offers when inserting a line, in a sensible order.
        public static readonly string[] Palette =
        {
            "run", "hill", "curve", "chicane", "jog", "slope", "ice", "rough",
            "crumble", "pad", "split", "gap", "step", "jump", "rails",
            "acid", "pillar", "sweeper", "crusher", "fan", "boost", "chaser", "blob", "goal",
        };

        /// Default arguments for a freshly inserted line.
        public static Cmd NewCmd(string verb)
        {
            var c = new Cmd { Verb = verb };
            switch (verb)
            {
                case "run": case "ice": case "rough": c.Set("len", 10f); c.Set("w", 7f); break;
                case "crumble": c.Set("len", 8f); c.Set("w", 6f); break;
                case "hill": c.Set("len", 14f); c.Set("drop", 4f); c.Set("w", 7f); break;
                case "slope": c.Set("len", 14f); c.Set("drop", 4f); c.Set("w", 7f); break;
                case "curve": c.Set("r", 14f); c.Set("a", 60f); c.Set("drop", 1.5f); c.Set("w", 7f); c.Set("bank", 12f); break;
                case "chicane": c.Set("r", 14f); c.Set("a", 40f); c.Set("drop", 2f); c.Set("w", 7f); c.Set("bank", 12f); break;
                case "jog": c.Set("dx", 6f); c.Set("d", 12f); c.Set("w", 7f); break;
                case "pad": c.Set("d", 8f); c.Set("w", 9f); break;
                case "split": c.Set("len", 14f); c.Set("side", 3f); c.Set("pit", 4f); c.Set("apron", 5f); break;
                case "gap": c.Set("len", 3f); break;
                case "step": c.Set("dy", 2f); break;
                case "jump": c.Set("gap", 2.6f); c.Set("drop", 2.4f); c.Set("lip", 5f); break;
                case "rails": c.Set("l", 1f); c.Set("r", 1f); c.Set("h", 0.7f); break;
                case "acid": c.Set("w", 3f); c.Set("d", 3f); c.Set("x", 0f); c.Set("back", 3f); c.Set("lead", 4f); break;
                case "pillar": c.Set("x", 0f); c.Set("back", 3f); c.Set("r", 0.85f); c.Set("h", 2.6f); break;
                case "sweeper": c.Set("x", 0f); c.Set("back", 4f); c.Set("len", 5f); c.Set("speed", 70f); c.Set("h", 0.55f); break;
                case "crusher": c.Set("x", 0f); c.Set("back", 4f); c.Set("w", 3f); c.Set("period", 2.4f); c.Set("phase", 0f); c.Set("lift", 4.5f); break;
                case "fan": c.Set("x", 0f); c.Set("back", 3f); c.Set("w", 6f); c.Set("d", 6f); c.Set("push", 16f); break;
                case "boost": c.Set("x", 0f); c.Set("back", 3f); c.Set("w", 4f); c.Set("d", 5f); c.Set("push", 26f); break;
                case "chaser": case "blob": c.Set("x", 0f); c.Set("back", 4f); c.Set("range", 20f); c.Set("speed", 9f); break;
                case "goal": c.Set("d", 9f); break;
            }
            return c;
        }

        // ---- parsing -------------------------------------------------------

        public static List<Cmd> Parse(string source)
        {
            var list = new List<Cmd>();
            foreach (var raw in source.Split('\n'))
            {
                string line = raw.Replace("\r", "").Trim();
                if (line.Length == 0) continue;

                string comment = "";
                int hash = line.IndexOf('#');
                if (hash >= 0)
                {
                    comment = line.Substring(hash + 1).Trim();
                    line = line.Substring(0, hash).Trim();
                    if (line.Length == 0) continue;
                }

                var parts = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                var cmd = new Cmd { Verb = parts[0].ToLowerInvariant(), Comment = comment };

                if (cmd.Verb == "name")
                {
                    cmd.Text = line.Substring(parts[0].Length).Trim();
                    list.Add(cmd);
                    continue;
                }

                for (int i = 1; i < parts.Length; i++)
                {
                    int eq = parts[i].IndexOf('=');
                    if (eq > 0)
                    {
                        string k = parts[i].Substring(0, eq);
                        if (float.TryParse(parts[i].Substring(eq + 1),
                                           NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                            cmd.Set(k, v);
                    }
                    else if (cmd.IsHeader && float.TryParse(parts[i], NumberStyles.Float,
                                                            CultureInfo.InvariantCulture, out var hv))
                        cmd.Set("v", hv);
                    else cmd.Flags.Add(parts[i].ToLowerInvariant());
                }
                list.Add(cmd);
            }
            return list;
        }

        public static string Render(List<Cmd> cmds)
        {
            var sb = new StringBuilder();
            foreach (var c in cmds)
            {
                sb.Append(c.ToString());
                if (!string.IsNullOrEmpty(c.Comment)) sb.Append("   # ").Append(c.Comment);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        // ---- building ------------------------------------------------------

        public static Level Build(string source) => Build(Parse(source));

        public static Level Build(List<Cmd> cmds)
        {
            string name = "COURSE";
            float time = 60f, width = 9f;
            int decor = 0, music = 1;
            float gold = 0f, silver = 0f, bronze = 0f;

            foreach (var c in cmds)
            {
                switch (c.Verb)
                {
                    case "name": name = c.Text; break;
                    case "time": time = First(c, 60f); break;
                    case "width": width = First(c, 9f); break;
                    case "decor": decor = Mathf.RoundToInt(First(c, 0f)); break;
                    case "music": music = Mathf.RoundToInt(First(c, 1f)); break;
                    case "medals":
                        gold = c.Get("gold", 0f);
                        silver = c.Get("silver", 0f);
                        bronze = c.Get("bronze", 0f);
                        break;
                }
            }

            var b = new CourseBuilder(name, time, width).Theme(decor, music);
            b.Level.Gold = gold; b.Level.Silver = silver; b.Level.Bronze = bronze;

            foreach (var c in cmds)
            {
                switch (c.Verb)
                {
                    case "name": case "time": case "width": case "decor": case "music":
                    case "medals":
                        break;

                    case "pad":
                        b.Pad(c.Get("d", 8f), c.Get("w", width),
                              c.Has("start") ? Surface.Start : Surface.Normal);
                        break;
                    case "run": b.Run(c.Get("len", 10f), c.Get("w", -1f)); break;
                    case "ice": b.Ice(c.Get("len", 10f), c.Get("w", -1f)); break;
                    case "rough": b.Rough(c.Get("len", 10f), c.Get("w", -1f)); break;
                    case "crumble": b.Crumble(c.Get("len", 8f), c.Get("w", -1f), c.Get("tile", 2.4f)); break;
                    case "slope": b.Slope(c.Get("len", 14f), c.Get("drop", 4f), c.Get("w", -1f)); break;
                    case "hill": b.Hill(c.Get("len", 14f), c.Get("drop", 4f), c.Get("w", -1f), c.Has("rails")); break;
                    case "jog": b.Jog(c.Get("dx", 0f), c.Get("d", 10f), c.Get("w", -1f)); break;
                    case "gap": b.Gap(c.Get("len", 3f)); break;
                    case "step": b.Step(c.Get("dy", 2f)); break;
                    case "jump": b.Jump(c.Get("gap", 2.6f), c.Get("drop", 2.4f), c.Get("lip", 5f)); break;

                    case "curve":
                        b.Curve(c.Get("r", 14f), c.Get("a", 60f), c.Get("drop", 0f),
                                c.Get("w", -1f), c.Get("bank", 0f), c.Has("rails"));
                        break;
                    case "chicane":
                        b.Chicane(c.Get("r", 14f), c.Get("a", 40f), c.Get("drop", 0f),
                                  c.Get("w", -1f), c.Get("bank", 0f), c.Has("rails"));
                        break;

                    case "split":
                        b.Split(c.Get("len", 14f), c.Get("side", 3f), c.Get("pit", 4f), c.Get("apron", 5f));
                        break;
                    case "rails":
                        b.Rails(c.Get("l", 1f) > 0.5f, c.Get("r", 1f) > 0.5f, c.Get("h", 0.7f));
                        break;

                    case "acid":
                        b.Acid(c.Get("w", 3f), c.Get("d", 3f), c.Get("x", 0f),
                               c.Get("back", 3f), c.Get("lead", 4f));
                        break;
                    case "pillar":
                        b.Pillar(c.Get("x", 0f), c.Get("back", 3f), c.Get("r", 0.85f), c.Get("h", 2.6f));
                        break;
                    case "sweeper":
                        b.Sweeper(c.Get("x", 0f), c.Get("back", 4f), c.Get("len", 5f),
                                  c.Get("speed", 70f), c.Get("h", 0.55f), c.Get("phase", 0f));
                        break;
                    case "crusher":
                        b.Crusher(c.Get("x", 0f), c.Get("back", 4f), c.Get("w", 3f),
                                  c.Get("period", 2.4f), c.Get("phase", 0f), c.Get("lift", 4.5f));
                        break;
                    case "fan":
                        b.Fan(c.Get("x", 0f), c.Get("back", 3f), c.Get("w", 6f),
                              c.Get("d", 6f), c.Get("push", 16f));
                        break;

                    case "boost":
                        b.Boost(c.Get("x", 0f), c.Get("back", 3f), c.Get("w", 4f),
                                c.Get("d", 5f), c.Get("push", 26f));
                        break;

                    case "chaser":
                        b.Enemy(EnemyKind.Chaser, c.Get("x", 0f), c.Get("back", 4f),
                                c.Get("range", 20f), c.Get("speed", 9f));
                        break;
                    case "blob":
                        b.Enemy(EnemyKind.Wanderer, c.Get("x", 0f), c.Get("back", 4f),
                                c.Get("range", 999f), c.Get("speed", 6f));
                        break;

                    case "goal": b.Goal(c.Get("d", 9f), c.Get("w", -1f)); break;

                    default:
                        Debug.LogWarning($"[course] unknown verb '{c.Verb}' ignored");
                        break;
                }

                // remember where the cursor ended up, so the editor can park the
                // marble at any line and let you test from there
                b.Level.Anchors.Add(b.Cursor);
            }

            return b.Done();
        }

        static float First(Cmd c, float fallback)
        {
            if (c.Values.Count > 0) return c.Values[0];
            if (c.Flags.Count > 0 && float.TryParse(c.Flags[0], NumberStyles.Float,
                                                    CultureInfo.InvariantCulture, out var v)) return v;
            return fallback;
        }
    }
}
