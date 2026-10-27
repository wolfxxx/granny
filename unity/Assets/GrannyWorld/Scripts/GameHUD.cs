using UnityEngine;
using UnityEngine.InputSystem;

namespace GrannyWorld
{
    /// <summary>Star counter, title card, controls help and the "all stars" celebration.</summary>
    public class GameHUD : MonoBehaviour
    {
        public string title = "Sporty Granny's Island";

        static int totalStars;
        static int collectedStars;
        static float lastCollectTime = -10f;
        static float allCollectedTime = -1f;

        bool showHelp = true;
        Texture2D starIcon;
        GUIStyle counterStyle, titleStyle, helpStyle, bannerStyle;

        public static void RegisterStar() => totalStars++;

        public static void CollectStar()
        {
            collectedStars++;
            lastCollectTime = Time.time;
            if (collectedStars >= totalStars)
            {
                allCollectedTime = Time.time;
                GameAudio.Play(ProceduralAudio.Fanfare, 0.8f, 0f);
            }
        }

        void Awake()
        {
            totalStars = 0;
            collectedStars = 0;
            lastCollectTime = -10f;
            allCollectedTime = -1f;
            starIcon = CreateStarIcon(64);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame) showHelp = !showHelp;
        }

        void OnGUI()
        {
            EnsureStyles();
            float scale = Screen.height / 1080f;
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;

            // Star counter with a little pop when a star is collected
            float pop = 1f + 0.35f * Mathf.Exp(-(Time.time - lastCollectTime) * 6f);
            float iconSize = 64f * pop;
            GUI.DrawTexture(new Rect(36f - (iconSize - 64f) / 2f, 30f - (iconSize - 64f) / 2f, iconSize, iconSize), starIcon);
            Shadowed(new Rect(112f, 34f, 400f, 60f), $"{collectedStars} / {totalStars}", counterStyle);

            // Title card for the first few seconds
            float tt = Time.timeSinceLevelLoad;
            float titleAlpha = Mathf.Clamp01(tt / 0.8f) * Mathf.Clamp01((5.5f - tt) / 1.2f);
            if (titleAlpha > 0f)
            {
                var c = GUI.color; GUI.color = new Color(1f, 1f, 1f, titleAlpha);
                Shadowed(new Rect(0f, h * 0.18f, w, 120f), title, titleStyle);
                GUI.color = c;
            }

            if (showHelp)
            {
                const string help = "WASD / Left stick  –  move\nShift  –  sprint      Ctrl  –  walk\nSpace / A  –  jump  (press again in the air for a flip!)\nMouse / Right stick  –  look      Scroll  –  zoom\nRed mushrooms are bouncy.  Collect every golden star!\nEsc  –  free the cursor      H  –  hide this help";
                var box = new Rect(30f, h - 250f, 720f, 220f);
                var c = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.35f);
                GUI.DrawTexture(box, Texture2D.whiteTexture);
                GUI.color = c;
                GUI.Label(new Rect(box.x + 24f, box.y + 16f, box.width - 48f, box.height - 32f), help, helpStyle);
            }

            if (allCollectedTime > 0f && Time.time - allCollectedTime < 7f)
            {
                float a = Mathf.Clamp01((Time.time - allCollectedTime) * 3f) * Mathf.Clamp01((7f - (Time.time - allCollectedTime)) / 1f);
                var c = GUI.color; GUI.color = new Color(1f, 1f, 1f, a);
                Shadowed(new Rect(0f, h * 0.4f, w, 120f), "All stars collected!\nGranny is unstoppable!", bannerStyle);
                GUI.color = c;
            }

            GUI.matrix = matrix;
        }

        static void Shadowed(Rect r, string text, GUIStyle style)
        {
            var col = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.55f * GUI.color.a);
            GUI.Label(new Rect(r.x + 3f, r.y + 3f, r.width, r.height), text, style);
            style.normal.textColor = col;
            GUI.Label(r, text, style);
        }

        void EnsureStyles()
        {
            if (counterStyle != null) return;
            counterStyle = new GUIStyle(GUI.skin.label) { fontSize = 52, fontStyle = FontStyle.Bold };
            counterStyle.normal.textColor = new Color(1f, 0.93f, 0.6f);
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 84, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = Color.white;
            helpStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, wordWrap = true };
            helpStyle.normal.textColor = new Color(1f, 1f, 1f, 0.95f);
            bannerStyle = new GUIStyle(titleStyle) { fontSize = 64 };
            bannerStyle.normal.textColor = new Color(1f, 0.88f, 0.35f);
        }

        static Texture2D CreateStarIcon(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = (i % 2 == 0 ? 0.48f : 0.21f) * size;
                pts[i] = new Vector2(size / 2f + Mathf.Cos(ang) * r, size / 2f + Mathf.Sin(ang) * r);
            }
            const int ss = 4; // supersampling for smooth edges
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                    if (InPolygon(new Vector2(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss), pts)) inside++;
                float a = inside / (float)(ss * ss);
                float shade = Mathf.Lerp(0.85f, 1f, y / (float)size);
                tex.SetPixel(x, y, new Color(1f * shade, 0.8f * shade, 0.2f * shade, a));
            }
            tex.Apply();
            return tex;
        }

        static bool InPolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }
    }
}
