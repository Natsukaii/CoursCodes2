using System.Collections;
using System.Collections.Generic;
using Arena.Characters;
using Arena.Combat;
using Arena.Interactions;
using Arena.UI;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena.Level
{
    /// <summary>
    /// Generates a level when the game starts: rooms linked by corridors on a 2 m grid, built with the wall prefabs
    /// (auto-tiling), themed floors and walls, Tron neons and decals, lights, reflection probes, crates, ammo and enemies,
    /// then rebuilds the NavMesh and places the player. Once every enemy is dead, a new, harder sector is generated.
    /// </summary>
    public class ProceduralLevel : MonoBehaviour
    {
        [System.Serializable]
        private class Settings
        {
            [Tooltip("0: a new random level at each play. Other values always give the same level.")]
            public int Seed = 0;

            [Min(2)] public int RoomCount = 8;
            [Tooltip("Inside size of the rooms, in cells of 2 m (min, max).")]
            public Vector2Int RoomSize = new Vector2Int(4, 9);
            [Tooltip("Space between two rooms, in cells (min, max).")]
            public Vector2Int Gap = new Vector2Int(2, 5);

            [Tooltip("Enemies in the first room after the spawn; one more every two rooms.")]
            [Min(0)] public int EnemiesBase = 1;
            [Min(1)] public int MaxEnemies = 30;
            [Range(0f, 1f)] public float AmmoCacheChance = 0.4f;

            [Tooltip("Chance that a room holds a bonus (shield, overdrive, heal...).")]
            [Range(0f, 1f)] public float BonusChance = 0.45f;

            [Tooltip("Exit portal in the final room, opened when every enemy is dead. Off: a new sector starts as soon as the sector is cleared.")]
            public bool UseExitPortal = true;
        }

        [System.Serializable]
        private class Theme
        {
            public string Name = "Zone";
            public Material WallMain;
            public Material WallNeon;
            public Material Floor;
            public Material Line;
            public Material[] Decals;
            public Color LightColor = Color.cyan;
        }

        [System.Serializable]
        private struct Pieces
        {
            public GameObject Straight, Corner, T, Cross, DeadEnd, Pillar, Window, Floor;
        }

        [System.Serializable]
        private struct Spawns
        {
            public GameObject[] Enemies;
            public GameObject Crate;
            public Shader NeonShader;
            public Texture2D RingTexture;
            public Material RingDecal;
        }

        #region FIELDS

        const float CELL = 2f;
        const float WALL_TOP = 3.52f;
        const float LINE_OFFSET = 1.32f;
        static readonly Vector2Int[] DIRS = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };   // N E S W

        [SerializeField] private Settings _settings;
        [SerializeField] private Pieces _pieces;
        [SerializeField] private Spawns _spawns;
        [Tooltip("0: spawn, 1: corridors, last: final room. The others are used at random.")]
        [SerializeField] private Theme[] _themes;
        [SerializeField] private NavMeshSurface _navMesh;

        private System.Random _rng;
        private Transform _root;
        private readonly List<RectInt> _rooms = new List<RectInt>();
        private readonly List<int> _roomTheme = new List<int>();
        private readonly Dictionary<Vector2Int, int> _floor = new Dictionary<Vector2Int, int>();   // cell -> room index, -1 corridor
        private readonly HashSet<Vector2Int> _walls = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _blocked = new HashSet<Vector2Int>();             // crates, pillars, spawn
        private readonly List<ReflectionProbe> _probes = new List<ReflectionProbe>();
        private int _sector;
        private int _lastRoom;
        private bool _clearing;
        private float _nextCheck;

        #endregion

        #region UNITY LIFECYCLES

        void Start()
        {
            StartCoroutine(Generate());
        }

        void Update()
        {
            if (_settings.UseExitPortal || _clearing || Time.time < _nextCheck || !_root)
                return;

            _nextCheck = Time.time + 0.5f;
            if (FindObjectsByType<Arena.AI.AI>(FindObjectsSortMode.None).Length == 0)
                StartCoroutine(NextSector());
        }

        #endregion

        #region GENERATION

        private IEnumerator NextSector()
        {
            _clearing = true;
            SetObjective("TRANSFERT VERS LE SECTEUR SUIVANT...");
            yield return new WaitForSeconds(_settings.UseExitPortal ? 0.6f : 3f);
            _sector++;
            yield return Generate();
            _clearing = false;
        }

        private IEnumerator Generate()
        {
            // 1. Loading screen, the player waits frozen
            Player player = Player.Instance;
            NeonHUD hud = player ? player.GetComponent<NeonHUD>() : null;
            hud?.ShowLoading($"SECTEUR {_sector + 1:00}", "GENERATION DU TERRAIN");
            if (player)
                player.enabled = false;

            if (_root)
            {
                Destroy(_root.gameObject);
                yield return null;   // the old level and its NavMesh are gone
            }

            int seed = _settings.Seed != 0 ? _settings.Seed + _sector : System.Environment.TickCount;
            _rng = new System.Random(seed);
            Debug.Log($"[ProceduralLevel] Sector {_sector + 1}, seed {seed}");

            _rooms.Clear(); _roomTheme.Clear(); _floor.Clear(); _walls.Clear(); _blocked.Clear(); _probes.Clear(); _groups.Clear();
            _root = new GameObject($"ProceduralLevel_Sector{_sector + 1:00}").transform;

            // 2. Terrain: layout, walls, floors, decoration, lights, crates
            Layout();
            Decorate();
            BuildWalls();
            BuildFloors();
            BuildNeons();
            BuildLights();
            BuildCrates();

            // 3. Let the physics register the new colliders (and remove the destroyed ones)
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            hud?.ShowLoading($"SECTEUR {_sector + 1:00}", "CALCUL DU NAVMESH");
            yield return null;

            // 4. NavMesh baked from the level geometry only (not the player, not the old level)
            BakeNavMesh();
            yield return null;

            // 5. Player, enemies, pickups, exit
            PlacePlayer();
            SpawnEnemies();
            SpawnPickups();
            SpawnExit();
            SetObjective($"SECTEUR {_sector + 1:00}  //  ELIMINER LES HOSTILES");

            // 6. Reflections, then the game can start
            yield return null;
            foreach (ReflectionProbe probe in _probes)
                if (probe)
                    probe.RenderProbe();
            yield return null;

            hud?.HideLoading();
            if (player)
                player.enabled = true;
        }

        private void BakeNavMesh()
        {
            // The scene NavMesh (hand-made level) is not used here
            if (_navMesh && _navMesh.enabled)
            {
                _navMesh.RemoveData();
                _navMesh.enabled = false;
            }

            NavMeshSurface surface = _root.gameObject.AddComponent<NavMeshSurface>();
            surface.agentTypeID = _navMesh ? _navMesh.agentTypeID : 0;
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
            surface.BuildNavMesh();
        }

        // Rooms placed one after the other next to an existing room, linked by straight corridors
        private void Layout()
        {
            Vector2Int size = RandomRoomSize();
            AddRoom(new RectInt(-size.x / 2, -size.y / 2, size.x, size.y));

            int tries = 0;
            while (_rooms.Count < _settings.RoomCount && tries++ < 400)
            {
                int parentIndex = _rng.Next(_rooms.Count);
                RectInt parent = _rooms[parentIndex];
                Vector2Int dir = DIRS[_rng.Next(4)];
                size = RandomRoomSize();
                int gap = Range(_settings.Gap.x, _settings.Gap.y);
                int width = _rng.NextDouble() < 0.6 ? 1 : 2;

                RectInt room;
                if (dir.x != 0)
                {
                    int x = dir.x > 0 ? parent.xMax + gap : parent.xMin - gap - size.x;
                    int y = Range(parent.yMin - size.y + width + 1, parent.yMax - width - 1);
                    room = new RectInt(x, y, size.x, size.y);
                }
                else
                {
                    int y = dir.y > 0 ? parent.yMax + gap : parent.yMin - gap - size.y;
                    int x = Range(parent.xMin - size.x + width + 1, parent.xMax - width - 1);
                    room = new RectInt(x, y, size.x, size.y);
                }

                if (Overlaps(room, 2))
                    continue;

                AddRoom(room);
                CarveCorridor(parent, room, dir, width);
            }

            // Spawn = first room, final room = the farthest one
            for (int i = 0; i < _rooms.Count; i++)
                _roomTheme.Add(i == 0 ? 0 : Range(2, Mathf.Max(2, _themes.Length - 2)));
            _lastRoom = FarthestRoom();
            _roomTheme[_lastRoom] = _themes.Length - 1;
        }

        private void AddRoom(RectInt r)
        {
            int index = _rooms.Count;
            _rooms.Add(r);
            for (int x = r.xMin; x < r.xMax; x++)
                for (int y = r.yMin; y < r.yMax; y++)
                    _floor[new Vector2Int(x, y)] = index;
        }

        private void CarveCorridor(RectInt a, RectInt b, Vector2Int dir, int width)
        {
            if (dir.x != 0)
            {
                int lo = Mathf.Max(a.yMin, b.yMin), hi = Mathf.Min(a.yMax, b.yMax) - width;
                int y = Range(lo, Mathf.Max(lo, hi));
                int x0 = dir.x > 0 ? a.xMax : b.xMax, x1 = dir.x > 0 ? b.xMin : a.xMin;
                for (int x = x0; x < x1; x++)
                    for (int w = 0; w < width; w++)
                        if (!_floor.ContainsKey(new Vector2Int(x, y + w)))
                            _floor[new Vector2Int(x, y + w)] = -1;
            }
            else
            {
                int lo = Mathf.Max(a.xMin, b.xMin), hi = Mathf.Min(a.xMax, b.xMax) - width;
                int x = Range(lo, Mathf.Max(lo, hi));
                int y0 = dir.y > 0 ? a.yMax : b.yMax, y1 = dir.y > 0 ? b.yMin : a.yMin;
                for (int y = y0; y < y1; y++)
                    for (int w = 0; w < width; w++)
                        if (!_floor.ContainsKey(new Vector2Int(x + w, y)))
                            _floor[new Vector2Int(x + w, y)] = -1;
            }
        }

        // Pillars and short cover walls inside the big rooms (never in the spawn)
        private void Decorate()
        {
            for (int i = 1; i < _rooms.Count; i++)
            {
                RectInt r = _rooms[i];
                if (r.width < 5 || r.height < 5)
                    continue;

                int pillars = Range(1, 1 + (r.width * r.height) / 20);
                for (int p = 0; p < pillars; p++)
                {
                    Vector2Int c = new Vector2Int(Range(r.xMin + 1, r.xMax - 2), Range(r.yMin + 1, r.yMax - 2));
                    if (CanBuildIn(c, i))
                        MakeWall(c);
                }

                if (r.width >= 7 && r.height >= 7 && _rng.NextDouble() < 0.7)
                {
                    bool horizontal = _rng.NextDouble() < 0.5;
                    Vector2Int c = r.position + new Vector2Int(r.width / 2 - 1, r.height / 2);
                    List<Vector2Int> line = new List<Vector2Int>();
                    for (int k = 0; k < 3; k++)
                        line.Add(c + (horizontal ? new Vector2Int(k, 0) : new Vector2Int(0, k - 1)));
                    if (line.TrueForAll(n => CanBuildIn(n, i, line)))
                        line.ForEach(MakeWall);
                }
            }

            // Walls: every empty cell next to a floor cell
            foreach (Vector2Int f in new List<Vector2Int>(_floor.Keys))
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        Vector2Int n = f + new Vector2Int(dx, dy);
                        if (!_floor.ContainsKey(n))
                            _walls.Add(n);
                    }
        }

        private bool CanBuildIn(Vector2Int c, int room, List<Vector2Int> ignore = null)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    Vector2Int n = c + new Vector2Int(dx, dy);
                    if (ignore != null && ignore.Contains(n))
                        continue;
                    if (!_floor.TryGetValue(n, out int r) || r != room)
                        return false;
                }
            return true;
        }

        private void MakeWall(Vector2Int c)
        {
            _floor.Remove(c);
            _walls.Add(c);
            _blocked.Add(c);
        }

        #endregion

        #region BUILD

        private void BuildWalls()
        {
            foreach (Vector2Int c in _walls)
            {
                List<Vector2Int> need = new List<Vector2Int>();
                foreach (Vector2Int d in DIRS)
                    if (_walls.Contains(c + d))
                        need.Add(d);

                bool straight = need.Count == 2 && need[0] == -need[1];
                GameObject prefab; Vector2Int[] arms;
                switch (need.Count)
                {
                    case 0: prefab = _pieces.Pillar; arms = new Vector2Int[0]; break;
                    case 1: prefab = _pieces.DeadEnd; arms = new[] { Vector2Int.down }; break;
                    case 2:
                        if (straight)
                        {
                            // A few windows on long walls between two floors
                            bool window = _pieces.Window && Floor(c + Perp(need[0])) && Floor(c - Perp(need[0])) && _rng.NextDouble() < 0.35;
                            prefab = window ? _pieces.Window : _pieces.Straight;
                            arms = new[] { Vector2Int.right, Vector2Int.left };
                        }
                        else { prefab = _pieces.Corner; arms = new[] { Vector2Int.left, Vector2Int.up }; }
                        break;
                    case 3: prefab = _pieces.T; arms = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up }; break;
                    default: prefab = _pieces.Cross; arms = DIRS; break;
                }

                float angle = SolveRotation(arms, need);
                GameObject wall = Instantiate(prefab, CellToWorld(c), Quaternion.Euler(0f, angle, 0f), Group(WallRoom(c), "Walls"));
                Theme theme = ThemeOf(WallRoom(c));
                MeshRenderer r = wall.GetComponent<MeshRenderer>();
                if (r && theme.WallNeon && theme.WallMain)
                    r.sharedMaterials = new[] { theme.WallNeon, theme.WallMain };
            }
        }

        // Floor tiles: one per cell, stretched under the walls, merged along the rows
        private void BuildFloors()
        {
            Dictionary<(int room, int y, float z0, float z1), List<(float x0, float x1)>> rows = new Dictionary<(int, int, float, float), List<(float, float)>>();
            foreach (KeyValuePair<Vector2Int, int> kv in _floor)
            {
                Vector2Int c = kv.Key;
                Vector3 p = CellToWorld(c);
                float x0 = p.x - Ext(c, Vector2Int.left), x1 = p.x + Ext(c, Vector2Int.right);
                float z0 = p.z - Ext(c, Vector2Int.down), z1 = p.z + Ext(c, Vector2Int.up);
                var key = (kv.Value, c.y, z0, z1);
                if (!rows.TryGetValue(key, out var list))
                    rows[key] = list = new List<(float, float)>();
                list.Add((x0, x1));
            }

            foreach (var kv in rows)
            {
                kv.Value.Sort((a, b) => a.x0.CompareTo(b.x0));
                List<(float x0, float x1)> merged = new List<(float, float)>();
                foreach (var s in kv.Value)
                {
                    if (merged.Count > 0 && Mathf.Abs(merged[merged.Count - 1].x1 - s.x0) < 0.01f)
                        merged[merged.Count - 1] = (merged[merged.Count - 1].x0, s.x1);
                    else
                        merged.Add(s);
                }

                foreach (var s in merged)
                {
                    float z0 = kv.Key.z0, z1 = kv.Key.z1;
                    GameObject tile = Instantiate(_pieces.Floor, new Vector3((s.x0 + s.x1) / 2f, 0.02f, (z0 + z1) / 2f), Quaternion.identity, Group(kv.Key.room, "Floor"));
                    tile.transform.localScale = new Vector3((s.x1 - s.x0) / 2f, 1f, (z1 - z0) / 2f);
                    Theme theme = ThemeOf(kv.Key.room);
                    if (theme.Floor)
                        foreach (MeshRenderer r in tile.GetComponentsInChildren<MeshRenderer>())
                            r.sharedMaterial = theme.Floor;
                }
            }
        }

        private void BuildNeons()
        {
            // Glowing line on top of every wall run
            foreach (bool horizontal in new[] { true, false })
            {
                Vector2Int step = horizontal ? Vector2Int.right : Vector2Int.up;
                foreach (Vector2Int c in _walls)
                {
                    if (_walls.Contains(c - step) || !_walls.Contains(c + step))
                        continue;
                    Vector2Int end = c;
                    while (_walls.Contains(end + step))
                        end += step;
                    Line(WallRoom(c), CellToWorld(c), CellToWorld(end), WALL_TOP, 0.1f, 0.03f);
                }
            }

            // Floor outline 50 cm from the walls, corners joined, stopped at the openings
            Dictionary<(bool h, Vector2Int side, float line, int room), List<Vector2>> segs = new Dictionary<(bool, Vector2Int, float, int), List<Vector2>>();
            foreach (KeyValuePair<Vector2Int, int> kv in _floor)
            {
                Vector2Int c = kv.Key;
                foreach (Vector2Int d in DIRS)
                {
                    if (!_walls.Contains(c + d))
                        continue;
                    Vector2Int along = new Vector2Int(Mathf.Abs(d.y), Mathf.Abs(d.x));
                    float lo = -1f, hi = 1f;
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        Vector2Int nb = c + along * sgn;
                        float e = _walls.Contains(nb) ? LINE_OFFSET : !_walls.Contains(nb + d) ? 0f : 1f;
                        if (sgn < 0) lo = -e; else hi = e;
                    }
                    Vector3 p = CellToWorld(c);
                    bool h = d.y != 0;
                    float line = h ? p.z + d.y * LINE_OFFSET : p.x + d.x * LINE_OFFSET;
                    float a = h ? p.x : p.z;
                    var key = (h, d, Mathf.Round(line * 100f) / 100f, kv.Value);
                    if (!segs.TryGetValue(key, out var list))
                        segs[key] = list = new List<Vector2>();
                    list.Add(new Vector2(a + lo, a + hi));
                }
            }
            foreach (var kv in segs)
            {
                kv.Value.Sort((a, b) => a.x.CompareTo(b.x));
                Vector2 cur = kv.Value[0];
                for (int i = 1; i <= kv.Value.Count; i++)
                {
                    if (i < kv.Value.Count && kv.Value[i].x <= cur.y + 0.01f) { cur.y = Mathf.Max(cur.y, kv.Value[i].y); continue; }
                    if (cur.y - cur.x > 0.3f)
                    {
                        Vector3 p0 = kv.Key.h ? new Vector3(cur.x, 0f, kv.Key.line) : new Vector3(kv.Key.line, 0f, cur.x);
                        Vector3 p1 = kv.Key.h ? new Vector3(cur.y, 0f, kv.Key.line) : new Vector3(kv.Key.line, 0f, cur.y);
                        Line(kv.Key.room, p0, p1, 0.035f, 0.06f, 0.02f);
                    }
                    if (i < kv.Value.Count) cur = kv.Value[i];
                }
            }

            // Decals scattered on the floor
            List<Vector2Int> cells = new List<Vector2Int>(_floor.Keys);
            Shuffle(cells);
            HashSet<Vector2Int> used = new HashSet<Vector2Int>();
            int n = 0;
            foreach (Vector2Int c in cells)
            {
                if (_rng.NextDouble() > 0.3 || NearAny(c, used))
                    continue;
                Theme theme = ThemeOf(_floor[c]);
                if (theme.Decals == null || theme.Decals.Length == 0)
                    continue;
                used.Add(c);
                Decal(_floor[c], theme.Decals[_rng.Next(theme.Decals.Length)], CellToWorld(c), new[] { 1.3f, 1.6f, 1.9f }[_rng.Next(3)], 90f * _rng.Next(4), 0.028f + 0.001f * (n++ % 5));
            }
        }

        private void BuildLights()
        {
            for (int i = 0; i < _rooms.Count; i++)
            {
                RectInt r = _rooms[i];
                Theme theme = ThemeOf(i);
                Vector3 centre = RoomCentre(r);
                bool big = r.width * r.height > 40;
                AddLight(i, centre + (big ? new Vector3(-r.width * 0.45f, 0f, 0f) : Vector3.zero), theme.LightColor, big ? 6f : 4.5f, Mathf.Max(r.width, r.height) * 1.5f + 2f, true);
                if (big)
                    AddLight(i, centre + new Vector3(r.width * 0.45f, 0f, 0f), theme.LightColor, 6f, Mathf.Max(r.width, r.height) * 1.5f, false);

                GameObject probeGo = new GameObject("ReflectionProbe");
                probeGo.transform.SetParent(Group(i, null), false);
                probeGo.transform.position = centre + Vector3.up * 1.6f;
                ReflectionProbe probe = probeGo.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                probe.size = new Vector3(r.width * CELL + 2f, 4f, r.height * CELL + 2f);
                probe.boxProjection = true;
                probe.resolution = 128;
                probe.hdr = true;
                probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
                probe.backgroundColor = new Color(0.015f, 0.01f, 0.025f);
                _probes.Add(probe);
            }

            // Final room: hot core light and target ring
            int last = _lastRoom;
            Vector3 core = RoomCentre(_rooms[last]);
            AddLight(last, core, new Color(0.15f, 0.75f, 0.85f), 6f, 7f, false);
            if (_spawns.RingDecal)
                Decal(last, _spawns.RingDecal, core, Mathf.Min(_rooms[last].width, _rooms[last].height) * CELL * 0.8f, 0f, 0.025f);

            // Dim lights along the corridors
            int k = 0;
            foreach (KeyValuePair<Vector2Int, int> kv in _floor)
                if (kv.Value == -1 && k++ % 5 == 0)
                    AddLight(-1, CellToWorld(kv.Key), ThemeOf(-1).LightColor, 2.5f, 6f, false);
        }

        private void BuildCrates()
        {
            for (int i = 1; i < _rooms.Count; i++)
            {
                int crates = Range(0, 1 + _rooms[i].width * _rooms[i].height / 25);
                for (int c = 0; c < crates && _spawns.Crate; c++)
                {
                    if (!TryFreeCell(i, out Vector2Int cell))
                        continue;
                    _blocked.Add(cell);
                    Vector3 p = CellToWorld(cell);
                    Instantiate(_spawns.Crate, new Vector3(p.x, 0.79f, p.z), Quaternion.Euler(0f, _rng.Next(4) * 15f, 0f), Group(i, "Props"));
                }
            }
        }

        // After the bake: pickups have no collider, but they must not punch holes in the NavMesh
        private void SpawnPickups()
        {
            for (int i = 1; i < _rooms.Count; i++)
            {
                if (_rng.NextDouble() < _settings.AmmoCacheChance && TryFreeCell(i, out Vector2Int ammoCell))
                {
                    _blocked.Add(ammoCell);
                    DamageType type = AmmoTypes.All[_rng.Next(3)];
                    int amount = type == DamageType.Physical ? 15 : type == DamageType.Energy ? 30 : 6;
                    AmmoPickup pickup = AmmoPickup.Spawn(CellToWorld(ammoCell), type, amount, _spawns.NeonShader, _spawns.RingTexture, true);
                    pickup.transform.SetParent(_root, true);
                }

                // Bonus: always one in the final room, random elsewhere
                if ((i == _lastRoom || _rng.NextDouble() < _settings.BonusChance) && TryFreeCell(i, out Vector2Int bonusCell))
                {
                    _blocked.Add(bonusCell);
                    PlayerBuffs.Kind kind = i == _lastRoom ? PlayerBuffs.Kind.Shield : BonusPickup.RandomKind(_rng);
                    BonusPickup bonus = BonusPickup.Spawn(CellToWorld(bonusCell), kind, _spawns.NeonShader, _spawns.RingTexture);
                    bonus.transform.SetParent(_root, true);
                }
            }
        }

        private void SpawnExit()
        {
            if (!_settings.UseExitPortal)
                return;

            // Exit portal in the final room, a bit away from the core light
            RectInt last = _rooms[_lastRoom];
            Vector3 p = RoomCentre(last) + new Vector3(0f, 0f, (last.height / 2 - 1) * CELL * 0.7f);
            LevelExit exit = new GameObject("ExitPortal").AddComponent<LevelExit>();
            exit.transform.SetParent(_root, false);
            exit.transform.position = new Vector3(p.x, 0f, p.z);
            exit.Configure("", true, _spawns.NeonShader, _spawns.RingTexture);
            exit.OnEntered += () => { if (!_clearing) StartCoroutine(NextSector()); };
        }

        private void PlacePlayer()
        {
            RectInt spawn = _rooms[0];
            Vector3 p = RoomCentre(spawn);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    _blocked.Add(WorldToCell(p) + new Vector2Int(dx, dy));

            if (Player.Instance)
                Player.Instance.Teleport(new Vector4(p.x, 0f, p.z, 0f));
        }

        private void SpawnEnemies()
        {
            if (_spawns.Enemies == null || _spawns.Enemies.Length == 0)
                return;

            int total = 0, last = _lastRoom;
            for (int i = 1; i < _rooms.Count && total < _settings.MaxEnemies; i++)
            {
                int count = _settings.EnemiesBase + i / 2 + _sector + (i == last ? 3 : 0);
                for (int e = 0; e < count && total < _settings.MaxEnemies; e++)
                {
                    if (!TryFreeCell(i, out Vector2Int cell))
                        break;
                    _blocked.Add(cell);
                    GameObject prefab = _spawns.Enemies[_rng.Next(_spawns.Enemies.Length)];
                    Vector3 position = CellToWorld(cell);
                    if (UnityEngine.AI.NavMesh.SamplePosition(position, out UnityEngine.AI.NavMeshHit hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
                        position = hit.position;
                    else
                        continue;   // no NavMesh here: no enemy
                    Instantiate(prefab, position, Quaternion.Euler(0f, _rng.Next(4) * 90f, 0f), Group(i, "Enemies"));
                    total++;
                }
            }
        }

        #endregion

        #region HELPERS

        private void Line(int room, Vector3 a, Vector3 b, float y, float width, float height)
        {
            Theme theme = ThemeOf(room);
            if (!theme.Line)
                return;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Neon";
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(Group(room, "Neons"), false);
            go.transform.position = new Vector3((a.x + b.x) / 2f, y, (a.z + b.z) / 2f);
            bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
            float length = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));
            go.transform.localScale = alongX ? new Vector3(length, height, width) : new Vector3(width, height, length);
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = theme.Line;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void Decal(int room, Material material, Vector3 position, float size, float yaw, float y)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Decal";
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(Group(room, "Decals"), false);
            go.transform.SetPositionAndRotation(new Vector3(position.x, y, position.z), Quaternion.Euler(90f, yaw, 0f));
            go.transform.localScale = new Vector3(size, size, 1f);
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void AddLight(int room, Vector3 position, Color color, float intensity, float range, bool shadows)
        {
            GameObject go = new GameObject("PointLight");
            go.transform.SetParent(Group(room, "Lights"), false);
            go.transform.position = new Vector3(position.x, 3f, position.z);
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        private readonly Dictionary<(int, string), Transform> _groups = new Dictionary<(int, string), Transform>();
        private Transform Group(int room, string sub)
        {
            if (!_root)
                return null;
            if (_groups.TryGetValue((room, sub), out Transform t) && t)
                return t;

            Transform parent;
            if (sub == null)
            {
                // Room names follow "NN_Name" so that the HUD shows the zone
                string name = room < 0 ? "Corridors" : $"{room + 1:00}_{ThemeOf(room).Name}";
                parent = new GameObject(name).transform;
                parent.SetParent(_root, false);
            }
            else
            {
                parent = new GameObject(sub).transform;
                parent.SetParent(Group(room, null), false);
            }
            _groups[(room, sub)] = parent;
            return parent;
        }

        private Theme ThemeOf(int room)
        {
            int index = room < 0 ? 1 : _roomTheme.Count > room ? _roomTheme[room] : 0;
            return _themes[Mathf.Clamp(index, 0, _themes.Length - 1)];
        }

        private int WallRoom(Vector2Int c)
        {
            Dictionary<int, int> count = new Dictionary<int, int>();
            int best = -1, bestCount = -1;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (_floor.TryGetValue(c + new Vector2Int(dx, dy), out int r))
                    {
                        int score = (dx == 0 || dy == 0 ? 2 : 1) + (r >= 0 ? 1 : 0);
                        count[r] = count.TryGetValue(r, out int v) ? v + score : score;
                        if (count[r] > bestCount) { bestCount = count[r]; best = r; }
                    }
            return best;
        }

        private int FarthestRoom()
        {
            // Breadth-first search on the floor from the spawn room
            Vector2Int start = new Vector2Int(_rooms[0].xMin, _rooms[0].yMin);
            Dictionary<Vector2Int, int> dist = new Dictionary<Vector2Int, int> { [start] = 0 };
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                foreach (Vector2Int d in DIRS)
                {
                    Vector2Int n = c + d;
                    if (_floor.ContainsKey(n) && !dist.ContainsKey(n))
                    {
                        dist[n] = dist[c] + 1;
                        queue.Enqueue(n);
                    }
                }
            }
            int best = _rooms.Count - 1, bestDist = -1;
            for (int i = 1; i < _rooms.Count; i++)
            {
                RectInt r = _rooms[i];
                int roomDist = -1;
                for (int x = r.xMin; x < r.xMax; x++)
                    for (int y = r.yMin; y < r.yMax; y++)
                        if (dist.TryGetValue(new Vector2Int(x, y), out int dd))
                            roomDist = Mathf.Max(roomDist, dd);
                if (roomDist > bestDist) { bestDist = roomDist; best = i; }
            }
            return best;
        }

        private bool TryFreeCell(int room, out Vector2Int cell)
        {
            RectInt r = _rooms[room];
            for (int t = 0; t < 30; t++)
            {
                cell = new Vector2Int(Range(r.xMin, r.xMax - 1), Range(r.yMin, r.yMax - 1));
                if (_floor.TryGetValue(cell, out int f) && f == room && !_blocked.Contains(cell))
                    return true;
            }
            cell = default;
            return false;
        }

        private bool Overlaps(RectInt r, int margin)
        {
            RectInt grown = new RectInt(r.xMin - margin, r.yMin - margin, r.width + margin * 2, r.height + margin * 2);
            foreach (RectInt o in _rooms)
                if (grown.Overlaps(o))
                    return true;
            return false;
        }

        private float Ext(Vector2Int c, Vector2Int d) => _walls.Contains(c + d) ? 2f : 1f;
        private bool Floor(Vector2Int c) => _floor.ContainsKey(c);
        private static Vector2Int Perp(Vector2Int d) => new Vector2Int(-d.y, d.x);
        private bool NearAny(Vector2Int c, HashSet<Vector2Int> set)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (set.Contains(c + new Vector2Int(dx, dy)) || _blocked.Contains(c + new Vector2Int(dx, dy)))
                        return true;
            return false;
        }

        private static Vector3 CellToWorld(Vector2Int c) => new Vector3(c.x * CELL, 0f, c.y * CELL);
        private static Vector2Int WorldToCell(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x / CELL), Mathf.RoundToInt(p.z / CELL));
        private static Vector3 RoomCentre(RectInt r) => new Vector3((r.xMin + r.xMax - 1) * 0.5f * CELL, 0f, (r.yMin + r.yMax - 1) * 0.5f * CELL);

        // Rotation (multiple of 90 deg) that turns the prefab arms into the needed neighbours.
        // Unity yaw: N -> E -> S -> W for +90 deg
        private static float SolveRotation(Vector2Int[] arms, List<Vector2Int> need)
        {
            for (int k = 0; k < 4; k++)
            {
                bool ok = arms.Length == need.Count;
                foreach (Vector2Int a in arms)
                {
                    Vector2Int r = a;
                    for (int i = 0; i < k; i++)
                        r = new Vector2Int(r.y, -r.x);
                    if (!need.Contains(r)) { ok = false; break; }
                }
                if (ok)
                    return k * 90f;
            }
            return 0f;
        }

        private Vector2Int RandomRoomSize() => new Vector2Int(Range(_settings.RoomSize.x, _settings.RoomSize.y), Range(_settings.RoomSize.x, _settings.RoomSize.y));
        private int Range(int min, int maxInclusive) => maxInclusive <= min ? min : _rng.Next(min, maxInclusive + 1);
        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void SetObjective(string text)
        {
            if (Player.Instance && Player.Instance.TryGetComponent(out NeonHUD hud))
                hud.SetObjective(text);
        }

        #endregion
    }
}
