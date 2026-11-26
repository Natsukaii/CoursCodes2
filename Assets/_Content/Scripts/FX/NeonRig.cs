using System.Collections.Generic;
using Arena.Characters;
using Arena.Combat;
using UnityEngine;

namespace Arena.FX
{
    /// <summary>
    /// Neon elements on a character, attached to its skeleton at startup:
    /// holographic ring on the ground, shoulder neons, head antenna or visor, light strip in the back.
    /// On the player the shoulder neons take the colour of the current ammo type;
    /// on an enemy a health bar appears above the head once it is hurt.
    /// </summary>
    public class NeonRig : MonoBehaviour
    {
        public enum Style
        {
            Player,
            Enemy
        }

        [System.Serializable]
        private class Settings
        {
            public Style Style = Style.Player;
            [ColorUsage(false, false)] public Color Color = new Color(0f, 0.85f, 1f);
            [Min(0f)] public float Intensity = 2.5f;
            [Min(0.1f)] public float RingSize = 1.8f;
        }

        [System.Serializable]
        private struct References
        {
            [Tooltip("URP Particles/Unlit shader.")]
            public Shader NeonShader;
            public Texture2D RingTexture;
        }

        #region FIELDS

        [SerializeField] private Settings _settings;
        [SerializeField] private References _references;

        private Transform _ring, _healthBar, _healthFill;
        private Material _main, _accent;
        private readonly List<Renderer> _weaponTubes = new List<Renderer>();
        private ProjectileLauncher _launcher;
        private Health _health;
        private Camera _camera;

        #endregion

        #region UNITY LIFECYCLES

        void Start()
        {
            _camera = Camera.main;
            _main = NeonMaterials.CreateAdditive(_references.NeonShader, null, _settings.Color * _settings.Intensity);
            _accent = NeonMaterials.CreateAdditive(_references.NeonShader, null, _settings.Color * _settings.Intensity * 0.6f);

            BuildRing();

            Transform head = FindBone("jnt_head");
            Transform body = FindBone("jnt_body");
            Transform shoulderL = FindBone("jnt_shoulder_L");
            Transform shoulderR = FindBone("jnt_shoulder_R");

            if (_settings.Style == Style.Player)
            {
                Material weapon = NeonMaterials.CreateAdditive(_references.NeonShader, null, _settings.Color * _settings.Intensity);
                _weaponTubes.Add(Tube("ShoulderNeon_L", shoulderL, weapon, new Vector3(0.05f, 0.1f, 0f), new Vector3(0.22f, 0.035f, 0.05f)));
                _weaponTubes.Add(Tube("ShoulderNeon_R", shoulderR, weapon, new Vector3(-0.05f, 0.1f, 0f), new Vector3(0.22f, 0.035f, 0.05f)));
                Tube("Antenna", head, _main, new Vector3(0.12f, 0.3f, -0.05f), new Vector3(0.025f, 0.28f, 0.025f));
                Tube("AntennaTip", head, _main, new Vector3(0.12f, 0.45f, -0.05f), new Vector3(0.06f, 0.06f, 0.06f));
                Tube("BackStrip", body, _accent, new Vector3(0f, 0.12f, -0.2f), new Vector3(0.04f, 0.38f, 0.03f));
                Tube("BackStrip_L", body, _accent, new Vector3(0.09f, 0.08f, -0.19f), new Vector3(0.025f, 0.26f, 0.03f));
                Tube("BackStrip_R", body, _accent, new Vector3(-0.09f, 0.08f, -0.19f), new Vector3(0.025f, 0.26f, 0.03f));

                Player player = GetComponent<Player>();
                _launcher = player ? player.Launcher : null;
                if (_launcher)
                {
                    _launcher.OnWeaponChange += HandleWeaponChange;
                    HandleWeaponChange(_launcher.Weapon, _launcher.WeaponIndex);
                }
            }
            else
            {
                Tube("Visor", head, _main, new Vector3(0f, 0.12f, 0.22f), new Vector3(0.3f, 0.045f, 0.03f));
                Tube("ShoulderNeon_L", shoulderL, _accent, new Vector3(0.05f, 0.1f, 0f), new Vector3(0.2f, 0.03f, 0.05f));
                Tube("ShoulderNeon_R", shoulderR, _accent, new Vector3(-0.05f, 0.1f, 0f), new Vector3(0.2f, 0.03f, 0.05f));
                Tube("Core", body, _main, new Vector3(0f, 0.05f, 0.21f), new Vector3(0.1f, 0.1f, 0.02f));

                AI.AI ai = GetComponent<AI.AI>();
                _health = ai ? ai.Health : null;
                BuildHealthBar(head);
            }
        }

