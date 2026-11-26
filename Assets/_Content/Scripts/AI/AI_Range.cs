using Arena.Combat;
using UnityEngine;

namespace Arena.AI
{
    [System.Serializable]
    public class AIRangeSetting
    {
        [Tooltip("Shots fired in a row before pausing.")]
        [Min(1)] public int Burst = 3;
        [Tooltip("Pause between two bursts, in seconds.")]
        [Min(0f)] public float BurstPause = 1.2f;
        [Tooltip("Time before the first shot once in range: the player has a moment to react, in seconds.")]
        [Min(0f)] public float AimTime = 0.5f;
    }

    /// <summary>
    /// Ranged enemy: comes in range with a clear view of the player, aims, then fires bursts.
    /// </summary>
    public class AI_Range : AI
    {
        [SerializeField] ProjectileLauncher launcher;
        [SerializeField] AIRangeSetting setting = new AIRangeSetting();

        private float _inRangeSince = -1f;
        private int _shotsInBurst;
        private float _nextBurst;

        void Start()
        {
            base.Start();
        }

        void Update()
        {
            if (FollowPlayer(basicSetting.RangeToStopFollow))
            {
                _inRangeSince = -1f;
                return;
            }

            if (_inRangeSince < 0f)
                _inRangeSince = Time.time;

            if (Time.time - _inRangeSince < setting.AimTime || Time.time < _nextBurst || !launcher)
                return;

            if (launcher.TryFire() && ++_shotsInBurst >= setting.Burst)
            {
                _shotsInBurst = 0;
                _nextBurst = Time.time + setting.BurstPause;
            }

            // Endless ammo for the enemies
            if (launcher.State.Ammo <= 0)
                launcher.AddAmmo(launcher.MaxAmmo);
        }
    }
}
