using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform playerTransform;
    private Camera playerCamera;
    public float catchDistance = 1.6f;
    private bool isStopped = false;

    [Header("Percepcao")]
    [Tooltip("Distancia maxima para o inimigo perceber o jogador olhando para ele.")]
    public float sightRange = 70f;
    public float sightCheckInterval = 0.25f;
    [Tooltip("Raio de perambulada enquanto o jogador ainda nao viu o inimigo.")]
    public float wanderRadius = 12f;
    public float wanderSpeed = 1.8f;
    public float chaseSpeed = 4.5f;

    [Header("Modelos (Noite / Dia)")]
    [Tooltip("Modelo usado a noite (Ch30).")]
    public GameObject nightModel;
    [Tooltip("Modelo usado de dia (mesmo personagem do jogador, Ch23).")]
    public GameObject dayModel;
    [Tooltip("A partir de quanto do dia (0 = noite, 1 = dia) o inimigo troca de modelo.")]
    [Range(0f, 1f)] public float dayModelThreshold = 0.5f;

    [Header("Danca")]
    [Tooltip("Chance, ao ser visto pela primeira vez, de dancar em vez de perseguir.")]
    [Range(0f, 1f)] public float danceChance = 0.15f;
    public float danceDurationMin = 4f;
    public float danceDurationMax = 7f;
    [Tooltip("Tempo apos a danca em que ele nao parte para cima do jogador.")]
    public float danceCooldown = 8f;

    [Header("Audio de Passos")]
    public AudioSource enemyFootsteps;
    public AudioClip footstepClip;
    [Range(0f, 1f)] public float footstepVolume = 0.5f;
    public float footstepMoveThreshold = 0.1f;

    [Header("Drop de Municao")]
    public GameObject ammoDropPrefab;           // Arraste o Prefab AmmoBox aqui
    [Range(0f, 1f)] public float dropChance = 0.35f; // 35% de chance de dropar balas

    [Header("Performance")]
    [Tooltip("Distancia a partir da qual o corpo do inimigo deixa de ser renderizado (a neblina ja esconde).")]
    public float renderDistance = 45f;
    public float renderCheckInterval = 0.5f;

    private bool isAggro = false;
    private bool isDancing = false;
    private float nextSightCheck;
    private float nextWanderTime;
    private float nextRenderCheck;
    private float danceEndTime;
    private float danceCooldownEnd;
    private Animator currentAnimator;
    private GameObject currentModel;
    private bool modelRenderersEnabled = true;
    private EnemyNightShade nightShade;
    private readonly RaycastHit[] lineOfSightHits = new RaycastHit[8];

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        nightShade = GetComponent<EnemyNightShade>();

        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerCamera = playerObj.GetComponentInChildren<Camera>();
        }
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (agent != null)
            agent.speed = wanderSpeed;

        // Cada inimigo checa a visao em um instante diferente, para distribuir o custo.
        nextSightCheck = Time.time + Random.Range(0f, sightCheckInterval);
        nextWanderTime = 0f;

        // Escolhe o modelo correto ja no nascimento (se ninguem estiver vendo).
        bool isDay = DayNightTransition.DayFactor >= dayModelThreshold;
        bool canSeeMe = PlayerCanSeeMe();
        SetNightModel(!isDay || canSeeMe);

        if (enemyFootsteps != null)
        {
            enemyFootsteps.loop = true;
            enemyFootsteps.playOnAwake = false;
            enemyFootsteps.spatialBlend = 1f;
            enemyFootsteps.volume = footstepVolume;
            if (footstepClip != null)
                enemyFootsteps.clip = footstepClip;
        }
    }

    void Update()
    {
        if (isStopped || playerTransform == null) return;

        // Esconde o corpo do inimigo quando ele esta longe demais (a neblina ja esconde).
        if (Time.time >= nextRenderCheck)
        {
            nextRenderCheck = Time.time + renderCheckInterval;
            ApplyModelVisibility(false);
        }

        // Checagem de visao: usada para agredir, dancar e trocar de modelo.
        if (Time.time >= nextSightCheck)
        {
            nextSightCheck = Time.time + sightCheckInterval;

            bool canSeeMe = PlayerCanSeeMe();

            // Troca de modelo apenas quando o jogador NAO esta olhando.
            UpdateDayModel(canSeeMe);

            if (!isAggro && !isDancing && Time.time >= danceCooldownEnd && canSeeMe)
            {
                if (Random.value < danceChance) StartDance();
                else BecomeAggro();
            }
        }

        if (isDancing)
        {
            if (Time.time >= danceEndTime) EndDance();
            UpdateAnimation();
            UpdateFootstepSound();
            return;
        }

        // Antes de ser visto, o inimigo apenas perambula pelo mapa.
        if (!isAggro)
        {
            Wander();
            UpdateAnimation();
            UpdateFootstepSound();
            return;
        }

        // Perseguicao
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

        UpdateAnimation();
        UpdateFootstepSound();
    }

    void BecomeAggro()
    {
        isAggro = true;
        if (agent != null)
            agent.speed = chaseSpeed;
    }

    // ---- Danca ----

    void StartDance()
    {
        isDancing = true;
        danceEndTime = Time.time + Random.Range(danceDurationMin, danceDurationMax);

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (currentAnimator != null)
            currentAnimator.SetBool("Dancing", true);
    }

    void EndDance()
    {
        isDancing = false;
        danceCooldownEnd = Time.time + danceCooldown;
        nextWanderTime = 0f;

        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = false;

        if (currentAnimator != null)
            currentAnimator.SetBool("Dancing", false);
    }

    // ---- Modelo Noite / Dia ----

    void UpdateDayModel(bool canSeeMe)
    {
        if (nightModel == null || dayModel == null) return;

        bool wantNightModel = DayNightTransition.DayFactor < dayModelThreshold;
        bool usingNightModel = nightModel.activeSelf;

        // Nunca troca enquanto o jogador estiver olhando para o inimigo.
        if (wantNightModel == usingNightModel) return;
        if (canSeeMe) return;

        SetNightModel(wantNightModel);
    }

    void SetNightModel(bool night)
    {
        if (nightModel != null) nightModel.SetActive(night);
        if (dayModel != null) dayModel.SetActive(!night);

        GameObject activeModel = night ? nightModel : dayModel;
        currentModel = activeModel;
        currentAnimator = activeModel != null ? activeModel.GetComponent<Animator>() : null;

        modelRenderersEnabled = true;
        ApplyModelVisibility(true);

        if (nightShade != null) nightShade.RefreshRenderers();
        UpdateAnimation();
    }

    // Liga/desliga os renderers do modelo ativo conforme a distancia.
    void ApplyModelVisibility(bool force)
    {
        if (currentModel == null || playerTransform == null) return;

        bool visible = Vector3.Distance(transform.position, playerTransform.position) <= renderDistance;
        if (!force && visible == modelRenderersEnabled) return;

        modelRenderersEnabled = visible;

        Renderer[] renderers = currentModel.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = visible;

        if (visible && nightShade != null) nightShade.RefreshRenderers();
    }

    void UpdateAnimation()
    {
        if (currentAnimator == null) return;

        bool moving = !isStopped && !isDancing && agent != null && agent.isOnNavMesh &&
                      agent.velocity.sqrMagnitude > 0.04f;

        currentAnimator.SetBool("Dancing", isDancing);
        currentAnimator.SetBool("Moving", moving);

        if (agent != null)
        {
            float speedFactor = Mathf.Clamp(agent.velocity.magnitude / 3.2f, 0.55f, 1.6f);
            currentAnimator.SetFloat("RunSpeed", speedFactor);
        }
    }

    void Wander()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f;
        if (Time.time < nextWanderTime && !arrived) return;

        Vector3 candidate = transform.position + Random.insideUnitSphere * wanderRadius;
        candidate.y = transform.position.y;

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }

        nextWanderTime = Time.time + Random.Range(2.5f, 5f);
    }

    // O inimigo so "percebe" o jogador se estiver na tela, dentro do alcance e sem
    // obstaculos entre os dois (e respeitando a neblina da cena).
    bool PlayerCanSeeMe()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return false;

        Vector3 camPos = playerCamera.transform.position;
        Vector3 target = transform.position + Vector3.up * 0.8f;
        Vector3 delta = target - camPos;
        float distance = delta.magnitude;

        float maxRange = sightRange;
        if (RenderSettings.fog)
            maxRange = Mathf.Min(maxRange, RenderSettings.fogEndDistance);

        if (distance > maxRange || distance < 0.01f) return false;

        Vector3 viewport = playerCamera.WorldToViewportPoint(target);
        if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f ||
            viewport.y < 0f || viewport.y > 1f)
            return false;

        int count = Physics.RaycastNonAlloc(camPos, delta / distance, lineOfSightHits,
            distance, ~0, QueryTriggerInteraction.Ignore);

        float closest = float.MaxValue;
        Transform closestTransform = null;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = lineOfSightHits[i];

            // Ignora os colliders do proprio jogador (a camera fica dentro dele).
            if (playerTransform != null && hit.transform.IsChildOf(playerTransform))
                continue;

            if (hit.distance < closest)
            {
                closest = hit.distance;
                closestTransform = hit.transform;
            }
        }

        return closestTransform != null &&
               (closestTransform == transform || closestTransform.IsChildOf(transform));
    }

    void UpdateFootstepSound()
    {
        if (enemyFootsteps == null) return;

        bool moving = !isStopped && agent != null && agent.isOnNavMesh &&
                      agent.velocity.sqrMagnitude > footstepMoveThreshold * footstepMoveThreshold;

        if (moving)
        {
            if (!enemyFootsteps.isPlaying) enemyFootsteps.Play();
        }
        else if (enemyFootsteps.isPlaying)
        {
            enemyFootsteps.Stop();
        }
    }

    public void StopChasing()
    {
        isStopped = true;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
        if (enemyFootsteps != null)
        {
            enemyFootsteps.Stop();
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
