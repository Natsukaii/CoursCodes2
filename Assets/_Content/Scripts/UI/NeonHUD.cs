using Arena.Characters;
using Arena.Combat;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Arena.UI
{
    /// <summary>
    /// Neon HUD (Cyberpunk / Tron style), built in code at startup so it needs no prefab nor font import:
    /// health and ammo panels with segmented gauges, current zone, objective, hostiles counter,
    /// scanlines, glitch on hit and "FLATLINED" screen on death.
    /// Put it on the Player.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class NeonHUD : MonoBehaviour
    {
        [System.Serializable]
        private class Settings
        {
            public Color Primary = new Color(0f, 0.9f, 1f);
            public Color Danger = new Color(1f, 0.1f, 0.6f);
            public Color Ammo = new Color(1f, 0.82f, 0.1f);
            public Color Panel = new Color(0.02f, 0.02f, 0.06f, 0.65f);

            [Tooltip("Health ratio under which the health gauge turns to the danger colour and pulses.")]
            [Range(0f, 1f)] public float LowHealth = 0.4f;

            [TextArea] public string Objective = "Atteindre le coeur du reacteur";

            [Range(0f, 0.3f)] public float ScanlinesOpacity = 0.08f;
        }

        #region FIELDS

        const int HEALTH_SEGMENTS = 20;
        const int MAX_AMMO_PIPS = 40;
        static readonly Regex ROOM_NAME = new Regex(@"^(\d{2})b?_(.+)$");

        [SerializeField] private Settings _settings;

        private Player _player;
        private Health _health;
        private ProjectileLauncher _launcher;
        private Font _font;

        private RectTransform _root, _healthPanel, _ammoPanel;
        private Text _healthValue, _ammoValue, _zoneNumber, _zoneName, _hostiles, _banner, _bannerSub, _deathTitle, _deathSub;
        private readonly List<Image> _healthSegments = new List<Image>();
        private readonly List<Image> _ammoPips = new List<Image>();
        private CanvasGroup _bannerGroup, _deathGroup;

        private int _previousHealth;
        private float _glitchEnd, _bannerTime = -10f, _nextHostileCheck;
        private string _currentZone;
        private Vector2 _healthPanelPos, _ammoPanelPos;

        #endregion

        #region UNITY LIFECYCLES

        void Awake()
        {
            _player = GetComponent<Player>();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void Start()
        {
            _health = _player.Health;
            _previousHealth = _health.Current;
            _health.OnChange += HandleHealthChange;
            _health.OnDied += HandleDied;
            HandleHealthChange(_health.Current, _health.Max);

            _launcher = _player.Launcher;
            if (_launcher)
            {
                _launcher.OnAmmoChange += HandleAmmoChange;
                HandleAmmoChange(_launcher.State.Ammo, _launcher.MaxAmmo);
            }
            else
                _ammoValue.text = "--";
        }

        void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnChange -= HandleHealthChange;
                _health.OnDied -= HandleDied;
            }

            if (_launcher)
                _launcher.OnAmmoChange -= HandleAmmoChange;

            if (_root)
                Destroy(_root.gameObject);
        }

        void Update()
        {
            if (_health == null)
                return;

            UpdateLowHealthPulse();
            UpdateGlitch();
            UpdateZone();
            UpdateBanner();
            UpdateHostiles();
        }

        #endregion

        #region BUILD

        private void Build()
        {
            GameObject canvasGo = new GameObject("NeonHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)canvasGo.transform;

            BuildScanlines();

            Color primary = _settings.Primary;

            // ---- health, bottom left
            _healthPanel = Panel("Health", new Vector2(0, 0), new Vector2(40, 40), new Vector2(560, 130), primary, false);
            _healthPanelPos = _healthPanel.anchoredPosition;
            Label(_healthPanel, "VITALS  //  SYS.HP", 18, Fade(primary, 0.75f), new Vector2(26, -14), new Vector2(400, 24), TextAnchor.UpperLeft);
            _healthValue = Label(_healthPanel, "100", 56, primary, new Vector2(26, -38), new Vector2(170, 70), TextAnchor.UpperLeft, true);
            for (int i = 0; i < HEALTH_SEGMENTS; i++)
            {
                Image seg = Box(_healthPanel, $"Seg{i}", new Vector2(190 + i * 18, -62), new Vector2(14, 34), primary);
                _healthSegments.Add(seg);
            }
            Label(_healthPanel, "> NEURAL LINK STABLE", 14, Fade(primary, 0.45f), new Vector2(190, -100), new Vector2(360, 20), TextAnchor.UpperLeft);

            // ---- ammo, bottom right
            Color ammo = _settings.Ammo;
            _ammoPanel = Panel("Ammo", new Vector2(1, 0), new Vector2(-40, 40), new Vector2(460, 130), ammo, true);
            _ammoPanelPos = _ammoPanel.anchoredPosition;
            Label(_ammoPanel, "AMMO  //  MAG", 18, Fade(ammo, 0.75f), new Vector2(-26, -14), new Vector2(400, 24), TextAnchor.UpperRight, false, true);
            _ammoValue = Label(_ammoPanel, "0 / 0", 50, ammo, new Vector2(-26, -40), new Vector2(260, 64), TextAnchor.UpperRight, true, true);
            for (int i = 0; i < MAX_AMMO_PIPS; i++)
                _ammoPips.Add(Box(_ammoPanel, $"Pip{i}", new Vector2(-26 - i * 10, -106), new Vector2(6, 14), ammo, true));

            // ---- zone, top left
            RectTransform zone = Rect("Zone", _root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -36), new Vector2(520, 90));
            Box(zone, "Accent", new Vector2(0, 0), new Vector2(6, 86), _settings.Danger);
            _zoneNumber = Label(zone, "ZONE --", 34, primary, new Vector2(20, 0), new Vector2(400, 44), TextAnchor.UpperLeft, true);
            _zoneName = Label(zone, "", 20, Fade(primary, 0.7f), new Vector2(20, -46), new Vector2(480, 30), TextAnchor.UpperLeft);
            Box(zone, "Line", new Vector2(20, -80), new Vector2(300, 2), Fade(primary, 0.5f));

            // ---- objective + hostiles, top right
            RectTransform obj = Rect("Objective", _root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -36), new Vector2(520, 110));
            Box(obj, "Accent", new Vector2(-6, 0), new Vector2(6, 106), _settings.Ammo, true);
            Label(obj, "OBJECTIF  >", 18, Fade(_settings.Ammo, 0.8f), new Vector2(-20, 0), new Vector2(480, 24), TextAnchor.UpperRight, false, true);
            Label(obj, _settings.Objective.ToUpperInvariant(), 22, Color.white, new Vector2(-20, -26), new Vector2(480, 30), TextAnchor.UpperRight, false, true);
            _hostiles = Label(obj, "HOSTILES  00", 26, _settings.Danger, new Vector2(-20, -62), new Vector2(480, 36), TextAnchor.UpperRight, true, true);

            // ---- zone banner, top centre
            RectTransform banner = Rect("Banner", _root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(900, 110));
            _bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            Box(banner, "Bg", new Vector2(0, 0), new Vector2(900, 110), _settings.Panel, false, true);
            Box(banner, "Top", new Vector2(0, 0), new Vector2(900, 2), primary, false, true);
            Box(banner, "Bottom", new Vector2(0, -108), new Vector2(900, 2), primary, false, true);
            _bannerSub = Label(banner, "ENTERING", 18, Fade(primary, 0.7f), new Vector2(0, -12), new Vector2(880, 24), TextAnchor.UpperCenter, false, false, true);
            _banner = Label(banner, "", 48, primary, new Vector2(0, -38), new Vector2(880, 60), TextAnchor.UpperCenter, true, false, true);

            // ---- death screen
            RectTransform death = Rect("Death", _root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _deathGroup = death.gameObject.AddComponent<CanvasGroup>();
            _deathGroup.alpha = 0f;
            Image dim = death.gameObject.AddComponent<Image>();
            dim.color = new Color(0.03f, 0f, 0.05f, 0.55f);
            dim.raycastTarget = false;
            _deathTitle = Label(death, "FLATLINED", 140, _settings.Danger, new Vector2(0, 60), new Vector2(1400, 170), TextAnchor.MiddleCenter, true, false, true, true);
            _deathSub = Label(death, "//  SIGNAL PERDU  //  REBOOT REQUIS", 26, Fade(_settings.Primary, 0.8f), new Vector2(0, -60), new Vector2(1200, 40), TextAnchor.MiddleCenter, false, false, true, true);
        }

        private void BuildScanlines()
        {
            Texture2D tex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels(new[] { Color.clear, Color.clear, Color.clear, Color.black });
            tex.Apply();

            RectTransform rt = Rect("Scanlines", _root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RawImage raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.color = new Color(1, 1, 1, _settings.ScanlinesOpacity * 4f);
            raw.uvRect = new Rect(0, 0, 1, 1080f / 4f);
            raw.raycastTarget = false;
        }

        // Panel with a dark background, a coloured side accent and corner brackets
        private RectTransform Panel(string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color, bool right)
        {
            RectTransform rt = Rect(name, _root, anchor, anchor, anchor, pos, size);
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.color = _settings.Panel;
            bg.raycastTarget = false;

            Box(rt, "Accent", new Vector2(right ? -0 : 0, 0), new Vector2(6, size.y), _settings.Danger, right);
            float L = 28f, T = 2f;
            Box(rt, "CornerA1", new Vector2(right ? -size.x + L : size.x - L, 0), new Vector2(L, T), color, right);
            Box(rt, "CornerA2", new Vector2(right ? -size.x + T : size.x - T, 0), new Vector2(T, L), color, right);
            Box(rt, "CornerB1", new Vector2(right ? -size.x + L : size.x - L, -size.y + T), new Vector2(L, T), color, right);
            Box(rt, "CornerB2", new Vector2(right ? -size.x + T : size.x - T, -size.y + L), new Vector2(T, L), color, right);
            Box(rt, "TopLine", new Vector2(right ? -10 : 10, 0), new Vector2(size.x * 0.45f, 1), Fade(color, 0.5f), right);
            return rt;
        }

        private RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        // Solid rectangle positioned from the top left (or top right) corner of its parent
        private Image Box(Transform parent, string name, Vector2 pos, Vector2 size, Color color, bool right = false, bool centered = false)
        {
            Vector2 a = centered ? new Vector2(0.5f, 1) : right ? new Vector2(1, 1) : new Vector2(0, 1);
            RectTransform rt = Rect(name, parent, a, a, a, pos, size);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Text Label(Transform parent, string text, int size, Color color, Vector2 pos, Vector2 box, TextAnchor align,
            bool glow = false, bool right = false, bool centered = false, bool middle = false)
        {
            Vector2 a = middle ? new Vector2(0.5f, 0.5f) : centered ? new Vector2(0.5f, 1) : right ? new Vector2(1, 1) : new Vector2(0, 1);
            RectTransform rt = Rect("Text", parent, a, a, a, pos, box);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = glow ? FontStyle.Bold : FontStyle.Normal;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            if (glow)
            {
                // Cheap neon halo: two coloured outlines
                Outline o1 = rt.gameObject.AddComponent<Outline>();
                o1.effectColor = Fade(color, 0.35f);
                o1.effectDistance = new Vector2(2, -2);
                Outline o2 = rt.gameObject.AddComponent<Outline>();
                o2.effectColor = Fade(color, 0.15f);
                o2.effectDistance = new Vector2(4, -4);
            }
            return t;
        }

        private static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);

        #endregion

        #region UPDATE

        private void HandleHealthChange(int current, int max)
        {
            if (current < _previousHealth)
                _glitchEnd = Time.time + 0.3f;

            _previousHealth = current;
            _healthValue.text = current.ToString("000");

            float ratio = max > 0 ? (float)current / max : 0f;
            int filled = Mathf.CeilToInt(ratio * HEALTH_SEGMENTS);
            for (int i = 0; i < _healthSegments.Count; i++)
                _healthSegments[i].enabled = true;
            ColorSegments(ratio, filled, 1f);
        }

        private void ColorSegments(float ratio, int filled, float pulse)
        {
            Color c = ratio <= _settings.LowHealth ? _settings.Danger : _settings.Primary;
            c *= pulse;
            c.a = 1f;
            for (int i = 0; i < _healthSegments.Count; i++)
                _healthSegments[i].color = i < filled ? c : new Color(0.08f, 0.08f, 0.14f, 0.9f);
            _healthValue.color = c;
        }

        private void HandleAmmoChange(int current, int max)
        {
            _ammoValue.text = $"{current:00} / {max:00}";
            for (int i = 0; i < _ammoPips.Count; i++)
            {
                _ammoPips[i].enabled = i < Mathf.Min(max, MAX_AMMO_PIPS);
                _ammoPips[i].color = i < current ? _settings.Ammo : new Color(0.12f, 0.1f, 0.05f, 0.9f);
            }
        }

        private void HandleDied()
        {
            _deathGroup.alpha = 1f;
            _glitchEnd = Time.time + 1f;
        }

        private void UpdateLowHealthPulse()
        {
            float ratio = (float)_health.Current / _health.Max;
            if (ratio > _settings.LowHealth || _health.IsDead)
                return;

            float pulse = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 4f));
            ColorSegments(ratio, Mathf.CeilToInt(ratio * HEALTH_SEGMENTS), pulse);
        }

        private void UpdateGlitch()
        {
            bool glitching = Time.time < _glitchEnd;
            Vector2 jitter = glitching ? new Vector2(Random.Range(-8f, 8f), Random.Range(-4f, 4f)) : Vector2.zero;
            _healthPanel.anchoredPosition = _healthPanelPos + jitter;
            _ammoPanel.anchoredPosition = _ammoPanelPos - jitter * 0.5f;
            _healthValue.color = glitching && Random.value < 0.5f ? _settings.Danger : _healthValue.color;

            if (_health.IsDead)
            {
                // The death title keeps glitching
                _deathTitle.rectTransform.anchoredPosition = new Vector2(0, 60) + (Random.value < 0.15f ? new Vector2(Random.Range(-14f, 14f), 0) : Vector2.zero);
                _deathTitle.color = Random.value < 0.08f ? _settings.Primary : _settings.Danger;
            }
        }

        // Zone under the player: raycast to the floor, then up to the room group (e.g. "03_Salle_Machines")
        private void UpdateZone()
        {
            RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
            Transform floor = null;
            float best = float.MaxValue;
            foreach (RaycastHit h in hits)
            {
                // Ignore the player's own colliders
                if (h.collider.transform.IsChildOf(transform) || h.distance >= best)
                    continue;
                best = h.distance;
                floor = h.collider.transform;
            }

            for (Transform t = floor; t; t = t.parent)
            {
                Match m = ROOM_NAME.Match(t.name);
                if (!m.Success)
                    continue;

                if (t.name != _currentZone)
                {
                    _currentZone = t.name;
                    string label = m.Groups[2].Value.Replace('_', ' ').ToUpperInvariant();
                    _zoneNumber.text = $"ZONE {m.Groups[1].Value}";
                    _zoneName.text = label;
                    _banner.text = $"{m.Groups[1].Value}  //  {label}";
                    _bannerTime = Time.time;
                }
                return;
            }
        }

        private void UpdateBanner()
        {
            float t = Time.time - _bannerTime;
            float alpha = t < 0.15f ? t / 0.15f : t < 2f ? 1f : Mathf.Clamp01(1f - (t - 2f) / 0.6f);
            // Flicker while appearing
            if (t < 0.4f && Random.value < 0.3f)
                alpha *= 0.3f;
            _bannerGroup.alpha = alpha;
        }

        private void UpdateHostiles()
        {
            if (Time.time < _nextHostileCheck)
                return;

            _nextHostileCheck = Time.time + 0.5f;
            int count = FindObjectsByType<Arena.AI.AI>(FindObjectsSortMode.None).Length;
            _hostiles.text = $"HOSTILES  {count:00}";
        }

        #endregion
    }
}
