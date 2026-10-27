using UnityEngine;

namespace GrannyWorld
{
    /// <summary>Springy mushroom: launches Granny upward and wobbles.</summary>
    public class BouncePad : MonoBehaviour
    {
        public float bounceHeight = 7f;
        public float wobbleDuration = 0.6f;

        Vector3 baseScale;
        float wobbleTime = -1f;
        float lastLaunchTime = -10f;

        void Awake() => baseScale = transform.localScale;

        public void Launch(GrannyController granny)
        {
            if (Time.time - lastLaunchTime < 0.2f) return;
            lastLaunchTime = Time.time;
            granny.Bounce(bounceHeight);
            wobbleTime = 0f;
            GameAudio.Play(ProceduralAudio.Boing, 0.6f, 0.1f);
            Fx.Sparkles(granny.transform.position, new Color(1f, 0.55f, 0.6f), 12);
        }

        void Update()
        {
            if (wobbleTime < 0f) return;
            wobbleTime += Time.deltaTime;
            float t = wobbleTime / wobbleDuration;
            if (t >= 1f)
            {
                transform.localScale = baseScale;
                wobbleTime = -1f;
                return;
            }
            float s = Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * 0.25f;
            transform.localScale = new Vector3(baseScale.x * (1f + s), baseScale.y * (1f - s), baseScale.z * (1f + s));
        }
    }
}
