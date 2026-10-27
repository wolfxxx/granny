using UnityEngine;

namespace GrannyWorld
{
    /// <summary>
    /// Scene component that owns the particle materials; <see cref="Fx"/> uses it to spawn
    /// one-shot effects (dust, sparkles, splashes) and ambient floating pollen.
    /// </summary>
    public class GameFx : MonoBehaviour
    {
        public Shader particleShader;
        public Transform pollenFollow;

        internal static GameFx Instance;
        internal Material additive;
        internal Material alphaBlended;

        void Awake()
        {
            if (particleShader == null)
            {
                Debug.LogWarning("GameFx: no particle shader assigned, effects disabled.");
                return;
            }
            Instance = this;
            var tex = CreateSoftDot(64);
            additive = new Material(particleShader) { name = "FxAdditive" };
            additive.SetTexture("_BaseMap", tex);
            additive.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            additive.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            alphaBlended = new Material(particleShader) { name = "FxAlpha" };
            alphaBlended.SetTexture("_BaseMap", tex);
            alphaBlended.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            alphaBlended.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        void Start()
        {
            if (pollenFollow != null) Fx.CreatePollen(pollenFollow);
        }

        static Texture2D CreateSoftDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
