using System;
using Arena.Combat;
using UnityEngine;

namespace Arena.Characters
{
    /// <summary>
    /// Bonuses picked up by the player: instant ones (heal, shield, ammo) and timed ones
    /// (Overdrive: damage x2, Haste: speed, Rapid fire: fire rate).
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class PlayerBuffs : MonoBehaviour
    {
        public enum Kind
        {
            Health,
            MegaHealth,
            Shield,
            Overdrive,
            Haste,
            RapidFire,
            Resupply
        }

        [System.Serializable]
        private class Settings
        {
            [Min(1)] public int Heal = 35;
            [Min(1)] public int ShieldGain = 60;
            [Min(0f)] public float OverdriveDuration = 10f;
            [Min(1f)] public float OverdriveDamage = 2f;
            [Min(0f)] public float HasteDuration = 8f;
            [Min(1f)] public float HasteSpeed = 1.4f;
            [Min(0f)] public float RapidFireDuration = 10f;
            [Min(1f)] public float RapidFireRate = 1.8f;
        }

        #region FIELDS

        public static readonly Kind[] Timed = { Kind.Overdrive, Kind.Haste, Kind.RapidFire };

        [SerializeField] private Settings _settings;

        /// <summary>
        /// Raised with the bonus each time one is applied (used by the HUD).
        /// </summary>
        public event Action<Kind> OnApplied;

        private Player _player;
        private readonly float[] _endTimes = new float[Enum.GetValues(typeof(Kind)).Length];

        #endregion

        #region UNITY LIFECYCLES

        void Awake()
        {
            _player = GetComponent<Player>();
        }

        void Update()
        {
            _player.SpeedMultiplier = IsActive(Kind.Haste) ? _settings.HasteSpeed : 1f;

            ProjectileLauncher launcher = _player.Launcher;
            if (launcher)
            {
                launcher.DamageMultiplier = IsActive(Kind.Overdrive) ? _settings.OverdriveDamage : 1f;
                launcher.FireRateMultiplier = IsActive(Kind.RapidFire) ? _settings.RapidFireRate : 1f;
            }
        }

        #endregion

        #region PUBLIC METHODS

        public bool IsActive(Kind kind) => Time.time < _endTimes[(int)kind];
        public float Remaining(Kind kind) => Mathf.Max(0f, _endTimes[(int)kind] - Time.time);
        public float Duration(Kind kind) => kind == Kind.Overdrive ? _settings.OverdriveDuration : kind == Kind.Haste ? _settings.HasteDuration : kind == Kind.RapidFire ? _settings.RapidFireDuration : 0f;

        /// <summary>
        /// Applies a bonus. Returns false if it would be useless (full health, full shield...): the pickup then stays.
        /// </summary>
        public bool Apply(Kind kind)
        {
            Health health = _player.Health;
            switch (kind)
            {
                case Kind.Health:
                    if (health.Current >= health.Max) return false;
                    health.Heal(_settings.Heal);
                    break;
                case Kind.MegaHealth:
                    if (health.Current >= health.Max && _player.Shield >= _player.MaxShield) return false;
                    health.Heal(health.Max);
                    _player.Shield = Mathf.Max(_player.Shield, _player.MaxShield * 0.5f);
                    break;
                case Kind.Shield:
                    if (_player.Shield >= _player.MaxShield) return false;
                    _player.Shield = Mathf.Min(_player.MaxShield, _player.Shield + _settings.ShieldGain);
                    break;
                case Kind.Resupply:
                    ProjectileLauncher launcher = _player.Launcher;
                    if (!launcher) return false;
                    for (int i = 0; i < launcher.Weapons.Count; i++)
                        launcher.AddAmmo(launcher.Weapons[i].DamageType, launcher.Weapons[i].MaxAmmo);
                    break;
                default:
                    _endTimes[(int)kind] = Time.time + Duration(kind);
                    break;
            }

            OnApplied?.Invoke(kind);
            return true;
        }

        public static string Label(Kind kind)
        {
            switch (kind)
            {
                case Kind.Health: return "SOIN";
                case Kind.MegaHealth: return "MEGA SOIN";
                case Kind.Shield: return "BOUCLIER";
                case Kind.Overdrive: return "OVERDRIVE";
                case Kind.Haste: return "HATE";
                case Kind.RapidFire: return "CADENCE";
                default: return "RAVITAILLEMENT";
            }
        }

        public static string Description(Kind kind)
        {
            switch (kind)
            {
                case Kind.Health: return "+35 PV";
                case Kind.MegaHealth: return "PV MAX + BOUCLIER";
                case Kind.Shield: return "+60 BOUCLIER";
                case Kind.Overdrive: return "DEGATS x2";
                case Kind.Haste: return "VITESSE +40%";
                case Kind.RapidFire: return "CADENCE x1.8";
                default: return "MUNITIONS MAX";
            }
        }

        public static Color Color(Kind kind)
        {
            switch (kind)
            {
                case Kind.Health: return new Color(0.2f, 1f, 0.45f);
                case Kind.MegaHealth: return new Color(0.6f, 1f, 0.3f);
                case Kind.Shield: return new Color(0.3f, 0.55f, 1f);
                case Kind.Overdrive: return new Color(1f, 0.25f, 0.1f);
                case Kind.Haste: return new Color(1f, 0.9f, 0.15f);
                case Kind.RapidFire: return new Color(0.65f, 0.25f, 1f);
                default: return new Color(0.85f, 0.95f, 1f);
            }
        }

        #endregion
    }
}
