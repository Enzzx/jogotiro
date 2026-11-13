using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform playerTransform;
    public float catchDistance = 1.6f;
    private bool isStopped = false;
    public AudioSource enemyFootsteps;

    [Header("Drop de Municao")]
    public GameObject ammoDropPrefab;           // Arraste o Prefab AmmoBox aqui
    [Range(0f, 1f)] public float dropChance = 0.35f; // 35% de chance de dropar balas

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    void Update()
    {
        if (isStopped || playerTransform == null) return;

        if (agent.isOnNavMesh)
        {
            agent.SetDestination(playerTransform.position);
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        if (distance <= catchDistance)
        {
            if (GameRulesManager.Instance != null)
            {
                GameRulesManager.Instance.OnPlayerCaught();
            }
        }
    }

    public void StopChasing()
    {
        isStopped = true;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
    }

    public void Die()
    {
        // Sorteia se o inimigo deixa cair municao no chao
        if (ammoDropPrefab != null && Random.value <= dropChance)
        {
            Vector3 dropPos = transform.position + Vector3.up * 0.3f;
            Instantiate(ammoDropPrefab, dropPos, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}