using Arena.Characters;
using Arena.Combat;
using UnityEngine;

namespace Arena.UI
{
    /// <summary>
    /// Drives the full screen damage vignette (DAMAGE material, used by the Full Screen Pass of the renderer)
    /// from the player's health: a flash on each hit, a permanent vignette at low health, full on death.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class DamageVignette : MonoBehaviour
    {
        [System.Serializable]
        private class Settings
        {
            [Tooltip("Vignette intensity right after a hit.")]
            [Min(0f)] public float HitIntensity = 1.5f;

            [Tooltip("Duration of the fade after a hit, in seconds.")]
            [Min(0.01f)] public float HitFadeDuration = 0.6f;

            [Tooltip("Health ratio under which the vignette stays visible (0.4 = 40 %).")]
            [Range(0f, 1f)] public float LowHealthThreshold = 0.4f;

            [Tooltip("Vignette intensity when the health is almost empty.")]
            [Min(0f)] public float LowHealthIntensity = 1.2f;

            [Tooltip("Heartbeat speed at low health, in beats per second. 0: no heartbeat.")]
            [Min(0f)] public float HeartbeatSpeed = 1.2f;

            [Tooltip("How much the heartbeat makes the low health vignette pulse.")]
            [Range(0f, 1f)] public float HeartbeatAmount = 0.15f;

            [Tooltip("Vignette intensity once dead.")]
            [Min(0f)] public float DeathIntensity = 2.2f;

            [Tooltip("Smoothing of the intensity changes, in seconds.")]
            [Min(0f)] public float Smoothing = 0.05f;
        }

        #region FIELDS

        static readonly int VIGNETTE_INTENSITY = Shader.PropertyToID("_VignetteIntensity");

        [Tooltip("Material of the Full Screen Pass Renderer Feature (DAMAGE).")]
        [SerializeField] private Material _material;
        [SerializeField] private Settings _settings;

        private Player _player;
        private Health _health;
        private int _previousHealth;
        private float _hitTime = -100f;
        private float _intensity;
        private float _velocity;
        private float _initialIntensity;

        #endregion

        #region UNITY LIFECYCLES

        void Awake()
        {
            _player = GetComponent<Player>();

            if (_material)
                _initialIntensity = _material.GetFloat(VIGNETTE_INTENSITY);
        }

        void Start()
        {
            // The Player initializes its Health in Awake, so it is ready here
            _health = _player.Health;
            _previousHealth = _health.Current;
            _health.OnChange += HandleHealthChange;

            Apply(0f);
        }

        void OnDestroy()
        {
            if (_health != null)
                _health.OnChange -= HandleHealthChange;

            // The material is an asset: give it back its value, so that play mode does not modify it
            if (_material)
                _material.SetFloat(VIGNETTE_INTENSITY, _initialIntensity);
        }

        void Update()
        {
            if (_health == null)
                return;

            float target = ComputeTargetIntensity();
            _intensity = Mathf.SmoothDamp(_intensity, target, ref _velocity, _settings.Smoothing);
            Apply(_intensity);
        }

        #endregion

        #region PRIVATE METHODS

#if UNITY_EDITOR
        [ContextMenu("Debug/Hit Flash")]
        private void DebugHit()
        {
            _hitTime = Time.time;
        }
#endif

        private void HandleHealthChange(int current, int max)
        {
            if (current < _previousHealth)
                _hitTime = Time.time;

            _previousHealth = current;
        }

        private float ComputeTargetIntensity()
        {
            if (_health.IsDead)
                return _settings.DeathIntensity;

            // Flash: full right after the hit, then eases out
            float hitProgress = Mathf.Clamp01((Time.time - _hitTime) / _settings.HitFadeDuration);
            float hit = (1f - hitProgress) * (1f - hitProgress) * _settings.HitIntensity;

            // Low health: 0 above the threshold, 1 when the health is empty
            float ratio = (float)_health.Current / _health.Max;
            float low = _settings.LowHealthThreshold > 0f
                ? Mathf.Clamp01((_settings.LowHealthThreshold - ratio) / _settings.LowHealthThreshold)
                : 0f;

            float heartbeat = 1f;
            if (_settings.HeartbeatSpeed > 0f)
                heartbeat += Mathf.Sin(Time.time * _settings.HeartbeatSpeed * Mathf.PI * 2f) * _settings.HeartbeatAmount;

            float lowHealth = low * _settings.LowHealthIntensity * heartbeat;

            return Mathf.Max(hit, lowHealth);
        }

        private void Apply(float intensity)
        {
            if (_material)
                _material.SetFloat(VIGNETTE_INTENSITY, intensity);
        }

        #endregion
    }
}
