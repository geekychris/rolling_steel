using UnityEngine;

namespace RollingSteel
{
    /// IMGUI on purpose: it needs no canvas, no prefab and no font asset, which
    /// keeps the whole game buildable from code.
    ///
    /// Anything a player owns - clock, falls, progress, their own wipeout - is
    /// drawn inside that player's viewport. Anything about the race as a whole is
    /// drawn across the middle of the screen.
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

        /// A camera's viewport in GUI coordinates (origin top-left).
        static Rect ViewOf(Camera c)
        {
            var r = c.rect;
            return new Rect(r.x * Screen.width,
                            (1f - r.y - r.height) * Screen.height,
                            r.width * Screen.width,
                            r.height * Screen.height);
        }

        void OnGUI()
        {
            var g = GameDirector.Instance;
            if (g == null || g.PlayerCount == 0) return;
            EnsureStyles();

            float w = Screen.width, h = Screen.height;
            bool two = g.PlayerCount > 1;

            foreach (var p in g.Players) DrawPlayer(g, p, ViewOf(p.Cam), two);

            if (two) Box(new Rect(w * 0.5f - 1f, 0f, 2f, h), new Color(0f, 0f, 0f, 0.7f));

            if (g.State != GameState.Title && !g.Editing)
            {
                var hint = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
                hint.fontSize = Mathf.RoundToInt(13 * s);
                Text(new Rect(0f, h - 22f * s, w, 20f * s),
                     "Q / E  turn view    Z / X  tilt    WHEEL  zoom    M  music    F1  edit    R  restart",
                     hint, new Color(1f, 1f, 1f, 0.4f));
            }

            switch (g.State)
            {
                case GameState.Title: if (!g.Editing) TitleCard(g, w, h); break;
                case GameState.LevelClear: ClearBanner(g, w, h, two); break;
                case GameState.GameOver: Banner(w, h, "OUT OF TIME", Red, "SPACE / R  TO TRY AGAIN"); break;
                case GameState.Won: WonBanner(g, w, h, two); break;
            }
        }

        // ---- one player's half of the screen --------------------------------

        void DrawPlayer(GameDirector g, Player p, Rect a, bool two)
        {
            if (p.Flash > 0.001f)
            {
                var c = p.FlashColor;
                Box(a, new Color(c.r, c.g, c.b, p.Flash * 0.42f));
            }

            if (g.State == GameState.Title) return;

            float barH = 46f * s;
            Box(new Rect(a.x, a.y, a.width, barH), new Color(0f, 0f, 0f, 0.55f));

            string left = two ? p.Label : $"COURSE {g.LevelIndex + 1}/{g.LevelCount}   {g.CurrentLevel.Name}";
            if (two && p.Wins > 0) left += $"   WON {p.Wins}";
            if (!g.Editing)
                Text(new Rect(a.x + 14f * s, a.y, a.width * 0.5f, barH), left, label,
                     two ? p.Tint : Cyan);

            Color timeCol = p.TimeLeft <= 10f ? Red : Amber;
            Text(new Rect(a.x, a.y, a.width, barH), $"{p.TimeLeft:0.0}", big, timeCol);

            var right = new GUIStyle(label) { alignment = TextAnchor.MiddleRight };
            Text(new Rect(a.x, a.y, a.width - 14f * s, barH), $"FALLS  {p.Deaths}", right, Amber);

            var mid = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            mid.fontSize = Mathf.RoundToInt(15 * s);
            float best = Progress.BestTime(g.CurrentLevel.Name);
            string bestTxt = !two && best > 0f ? $"   BEST {Progress.Format(best)}" : "";
            Text(new Rect(a.x, a.y + barH + 12f * s, a.width, 20f * s),
                 $"{Progress.Format(p.CourseTime)}{bestTxt}", mid, new Color(1f, 1f, 1f, 0.6f));

            float pw = a.width * 0.5f, px0 = a.x + (a.width - pw) * 0.5f, py = a.y + barH + 4f * s;
            Box(new Rect(px0, py, pw, 4f * s), new Color(1f, 1f, 1f, 0.16f));
            Box(new Rect(px0, py, pw * g.CourseProgress(p), 4f * s), two ? p.Tint : Cyan);

            if (p.Dying)
                LocalBanner(a, p.DeathReason, Red, "-3 SECONDS");
            else if (p.OutOfTime && g.State == GameState.Playing)
                LocalBanner(a, "OUT OF TIME", Red, two ? "" : "");
            else if (p.Finished && g.State == GameState.Playing)
                LocalBanner(a, "FINISHED", Green, Progress.Format(p.FinishTime));
        }

