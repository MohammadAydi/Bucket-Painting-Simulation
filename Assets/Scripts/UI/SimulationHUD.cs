
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using onlyone;

public class SimulationHUD : MonoBehaviour
{
    [Header("Toggle")]
    public KeyCode toggleKey = KeyCode.F2;
    public bool startVisible = false;

    [Header("Layout")]
    public float panelWidth = 380f;

    enum Tab { Scenes, Fluid, Pendulum, Bucket, Object }
    Tab _tab = Tab.Scenes;
    bool _visible;
    Vector2 _scroll;
    Rect _windowRect;

    // ── auto-discovered scene-relevant refs (re-fetched on every scene load) ──
    FluidManager3D        _fluidManager;
    ParticleSettings      _particleSettings;
    PigmentSettings       _pigmentSettings;
    FluidRendererSettings _rendererSettings;

    SphericalPendulum _pendulum;
    PbdRope           _rope;
    RopeConfigLoader  _ropeLoader;
    BucketManualController _bucketController;

    // 25002500 Bucket refs 250025002500250025002500250025002500250025002500250025002500250025002500250025002500250025002500250025002500250025002500250025002500250025002500250025002500250025002500250025002500250025002500
    BucketGenerator        _bucketGenerator;
    BucketHandleGenerator  _bucketHandle;
    BucketHoleCutter       _bucketHoleCutter;
    BucketHoleShaderFeeder _bucketShaderFeeder;
    int _editingHoleIndex = -1;

    // ── draggable box / object gizmo ────────────────────────────────────────
    Transform _boxTarget;
    List<Transform> _boxCandidates = new List<Transform>();
    enum GizmoAxis { None, X, Y, Z }
    GizmoAxis _dragAxis = GizmoAxis.None;
    Vector3 _dragAxisWorldDir;
    float   _dragPixelsPerUnit;
    const float GizmoHandleLen = 1.0f;   // world units from pivot to handle
    const float GizmoHandlePx  = 9f;     // handle square half-size on screen

    List<string> _sceneNames = new List<string>();
    string _statusMsg = "";
    float  _statusMsgTimer;

    // GUIStyles built lazily inside OnGUI (skin isn't ready in Awake)
    GUIStyle _headerStyle, _tabStyle, _tabActiveStyle, _boxStyle, _sliderLabelStyle, _smallBtn;
    bool _stylesBuilt;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        var existing = FindObjectsByType<SimulationHUD>(FindObjectsSortMode.None);
        if (existing.Length > 1) { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);

