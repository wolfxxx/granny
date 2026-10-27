using UnityEngine;

namespace GrannyWorld
{
    /// <summary>Spinning, bobbing golden star. Collected when Granny touches it.</summary>
    [RequireComponent(typeof(SphereCollider))]
    public class Collectible : MonoBehaviour
    {
        public float spinSpeed = 140f;
        public float bobHeight = 0.2f;
        public float bobSpeed = 2.2f;

        Vector3 basePosition;
        float phase;

        void Start()
        {
            basePosition = transform.position;
            phase = Random.value * Mathf.PI * 2f;
            GameHUD.RegisterStar();
        }

        void Update()
        {
            float t = Time.time;
            transform.SetPositionAndRotation(
                basePosition + Vector3.up * (Mathf.Sin(t * bobSpeed + phase) * bobHeight),
                Quaternion.Euler(0f, t * spinSpeed + phase * Mathf.Rad2Deg, 0f));
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<GrannyController>() == null) return;
            GameHUD.CollectStar();
            Fx.Sparkles(transform.position, new Color(1f, 0.85f, 0.3f), 28);
            GameAudio.Play(ProceduralAudio.Collect, 0.7f, 0f);
            Destroy(gameObject);
        }
    }
}
