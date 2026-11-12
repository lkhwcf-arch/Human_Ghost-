// R6 맵 그림 확인용 (임시). Assets/_R6_Check 폴더만 건드림. 기존 씬·프리팹은 열지 않음.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class R6CheckImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/_R6_Check/")) return;
        R6CheckBuilder.Apply((TextureImporter)assetImporter);
    }
}

public static class R6CheckBuilder
{
    const string Root = "Assets/_R6_Check";
    static string outDir;
    static List<string> log = new List<string>();

    public static void Apply(TextureImporter ti)
    {
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        bool isPlayer = ti.assetPath.EndsWith("Player_Idle.png");
        ti.spritePixelsPerUnit = isPlayer ? 212f / 1.5f : 48f;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = 8192;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = isPlayer ? (int)SpriteAlignment.BottomCenter : (int)SpriteAlignment.TopLeft;
        s.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(s);
    }

    static Sprite Load(string rel)
    {
        string p = Root + "/Tex/" + rel;
        var ti = AssetImporter.GetAtPath(p) as TextureImporter;
        if (ti == null) { log.Add("MISSING " + p); return null; }
        if (ti.maxTextureSize != 8192 || ti.textureType != TextureImporterType.Sprite)
        { Apply(ti); ti.SaveAndReimport(); }
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(p);
        if (sp == null) log.Add("NOSPRITE " + p);
        else log.Add(string.Format("{0}: tex {1}x{2} -> {3:F2}x{4:F2} units", rel, sp.texture.width, sp.texture.height, sp.bounds.size.x, sp.bounds.size.y));
        return sp;
    }

    static SpriteRenderer Put(Transform parent, string name, Sprite sp, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp; sr.sortingOrder = order;
        return sr;
    }

    static Camera cam;
    static void Shot(string file, Vector2 center, float ortho, int w, int h)
    {
        cam.orthographicSize = ortho;
        cam.transform.position = new Vector3(center.x, center.y, -10);
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt; cam.aspect = (float)w / h;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        File.WriteAllBytes(Path.Combine(outDir, file), tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
    }

    public static void Run()
    {
        outDir = System.Environment.GetEnvironmentVariable("R6_OUT");
        Directory.CreateDirectory(outDir);
        try { Build(); }
        catch (System.Exception e) { log.Add("ERROR " + e); }
        File.WriteAllLines(Path.Combine(outDir, "unity_log.txt"), log.ToArray());
    }

    static void Build()
    {
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.35f, 0.46f, 0.6f);
        camGo.tag = "MainCamera";

        // ---- 8006 옹기 가마터 길: 레이어 조립 ----
        var onggi = new GameObject("L8006_Onggi_R6").transform;
        var L = "MapLayers/L8006_Map_2b_Onggi/";
        Put(onggi, "01_Sky", Load(L + "01_Sky.png"), -100);
        Put(onggi, "02_Far", Load(L + "02_Far.png"), -90);
        Put(onggi, "03_Near", Load(L + "03_Near.png"), -86);
        Put(onggi, "04_KilnYard", Load(L + "04_KilnYard.png"), -80);
        var G = "Onggi_Layout/";
        Put(onggi, "05a_PitDepth", Load(G + "05a_PitDepth.png"), 29);
        Put(onggi, "05b_Supports", Load(G + "05b_Supports.png"), 30);
        Put(onggi, "05e_Props_and_Ladders", Load(G + "05e_Props_and_Ladders.png"), 32);
        Put(onggi, "05c_Terrain", Load(G + "05c_Terrain.png"), 40);
        Put(onggi, "05d_Platforms", Load(G + "05d_Platforms.png"), 41);
        Put(onggi, "06_Foreground", Load(G + "06_Foreground.png"), 70);
        var ov = Put(onggi, "LDtk_Overlay(확인용)", Load("Overlay/L8006_Map_2b_Onggi_Overlay.png"), 90);

        // ---- 플레이어 (실제 프리팹) ----
        var psp = Load("Player_Idle.png");
        GameObject player = Put(null, "Player_Idle_1.5칸(확인용)", psp, 61).gameObject;
        System.Action<float, float> placePlayer = (x, feetY) =>
        {
            if (player == null) return;
            player.transform.position = new Vector3(x, feetY, 0);
            Bounds b = new Bounds(); bool has = false;
            foreach (var sr in player.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.sprite == null || !sr.enabled) continue;
                if (!has) { b = sr.bounds; has = true; } else b.Encapsulate(sr.bounds);
            }
            if (has) player.transform.position += new Vector3(0, feetY - b.min.y, 0);
            if (has) log.Add(string.Format("player @x{0}: sprite bounds h={1:F2} w={2:F2}", x, b.size.y, b.size.x));
        };

