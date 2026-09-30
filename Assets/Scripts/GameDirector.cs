using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RollingSteel
{
    public enum GameState { Title, Playing, Dying, LevelClear, GameOver, Won }

    /// Owns the run: builds the world in code, runs the clock, and moves between
    /// the three courses. There is exactly one authored asset in this project
    /// (an empty scene holding this component); everything else is made here.
    public class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        const float DeathPenalty = 3f;
        const float DyingHold = 1.35f;   // long enough for the shatter to read
        const float ClearHold = 3.4f;    // long enough for the flyover to read

        public GameState State { get; private set; } = GameState.Title;
        readonly List<Player> players = new List<Player>();
        public IReadOnlyList<Player> Players => players;
        public Player P1 => players[0];
        public int PlayerCount => players.Count;
        /// How many players the next run will use; chosen on the title screen.
        public int WantPlayers { get; private set; } = 1;

        public MarbleController Marble => players.Count > 0 ? players[0].Marble : null;
        public float TimeLeft => players.Count > 0 ? players[0].TimeLeft : 0f;
        public int LevelIndex { get; private set; }
        public int Deaths => players.Count > 0 ? players[0].Deaths : 0;
        /// Seconds spent on the current course, and on the run so far.
        public float CourseTime => players.Count > 0 ? players[0].CourseTime : 0f;
        public float RunTime => players.Count > 0 ? players[0].RunTime : 0f;
        public int LastMedal { get; private set; }
        public float LastCourseTime { get; private set; }
        public bool LastWasBest { get; private set; }
        /// Which course the title screen has highlighted.
        public int TitleSelect { get; private set; }
        public string DeathReason => players.Count > 0 ? players[0].DeathReason : "";
        public bool AnyMarbleLive
        {
            get
            {
                if (State != GameState.Playing || Editing) return false;
                foreach (var p in players) if (p.Racing) return true;
                return false;
            }
        }
        public bool Editing => editor != null && editor.Active;
        public float KillY => built?.KillY ?? -200f;
        public Level CurrentLevel => levels[Mathf.Clamp(LevelIndex, 0, levels.Count - 1)];
        public Level LevelAt(int i) => levels[Mathf.Clamp(i, 0, levels.Count - 1)];
        public int LevelCount => levels.Count;

        List<Level> levels;
        BuiltLevel built;
        Camera cam;
        IsoCamera isoCam;
        AudioSource rollSrc;
        AudioSource musicSrc;
        CourseEditor editor;
        int currentTheme = -1;
        bool musicMuted;
        float stateTimer;
        float cineT;
        int startedAt;

        // ghost of the best run on this course
        readonly List<Vector3> ghostRec = new List<Vector3>();
        float ghostAccum;
        GhostData ghostData;
        GameObject ghostGo;
        Renderer ghostView;                     // position of the title flyover along the course

        /// Which player took the last course, for the two-player banner.
        public Player LastWinner { get; private set; }

        // ---- headless capture / demo hooks --------------------------------
        bool demoMode, autoStart;
        float startYaw;
        string shotDir, musicDumpDir, coursesDir;
        float quitAfter = -1f;
        float[] shotTimes = { 1f, 6f, 11f, 20f, 27f, 33f, 45f, 54f, 62f, 67.5f };
        float killAt = -1f;
        bool killTriggered;
        int nextShot;
        float clock;
        int demoWp;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;

            ParseArgs();
            CourseStore.Init(coursesDir);
            levels = CourseStore.LoadLevels();

            if (!string.IsNullOrEmpty(musicDumpDir))
            {
                Directory.CreateDirectory(musicDumpDir);
                for (int i = 0; i <= 6; i++)
                {
                    string path = Path.Combine(musicDumpDir, $"theme{i}.wav");
                    Music.WriteWav(path, Music.RenderRaw(i));
                    Debug.Log($"[music] wrote {path}");
                }
                Application.Quit();
                return;
            }

            Progress.Load();
            Sfx.Init(gameObject);

            musicSrc = gameObject.AddComponent<AudioSource>();
            musicSrc.loop = true;
            musicSrc.playOnAwake = false;
            musicSrc.spatialBlend = 0f;
            musicSrc.volume = 0.34f;
            musicSrc.mute = musicMuted;

            BuildRig();
            BuildPlayers(WantPlayers);
            BuildGhost();
            gameObject.AddComponent<Hud>();
            editor = gameObject.AddComponent<CourseEditor>();

            // Show the first course behind the title card rather than an empty void.
            LoadLevel(0, resetClock: true);
            if (autoStart) { State = GameState.Playing; Sfx.Play(Sfx.Clip.Start); }
            else { State = GameState.Title; Marble.Frozen = true; }

            cineT = 0f;
            PlayTheme(State == GameState.Title ? 0 : CurrentLevel.MusicTheme);
        }

        void ParseArgs()
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length; i++)
            {
                switch (a[i])
                {
                    case "-autostart": autoStart = true; break;
                    case "-demo": demoMode = true; autoStart = true; break;
                    case "-shots": if (i + 1 < a.Length) shotDir = a[++i]; break;
                    case "-yaw": if (i + 1 < a.Length) float.TryParse(a[++i], out startYaw); break;
                    case "-dumpmusic": if (i + 1 < a.Length) musicDumpDir = a[++i]; break;
                    case "-mute": musicMuted = true; break;
                    case "-courses": if (i + 1 < a.Length) coursesDir = a[++i]; break;
                    case "-players":
                        if (i + 1 < a.Length && int.TryParse(a[++i], out var n))
                            WantPlayers = Mathf.Clamp(n, 1, 2);
                        break;

                    // dev aids: force a wipeout, and choose when screenshots land,
                    // so the death effects can be captured without waiting for the
                    // demo driver to make a mistake.
                    case "-killat": if (i + 1 < a.Length) float.TryParse(a[++i], out killAt); break;
                    case "-shotat":
                        if (i + 1 < a.Length)
                        {
                            var parts = a[++i].Split(',');
                            var times = new List<float>();
                            foreach (var t in parts)
                                if (float.TryParse(t, out var v)) times.Add(v);
                            if (times.Count > 0) shotTimes = times.ToArray();
                        }
                        break;
                    case "-quitafter": if (i + 1 < a.Length) float.TryParse(a[++i], out quitAfter); break;
                }
            }
            if (!string.IsNullOrEmpty(shotDir)) Directory.CreateDirectory(shotDir);
        }

        // ---- world ---------------------------------------------------------

        void BuildRig()
        {
            var lightGo = new GameObject("KeyLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;
            lightGo.transform.rotation = Quaternion.Euler(52f, -30f, 0f);

            var fill = new GameObject("FillLight");
            var fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional;
            fl.color = new Color(0.45f, 0.55f, 0.85f);
            fl.intensity = 0.45f;
            fl.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(20f, 160f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.38f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.2f, 0.22f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.07f, 0.07f, 0.1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.11f);
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 135f;
        }

        /// Rebuild the roster. Each player gets their own marble and their own
        /// camera; in two-player the viewports split the screen down the middle.
        void BuildPlayers(int count)
        {
            foreach (var p in players)
            {
                if (p.Marble != null) Destroy(p.Marble.gameObject);
                if (p.Cam != null) Destroy(p.Cam.gameObject);
            }
            players.Clear();

            for (int i = 0; i < count; i++)
            {
                var p = new Player { Index = i };

                var camGo = new GameObject($"Camera{i + 1}");
                if (i == 0) { camGo.tag = "MainCamera"; camGo.AddComponent<AudioListener>(); }
                p.Cam = camGo.AddComponent<Camera>();
                p.Cam.clearFlags = CameraClearFlags.SolidColor;
                p.Cam.backgroundColor = new Color(0.05f, 0.06f, 0.11f);
                p.Cam.rect = count == 1
                    ? new Rect(0f, 0f, 1f, 1f)
                    : new Rect(i * 0.5f, 0f, 0.5f, 1f);

                p.Rig = camGo.AddComponent<IsoCamera>();
                p.Rig.Yaw = startYaw;
                if (count > 1) p.Rig.Size = 10f;      // half the width, so pull in

                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Marble" + (i + 1);
                go.transform.localScale = Vector3.one * (CourseBuilder.MarbleRadius * 2f);
                go.GetComponent<MeshRenderer>().sharedMaterial = MatLib.Get(i == 0 ? "Marble" : "MarbleTwo");
                go.GetComponent<SphereCollider>().sharedMaterial = MatLib.Marble;

                p.Marble = go.AddComponent<MarbleController>();
                p.Marble.Bind(p.Cam);
                if (count > 1)
                {
                    // one keyboard, two drivers
                    p.Marble.UpKeys = new[] { i == 0 ? KeyCode.W : KeyCode.UpArrow };
                    p.Marble.DownKeys = new[] { i == 0 ? KeyCode.S : KeyCode.DownArrow };
                    p.Marble.LeftKeys = new[] { i == 0 ? KeyCode.A : KeyCode.LeftArrow };
                    p.Marble.RightKeys = new[] { i == 0 ? KeyCode.D : KeyCode.RightArrow };
                }

                p.Roll = go.AddComponent<AudioSource>();
                p.Roll.clip = Sfx.Roll;
                p.Roll.loop = true;
                p.Roll.volume = 0f;
                p.Roll.spatialBlend = 0f;
                p.Roll.Play();

                p.Rig.Target = go.transform;
                players.Add(p);
            }

            cam = players[0].Cam;
            isoCam = players[0].Rig;
        }

        void BuildGhost()
        {
            ghostGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ghostGo.name = "Ghost";
            ghostGo.transform.localScale = Vector3.one * (CourseBuilder.MarbleRadius * 2f);
            var gcol = ghostGo.GetComponent<Collider>();
            if (gcol != null) { gcol.enabled = false; Destroy(gcol); }
            ghostView = ghostGo.GetComponent<MeshRenderer>();
            ghostView.sharedMaterial = MatLib.Get("Ghost");
            ghostView.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ghostView.enabled = false;
        }

        void LoadLevel(int index, bool resetClock)
        {
            ClearBuilt();
            LevelIndex = index;
            built = LevelBuilder.Build(levels[index]);

            ghostRec.Clear();
            ghostAccum = 0f;
            ghostData = players.Count == 1 ? Ghost.Load(levels[index].Name) : null;
            if (ghostView != null) ghostView.enabled = false;

            LastWinner = null;
            Time.timeScale = 1f;

            Vector3 across = built.Root.transform.right;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                p.ResetForCourse(levels[index].TimeBonus, resetClock);
                p.Marble.Frozen = false;
                p.Marble.SetVisible(true);
                // two marbles cannot start on the same square inch
                p.Marble.Teleport(built.SpawnWorld +
                                  (players.Count > 1 ? across * (i == 0 ? -1.3f : 1.3f) : Vector3.zero));
                p.Rig.Hold = false;
                p.Rig.Snap();
                ResyncDemo(p);
            }

            Debug.Log($"[level] {index + 1}/{levels.Count} {levels[index].Name} clock={players[0].TimeLeft:0.0}");
            PlayTheme(levels[index].MusicTheme);
        }

        // ---- loop ----------------------------------------------------------

        void Update()
        {
            clock += Time.unscaledDeltaTime;
            stateTimer += Time.unscaledDeltaTime;
            foreach (var p in players)
                p.Flash = Mathf.MoveTowards(p.Flash, 0f, Time.unscaledDeltaTime * 3.4f);

            HandleKeys();
            HandleCapture();

            if (killAt > 0f && !killTriggered && clock >= killAt && State == GameState.Playing)
            {
                killTriggered = true;
                Kill(players[0].Marble, "FELL OFF");
            }

            if (demoMode && !Editing)
                foreach (var p in players)
                {
                    if (p.Marble == null) continue;
                    p.Marble.UseScriptedInput = true;
                    p.Marble.ScriptedInput = DemoInput(p);
                }

            switch (State)
            {
                case GameState.Playing: if (!Editing) TickPlaying(); break;
                case GameState.LevelClear: if (stateTimer >= ClearHold) Advance(); break;
            }

            UpdateCinematic();
            UpdateRollAudio();
        }

        /// Steer along the course centreline, then convert that world-space wish
        /// into camera-relative stick input so the demo works at any view angle.
        Vector2 DemoInput(Player pl)
        {
            var path = built?.PathWorld;
            if (path == null || path.Count == 0) return Vector2.up;

            Vector3 pos = pl.Marble.transform.position;
            while (pl.DemoWp < path.Count - 1)
            {
                Vector3 d = path[pl.DemoWp] - pos;
                d.y = 0f;
                if (d.magnitude < 2.0f) pl.DemoWp++; else break;
            }

            Vector3 toWp = path[pl.DemoWp] - pos;
            toWp.y = 0f;
            if (toWp.sqrMagnitude < 0.0001f) return Vector2.up;

            // Velocity matching rather than full throttle: this brakes into turns
            // and on ice, which is what a human does and what the course expects.
            const float CruiseSpeed = 8.5f;
            Vector3 v = pl.Marble.Body.linearVelocity;
            v.y = 0f;
            Vector3 err = toWp.normalized * CruiseSpeed - v;
            Vector3 wish = err.sqrMagnitude < 0.0001f ? toWp.normalized : err.normalized;

            Transform c = pl.Cam.transform;
            Vector3 f = Vector3.ProjectOnPlane(c.forward, Vector3.up).normalized;
            Vector3 r = Vector3.ProjectOnPlane(c.right, Vector3.up).normalized;
            return new Vector2(Vector3.Dot(wish, r), Vector3.Dot(wish, f));
        }

        /// Snap the demo driver onto whichever waypoint is nearest right now.
        void ResyncDemo(Player p)
        {
            p.DemoWp = 0;
            var path = built?.PathWorld;
            if (path == null || path.Count == 0 || p.Marble == null) return;

            float best = float.MaxValue;
            Vector3 pos = p.Marble.transform.position;
            for (int i = 0; i < path.Count; i++)
            {
                float d = (path[i] - pos).sqrMagnitude;
                if (d < best) { best = d; p.DemoWp = i; }
            }
        }

        /// The camera goes on a slow helicopter orbit whenever nobody is driving:
        /// a travelling flyover of the course behind the title card, and a circle
        /// of the finish pad when a course is cleared.
        void UpdateCinematic()
        {
            if (isoCam == null || built == null) return;

            switch (State)
            {
                case GameState.Title:
                    if (LevelIndex != TitleSelect) { LoadLevel(TitleSelect, resetClock: true); FreezeAll(); }
                    // drift the focus along the course, so it is a flyover of the
                    // whole thing rather than a turntable of one spot
                    cineT = Mathf.Repeat(cineT + Time.unscaledDeltaTime * 0.045f, 1f);
                    OrbitAll(PathPointAt(cineT) + Vector3.up * 5f, 9f, 27f, 20f);
                    break;

                case GameState.LevelClear:
                case GameState.Won:
                    // look a little above the pad, so the pad itself sits below
                    // the banner rather than behind it
                    OrbitAll(built.GoalWorld + Vector3.up * 7f, 34f, 30f, 16f);
                    break;

                default:
                    foreach (var pl in players) if (pl.Rig.Cinematic) pl.Rig.EndCinematic();
                    break;
            }

            if (State == GameState.Title && ghostView != null) ghostView.enabled = false;
        }

        /// Put every camera on the same orbit. In two-player both halves of the
        /// screen fly around the same point, which reads as one shot rather than two.
        void OrbitAll(Vector3 focus, float spin, float pitch, float size)
        {
            foreach (var p in players)
            {
                p.Rig.CineSpin = spin;
                p.Rig.CinePitch = pitch;
                p.Rig.CineSize = players.Count > 1 ? size * 0.8f : size;
                p.Rig.BeginCinematic(focus);
                p.Rig.CineFocus = focus;
            }
        }

        /// A point along the course centreline, 0 at the start and 1 at the goal.
        Vector3 PathPointAt(float t)
        {
            var path = built.PathWorld;
            if (path == null || path.Count == 0) return built.SpawnWorld;

            float f = Mathf.Clamp01(t) * (path.Count - 1);
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, path.Count - 2);
            return Vector3.Lerp(path[i], path[i + 1], f - i) + Vector3.up * 2f;
        }

        void TickPlaying()
        {
            bool everyoneDone = true;
            foreach (var p in players)
            {
                TickPlayer(p);
                if (!p.OutOfTime) everyoneDone = false;
            }

            // a ghost only means anything when there is one marble to compare to
            if (players.Count == 1) { RecordGhost(); PlayGhost(); }

            if (everyoneDone)
            {
                Enter(GameState.GameOver);
                FreezeAll();
                Sfx.Play(Sfx.Clip.Death);
            }
        }

        void TickPlayer(Player p)
        {
            if (p.Finished || p.Marble == null) return;

            if (p.Dying)
            {
                p.DyingTimer += Time.unscaledDeltaTime;
                // slow motion is a single-player luxury; it would freeze the other
                // player's race too
                if (players.Count == 1)
                    Time.timeScale = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(p.DyingTimer / DyingHold));
                if (p.DyingTimer >= DyingHold) Respawn(p);
                return;
            }

            if (p.OutOfTime) return;

            p.TimeLeft -= Time.deltaTime;
            p.CourseTime += Time.deltaTime;
            p.RunTime += Time.deltaTime;

            if (p.TimeLeft <= 10f && p.TimeLeft > 0f && clock - p.LastWarnBeep > 1f)
            {
                p.LastWarnBeep = clock;
                Sfx.Play(Sfx.Clip.Warn);
            }

            if (p.TimeLeft <= 0f)
            {
                p.TimeLeft = 0f;
                p.OutOfTime = true;
                p.Marble.Frozen = true;
                Sfx.Play(Sfx.Clip.Death);
                return;
            }

            // How far it has dropped since it last had contact. Reacting to this
            // rather than an absolute floor means the fall is still on screen when
            // the camera stops following, so the wipeout is actually visible.
            float dropped = p.Marble.LastGrounded.y - p.Marble.transform.position.y;
            bool airborne = !p.Marble.Grounded;

            if (airborne && dropped > 4.5f && !p.FallWhistle)
            {
                p.FallWhistle = true;
                Sfx.Play(Sfx.Clip.Fall);
            }
            if (!airborne || dropped < 1f) p.FallWhistle = false;

            if ((airborne && dropped > 12f) || p.Marble.transform.position.y < KillY)
                Kill(p.Marble, "FELL OFF");
        }

        void FreezeAll()
        {
            foreach (var p in players) if (p.Marble != null) p.Marble.Frozen = true;
        }

        public Player PlayerOf(MarbleController m)
        {
            foreach (var p in players) if (p.Marble == m) return p;
            return null;
        }

        /// Closest racing marble to a point - what the chasers steer at.
        public MarbleController NearestMarble(Vector3 from)
        {
            MarbleController best = null;
            float bestD = float.MaxValue;
            foreach (var p in players)
            {
                if (!p.Racing || p.Marble == null) continue;
                float d = (p.Marble.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p.Marble; }
            }
            return best;
        }

        /// Sample the marble on a fixed clock so playback lines up with course
        /// time, and in course space so the ghost survives any change to how the
        /// course root is oriented.
        void RecordGhost()
        {
            if (built?.Root == null) return;
            const float step = 1f / Ghost.Hz;

            ghostAccum += Time.deltaTime;
            int guard = 0;
            while (ghostAccum >= step && guard++ < 8)
            {
                ghostAccum -= step;
                ghostRec.Add(built.Root.transform.InverseTransformPoint(Marble.transform.position));
            }
        }

        void PlayGhost()
        {
            if (ghostGo == null || ghostView == null) return;
            if (ghostData == null || built?.Root == null) { ghostView.enabled = false; return; }

            if (Ghost.Sample(ghostData, CourseTime, out var local))
            {
                ghostGo.transform.position = built.Root.transform.TransformPoint(local);
                ghostView.enabled = true;
            }
            else ghostView.enabled = false;
        }

        void HandleKeys()
        {
            bool go = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)
                   || Input.GetKeyDown(KeyCode.KeypadEnter);

            if (Input.GetKeyDown(KeyCode.F1)) { editor.Toggle(); return; }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Editing) editor.EscapePressed();
                else Application.Quit();
                return;
            }

            if (Editing) return;          // the editor owns the keyboard while it is open

            if (Input.GetKeyDown(KeyCode.M))
            {
                musicMuted = !musicMuted;
                if (musicSrc != null) musicSrc.mute = musicMuted;
            }

            switch (State)
            {
                case GameState.Title:
                    if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                        TitleSelect = Mathf.Min(TitleSelect + 1, levels.Count - 1);
                    if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                        TitleSelect = Mathf.Max(TitleSelect - 1, 0);
                    if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
                        WantPlayers = WantPlayers == 1 ? 2 : 1;
                    if (go) StartRun();
                    break;
                case GameState.GameOver:
                case GameState.Won:
                    if (go || Input.GetKeyDown(KeyCode.R)) StartRun();
                    break;
                default:
                    if (Input.GetKeyDown(KeyCode.R)) StartRun();
                    break;
            }
        }

        void HandleCapture()
        {
            if (shotDir != null && nextShot < shotTimes.Length && clock >= shotTimes[nextShot])
            {
                string path = Path.Combine(shotDir, $"shot{nextShot + 1}.png");
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log($"[shot] {path} at t={clock:0.0} state={State}");
                nextShot++;
            }

            if (quitAfter > 0f && clock >= quitAfter) Application.Quit();
        }

        void UpdateRollAudio()
        {
            foreach (var p in players)
            {
                if (p.Roll == null || p.Marble == null) continue;
                bool live = State == GameState.Playing && p.Racing && p.Marble.Grounded;
                float sp = live ? Mathf.Clamp01(p.Marble.Speed / p.Marble.MaxSpeed) : 0f;
                p.Roll.volume = Mathf.Lerp(p.Roll.volume, sp * 0.22f, Time.deltaTime * 8f);
                p.Roll.pitch = 0.6f + sp * 0.9f;
            }
        }

        public void RebuildFromEditor(List<Cmd> cmds, int parkLine)
        {
            Level lvl;
            try { lvl = CourseScript.Build(cmds); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[editor] course will not build: {e.Message}");
                return;
            }

            levels[LevelIndex] = lvl;
            ClearBuilt();
            built = LevelBuilder.Build(lvl);

            Vector3 spot = built.SpawnWorld;
            if (parkLine >= 0 && parkLine < lvl.Anchors.Count)
                spot = built.Root.transform.TransformPoint(lvl.Anchors[parkLine] + Vector3.up * 1.2f);

            var p1 = players[0];
            p1.Marble.SetVisible(true);
            p1.Marble.Teleport(spot);
            p1.Marble.Frozen = true;
            Time.timeScale = 1f;
            p1.Rig.Hold = false;
            p1.Rig.Snap();
        }

        /// Leaving the editor drops you in wherever you were last parked.
        public void LeaveEditor()
        {
            foreach (var p in players)
            {
                p.Marble.Frozen = State == GameState.Title;
                p.FallWhistle = false;
                ResyncDemo(p);
            }
        }

        void ClearBuilt()
        {
            if (built?.Root != null) Destroy(built.Root);
            foreach (var e in FindObjectsByType<EnemyBall>(FindObjectsSortMode.None))
                if (e != null) Destroy(e.gameObject);
        }

        /// Themes are synthesised on first use and cached, so switching costs
        /// nothing after the first time a course is reached.
        void PlayTheme(int index)
        {
            if (musicSrc == null || index == currentTheme) return;
            currentTheme = index;
            musicSrc.clip = Music.Theme(index);
            musicSrc.Play();
        }

        void Enter(GameState s)
        {
            if (s != GameState.Dying) Time.timeScale = 1f;
            State = s;
            stateTimer = 0f;
            if (s == GameState.LevelClear || s == GameState.GameOver || s == GameState.Won)
                Debug.Log($"[state] {s} t={clock:0.0} falls={Deaths} clock={TimeLeft:0.0}");
        }

        public void StartRun() => StartRun(TitleSelect);

        public void StartRun(int from)
        {
            if (players.Count != WantPlayers) BuildPlayers(WantPlayers);
            foreach (var p in players)
            {
                p.Deaths = 0;
                p.RunTime = 0f;
                p.Wins = 0;
                p.DeathReason = "";
            }
            startedAt = Mathf.Clamp(from, 0, levels.Count - 1);
            LoadLevel(startedAt, resetClock: true);
            Enter(GameState.Playing);
            Sfx.Play(Sfx.Clip.Start);
        }

        /// Convenience for the single-player paths and the -killat dev flag.
        public void KillMarble(string reason) => Kill(players[0].Marble, reason);

        public void Kill(MarbleController marble, string reason)
        {
            var p = PlayerOf(marble);
            if (p == null || State != GameState.Playing || !p.Racing) return;

            if (built?.Root != null)
            {
                Transform root = built.Root.transform;
                Vector3 cs = root.InverseTransformPoint(p.Marble.transform.position);
                Vector3 lg = root.InverseTransformPoint(p.Marble.LastGrounded);
                Debug.Log($"[death] {reason} p{p.Index + 1} course={LevelIndex + 1} " +
                          $"z={cs.z:0.0} x={cs.x:0.0} y={cs.y:0.0} " +
                          $"lastGround(z={lg.z:0.0} x={lg.x:0.0} y={lg.y:0.0})");
            }

            p.DeathReason = reason;
            p.Deaths++;
            p.TimeLeft = Mathf.Max(0f, p.TimeLeft - DeathPenalty);

            Vector3 at = p.Marble.transform.position;
            p.Marble.Frozen = true;
            p.Marble.SetVisible(false);
            p.Rig.Hold = true;
            p.Rig.Shake(1.4f);

            DeathFx.Burst(at, "Marble", 16, force: 12f);
            Sfx.Play(Sfx.Clip.Shatter);

            switch (reason)
            {
                case "DISSOLVED":
                    Sfx.Play(Sfx.Clip.Sizzle);
                    p.Flash = 0.85f; p.FlashColor = new Color(0.35f, 1f, 0.4f);
                    break;
                case "EATEN":
                    Sfx.Play(Sfx.Clip.Chomp);
                    p.Flash = 0.85f; p.FlashColor = new Color(0.5f, 1f, 0.55f);
                    break;
                case "CRUSHED":
                    Sfx.Play(Sfx.Clip.Thud);
                    p.Flash = 0.9f; p.FlashColor = new Color(1f, 0.55f, 0.3f);
                    break;
                default:
                    if (!p.FallWhistle) Sfx.Play(Sfx.Clip.Fall);
                    p.Flash = 0.9f; p.FlashColor = new Color(1f, 0.45f, 0.35f);
                    break;
            }

            if (players.Count == 1) Time.timeScale = 0.3f;
            p.Dying = true;
            p.DyingTimer = 0f;
        }

        void Respawn(Player p)
        {
            Time.timeScale = 1f;
            p.Dying = false;
            p.FallWhistle = false;
            p.Marble.SetVisible(true);

            if (p.TimeLeft <= 0f)
            {
                p.OutOfTime = true;
                p.Marble.Frozen = true;
                return;
            }

            p.Marble.Teleport(RespawnPointOnCourse(p));
            p.Marble.Frozen = false;

            foreach (var e in built.Enemies) if (e != null) e.ResetToHome();
            p.Rig.Hold = false;
            p.Rig.Snap();
            ResyncDemo(p);
        }

        /// Put the marble back on the course centreline just behind where it was
        /// last on solid ground. Rewinding its own trail could strand it in mid-air
        /// over the spot that killed it, which turned into a death loop.
        Vector3 RespawnPointOnCourse(Player p)
        {
            var path = built?.PathWorld;
            if (path == null || path.Count == 0) return built?.SpawnWorld ?? Vector3.zero;

            Vector3 anchor = p.Marble.LastGrounded;
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < path.Count; i++)
            {
                if (!built.PathSupported[i]) continue;   // never respawn out over a gap
                float d = (path[i] - anchor).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best < 0) return built.SpawnWorld;

            // step back one supported waypoint so there is a little run-up
            for (int i = best - 1; i >= 0; i--)
                if (built.PathSupported[i]) { best = i; break; }

            return path[best] + Vector3.up * 0.4f;
        }

        public void ReachGoal(MarbleController marble)
        {
            var p = PlayerOf(marble);
            if (p == null || State != GameState.Playing || !p.Racing) return;

            p.Finished = true;
            p.FinishTime = p.CourseTime;
            p.Marble.Frozen = true;

            LastCourseTime = p.CourseTime;
            LastMedal = Progress.MedalFor(CurrentLevel, p.CourseTime);

            if (players.Count == 1)
            {
                LastWasBest = Progress.SubmitCourse(CurrentLevel.Name, p.CourseTime, p.Deaths);
                if (LastWasBest) Ghost.Save(CurrentLevel.Name, ghostRec);
            }
            else if (LastWinner == null)
            {
                // first to the pad takes the course; the other is simply late
                LastWinner = p;
                p.Wins++;
                LastWasBest = false;
            }

            Sfx.Play(Sfx.Clip.Goal);
            FreezeAll();
            Enter(GameState.LevelClear);
        }

        void Advance()
        {
            if (LevelIndex + 1 >= levels.Count)
            {
                // a full run only counts if it actually started at the first course
                if (startedAt == 0 && players.Count == 1)
                    LastWasBest = Progress.SubmitRun(RunTime, Deaths);
                Enter(GameState.Won);
                FreezeAll();
                return;
            }

            LoadLevel(LevelIndex + 1, resetClock: false);
            Enter(GameState.Playing);
            Sfx.Play(Sfx.Clip.Start);
        }

        /// 0..1 along the current course, for the HUD progress bar. Measured as
        /// distance along the route rather than course-space Z, because a curve
        /// can turn the course through 90 degrees and stop Z increasing at all.
        public float CourseProgress(Player pl)
        {
            var path = built?.PathWorld;
            if (path == null || path.Count == 0 || pl?.Marble == null) return 0f;

            Vector3 p = pl.Marble.transform.position;
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < path.Count; i++)
            {
                float d = (path[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return built.PathProgress[best];
        }
    }
}
