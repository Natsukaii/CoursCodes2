using System;
using Arena.Characters;
using Arena.Combat;
using Arena.FX;
using UnityEngine;

namespace Arena.Interactions
{
    /// <summary>
    /// Drops something when the owner dies (called by the AI): ammo of a random type, a health pack,
    /// or more rarely a bonus.
    /// </summary>
    public class AmmoDrop : MonoBehaviour
    {
        [System.Serializable]
        private class Settings
        {
            [Tooltip("Chance to drop something, from 0 to 1.")]
            [Range(0f, 1f)] public float Chance = 0.85f;

            [Tooltip("Ammo given, per type.")]
            [Min(1)] public int Physical = 15;
            [Min(1)] public int Energy = 30;
            [Min(1)] public int Digital = 4;

            [Tooltip("Relative chance of each type (Physical, Energy, Digital).")]
            public Vector3 Weights = new Vector3(1f, 1f, 0.6f);

            [Tooltip("Chance that the drop is a health pack instead of ammo, from 0 to 1.")]
            [Range(0f, 1f)] public float HealthChance = 0.25f;
            [Min(1)] public int Health = 25;

            [Tooltip("Chance that the drop is a random bonus (shield, overdrive...), from 0 to 1.")]
            [Range(0f, 1f)] public float BonusChance = 0.08f;
        }

        [System.Serializable]
        private struct References
        {
            [Tooltip("URP Particles/Unlit shader, for the glow.")]
            public Shader NeonShader;
            [Tooltip("Ring texture drawn on the ground under the pickup.")]
            public Texture2D RingTexture;
        }

        [SerializeField] private Settings _settings;
        [SerializeField] private References _references;

        /// <summary>
        /// Spawns the pickup at the owner's position. Returns null if nothing dropped.
        /// </summary>
        public PickupBase Drop()
        {
            if (UnityEngine.Random.value > _settings.Chance)
                return null;

            Vector3 position = transform.position;
            position.y = 0f;

            float r = UnityEngine.Random.value;
            if (r < _settings.BonusChance)
                return BonusPickup.Spawn(position, BonusPickup.RandomKind(), _references.NeonShader, _references.RingTexture, 30f);
            if (r < _settings.BonusChance + _settings.HealthChance)
                return AmmoPickup.SpawnHealth(position, _settings.Health, _references.NeonShader, _references.RingTexture);

            Vector3 w = _settings.Weights;
            float t = UnityEngine.Random.value * (w.x + w.y + w.z);
            DamageType type = t < w.x ? DamageType.Physical : t < w.x + w.y ? DamageType.Energy : DamageType.Digital;
            int amount = type == DamageType.Physical ? _settings.Physical : type == DamageType.Energy ? _settings.Energy : _settings.Digital;
            return AmmoPickup.Spawn(position, type, amount, _references.NeonShader, _references.RingTexture);
        }
    }

    /// <summary>
    /// Glowing pickup floating above the ground, attracted by the nearby player, collected on contact.
    /// Built in code. Blinks then disappears at the end of its lifetime (0: stays forever).
    /// </summary>
    public abstract class PickupBase : MonoBehaviour
    {
        const float MAGNET_RADIUS = 4f;
        const float COLLECT_RADIUS = 0.9f;

        protected Transform Core;
        private Transform _ring;
        private Light _light;
        private Renderer[] _renderers;
        private float _spawnTime, _speed, _lifetime;

        /// <summary>
        /// Gives the content to the player. Returns false if it is useless now: the pickup then stays.
        /// </summary>
        protected abstract bool Collect(Player player);

        protected static T Create<T>(string name, Vector3 position, Color color, Shader shader, Texture2D ring, float lifetime, bool diamond) where T : PickupBase
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;
            T pickup = root.AddComponent<T>();
            pickup._spawnTime = Time.time;
            pickup._lifetime = lifetime;

            Material outer = NeonMaterials.CreateAdditive(shader, null, color * 2.5f);
            Material inner = NeonMaterials.CreateAdditive(shader, null, color * 5f);
            pickup.Core = new GameObject("Core").transform;
            pickup.Core.SetParent(root.transform, false);
            pickup.Core.localPosition = new Vector3(0f, 0.7f, 0f);

            if (diamond)
            {
                // Bonus: tilted glowing cube in a frame of 4 bars
                Transform gem = NeonMaterials.CreateTube("Gem", inner).transform;
                gem.SetParent(pickup.Core, false);
                gem.localRotation = Quaternion.Euler(45f, 0f, 45f);
                gem.localScale = Vector3.one * 0.32f;
                for (int i = 0; i < 4; i++)
                {
                    Transform bar = NeonMaterials.CreateTube("Frame", outer).transform;
                    bar.SetParent(pickup.Core, false);
                    bar.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                    bar.localPosition = bar.localRotation * new Vector3(0.3f, 0f, 0f);
                    bar.localScale = new Vector3(0.03f, 0.65f, 0.03f);
                }
            }
            else
            {
                // Ammo / health: glowing cell in a frame
                for (int i = 0; i < 4; i++)
                {
                    Transform bar = NeonMaterials.CreateTube("Frame", outer).transform;
                    bar.SetParent(pickup.Core, false);
                    bar.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                    bar.localPosition = bar.localRotation * new Vector3(0.2f, 0f, 0f);
                    bar.localScale = new Vector3(0.04f, 0.45f, 0.4f);
                }
                Transform cell = NeonMaterials.CreateTube("Cell", inner).transform;
                cell.SetParent(pickup.Core, false);
                cell.localScale = new Vector3(0.16f, 0.3f, 0.16f);
            }

