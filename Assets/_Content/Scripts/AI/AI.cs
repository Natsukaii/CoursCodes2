using Arena.Characters;
using Arena.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Arena.AI
{
    [System.Serializable]
    public class AIBasicSetting
    {
        public float Speed = 5f;
        public float RangeToStopFollow = 10f;
    }
    
    public class AI : MonoBehaviour, IDamageable
    {
        NavMeshAgent agent;
        protected Player player;
        [SerializeField] protected Health health;
        [SerializeField] protected AIBasicSetting basicSetting = new AIBasicSetting();
        
        
        protected void Start()
        {
            player = FindAnyObjectByType<Player>();
            agent = GetComponent<NavMeshAgent>();
        }
        
        protected bool FollowPlayer(float RangeToStopFollow)
        {
            if (player == null)
            {
                Debug.LogWarning("Player not found");
                return false;
            }

            bool playerHit = false;
            Vector3 origin = transform.position + Vector3.up * 0.9f;
            Vector3 direction = transform.forward;

            Debug.DrawRay(origin, direction * RangeToStopFollow, Color.red, 1f);

            if (Physics.Raycast(origin, direction, out RaycastHit hit, RangeToStopFollow))
            {
                Debug.Log($"Touché : {hit.collider.name}");
                playerHit = hit.collider.CompareTag("Player");
                Debug.Log(playerHit);
            }

            if (Vector3.Distance(transform.position, player.transform.position) > RangeToStopFollow || !playerHit)
            {
                agent.Resume();
                agent.SetDestination(player.transform.position);
                return true;
            }
            else
            {
                agent.Stop();
                transform.rotation = Quaternion.LookRotation(player.transform.position - transform.position);
                return false;
            }
        }

        public void TakeDamage(DamageInfo damage)
        {
            health.TakeDamage(damage);
            if (health.IsDead)
            {
                OnDeath();
            }
        }

        protected void OnDeath()
        {
            Destroy(gameObject);
        }
    }
}
