using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ConsoleCards.Intro.Editor
{
    /// <summary>
    /// Owner, 2026-10-29 (doc 37): builds the title intro scene from the approved "Console Cards Lounge" mock-up.
    /// Console Cards &gt; Intro &gt; Build Intro Scene makes <c>Presentation/Scenes/Intro.unity</c> (the room, the
    /// hexagon table, your console and pieces, the lights, the post-processing, the words and the
    /// <see cref="IntroDirector"/>), its meshes and materials under <c>Content/Intro/</c>, and puts the scene first in
    /// Build Settings (the table scene stays next). Running it again builds the scene again from scratch.
    /// It also removes the 2D intro of 2026-10-28 (v0.1) if it is still there.
    /// Everything in the room is made here or comes from the project's own models; nothing is downloaded.
    /// </summary>
    public static class IntroSceneBuilder
    {
        public const string ScenePath = "Assets/ConsoleCards/Presentation/Scenes/Intro.unity";
        private const string Root = "Assets/ConsoleCards/Content/Intro/";
        private const string TexPath = Root + "Textures/";
        private const string MatPath = Root + "Materials/";
        private const string VolumePath = Root + "IntroVolume.asset";
        private const string FontPath = "Assets/ConsoleCards/Content/UI/Fonts/";
        private const string ModelPath = "Assets/3D Models/";

        // The table (metres): felt height and the hexagon's radii, as in the mock-up.
        private const float FeltY = 0.76f;
        private const float RIn = 1.19f;
        private const float ROut = 1.32f;
        private const float HexA0 = 0f; // corners at 0°, 60°…: a flat edge faces the camera (+z)
        private static readonly float Apothem = RIn * Mathf.Cos(Mathf.PI / 6f);

        private static readonly string[] OldIntro =
        {
            "Assets/ConsoleCards/Editor/UI/TitleIntroBuilder.cs",
            "Assets/ConsoleCards/Presentation/UI/TitleIntroView.cs",
            "Assets/ConsoleCards/Presentation/UI/TitleIntroTarget.cs",
            "Assets/ConsoleCards/Content/UI/Intro",
            "Assets/ConsoleCards/Content/Prefabs/Final/UI/TitleIntro.prefab",
        };

        private static Dictionary<string, Material> mats;
        private static System.Random random;

        [MenuItem("Console Cards/Intro/Build Intro Scene")]
        public static void BuildMenu()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Build Intro Scene",
                    "Intro.unity is built again from scratch. Changes you made to that scene are replaced.", "Build", "Cancel"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                EditorUtility.DisplayProgressBar("Build Intro Scene", "Removing the old 2D intro", 0.05f);
                RemoveOldIntro();
                EditorUtility.DisplayProgressBar("Build Intro Scene", "Textures and materials", 0.2f);
                Folders();
                ImportTextures();
                MakeMaterials();
                EditorUtility.DisplayProgressBar("Build Intro Scene", "The room", 0.45f);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                BuildScene();
                EditorUtility.DisplayProgressBar("Build Intro Scene", "Saving", 0.9f);
                EditorSceneManager.SaveScene(scene, ScenePath);
                PutFirstInBuild();
                AssetDatabase.SaveAssets();
                Debug.Log("Intro: built " + ScenePath + " and put it first in Build Settings. Press Play in this scene to try it.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Console Cards/Intro/Take Intro Out Of The Build")]
        public static void RemoveFromBuild()
        {
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToArray();
            Debug.Log("Intro: taken out of Build Settings; the game starts on the table scene again.");
        }

        // ---------- clean-up of v0.1 ----------

        private static void RemoveOldIntro()
        {
            foreach (string path in OldIntro)
            {
                if (AssetDatabase.IsValidFolder(path) || File.Exists(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log("Intro: removed the old 2D intro's " + path);
                }
            }
        }

        // ---------- textures and materials ----------

        private static void Folders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ConsoleCards/Content/Intro")) AssetDatabase.CreateFolder("Assets/ConsoleCards/Content", "Intro");
            if (!AssetDatabase.IsValidFolder(Root + "Materials")) AssetDatabase.CreateFolder("Assets/ConsoleCards/Content/Intro", "Materials");
            if (!AssetDatabase.IsValidFolder(Root + "Meshes")) AssetDatabase.CreateFolder("Assets/ConsoleCards/Content/Intro", "Meshes");
        }

        private static void ImportTextures()
        {
            if (!AssetDatabase.IsValidFolder(Root + "Textures"))
            {
                Debug.LogWarning("Intro: the textures folder " + TexPath + " is missing; surfaces will be plain colours.");
                return;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "Textures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                string file = Path.GetFileNameWithoutExtension(path);
                bool normal = file.EndsWith("_N");
                bool clamp = file.Contains("Rug") || file.Contains("Poster") || file.Contains("Dust") || file.Contains("BoxSpine");
                TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                TextureWrapMode wrap = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                if (importer.textureType == type && importer.wrapMode == wrap && importer.anisoLevel == 8 && importer.maxTextureSize == 2048) continue;
                importer.textureType = type;
                importer.sRGBTexture = !normal;
                importer.wrapMode = wrap;
                importer.anisoLevel = 8;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 2048;
                importer.alphaIsTransparency = file.Contains("Dust");
                importer.SaveAndReimport();
            }
        }

        private static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath + name + ".png");

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        private static Material Lit(string name, Color color, float smooth, string albedo = null, string normal = null,
            float tilingMetres = 0f, float normalScale = 1f, float metallic = 0f)
        {
            string path = MatPath + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.shader = shader;
            }

            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", albedo != null ? Tex(albedo) : null);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metallic);
            Texture2D bump = normal != null ? Tex(normal) : null;
            m.SetTexture("_BumpMap", bump);
            m.SetFloat("_BumpScale", normalScale);
            if (bump != null) m.EnableKeyword("_NORMALMAP");
            else m.DisableKeyword("_NORMALMAP");
            float tile = tilingMetres > 0f ? 1f / tilingMetres : 1f;
            m.SetTextureScale("_BaseMap", new Vector2(tile, tile));
            m.SetColor("_EmissionColor", Color.black);
            m.DisableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        private static Material Glow(string name, Color color, float strength, string map = null, Color? baseColor = null)
        {
            Material m = Lit(name, baseColor ?? Color.black, 0.3f, map);
            m.SetColor("_EmissionColor", color * strength);
            if (map != null) m.SetTexture("_EmissionMap", Tex(map));
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static readonly string[] BoxColours =
            { "#f2762e", "#06c9cc", "#ffc93b", "#2f7bff", "#ff3b3b", "#2fd46b", "#e8eced", "#7a4bff", "#1f4a4f", "#f29b2e" };

        private static void MakeMaterials()
        {
            mats = new Dictionary<string, Material>();
            Lit("M_Floor", Color.white, 0.42f, "T_FloorPlanks_A", "T_FloorPlanks_N", 2.47f, 0.8f);
            Lit("M_Wall", Hex("#3a4247"), 0.12f, "T_WallSlats_A", "T_WallSlats_N", 1.1f, 0.35f);
            Lit("M_WallPanel", Hex("#4a555a"), 0.18f, "T_WallSlats_A", "T_WallSlats_N", 1.1f, 1f);
            Lit("M_Ceiling", Hex("#1c1f21"), 0.1f);
            Lit("M_Walnut", Color.white, 0.5f, "T_Walnut_A", "T_Walnut_N", 1f, 0.6f);
            Lit("M_WalnutDark", Hex("#9a8a80"), 0.45f, "T_Walnut_A", "T_Walnut_N", 1f, 0.6f);
            Lit("M_Felt", Color.white, 0.05f, "T_Felt_A", "T_Felt_N", 1f, 0.5f);
            Lit("M_Charcoal", Hex("#222527"), 0.3f);
            Lit("M_Charcoal2", Hex("#2b3033"), 0.35f);
            Lit("M_Black", Hex("#0c0e0f"), 0.2f);
            Lit("M_Device", Hex("#141719"), 0.45f);
            Lit("M_Steel", Hex("#8a9296"), 0.75f, null, null, 0f, 1f, 1f);
            Lit("M_Leather", Hex("#4a2c1f"), 0.42f, null, "T_Leather_N", 0.3f, 0.7f);
            Lit("M_FabricTeal", Hex("#1f4a4f"), 0.08f, null, "T_Fabric_N", 0.25f, 0.6f);
            Lit("M_FabricTeal2", Hex("#24565b"), 0.08f, null, "T_Fabric_N", 0.25f, 0.6f);
            Lit("M_FabricOrange", Hex("#f2762e"), 0.08f, null, "T_Fabric_N", 0.25f, 0.6f);
            Lit("M_FabricCharcoal", Hex("#2b3033"), 0.08f, null, "T_Fabric_N", 0.25f, 0.6f);
            Lit("M_FabricCyan", Hex("#0b5152"), 0.08f, null, "T_Fabric_N", 0.25f, 0.6f);
            Lit("M_Rug", Color.white, 0.05f, "T_Rug_A");
            Lit("M_TileA", Hex("#4a5155"), 0.5f);
            Lit("M_TileB", Hex("#3e4447"), 0.5f);
            Glow("M_TileOrange", Hex("#f2762e"), 0.6f, null, Hex("#5a2a12"));
            Glow("M_TileCyan", Hex("#06eff0"), 0.5f, null, Hex("#0b4041"));
            Lit("M_Pot", Hex("#3a3f42"), 0.55f);
            Lit("M_Leaf1", Hex("#2f5a33"), 0.25f);
            Lit("M_Leaf2", Hex("#3f7a3c"), 0.25f);
            Lit("M_TVBezel", Hex("#0a0c0d"), 0.85f);
            Glow("M_TVScreen", Color.white, 0.16f, "center", Hex("#050607"));
            Glow("M_LedCyan", Hex("#06eff0"), 2.4f);
            Glow("M_LedOrange", Hex("#f2762e"), 1.8f);
            Glow("M_LedWarm", Hex("#ffb46b"), 3.2f);
            Glow("M_LampShade", Hex("#ffb46b"), 1.6f, null, Hex("#3a2a1a"));
            Glow("M_Poster_TrapFloor", Color.white, 0.35f, "T_Poster_TrapFloor_A", Color.white);
            Glow("M_Poster_TowerClash", Color.white, 0.35f, "T_Poster_TowerClash_A", Color.white);
            foreach (KeyValuePair<string, string> seat in SeatColours)
            {
                Glow("M_Border_" + seat.Key, Hex(seat.Value), 1.1f);
                Lit("M_Plastic_" + seat.Key, Hex(seat.Value), 0.8f);
            }

            Glow("M_Border_Empty", Hex("#4b5357"), 0.4f);
            for (int i = 0; i < BoxColours.Length; i++) Lit("M_Box" + i, Hex(BoxColours[i]), 0.5f, "T_BoxSpine_A");

            // The console's screen picture as a glowing material (the director changes its glow).
            Texture2D logo = AssetDatabase.LoadAssetAtPath<Texture2D>(ModelPath + "Console3D/center.png");
            Material screen = Lit("M_ConsoleScreen", Color.white, 0.2f);
            screen.SetTexture("_BaseMap", logo);
            screen.SetTexture("_EmissionMap", logo);
            screen.SetColor("_EmissionColor", Color.white * 0.35f);
            screen.EnableKeyword("_EMISSION");
            screen.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Material tv = mats["M_TVScreen"];
            tv.SetTexture("_BaseMap", logo);
            tv.SetTexture("_EmissionMap", logo);

            // Dust: a copy of URP's particle material with the soft dot.
            Material dust = AssetDatabase.LoadAssetAtPath<Material>(MatPath + "M_Dust.mat");
            Material source = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
            if (dust == null && source != null)
            {
                dust = new Material(source);
                AssetDatabase.CreateAsset(dust, MatPath + "M_Dust.mat");
            }

            if (dust != null)
            {
                dust.SetTexture("_BaseMap", Tex("T_Dust"));
                dust.SetColor("_BaseColor", new Color(1f, 0.85f, 0.7f, 0.55f));
                EditorUtility.SetDirty(dust);
                mats["M_Dust"] = dust;
            }
        }

        private static readonly KeyValuePair<string, string>[] SeatColours =
        {
            new KeyValuePair<string, string>("Red", "#ff3b3b"),
            new KeyValuePair<string, string>("Blue", "#2f7bff"),
            new KeyValuePair<string, string>("Green", "#2fd46b"),
            new KeyValuePair<string, string>("Yellow", "#ffc93b"),
        };

        private static Material M(string name) => mats.TryGetValue(name, out Material m) ? m : null;

        // ---------- the scene ----------

        private static void BuildScene()
        {
            random = new System.Random(7);
            Transform room = Group("Room");
            Transform table = Group("Table");
            Transform pieces = Group("Pieces");
            Transform lights = Group("Lights");

            Environment();
            BuildRoom(room);
            BuildTable(table);
            Transform console = PlaceConsole(pieces, out Renderer screen, out Transform focus, out Bounds consoleBounds);
            BuildPieces(pieces);
            List<Light> pulse = new List<Light>();
            BuildLights(lights, focus, pulse, out Light consoleLight, out Light sweep);
            Camera camera = BuildCamera();
            Volume volume = BuildVolume();
            BuildDust();

            GameObject hit = new GameObject("Console Hit");
            hit.transform.position = consoleBounds.center + new Vector3(0f, 0.03f, 0f);
            BoxCollider box = hit.AddComponent<BoxCollider>();
            box.size = consoleBounds.size + new Vector3(0.04f, 0.1f, 0.04f);

            Ui(out UiParts ui);
            GameObject directorGo = new GameObject("Intro Director");
            AudioSource audio = directorGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            IntroDirector director = directorGo.AddComponent<IntroDirector>();
            SerializedObject so = new SerializedObject(director);
            Set(so, "shotCamera", camera);
            Set(so, "consoleHit", box);
            Set(so, "screenFocus", focus);
            Set(so, "screenRenderer", screen);
            Set(so, "consoleLight", consoleLight);
            Set(so, "sweepLight", sweep);
            so.FindProperty("sweepFrom").vector3Value = new Vector3(consoleBounds.min.x, consoleBounds.max.y + 0.05f, consoleBounds.center.z);
            so.FindProperty("sweepTo").vector3Value = new Vector3(consoleBounds.max.x, consoleBounds.max.y + 0.05f, consoleBounds.center.z);
            SerializedProperty list = so.FindProperty("pulseLights");
            list.arraySize = pulse.Count;
            for (int i = 0; i < pulse.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = pulse[i];
            Set(so, "volume", volume);
            Set(so, "prompt", ui.Prompt);
            Set(so, "promptGroup", ui.PromptGroup);
            Set(so, "promptLift", ui.Lift);
            Set(so, "promptWord", ui.Word);
            Set(so, "promptLine", ui.Line);
            Set(so, "hud", ui.Hud);
            Set(so, "exitButton", ui.Exit);
            Set(so, "audioSource", audio);
            Set(so, "hoverClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ConsoleCards/Content/Resources/CardSounds/CardTap.wav"));
            Set(so, "bootClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ConsoleCards/Content/Resources/UiSounds/TablePing.wav"));
            so.FindProperty("nextScene").stringValue = "Tabletop";
            so.ApplyModifiedPropertiesWithoutUndo();
            if (console == null) Debug.LogWarning("Intro: the console model was not found; the dive aims at the table centre.");
        }

        private static void Environment()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#1b2328");
            RenderSettings.ambientEquatorColor = Hex("#14191b");
            RenderSettings.ambientGroundColor = Hex("#0c0d0e");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Hex("#07090a");
            RenderSettings.fogDensity = 0.055f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 1f;
        }

        private static void BuildRoom(Transform room)
        {
            const float wz = -4.3f;
            Mesh floor = IntroMeshes.Plane("Floor", new Vector3(-7f, 0f, wz), new Vector3(14f, 0f, 0f), new Vector3(0f, 0f, 10.3f), Vector3.up);
            Place("Floor", room, floor, M("M_Floor"), Vector3.zero);
            Place("Back Wall", room, IntroMeshes.Plane("BackWall", new Vector3(-6f, 0f, wz), new Vector3(12f, 0f, 0f), new Vector3(0f, 3.2f, 0f), Vector3.forward), M("M_Wall"), Vector3.zero);
            Place("Left Wall", room, IntroMeshes.Plane("LeftWall", new Vector3(-4.9f, 0f, wz), new Vector3(0f, 0f, 10.3f), new Vector3(0f, 3.2f, 0f), Vector3.right), M("M_Wall"), Vector3.zero);
            Place("Right Wall", room, IntroMeshes.Plane("RightWall", new Vector3(4.9f, 0f, wz), new Vector3(0f, 0f, 10.3f), new Vector3(0f, 3.2f, 0f), Vector3.left), M("M_Wall"), Vector3.zero);
            Place("Ceiling", room, IntroMeshes.Plane("Ceiling", new Vector3(-6f, 3.2f, wz), new Vector3(12f, 0f, 0f), new Vector3(0f, 0f, 10.3f), Vector3.down), M("M_Ceiling"), Vector3.zero);
            Box("Skirting", room, new Vector3(0f, 0.05f, wz + 0.02f), new Vector3(12f, 0.1f, 0.04f), M("M_Charcoal2"));

            // Rug: a hexagon in our colours under the table.
            Place("Rug", room, IntroMeshes.Prism("Rug", 6, 2.35f, 0f, 0.012f, HexA0, false, 1f, true), M("M_Rug"), Vector3.zero);

            // TV wall: slatted panel, LED columns, the TV with our logo, the media unit.
            Material panel = M("M_WallPanel");
            if (panel != null) panel.SetTextureScale("_BaseMap", new Vector2(3.2f / 1.1f, 2.6f / 1.1f));
            Box("TV Panel", room, new Vector3(0f, 1.55f, wz + 0.03f), new Vector3(3.2f, 2.6f, 0.04f), panel);
            Led("LED Column L", room, new Vector3(-1.62f, 1.55f, wz + 0.06f), new Vector3(0.02f, 2.5f, 0.02f), M("M_LedCyan"));
            Led("LED Column R", room, new Vector3(1.62f, 1.55f, wz + 0.06f), new Vector3(0.02f, 2.5f, 0.02f), M("M_LedCyan"));
            Box("TV", room, new Vector3(0f, 1.72f, wz + 0.09f), new Vector3(2.05f, 1.17f, 0.05f), M("M_TVBezel"));
            Picture("TV Screen", room, new Vector3(0f, 1.72f, wz + 0.118f), new Vector2(1.98f, 1.1f), M("M_TVScreen"));
            Box("Media Unit", room, new Vector3(0f, 0.27f, wz + 0.3f), new Vector3(2.7f, 0.48f, 0.48f), M("M_WalnutDark"));
            Box("Media Top", room, new Vector3(0f, 0.51f, wz + 0.3f), new Vector3(2.72f, 0.03f, 0.5f), M("M_Charcoal"));
            Led("Media LED", room, new Vector3(0f, 0.035f, wz + 0.53f), new Vector3(2.6f, 0.012f, 0.01f), M("M_LedCyan"));
            Box("Console Dock", room, new Vector3(-0.55f, 0.57f, wz + 0.3f), new Vector3(0.42f, 0.08f, 0.28f), M("M_Charcoal2"));
            Led("Dock Light", room, new Vector3(-0.4f, 0.575f, wz + 0.445f), new Vector3(0.08f, 0.01f, 0.004f), M("M_LedCyan"));
            Box("Receiver", room, new Vector3(0.6f, 0.56f, wz + 0.3f), new Vector3(0.32f, 0.06f, 0.24f), M("M_Device"));
            Led("Receiver Light", room, new Vector3(0.5f, 0.56f, wz + 0.421f), new Vector3(0.03f, 0.01f, 0.004f), M("M_LedOrange"));
            foreach (float x in new[] { -1.15f, 1.15f })
            {
                Box("Speaker", room, new Vector3(x, 0.8f, wz + 0.3f), new Vector3(0.22f, 0.55f, 0.24f), M("M_Device"));
                Cyl("Speaker Cone", room, new Vector3(x, 0.9f, wz + 0.425f), 0.06f, 0.01f, M("M_Charcoal2"), new Vector3(90f, 0f, 0f));
            }

            // Shelving units with our game boxes, LED strips under each shelf, posters of our games above.
            foreach (float sx in new[] { -2.75f, 2.75f })
            {
                const float w = 1.5f, h = 2.5f, d = 0.38f;
                float z = wz + d / 2f + 0.02f;
                Box("Shelf Back", room, new Vector3(sx, h / 2f, wz + 0.035f), new Vector3(w, h, 0.03f), M("M_WalnutDark"));
                Box("Shelf Side", room, new Vector3(sx - w / 2f + 0.02f, h / 2f, z), new Vector3(0.04f, h, d), M("M_Walnut"));
                Box("Shelf Side", room, new Vector3(sx + w / 2f - 0.02f, h / 2f, z), new Vector3(0.04f, h, d), M("M_Walnut"));
                for (int k = 0; k < 5; k++)
                {
                    float y = 0.08f + k * 0.55f;
                    Box("Shelf", room, new Vector3(sx, y, z), new Vector3(w, 0.035f, d), M("M_Walnut"));
                    if (k > 0) Led("Shelf LED", room, new Vector3(sx, y - 0.02f, z + d / 2f - 0.01f), new Vector3(w - 0.1f, 0.008f, 0.008f), M(k % 2 == 1 ? "M_LedOrange" : "M_LedCyan"));
                    if (k == 4) continue;
                    FillShelf(room, sx, w, y + 0.0175f, z);
                }
            }

            Box("Poster Frame", room, new Vector3(-2.75f, 2.85f, wz + 0.03f), new Vector3(0.9f, 0.56f, 0.03f), M("M_Black"));
            Picture("Poster Trap Floor", room, new Vector3(-2.75f, 2.85f, wz + 0.047f), new Vector2(0.82f, 0.48f), M("M_Poster_TrapFloor"));
            Box("Poster Frame", room, new Vector3(2.75f, 2.85f, wz + 0.03f), new Vector3(0.9f, 0.56f, 0.03f), M("M_Black"));
            Picture("Poster Tower Clash", room, new Vector3(2.75f, 2.85f, wz + 0.047f), new Vector2(0.82f, 0.48f), M("M_Poster_TowerClash"));

            // Left: sofa, coffee table, floor lamp, plants.
            Furniture("Sofa", room, new Vector3(-3.35f, 0f, -2.55f), 0.55f, (g, at) =>
            {
                Box("Seat", g, at(new Vector3(0f, 0.25f, 0f)), new Vector3(2.1f, 0.32f, 0.9f), M("M_FabricTeal"), Yaw(0.55f));
                Box("Back", g, at(new Vector3(0f, 0.62f, -0.36f)), new Vector3(2.1f, 0.5f, 0.2f), M("M_FabricTeal"), Yaw(0.55f));
                Box("Arm", g, at(new Vector3(-1f, 0.5f, 0f)), new Vector3(0.18f, 0.34f, 0.9f), M("M_FabricTeal"), Yaw(0.55f));
                Box("Arm", g, at(new Vector3(1f, 0.5f, 0f)), new Vector3(0.18f, 0.34f, 0.9f), M("M_FabricTeal"), Yaw(0.55f));
                Box("Cushion", g, at(new Vector3(-0.45f, 0.47f, -0.05f)), new Vector3(0.85f, 0.12f, 0.7f), M("M_FabricTeal2"), Yaw(0.55f));
                Box("Cushion", g, at(new Vector3(0.45f, 0.47f, -0.05f)), new Vector3(0.85f, 0.12f, 0.7f), M("M_FabricTeal2"), Yaw(0.55f));
                Box("Pillow", g, at(new Vector3(-0.55f, 0.68f, -0.2f)), new Vector3(0.38f, 0.32f, 0.12f), M("M_FabricOrange"), Yaw(0.55f));
                Box("Pillow", g, at(new Vector3(0.6f, 0.68f, -0.22f)), new Vector3(0.4f, 0.34f, 0.12f), M("M_FabricCharcoal"), Yaw(0.55f));
                Box("Base", g, at(new Vector3(0f, 0.045f, 0f)), new Vector3(2f, 0.09f, 0.8f), M("M_Charcoal"), Yaw(0.55f));
            });
            Place("Coffee Table", room, IntroMeshes.Frustum("HexTop042", 6, 0.42f, 0.42f, 0.36f, 0.4f, HexA0, false, true, true), M("M_WalnutDark"), new Vector3(-2.35f, 0f, -1.55f));
            Cyl("Coffee Table Leg", room, new Vector3(-2.35f, 0.18f, -1.55f), 0.05f, 0.36f, M("M_Charcoal"));
            Box("Book", room, new Vector3(-2.3f, 0.425f, -1.5f), new Vector3(0.16f, 0.05f, 0.1f), M("M_Charcoal2"), Yaw(0.4f));
            Cyl("Lamp Base", room, new Vector3(-4.35f, 0.015f, -3.55f), 0.18f, 0.03f, M("M_Charcoal"));
            Box("Lamp Pole", room, new Vector3(-4.35f, 0.8f, -3.55f), new Vector3(0.025f, 1.6f, 0.025f), M("M_Charcoal"));
            Place("Lamp Shade", room, IntroMeshes.Frustum("LampShade", 20, 0.24f, 0.17f, 1.5f, 1.82f, 0f, true, false, false), M("M_LampShade"), new Vector3(-4.35f, 0f, -3.55f), false);
            Plant(room, new Vector3(-4.35f, 0f, -1.2f), 1.1f);
            Plant(room, new Vector3(4.3f, 0f, -3.5f), 1.3f);
            Plant(room, new Vector3(4.25f, 0f, -0.9f), 0.8f);

            // Right: armchair and side table.
            Furniture("Armchair", room, new Vector3(3.3f, 0f, -2.1f), -0.7f, (g, at) =>
            {
                Box("Seat", g, at(new Vector3(0f, 0.25f, 0f)), new Vector3(0.95f, 0.32f, 0.9f), M("M_Leather"), Yaw(-0.7f));
                Box("Back", g, at(new Vector3(0f, 0.65f, -0.36f)), new Vector3(0.95f, 0.55f, 0.2f), M("M_Leather"), Yaw(-0.7f));
                Box("Arm", g, at(new Vector3(-0.42f, 0.5f, 0f)), new Vector3(0.14f, 0.32f, 0.9f), M("M_Leather"), Yaw(-0.7f));
                Box("Arm", g, at(new Vector3(0.42f, 0.5f, 0f)), new Vector3(0.14f, 0.32f, 0.9f), M("M_Leather"), Yaw(-0.7f));
                Box("Pillow", g, at(new Vector3(0f, 0.68f, -0.22f)), new Vector3(0.4f, 0.3f, 0.1f), M("M_FabricCyan"), Yaw(-0.7f));
            });
            Place("Side Table", room, IntroMeshes.Frustum("HexTop024", 6, 0.24f, 0.24f, 0.52f, 0.55f, HexA0, false, true, true), M("M_Walnut"), new Vector3(3.85f, 0f, -1.05f));
            Cyl("Side Table Leg", room, new Vector3(3.85f, 0.26f, -1.05f), 0.03f, 0.52f, M("M_Charcoal"));
            Box("Collectible", room, new Vector3(3.85f, 0.6f, -1.05f), new Vector3(0.1f, 0.1f, 0.1f), M("M_Plastic_Yellow"), Yaw(0.5f));
        }

        private static void FillShelf(Transform room, float sx, float w, float yb, float z)
        {
            float x = sx - w / 2f + 0.1f;
            while (x < sx + w / 2f - 0.12f)
            {
                double kind = random.NextDouble();
                if (kind < 0.72)
                {
                    float bw = 0.035f + (float)random.NextDouble() * 0.03f, bh = 0.26f + (float)random.NextDouble() * 0.12f;
                    Box("Game Box", room, new Vector3(x + bw / 2f, yb + bh / 2f, z + 0.02f), new Vector3(bw, bh, 0.26f), M("M_Box" + random.Next(BoxColours.Length)));
                    x += bw + 0.004f;
                }
                else if (kind < 0.86)
                {
                    float s = 0.12f + (float)random.NextDouble() * 0.06f;
                    Box("Collectible", room, new Vector3(x + s / 2f, yb + s / 2f, z + 0.03f), new Vector3(s, s, s), M("M_Plastic_" + SeatColours[random.Next(4)].Key));
                    x += s + 0.04f;
                }
                else
                {
                    Cyl("Figure Base", room, new Vector3(x + 0.05f, yb + 0.01f, z + 0.05f), 0.04f, 0.02f, M("M_Charcoal2"));
                    Ball("Figure", room, new Vector3(x + 0.05f, yb + 0.1f, z + 0.05f), 0.05f, M("M_Plastic_" + SeatColours[random.Next(4)].Key), new Vector3(0.7f, 1.3f, 0.7f));
                    x += 0.14f;
                }
            }
        }

        private static void Plant(Transform room, Vector3 at, float s)
        {
            Place("Pot", room, IntroMeshes.Frustum("Pot", 16, 0.17f, 0.2f, 0f, 0.38f, 0f, true, true, false), M("M_Pot"), at, true, s);
            for (int k = 0; k < 7; k++)
            {
                float a = k / 7f * Mathf.PI * 2f + (float)random.NextDouble(), r = 0.06f + (float)random.NextDouble() * 0.08f;
                Vector3 p = at + new Vector3(Mathf.Cos(a) * r * s, (0.55f + (float)random.NextDouble() * 0.45f) * s, Mathf.Sin(a) * r * s);
                Ball("Leaves", room, p, (0.13f + (float)random.NextDouble() * 0.08f) * s, M(k % 2 == 0 ? "M_Leaf1" : "M_Leaf2"), new Vector3(1f, 1.4f, 1f));
            }
        }

        private static void BuildTable(Transform table)
        {
            Place("Felt", table, IntroMeshes.Prism("TableFelt", 6, RIn, FeltY - 0.03f, FeltY, HexA0), M("M_Felt"), Vector3.zero);
            Place("Rim", table, IntroMeshes.Ring("TableRim", 6, RIn, ROut, FeltY - 0.11f, FeltY + 0.045f, HexA0), M("M_Walnut"), Vector3.zero);
            Led("Rim Inlay", table, IntroMeshes.FlatRing("RimInlay", 6, RIn + 0.055f, RIn + 0.075f, FeltY + 0.0462f, HexA0), M("M_LedOrange"));
            Led("Felt Line", table, IntroMeshes.FlatRing("FeltLine", 6, RIn - 0.03f, RIn - 0.012f, FeltY + 0.002f, HexA0), M("M_LedCyan"));
            Place("Apron", table, IntroMeshes.Prism("TableApron", 6, 1.12f, 0.56f, FeltY - 0.11f, HexA0, true), M("M_Charcoal"), Vector3.zero);
            Led("Under Glow", table, IntroMeshes.FlatRing("UnderGlow", 6, ROut - 0.06f, ROut - 0.03f, FeltY - 0.111f, HexA0, false), M("M_LedCyan"));
            Place("Pedestal", table, IntroMeshes.Frustum("Pedestal", 6, 0.24f, 0.2f, 0.08f, 0.56f, HexA0, false, true, false), M("M_Charcoal2"), Vector3.zero);
            Place("Base", table, IntroMeshes.Frustum("TableBase", 6, 0.62f, 0.58f, 0f, 0.08f, HexA0, false, true, false), M("M_Charcoal"), Vector3.zero);
            Led("Base Line", table, IntroMeshes.FlatRing("BaseLine", 6, 0.5f, 0.52f, 0.081f, HexA0), M("M_LedCyan"));
            for (int i = 0; i < 6; i++)
            {
                float a = HexA0 + i * Mathf.PI / 3f, r = ROut - 0.075f;
                Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Cyl("Cup Holder", table, p + new Vector3(0f, FeltY + 0.048f, 0f), 0.05f, 0.004f, M("M_Steel"));
                Cyl("Cup Well", table, p + new Vector3(0f, FeltY + 0.034f, 0f), 0.043f, 0.026f, M("M_Black"));
            }

            // Pendant over the table: a hexagon shade with a warm underside.
            Place("Pendant", table, IntroMeshes.Frustum("PendantShade", 6, 0.5f, 0.38f, 2.02f, 2.16f, HexA0, false, true, false), M("M_Charcoal"), Vector3.zero);
            Led("Pendant Light", table, IntroMeshes.FlatRing("PendantUnder", 6, 0f, 0.49f, 2.019f, HexA0, false), M("M_LedWarm"));
            Led("Pendant Trim", table, IntroMeshes.FlatRing("PendantTrim", 6, 0.39f, 0.41f, 2.161f, HexA0), M("M_LedOrange"));
            Box("Pendant Cable", table, new Vector3(0f, 2.68f, 0f), new Vector3(0.012f, 1.04f, 0.012f), M("M_Charcoal"));
        }

        // Seats round the hexagon: red at the front edge (no chair: the camera is there), blue, green, yellow, two empty.
        private static readonly (float angle, string colour)[] Seats =
        {
            (90f, "Red"), (30f, "Blue"), (150f, null), (210f, "Green"), (270f, "Yellow"), (330f, null),
        };

        private static void BuildPieces(Transform pieces)
        {
            string[] cards = { "ConsoleC-A", "ConsoleC-B", "ConsoleC-X", "ConsoleC-Y", "ConsoleC-Up", "ConsoleC-Left" };
            string[] dice = { "Dice_d6", "Dice_d10", "Dice_d12", "Dice_d20", "Dice_d8" };
            int cardIndex = 0;
            foreach ((float angle, string colour) seat in Seats)
            {
                float a = seat.angle * Mathf.Deg2Rad;
                float yaw = Mathf.Atan2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Rad2Deg;
                Quaternion r = Quaternion.Euler(0f, yaw, 0f);
                Vector3 At(float side, float y, float d) => r * new Vector3(side, 0f, d) + new Vector3(0f, y, 0f);
                Transform g = new GameObject("Seat " + seat.angle).transform;
                g.SetParent(pieces, false);

                if (seat.angle != 90f)
                {
                    Vector3 c = At(0f, 0f, Apothem + 0.62f);
                    Box("Chair Seat", g, c + Vector3.up * 0.46f, new Vector3(0.56f, 0.12f, 0.52f), M("M_Leather"), yaw);
                    Box("Chair Back", g, c + r * new Vector3(0f, 0.82f, 0.24f), new Vector3(0.56f, 0.62f, 0.12f), M("M_Leather"), yaw);
                    Box("Chair Arm", g, c + r * new Vector3(0.3f, 0.6f, 0f), new Vector3(0.08f, 0.28f, 0.52f), M("M_Leather"), yaw);
                    Box("Chair Arm", g, c + r * new Vector3(-0.3f, 0.6f, 0f), new Vector3(0.08f, 0.28f, 0.52f), M("M_Leather"), yaw);
                    Box("Chair Post", g, c + Vector3.up * 0.2f, new Vector3(0.1f, 0.4f, 0.1f), M("M_Charcoal"), yaw);
                    Cyl("Chair Foot", g, c + Vector3.up * 0.015f, 0.28f, 0.03f, M("M_Charcoal"));
                }

                float y = FeltY + 0.001f;
                float md = Apothem * 0.74f;
                Box("Mat", g, At(0f, y + 0.002f, md), new Vector3(0.66f, 0.004f, 0.34f), M("M_Charcoal"), yaw);
                Material border = M(seat.colour != null ? "M_Border_" + seat.colour : "M_Border_Empty");
                Led("Mat Edge", g, At(0f, y + 0.0045f, md + 0.165f), new Vector3(0.66f, 0.0012f, 0.012f), border, yaw);
                Led("Mat Edge", g, At(0f, y + 0.0045f, md - 0.165f), new Vector3(0.66f, 0.0012f, 0.012f), border, yaw);
                Led("Mat Edge", g, At(-0.325f, y + 0.0045f, md), new Vector3(0.012f, 0.0012f, 0.34f), border, yaw);
                Led("Mat Edge", g, At(0.325f, y + 0.0045f, md), new Vector3(0.012f, 0.0012f, 0.34f), border, yaw);
                Box("Deck", g, At(0.24f, y + 0.02f, md + 0.02f), new Vector3(0.066f, 0.036f, 0.092f),
                    AssetDatabase.LoadAssetAtPath<Material>(ModelPath + "CARD/Card Prefabs/Materials/backConsoleCard.mat") ?? M("M_Charcoal2"), yaw);
                if (seat.colour == null) continue;

                for (int k = 0; k < 3; k++)
                {
                    string card = cards[cardIndex++ % cards.Length];
                    Model(ModelPath + "CARD/Card Prefabs/Cards/" + card + ".prefab", g, "Card", At(-0.17f + k * 0.1f, y + 0.003f, md + 0.01f), 0.092f, true, yaw, Axis.Z);
                }

                for (int k = 0; k < 2; k++)
                {
                    string die = dice[(cardIndex + k) % dice.Length];
                    Model(ModelPath + "Dice Collection/Prefabs/" + die + ".prefab", g, "Die", At(0.12f + k * 0.05f, y + 0.003f, md - 0.09f), 0.024f, false, yaw + k * 35f + cardIndex * 20f, Axis.None);
                }

                if (seat.colour == "Red")
                {
                    Model(ModelPath + "Controller/Prefab/Controller.prefab", g, "Controller", At(0.47f, y + 0.003f, md + 0.05f), 0.16f, false, yaw + 25f, Axis.None);
                }
            }

            // The Trap Floor tile grid in front of the console, with pawns.
            const float tw = 0.066f;
            for (int ix = 0; ix < 8; ix++)
            {
                for (int iz = 0; iz < 5; iz++)
                {
                    double f = random.NextDouble();
                    string m = f > 0.86 ? "M_TileOrange" : f > 0.78 ? "M_TileCyan" : f > 0.4 ? "M_TileA" : "M_TileB";
                    Box("Tile", pieces, new Vector3((ix - 3.5f) * tw, FeltY + 0.006f, 0.27f + iz * tw), new Vector3(tw * 0.92f, 0.008f, tw * 0.92f), M(m));
                }
            }

            (int, int, string)[] pawns = { (-2, 1, "Red"), (1, 3, "Blue"), (3, 0, "Yellow"), (-3, 4, "Green") };
            foreach ((int ix, int iz, string colour) in pawns)
            {
                Vector3 p = new Vector3((ix - 0.5f) * tw, FeltY, 0.27f + iz * tw);
                Material m = M("M_Plastic_" + colour);
                Cyl("Pawn Base", pieces, p + Vector3.up * 0.013f, 0.02f, 0.006f, m);
                Place("Pawn Body", pieces, IntroMeshes.Frustum("PawnBody", 14, 0.014f, 0.008f, 0.016f, 0.05f, 0f, true, false, false), m, p);
                Ball("Pawn Head", pieces, p + Vector3.up * 0.062f, 0.014f, m, Vector3.one);
            }
        }

        private static Transform PlaceConsole(Transform pieces, out Renderer screen, out Transform focus, out Bounds bounds)
        {
            screen = null;
            GameObject console = Model(ModelPath + "Console3D/Prefab/Console Card Board.prefab", pieces, "Console",
                new Vector3(0f, FeltY + 0.001f, -0.08f), 0.7f, true, 0f, Axis.X);
            focus = new GameObject("Screen Focus").transform;
            focus.SetParent(pieces, false);
            if (console == null)
            {
                bounds = new Bounds(new Vector3(0f, FeltY + 0.04f, -0.08f), new Vector3(0.7f, 0.08f, 0.35f));
                focus.position = bounds.center + Vector3.up * 0.04f;
                return null;
            }

            bounds = BoundsOf(console);
            Material mine = M("M_ConsoleScreen");
            foreach (Renderer r in console.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = r.sharedMaterials;
                bool isScreen = r.gameObject.name == "Screen";
                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] != null && shared[i].name.Contains("Screen Default"))
                    {
                        shared[i] = mine;
                        isScreen = true;
                    }
                }

                if (!isScreen) continue;
                if (r.gameObject.name == "Screen") for (int i = 0; i < shared.Length; i++) shared[i] = mine;
                r.sharedMaterials = shared;
                if (screen == null || r.bounds.size.sqrMagnitude > screen.bounds.size.sqrMagnitude) screen = r;
            }

            focus.position = screen != null
                ? new Vector3(screen.bounds.center.x, screen.bounds.max.y + 0.002f, screen.bounds.center.z)
                : bounds.center + Vector3.up * (bounds.extents.y + 0.002f);
            if (screen == null) Debug.LogWarning("Intro: the console's screen was not found (no 'Screen' part); its glow will not change.");
            return console.transform;
        }

        private static void BuildLights(Transform lights, Transform focus, List<Light> pulse, out Light consoleLight, out Light sweep)
        {
            Light spot = MakeLight("Pendant Spot", lights, LightType.Spot, new Vector3(0f, 2.0f, 0f), Hex("#ffb46b"), 3.4f, 4.5f);
            spot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            spot.spotAngle = 112f;
            spot.innerSpotAngle = 62f;
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.9f;
            spot.shadowNearPlane = 0.2f;
            UniversalAdditionalLightData data = spot.GetUniversalAdditionalLightData();
            if (data != null) data.additionalLightsShadowResolutionTier = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh;

            MakeLight("Floor Lamp", lights, LightType.Point, new Vector3(-4.35f, 1.62f, -3.55f), Hex("#ffb46b"), 2.2f, 5f);
            pulse.Add(MakeLight("LED Column L", lights, LightType.Point, new Vector3(-1.55f, 1.7f, -4f), Hex("#06eff0"), 1.5f, 3.8f));
            pulse.Add(MakeLight("LED Column R", lights, LightType.Point, new Vector3(1.55f, 1.7f, -4f), Hex("#06eff0"), 1.5f, 3.8f));
            pulse.Add(MakeLight("Media LED", lights, LightType.Point, new Vector3(0f, 0.25f, -3.7f), Hex("#06eff0"), 1.0f, 2.6f));
            MakeLight("Shelf Glow L", lights, LightType.Point, new Vector3(-2.75f, 1.3f, -3.75f), Hex("#f2762e"), 1.2f, 2.8f);
            MakeLight("Shelf Glow R", lights, LightType.Point, new Vector3(2.75f, 1.3f, -3.75f), Hex("#f2762e"), 1.2f, 2.8f);
            pulse.Add(MakeLight("Table Under Glow", lights, LightType.Point, new Vector3(0f, 0.45f, 0f), Hex("#06eff0"), 0.7f, 2.3f));
            consoleLight = MakeLight("Console Glow", lights, LightType.Point, focus.position + Vector3.up * 0.12f, Hex("#06eff0"), 0.25f, 1.1f);
            sweep = MakeLight("Button Sweep", lights, LightType.Point, focus.position + Vector3.up * 0.05f, Hex("#06eff0"), 0f, 0.35f);

            GameObject probe = new GameObject("Reflection Probe");
            probe.transform.SetParent(lights, false);
            probe.transform.position = new Vector3(0f, 1.4f, -0.5f);
            ReflectionProbe rp = probe.AddComponent<ReflectionProbe>();
            rp.mode = ReflectionProbeMode.Realtime;
            rp.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            rp.size = new Vector3(10f, 3.3f, 10.5f);
            rp.boxProjection = true;
            rp.resolution = 256;
            rp.intensity = 0.9f;
        }

        private static Light MakeLight(string name, Transform parent, LightType type, Vector3 position, Color color, float intensity, float range)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            Light l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        private static Camera BuildCamera()
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 42f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#030405");
            cam.allowHDR = true;
            go.transform.position = new Vector3(0f, 2.02f, 3.3f);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.78f, -0.22f) - go.transform.position);
            go.AddComponent<AudioListener>();
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            return cam;
        }

        private static Volume BuildVolume()
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath) != null) AssetDatabase.DeleteAsset(VolumePath);
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);
            T Add<T>() where T : VolumeComponent
            {
                T c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }

            Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            Bloom bloom = Add<Bloom>();
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.75f);
            bloom.highQualityFiltering.Override(true);
            Vignette vignette = Add<Vignette>();
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);
            DepthOfField dof = Add<DepthOfField>();
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(4.6f);
            dof.gaussianEnd.Override(9f);
            dof.gaussianMaxRadius.Override(1f);
            dof.highQualitySampling.Override(true);
            ColorAdjustments colour = Add<ColorAdjustments>();
            colour.postExposure.Override(0.35f);
            colour.contrast.Override(8f);
            colour.saturation.Override(6f);
            FilmGrain grain = Add<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);
            Add<ChromaticAberration>().intensity.Override(0f);
            Add<LensDistortion>().intensity.Override(0f);
            MotionBlur blur = Add<MotionBlur>();
            blur.intensity.Override(0f);
            blur.quality.Override(MotionBlurQuality.Medium);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            GameObject go = new GameObject("Post Processing");
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
            return volume;
        }

        private static void BuildDust()
        {
            GameObject go = new GameObject("Dust In The Light");
            go.transform.position = new Vector3(0f, 1.45f, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 10f;
            main.startLifetime = 12f;
            main.startSpeed = 0.02f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.01f);
            main.startColor = new Color(1f, 0.85f, 0.7f, 0.5f);
            main.maxParticles = 240;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 20f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.4f, 1.2f, 2.4f);
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.3f;
            ParticleSystem.ColorOverLifetimeModule fadeInOut = ps.colorOverLifetime;
            fadeInOut.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fadeInOut.color = g;
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = M("M_Dust");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ---------- words ----------

        private struct UiParts
        {
            public RectTransform Prompt;
            public CanvasGroup PromptGroup;
            public RectTransform Lift;
            public Text Word;
            public RectTransform Line;
            public CanvasGroup Hud;
            public Button Exit;
        }

        private static void Ui(out UiParts parts)
        {
            Font bold = AssetDatabase.LoadAssetAtPath<Font>(FontPath + "Oxanium-Bold.ttf");
            Font medium = AssetDatabase.LoadAssetAtPath<Font>(FontPath + "Oxanium-Medium.ttf");
            Color words = Hex("#e8eced"), muted = Hex("#9ba4a7"), cyan = Hex("#06eff0"), orange = Hex("#f2762e");

            GameObject canvasGo = new GameObject("Intro UI", typeof(RectTransform));
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            RectTransform root = (RectTransform)canvasGo.transform;

            GameObject events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();

            RectTransform hudRect = Rect("HUD", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            CanvasGroup hud = hudRect.gameObject.AddComponent<CanvasGroup>();
            RectTransform brand = Rect("Brand", hudRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -36f), new Vector2(520f, 40f));
            Image dot = Rect("Dot", brand, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(14f, 14f)).gameObject.AddComponent<Image>();
            dot.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ConsoleCards/Content/UI/Figma/Components/cyan_on ellipse.png");
            dot.color = dot.sprite != null ? Color.white : cyan;
            dot.raycastTarget = false;
            Label(Rect("Name", brand, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(-26f, 0f)), bold, 24, words,
                TextAnchor.MiddleLeft, "CONSOLE CARDS <color=#" + ColorUtility.ToHtmlStringRGB(orange) + ">///</color>");
            Label(Rect("Hint", hudRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 30f)), medium, 15, muted,
                TextAnchor.MiddleCenter, "CLICK THE CONSOLE   ·   ENTER   ·   A / START");

            RectTransform exitRect = Rect("Exit", hudRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 32f), new Vector2(150f, 52f));
            Image plate = exitRect.gameObject.AddComponent<Image>();
            plate.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ConsoleCards/Content/UI/Figma/Components/Button_disabled.png");
            plate.type = plate.sprite != null && plate.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            plate.color = plate.sprite != null ? Color.white : Hex("#1c1f1f");
            Button exit = exitRect.gameObject.AddComponent<Button>();
            exit.targetGraphic = plate;
            Label(Rect("Label", exitRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), bold, 18, words, TextAnchor.MiddleCenter, "EXIT");

            // ENTER over the console: orange caps, the word, a cyan line, POWER ON. The director moves it each frame.
            RectTransform prompt = Rect("Prompt", root, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(520f, 120f));
            CanvasGroup promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
            promptGroup.blocksRaycasts = false;
            RectTransform lift = Rect("Lift", prompt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Text word = Label(Rect("Word", lift, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(360f, 64f)),
                bold, 46, words, TextAnchor.MiddleCenter, "E N T E R");
            Shadow shadow = word.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -3f);
            Label(Rect("Caps Left", lift, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-178f, 22f), new Vector2(60f, 40f)), bold, 22, orange, TextAnchor.MiddleRight, "///");
            Label(Rect("Caps Right", lift, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(178f, 22f), new Vector2(60f, 40f)), bold, 22, orange, TextAnchor.MiddleLeft, "///");
            RectTransform line = Rect("Line", lift, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(240f, 3f));
            Image lineImage = line.gameObject.AddComponent<Image>();
            lineImage.color = cyan;
            lineImage.raycastTarget = false;
            Label(Rect("Sub", lift, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(400f, 24f)), medium, 13, muted, TextAnchor.MiddleCenter, "P O W E R   O N");

            parts = new UiParts { Prompt = prompt, PromptGroup = promptGroup, Lift = lift, Word = word, Line = line, Hud = hud, Exit = exit };
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = min;
            r.anchorMax = max;
            r.pivot = pivot;
            if (min == Vector2.zero && max == Vector2.one)
            {
                r.offsetMin = pos;
                r.offsetMax = size;
            }
            else
            {
                r.anchoredPosition = pos;
                r.sizeDelta = size;
            }

            return r;
        }

        private static Text Label(RectTransform r, Font font, int size, Color color, TextAnchor anchor, string text)
        {
            Text t = r.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.text = text;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ---------- objects ----------

        private enum Axis
        {
            None,
            X,
            Z,
        }

        private static Transform Group(string name) => new GameObject(name).transform;

        private static Quaternion Yaw(float radians) => Quaternion.Euler(0f, radians * Mathf.Rad2Deg, 0f);

        private static GameObject Place(string name, Transform parent, Mesh mesh, Material mat, Vector3 position, bool shadows = true, float scale = 1f)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static void Led(string name, Transform parent, Mesh mesh, Material mat) => Place(name, parent, mesh, mat, Vector3.zero, false);

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material mat, bool shadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, float yawDegrees = 0f)
            => Box(name, parent, center, size, mat, Quaternion.Euler(0f, yawDegrees, 0f));

        private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, Quaternion rotation, bool shadows = true)
        {
            GameObject go = Primitive(PrimitiveType.Cube, name, parent, mat, shadows);
            go.transform.position = center;
            go.transform.rotation = rotation;
            go.transform.localScale = size;
            return go;
        }

        private static void Led(string name, Transform parent, Vector3 center, Vector3 size, Material mat, float yawDegrees = 0f)
            => Box(name, parent, center, size, mat, Quaternion.Euler(0f, yawDegrees, 0f), false);

        private static GameObject Cyl(string name, Transform parent, Vector3 center, float radius, float height, Material mat, Vector3? euler = null)
        {
            GameObject go = Primitive(PrimitiveType.Cylinder, name, parent, mat);
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            return go;
        }

        private static GameObject Ball(string name, Transform parent, Vector3 center, float radius, Material mat, Vector3 squash)
        {
            GameObject go = Primitive(PrimitiveType.Sphere, name, parent, mat);
            go.transform.position = center;
            go.transform.localScale = Vector3.Scale(squash, Vector3.one * radius * 2f);
            return go;
        }

        // A flat picture (TV, posters) facing the camera.
        private static void Picture(string name, Transform parent, Vector3 center, Vector2 size, Material mat)
        {
            GameObject go = Primitive(PrimitiveType.Quad, name, parent, mat, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        private static void Furniture(string name, Transform parent, Vector3 at, float yawRadians, System.Action<Transform, System.Func<Vector3, Vector3>> build)
        {
            Transform g = new GameObject(name).transform;
            g.SetParent(parent, false);
            Quaternion r = Yaw(yawRadians);
            build(g, local => at + r * local);
        }

        /// <summary>
        /// One of the project's own models, without its game behaviour: laid flat (thinnest side up) when asked,
        /// turned so its long side follows <paramref name="longAxis"/>, scaled so that side is <paramref name="size"/>
        /// metres, and stood on <paramref name="at"/>.
        /// </summary>
        private static GameObject Model(string path, Transform parent, string name, Vector3 at, float size, bool flat, float yaw, Axis longAxis)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("Intro: model not found: " + path);
                return null;
            }

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            Strip(go);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            Quaternion lie = Quaternion.identity;
            Bounds b = BoundsOf(go);
            if (flat)
            {
                Vector3 s = b.size;
                if (s.x <= s.y && s.x <= s.z) lie = Quaternion.Euler(0f, 0f, 90f);
                else if (s.z <= s.x && s.z <= s.y) lie = Quaternion.Euler(90f, 0f, 0f);
                go.transform.rotation = lie;
                b = BoundsOf(go);
            }

            if (longAxis == Axis.X && b.size.z > b.size.x) lie = Quaternion.Euler(0f, 90f, 0f) * lie;
            if (longAxis == Axis.Z && b.size.x > b.size.z) lie = Quaternion.Euler(0f, 90f, 0f) * lie;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * lie;
            b = BoundsOf(go);
            float longest = longAxis == Axis.None ? Mathf.Max(b.size.x, b.size.y, b.size.z) : Mathf.Max(b.size.x, b.size.z);
            if (longest > 1e-6f) go.transform.localScale *= size / longest;
            b = BoundsOf(go);
            go.transform.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
            return go;
        }

        // Keeps only what draws: no colliders, bodies or game scripts in the intro.
        private static void Strip(GameObject go)
        {
            foreach (Component c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer || c is LODGroup) continue;
                if (c is Collider || c is Rigidbody || c is MonoBehaviour || c is Animator || c is AudioSource) Object.DestroyImmediate(c);
            }
        }

        private static Bounds BoundsOf(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static void Set(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) Debug.LogWarning("Intro: IntroDirector has no field " + field);
            else p.objectReferenceValue = value;
        }

        private static void PutFirstInBuild()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
