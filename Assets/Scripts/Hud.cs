using UnityEngine;

namespace RollingSteel
{
    /// IMGUI on purpose: it needs no canvas, no prefab and no font asset, which
    /// keeps the whole game buildable from code.
    public class Hud : MonoBehaviour
    {
        static readonly Color Amber = new Color(1f, 0.78f, 0.22f);
        static readonly Color Cyan = new Color(0.45f, 0.88f, 1f);
        static readonly Color Red = new Color(1f, 0.35f, 0.32f);
        static readonly Color Green = new Color(0.55f, 1f, 0.6f);

        Texture2D px;
        GUIStyle label, big, huge, small;
        float s;

        void EnsureStyles()
        {
            if (px == null)
            {
                px = new Texture2D(1, 1);
                px.SetPixel(0, 0, Color.white);
                px.Apply();
                px.hideFlags = HideFlags.HideAndDontSave;
            }

            s = Mathf.Max(0.6f, Screen.height / 720f);

            label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            big ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            huge ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            small ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };

            label.fontSize = Mathf.RoundToInt(18 * s);
            big.fontSize = Mathf.RoundToInt(34 * s);
            huge.fontSize = Mathf.RoundToInt(58 * s);
            small.fontSize = Mathf.RoundToInt(20 * s);
        }

