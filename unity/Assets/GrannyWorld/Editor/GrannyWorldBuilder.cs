using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GrannyWorld.EditorTools
{
    /// <summary>
    /// Builds "Sporty Granny's Island": a sculpted terrain island with meadows, a pond, a mountain,
    /// a cottage, forests, bouncy mushrooms, a spiral trail of floating islands, golden stars,
    /// stylized water, sky, lighting and post-processing, plus the playable Granny and camera.
    /// Everything is generated, so the menu item can be re-run at any time.
    /// </summary>
    public static class GrannyWorldBuilder
    {
        const string Root = "Assets/GrannyWorld";
        const string Gen = Root + "/Generated";
        const string ScenePath = Root + "/Scenes/GrannyWorld.unity";

        const float TerrainSize = 260f;
        const float TerrainHeight = 60f;
        const float WaterLevel = 6f;
        static readonly Vector2 CottagePos = new Vector2(0f, 8f);
        static readonly Vector2 PondPos = new Vector2(-45f, 25f);
        static readonly Vector2 MountainPos = new Vector2(55f, 50f);
        static readonly Vector2 TrailCenter = new Vector2(-25f, 30f);
        const float TrailRadius = 22f;

        static System.Random rng;
        static Terrain terrain;
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static readonly List<Vector2> keepClear = new List<Vector2>();   // xz points trees must avoid
        static Transform envRoot;

        [MenuItem("Tools/Granny World/Build Everything")]
        public static string BuildEverything()
        {
            GrannyAnimationBuilder.Build();
            return Build();
        }

        [MenuItem("Tools/Granny World/Build World")]
        public static string Build()
        {
            rng = new System.Random(20261027);
            mats.Clear();
            keepClear.Clear();
            EnsureFolder(Gen);

            CreateMaterials();
            ConfigurePropImports();
            ConfigureGrannyImport();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            envRoot = new GameObject("Environment").transform;

            BuildTerrain();
            BuildWater();
            var sun = BuildLighting();
            BuildPostProcessing();

            BuildCottageArea();
            var trailEnd = BuildSkyTrail();
            BuildPond();
            BuildLandmarks();
            ScatterVegetation();
            BuildClouds();

            var player = BuildPlayer(out var cam);
            BuildGameSystems(player, cam);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Lightmapping.lightingSettings = CreateLightingSettings();
            Lightmapping.BakeAsync(); // environment-only bake: ambient probe + skybox reflections
            AssetDatabase.SaveAssets();
            return $"Built {ScenePath}. Stars: {Object.FindObjectsByType<Collectible>().Length}. Trail top at {trailEnd}";
        }

        // ================================================================ materials
        static void CreateMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            void M(string name, Color c, float smooth = 0.12f, float metal = 0f)
            {
                var m = LoadOrCreate(Root + "/Materials/" + name + ".mat", lit);
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metal);
                m.enableInstancing = true;
                mats[name] = m;
            }
            // Same names as the Blender materials (minus the GW_ prefix)
            M("Bark", new Color(0.36f, 0.22f, 0.13f));
            M("Leaf", new Color(0.32f, 0.62f, 0.2f));
            M("LeafDark", new Color(0.16f, 0.45f, 0.2f));
            M("LeafLight", new Color(0.62f, 0.76f, 0.25f));
            M("Pine", new Color(0.12f, 0.36f, 0.24f));
            M("Rock", new Color(0.55f, 0.54f, 0.52f), 0.2f);
            M("Moss", new Color(0.38f, 0.56f, 0.2f));
            M("CapRed", new Color(0.9f, 0.16f, 0.13f), 0.45f);
            M("CapSpot", new Color(0.98f, 0.96f, 0.92f), 0.3f);
            M("Stem", new Color(0.95f, 0.9f, 0.78f));
            M("Petal", new Color(1f, 0.52f, 0.72f), 0.2f);
            M("PetalY", new Color(1f, 0.85f, 0.25f), 0.2f);
            M("Center", new Color(1f, 0.72f, 0.1f));
            M("Grass", new Color(0.36f, 0.64f, 0.2f));
            M("Wall", new Color(0.97f, 0.92f, 0.82f));
            M("Roof", new Color(0.78f, 0.3f, 0.24f), 0.25f);
            M("Wood", new Color(0.5f, 0.32f, 0.18f));
            M("Window", new Color(0.6f, 0.85f, 1f), 0.9f);
            M("Chimney", new Color(0.6f, 0.4f, 0.34f));
            M("Dirt", new Color(0.5f, 0.36f, 0.22f));
            M("Gold", new Color(1f, 0.78f, 0.18f), 0.75f);
            M("Cloud", new Color(1f, 1f, 1f), 0f);

            var gold = mats["Gold"];
            gold.EnableKeyword("_EMISSION");
            gold.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.08f) * 1.4f);
            gold.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            var window = mats["Window"];
            window.EnableKeyword("_EMISSION");
            window.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.5f) * 0.6f);
            var cloud = mats["Cloud"];
            cloud.EnableKeyword("_EMISSION");
            cloud.SetColor("_EmissionColor", new Color(0.45f, 0.47f, 0.52f));

            var water = LoadOrCreate(Root + "/Materials/Water.mat", AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Shaders/StylizedWater.shader"));
            mats["Water"] = water;

            foreach (var m in mats.Values) EditorUtility.SetDirty(m);
        }

        static void ConfigurePropImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Models/Props" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var kv in mats)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "GW_" + kv.Key), kv.Value);
                importer.SaveAndReimport();
            }
        }

        static void ConfigureGrannyImport()
        {
            const string texDir = Root + "/Models/Granny/Textures/";
            const string fbxPath = Root + "/Models/Granny/SportyGranny.fbx";
            // Fresh checkouts don't contain the Mixamo textures: pull them out of the FBX.
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "FitnessGrandma_diffuse.jpg") == null)
            {
                ((ModelImporter)AssetImporter.GetAtPath(fbxPath)).ExtractTextures(texDir.TrimEnd('/'));
                AssetDatabase.Refresh();
            }
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(texDir + "FitnessGrandma_normal.jpg");
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }
            var faceImporter = (TextureImporter)AssetImporter.GetAtPath(texDir + "FitnessGrandma_FaceAnimMap.png");
            if (faceImporter != null && !faceImporter.alphaIsTransparency)
            {
                faceImporter.alphaIsTransparency = true;
                faceImporter.SaveAndReimport();
            }

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var body = LoadOrCreate(Root + "/Materials/GrannyBody.mat", lit);
            body.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "FitnessGrandma_diffuse.jpg"));
            body.SetColor("_BaseColor", Color.white);
            body.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "FitnessGrandma_normal.jpg"));
            body.EnableKeyword("_NORMALMAP");
            body.SetFloat("_Smoothness", 0.3f);

            var face = LoadOrCreate(Root + "/Materials/GrannyFace.mat", lit);
            face.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "FitnessGrandma_FaceAnimMap.png"));
            face.SetColor("_BaseColor", Color.white);
            face.SetFloat("_Smoothness", 0.35f);
            face.SetFloat("_AlphaClip", 1f);
            face.SetFloat("_Cutoff", 0.5f);
            face.EnableKeyword("_ALPHATEST_ON");
            face.SetOverrideTag("RenderType", "TransparentCutout");
            face.renderQueue = (int)RenderQueue.AlphaTest;

            var lens = LoadOrCreate(Root + "/Materials/GrannyLens.mat", lit);
            lens.SetColor("_BaseColor", new Color(0.8f, 0.95f, 1f, 0.18f));
            lens.SetFloat("_Smoothness", 0.95f);
            MakeTransparent(lens);

            foreach (var m in new[] { body, face, lens }) EditorUtility.SetDirty(m);

            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Grandma_MAT"), body);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Lens_MAT"), lens);
            foreach (var n in new[] { "FitGrandma_Brows_MAT1", "FitGrandma_Eyes_MAT1", "FitGrandma_Mouth_MAT1" })
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), face);
            importer.SaveAndReimport();
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        // ================================================================ terrain
        static float HeightAt(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z) / 115f;
            float warp = (Fbm(x * 0.012f, z * 0.012f, 3) - 0.5f) * 0.35f;
            float island = SmoothStep(1.0f, 0.62f, r + warp);

            float meadow = 8.6f + (Fbm(x * 0.02f + 100f, z * 0.02f + 30f, 4) - 0.5f) * 4.5f;
            float hills = Mathf.Pow(Fbm(x * 0.009f + 50f, z * 0.009f + 50f, 4), 2.2f) * 16f;
            float mountain = 26f * Mathf.Exp(-((x - MountainPos.x) * (x - MountainPos.x) + (z - MountainPos.y) * (z - MountainPos.y)) / (2f * 24f * 24f));
            mountain *= 0.85f + 0.3f * Fbm(x * 0.05f, z * 0.05f, 3);
            float h = Mathf.Lerp(1.0f, meadow + hills * 0.6f + mountain, island);

            // flatten the cottage garden
            float village = SmoothStep(30f, 14f, Vector2.Distance(new Vector2(x, z), CottagePos + new Vector2(0f, -4f)));
            h = Mathf.Lerp(h, 9f, village);
            // pond bowl
            float pond = SmoothStep(17f, 5f, Vector2.Distance(new Vector2(x, z), PondPos));
            h = Mathf.Lerp(h, 3.4f, pond);
            return h;
        }

        static readonly Vector2[][] Paths =
        {
            new[] { new Vector2(0f, 1f), new Vector2(0f, -14f), new Vector2(-12f, -24f), new Vector2(-30f, -18f), new Vector2(-40f, -2f), new Vector2(-38f, 10f) },
            new[] { new Vector2(0f, -14f), new Vector2(16f, -20f), new Vector2(34f, -6f), new Vector2(40f, 18f), new Vector2(46f, 32f) },
            new[] { new Vector2(0f, -14f), new Vector2(4f, -40f), new Vector2(-6f, -62f), new Vector2(-4f, -80f) },
        };

        static float PathDistance(float x, float z)
        {
            float best = float.MaxValue;
            var p = new Vector2(x, z);
            foreach (var path in Paths)
                for (int i = 0; i < path.Length - 1; i++)
                {
                    Vector2 a = path[i], b = path[i + 1];
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                    best = Mathf.Min(best, Vector2.Distance(p, a + (b - a) * t));
                }
            return best;
        }

        static void BuildTerrain()
        {
            const int res = 513;
            var td = new TerrainData { heightmapResolution = res };
            td.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
            float half = TerrainSize * 0.5f;
            var heights = new float[res, res];
            for (int zi = 0; zi < res; zi++)
            for (int xi = 0; xi < res; xi++)
            {
                float x = xi / (float)(res - 1) * TerrainSize - half;
                float z = zi / (float)(res - 1) * TerrainSize - half;
                heights[zi, xi] = Mathf.Clamp01(HeightAt(x, z) / TerrainHeight);
            }
            td.SetHeights(0, 0, heights);

            // --- textures & layers
            var layers = new[]
            {
                MakeLayer("Grass", new Color(0.36f, 0.62f, 0.22f), new Color(0.46f, 0.7f, 0.25f), 6f, 0.25f),
                MakeLayer("GrassDark", new Color(0.24f, 0.5f, 0.2f), new Color(0.33f, 0.58f, 0.22f), 7f, 0.2f),
                MakeLayer("Sand", new Color(0.9f, 0.82f, 0.6f), new Color(0.96f, 0.9f, 0.72f), 5f, 0.12f),
                MakeLayer("Rock", new Color(0.46f, 0.44f, 0.42f), new Color(0.62f, 0.6f, 0.57f), 8f, 0.35f),
                MakeLayer("Path", new Color(0.62f, 0.48f, 0.32f), new Color(0.72f, 0.58f, 0.4f), 4f, 0.25f),
            };
            td.terrainLayers = layers;
            const int aRes = 512;
            td.alphamapResolution = aRes;
            var alpha = new float[aRes, aRes, layers.Length];
            for (int zi = 0; zi < aRes; zi++)
            for (int xi = 0; xi < aRes; xi++)
            {
                float u = xi / (float)(aRes - 1), v = zi / (float)(aRes - 1);
                float x = u * TerrainSize - half, z = v * TerrainSize - half;
                float h = td.GetInterpolatedHeight(u, v);
                float slope = td.GetSteepness(u, v);
                float sand = SmoothStep(7.9f, 7.0f, h);
                float rock = SmoothStep(26f, 38f, slope) * (1f - sand);
                float path = SmoothStep(2.6f, 1.4f, PathDistance(x, z)) * (1f - rock) * (1f - sand);
                float rest = Mathf.Max(0f, 1f - sand - rock - path);
                float dark = Mathf.Clamp01((Fbm(x * 0.04f, z * 0.04f, 3) - 0.45f) * 3f + SmoothStep(14f, 24f, h) * 0.6f);
                alpha[zi, xi, 0] = rest * (1f - dark);
                alpha[zi, xi, 1] = rest * dark;
                alpha[zi, xi, 2] = sand;
                alpha[zi, xi, 3] = rock;
                alpha[zi, xi, 4] = path;
            }
            td.SetAlphamaps(0, 0, alpha);

            // --- grass & flower details (GPU-instanced meshes)
            const int dRes = 512;
            td.SetDetailResolution(dRes, 32);
            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            td.detailPrototypes = new[]
            {
                Detail("GrassTuft", 0.8f, 1.5f),
                Detail("Flower_0", 0.8f, 1.3f),
                Detail("Flower_1", 0.8f, 1.3f),
            };
            var grass = new int[dRes, dRes];
            var pink = new int[dRes, dRes];
            var yellow = new int[dRes, dRes];
            for (int zi = 0; zi < dRes; zi++)
            for (int xi = 0; xi < dRes; xi++)
            {
                float u = (xi + 0.5f) / dRes, v = (zi + 0.5f) / dRes;
                float x = u * TerrainSize - half, z = v * TerrainSize - half;
                float h = td.GetInterpolatedHeight(u, v);
                if (h < 8f || td.GetSteepness(u, v) > 28f || PathDistance(x, z) < 2.2f) continue;
                if (Vector2.Distance(new Vector2(x, z), CottagePos) < 4.5f) continue;
                float n = Fbm(x * 0.06f + 7f, z * 0.06f, 3);
                grass[zi, xi] = n > 0.38f ? (n > 0.55f ? 3 : 2) : 1;
                float f = Fbm(x * 0.05f + 300f, z * 0.05f + 300f, 3);
                if (f > 0.62f && rng.NextDouble() < 0.5) pink[zi, xi] = 1;
                else if (f < 0.3f && rng.NextDouble() < 0.35) yellow[zi, xi] = 1;
            }
            td.SetDetailLayer(0, 0, 0, grass);
            td.SetDetailLayer(0, 0, 1, pink);
            td.SetDetailLayer(0, 0, 2, yellow);

            string tdPath = Gen + "/IslandTerrain.asset";
            AssetDatabase.DeleteAsset(tdPath);
            AssetDatabase.CreateAsset(td, tdPath);
            // Splat textures of a script-created TerrainData are not saved unless added as sub-assets.
            foreach (var splat in td.alphamapTextures) AssetDatabase.AddObjectToAsset(splat, td);
            EditorUtility.SetDirty(td);

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Island Terrain";
            go.transform.SetParent(envRoot);
            go.transform.position = new Vector3(-half, 0f, -half);
            terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = LoadOrCreate(Gen + "/TerrainLit.mat", Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            terrain.detailObjectDistance = 90f;
            terrain.detailObjectDensity = 1f;
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 250f;
            terrain.drawInstanced = true;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        static DetailPrototype Detail(string prop, float min, float max)
        {
            return new DetailPrototype
            {
                prototype = Prop(prop),
                usePrototypeMesh = true,
                useInstancing = true,
                renderMode = DetailRenderMode.VertexLit,
                minWidth = min, maxWidth = max,
                minHeight = min, maxHeight = max,
                noiseSpread = 0.4f,
                healthyColor = Color.white,
                dryColor = Color.white,
            };
        }

        static TerrainLayer MakeLayer(string name, Color a, Color b, float tile, float contrast)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = TileNoise(x, y, size, 8) * 0.55f + TileNoise(x, y, size, 32) * 0.3f + TileNoise(x, y, size, 64) * 0.15f;
                n = Mathf.Clamp01(0.5f + (n - 0.5f) * (1f + contrast * 4f));
                px[y * size + x] = Color.Lerp(a, b, n);
            }
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{Gen}/Tex_{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.anisoLevel = 4;
            ti.SaveAndReimport();

            string layerPath = $"{Gen}/Layer_{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, layerPath); }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = 0.05f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        // ================================================================ water & sky
        static void BuildWater()
        {
            const int cells = 300;
            const float size = 3000f;
            var verts = new Vector3[(cells + 1) * (cells + 1)];
            var tris = new int[cells * cells * 6];
            for (int z = 0, i = 0; z <= cells; z++)
            for (int x = 0; x <= cells; x++, i++)
                verts[i] = new Vector3(x / (float)cells * size - size / 2f, 0f, z / (float)cells * size - size / 2f);
            for (int z = 0, t = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                int i = z * (cells + 1) + x;
                tris[t++] = i; tris[t++] = i + cells + 1; tris[t++] = i + 1;
                tris[t++] = i + 1; tris[t++] = i + cells + 1; tris[t++] = i + cells + 2;
            }
            var mesh = new Mesh { name = "WaterGrid", indexFormat = IndexFormat.UInt32, vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, 4f, size));
            string path = Gen + "/WaterGrid.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject("Sea");
            go.transform.SetParent(envRoot);
            go.transform.position = new Vector3(0f, WaterLevel, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mats["Water"];
            mr.shadowCastingMode = ShadowCastingMode.Off;

            // sandy sea floor beyond the terrain so there is no visible edge under the water
            var seabed = GameObject.CreatePrimitive(PrimitiveType.Plane);
            seabed.name = "Seabed";
            seabed.transform.SetParent(envRoot);
            seabed.transform.position = new Vector3(0f, 0.95f, 0f);
            seabed.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
            Object.DestroyImmediate(seabed.GetComponent<Collider>());
            var sand = LoadOrCreate(Root + "/Materials/Seabed.mat", Shader.Find("Universal Render Pipeline/Lit"));
            sand.SetColor("_BaseColor", new Color(0.9f, 0.83f, 0.62f));
            sand.SetFloat("_Smoothness", 0.05f);
            var seabedRenderer = seabed.GetComponent<MeshRenderer>();
            seabedRenderer.sharedMaterial = sand;
            seabedRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Light BuildLighting()
        {
            var sky = LoadOrCreate(Gen + "/Sky.mat", Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_SunSizeConvergence", 4f);
            sky.SetFloat("_AtmosphereThickness", 1.0f);
            sky.SetColor("_SkyTint", new Color(0.46f, 0.5f, 0.6f));
            sky.SetColor("_GroundColor", new Color(0.62f, 0.74f, 0.86f));
            sky.SetFloat("_Exposure", 1.3f);
            RenderSettings.skybox = sky;

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sunGo.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.7f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.68f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.33f, 0.34f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.83f, 0.94f);
            RenderSettings.fogStartDistance = 120f;
            RenderSettings.fogEndDistance = 650f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            return sun;
        }

        static LightingSettings CreateLightingSettings()
        {
            string path = Gen + "/GrannyLighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (ls == null) { ls = new LightingSettings(); AssetDatabase.CreateAsset(ls, path); }
            ls.bakedGI = false;
            ls.realtimeGI = false;
            EditorUtility.SetDirty(ls);
            return ls;
        }

        static void BuildPostProcessing()
        {
            string path = Gen + "/GrannyPost.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.7f);
            bloom.scatter.Override(0.65f);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.contrast.Override(12f);
            color.saturation.Override(16f);
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(3f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);

            var go = new GameObject("Post Processing");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // ================================================================ set pieces
        static void BuildCottageArea()
        {
            var parent = Group("Cottage Garden");
            var cottage = Place("Cottage", CottagePos.x, CottagePos.y, 180f, 1.1f, parent, sink: 0.05f);
            cottage.AddComponent<MeshCollider>();
            keepClear.Add(CottagePos);

            // garden fence with a gate opening towards the spawn point
            float x0 = -10f, x1 = 10f, z0 = CottagePos.y - 9f, z1 = CottagePos.y + 7f;
            for (float x = x0 + 1.1f; x < x1; x += 2.2f)
            {
                if (Mathf.Abs(x) > 1.8f) Fence(x, z0, 0f, parent);
                Fence(x, z1, 0f, parent);
            }
            for (float z = z0 + 1.1f; z < z1; z += 2.2f)
            {
                Fence(x0, z, 90f, parent);
                Fence(x1, z, 90f, parent);
            }
            // stumps as garden seats & a bouncy mushroom to reach the roof
            Place("Stump", -5f, CottagePos.y - 5f, 0f, 0.6f, parent).AddComponent<MeshCollider>();
            Place("Stump", -6.4f, CottagePos.y - 3.6f, 0f, 0.45f, parent).AddComponent<MeshCollider>();
            Mushroom(6.5f, CottagePos.y - 4.5f, 1f, 6.5f, parent);
            Place("Bush_0", -8.5f, CottagePos.y + 5.5f, 30f, 1.1f, parent);
            Place("Bush_1", 8.5f, CottagePos.y + 5.5f, 70f, 1.0f, parent);
            Place("Bush_1", -3f, CottagePos.y + 3.5f, 0f, 0.8f, parent);

            // star on the roof ridge
            var bounds = cottage.GetComponent<MeshRenderer>().bounds;
            Star(new Vector3(-1.2f, bounds.max.y - 0.2f, CottagePos.y + 0.2f));
        }

        static void Fence(float x, float z, float yaw, Transform parent)
        {
            var f = Place("Fence", x, z, yaw, 1f, parent, sink: 0.1f);
            f.AddComponent<BoxCollider>();
        }

        static Vector3 BuildSkyTrail()
        {
            var parent = Group("Sky Trail");
            const int count = 14;
            float step = 8.5f / TrailRadius;
            float angle = -70f * Mathf.Deg2Rad;
            float top = Ground(TrailCenter.x + Mathf.Cos(angle) * TrailRadius, TrailCenter.y + Mathf.Sin(angle) * TrailRadius) + 1.5f;
            Vector3 last = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                float a = angle + i * step;
                var xz = TrailCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * TrailRadius;
                bool final = i == count - 1;
                string mesh = final || i % 2 == 1 ? "FloatIsland_1" : "FloatIsland_0";
                var island = Instantiate(mesh, parent);
                island.transform.position = new Vector3(xz.x, top, xz.y);
                island.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                island.transform.localScale = final ? new Vector3(2f, 1.6f, 2f) : Vector3.one;
                island.AddComponent<MeshCollider>();
                MarkStatic(island);
                keepClear.Add(xz);
                last = island.transform.position;

                bool mushroom = i == 4 || i == 9;
                if (mushroom) Mushroom(xz.x, xz.y, 0.6f, 7.5f, parent, top);
                if (!mushroom && (i % 2 == 1 || i == 6)) Star(island.transform.position + Vector3.up * 1.2f);
                if (final)
                {
                    for (int s = 0; s < 3; s++)
                    {
                        float sa = s * Mathf.PI * 2f / 3f;
                        Star(island.transform.position + new Vector3(Mathf.Cos(sa) * 3f, 1.2f, Mathf.Sin(sa) * 3f));
                    }
                    Place("Tree_Round_2", xz.x, xz.y, 0f, 0.8f, parent, top);
                    Place("Bush_1", xz.x + 3.8f, xz.y + 1.5f, 0f, 0.8f, parent, top);
                }
                top += mushroom ? 5f : 1.5f;
            }
            return last;
        }

        static void BuildPond()
        {
            var parent = Group("Pond");
            int n = 8;
            for (int i = 0; i < n; i++)
            {
                float x = PondPos.x - 12f + i * 3.3f;
                float z = PondPos.y + Mathf.Sin(i * 0.9f) * 1.6f;
                var stump = Instantiate("Stump", parent);
                float topY = WaterLevel + 0.45f + (i % 2) * 0.35f;
                stump.transform.position = new Vector3(x, topY, z);
                stump.transform.localScale = new Vector3(0.75f, 10f, 0.75f);
                stump.AddComponent<MeshCollider>();
                MarkStatic(stump);
                if (i == 3 || i == 6) Star(new Vector3(x, topY + 1.1f, z));
            }
            // reeds & rocks around the shore
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.3f;
                float r = 14.5f + (float)rng.NextDouble() * 2f;
                Place(i % 3 == 0 ? "Rock_1" : "Bush_0", PondPos.x + Mathf.Cos(a) * r, PondPos.y + Mathf.Sin(a) * r,
                    (float)rng.NextDouble() * 360f, 0.5f + (float)rng.NextDouble() * 0.4f, parent);
            }
            keepClear.Add(PondPos);
        }

        static void BuildLandmarks()
        {
            var parent = Group("Landmarks");
            // summit star + slope stars
            Star(new Vector3(MountainPos.x, Ground(MountainPos.x, MountainPos.y) + 1.3f, MountainPos.y));
            Star(new Vector3(42f, Ground(42f, 36f) + 1.3f, 36f));
            Star(new Vector3(64f, Ground(64f, 34f) + 1.3f, 34f));

            // giant rocks with stars on top
            var rocks = new[] { new Vector2(28f, 26f), new Vector2(18f, 48f), new Vector2(-20f, -40f) };
            for (int i = 0; i < rocks.Length; i++)
            {
                var rock = Place("Rock_" + i, rocks[i].x, rocks[i].y, i * 70f, 3.2f, parent, sink: 0.6f);
                rock.AddComponent<MeshCollider>();
                keepClear.Add(rocks[i]);
                Star(new Vector3(rocks[i].x, rock.GetComponent<MeshRenderer>().bounds.max.y + 1f, rocks[i].y));
            }

            // beach stars around the island
            foreach (float deg in new[] { 200f, 300f, 20f, 120f })
            {
                float a = deg * Mathf.Deg2Rad;
                for (float r = 95f; r > 60f; r -= 2f)
                {
                    float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                    if (Ground(x, z) > WaterLevel + 0.8f) { Star(new Vector3(x, Ground(x, z) + 1.2f, z)); break; }
                }
            }

            // meadow mushrooms with stars floating high above them
            var shrooms = new[] { new Vector2(22f, -18f), new Vector2(-26f, -30f), new Vector2(8f, -50f), new Vector2(36f, 6f) };
            for (int i = 0; i < shrooms.Length; i++)
            {
                Mushroom(shrooms[i].x, shrooms[i].y, 1.1f, 9f, parent);
                if (i < 2) Star(new Vector3(shrooms[i].x, Ground(shrooms[i].x, shrooms[i].y) + 10f, shrooms[i].y));
            }
        }

        static void ScatterVegetation()
        {
            var parent = Group("Vegetation");
            var trees = new List<Vector2>();
            bool Free(Vector2 p, float minTree, float clear)
            {
                foreach (var t in trees) if ((t - p).sqrMagnitude < minTree * minTree) return false;
                foreach (var k in keepClear) if ((k - p).sqrMagnitude < clear * clear) return false;
                return PathDistance(p.x, p.y) > 3f && Vector2.Distance(p, CottagePos) > 13f;
            }

            for (int attempt = 0, placed = 0; attempt < 6000 && placed < 230; attempt++)
            {
                var p = new Vector2(R(-115f, 115f), R(-115f, 115f));
                float h = Ground(p.x, p.y);
                if (h < 8.2f || Slope(p.x, p.y) > 24f) continue;
                bool high = h > 15f;
                // forests cluster where low-frequency noise is high
                float forest = Fbm(p.x * 0.018f + 500f, p.y * 0.018f, 3);
                if (forest < 0.45f && rng.NextDouble() > 0.15) continue;
                if (!Free(p, 4.2f, 7f)) continue;
                trees.Add(p);
                placed++;
                string kind = high || (forest > 0.62f && rng.NextDouble() < 0.5)
                    ? "Tree_Pine_" + rng.Next(2)
                    : "Tree_Round_" + rng.Next(3);
                float scale = R(0.8f, 1.35f);
                var tree = Place(kind, p.x, p.y, R(0f, 360f), scale, parent, sink: 0.25f);
                var capsule = tree.AddComponent<CapsuleCollider>();
                capsule.radius = 0.4f;
                capsule.height = 4f;
                capsule.center = new Vector3(0f, 2f, 0f);
            }

            for (int i = 0, attempt = 0; i < 110 && attempt < 3000; attempt++)
            {
                var p = new Vector2(R(-110f, 110f), R(-110f, 110f));
                if (Ground(p.x, p.y) < 8f || Slope(p.x, p.y) > 30f || !Free(p, 1.5f, 5f)) continue;
                Place("Bush_" + rng.Next(2), p.x, p.y, R(0f, 360f), R(0.6f, 1.2f), parent, sink: 0.15f);
                i++;
            }

            for (int i = 0, attempt = 0; i < 45 && attempt < 3000; attempt++)
            {
                var p = new Vector2(R(-110f, 110f), R(-110f, 110f));
                float h = Ground(p.x, p.y);
                if (h < 6.6f || !Free(p, 2.5f, 5f)) continue;
                var rock = Place("Rock_" + rng.Next(3), p.x, p.y, R(0f, 360f), R(0.4f, 1.3f), parent, sink: 0.2f);
                rock.AddComponent<MeshCollider>();
                i++;
            }

            for (int i = 0, attempt = 0; i < 12 && attempt < 2000; attempt++)
            {
                var p = new Vector2(R(-90f, 90f), R(-90f, 90f));
                if (Ground(p.x, p.y) < 8.5f || !Free(p, 3f, 6f)) continue;
                Place("Stump", p.x, p.y, R(0f, 360f), R(0.5f, 0.8f), parent).AddComponent<MeshCollider>();
                i++;
            }
        }

        static void BuildClouds()
        {
            var parent = Group("Clouds");
            for (int i = 0; i < 22; i++)
            {
                var cloud = Instantiate("Bush_" + (i % 2), parent);
                float a = R(0f, Mathf.PI * 2f), r = R(30f, 230f);
                cloud.transform.position = new Vector3(Mathf.Cos(a) * r, R(55f, 85f), Mathf.Sin(a) * r);
                cloud.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                float s = R(6f, 13f);
                cloud.transform.localScale = new Vector3(s * R(1f, 1.8f), s * 0.55f, s);
                var mr = cloud.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mats["Cloud"];
                mr.shadowCastingMode = ShadowCastingMode.Off;
                cloud.AddComponent<Drift>().velocity = new Vector3(R(1f, 2.2f), 0f, R(0.2f, 0.6f));
            }
        }

        // ================================================================ player & systems
        static GameObject BuildPlayer(out Camera cam)
        {
            Vector3 spawn = new Vector3(0f, Ground(0f, -4f) + 0.1f, -4f);

            var player = new GameObject("Granny") { tag = "Player", layer = 2 };
            player.transform.SetPositionAndRotation(spawn, Quaternion.identity);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.6f;
            cc.radius = 0.32f;
            cc.center = new Vector3(0f, 0.83f, 0f);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.05f;
            cc.minMoveDistance = 0f;

            var pivot = new GameObject("FlipPivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 0.8f, 0f);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/Granny/SportyGranny.fbx"));
            model.transform.SetParent(pivot, false);
            model.transform.localPosition = new Vector3(0f, -0.85f, 0f);
            model.transform.localScale = Vector3.one * 0.85f;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Root + "/Animations/Granny.controller");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;

            var controller = player.AddComponent<GrannyController>();
            controller.animator = animator;
            controller.flipPivot = pivot;
            controller.drownHeight = WaterLevel - 1.4f;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetPositionAndRotation(spawn + new Vector3(0f, 2.8f, -6.5f), Quaternion.Euler(14f, 0f, 0f));
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1500f;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.AddComponent<AudioListener>();
            var orbit = camGo.AddComponent<OrbitCamera>();
            orbit.target = player.transform;
            controller.cameraTransform = camGo.transform;
            return player;
        }

        static void BuildGameSystems(GameObject player, Camera cam)
        {
            var systems = new GameObject("Game Systems");
            var fx = systems.AddComponent<GameFx>();
            fx.particleShader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Shaders/SimpleParticle.shader");
            fx.pollenFollow = player.transform;
            systems.AddComponent<GameHUD>();
            cam.gameObject.AddComponent<AudioSource>();
            cam.gameObject.AddComponent<GameAudio>();
        }

        // ================================================================ placement helpers
        static GameObject Prop(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Props/{name}.fbx");

        static GameObject Instantiate(string prop, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Prop(prop), parent);
            return go;
        }

        static GameObject Place(string prop, float x, float z, float yaw, float scale, Transform parent, float? y = null, float sink = 0f)
        {
            var go = Instantiate(prop, parent);
            go.transform.position = new Vector3(x, (y ?? Ground(x, z)) - sink * scale, z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            MarkStatic(go);
            return go;
        }

        static void Mushroom(float x, float z, float scale, float bounce, Transform parent, float? y = null)
        {
            var go = Instantiate("Mushroom", parent);
            go.transform.position = new Vector3(x, y ?? Ground(x, z) - 0.05f, z);
            go.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshCollider>();
            go.AddComponent<BouncePad>().bounceHeight = bounce;
            keepClear.Add(new Vector2(x, z));
        }

        static void Star(Vector3 position)
        {
            var parent = GameObject.Find("Stars")?.transform ?? new GameObject("Stars").transform;
            var go = Instantiate("Star", parent);
            go.name = "Star";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 1.1f;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.6f;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            go.AddComponent<Collectible>();
        }

        static Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(envRoot);
            return t;
        }

        static void MarkStatic(GameObject go) => GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

        static float Ground(float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;

        static float Slope(float x, float z)
        {
            var td = terrain.terrainData;
            return td.GetSteepness((x + TerrainSize / 2f) / TerrainSize, (z + TerrainSize / 2f) / TerrainSize);
        }

        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // ================================================================ math & asset helpers
        static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x + i * 17.3f, y + i * 31.7f) * amp;
                norm += amp;
                x *= 2.03f; y *= 2.03f; amp *= 0.5f;
            }
            return sum / norm;
        }

        /// <summary>Periodic value noise so terrain textures tile seamlessly.</summary>
        static float TileNoise(int x, int y, int size, int period)
        {
            float fx = x / (float)size * period, fy = y / (float)size * period;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
            float H(int a, int b)
            {
                a = ((a % period) + period) % period; b = ((b % period) + period) % period;
                uint h = (uint)(a * 374761393 + b * 668265263 + period * 2246822519);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
            return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), tx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), tx), ty);
        }

        static Material LoadOrCreate(string path, Shader shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