        void OnDestroy()
        {
            if (_launcher)
                _launcher.OnWeaponChange -= HandleWeaponChange;

            // The ring is not parented to the character
            if (_ring)
                Destroy(_ring.gameObject);
        }

        void LateUpdate()
        {
            if (_ring)
            {
                _ring.position = new Vector3(transform.position.x, 0.06f, transform.position.z);
                _ring.rotation = Quaternion.Euler(90f, Time.time * (_settings.Style == Style.Player ? 30f : -45f), 0f);
            }

            UpdateHealthBar();
        }

        #endregion

        #region PRIVATE METHODS

        private void HandleWeaponChange(WeaponData weapon, int index)
        {
            Color c = AmmoTypes.Color(weapon.DamageType) * _settings.Intensity * 1.2f;
            foreach (Renderer r in _weaponTubes)
                if (r)
                    r.sharedMaterial.SetColor("_BaseColor", c);
        }

        private void BuildRing()
        {
            if (!_references.RingTexture)
                return;

            Material mat = NeonMaterials.CreateAdditive(_references.NeonShader, _references.RingTexture, _settings.Color * _settings.Intensity * 0.6f);
            _ring = NeonMaterials.CreateGroundQuad("NeonRing", mat, _settings.RingSize).transform;
            // Not parented: stays flat on the ground whatever the character does
        }

        private void BuildHealthBar(Transform head)
        {
            if (_health == null)
                return;

            _healthBar = new GameObject("HealthBar").transform;
            _healthBar.SetParent(transform, false);
            _healthBar.position = (head ? head.position : transform.position + Vector3.up * 1.4f) + Vector3.up * 0.75f;

            Transform bg = NeonMaterials.CreateTube("Bg", NeonMaterials.CreateAdditive(_references.NeonShader, null, _settings.Color * 0.25f)).transform;
            bg.SetParent(_healthBar, false);
            bg.localScale = new Vector3(1f, 0.08f, 0.01f);

            _healthFill = NeonMaterials.CreateTube("Fill", NeonMaterials.CreateAdditive(_references.NeonShader, null, _settings.Color * 3f)).transform;
            _healthFill.SetParent(_healthBar, false);
            _healthFill.localPosition = new Vector3(0f, 0f, -0.01f);
            _healthFill.localScale = new Vector3(1f, 0.08f, 0.01f);

            _healthBar.gameObject.SetActive(false);
        }

        private void UpdateHealthBar()
        {
            if (!_healthBar || _health == null)
                return;

            float ratio = _health.Max > 0 ? Mathf.Clamp01((float)_health.Current / _health.Max) : 0f;
            bool show = ratio < 0.999f && ratio > 0f;
            if (_healthBar.gameObject.activeSelf != show)
                _healthBar.gameObject.SetActive(show);
            if (!show)
                return;

            // Billboard facing the camera, fill anchored on the left
            if (_camera)
                _healthBar.rotation = Quaternion.LookRotation(_healthBar.position - _camera.transform.position, _camera.transform.up);
            _healthFill.localScale = new Vector3(ratio, 0.08f, 0.01f);
            _healthFill.localPosition = new Vector3(-(1f - ratio) * 0.5f, 0f, -0.01f);
        }

        // Neon tube on a bone, placed with an offset expressed in the character's frame, then following the bone
        private Renderer Tube(string name, Transform bone, Material material, Vector3 offset, Vector3 size)
        {
            if (!bone)
                return null;

            GameObject go = NeonMaterials.CreateTube(name, material);
            Transform t = go.transform;
            t.position = bone.position + transform.rotation * offset;
            t.rotation = transform.rotation;
            t.localScale = size;
            t.SetParent(bone, true);
            return go.GetComponent<Renderer>();
        }

        private Transform FindBone(string boneName)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == boneName)
                    return t;
            return null;
        }

        #endregion
    }
}
