using System;
using Arena.Characters;
using Arena.FX;
using Arena.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arena.Level
{
    /// <summary>
    /// Exit portal: a column of light with rotating rings. Locked (red) while enemies remain if asked,
    /// open (cyan) otherwise. Walking into an open portal loads the next scene or raises OnEntered
    /// (used by the procedural level to generate the next sector). Built in code.
    /// </summary>
    public class LevelExit : MonoBehaviour
    {
        [System.Serializable]
        private class Settings
        {
            [Tooltip("Scene loaded when the player enters the open portal. Empty: only raises OnEntered.")]
            public string SceneToLoad = "";
            [Tooltip("The portal stays locked while at least one enemy is alive.")]
            public bool UnlockWhenCleared = true;
            [Min(0.5f)] public float Radius = 1.6f;
            [ColorUsage(false, false)] public Color OpenColor = new Color(0f, 0.9f, 1f);
            [ColorUsage(false, false)] public Color LockedColor = new Color(1f, 0.1f, 0.25f);
        }

        [System.Serializable]
        private struct References
        {
            [Tooltip("URP Particles/Unlit shader.")]
            public Shader NeonShader;
            public Texture2D RingTexture;
        }

        #region FIELDS

        [SerializeField] private Settings _settings = new Settings();
        [SerializeField] private References _references;

        public event Action OnEntered;

        public bool IsLocked { get; private set; } = true;

        private Material _glow, _ringMat;
        private Transform _ringA, _ringB, _floorRing, _column;
        private Light _light;
        private bool _used;
        private float _nextCheck;

        #endregion

        #region UNITY LIFECYCLES

        void Start()
        {
            Build();
            SetLocked(_settings.UnlockWhenCleared && EnemiesLeft() > 0, false);
        }

        void Update()
        {
            float t = Time.time;
            _ringA.localRotation = Quaternion.Euler(0f, t * 60f, 0f);
            _ringB.localRotation = Quaternion.Euler(0f, -t * 45f + 90f, 0f);
            _floorRing.localRotation = Quaternion.Euler(90f, t * 30f, 0f);
            float pulse = IsLocked ? 1f : 1f + Mathf.Sin(t * 4f) * 0.25f;
            _column.localScale = new Vector3(0.12f * pulse, 7f, 0.12f * pulse);

            if (IsLocked && t >= _nextCheck)
            {
                _nextCheck = t + 0.5f;
                if (!_settings.UnlockWhenCleared || EnemiesLeft() == 0)
                    SetLocked(false, true);
            }

            Player player = Player.Instance;
            if (_used || IsLocked || !player || player.Health.IsDead)
                return;

            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            if (d.magnitude <= _settings.Radius)
                Enter();
        }

        #endregion

        #region PUBLIC METHODS

        public void Configure(string sceneToLoad, bool unlockWhenCleared, Shader shader, Texture2D ring)
        {
            _settings.SceneToLoad = sceneToLoad;
            _settings.UnlockWhenCleared = unlockWhenCleared;
            _references.NeonShader = shader;
            _references.RingTexture = ring;
        }

        #endregion

        #region PRIVATE METHODS

        private void Enter()
        {
            _used = true;
            OnEntered?.Invoke();

            if (!string.IsNullOrEmpty(_settings.SceneToLoad))
            {
                Hud()?.SetObjective("TRANSFERT EN COURS...");
                SceneManager.LoadScene(_settings.SceneToLoad);
            }
        }

        private void SetLocked(bool locked, bool announce)
        {
            IsLocked = locked;
            Color c = locked ? _settings.LockedColor : _settings.OpenColor;
            _glow.SetColor("_BaseColor", c * 3f);
            _ringMat.SetColor("_BaseColor", c * 2f);
            _light.color = c;
            _light.intensity = locked ? 2f : 5f;

            if (announce && !locked)
                Hud()?.SetObjective("PORTAIL OUVERT  //  REJOINDRE LA SORTIE");
        }

        private void Build()
        {
            Shader shader = _references.NeonShader;
            _glow = NeonMaterials.CreateAdditive(shader, null, Color.white);
            _ringMat = NeonMaterials.CreateAdditive(shader, _references.RingTexture, Color.white);

            _column = NeonMaterials.CreateTube("Column", _glow).transform;
            _column.SetParent(transform, false);
            _column.localPosition = new Vector3(0f, 3.5f, 0f);

            _floorRing = NeonMaterials.CreateGroundQuad("FloorRing", _ringMat, 3.6f).transform;
            _floorRing.SetParent(transform, false);
            _floorRing.localPosition = new Vector3(0f, 0.06f, 0f);

            _ringA = VerticalRing("RingA", 2.6f);
            _ringB = VerticalRing("RingB", 2.1f);

            GameObject lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 7f;
            _light.shadows = LightShadows.None;
        }

        private Transform VerticalRing(string name, float size)
        {
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(0f, 1.5f, 0f);

            Material twoSided = new Material(_ringMat);
            twoSided.SetFloat("_Cull", 0f);
            _ringMaterials.Add(twoSided);

            GameObject quad = NeonMaterials.CreateGroundQuad(name + "Quad", twoSided, size);
            quad.transform.SetParent(pivot, false);
            quad.transform.localRotation = Quaternion.identity;
            return pivot;
        }

        private readonly System.Collections.Generic.List<Material> _ringMaterials = new System.Collections.Generic.List<Material>();

        void LateUpdate()
        {
            // Keep the vertical rings in sync with the ring colour
            Color c = _ringMat.GetColor("_BaseColor");
            foreach (Material m in _ringMaterials)
                m.SetColor("_BaseColor", c);
        }

        private static int EnemiesLeft() => FindObjectsByType<Arena.AI.AI>(FindObjectsSortMode.None).Length;

        private static NeonHUD Hud()
        {
            return Player.Instance && Player.Instance.TryGetComponent(out NeonHUD hud) ? hud : null;
        }

        #endregion
    }
}
