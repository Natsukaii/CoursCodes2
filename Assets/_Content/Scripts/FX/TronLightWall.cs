using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena.FX
{
    /// <summary>
    /// Tron light cycle wall: a vertical ribbon of light left on the ground along the path of the object,
    /// with a bright top edge, fading out with time. The mesh is rebuilt each frame in world space.
    /// </summary>
    public class TronLightWall : MonoBehaviour
    {
        [System.Serializable]
        private class Settings
        {
            [ColorUsage(true, true)] public Color Color = new Color(0f, 1.6f, 2.2f, 1f);
            [Tooltip("Height of the wall, in meters.")]
            [Min(0.05f)] public float Height = 1.1f;
            [Tooltip("Time a piece of wall stays, in seconds: the longer, the longer the wall.")]
            [Min(0.1f)] public float Lifetime = 1.4f;
            [Tooltip("Distance between two points of the wall, in meters.")]
            [Min(0.02f)] public float Resolution = 0.15f;
            [Tooltip("Height of the bottom of the wall, in meters.")]
            public float BaseHeight = 0.03f;
        }

        [System.Serializable]
        private struct References
        {
            [Tooltip("URP Particles/Unlit shader.")]
            public Shader NeonShader;
        }

        struct Point
        {
            public Vector3 Position;
            public float Time;
        }

        #region FIELDS

        [SerializeField] private Settings _settings;
        [SerializeField] private References _references;

        private readonly List<Point> _points = new List<Point>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();
        private Mesh _mesh;
        private GameObject _wall;
        private Material _material;

        #endregion

        #region UNITY LIFECYCLES

        void Awake()
        {
            _mesh = new Mesh { name = "TronLightWall" };
            _mesh.MarkDynamic();

            _material = NeonMaterials.CreateAdditive(_references.NeonShader, CreateProfileTexture(), _settings.Color);
            _material.SetFloat("_Cull", 0f);   // visible from both sides

            _wall = new GameObject($"{name}_TronWall");
            _wall.AddComponent<MeshFilter>().sharedMesh = _mesh;
            MeshRenderer r = _wall.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void OnDestroy()
        {
            if (_wall)
                Destroy(_wall);
            if (_mesh)
                Destroy(_mesh);
        }

        void LateUpdate()
        {
            float now = Time.time;
            Vector3 head = transform.position;
            head.y = 0f;

            if (_points.Count > 0)
            {
                float d = Vector3.Distance(_points[_points.Count - 1].Position, head);
                if (d > 4f)
                    _points.Clear();   // teleport: do not draw a line across the level
                else if (d >= _settings.Resolution)
                    _points.Add(new Point { Position = head, Time = now });
            }
            else
                _points.Add(new Point { Position = head, Time = now });

            int expired = 0;
            while (expired < _points.Count && now - _points[expired].Time > _settings.Lifetime)
                expired++;
            if (expired > 0)
                _points.RemoveRange(0, expired);

            Rebuild(head, now);
        }

        #endregion

        #region PUBLIC METHODS

        public void SetColor(Color hdr)
        {
            _settings.Color = hdr;
            if (_material)
                _material.SetColor("_BaseColor", hdr);
        }

        #endregion

        #region PRIVATE METHODS

        private void Rebuild(Vector3 head, float now)
        {
            _vertices.Clear(); _colors.Clear(); _uvs.Clear(); _triangles.Clear();
            int count = _points.Count + 1;
            if (count < 2)
            {
                _mesh.Clear();
                return;
            }

            float bottom = _settings.BaseHeight, top = _settings.BaseHeight + _settings.Height;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = i < _points.Count ? _points[i].Position : head;
                float age = i < _points.Count ? now - _points[i].Time : 0f;
                float fade = Mathf.Clamp01(1f - age / _settings.Lifetime);
                // The wall also lowers as it fades, like a dying light trail
                float h = Mathf.Lerp(bottom + 0.05f, top, Mathf.Sqrt(fade));
                Color c = new Color(1f, 1f, 1f, fade * fade);
                float u = i / (float)(count - 1);

                _vertices.Add(new Vector3(p.x, bottom, p.z));
                _vertices.Add(new Vector3(p.x, h, p.z));
                _colors.Add(c); _colors.Add(c);
                _uvs.Add(new Vector2(u, 0f)); _uvs.Add(new Vector2(u, 1f));

                if (i > 0)
                {
                    int a = (i - 1) * 2;
                    _triangles.Add(a); _triangles.Add(a + 1); _triangles.Add(a + 2);
                    _triangles.Add(a + 2); _triangles.Add(a + 1); _triangles.Add(a + 3);
                }
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();
        }

        // Vertical light profile: soft body, bright line on top, thin line at the bottom
        private static Texture2D CreateProfileTexture()
        {
            const int H = 64;
            Texture2D tex = new Texture2D(4, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < H; y++)
            {
                float v = y / (H - 1f);
                float a = 0.12f + 0.3f * v * v;
                if (v > 0.9f) a = 1f;
                else if (v > 0.84f) a = Mathf.Lerp(a, 1f, (v - 0.84f) / 0.06f);
                if (v < 0.05f) a = 0.8f;
                for (int x = 0; x < 4; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return tex;
        }

        #endregion
    }
}
