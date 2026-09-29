using System.Collections.Generic;
using UnityEngine;

namespace RollingSteel
{
    /// In-game course editor.
    ///
    /// Courses are a list of lines, each a verb with named numbers, so editing is
    /// selecting a line, selecting a field, and nudging the number. Every change
    /// rebuilds the course immediately and parks the marble at the line you are
    /// editing, so you are always looking at what you just changed.
    public class CourseEditor : MonoBehaviour
    {
        public bool Active { get; private set; }

        List<Cmd> cmds = new List<Cmd>();
        int line, field;
        int palette;
        bool inserting;
        string status = "";
        float statusUntil;

        GUIStyle mono, head;
        Texture2D px;

        GameDirector Director => GameDirector.Instance;

        // ---- open / close --------------------------------------------------

        public void Toggle()
        {
            if (Active) Close();
            else Open();
        }

        void Open()
        {
            cmds = CourseScript.Parse(CourseStore.SourceFor(Director.LevelIndex));
            line = Mathf.Clamp(line, 0, Mathf.Max(0, cmds.Count - 1));
            field = 0;
            Active = true;
            Say("editing " + (CourseStore.PathFor(Director.LevelIndex) ?? "(memory only)"));
            Rebuild(park: true);
        }

        public void Close()
        {
            Active = false;
            inserting = false;
            Director.LeaveEditor();
        }

        void Say(string s) { status = s; statusUntil = Time.unscaledTime + 4f; }

        // ---- input ---------------------------------------------------------

        void Update()
        {
            if (!Active) return;

            if (inserting) { PaletteKeys(); return; }

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (Input.GetKeyDown(KeyCode.DownArrow)) { line = Mathf.Min(line + 1, cmds.Count - 1); field = 0; Rebuild(true); }
            if (Input.GetKeyDown(KeyCode.UpArrow)) { line = Mathf.Max(line - 1, 0); field = 0; Rebuild(true); }

            if (Input.GetKeyDown(KeyCode.Tab) && cmds.Count > 0)
            {
                int n = Mathf.Max(1, cmds[line].Keys.Count);
                field = (field + (shift ? n - 1 : 1)) % n;
            }

            float dir = 0f;
            if (Input.GetKeyDown(KeyCode.RightArrow)) dir = 1f;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) dir = -1f;
            if (dir != 0f) Nudge(dir * (shift ? 10f : alt ? 0.1f : 1f));

