using Arena.Characters;
using Arena.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Arena.AI
{
    [System.Serializable]
    public class AIBasicSetting
    {
        [Tooltip("Movement speed, in meters per second.")]
        public float Speed = 3f;

        [Tooltip("Distance to the player at which the AI stops to attack, in meters.")]
        public float RangeToStopFollow = 10f;
    }

    [System.Serializable]
    public class AIAggroSetting
    {
        [Tooltip("Distance at which the AI can spot the player, in meters. Walls block the view.")]
        public float DetectionRadius = 14f;

        [Tooltip("Field of view, in degrees. Closer than 4 m the player is always noticed.")]
        [Range(10f, 360f)] public float FieldOfView = 200f;

        [Tooltip("Time the AI keeps hunting after losing sight of the player, in seconds.")]
        public float LoseSightTime = 6f;

        [Tooltip("Allies within this radius join the fight when the AI spots the player or is hit, in meters.")]
        public float AlertRadius = 9f;

        [Tooltip("Height of the eyes, for the line of sight, in meters.")]
        public float EyeHeight = 1.3f;
    }

    /// <summary>
    /// Base enemy: health, NavMesh movement, walk animation, and aggro.
    /// The AI only hunts the player once it has seen him (line of sight, walls block it), has been hit,
    /// or has been alerted by an ally; it goes back to its post after losing him for a while.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class AI : MonoBehaviour, IDamageable
    {
        #region FIELDS

        const float KMH_WALK = 10f / 3.6f;
        static readonly int ForwardParameter = Animator.StringToHash("Forward");
        static readonly int RightParameter = Animator.StringToHash("Right");
        static readonly int IsGroundedParameter = Animator.StringToHash("IsGrounded");
        static readonly int IsRunningParameter = Animator.StringToHash("IsRunning");

        [SerializeField] protected Health health;
        [SerializeField] protected AIBasicSetting basicSetting = new AIBasicSetting();
        [SerializeField] protected AIAggroSetting aggro = new AIAggroSetting();

        protected Player player;
        protected NavMeshAgent agent;
        private Animator _animator;
        private Vector3 _home;
        private float _lastSeenTime = -100f;
        private Vector3 _lastSeenPosition;
        private bool _deathHandled;
        private float _nextSightCheck;
        private bool _canSee;

        public Health Health => health;
        public bool IsAggro { get; private set; }

        /// <summary>
        /// True if the player is visible right now (refreshed a few times per second).
        /// </summary>
        protected bool CanSeePlayer => _canSee;

        #endregion

        #region UNITY LIFECYCLES

        protected void Start()
        {
            // The health was never initialized: an AI saved with 0 health points could not die
            health.Initialize();
            player = Player.Instance ? Player.Instance : FindAnyObjectByType<Player>();
            agent = GetComponent<NavMeshAgent>();
            agent.speed = basicSetting.Speed;
            agent.acceleration = 20f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.2f;
            agent.autoBraking = true;

            // Make sure the agent stands on the NavMesh (spawned or placed slightly off)
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                agent.Warp(hit.position);

            _home = transform.position;
            _animator = GetComponentInChildren<Animator>();
            if (_animator)
                _animator.SetBool(IsGroundedParameter, true);
        }

        protected virtual void LateUpdate()
        {
            Animate();
        }

        #endregion

        #region PUBLIC METHODS

        public void TakeDamage(DamageInfo damage)
        {
            if (_deathHandled)
                return;

            health.TakeDamage(damage);

            // Being shot reveals the shooter
            if (player && damage.Source == player.gameObject)
                Alert(player.transform.position, true);

            if (health.IsDead)
                OnDeath();
        }

        /// <summary>
        /// Starts hunting the player, seen at this position. Propagates to the nearby allies.
        /// </summary>
        public void Alert(Vector3 playerPosition, bool propagate)
        {
            bool wasAggro = IsAggro;
            IsAggro = true;
            _lastSeenTime = Time.time;
            _lastSeenPosition = playerPosition;

            if (!propagate || wasAggro)
                return;

            foreach (Collider c in Physics.OverlapSphere(transform.position, aggro.AlertRadius, ~0, QueryTriggerInteraction.Ignore))
                if (c.TryGetComponent(out AI ally) && ally != this && !ally.IsAggro && !ally._deathHandled)
                    ally.Alert(playerPosition, false);
        }

        #endregion

        #region PROTECTED METHODS

        /// <summary>
        /// Updates the aggro, then moves towards the player until RangeToStopFollow if needed.
        /// Returns true while moving / idle, false when in range with a clear view: time to attack.
        /// </summary>
        protected bool FollowPlayer(float rangeToStopFollow)
        {
            if (!player || player.Health.IsDead || !agent || !agent.isOnNavMesh)
            {
                StopMoving();
                return true;
            }

            UpdateAggro();

            if (!IsAggro)
            {
                // Back to the post
                if ((transform.position - _home).sqrMagnitude > 1f)
                    MoveTo(_home);
                else
                    StopMoving();
                return true;
            }

            Vector3 target = _canSee ? player.transform.position : _lastSeenPosition;
            float distance = FlatDistance(transform.position, player.transform.position);

            if (_canSee && distance <= rangeToStopFollow)
            {
                StopMoving();
                FacePlayer();
                return false;
            }

            MoveTo(target);
            return true;
        }

        protected void FacePlayer()
        {
            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 720f * Time.deltaTime);
        }

        protected void MoveTo(Vector3 position)
        {
            agent.isStopped = false;
            agent.speed = basicSetting.Speed;
            if ((agent.destination - position).sqrMagnitude > 0.25f || !agent.hasPath)
                agent.SetDestination(position);
        }

        protected void StopMoving()
        {
            if (agent && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }

        protected static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        protected void OnDeath()
        {
            // Several projectiles can hit in the same frame: die (and drop) only once
            if (_deathHandled)
                return;
            _deathHandled = true;

            if (TryGetComponent(out Arena.Interactions.AmmoDrop drop))
                drop.Drop();

            Destroy(gameObject);
        }

        #endregion

        #region PRIVATE METHODS

        private void UpdateAggro()
        {
            if (Time.time >= _nextSightCheck)
            {
                _nextSightCheck = Time.time + 0.15f;
                _canSee = LineOfSight();
            }

            if (_canSee)
                Alert(player.transform.position, !IsAggro);
            else if (IsAggro && Time.time - _lastSeenTime > aggro.LoseSightTime)
                IsAggro = false;
        }

        // The player is in range, in the field of view, and no wall stands in between
        private bool LineOfSight()
        {
            Vector3 eyes = transform.position + Vector3.up * aggro.EyeHeight;
            Vector3 chest = player.transform.position + Vector3.up * 1f;
            Vector3 toPlayer = chest - eyes;
            float distance = toPlayer.magnitude;

            float radius = IsAggro ? aggro.DetectionRadius * 1.5f : aggro.DetectionRadius;
            if (distance > radius)
                return false;

            Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
            if (!IsAggro && distance > 4f && Vector3.Angle(transform.forward, flat) > aggro.FieldOfView * 0.5f)
                return false;

            // The first thing hit on the way must be the player (enemies and triggers are ignored)
            foreach (RaycastHit hit in Physics.RaycastAll(eyes, toPlayer / distance, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform t = hit.collider.transform;
                if (t.IsChildOf(transform) || hit.collider.GetComponentInParent<AI>() || t.IsChildOf(player.transform))
                    continue;
                if (hit.distance < distance - 0.3f)
                    return false;
            }
            return true;
        }

        private void Animate()
        {
            if (!_animator || !agent)
                return;

            Vector3 local = transform.InverseTransformDirection(agent.velocity);
            Vector2 locomotion = Vector2.ClampMagnitude(new Vector2(local.x, local.z) / KMH_WALK, 1f);
            float dt = Time.deltaTime;
            _animator.SetFloat(ForwardParameter, locomotion.y, 0.1f, dt);
            _animator.SetFloat(RightParameter, locomotion.x, 0.1f, dt);
            _animator.SetBool(IsRunningParameter, agent.velocity.magnitude > 3.2f);
        }

        #endregion
    }
}
