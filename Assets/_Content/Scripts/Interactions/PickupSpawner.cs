using Arena.Characters;
using UnityEngine;

namespace Arena.Interactions
{
    /// <summary>
    /// Places bonuses in a hand-made level when the game starts. Each entry: a position and a bonus.
    /// </summary>
    public class PickupSpawner : MonoBehaviour
    {
        [System.Serializable]
        private struct Entry
        {
            public Vector3 Position;
            public PlayerBuffs.Kind Kind;
        }

        [System.Serializable]
        private struct References
        {
            [Tooltip("URP Particles/Unlit shader, for the glow.")]
            public Shader NeonShader;
            public Texture2D RingTexture;
        }

        [SerializeField] private Entry[] _entries;
        [SerializeField] private References _references;

        void Start()
        {
            foreach (Entry e in _entries)
            {
                BonusPickup p = BonusPickup.Spawn(e.Position, e.Kind, _references.NeonShader, _references.RingTexture);
                p.transform.SetParent(transform, true);
            }
        }

        void OnDrawGizmos()
        {
            if (_entries == null)
                return;
            foreach (Entry e in _entries)
            {
                Gizmos.color = PlayerBuffs.Color(e.Kind);
                Gizmos.DrawWireCube(e.Position + Vector3.up * 0.7f, Vector3.one * 0.5f);
            }
        }
    }
}
