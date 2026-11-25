using UnityEngine;
using UnityEngine.Rendering;

namespace Arena.FX
{
    /// <summary>
    /// Neon light trail (light cycle style) behind the object, made with a Particle System:
    /// glowing particles emitted with the distance travelled, linked by a ribbon trail.
    /// The particle system is not parented, so the trail survives the object (projectile destroyed on impact).
    /// Can also add a small point light following the object.
    /// </summary>
    public class NeonTrail : MonoBehaviour
    {
        [System.Serializable]
        private class Settings
        {
            [ColorUsage(true, true)] public Color Color = new Color(0f, 2.4f, 3f, 1f);

            [Tooltip("Width of the trail at its start, in meters.")]
            [Min(0.01f)] public float Width = 0.35f;

            [Tooltip("Lifetime of the trail, in seconds: the longer, the longer the trail.")]
            [Min(0.05f)] public float Lifetime = 0.6f;

            [Tooltip("Particles emitted per meter travelled.")]
            [Min(0.5f)] public float ParticlesPerMeter = 8f;

            [Tooltip("Position of the trail, relative to the object.")]
            public Vector3 Offset = new Vector3(0f, 0.3f, 0f);

            [Tooltip("Glowing dots along the trail, in addition to the ribbon.")]
            public bool Sparks = true;

            [Header("Light")]
            public bool AddLight = true;
            public Color LightColor = new Color(0.2f, 0.85f, 1f);
            [Min(0f)] public float LightIntensity = 2f;
            [Min(0f)] public float LightRange = 4f;
        }

        [System.Serializable]
        private struct References
        {
            [Tooltip("URP Particles/Unlit shader.")]
            public Shader ParticleShader;
            public Texture2D DotTexture;
            public Texture2D RibbonTexture;
        }

        #region FIELDS

        [SerializeField] private Settings _settings;
        [SerializeField] private References _references;

        private ParticleSystem _particles;
        private Light _light;

        #endregion

        #region UNITY LIFECYCLES

        void Awake()
        {
            CreateParticles();

            if (_settings.AddLight)
                CreateLight();
        }

        void OnEnable()
        {
            if (!_particles)
                return;

            // Teleport without drawing a line from the previous position
            _particles.transform.position = transform.TransformPoint(_settings.Offset);
            _particles.Clear();
            _particles.Play();
        }

        void OnDisable()
        {
            if (_particles)
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void LateUpdate()
        {
            if (_particles)
                _particles.transform.position = transform.TransformPoint(_settings.Offset);
        }

        void OnDestroy()
        {
            // Let the trail fade out instead of disappearing with the object
            if (_particles)
            {
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(_particles.gameObject, _settings.Lifetime + 0.1f);
            }
        }

        #endregion

        #region PRIVATE METHODS

        private void CreateParticles()
        {
            GameObject go = new GameObject($"{name}_NeonTrail");
            go.transform.position = transform.TransformPoint(_settings.Offset);

            _particles = go.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = _settings.Lifetime;
            main.startSpeed = 0f;
            main.startSize = _settings.Width;
            main.startColor = Color.white;
            main.maxParticles = 1000;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = _settings.ParticlesPerMeter;

            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = false;

            // Fades and thins out with time
            ParticleSystem.ColorOverLifetimeModule color = _particles.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = _particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

            // Ribbon linking the particles: the light trail itself
            ParticleSystem.TrailModule trails = _particles.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.Ribbon;
            trails.ribbonCount = 1;
            trails.dieWithParticles = true;
            trails.sizeAffectsWidth = true;
            trails.inheritParticleColor = true;
            trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            trails.minVertexDistance = 0.05f;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.trailMaterial = CreateAdditiveMaterial(_references.RibbonTexture, _settings.Color);
            renderer.material = _settings.Sparks ? CreateAdditiveMaterial(_references.DotTexture, _settings.Color * 0.6f) : null;
            if (!_settings.Sparks)
                renderer.renderMode = ParticleSystemRenderMode.None;

            _particles.Play();
        }

        private Material CreateAdditiveMaterial(Texture2D texture, Color color)
        {
            Shader shader = _references.ParticleShader ? _references.ParticleShader : Shader.Find("Universal Render Pipeline/Particles/Unlit");
            Material material = new Material(shader) { name = "NeonTrail (runtime)" };

            // Transparent + additive, as set by the URP material inspector
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;

            if (texture)
                material.SetTexture("_BaseMap", texture);

            material.SetColor("_BaseColor", color);
            return material;
        }

        private void CreateLight()
        {
            GameObject go = new GameObject("NeonGlow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = _settings.Offset + Vector3.up * 0.6f;

            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = _settings.LightColor;
            _light.intensity = _settings.LightIntensity;
            _light.range = _settings.LightRange;
            _light.shadows = LightShadows.None;
        }

        #endregion
    }
}