            if (ring)
            {
                pickup._ring = NeonMaterials.CreateGroundQuad("Ring", NeonMaterials.CreateAdditive(shader, ring, color * 1.6f), diamond ? 2f : 1.6f).transform;
                pickup._ring.SetParent(root.transform, false);
                pickup._ring.localPosition = new Vector3(0f, 0.05f, 0f);
            }

            GameObject lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            pickup._light = lightGo.AddComponent<Light>();
            pickup._light.type = LightType.Point;
            pickup._light.color = color;
            pickup._light.intensity = diamond ? 3.5f : 2.5f;
            pickup._light.range = diamond ? 4.5f : 3.5f;
            pickup._light.shadows = LightShadows.None;

            pickup._renderers = root.GetComponentsInChildren<Renderer>();
            return pickup;
        }

        void Update()
        {
            float age = Time.time - _spawnTime;
            if (_lifetime > 0f && age > _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // Spin, bob, and blink before disappearing
            Core.localRotation = Quaternion.Euler(0f, Time.time * 120f, 0f);
            Core.localPosition = new Vector3(0f, 0.7f + Mathf.Sin(Time.time * 3f) * 0.1f, 0f);
            if (_ring)
                _ring.localRotation = Quaternion.Euler(90f, -Time.time * 40f, 0f);

            bool visible = _lifetime <= 0f || age < _lifetime - 5f || Mathf.Repeat(Time.time * 6f, 1f) > 0.35f;
            foreach (Renderer r in _renderers)
                if (r) r.enabled = visible;
            _light.enabled = visible;

            Player player = Player.Instance;
            if (!player || player.Health.IsDead)
                return;

            Vector3 flat = player.transform.position - transform.position;
            flat.y = 0f;
            float distance = flat.magnitude;

            if (distance < COLLECT_RADIUS)
            {
                if (Collect(player))
                    Destroy(gameObject);
                return;
            }

            if (distance < MAGNET_RADIUS)
            {
                _speed = Mathf.Min(_speed + 25f * Time.deltaTime, 14f);
                transform.position += flat.normalized * Mathf.Min(_speed * Time.deltaTime, distance);
            }
            else
                _speed = 0f;
        }
    }

    /// <summary>
    /// Ammo of one type, or a health pack.
    /// </summary>
    public class AmmoPickup : PickupBase
    {
        /// <summary>
        /// Raised with the type and amount each time the player collects ammo (used by the HUD).
        /// </summary>
        public static event Action<DamageType, int> OnCollected;

        /// <summary>
        /// Raised with the health points given each time the player collects a health pack.
        /// </summary>
        public static event Action<int> OnHealed;

        static readonly Color HEALTH_COLOR = new Color(0.2f, 1f, 0.45f);
        const float DROP_LIFETIME = 30f;

        private DamageType _type;
        private int _amount;
        private bool _isHealth;

        public static AmmoPickup Spawn(Vector3 position, DamageType type, int amount, Shader shader, Texture2D ring, bool permanent = false)
        {
            AmmoPickup p = Create<AmmoPickup>($"AmmoPickup_{type}", position, AmmoTypes.Color(type), shader, ring, permanent ? 0f : DROP_LIFETIME, false);
            p._type = type;
            p._amount = amount;
            return p;
        }

        public static AmmoPickup SpawnHealth(Vector3 position, int amount, Shader shader, Texture2D ring, bool permanent = false)
        {
            AmmoPickup p = Create<AmmoPickup>("HealthPickup", position, HEALTH_COLOR, shader, ring, permanent ? 0f : DROP_LIFETIME, false);
            p._isHealth = true;
            p._amount = amount;
            return p;
        }

        protected override bool Collect(Player player)
        {
            if (_isHealth)
            {
                if (player.Health.Current >= player.Health.Max)
                    return false;
                player.Health.Heal(_amount);
                OnHealed?.Invoke(_amount);
                return true;
            }

            if (player.Launcher && player.Launcher.AddAmmo(_type, _amount))
                OnCollected?.Invoke(_type, _amount);
            return true;
        }
    }

    /// <summary>
    /// Bonus applied through PlayerBuffs: heal, mega heal, shield, overdrive, haste, rapid fire, resupply.
    /// </summary>
    public class BonusPickup : PickupBase
    {
        private PlayerBuffs.Kind _kind;

        public static BonusPickup Spawn(Vector3 position, PlayerBuffs.Kind kind, Shader shader, Texture2D ring, float lifetime = 0f)
        {
            BonusPickup p = Create<BonusPickup>($"Bonus_{kind}", position, PlayerBuffs.Color(kind), shader, ring, lifetime, true);
            p._kind = kind;
            return p;
        }

        /// <summary>
        /// Random bonus, the strongest ones being the rarest.
        /// </summary>
        public static PlayerBuffs.Kind RandomKind(System.Random rng = null)
        {
            float r = rng != null ? (float)rng.NextDouble() : UnityEngine.Random.value;
            if (r < 0.28f) return PlayerBuffs.Kind.Health;
            if (r < 0.44f) return PlayerBuffs.Kind.Shield;
            if (r < 0.58f) return PlayerBuffs.Kind.Overdrive;
            if (r < 0.72f) return PlayerBuffs.Kind.Haste;
            if (r < 0.86f) return PlayerBuffs.Kind.RapidFire;
            if (r < 0.95f) return PlayerBuffs.Kind.Resupply;
            return PlayerBuffs.Kind.MegaHealth;
        }

        protected override bool Collect(Player player)
        {
            return player.TryGetComponent(out PlayerBuffs buffs) && buffs.Apply(_kind);
        }
    }
}
