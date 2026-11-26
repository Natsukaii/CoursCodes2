using Arena.Combat;
using Arena.FX;
using UnityEngine;

namespace Arena.AI
{
    [System.Serializable]
    public class AIMeleSetting
    {
        [Tooltip("Damage of one sword hit.")]
        public int Amount = 15;
        public DamageType Type;

        [Tooltip("Reach of the sword, in meters.")]
        public float Reach = 2.1f;
        [Tooltip("Width of the slash in front of the enemy, in degrees.")]
        public float Angle = 150f;

        [Tooltip("Time spent raising the sword before the slash: the player can dodge, in seconds.")]
        public float Windup = 0.4f;
        public float SlashDuration = 0.16f;
        public float Recover = 0.3f;
        [Tooltip("Time between two attacks, in seconds.")]
        public float Cooldown = 0.9f;

        [ColorUsage(false, false)] public Color BladeColor = new Color(1f, 0.1f, 0.25f);
    }

    /// <summary>
    /// Melee enemy with a neon sword: comes close, raises the sword (telegraph), slashes, recovers.
    /// The sword and its swing are built and animated in code.
    /// </summary>
    public class AI_Mele : AI
    {
        enum Phase { Ready, Windup, Slash, Recover }

        [SerializeField] private AIMeleSetting _meleSettingSetting = new AIMeleSetting();
        [Tooltip("URP Particles/Unlit shader, for the glowing blade.")]
        [SerializeField] private Shader _neonShader;

        // Sword poses, relative to the body: pitch, yaw
        static readonly Quaternion REST = Quaternion.Euler(55f, 25f, 0f);
        static readonly Quaternion WINDUP = Quaternion.Euler(-55f, 85f, -20f);
        static readonly Quaternion SLASH_END = Quaternion.Euler(20f, -95f, 10f);

        private Phase _phase;
        private float _phaseStart, _nextAttack;
        private bool _hitDone;
        private Transform _sword;
        private TrailRenderer _trail;
        private Material _bladeMaterial;

        void Start()
        {
            base.Start();
            // Older prefabs saved the damage at 0
            if (_meleSettingSetting.Amount <= 0)
                _meleSettingSetting.Amount = 15;
            BuildSword();
        }

        void Update()
        {
            if (_phase != Phase.Ready)
            {
                UpdateAttack();
                return;
            }

            float stopRange = Mathf.Min(basicSetting.RangeToStopFollow, _meleSettingSetting.Reach * 0.75f);
            if (!FollowPlayer(Mathf.Max(stopRange, 1.3f)) && Time.time >= _nextAttack)
                StartPhase(Phase.Windup);
            else
                PoseSword(REST, 10f);
        }

        private void UpdateAttack()
        {
            float t = Time.time - _phaseStart;
            StopMoving();

            switch (_phase)
            {
                case Phase.Windup:
                    FacePlayer();
                    PoseSword(Quaternion.Slerp(REST, WINDUP, Ease(t / _meleSettingSetting.Windup)), 0f);
                    // Telegraph: the blade gets brighter
                    _bladeMaterial.SetColor("_BaseColor", _meleSettingSetting.BladeColor * Mathf.Lerp(2.5f, 6f, t / _meleSettingSetting.Windup));
                    if (t >= _meleSettingSetting.Windup)
                        StartPhase(Phase.Slash);
                    break;

                case Phase.Slash:
                    float k = Mathf.Clamp01(t / _meleSettingSetting.SlashDuration);
                    PoseSword(Quaternion.Slerp(WINDUP, SLASH_END, Ease(k)), 0f);
                    if (!_hitDone && k >= 0.45f)
                    {
                        _hitDone = true;
                        TryHit();
                    }
                    if (k >= 1f)
                        StartPhase(Phase.Recover);
                    break;

                case Phase.Recover:
                    PoseSword(Quaternion.Slerp(SLASH_END, REST, Ease(t / _meleSettingSetting.Recover)), 0f);
                    if (t >= _meleSettingSetting.Recover)
                    {
                        _nextAttack = Time.time + _meleSettingSetting.Cooldown;
                        StartPhase(Phase.Ready);
                    }
                    break;
            }
        }

        private void StartPhase(Phase phase)
        {
            _phase = phase;
            _phaseStart = Time.time;
            _hitDone = false;
            if (_trail)
                _trail.emitting = phase == Phase.Slash;
            if (phase == Phase.Ready || phase == Phase.Recover)
                _bladeMaterial.SetColor("_BaseColor", _meleSettingSetting.BladeColor * 2.5f);
        }

        private void TryHit()
        {
            if (!player || player.Health.IsDead)
                return;

            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            if (d.magnitude <= _meleSettingSetting.Reach && Vector3.Angle(transform.forward, d) <= _meleSettingSetting.Angle * 0.5f)
                player.TakeDamage(new DamageInfo(_meleSettingSetting.Amount, _meleSettingSetting.Type, gameObject));
        }

        private void BuildSword()
        {
            _sword = new GameObject("NeonSword").transform;
            _sword.SetParent(transform, false);
            _sword.localPosition = new Vector3(0.38f, 1.0f, 0.12f);
            _sword.localRotation = REST;

            _bladeMaterial = NeonMaterials.CreateAdditive(_neonShader, null, _meleSettingSetting.BladeColor * 2.5f);
            Material dark = NeonMaterials.CreateAdditive(_neonShader, null, _meleSettingSetting.BladeColor * 0.6f);

            Transform blade = NeonMaterials.CreateTube("Blade", _bladeMaterial).transform;
            blade.SetParent(_sword, false);
            blade.localPosition = new Vector3(0f, 0f, 0.68f);
            blade.localScale = new Vector3(0.045f, 0.09f, 1.05f);

            Transform guard = NeonMaterials.CreateTube("Guard", dark).transform;
            guard.SetParent(_sword, false);
            guard.localPosition = new Vector3(0f, 0f, 0.13f);
            guard.localScale = new Vector3(0.06f, 0.26f, 0.05f);

            Transform grip = NeonMaterials.CreateTube("Grip", dark).transform;
            grip.SetParent(_sword, false);
            grip.localPosition = new Vector3(0f, 0f, 0.02f);
            grip.localScale = new Vector3(0.04f, 0.04f, 0.18f);

            // Slash arc: trail left by the tip of the blade during the swing
            Transform tip = new GameObject("Tip").transform;
            tip.SetParent(_sword, false);
            tip.localPosition = new Vector3(0f, 0f, 1.2f);
            _trail = tip.gameObject.AddComponent<TrailRenderer>();
            _trail.time = 0.14f;
            _trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 0f));
            _trail.minVertexDistance = 0.03f;
            _trail.material = NeonMaterials.CreateAdditive(_neonShader, null, _meleSettingSetting.BladeColor * 3f);
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            _trail.colorGradient = g;
            _trail.emitting = false;
        }

        private void PoseSword(Quaternion pose, float smoothing)
        {
            if (!_sword)
                return;
            _sword.localRotation = smoothing > 0f ? Quaternion.Slerp(_sword.localRotation, pose, 1f - Mathf.Exp(-smoothing * Time.deltaTime)) : pose;
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
