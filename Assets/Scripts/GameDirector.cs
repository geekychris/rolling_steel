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
        const float DyingHold = 0.9f;
        const float ClearHold = 2.2f;

        public GameState State { get; private set; } = GameState.Title;
        public MarbleController Marble { get; private set; }
        public float TimeLeft { get; private set; }
        public int LevelIndex { get; private set; }
        public int Deaths { get; private set; }
        public string DeathReason { get; private set; } = "";
        public bool MarbleIsLive => State == GameState.Playing;
        public float KillY => built?.KillY ?? -200f;
        public Level CurrentLevel => levels[Mathf.Clamp(LevelIndex, 0, levels.Count - 1)];
        public int LevelCount => levels.Count;

        List<Level> levels;
        BuiltLevel built;
        Camera cam;
        IsoCamera isoCam;
        AudioSource rollSrc;
        float stateTimer;
        float lastWarnBeep;

        // ---- headless capture / demo hooks --------------------------------
        bool demoMode, autoStart;
        float startYaw;
        string shotDir;
        float quitAfter = -1f;
        readonly float[] shotTimes = { 1f, 6f, 11f, 20f, 27f, 33f, 45f, 54f, 62f, 67.5f };
        int nextShot;
        float clock;
        int demoWp;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;

            levels = CourseLibrary.All();
            ParseArgs();

            Sfx.Init(gameObject);
            BuildRig();
            BuildMarble();
            gameObject.AddComponent<Hud>();

            // Show the first course behind the title card rather than an empty void.
            LoadLevel(0, resetClock: true);
            if (autoStart) { State = GameState.Playing; Sfx.Play(Sfx.Clip.Start); }
            else { State = GameState.Title; Marble.Frozen = true; }
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
            if (built?.Root != null) Destroy(built.Root);
            foreach (var e in FindObjectsByType<EnemyBall>(FindObjectsSortMode.None))
                if (e != null) Destroy(e.gameObject);

            LevelIndex = index;
            built = LevelBuilder.Build(levels[index]);

            if (resetClock) TimeLeft = 0f;
            TimeLeft += levels[index].TimeBonus;
            Debug.Log($"[level] {index + 1}/{levels.Count} {levels[index].Name} clock={TimeLeft:0.0}");

            Marble.Frozen = false;
            Marble.Teleport(built.SpawnWorld);
            isoCam.Hold = false;
            isoCam.Snap();
            ResyncDemo();
        }

        // ---- loop ----------------------------------------------------------

        void Update()
        {
            clock += Time.deltaTime;
            stateTimer += Time.deltaTime;

            HandleKeys();
            HandleCapture();

            if (demoMode && Marble != null)
            {
                Marble.UseScriptedInput = true;
                Marble.ScriptedInput = DemoInput();
            }

            switch (State)
            {
                case GameState.Playing: TickPlaying(); break;
                case GameState.Dying: if (stateTimer >= DyingHold) Respawn(); break;
                case GameState.LevelClear: if (stateTimer >= ClearHold) Advance(); break;
            }

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

            if (Marble.transform.position.y < KillY) KillMarble("FELL OFF");
        }

        void HandleKeys()
        {
            bool go = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)
                   || Input.GetKeyDown(KeyCode.KeypadEnter);

            if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();

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

        void Enter(GameState s)
        {
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
            Marble.Frozen = true;
            isoCam.Hold = true;
            Enter(GameState.Dying);
            Sfx.Play(Sfx.Clip.Death);
        }

        void Respawn()
        {
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
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < path.Count; i++)
            {
                float d = (path[i] - anchor).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }

            return path[Mathf.Max(0, best - 1)] + Vector3.up * 0.4f;
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

        /// 0..1 along the current course, for the HUD progress bar.
        public float Progress()
        {
            if (built?.Root == null || Marble == null) return 0f;
            float z = built.Root.transform.InverseTransformPoint(Marble.transform.position).z;
            return Mathf.Clamp01(Mathf.InverseLerp(built.StartZ, built.GoalZ, z));
        }
    }
}