        _visible = startVisible;
        CacheSceneNames();
        SceneManager.sceneLoaded += (scene, mode) => { RefreshReferences(); };
        RefreshReferences();
    }

    void CacheSceneNames()
    {
        _sceneNames.Clear();
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrEmpty(name)) _sceneNames.Add(name);
        }
    }

    void RefreshReferences()
    {
        _fluidManager     = FindFirstObjectByType<FluidManager3D>();
        _particleSettings = _fluidManager ? _fluidManager.SimSettings   : FindFirstObjectByType<ParticleSettings>();
        _pigmentSettings  = _fluidManager ? _fluidManager.PigmentSettings : null;
        _rendererSettings = FindFirstObjectByType<FluidRendererSettings>();

        _pendulum   = FindFirstObjectByType<SphericalPendulum>();
        _rope       = FindFirstObjectByType<PbdRope>();
        _ropeLoader = FindFirstObjectByType<RopeConfigLoader>();
        _bucketController   = FindFirstObjectByType<BucketManualController>();
        _bucketGenerator    = FindFirstObjectByType<BucketGenerator>();
        _bucketHandle       = FindFirstObjectByType<BucketHandleGenerator>();
        _bucketHoleCutter   = FindFirstObjectByType<BucketHoleCutter>();
        _bucketShaderFeeder = FindFirstObjectByType<BucketHoleShaderFeeder>();
        _editingHoleIndex   = -1;

        // Find likely "box" objects: anything named like "box", plus the fluid
        // boundary volume (the container box for the simulation), if present.
        _boxCandidates.Clear();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name.ToLowerInvariant().Contains("box"))
                _boxCandidates.Add(t);
        }
        var boundary = FindFirstObjectByType<FluidBoundary3D>();
        if (boundary != null && !_boxCandidates.Contains(boundary.transform))
            _boxCandidates.Add(boundary.transform);

        if (_boxTarget == null || !_boxCandidates.Contains(_boxTarget))
            _boxTarget = _boxCandidates.Count > 0 ? _boxCandidates[0] : null;
        _dragAxis = GizmoAxis.None;

        // Pick a sensible default tab for the scene we just landed in.
        if (_fluidManager != null || _particleSettings != null) _tab = Tab.Fluid;
        else if (_pendulum != null || _rope != null)             _tab = Tab.Pendulum;
        else                                                     _tab = Tab.Scenes;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) _visible = !_visible;
        if (_statusMsgTimer > 0f)
        {
            _statusMsgTimer -= Time.unscaledDeltaTime;
            if (_statusMsgTimer <= 0f) _statusMsg = "";
        }

        UpdateGizmoDrag();
    }

    // ── runtime "move like in the Unity editor" axis gizmo ─────────────────────
    void UpdateGizmoDrag()
    {
        if (_boxTarget == null || _tab != Tab.Object || !_visible) return;
        var cam = Camera.main;
        if (cam == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mouseGui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            foreach (var axis in new[] { GizmoAxis.X, GizmoAxis.Y, GizmoAxis.Z })
            {
                Vector3 dir = AxisDir(axis);
                Vector3 tipWorld = _boxTarget.position + dir * GizmoHandleLen;
                Vector3 tipScreen = cam.WorldToScreenPoint(tipWorld);
                if (tipScreen.z < 0f) continue; // behind camera
                Vector2 tipGui = new Vector2(tipScreen.x, Screen.height - tipScreen.y);
                if (Vector2.Distance(mouseGui, tipGui) <= GizmoHandlePx + 4f)
                {
                    // Precompute screen-space pixels-per-world-unit along this axis.
                    Vector3 originScreen = cam.WorldToScreenPoint(_boxTarget.position);
                    Vector2 screenAxisVec = new Vector2(tipScreen.x - originScreen.x, tipScreen.y - originScreen.y);
                    float len = screenAxisVec.magnitude;
                    if (len < 0.001f) continue;

                    _dragAxis = axis;
                    _dragAxisWorldDir = dir;
                    _dragPixelsPerUnit = len / GizmoHandleLen;
                    _lastGizmoMouseGui = mouseGui;
                    break;
                }
            }
        }

        if (_dragAxis != GizmoAxis.None && Input.GetMouseButton(0))
        {
            Vector2 mouseGui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            Vector2 delta = mouseGui - _lastGizmoMouseGui;

            Vector3 originScreen = cam.WorldToScreenPoint(_boxTarget.position);
            Vector3 tipScreen = cam.WorldToScreenPoint(_boxTarget.position + _dragAxisWorldDir * GizmoHandleLen);
            Vector2 screenAxisDir = (new Vector2(tipScreen.x, Screen.height - tipScreen.y)
                                    - new Vector2(originScreen.x, Screen.height - originScreen.y)).normalized;

            float projected = Vector2.Dot(delta, screenAxisDir);
            float worldMove = projected / Mathf.Max(_dragPixelsPerUnit, 0.001f);
            _boxTarget.position += _dragAxisWorldDir * worldMove;
        }

        if (Input.GetMouseButtonUp(0)) _dragAxis = GizmoAxis.None;

        _lastGizmoMouseGui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
    }

    Vector2 _lastGizmoMouseGui;

    static Vector3 AxisDir(GizmoAxis a) => a switch
    {
        GizmoAxis.X => Vector3.right,
        GizmoAxis.Y => Vector3.up,
        GizmoAxis.Z => Vector3.forward,
        _ => Vector3.zero
    };

    void ShowStatus(string msg)
    {
        _statusMsg = msg;
        _statusMsgTimer = 2f;
    }

    // ── OnGUI ────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        BuildStyles();

        // Small always-visible hint so users know the HUD exists.
        GUI.Label(new Rect(8, 8, 260, 20), $"[{toggleKey}] toggle settings HUD", _sliderLabelStyle);

        // Always-visible Reload button — reloads the active scene so any
        // settings that require a restart take effect immediately.
        if (GUI.Button(new Rect(8, 30, 80, 22), "⟳ Reload"))
            ReloadCurrentScene();

        if (!_visible) return;

        float h = Mathf.Min(Screen.height - 20, 720);
        _windowRect = new Rect(Screen.width - panelWidth - 10, 10, panelWidth, h);
        _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "");

        if (_tab == Tab.Object && _boxTarget != null) DrawGizmoHandles();
    }

    void DrawGizmoHandles()
    {
        var cam = Camera.main;
        if (cam == null) return;

        DrawHandle(cam, GizmoAxis.X, new Color(1f, 0.25f, 0.25f));
        DrawHandle(cam, GizmoAxis.Y, new Color(0.3f, 1f, 0.3f));
        DrawHandle(cam, GizmoAxis.Z, new Color(0.3f, 0.55f, 1f));
    }

    void DrawHandle(Camera cam, GizmoAxis axis, Color color)
    {
        Vector3 origin = _boxTarget.position;
        Vector3 dir = AxisDir(axis);
        Vector3 tipWorld = origin + dir * GizmoHandleLen;

        Vector3 originScreen = cam.WorldToScreenPoint(origin);
        Vector3 tipScreen    = cam.WorldToScreenPoint(tipWorld);
        if (tipScreen.z < 0f || originScreen.z < 0f) return;

        Vector2 o = new Vector2(originScreen.x, Screen.height - originScreen.y);
        Vector2 t = new Vector2(tipScreen.x, Screen.height - tipScreen.y);

        var prevColor = GUI.color;
        GUI.color = color;
        DrawLine(o, t, 3f);
        GUI.color = (_dragAxis == axis) ? Color.yellow : color;
        GUI.Box(new Rect(t.x - GizmoHandlePx, t.y - GizmoHandlePx, GizmoHandlePx * 2, GizmoHandlePx * 2), GUIContent.none);
        GUI.color = prevColor;
    }

    static Texture2D _lineTex;
    static void DrawLine(Vector2 a, Vector2 b, float width)
    {
        if (_lineTex == null) { _lineTex = new Texture2D(1, 1); _lineTex.SetPixel(0, 0, Color.white); _lineTex.Apply(); }
        Vector2 d = b - a;
        float len = d.magnitude;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

        var matrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(ang, a);
        GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, len, width), _lineTex);
        GUI.matrix = matrix;
    }

    void DrawWindow(int id)
    {
        GUILayout.BeginVertical();

        // Title bar
        GUILayout.BeginHorizontal();
        GUILayout.Label("Simulation Control", _headerStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("✕", GUILayout.Width(24))) _visible = false;
        GUILayout.EndHorizontal();

        // Tabs
        GUILayout.BeginHorizontal();
        DrawTabButton(Tab.Scenes,   "Scenes");
        DrawTabButton(Tab.Fluid,    "Fluid");
        DrawTabButton(Tab.Pendulum, "Pendulum");
        DrawTabButton(Tab.Bucket, "Bucket");
        DrawTabButton(Tab.Object, "Box");
        GUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(_statusMsg))
            GUILayout.Label(_statusMsg, _sliderLabelStyle);

        _scroll = GUILayout.BeginScrollView(_scroll);

        switch (_tab)
        {
            case Tab.Scenes:   DrawScenesTab();   break;
            case Tab.Fluid:    DrawFluidTab();    break;
            case Tab.Pendulum: DrawPendulumTab(); break;
            case Tab.Bucket: DrawBucketTab(); break;
            case Tab.Object:   DrawObjectTab();   break;
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        // Draggable by the title bar area
        GUI.DragWindow(new Rect(0, 0, panelWidth, 30));
    }

    void DrawTabButton(Tab t, string label)
    {
        bool active = _tab == t;
        if (GUILayout.Toggle(active, label, active ? _tabActiveStyle : _tabStyle, GUILayout.Height(26)))
            _tab = t;
    }

    // ── Scenes tab ───────────────────────────────────────────────────────────
    void DrawScenesTab()
    {
        GUILayout.Space(4);
        GUILayout.Label("Loaded scene: " + SceneManager.GetActiveScene().name, _headerStyle);
        GUILayout.Space(4);

        // ── Reload button ────────────────────────────────────────────────────
        GUILayout.Label(
            "Some settings (particle count, rope segments, etc.) only take effect\n" +
            "after a scene reload. Use the button below or the ⟳ Reload shortcut\n" +
            "at the top-left of the screen to restart the current scene.",
            _sliderLabelStyle);
        GUILayout.Space(4);
        if (GUILayout.Button("⟳  Reload Current Scene", GUILayout.Height(34)))
            ReloadCurrentScene();
        GUILayout.Space(8);

        if (_sceneNames.Count == 0)
        {
            GUILayout.Label(
                "No scenes found in Build Settings.\n" +
                "Add scenes via File > Build Profiles > Scene List.", _sliderLabelStyle);
            return;
        }

        Header("Switch Scene");
        foreach (var sceneName in _sceneNames)
        {
            bool isCurrent = sceneName == SceneManager.GetActiveScene().name;
            GUI.enabled = !isCurrent;
            if (GUILayout.Button(isCurrent ? $"● {sceneName} (current)" : sceneName, GUILayout.Height(30)))
            {
                SceneManager.LoadScene(sceneName);
            }
            GUI.enabled = true;
        }
    }

    void ReloadCurrentScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ── Fluid tab ────────────────────────────────────────────────────────────
    void DrawFluidTab()
    {
        if (_fluidManager == null && _particleSettings == null && _pigmentSettings == null && _rendererSettings == null)
        {
            GUILayout.Label("No fluid simulation objects found in this scene.", _sliderLabelStyle);
            return;
        }

        if (_fluidManager != null)
        {
            Header("Simulation Time");
            _fluidManager.normalTimeScale = Slider("Time Scale", _fluidManager.normalTimeScale, 0f, 3f);
            _fluidManager.slowTimeScale   = Slider("Slow Scale", _fluidManager.slowTimeScale, 0f, 1f);
            _fluidManager.inSlowMode      = Toggle("Slow Mode", _fluidManager.inSlowMode);
        }

        if (_particleSettings != null)
        {
            var s = _particleSettings;

            Header("Count & Shape (changing these rebuilds the sim)");
            s.particleCount    = (int)Slider("Particle Count", s.particleCount, 2, 1000000);
            s.radius            = Slider("Radius", s.radius, 0.001f, 1f);
            s.segments          = (int)Slider("Segments", s.segments, 3, 32);
            s.sphereResolution  = (int)Slider("Sphere Resolution", s.sphereResolution, 0, 4);
            s.particleSpacing   = Slider("Particle Spacing", s.particleSpacing, 0.0001f, 10f);

           
           
            Header("Particle Physics");
            s.gravity                   = Slider("Gravity", s.gravity, -20f, 20f);
            s.pressureMultiplier        = Slider("Pressure", s.pressureMultiplier, 0f, 1000f);
            s.targetDensity             = Slider("Target Density", s.targetDensity, 0f, 1000f);
            s.nearPressureMultiplier    = Slider("Near Pressure", s.nearPressureMultiplier, 0f, 1000f);
            s.viscosityCoeff            = Slider("Viscosity", s.viscosityCoeff, 0f, 1000f);
            s.surfaceTensionCoeff       = Slider("Surface Tension", s.surfaceTensionCoeff, 0f, 100f);
            s.surfaceTensionThreshold   = Slider("Tension Threshold", s.surfaceTensionThreshold, 0f, 2f);
            s.collisionDamping          = Slider("Collision Damping", s.collisionDamping, 0f, 1f);
            s.smoothingRadius           = Slider("Smoothing Radius", s.smoothingRadius, 0.01f, 4f);
            s.mass                      = Slider("Particle Mass", s.mass, 0f, 20f);

            Header("Solver Methods");
            s.pressureSolverMethod      = EnumButton("Pressure Solver", s.pressureSolverMethod);
          
            Header("Visualization");
          
            // Push every frame the tab is open — FluidManager3D.OnSettingsChanged() diffs
            // against cached values internally, so this is cheap and guarantees every
            // slider/toggle edit actually reaches the simulation (relying on GUI.changed
            // here was unreliable across multiple controls in the same OnGUI pass).
            s.NotifyChanged();
        }

        if (_pigmentSettings != null)
        {
            var p = _pigmentSettings;
            Header("Pigment");
            p.diffusionCoeff = Slider("Diffusion", p.diffusionCoeff, 0f, 2f);
            p.mixingModel    = EnumButton("Mixing Model", p.mixingModel);

            Header($"Spawn Colors ({(p.spawnColors?.Length ?? 0)})");
            GUILayout.Label(
                "One color per particle group — the spawner assigns these to particles at spawn time.",
                _sliderLabelStyle);

            if (p.spawnColors == null) p.spawnColors = new Color[0];
            for (int i = 0; i < p.spawnColors.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Group {i}", _sliderLabelStyle, GUILayout.Width(60));
                DrawSwatch(p.spawnColors[i]);
                GUILayout.EndHorizontal();
                p.spawnColors[i] = ColorField(null, p.spawnColors[i]);
                if (GUILayout.Button("Remove", GUILayout.Height(20)))
                {
                    var list = new List<Color>(p.spawnColors);
                    list.RemoveAt(i);
                    p.spawnColors = list.ToArray();
                    p.NotifyChanged();
                    break; // list changed size — bail out of this frame's loop
                }
                GUILayout.Space(4);
            }
            if (GUILayout.Button("+ Add Color", GUILayout.Height(24)))
            {
                var list = new List<Color>(p.spawnColors) { Color.white };
                p.spawnColors = list.ToArray();
                p.NotifyChanged();
            }

            p.NotifyChanged();
        }

        if (_rendererSettings != null)
        {
            var r = _rendererSettings;
            Header("Fluid Renderer");
            r.renderMode          = EnumButton("Render Mode", r.renderMode);
            r.blurType             = EnumButton("Blur Type", r.blurType);
            r.depthParticleSize   = Slider("Depth Particle Size", r.depthParticleSize, 0.01f, 1f);
            r.specularStrength    = Slider("Specular Strength", r.specularStrength, 0f, 5f);
            r.specularShininess   = Slider("Specular Shininess", r.specularShininess, 1f, 256f);
            r.reflectionStrength  = Slider("Reflection", r.reflectionStrength, 0f, 1f);
            r.ambientStrength     = Slider("Ambient", r.ambientStrength, 0f, 1f);
            r.useHalfLambert      = Toggle("Half Lambert", r.useHalfLambert);
            r.fillLightStrength   = Slider("Fill Light", r.fillLightStrength, 0f, 1f);

            var bs = r.bilateralSettings;
            Header("Bilateral Blur");
            bs.worldRadius        = Slider("World Radius", bs.worldRadius, 0.01f, 1f);
            bs.strength           = Slider("Strength", bs.strength, 0f, 1f);
            bs.diffStrength       = Slider("Diff Strength", bs.diffStrength, 0f, 50f);
            bs.maxScreenSpaceSize = (int)Slider("Max Screen Space", bs.maxScreenSpaceSize, 1, 128);
            bs.iterations         = (int)Slider("Iterations", bs.iterations, 1, 20);
            r.bilateralSettings = bs;
        }
    }

    // ── Pendulum tab ─────────────────────────────────────────────────────────
    void DrawPendulumTab()
    {
        if (_pendulum == null && _rope == null)
        {
            GUILayout.Label("No pendulum/rope objects found in this scene.", _sliderLabelStyle);
            return;
        }

        if (_ropeLoader != null)
        {
            Header("Presets");
            var names = _ropeLoader.GetConfigNames();
            if (names.Length == 0)
            {
                GUILayout.Label("No config presets assigned on RopeConfigLoader.", _sliderLabelStyle);
            }
            else
            {
                for (int i = 0; i < names.Length; i++)
                {
                    if (GUILayout.Button(names[i], GUILayout.Height(24)))
                    {
                        _ropeLoader.Apply(i);
                        ShowStatus($"Applied preset: {names[i]}");
                    }
                }
            }
        }

        if (_bucketController != null)
        {
            Header("Bucket Manual Control (WASD)");
            bool active = _bucketController.IsManualModeActive;
            GUILayout.Label(active
                ? "ACTIVE — WASD/QE currently move the bucket, not the camera."
                : "Inactive — WASD is free for your camera controller.", _sliderLabelStyle);

            if (active && GUILayout.Button("Exit Manual Mode (release WASD)", GUILayout.Height(26)))
            {
                _bucketController.ForceExitManualMode();
                ShowStatus("Exited manual bucket mode — WASD released.");
            }

            bool keyboard = _bucketController.UsesKeyboardInput;
            bool newKeyboard = GUILayout.Toggle(keyboard, " Keyboard drives the bucket (uncheck to free WASD for camera, uses mouse-drag instead)");
            if (newKeyboard != keyboard) _bucketController.UsesKeyboardInput = newKeyboard;

            GUILayout.Label(
                "Tip: this scene may not have a camera controller attached. " +
                "Add FluidCameraController to your camera for orbit/fly controls " +
                "(Alt+drag / RMB+WASD) that won't conflict with the bucket.",
                _sliderLabelStyle);
        }

        if (_rope != null)
        {
            Header("Rope — Forces & Solver (live)");
            _rope.LiveGravity          = Slider("Gravity", _rope.LiveGravity, 0f, 30f);
            _rope.LiveCompliance       = Slider("Compliance", _rope.LiveCompliance, 0f, 0.001f);
            _rope.LiveInternalDamping  = Slider("Internal Damping", _rope.LiveInternalDamping, 0f, 2f);
            _rope.LiveEnableBending    = Toggle("Enable Bending", _rope.LiveEnableBending);
            _rope.LiveBendingStiffness = Slider("Bending Stiffness", _rope.LiveBendingStiffness, 0f, 2f);

            Header("Rope — Torsion (live)");
            _rope.LiveEnableTorsion      = Toggle("Enable Torsion", _rope.LiveEnableTorsion);
            _rope.LiveTorsionalStiffness = Slider("Torsional Stiffness", _rope.LiveTorsionalStiffness, 0f, 200f);
            _rope.LiveDampingRatio       = Slider("Damping Ratio", _rope.LiveDampingRatio, 0f, 1f);
            _rope.LiveMaxTwistRate       = Slider("Max Twist Rate", _rope.LiveMaxTwistRate, 1f, 2000f);

            Header("Bucket (live)");
            _rope.LiveDynamicBucket = Toggle("Dynamic Bucket", _rope.LiveDynamicBucket);
            _rope.LiveBucketMass    = Slider("Bucket Mass", _rope.LiveBucketMass, 0.01f, 20f);
            _rope.LiveAirDensity    = Slider("Air Density", _rope.LiveAirDensity, 0f, 5f);
            _rope.LiveBucketDrag    = Slider("Bucket Drag Coeff", _rope.LiveBucketDrag, 0f, 3f);

            Header("Rope — Structure (rebuild required)");
            _rope.StructSegments             = (int)Slider("Segments", _rope.StructSegments, 2, 80);
            _rope.StructRopeLength           = Slider("Rope Length", _rope.StructRopeLength, 0.1f, 5f);
            _rope.StructRopeWidth            = Slider("Rope Width", _rope.StructRopeWidth, 0.0005f, 0.05f);
            _rope.StructSubsteps             = (int)Slider("Substeps", _rope.StructSubsteps, 1, 8);
            _rope.StructConstraintIterations = (int)Slider("Constraint Iterations", _rope.StructConstraintIterations, 1, 80);
            _rope.StructTorsionIterations    = (int)Slider("Torsion Iterations", _rope.StructTorsionIterations, 1, 40);

            if (GUILayout.Button("Rebuild Rope", GUILayout.Height(28)))
            {
                _rope.RebuildRope();
                ShowStatus("Rope rebuilt.");
            }
        }
        else if (_pendulum != null)
        {
            // Standalone pendulum (no rope) — RK4 mode.
            Header("Pendulum (standalone, live)");
            _pendulum.LiveGravity         = Slider("Gravity", _pendulum.LiveGravity, 0f, 30f);
            _pendulum.LiveAirDensity      = Slider("Air Density", _pendulum.LiveAirDensity, 0f, 5f);
            _pendulum.LivePivotFriction   = Slider("Pivot Friction", _pendulum.LivePivotFriction, 0f, 2f);
            _pendulum.LiveMass            = Slider("Bucket Mass", _pendulum.LiveMass, 0.01f, 20f);
            _pendulum.LiveBucketRadius    = Slider("Bucket Radius", _pendulum.LiveBucketRadius, 0.01f, 2f);
            _pendulum.LiveDragCoefficient = Slider("Drag Coefficient", _pendulum.LiveDragCoefficient, 0f, 3f);
            _pendulum.LiveLength          = Slider("Rope Length", _pendulum.LiveLength, 0.1f, 5f);

            if (GUILayout.Button("Relaunch", GUILayout.Height(28)))
            {
                _pendulum.Relaunch();
                ShowStatus("Pendulum relaunched.");
            }
        }
    }

    // ── Object/Box tab ───────────────────────────────────────────────────────
    void DrawObjectTab()
    {
        if (_boxCandidates.Count == 0)
        {
            GUILayout.Label(
                "No object named \"box\" (or FluidBoundary3D) found in this scene.\n" +
                "Rename your box GameObject to include \"box\", or add a FluidBoundary3D component to it.",
                _sliderLabelStyle);
            return;
        }

        Header("Target Object");
        if (_boxCandidates.Count > 1)
        {
            foreach (var t in _boxCandidates)
            {
                bool active = t == _boxTarget;
                if (GUILayout.Toggle(active, t.name, active ? _tabActiveStyle : _tabStyle, GUILayout.Height(22)))
                    _boxTarget = t;
            }
        }
        else
        {
            GUILayout.Label(_boxTarget.name, _headerStyle);
        }

        if (_boxTarget == null) return;

        GUILayout.Space(4);
        GUILayout.Label(
            "Drag the colored handles in the 3D view to move the box (like the " +
            "Unity move gizmo), or edit the values below directly.", _sliderLabelStyle);

        Header("Position");
        var pos = _boxTarget.position;
        pos.x = Slider("X", pos.x, -20f, 20f);
        pos.y = Slider("Y", pos.y, -20f, 20f);
        pos.z = Slider("Z", pos.z, -20f, 20f);
        _boxTarget.position = pos;

        Header("Rotation (Euler)");
        var rot = _boxTarget.eulerAngles;
        rot.x = Slider("X", rot.x, 0f, 360f);
        rot.y = Slider("Y", rot.y, 0f, 360f);
        rot.z = Slider("Z", rot.z, 0f, 360f);
        _boxTarget.eulerAngles = rot;

        Header("Scale");
        var scl = _boxTarget.localScale;
        bool uniform = GUILayout.Toggle(_uniformScale, " Uniform Scale");
        _uniformScale = uniform;
        if (uniform)
        {
            float s = Slider("Scale", scl.x, 0.01f, 10f);
            scl = new Vector3(s, s, s);
        }
        else
        {
            scl.x = Slider("X", scl.x, 0.01f, 10f);
            scl.y = Slider("Y", scl.y, 0.01f, 10f);
            scl.z = Slider("Z", scl.z, 0.01f, 10f);
        }
        _boxTarget.localScale = scl;

        GUILayout.Space(6);
        if (GUILayout.Button("Reset Rotation", GUILayout.Height(24))) _boxTarget.eulerAngles = Vector3.zero;
    }


    // ── Bucket tab ───────────────────────────────────────────────────────────
    // All physical dimensions are in metres (Unity world units), matching the
    // values stored directly on BucketGenerator / BucketHandleGenerator.
    void DrawBucketTab()
    {
        if (_bucketGenerator == null && _bucketHoleCutter == null)
        {
            GUILayout.Label("No BucketGenerator found in this scene.", _sliderLabelStyle);
            return;
        }

        bool needsRebuild = false;

        // ── BucketGenerator ──────────────────────────────────────────────────
        if (_bucketGenerator != null)
        {
            var b = _bucketGenerator;

            Header("Bucket Dimensions");
            float newTopR    = Slider("Top Radius",    b.topRadius,    1f, 50f);
            float newBotR    = Slider("Bottom Radius", b.bottomRadius, 1f, 50f);
            float newH       = Slider("Height",        b.height,       1f, 50f);
            float newThick   = Slider("Thickness",     b.thickness,    0.1f, 5f);

            Header("Mesh Quality");
            int newSegs      = (int)Slider("Segments",            b.segments,           8,  128);
            int newHeightSub = (int)Slider("Height Subdivisions", b.heightSubdivisions, 2,  50);
            int newFloorSub  = (int)Slider("Floor Subdivisions",  b.floorSubdivisions,  2,  50);

            Header("Compartment Dividers");
            float newDivThick = Slider("Divider Thickness", b.dividerThickness, 0.001f, 5f);

            if (b.compartmentRatios == null) b.compartmentRatios = new System.Collections.Generic.List<float>();
            GUILayout.Label($"Compartments: {b.compartmentRatios.Count}", _sliderLabelStyle);
            for (int i = 0; i < b.compartmentRatios.Count; i++)
            {
                GUILayout.BeginHorizontal();
                b.compartmentRatios[i] = Slider($"  Ratio {i}", b.compartmentRatios[i], 0f, 100f);
                if (GUILayout.Button("\u2715", GUILayout.Width(22), GUILayout.Height(18)))
                { b.compartmentRatios.RemoveAt(i); needsRebuild = true; GUILayout.EndHorizontal(); goto AfterCompartments; }
                GUILayout.EndHorizontal();
            }
            AfterCompartments:
            if (GUILayout.Button("+ Add Compartment", GUILayout.Height(22)))
            { b.compartmentRatios.Add(1f); needsRebuild = true; }

            if (!Mathf.Approximately(newTopR,    b.topRadius)        ||
                !Mathf.Approximately(newBotR,    b.bottomRadius)     ||
                !Mathf.Approximately(newH,       b.height)           ||
                !Mathf.Approximately(newThick,   b.thickness)        ||
                !Mathf.Approximately(newDivThick,b.dividerThickness) ||
                newSegs != b.segments || newHeightSub != b.heightSubdivisions || newFloorSub != b.floorSubdivisions)
            {
                b.topRadius          = newTopR;
                b.bottomRadius       = newBotR;
                b.height             = newH;
                b.thickness          = newThick;
                b.dividerThickness   = newDivThick;
                b.segments           = newSegs;
                b.heightSubdivisions = newHeightSub;
                b.floorSubdivisions  = newFloorSub;
                needsRebuild = true;
            }

            if (needsRebuild)
            {
                b.GenerateBucket();
                if (_bucketHandle != null) _bucketHandle.GenerateHandle();
                ShowStatus("Bucket rebuilt.");
            }
        }

        // ── BucketHandleGenerator ────────────────────────────────────────────
        if (_bucketHandle != null)
        {
            var h = _bucketHandle;
            Header("Handle");
            float newW  = Slider("Width",     h.handleWidth,     0.002f, 2f);
            float newT  = Slider("Thickness", h.handleThickness, 0.001f, 1f);
            int   newS  = (int)Slider("Segments", h.handleSegments, 8, 64);
            float newCl = Slider("Clearance", h.clearance,       0f,    0.05f);

            if (!Mathf.Approximately(newW,  h.handleWidth)     ||
                !Mathf.Approximately(newT,  h.handleThickness) ||
                !Mathf.Approximately(newCl, h.clearance)       ||
                newS != h.handleSegments)
            {
                h.handleWidth     = newW;
                h.handleThickness = newT;
                h.handleSegments  = newS;
                h.clearance       = newCl;
                h.GenerateHandle();
                ShowStatus("Handle rebuilt.");
            }
        }

        // ── BucketHoleCutter ─────────────────────────────────────────────────
        if (_bucketHoleCutter != null)
        {
            var c = _bucketHoleCutter;
            Header("Holes");
            bool newEnable = Toggle("Enable Holes", c.enableHoles);
            if (newEnable != c.enableHoles) { c.enableHoles = newEnable; PushShaderData(); }

            if (c.holes == null) c.holes = new System.Collections.Generic.List<HoleData>();
            GUILayout.Label($"Holes: {c.holes.Count} / {BucketHoleShaderFeeder.MAX_HOLES}", _sliderLabelStyle);

            bool holeChanged = false;
            for (int i = 0; i < c.holes.Count; i++)
            {
                var hole = c.holes[i];
                GUILayout.BeginHorizontal();
                bool expanded = (_editingHoleIndex == i);
                if (GUILayout.Button($"{(expanded ? "▾" : "▸")}  {hole.holeName}", expanded ? _tabActiveStyle : _tabStyle, GUILayout.Height(22)))
                    _editingHoleIndex = expanded ? -1 : i;
                if (GUILayout.Button("\u2715", GUILayout.Width(22), GUILayout.Height(22)))
                { c.holes.RemoveAt(i); _editingHoleIndex = -1; holeChanged = true; GUILayout.EndHorizontal(); goto AfterHoles; }
                GUILayout.EndHorizontal();

                if (_editingHoleIndex == i)
                {
                    hole.type     = EnumButton("Type",     hole.type);
                    hole.location = EnumButton("Location", hole.location);

                    if (hole.type == BucketHoleCutter.HoleType.Circular)
                    {
                        float newR = Slider("Radius", hole.radius, 0.001f, 25f);
                        if (!Mathf.Approximately(newR, hole.radius)) { hole.radius = newR; holeChanged = true; }
                    }
                    else
                    {
                        float newW2 = Slider("Width",  hole.width,  0.001f, 10f);
                        float newH2 = Slider("Height", hole.height, 0.001f, 10f);
                        if (!Mathf.Approximately(newW2, hole.width))  { hole.width  = newW2; holeChanged = true; }
                        if (!Mathf.Approximately(newH2, hole.height)) { hole.height = newH2; holeChanged = true; }
                    }

                    if (hole.location == BucketHoleCutter.HoleLocation.Side)
                    {
                        float newAng = Slider("Angle (\u00b0)",   hole.angleDegrees,  0f,    360f);
                        float newHP  = Slider("Height Position", hole.heightPosition, 0f,    50f);
                        if (!Mathf.Approximately(newAng, hole.angleDegrees))   { hole.angleDegrees   = newAng; holeChanged = true; }
                        if (!Mathf.Approximately(newHP,  hole.heightPosition)) { hole.heightPosition = newHP;  holeChanged = true; }
                    }
                    else
                    {
                        float newOX = Slider("Offset X", hole.bottomOffset.x, -50f, 50f);
                        float newOY = Slider("Offset Y", hole.bottomOffset.y, -50f, 50f);
                        if (!Mathf.Approximately(newOX, hole.bottomOffset.x) ||
                            !Mathf.Approximately(newOY, hole.bottomOffset.y))
                        { hole.bottomOffset = new Vector2(newOX, newOY); holeChanged = true; }
                    }
                }
            }
            AfterHoles:

            if (c.holes.Count < BucketHoleShaderFeeder.MAX_HOLES)
            {
                if (GUILayout.Button("+ Add Hole", GUILayout.Height(22)))
                {
                    c.holes.Add(new HoleData { holeName = $"Hole {c.holes.Count}" });
                    _editingHoleIndex = c.holes.Count - 1;
                    holeChanged = true;
                }
            }

            if (holeChanged) PushShaderData();
        }
    }

    void PushShaderData()
    {
        if (_bucketShaderFeeder != null) _bucketShaderFeeder.UpdateShaderData();
    }


    bool _uniformScale = true;
    void Header(string title)
    {
        GUILayout.Space(6);
        GUILayout.Label(title, _headerStyle);
    }

    float Slider(string label, float value, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, _sliderLabelStyle, GUILayout.Width(150));
        float v = GUILayout.HorizontalSlider(value, min, max, GUILayout.MinWidth(120));
        GUILayout.Label(v.ToString("F3"), _sliderLabelStyle, GUILayout.Width(55));
        GUILayout.EndHorizontal();
        return v;
    }

    // Click-to-cycle enum. R/G/B/A sliders since IMGUI has no built-in color picker.
    Color ColorField(string label, Color c)
    {
        if (!string.IsNullOrEmpty(label)) GUILayout.Label(label, _sliderLabelStyle);
        c.r = Slider("R", c.r, 0f, 1f);
        c.g = Slider("G", c.g, 0f, 1f);
        c.b = Slider("B", c.b, 0f, 1f);
        c.a = Slider("A", c.a, 0f, 1f);
        return c;
    }

    void DrawSwatch(Color c)
    {
        var rect = GUILayoutUtility.GetRect(24, 16, GUILayout.Width(24), GUILayout.Height(16));
        var prev = GUI.color;
        GUI.color = c;
        if (_lineTex == null) { _lineTex = new Texture2D(1, 1); _lineTex.SetPixel(0, 0, Color.white); _lineTex.Apply(); }
        GUI.DrawTexture(rect, _lineTex);
        GUI.color = prev;
    }

    bool Toggle(string label, bool value)
    {
        return GUILayout.Toggle(value, " " + label);
    }

    // Click to cycle through enum values — makes every enum field (render mode,
    // blur type, solver method, mixing model, ...) directly editable, which the
    // previous UI never exposed at all.
    T EnumButton<T>(string label, T value) where T : System.Enum
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, _sliderLabelStyle, GUILayout.Width(150));
        if (GUILayout.Button(value.ToString(), GUILayout.MinWidth(120)))
        {
            var values = (T[])System.Enum.GetValues(typeof(T));
            int idx = System.Array.IndexOf(values, value);
            idx = (idx + 1) % values.Length;
            value = values[idx];
        }
        GUILayout.EndHorizontal();
        return value;
    }

    void BuildStyles()
    {
        if (_stylesBuilt) return;
        _stylesBuilt = true;

        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        _sliderLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
        };
        _tabStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };
        _tabActiveStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = GUI.skin.button.active.background }
        };
        _smallBtn = new GUIStyle(GUI.skin.button) { fontSize = 10 };
    }
}