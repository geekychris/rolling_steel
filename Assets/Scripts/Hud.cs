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
            float barH = 46f * s;

            Box(new Rect(0f, 0f, w, barH), new Color(0f, 0f, 0f, 0.55f));

            Text(new Rect(16f * s, 0f, w * 0.4f, barH),
                 $"COURSE {g.LevelIndex + 1}/{g.LevelCount}   {g.CurrentLevel.Name}", label, Cyan);

            Color timeCol = g.TimeLeft <= 10f ? Red : Amber;
            Text(new Rect(0f, 0f, w, barH), $"{g.TimeLeft:0.0}", big, timeCol);

            var rightStyle = new GUIStyle(label) { alignment = TextAnchor.MiddleRight };
            Text(new Rect(0f, 0f, w - 16f * s, barH), $"FALLS  {g.Deaths}", rightStyle, Amber);

            // progress along the course
            float pw = w * 0.5f, px0 = (w - pw) * 0.5f, py = barH + 6f * s, ph = 5f * s;
            Box(new Rect(px0, py, pw, ph), new Color(1f, 1f, 1f, 0.16f));
            Box(new Rect(px0, py, pw * g.Progress(), ph), Cyan);

            if (g.State != GameState.Title)
            {
                var hint = new GUIStyle(small) { alignment = TextAnchor.MiddleLeft };
                hint.fontSize = Mathf.RoundToInt(14 * s);
                Text(new Rect(14f * s, h - 26f * s, w * 0.6f, 20f * s),
                     "Q / E  turn view      Z / X  tilt      WHEEL  zoom      R  restart",
                     hint, new Color(1f, 1f, 1f, 0.45f));
            }

            switch (g.State)
            {
                case GameState.Title: TitleCard(w, h); break;
                case GameState.Dying: Banner(w, h, g.DeathReason, Red, "-3 SECONDS"); break;
                case GameState.LevelClear: Banner(w, h, "COURSE CLEAR", Green, "TIME CARRIES OVER"); break;
                case GameState.GameOver: Banner(w, h, "OUT OF TIME", Red, "SPACE / R  TO TRY AGAIN"); break;
                case GameState.Won: Banner(w, h, "ALL COURSES CLEAR", Green, $"FALLS: {g.Deaths}   -   SPACE TO PLAY AGAIN"); break;
            }
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
            Box(new Rect(0f, 0f, w, h), new Color(0.02f, 0.03f, 0.07f, 0.72f));

            Text(new Rect(0f, h * 0.24f, w, 70f * s), "ROLLING STEEL", huge, Amber);
            Text(new Rect(0f, h * 0.24f + 62f * s, w, 30f * s), "three courses, one clock", small, Cyan);

            string[] lines =
            {
                "WASD  or  ARROW KEYS   -   push the marble",
                "Q / E  turn the view      Z / X  tilt      WHEEL  zoom",
                "steering follows the camera, so turn it to suit the course",
                "",
                "the marble has momentum: steer early, brake early",
                "avoid the acid, the blobs and the steel marbles",
                "falling costs you 3 seconds   -   the clock never stops",
                "",
                "R  restart      ESC  quit",
            };

            float y = h * 0.46f;
            foreach (var line in lines)
            {
                Text(new Rect(0f, y, w, 26f * s), line, small, new Color(1f, 1f, 1f, 0.82f));
                y += 28f * s;
            }

            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3.2f);
            Text(new Rect(0f, h * 0.8f, w, 40f * s), "PRESS  SPACE  TO  START", big,
                 new Color(Green.r, Green.g, Green.b, pulse));
        }
    }
}