        // ---- 전체 경로 ----
        if (player != null) player.SetActive(false);
        ov.enabled = false; Shot("8006_full_clean.png", new Vector2(60, -16), 16, 3840, 1024);
        ov.enabled = true; Shot("8006_full_overlay.png", new Vector2(60, -16), 16, 3840, 1024);
        if (player != null) player.SetActive(true);

        // ---- 게임 카메라 장면 (orthographic 5.4, 16:9, 진행방향 +0.8) ----
        float ortho = 5.4f, hw = ortho * 16f / 9f;
        var shots = new[] {
            new float[]{5,22}, new float[]{11,22}, new float[]{17,18}, new float[]{27,22}, new float[]{45,22},
            new float[]{56,18}, new float[]{65,22}, new float[]{78,22}, new float[]{84,22}, new float[]{101,22}, new float[]{114,20} };
        foreach (var s in shots)
        {
            float px = s[0], feet = -s[1];
            placePlayer(px + 0.5f, feet);
            float cx = Mathf.Clamp(px + 0.5f + 0.8f, hw, 120 - hw);
            float cy = Mathf.Max(-32 + ortho, feet + 2.7f);
            string n = "8006_x" + ((int)px).ToString("000");
            ov.enabled = false; Shot(n + "_clean.png", new Vector2(cx, cy), ortho, 1920, 1080);
            ov.enabled = true; Shot(n + "_overlay.png", new Vector2(cx, cy), ortho, 1920, 1080);
        }
        // 기존 확대 4.3
        placePlayer(27.5f, -22); ov.enabled = false;
        Shot("8006_x027_zoom43.png", new Vector2(Mathf.Clamp(28.8f, 4.3f * 16 / 9, 120), -19.3f), 4.3f, 1920, 1080);
        ov.enabled = false;
        if (player != null) player.transform.position = new Vector3(5.5f, -22, 0);
        placePlayer(5.5f, -22);

        // ---- 나머지 맵: 한 장 배경 + 덧그림 ----
        string[] maps = { "L8001_Map_1_Dohwagol", "L8002_Map_2_Sangil", "L8003_Map_3_Hyeopgok", "L8004_BossRoom", "L8005_Jangbubang",
                          "X01_Galimgil", "E01_Old_Temple", "E02_Next_Village", "M01_Old_Mine", "M02_Yogoe_Lair", "M03_Jin_Route_End", "L8006_Map_2b_Onggi" };
        var others = new GameObject("Other_Maps_R6").transform;
        float oy = 100;
        foreach (var m in maps)
        {
            var root = new GameObject(m).transform; root.SetParent(others, false);
            root.position = new Vector3(0, oy, 0);
            var bg = Put(root, "Map", Load("Maps/" + m + ".png"), -100);
            var o = Put(root, "Overlay", Load("Overlay/" + m + "_Overlay.png"), 90);
            if (bg == null || bg.sprite == null) continue;
            var sz = bg.sprite.bounds.size;
            float aspect = sz.x / sz.y; int W, H;
            if (aspect >= 1) { W = 2400; H = Mathf.RoundToInt(2400 / aspect); } else { H = 1600; W = Mathf.RoundToInt(1600 * aspect); }
            float orth = sz.y / 2f;
            var c = new Vector2(sz.x / 2f, oy - sz.y / 2f);
            o.enabled = false; Shot("map_" + m + "_clean.png", c, orth, W, H);
            o.enabled = true; Shot("map_" + m + "_overlay.png", c, orth, W, H);
            o.enabled = false;
            oy += sz.y + 20;
        }
        others.gameObject.SetActive(false);
        ov.enabled = false;

        cam.orthographicSize = 5.4f; cam.transform.position = new Vector3(hw, -19.3f, -10); cam.targetTexture = null;
        EditorSceneManager.SaveScene(scene, Root + "/R6_Check.unity");
        log.Add("saved scene");
    }
}

