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
    public float spawnRadiusMin = 45f; // Distancia minima do player (escondido no fog)
    public float spawnRadiusMax = 65f; // Distancia maxima (dentro dos 80m de visao)

    private int currentWave = 0;

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) playerTransform = p.transform;
        }

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
                SpawnSingleEnemy();
                yield return new WaitForSeconds(0.4f); // Espacamento leve entre cada spawn
            }

            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    void SpawnSingleEnemy()
    {
        if (enemyPrefab == null || playerTransform == null) return;

        // Sorteia uma direcao aleatoria em 360 graus ao redor do jogador
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        float randomDistance = Random.Range(spawnRadiusMin, spawnRadiusMax);
        Vector3 candidatePosition = playerTransform.position + new Vector3(randomDir.x, 0f, randomDir.y) * randomDistance;

        // Encontra o ponto valido mais proximo na malha de navegacao (NavMesh) do terreno
        if (NavMesh.SamplePosition(candidatePosition, out NavMeshHit hit, 15f, NavMesh.AllAreas))
        {
            Instantiate(enemyPrefab, hit.position, Quaternion.identity);
        }
    }
}