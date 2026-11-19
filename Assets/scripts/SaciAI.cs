using UnityEngine;
using UnityEngine.AI;

// Saci que corre pelo mapa pulando, sem atacar o jogador.
// Quando o player chega perto, ele comeca a rir (som continuo, em loop).
[RequireComponent(typeof(NavMeshAgent))]
public class SaciAI : MonoBehaviour
{
    [Header("Movimento")]
    [Tooltip("Velocidade media enquanto percorre o mapa.")]
    public float roamSpeed = 4.2f;
    [Tooltip("Raio maximo de um destino sorteado (a partir da posicao atual).")]
    public float roamRadius = 60f;
    [Tooltip("Altura dos pulos.")]
    public float hopHeight = 0.45f;
    [Tooltip("Pulos por segundo.")]
    public float hopFrequency = 2.4f;
    [Tooltip("Tempo minimo entre a escolha de dois destinos.")]
    public float roamTimeMin = 3f;
    public float roamTimeMax = 7f;

    [Header("Risada")]
    [Tooltip("Distancia a partir da qual o jogador comeca a ouvir a risada.")]
    public float laughRadius = 28f;
    [Range(0f, 1f)] public float laughVolume = 0.8f;
    public float laughFadeSpeed = 2.5f;
    public float pitchChangeInterval = 2.5f;

    [Header("Chao")]
    public float groundOffset = 0f;

    private NavMeshAgent agent;
    private Transform player;
    private Transform visual;
    private Vector3 visualBasePosition;
    private AudioSource laughSource;
    private float hopPhase;
    private float nextRoamTime;
    private float nextPitchTime;
    private float currentVolume;
    private Terrain terrain;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        terrain = Terrain.activeTerrain;

        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;

        // O modelo fica como filho do root: o root anda no NavMesh e o filho pula.
        visual = transform.childCount > 0 ? transform.GetChild(0) : transform;
        visualBasePosition = visual.localPosition;

        laughSource = GetComponent<AudioSource>();
        if (laughSource == null) laughSource = gameObject.AddComponent<AudioSource>();

        laughSource.clip = CreateLaughClip();
        laughSource.loop = true;
        laughSource.playOnAwake = false;
        laughSource.spatialBlend = 0.85f;
        laughSource.rolloffMode = AudioRolloffMode.Linear;
        laughSource.minDistance = 2f;
        laughSource.maxDistance = laughRadius * 1.2f;
        laughSource.volume = 0f;