            if (Input.GetKeyDown(KeyCode.Space)) ToggleFlag();
            if (Input.GetKeyDown(KeyCode.I)) { inserting = true; palette = 0; }
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) DeleteLine();
            if (Input.GetKeyDown(KeyCode.D)) Duplicate();
            if (Input.GetKeyDown(KeyCode.S)) SaveToDisk();
            if (Input.GetKeyDown(KeyCode.L)) { Open(); Say("reloaded from disk"); }

        }

        /// The director routes Escape here, so one key is handled in one place.
        public void EscapePressed()
        {
            if (inserting) inserting = false;
            else Close();
        }

        void PaletteKeys()
        {
            // Escape is routed in from the director
            if (Input.GetKeyDown(KeyCode.Period) || Input.GetKeyDown(KeyCode.DownArrow))
                palette = (palette + 1) % CourseScript.Palette.Length;
            if (Input.GetKeyDown(KeyCode.Comma) || Input.GetKeyDown(KeyCode.UpArrow))
                palette = (palette + CourseScript.Palette.Length - 1) % CourseScript.Palette.Length;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                var cmd = CourseScript.NewCmd(CourseScript.Palette[palette]);
                cmds.Insert(Mathf.Min(line + 1, cmds.Count), cmd);
                line = Mathf.Min(line + 1, cmds.Count - 1);
                field = 0;
                inserting = false;
                Rebuild(true);
                Say("inserted " + cmd.Verb);
            }
        }

        // ---- edits ----------------------------------------------------------

        void Nudge(float amount)
        {
            if (cmds.Count == 0) return;
            var c = cmds[line];
            if (c.Keys.Count == 0) return;

            field = Mathf.Clamp(field, 0, c.Keys.Count - 1);
            string key = c.Keys[field];

            // angles and speeds want coarser steps than widths do
            float step = key == "a" || key == "speed" || key == "bank" || key == "range" ? 5f : 0.5f;
            c.Values[field] = Mathf.Round((c.Values[field] + amount * step) * 1000f) / 1000f;
            Rebuild(true);
        }

        void ToggleFlag()
        {
            if (cmds.Count == 0) return;
            var c = cmds[line];
            string flag = c.Verb == "pad" ? "start"
                        : c.Verb == "hill" || c.Verb == "curve" || c.Verb == "chicane" ? "rails"
                        : null;
            if (flag == null) { Say("no flag on " + c.Verb); return; }
            c.Toggle(flag);
            Rebuild(true);
        }

        void DeleteLine()
        {
            if (cmds.Count == 0) return;
            Say("deleted " + cmds[line].Verb);
            cmds.RemoveAt(line);
            line = Mathf.Clamp(line, 0, Mathf.Max(0, cmds.Count - 1));
            Rebuild(true);
        }

        void Duplicate()
        {
            if (cmds.Count == 0) return;
            cmds.Insert(line + 1, cmds[line].Clone());
            line++;
            Rebuild(true);
        }

        void SaveToDisk()
        {
            string src = CourseScript.Render(cmds);
            Say(CourseStore.Save(Director.LevelIndex, src)
                ? "saved " + CourseStore.PathFor(Director.LevelIndex)
                : "could not save");
        }

        void Rebuild(bool park)
        {
            Director.RebuildFromEditor(cmds, park ? line : -1);
        }

        // ---- drawing ---------------------------------------------------------

        void Styles()
        {
            if (px == null)
            {
                px = new Texture2D(1, 1);
                px.SetPixel(0, 0, Color.white);
                px.Apply();
                px.hideFlags = HideFlags.HideAndDontSave;
            }
            mono ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, richText = false };
            head ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            mono.fontSize = 13;
            head.fontSize = 15;
        }

        void Box(Rect r, Color c)
        {
            var prev = GUI.color; GUI.color = c; GUI.DrawTexture(r, px); GUI.color = prev;
        }

        void Text(Rect r, string t, GUIStyle st, Color c)
        {
            var prev = GUI.contentColor; GUI.contentColor = c; GUI.Label(r, t, st); GUI.contentColor = prev;
        }

        void OnGUI()
        {
            if (!Active) return;
            Styles();

            float w = 470f, h = Screen.height;
            Box(new Rect(0f, 0f, w, h), new Color(0.02f, 0.03f, 0.06f, 0.88f));

            Text(new Rect(12f, 8f, w, 22f),
                 $"COURSE EDITOR   {Director.CurrentLevel.Name}   ({Director.LevelIndex + 1}/{Director.LevelCount})",
                 head, new Color(1f, 0.78f, 0.22f));

            float y = 36f, rowH = 17f;
            int visible = Mathf.FloorToInt((h - 150f) / rowH);
            int first = Mathf.Clamp(line - visible / 2, 0, Mathf.Max(0, cmds.Count - visible));

            for (int i = first; i < cmds.Count && i < first + visible; i++)
            {
                var c = cmds[i];
                bool sel = i == line;
                if (sel) Box(new Rect(6f, y - 1f, w - 12f, rowH), new Color(0.2f, 0.4f, 0.6f, 0.55f));

                Text(new Rect(12f, y, 34f, rowH), i.ToString(), mono, new Color(1f, 1f, 1f, 0.35f));
                Text(new Rect(46f, y, 90f, rowH), c.Verb, mono,
                     sel ? Color.white : new Color(0.45f, 0.88f, 1f));

                float x = 130f;
                for (int k = 0; k < c.Keys.Count; k++)
                {
                    string t = $"{c.Keys[k]}={c.Values[k]:0.###}";
                    bool hot = sel && k == field;
                    if (hot) Box(new Rect(x - 3f, y - 1f, t.Length * 7.6f + 6f, rowH), new Color(1f, 0.78f, 0.22f, 0.35f));
                    Text(new Rect(x, y, 200f, rowH), t, mono,
                         hot ? new Color(1f, 0.9f, 0.5f) : new Color(1f, 1f, 1f, 0.8f));
                    x += t.Length * 7.6f + 10f;
                }
                foreach (var f in c.Flags)
                {
                    Text(new Rect(x, y, 90f, rowH), f, mono, new Color(0.55f, 1f, 0.6f));
                    x += f.Length * 7.6f + 10f;
                }
                y += rowH;
            }

            float footer = h - 108f;
            Box(new Rect(0f, footer, w, 108f), new Color(0f, 0f, 0f, 0.55f));

            if (inserting)
            {
                Text(new Rect(12f, footer + 6f, w, 20f),
                     $"INSERT:  < {CourseScript.Palette[palette]} >", head, new Color(0.55f, 1f, 0.6f));
                Text(new Rect(12f, footer + 30f, w, 20f),
                     ", / .  choose      ENTER  insert      ESC  cancel", mono, new Color(1f, 1f, 1f, 0.7f));
            }
            else
            {
                Text(new Rect(12f, footer + 4f, w, 20f),
                     "UP/DOWN line    TAB field    LEFT/RIGHT value", mono, new Color(1f, 1f, 1f, 0.75f));
                Text(new Rect(12f, footer + 22f, w, 20f),
                     "SHIFT x10   ALT x0.1   SPACE flag   I insert   D dup   DEL delete",
                     mono, new Color(1f, 1f, 1f, 0.55f));
                Text(new Rect(12f, footer + 40f, w, 20f),
                     "S save    L reload    F1/ESC close    (course rebuilds live)",
                     mono, new Color(1f, 1f, 1f, 0.55f));
            }

            if (Time.unscaledTime < statusUntil)
                Text(new Rect(12f, footer + 76f, w - 20f, 20f), status, mono, new Color(1f, 0.78f, 0.22f));
        }
    }
}
