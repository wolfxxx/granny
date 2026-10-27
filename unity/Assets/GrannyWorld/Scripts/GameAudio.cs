using System.Collections;
using UnityEngine;

namespace GrannyWorld
{
    /// <summary>Plays one-shot sound effects and a gentle wind + birdsong ambience.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class GameAudio : MonoBehaviour
    {
        public float ambienceVolume = 0.25f;

        static GameAudio instance;
        AudioSource sfx;

        void Awake()
        {
            instance = this;
            sfx = GetComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
        }

        void Start()
        {
            var wind = gameObject.AddComponent<AudioSource>();
            wind.clip = ProceduralAudio.Wind;
            wind.loop = true;
            wind.volume = ambienceVolume * 0.6f;
            wind.Play();
            StartCoroutine(Birdsong());
        }

        IEnumerator Birdsong()
        {
            var bird = gameObject.AddComponent<AudioSource>();
            bird.spatialBlend = 0f;
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(2.5f, 7f));
                int calls = Random.Range(1, 4);
                bird.panStereo = Random.Range(-0.8f, 0.8f);
                var clip = ProceduralAudio.Chirps[Random.Range(0, ProceduralAudio.Chirps.Length)];
                for (int i = 0; i < calls; i++)
                {
                    bird.pitch = Random.Range(0.9f, 1.15f);
                    bird.PlayOneShot(clip, ambienceVolume * Random.Range(0.4f, 0.9f));
                    yield return new WaitForSeconds(clip.length + Random.Range(0.03f, 0.15f));
                }
            }
        }

        public static void Play(AudioClip clip, float volume, float pitchVariation)
        {
            if (instance == null || clip == null) return;
            instance.sfx.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            instance.sfx.PlayOneShot(clip, volume);
        }
    }

    /// <summary>Synthesises all game sounds at runtime so the project needs no audio files.</summary>
    public static class ProceduralAudio
    {
        const int SampleRate = 44100;
        static readonly System.Random rng = new System.Random(1234);

        static AudioClip jump, doubleJump, collect, boing, land, splash, wind, fanfare;
        static AudioClip[] chirps;

        public static AudioClip Jump => jump ??= Make("Jump", 0.18f, t =>
        {
            float ph = Sweep(t, 0.18f, 260f, 640f);
            return (Mathf.Sin(ph) + 0.25f * Mathf.Sin(2f * ph)) * Env(t, 0.18f, 0.005f) * 0.6f;
        });

        public static AudioClip DoubleJump => doubleJump ??= Make("DoubleJump", 0.3f, t =>
        {
            float ph = Sweep(t, 0.3f, 480f, 1250f);
            float whoosh = Noise() * 0.35f * Mathf.Sin(Mathf.PI * t / 0.3f);
            return (Mathf.Sin(ph) * 0.5f + whoosh) * Env(t, 0.3f, 0.01f);
        });

        public static AudioClip Collect => collect ??= Make("Collect", 0.55f, t =>
        {
            float f = t < 0.08f ? 987.8f : 1318.5f;
            float local = t < 0.08f ? t : t - 0.08f;
            float decay = Mathf.Exp(-local * (t < 0.08f ? 18f : 7f));
            float tone = Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * f * t);
            return tone * decay * Mathf.Clamp01(local * 400f) * 0.45f;
        });

        public static AudioClip Boing => boing ??= Make("Boing", 0.45f, t =>
        {
            float ph = Sweep(t, 0.45f, 120f, 460f) + 2.2f * Mathf.Sin(2f * Mathf.PI * 16f * t);
            return Mathf.Sin(ph) * Mathf.Exp(-t * 6f) * Mathf.Clamp01(t * 300f) * 0.7f;
        });

        public static AudioClip Land => land ??= Make("Land", 0.2f, t =>
        {
            float thump = Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 28f);
            return (thump + LowNoise(0.15f) * Mathf.Exp(-t * 35f) * 0.8f) * 0.8f;
        });

        public static AudioClip Splash => splash ??= Make("Splash", 0.6f, t =>
            LowNoise(Mathf.Lerp(0.6f, 0.08f, t / 0.6f)) * Mathf.Exp(-t * 6f) * Mathf.Clamp01(t * 80f) * 1.2f);

        public static AudioClip Fanfare => fanfare ??= Make("Fanfare", 1.2f, t =>
        {
            float[] notes = { 523.3f, 659.3f, 784f, 1046.5f };
            int i = Mathf.Min((int)(t / 0.15f), 3);
            float local = t - i * 0.15f;
            float f = notes[i];
            float decay = i == 3 ? Mathf.Exp(-local * 2.5f) : Mathf.Exp(-local * 10f);
            return (Mathf.Sin(2f * Mathf.PI * f * t) + 0.4f * Mathf.Sin(4f * Mathf.PI * f * t)) * decay * Mathf.Clamp01(local * 300f) * 0.35f;
        });

        public static AudioClip[] Chirps => chirps ??= new[]
        {
            Make("Chirp0", 0.12f, t => Mathf.Sin(Sweep(t, 0.12f, 2600f, 4300f) + 3f * Mathf.Sin(2f * Mathf.PI * 60f * t)) * Mathf.Sin(Mathf.PI * t / 0.12f) * 0.3f),
            Make("Chirp1", 0.09f, t => Mathf.Sin(Sweep(t, 0.09f, 4100f, 2900f)) * Mathf.Sin(Mathf.PI * t / 0.09f) * 0.3f),
            Make("Chirp2", 0.2f, t => Mathf.Sin(2f * Mathf.PI * (3200f + 600f * Mathf.Sin(2f * Mathf.PI * 25f * t)) * t) * Mathf.Sin(Mathf.PI * t / 0.2f) * 0.22f),
        };

        public static AudioClip Wind => wind ??= MakeWind();

        static AudioClip Make(string name, float duration, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[n];
            lowState = 0f;
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)SampleRate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeWind()
        {
            const float duration = 8f;
            int n = (int)(duration * SampleRate);
            var data = new float[n];
            float brown = 0f;
            for (int i = 0; i < n; i++)
            {
                brown = Mathf.Clamp(brown + Noise() * 0.02f, -1f, 1f) * 0.998f;
                float t = i / (float)SampleRate;
                float gust = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * t / duration) * Mathf.Sin(2f * Mathf.PI * 3f * t / duration);
                data[i] = brown * gust;
            }
            // crossfade the tail into the head so the loop is seamless
            int fade = SampleRate / 2;
            for (int i = 0; i < fade; i++)
            {
                float a = i / (float)fade;
                data[i] = Mathf.Lerp(data[n - fade + i], data[i], a);
            }
            var clip = AudioClip.Create("Wind", n - fade, 1, SampleRate, false);
            var trimmed = new float[n - fade];
            System.Array.Copy(data, trimmed, n - fade);
            clip.SetData(trimmed, 0);
            return clip;
        }

        static float Sweep(float t, float duration, float f0, float f1) =>
            2f * Mathf.PI * (f0 * t + (f1 - f0) * t * t / (2f * duration));

        static float Env(float t, float duration, float attack) =>
            Mathf.Clamp01(t / attack) * Mathf.Pow(Mathf.Clamp01(1f - t / duration), 2f);

        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

        static float lowState;
        static float LowNoise(float cutoff)
        {
            lowState += (Noise() - lowState) * cutoff;
            return lowState;
        }
    }
}
