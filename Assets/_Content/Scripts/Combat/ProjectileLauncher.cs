using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena.Combat
{
    /// <summary>
    /// Fires projectiles from a muzzle, limited by the fire interval and the ammo count.
    /// Placed on the root of the shooter, which the projectiles ignore.
    /// Can hold several weapons (one per ammo type), each with its own ammo, and switch between them.
    /// </summary>
    public class ProjectileLauncher : MonoBehaviour
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("Weapon data: damage, fire interval, ammo, projectile. Used when the arsenal is empty.")]
            public WeaponData Weapon;

            [Tooltip("Weapons the launcher can switch between (one per ammo type). Empty: only Weapon.")]
            public WeaponData[] Arsenal;
        }

        [System.Serializable]
        public struct References
        {
            [Tooltip("Spawn point of the projectiles. Its forward axis gives the direction.")]
            public Transform Muzzle;
        }

        [System.Serializable]
        public class StateContainer
        {
            [Tooltip("Ammo of the current weapon.")]
            public int Ammo;
        }

        #region FIELDS

        [SerializeField] private Settings _settings;
        [SerializeField] private References _references;
        [SerializeField] private StateContainer _state;

        /// <summary>
        /// Raised with the current and max ammo of the current weapon each time they change.
        /// </summary>
        public event Action<int, int> OnAmmoChange;

        /// <summary>
        /// Raised with the new weapon and its index when the current weapon changes.
        /// </summary>
        public event Action<WeaponData, int> OnWeaponChange;

        public StateContainer State => _state;
        public int MaxAmmo => Weapon.MaxAmmo;
        public WeaponData Weapon => _weapons[_index];
        public int WeaponIndex => _index;
        public IReadOnlyList<WeaponData> Weapons => _weapons;

        /// <summary>
        /// Multiplies the damage of the projectiles (Overdrive bonus).
        /// </summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>
        /// Multiplies the fire rate (Rapid fire bonus).
        /// </summary>
        public float FireRateMultiplier { get; set; } = 1f;

        private readonly List<WeaponData> _weapons = new List<WeaponData>();
        private int[] _ammo;
        private int _index;
        private float _nextFireTime;

        #endregion

        #region UNITY LIFECYCLES

        private void Awake()
        {
            if (_settings.Arsenal != null)
                foreach (WeaponData weapon in _settings.Arsenal)
                    if (weapon)
                        _weapons.Add(weapon);

            if (_weapons.Count == 0)
                _weapons.Add(_settings.Weapon);

            _ammo = new int[_weapons.Count];
            for (int i = 0; i < _weapons.Count; i++)
                _ammo[i] = _weapons[i].MaxAmmo;

            _index = 0;
            _state.Ammo = _ammo[0];
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Fires one projectile if the weapon is ready and has ammo. Returns true if a shot was fired.
        /// </summary>
        public bool TryFire()
        {
            if (Time.time < _nextFireTime || _state.Ammo <= 0)
                return false;

            WeaponData weapon = Weapon;
            _nextFireTime = Time.time + weapon.FireInterval / Mathf.Max(0.1f, FireRateMultiplier);

            SetAmmo(_state.Ammo - 1);

            Transform muzzle = _references.Muzzle;
            Projectile projectile = Instantiate(weapon.ProjectilePrefab, muzzle.position, muzzle.rotation);
            int damage = Mathf.RoundToInt(weapon.Damage * DamageMultiplier);
            projectile.Launch(new DamageInfo(damage, weapon.DamageType, gameObject));

            // With several ammo types, each projectile takes the colour of its type
            if (_weapons.Count > 1)
                Tint(projectile.gameObject, AmmoTypes.Color(weapon.DamageType));
            return true;
        }

        /// <summary>
        /// Adds ammo to the current weapon, without exceeding its maximum.
        /// </summary>
        public void AddAmmo(int amount)
        {
            AddAmmoAt(_index, amount);
        }

        /// <summary>
        /// Adds ammo to the weapon using this ammo type. Returns false if the launcher has no such weapon.
        /// </summary>
        public bool AddAmmo(DamageType type, int amount)
        {
            int i = IndexOf(type);
            if (i < 0)
                return false;

            AddAmmoAt(i, amount);
            return true;
        }

        public int GetAmmo(int index) => index == _index ? _state.Ammo : _ammo[index];

        public int IndexOf(DamageType type)
        {
            for (int i = 0; i < _weapons.Count; i++)
                if (_weapons[i].DamageType == type)
                    return i;
            return -1;
        }

        /// <summary>
        /// Makes another weapon the current one. Each weapon keeps its own ammo.
        /// </summary>
        public void SelectWeapon(int index)
        {
            if (index < 0 || index >= _weapons.Count || index == _index)
                return;

            _ammo[_index] = _state.Ammo;
            _index = index;
            _nextFireTime = Mathf.Max(_nextFireTime, Time.time + 0.1f);
            SetAmmo(_ammo[_index]);
            OnWeaponChange?.Invoke(Weapon, _index);
        }

        /// <summary>
        /// Selects the next (direction 1) or previous (direction -1) weapon, looping.
        /// </summary>
        public void CycleWeapon(int direction)
        {
            if (_weapons.Count < 2)
                return;

            int count = _weapons.Count;
            SelectWeapon(((_index + direction) % count + count) % count);
        }

        #endregion

        #region PRIVATE METHODS

        private static void Tint(GameObject projectile, Color color)
        {
            foreach (Arena.FX.NeonTrail trail in projectile.GetComponentsInChildren<Arena.FX.NeonTrail>())
                trail.SetColor(color * 3f, color);

            foreach (MeshRenderer r in projectile.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.sharedMaterial || !r.sharedMaterial.HasProperty("_EmissionColor"))
                    continue;
                Material m = r.material;
                m.SetColor("_BaseColor", color);
                m.SetColor("_EmissionColor", color * 3f);
            }
        }

        private void AddAmmoAt(int index, int amount)
        {
            if (amount <= 0)
                return;

            if (index == _index)
                SetAmmo(Mathf.Min(_state.Ammo + amount, MaxAmmo));
            else
                _ammo[index] = Mathf.Min(_ammo[index] + amount, _weapons[index].MaxAmmo);
        }

        private void SetAmmo(int value)
        {
            _state.Ammo = value;
            _ammo[_index] = value;
            OnAmmoChange?.Invoke(_state.Ammo, MaxAmmo);
        }

        [ContextMenu("Debug/Try Fire")]
        private void DebugTryFire()
        {
            TryFire();
        }

        [ContextMenu("Debug/Next Weapon")]
        private void DebugNextWeapon()
        {
            CycleWeapon(1);
        }

        #endregion
    }
}