        void LocalBanner(Rect a, string title, Color c, string sub)
        {
            float bh = 120f * s;
            float y = a.y + a.height * 0.5f - bh * 0.5f;
            Box(new Rect(a.x, y, a.width, bh), new Color(0f, 0f, 0f, 0.55f));
            Text(new Rect(a.x, y + 26f * s, a.width, 50f * s), title,
                 a.width < Screen.width * 0.75f ? big : huge, c);
            if (sub.Length > 0)
                Text(new Rect(a.x, y + 80f * s, a.width, 28f * s), sub, small, new Color(1f, 1f, 1f, 0.85f));
        }

        // ---- whole-race overlays ---------------------------------------------

        void ClearBanner(GameDirector g, float w, float h, bool two)
        {
            if (two)
            {
                var win = g.LastWinner;
                Banner(w, h, win != null ? $"{win.Label} TAKES IT" : "COURSE CLEAR",
                       win != null ? win.Tint : Green,
                       $"{Progress.Format(g.LastCourseTime)}   -   {Score(g)}");
                return;
            }

            string medal = Progress.MedalName(g.LastMedal);
            string sub = Progress.Format(g.LastCourseTime);
            if (medal.Length > 0) sub += "   " + medal;
            if (g.LastWasBest) sub += "   -   NEW BEST";
            Banner(w, h, "COURSE CLEAR", g.LastMedal > 0 ? Progress.MedalColor(g.LastMedal) : Green, sub);
        }

        void WonBanner(GameDirector g, float w, float h, bool two)
        {
            if (two)
            {
                var a = g.Players[0];
                var b = g.Players[1];
                string who = a.Wins == b.Wins ? "A DRAW" : (a.Wins > b.Wins ? "P1 WINS" : "P2 WINS");
                Banner(w, h, who, a.Wins >= b.Wins ? a.Tint : b.Tint,
                       $"{Score(g)}   -   SPACE TO PLAY AGAIN");
                return;
            }

            Banner(w, h, "ALL COURSES CLEAR", Green,
                   $"{Progress.Format(g.RunTime)}   FALLS {g.Deaths}" +
                   (g.LastWasBest ? "   -   NEW BEST RUN" : "") + "   -   SPACE TO PLAY AGAIN");
        }

        static string Score(GameDirector g) =>
            g.PlayerCount > 1 ? $"P1 {g.Players[0].Wins}  -  {g.Players[1].Wins} P2" : "";

        void Banner(float w, float h, string title, Color c, string sub)
        {
            float bh = 150f * s;
            Box(new Rect(0f, h * 0.5f - bh * 0.5f, w, bh), new Color(0f, 0f, 0f, 0.6f));
            Text(new Rect(0f, h * 0.5f - 34f * s, w, 60f * s), title, huge, c);
            Text(new Rect(0f, h * 0.5f + 34f * s, w, 30f * s), sub, small, new Color(1f, 1f, 1f, 0.85f));
        }

        void TitleCard(GameDirector g, float w, float h)
        {
            Box(new Rect(0f, 0f, w, h), new Color(0.02f, 0.03f, 0.07f, 0.42f));   // let the flyover through

            Text(new Rect(0f, h * 0.08f, w, 70f * s), "ROLLING STEEL", huge, Amber);
            Text(new Rect(0f, h * 0.08f + 62f * s, w, 30f * s), "six courses, one clock", small, Cyan);

            float rowH = 26f * s, listW = 470f * s, x0 = (w - listW) * 0.5f;
            float y = h * 0.27f;

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

            y += 12f * s;
            Text(new Rect(0f, y, w, 26f * s),
                 g.WantPlayers > 1 ? "TWO PLAYERS   -   P1 WASD    P2 ARROWS" : "ONE PLAYER",
                 small, g.WantPlayers > 1 ? new Color(1f, 0.62f, 0.35f) : Cyan);
            y += 26f * s;

            if (Progress.BestRunTime > 0f)
            {
                Text(new Rect(0f, y, w, 24f * s),
                     $"BEST FULL RUN   {Progress.Format(Progress.BestRunTime)}   " +
                     $"({Progress.BestRunFalls} falls)", small, Cyan);
                y += 24f * s;
            }

            Text(new Rect(0f, y, w, 24f * s),
                 "UP / DOWN  course       LEFT / RIGHT  players       SPACE  start",
                 small, new Color(1f, 1f, 1f, 0.7f));
            y += 22f * s;
            Text(new Rect(0f, y, w, 24f * s),
                 "Q/E turn view   Z/X tilt   WHEEL zoom   M music   F1 editor",
                 small, new Color(1f, 1f, 1f, 0.5f));

            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3.2f);
            Text(new Rect(0f, h * 0.88f, w, 40f * s), "PRESS  SPACE  TO  START", big,
                 new Color(Green.r, Green.g, Green.b, pulse));
        }
    }
}
