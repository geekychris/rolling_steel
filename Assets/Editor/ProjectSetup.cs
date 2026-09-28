using System.IO;
using RollingSteel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// One-shot project bootstrap, run headlessly with -executeMethod ProjectSetup.Run.
/// Creates the material assets, the single scene, and the player settings, so the
/// whole project can be regenerated from source with no manual editor work.
public static class ProjectSetup
{
    const string MatDir = "Assets/Resources/Mat";
    const string ScenePath = "Assets/Scenes/Main.unity";

    public static void Run()
    {
        CreateMaterials();
        CreateScene();
        ConfigurePlayer();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ProjectSetup] SETUP_OK");
        EditorApplication.Exit(0);
    }

    static void EnsureDir(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    static void CreateMaterials()
    {
        EnsureDir(MatDir);

        //     name       colour                        metal smooth  emission
        Mat("Deck", new Color(0.58f, 0.62f, 0.70f), 0.05f, 0.25f);
        Mat("DeckAlt", new Color(0.52f, 0.56f, 0.65f), 0.05f, 0.25f);
        Mat("Rail", new Color(0.28f, 0.31f, 0.39f), 0.25f, 0.35f);
        Mat("Ice", new Color(0.62f, 0.86f, 0.96f), 0.00f, 0.93f);
        Mat("Rough", new Color(0.72f, 0.57f, 0.34f), 0.00f, 0.05f);
        Mat("Acid", new Color(0.24f, 0.92f, 0.34f), 0.00f, 0.80f, new Color(0.10f, 0.45f, 0.13f));
        Mat("Goal", new Color(1.00f, 0.82f, 0.20f), 0.10f, 0.60f, new Color(0.55f, 0.38f, 0.05f));
        Mat("Start", new Color(0.26f, 0.55f, 1.00f), 0.10f, 0.50f, new Color(0.06f, 0.16f, 0.42f));
        Mat("Marble", new Color(0.86f, 0.88f, 0.93f), 0.95f, 0.86f);
        Mat("Steel", new Color(0.33f, 0.34f, 0.39f), 0.90f, 0.70f);
        Mat("Blob", new Color(0.34f, 0.94f, 0.40f), 0.00f, 0.30f, new Color(0.09f, 0.42f, 0.11f));

        // floating scenery
        Mat("Bark", new Color(0.32f, 0.24f, 0.18f), 0.00f, 0.12f);
        Mat("Leaf", new Color(0.24f, 0.55f, 0.30f), 0.00f, 0.22f);
        Mat("Stone", new Color(0.26f, 0.27f, 0.33f), 0.05f, 0.18f);
        Mat("Crystal", new Color(0.55f, 0.82f, 0.95f), 0.20f, 0.88f, new Color(0.05f, 0.15f, 0.22f));
        Mat("Glow", new Color(1.00f, 0.66f, 0.28f), 0.00f, 0.55f, new Color(0.60f, 0.30f, 0.06f));
    }

    static void Mat(string name, Color col, float metallic, float smooth, Color? emission = null)
    {
        var shader = Shader.Find("Standard");
        if (shader == null) { Debug.LogError("[ProjectSetup] Standard shader not found"); return; }

        var m = new Material(shader) { name = name };
        m.SetColor("_Color", col);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smooth);

        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        string path = $"{MatDir}/{name}.mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
    }

    static void CreateScene()
    {
        EnsureDir("Assets/Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("GameDirector");
        go.AddComponent<GameDirector>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static void ConfigurePlayer()
    {
        PlayerSettings.companyName = "claude world";
        PlayerSettings.productName = "Rolling Steel";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.macRetinaSupport = true;
    }
}
