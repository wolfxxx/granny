using UnityEngine;

namespace GrannyWorld
{
    /// <summary>Slowly moves clouds across the sky, wrapping around the world.</summary>
    public class Drift : MonoBehaviour
    {
        public Vector3 velocity = new Vector3(1.5f, 0f, 0.4f);
        public float wrapRadius = 260f;
        public float bobAmplitude = 0.6f;

        float baseY;
        float phase;

        void Start()
        {
            baseY = transform.position.y;
            phase = Random.value * 10f;
        }

        void Update()
        {
            Vector3 p = transform.position + velocity * Time.deltaTime;
            if (p.x > wrapRadius) p.x -= wrapRadius * 2f;
            if (p.z > wrapRadius) p.z -= wrapRadius * 2f;
            p.y = baseY + Mathf.Sin(Time.time * 0.3f + phase) * bobAmplitude;
            transform.position = p;
        }
    }
}
