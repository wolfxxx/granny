using UnityEngine;

namespace GrannyWorld
{
    /// <summary>Spawns particle effects using the materials owned by <see cref="GameFx"/>.</summary>
    public static class Fx
    {
        public static void Sparkles(Vector3 position, Color color, int count)
        {
            var ps = OneShot("Sparkles", position, true, 1.2f);
            if (ps == null) return;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
            main.gravityModifier = 0.4f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            FadeAndShrink(ps);
            ps.Emit(count);
        }

        public static void Dust(Vector3 position, int count)
        {
            var ps = OneShot("Dust", position + Vector3.up * 0.1f, false, 1.2f);
            if (ps == null) return;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startColor = new Color(0.86f, 0.8f, 0.68f, 0.55f);
            main.gravityModifier = -0.03f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.6f;
            limit.dampen = 0.15f;
            FadeAndGrow(ps);
            ps.Emit(count);
        }

        public static void Splash(Vector3 position)
        {
            var ps = OneShot("Splash", position, false, 1.6f);
            if (ps == null) return;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startColor = new Color(0.85f, 0.95f, 1f, 0.8f);
            main.gravityModifier = 1.6f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            FadeAndShrink(ps);
            ps.Emit(40);
        }

        public static ParticleSystem CreateRunDust(Transform parent)
        {
            var fx = GameFx.Instance;
            if (fx == null) return null;
            var ps = Create("RunDust", fx.alphaBlended);
            ps.transform.SetParent(parent, false);
            ps.transform.localPosition = new Vector3(0f, 0.1f, -0.1f);
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startColor = new Color(0.86f, 0.8f, 0.68f, 0.45f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            FadeAndGrow(ps);
            ps.Play();
            return ps;
        }

        public static void CreatePollen(Transform follow)
        {
            var fx = GameFx.Instance;
            if (fx == null) return;
            var ps = Create("Pollen", fx.additive);
            ps.transform.SetParent(follow, false);
            ps.transform.localPosition = Vector3.up * 3f;
            var main = ps.main;
            main.loop = true;
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new Color(1f, 0.95f, 0.7f, 0.8f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 10f, 40f);
            var emission = ps.emission;
            emission.rateOverTime = 30f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.25f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) }
            });
            ps.Play();
        }

        static ParticleSystem OneShot(string name, Vector3 position, bool additive, float lifetime)
        {
            var fx = GameFx.Instance;
            if (fx == null) return null;
            var ps = Create(name, additive ? fx.additive : fx.alphaBlended);
            ps.transform.position = position;
            var main = ps.main;
            main.loop = false;
            var emission = ps.emission;
            emission.enabled = false;
            Object.Destroy(ps.gameObject, lifetime + 0.5f);
            return ps;
        }

        static ParticleSystem Create(string name, Material material)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        static void FadeAndShrink(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) }
            });
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
        }

        static void FadeAndGrow(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            });
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
        }
    }
}
