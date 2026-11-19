using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class HordeSpawner : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject enemyPrefab;   // Arraste o Prefab Enemy aqui
    public Transform playerTransform;  // Arraste o Player aqui

    [Header("Configuracao das Hordas")]
    public int initialEnemiesPerWave = 5;
    public int extraEnemiesPerWave = 3;
    public float timeBetweenWaves = 12f;
    public float spawnRadiusMin = 24f; // Distancia minima do player (dentro da neblina)
    public float spawnRadiusMax = 36f; // Distancia maxima (a neblina termina em 40m)
    [Tooltip("Tentativas para achar um ponto valido no NavMesh antes de desistir de um inimigo.")]
    public int spawnAttempts = 15;
    [Tooltip("Distancia maxima (em altura) entre o NavMesh e o terreno para o ponto valer como chao.")]
    public float maxGroundGap = 1.5f;
    [Tooltip("Evita nascer inimigos na frente do jogador: eles so aparecem quando ele olha para os lados/para tras, sem agredir no instante do spawn.")]
    public bool avoidPlayerView = true;
    [Range(0f, 0.5f)]
    [Tooltip("Margem extra (em viewport) para considerar um ponto como 'na frente do jogador'.")]
    public float viewMargin = 0.08f;
    [Tooltip("Limite de inimigos vivos ao mesmo tempo (protege a performance).")]
    public int maxAliveEnemies = 20;

    private int currentWave = 0;
    private Camera playerCamera;

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (playerTransform != null)
            playerCamera = playerTransform.GetComponentInChildren<Camera>(true);
        if (playerCamera == null)
            playerCamera = Camera.main;

        StartCoroutine(SpawnWavesRoutine());
    }

    IEnumerator SpawnWavesRoutine()
    {
        // Pequena pausa inicial antes da primeira horda surgir
        yield return new WaitForSeconds(3f);

        while (true)
        {
            currentWave++;
            int countToSpawn = initialEnemiesPerWave + (currentWave - 1) * extraEnemiesPerWave;
            Debug.Log("Iniciando Horda " + currentWave + " com " + countToSpawn + " inimigos.");

            for (int i = 0; i < countToSpawn; i++)
            {
                if (CountAliveEnemies() >= maxAliveEnemies) break;

                SpawnSingleEnemy();
                yield return new WaitForSeconds(0.4f); // Espacamento leve entre cada spawn
            }

            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    int CountAliveEnemies()
    {
        return FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude).Length;
    }

    void SpawnSingleEnemy()
    {
        if (enemyPrefab == null || playerTransform == null) return;

        Terrain terrain = Terrain.activeTerrain;

        // Tenta varias posicoes ate achar uma que esteja realmente no chao do NavMesh
        // (evita pontos em cima de copas/pedras, onde o inimigo nasceria preso).
        for (int attempt = 0; attempt < spawnAttempts; attempt++)
        {
            Vector2 randomDir = Random.insideUnitCircle;
            if (randomDir.sqrMagnitude < 0.0001f) continue;
            randomDir.Normalize();

            float randomDistance = Random.Range(spawnRadiusMin, spawnRadiusMax);
            Vector3 candidatePosition = playerTransform.position + new Vector3(randomDir.x, 0f, randomDir.y) * randomDistance;

            // Usa a altura do terreno como referencia para achar o NavMesh certo
            // (evita cair em pedacos de malha que estao acima ou abaixo do chao).
            if (terrain != null)
                candidatePosition.y = terrain.SampleHeight(candidatePosition);

            // Encontra o ponto valido mais proximo na malha de navegacao (NavMesh) do terreno
            if (!NavMesh.SamplePosition(candidatePosition, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                continue;

            // Descarta pontos muito acima/abaixo do terreno (ilhas do NavMesh em cima de colliders).
            if (terrain != null && Mathf.Abs(hit.position.y - terrain.SampleHeight(hit.position)) > maxGroundGap)
                continue;

            // Nao nasce na frente do jogador: evita inimigos surgindo "na cara" e
            // ficando agressivos antes de o jogador conseguir reagir.
            if (avoidPlayerView && IsInsidePlayerView(hit.position))
                continue;

            GameObject enemy = Instantiate(enemyPrefab, hit.position, Quaternion.identity);
            EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
            if (enemyAI != null) enemyAI.SnapToGround();
            return;
        }
    }

    bool IsInsidePlayerView(Vector3 worldPosition)
    {
        if (playerCamera == null) return false;

        Vector3 viewport = playerCamera.WorldToViewportPoint(worldPosition + Vector3.up * 1f);

        // Atras da camera: nao esta sendo visto.
        if (viewport.z <= 0f) return false;

        return viewport.x > -viewMargin && viewport.x < 1f + viewMargin &&
               viewport.y > -viewMargin && viewport.y < 1f + viewMargin;
    }
}