        void Box(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, px);
            GUI.color = prev;
        }

        void Text(Rect r, string t, GUIStyle st, Color c)
        {
            Color prev = GUI.contentColor;
            GUI.contentColor = new Color(0f, 0f, 0f, 0.75f);
            GUI.Label(new Rect(r.x + 2f * s, r.y + 2f * s, r.width, r.height), t, st);   // drop shadow
            GUI.contentColor = c;
            GUI.Label(r, t, st);
            GUI.contentColor = prev;
        }

        void OnGUI()
        {
            var g = GameDirector.Instance;
            if (g == null) return;
            EnsureStyles();

            float w = Screen.width, h = Screen.height;

            if (g.Flash > 0.001f)
            {
                var c = g.FlashColor;
                Box(new Rect(0f, 0f, w, h), new Color(c.r, c.g, c.b, g.Flash * 0.42f));
            }

            float barH = 46f * s;

            Box(new Rect(0f, 0f, w, barH), new Color(0f, 0f, 0f, 0.55f));

            if (!g.Editing)
                Text(new Rect(16f * s, 0f, w * 0.4f, barH),
                     $"COURSE {g.LevelIndex + 1}/{g.LevelCount}   {g.CurrentLevel.Name}", label, Cyan);

            Color timeCol = g.TimeLeft <= 10f ? Red : Amber;
            Text(new Rect(0f, 0f, w, barH), $"{g.TimeLeft:0.0}", big, timeCol);

            var rightStyle = new GUIStyle(label) { alignment = TextAnchor.MiddleRight };
            Text(new Rect(0f, 0f, w - 16f * s, barH), $"FALLS  {g.Deaths}", rightStyle, Amber);

            if (g.State != GameState.Title)
            {
                var mid = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
                mid.fontSize = Mathf.RoundToInt(15 * s);
                float best = Progress.BestTime(g.CurrentLevel.Name);
                string bestTxt = best > 0f ? $"   BEST {Progress.Format(best)}" : "";
                Text(new Rect(0f, barH + 12f * s, w, 20f * s),
                     $"{Progress.Format(g.CourseTime)}{bestTxt}", mid, new Color(1f, 1f, 1f, 0.6f));
            }

            // progress along the course
            float pw = w * 0.5f, px0 = (w - pw) * 0.5f, py = barH + 4f * s, ph = 4f * s;
            Box(new Rect(px0, py, pw, ph), new Color(1f, 1f, 1f, 0.16f));
            Box(new Rect(px0, py, pw * g.CourseProgress(), ph), Cyan);

            if (g.State != GameState.Title && !g.Editing)
            {
                var hint = new GUIStyle(small) { alignment = TextAnchor.MiddleLeft };
                hint.fontSize = Mathf.RoundToInt(14 * s);
                Text(new Rect(14f * s, h - 26f * s, w * 0.6f, 20f * s),
                     "Q / E  turn view    Z / X  tilt    WHEEL  zoom    M  music    F1  edit course    R  restart",
                     hint, new Color(1f, 1f, 1f, 0.45f));
            }

            switch (g.State)
            {
                case GameState.Title: if (!g.Editing) TitleCard(w, h); break;
                case GameState.Dying: Banner(w, h, g.DeathReason, Red, "-3 SECONDS"); break;
                case GameState.LevelClear: ClearBanner(g, w, h); break;
                case GameState.GameOver: Banner(w, h, "OUT OF TIME", Red, "SPACE / R  TO TRY AGAIN"); break;
                case GameState.Won:
                    Banner(w, h, "ALL COURSES CLEAR", Green,
                           $"{Progress.Format(g.RunTime)}   FALLS {g.Deaths}" +
                           (g.LastWasBest ? "   -   NEW BEST RUN" : "") + "   -   SPACE TO PLAY AGAIN");
                    break;
            }
        }

        void ClearBanner(GameDirector g, float w, float h)
        {
            string medal = Progress.MedalName(g.LastMedal);
            string sub = Progress.Format(g.LastCourseTime);
            if (medal.Length > 0) sub += "   " + medal;
            if (g.LastWasBest) sub += "   -   NEW BEST";

            Color c = g.LastMedal > 0 ? Progress.MedalColor(g.LastMedal) : Green;
            Banner(w, h, "COURSE CLEAR", c, sub);
        }

        void Banner(float w, float h, string title, Color c, string sub)
        {
            float bh = 150f * s;
            Box(new Rect(0f, h * 0.5f - bh * 0.5f, w, bh), new Color(0f, 0f, 0f, 0.6f));
            Text(new Rect(0f, h * 0.5f - 34f * s, w, 60f * s), title, huge, c);
            Text(new Rect(0f, h * 0.5f + 34f * s, w, 30f * s), sub, small, new Color(1f, 1f, 1f, 0.85f));
        }

        void TitleCard(float w, float h)
        {
            Box(new Rect(0f, 0f, w, h), new Color(0.02f, 0.03f, 0.07f, 0.42f));   // let the flyover through

            var g = GameDirector.Instance;

            Text(new Rect(0f, h * 0.10f, w, 70f * s), "ROLLING STEEL", huge, Amber);
            Text(new Rect(0f, h * 0.10f + 62f * s, w, 30f * s),
                 "six courses, one clock", small, Cyan);

            // course select
            float rowH = 26f * s, listW = 470f * s, x0 = (w - listW) * 0.5f;
            float y = h * 0.30f;

            var nameStyle = new GUIStyle(small) { alignment = TextAnchor.MiddleLeft };
            var timeStyle = new GUIStyle(small) { alignment = TextAnchor.MiddleRight };
            nameStyle.fontSize = timeStyle.fontSize = Mathf.RoundToInt(17 * s);

            for (int i = 0; i < g.LevelCount; i++)
            {
                var lvl = g.LevelAt(i);
                bool sel = i == g.TitleSelect;
                if (sel) Box(new Rect(x0 - 10f * s, y, listW + 20f * s, rowH),
                             new Color(0.25f, 0.5f, 0.75f, 0.45f));

                float best = Progress.BestTime(lvl.Name);
                int medal = Progress.MedalFor(lvl, best);

                Text(new Rect(x0, y, listW * 0.6f, rowH), $"{i + 1}   {lvl.Name}", nameStyle,
                     sel ? Color.white : new Color(1f, 1f, 1f, 0.75f));
                Text(new Rect(x0, y, listW * 0.85f, rowH), Progress.Format(best), timeStyle,
                     best > 0f ? Amber : new Color(1f, 1f, 1f, 0.3f));
                Text(new Rect(x0, y, listW, rowH), Progress.MedalName(medal), timeStyle,
                     Progress.MedalColor(medal));
                y += rowH;
            }

            y += 14f * s;
            if (Progress.BestRunTime > 0f)
            {
                Text(new Rect(0f, y, w, 24f * s),
                     $"BEST FULL RUN   {Progress.Format(Progress.BestRunTime)}   " +
                     $"({Progress.BestRunFalls} falls)", small, Cyan);
                y += 26f * s;
            }

            Text(new Rect(0f, y, w, 24f * s),
                 "UP / DOWN  choose course       SPACE  start from there",
                 small, new Color(1f, 1f, 1f, 0.7f));
            y += 24f * s;
            Text(new Rect(0f, y, w, 24f * s),
                 "WASD push   Q/E turn view   Z/X tilt   WHEEL zoom   M music   F1 editor",
                 small, new Color(1f, 1f, 1f, 0.5f));

            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3.2f);
            Text(new Rect(0f, h * 0.86f, w, 40f * s), "PRESS  SPACE  TO  START", big,
                 new Color(Green.r, Green.g, Green.b, pulse));
        }
    }
}
