using UnityEngine;
using UnityEngine.AI;

// Espalha caixas de municao aleatoriamente pelo terreno no inicio da partida.
public class AmmoSpawner : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject ammoBoxPrefab;
    public Terrain terrain;
    public Transform playerTransform;

    [Header("Distribuicao")]
    public int amount = 27;
    public float minDistanceFromPlayer = 15f;
    public float edgeMargin = 15f;
    public float heightOffset = 0.15f;

    void Start()
    {
        SpawnAll();
    }

    public void SpawnAll()
    {
        if (ammoBoxPrefab == null)
        {
            Debug.LogWarning("AmmoSpawner: ammoBoxPrefab nao atribuido.");
            return;
        }

        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("AmmoSpawner: nenhum Terrain encontrado.");
            return;
        }

        if (playerTransform == null)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null) playerTransform = player.transform;
        }

        Vector3 terrainPos = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        int spawned = 0;
        int attempts = 0;
        int maxAttempts = amount * 40;

        while (spawned < amount && attempts < maxAttempts)
        {
            attempts++;

            float x = terrainPos.x + Random.Range(edgeMargin, size.x - edgeMargin);
            float z = terrainPos.z + Random.Range(edgeMargin, size.z - edgeMargin);
            float y = terrainPos.y + terrain.SampleHeight(new Vector3(x, terrainPos.y, z));
            Vector3 candidate = new Vector3(x, y + 1f, z);

            if (playerTransform != null &&
                Vector3.Distance(candidate, playerTransform.position) < minDistanceFromPlayer)
                continue;

            // Coloca apenas em locais caminhaveis (NavMesh) para nunca ficar inalcancavel.
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                continue;

            Vector3 spawnPos = hit.position + Vector3.up * heightOffset;
            GameObject box = Instantiate(ammoBoxPrefab, spawnPos,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            box.name = "AmmoBox";
            spawned++;
        }

        Debug.Log("AmmoSpawner: " + spawned + " caixas de municao espalhadas.");
    }
}