        nextRoamTime = 0f;
        nextPitchTime = Time.time + Random.Range(1f, pitchChangeInterval);
    }

    void Start()
    {
        SnapToTerrain();

        if (agent != null)
        {
            agent.speed = roamSpeed;
            agent.angularSpeed = 360f;
            agent.acceleration = 18f;
            agent.stoppingDistance = 0.1f;
            agent.updateRotation = true;
        }
    }

    void Update()
    {
        UpdateLaugh();

        if (agent == null || !agent.isOnNavMesh) return;

        bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;
        if (Time.time >= nextRoamTime || arrived)
            PickNewDestination();

        UpdateHop();
    }

    void LateUpdate()
    {
        // Pequeno ajuste para nao afundar/flutuar em encostas.
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null || agent == null || !agent.isOnNavMesh) return;

        float terrainY = terrain.SampleHeight(transform.position) + groundOffset;
        Vector3 navPosition = transform.position - Vector3.up * agent.baseOffset;
        float desiredOffset = terrainY - navPosition.y;
        if (Mathf.Abs(agent.baseOffset - desiredOffset) > 0.02f)
            agent.baseOffset = Mathf.Clamp(desiredOffset, -50f, 50f);
    }

    void PickNewDestination()
    {
        nextRoamTime = Time.time + Random.Range(roamTimeMin, roamTimeMax);

        Vector3 randomPoint = transform.position + Random.insideUnitSphere * roamRadius;
        randomPoint.y = transform.position.y;

        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, roamRadius, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    void UpdateHop()
    {
        if (visual == null) return;

        float speed = agent.velocity.magnitude;
        bool moving = speed > 0.15f;

        if (moving)
            hopPhase += Time.deltaTime * hopFrequency;
        else
            hopPhase = Mathf.Repeat(hopPhase + Time.deltaTime * 0.6f, 1f);

        float hop = Mathf.Abs(Mathf.Sin(hopPhase * Mathf.PI)) * hopHeight;
        Vector3 pos = visualBasePosition;
        pos.y += moving ? hop : 0f;
        visual.localPosition = pos;
    }

    void UpdateLaugh()
    {
        if (player == null || laughSource == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        float target = distance <= laughRadius
            ? Mathf.Lerp(laughVolume, laughVolume * 0.12f, distance / laughRadius)
            : 0f;

        currentVolume = Mathf.MoveTowards(currentVolume, target, laughFadeSpeed * Time.deltaTime);
        laughSource.volume = currentVolume;

        if (currentVolume > 0.005f)
        {
            if (!laughSource.isPlaying) laughSource.Play();
        }
        else if (laughSource.isPlaying)
        {
            laughSource.Stop();
        }

        if (Time.time >= nextPitchTime)
        {
            nextPitchTime = Time.time + Random.Range(pitchChangeInterval * 0.6f, pitchChangeInterval * 1.4f);
            laughSource.pitch = Random.Range(0.92f, 1.12f);
        }
    }

    void SnapToTerrain()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        Vector3 pos = transform.position;
        pos.y = terrain.SampleHeight(pos) + groundOffset;
        transform.position = pos;
    }

    // Gera uma risada provisoria (nada de arquivo externo): varias gargalhadas curtas
    // com tom que cai dentro de cada "ha", separadas por pequenas pausas.
    static AudioClip CreateLaughClip()
    {
        const int sampleRate = 44100;
        const float duration = 3.4f;
        int length = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[length];
        var rnd = new System.Random(11);

        float cursor = 0.12f;
        for (int burst = 0; burst < 7; burst++)
        {
            float burstDuration = 0.20f + (float)rnd.NextDouble() * 0.07f;
            int start = Mathf.RoundToInt(cursor * sampleRate);
            int count = Mathf.RoundToInt(burstDuration * sampleRate);
            float baseFrequency = 320f + (float)rnd.NextDouble() * 140f;
            float phase = 0f;
            float noise = 0f;

            for (int i = 0; i < count; i++)
            {
                int index = start + i;
                if (index >= length) break;

                float progress = (float)i / count;
                float envelope = Mathf.Sin(Mathf.Clamp01(progress / 0.10f) * Mathf.PI * 0.5f) *
                                 Mathf.Pow(1f - progress, 1.5f);

                float frequency = baseFrequency * Mathf.Lerp(1.18f, 0.70f, progress);
                phase += 2f * Mathf.PI * frequency / sampleRate;

                float tone = Mathf.Sin(phase) * 0.60f + Mathf.Sin(phase * 2.01f) * 0.22f;

                // Ruido suave (media movel curta) para dar o "sopro" do "ha".
                noise = Mathf.Lerp(noise, (float)rnd.NextDouble() * 2f - 1f, 0.35f);

                data[index] += (tone + noise * 0.5f) * envelope * 0.42f;
            }

            cursor += burstDuration + 0.11f + (float)rnd.NextDouble() * 0.13f;
            if (cursor > duration - 0.35f) break;
        }

        // Fade nas pontas para o loop nao estalar.
        int fade = Mathf.RoundToInt(sampleRate * 0.02f);
        for (int i = 0; i < fade; i++)
        {
            float k = (float)i / fade;
            data[i] *= k;
            data[length - 1 - i] *= k;
        }

        for (int i = 0; i < length; i++)
            data[i] = Mathf.Clamp(data[i], -1f, 1f);

        AudioClip clip = AudioClip.Create("SaciLaugh", length, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
