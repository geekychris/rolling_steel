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
        public MarbleController Marble { get; private set; }
        public float TimeLeft { get; private set; }
        public int LevelIndex { get; private set; }
        public int Deaths { get; private set; }
        public string DeathReason { get; private set; } = "";
        public bool MarbleIsLive => State == GameState.Playing && !Editing;
        public bool Editing => editor != null && editor.Active;
        public float KillY => built?.KillY ?? -200f;
        public Level CurrentLevel => levels[Mathf.Clamp(LevelIndex, 0, levels.Count - 1)];
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
        float lastWarnBeep;
        bool fallWhistle;
        float cineT;                     // position of the title flyover along the course

        /// Screen flash, driven by the HUD. Decays on unscaled time.
        public float Flash { get; private set; }
        public Color FlashColor { get; private set; } = Color.white;

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

            Sfx.Init(gameObject);

            musicSrc = gameObject.AddComponent<AudioSource>();
            musicSrc.loop = true;
            musicSrc.playOnAwake = false;
            musicSrc.spatialBlend = 0f;
            musicSrc.volume = 0.34f;
            musicSrc.mute = musicMuted;

            BuildRig();
            BuildMarble();
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
            var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.11f);
            camGo.AddComponent<AudioListener>();
            isoCam = camGo.AddComponent<IsoCamera>();
            isoCam.Yaw = startYaw;   // 0 puts the 45-degree course on the screen diagonal

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

        void BuildMarble()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Marble";
            go.transform.localScale = Vector3.one * (CourseBuilder.MarbleRadius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = MatLib.Get("Marble");
            go.GetComponent<SphereCollider>().sharedMaterial = MatLib.Marble;

            Marble = go.AddComponent<MarbleController>();
            Marble.Bind(cam);

            rollSrc = go.AddComponent<AudioSource>();
            rollSrc.clip = Sfx.Roll;
            rollSrc.loop = true;
            rollSrc.volume = 0f;
            rollSrc.spatialBlend = 0f;
            rollSrc.Play();

            isoCam.Target = go.transform;
        }

        void LoadLevel(int index, bool resetClock)
        {
            ClearBuilt();
            LevelIndex = index;
            built = LevelBuilder.Build(levels[index]);

            if (resetClock) TimeLeft = 0f;
            TimeLeft += levels[index].TimeBonus;
            Debug.Log($"[level] {index + 1}/{levels.Count} {levels[index].Name} clock={TimeLeft:0.0}");

            PlayTheme(levels[index].MusicTheme);

            Marble.Frozen = false;
            Marble.SetVisible(true);
            fallWhistle = false;
            Time.timeScale = 1f;
            Marble.Teleport(built.SpawnWorld);
            isoCam.Hold = false;
            isoCam.Snap();
            ResyncDemo();
        }

        // ---- loop ----------------------------------------------------------

        void Update()
        {
            clock += Time.unscaledDeltaTime;
            stateTimer += Time.unscaledDeltaTime;
            Flash = Mathf.MoveTowards(Flash, 0f, Time.unscaledDeltaTime * 3.4f);

            HandleKeys();
            HandleCapture();

            if (killAt > 0f && !killTriggered && clock >= killAt && State == GameState.Playing)
            {
                killTriggered = true;
                KillMarble("FELL OFF");
            }

            if (demoMode && Marble != null && !Editing)
            {
                Marble.UseScriptedInput = true;
                Marble.ScriptedInput = DemoInput();
            }

            switch (State)
            {
                case GameState.Playing: if (!Editing) TickPlaying(); break;
                case GameState.Dying:
                    // ease back out of slow motion rather than snapping
                    Time.timeScale = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(stateTimer / DyingHold));
                    if (stateTimer >= DyingHold) Respawn();
                    break;
                case GameState.LevelClear: if (stateTimer >= ClearHold) Advance(); break;
            }

            UpdateCinematic();
            UpdateRollAudio();
        }

        /// Steer along the course centreline, then convert that world-space wish
        /// into camera-relative stick input so the demo works at any view angle.
        Vector2 DemoInput()
        {
            var path = built?.PathWorld;
            if (path == null || path.Count == 0) return Vector2.up;

            Vector3 pos = Marble.transform.position;
            while (demoWp < path.Count - 1)
            {
                Vector3 d = path[demoWp] - pos;
                d.y = 0f;
                if (d.magnitude < 2.0f) demoWp++; else break;
            }

            Vector3 toWp = path[demoWp] - pos;
            toWp.y = 0f;
            if (toWp.sqrMagnitude < 0.0001f) return Vector2.up;

            // Velocity matching rather than full throttle: this brakes into turns
            // and on ice, which is what a human does and what the course expects.
            const float CruiseSpeed = 8.5f;
            Vector3 v = Marble.Body.linearVelocity;
            v.y = 0f;
            Vector3 err = toWp.normalized * CruiseSpeed - v;
            Vector3 wish = err.sqrMagnitude < 0.0001f ? toWp.normalized : err.normalized;

            Vector3 f = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 r = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            return new Vector2(Vector3.Dot(wish, r), Vector3.Dot(wish, f));
        }

        /// Snap the demo driver onto whichever waypoint is nearest right now.
        void ResyncDemo()
        {
            demoWp = 0;
            var path = built?.PathWorld;
            if (path == null || path.Count == 0 || Marble == null) return;

            float best = float.MaxValue;
            Vector3 pos = Marble.transform.position;
            for (int i = 0; i < path.Count; i++)
            {
                float d = (path[i] - pos).sqrMagnitude;
                if (d < best) { best = d; demoWp = i; }
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
                    // drift the focus along the course, so it is a flyover of the
                    // whole thing rather than a turntable of one spot
                    cineT = Mathf.Repeat(cineT + Time.unscaledDeltaTime * 0.045f, 1f);
                    isoCam.CineSpin = 9f;
                    isoCam.CinePitch = 27f;
                    isoCam.CineSize = 20f;
                    isoCam.BeginCinematic(PathPointAt(cineT) + Vector3.up * 5f);
                    isoCam.CineFocus = PathPointAt(cineT) + Vector3.up * 5f;
                    break;

                case GameState.LevelClear:
                case GameState.Won:
                    isoCam.CineSpin = 34f;
                    isoCam.CinePitch = 30f;
                    isoCam.CineSize = 16f;
                    // look a little above the pad, so the pad itself sits below
                    // the banner rather than behind it
                    isoCam.BeginCinematic(built.GoalWorld + Vector3.up * 7f);
                    isoCam.CineFocus = built.GoalWorld + Vector3.up * 7f;
                    break;

                default:
                    if (isoCam.Cinematic) isoCam.EndCinematic();
                    break;
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
            TimeLeft -= Time.deltaTime;

            if (TimeLeft <= 10f && TimeLeft > 0f && clock - lastWarnBeep > 1f)
            {
                lastWarnBeep = clock;
                Sfx.Play(Sfx.Clip.Warn);
            }

            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                Enter(GameState.GameOver);
                Marble.Frozen = true;
                Sfx.Play(Sfx.Clip.Death);
                return;
            }

            // How far it has dropped since it last had contact. Reacting to this
            // rather than an absolute floor means the fall is still on screen when
            // the camera stops following, so the wipeout is actually visible.
            float dropped = Marble.LastGrounded.y - Marble.transform.position.y;
            bool airborne = !Marble.Grounded;

            if (airborne && dropped > 4.5f && !fallWhistle)
            {
                fallWhistle = true;
                Sfx.Play(Sfx.Clip.Fall);
            }
            if (!airborne || dropped < 1f) fallWhistle = false;

            if ((airborne && dropped > 12f) || Marble.transform.position.y < KillY)
                KillMarble("FELL OFF");
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
            if (rollSrc == null) return;
            bool live = State == GameState.Playing && Marble.Grounded;
            float s = live ? Mathf.Clamp01(Marble.Speed / Marble.MaxSpeed) : 0f;
            rollSrc.volume = Mathf.Lerp(rollSrc.volume, s * 0.22f, Time.deltaTime * 8f);
            rollSrc.pitch = 0.6f + s * 0.9f;
        }

        // ---- transitions ----------------------------------------------------

        /// Rebuild the current course from the editor's lines. A course that will
        /// not build is left alone rather than taking the level down, so a
        /// half-finished edit never strands you.
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

            Marble.SetVisible(true);
            Marble.Teleport(spot);
            Marble.Frozen = true;
            Time.timeScale = 1f;
            isoCam.Hold = false;
            isoCam.Snap();
        }

        /// Leaving the editor drops you in wherever you were last parked.
        public void LeaveEditor()
        {
            Marble.Frozen = State == GameState.Title;
            fallWhistle = false;
            ResyncDemo();
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

        public void StartRun()
        {
            Deaths = 0;
            DeathReason = "";
            LoadLevel(0, resetClock: true);
            Enter(GameState.Playing);
            Sfx.Play(Sfx.Clip.Start);
        }

        public void KillMarble(string reason)
        {
            if (State != GameState.Playing) return;

            if (built?.Root != null)
            {
                Vector3 cs = built.Root.transform.InverseTransformPoint(Marble.transform.position);
                Vector3 lg = built.Root.transform.InverseTransformPoint(Marble.LastGrounded);
                Debug.Log($"[death] {reason} course={LevelIndex + 1} z={cs.z:0.0} x={cs.x:0.0} y={cs.y:0.0} " +
                          $"lastGround(z={lg.z:0.0} x={lg.x:0.0} y={lg.y:0.0})");
            }
            DeathReason = reason;
            Deaths++;
            TimeLeft = Mathf.Max(0f, TimeLeft - DeathPenalty);

            Vector3 at = Marble.transform.position;
            Marble.Frozen = true;
            Marble.SetVisible(false);
            isoCam.Hold = true;
            isoCam.Shake(1.4f);

            DeathFx.Burst(at, "Marble", 16, force: 12f);
            Sfx.Play(Sfx.Clip.Shatter);

            switch (reason)
            {
                case "DISSOLVED":
                    Sfx.Play(Sfx.Clip.Sizzle);
                    Flash = 0.85f; FlashColor = new Color(0.35f, 1f, 0.4f);
                    break;
                case "EATEN":
                    Sfx.Play(Sfx.Clip.Chomp);
                    Flash = 0.85f; FlashColor = new Color(0.5f, 1f, 0.55f);
                    break;
                default:
                    if (!fallWhistle) Sfx.Play(Sfx.Clip.Fall);
                    Flash = 0.9f; FlashColor = new Color(1f, 0.45f, 0.35f);
                    break;
            }

            // a beat of slow motion so the debris reads before the respawn
            Time.timeScale = 0.3f;
            Enter(GameState.Dying);
        }

        void Respawn()
        {
            Time.timeScale = 1f;
            fallWhistle = false;
            Marble.SetVisible(true);

            if (TimeLeft <= 0f)
            {
                Enter(GameState.GameOver);
                return;
            }

            Marble.Teleport(RespawnPointOnCourse());
            Marble.Frozen = false;

            foreach (var e in built.Enemies) if (e != null) e.ResetToHome();
            isoCam.Hold = false;
            isoCam.Snap();
            ResyncDemo();
            Enter(GameState.Playing);
        }

        /// Put the marble back on the course centreline just behind where it was
        /// last on solid ground. Rewinding its own trail could strand it in mid-air
        /// over the spot that killed it, which turned into a death loop.
        Vector3 RespawnPointOnCourse()
        {
            var path = built?.PathWorld;
            if (path == null || path.Count == 0) return built?.SpawnWorld ?? Vector3.zero;

            Vector3 anchor = Marble.LastGrounded;
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

        public void ReachGoal()
        {
            if (State != GameState.Playing) return;
            Marble.Frozen = true;
            Enter(GameState.LevelClear);
            Sfx.Play(Sfx.Clip.Goal);
        }

        void Advance()
        {
            if (LevelIndex + 1 >= levels.Count)
            {
                Enter(GameState.Won);
                Marble.Frozen = true;
                return;
            }

            LoadLevel(LevelIndex + 1, resetClock: false);
            Enter(GameState.Playing);
            Sfx.Play(Sfx.Clip.Start);
        }

        /// 0..1 along the current course, for the HUD progress bar. Measured as
        /// distance along the route rather than course-space Z, because a curve
        /// can turn the course through 90 degrees and stop Z increasing at all.
        public float Progress()
        {
            var path = built?.PathWorld;
            if (path == null || path.Count == 0 || Marble == null) return 0f;

            Vector3 p = Marble.transform.position;
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
