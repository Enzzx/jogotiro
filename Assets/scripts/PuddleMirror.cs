using UnityEngine;
using UnityEngine.Rendering.Universal;

// Poca que funciona como um espelho no chao, compativel com URP.
// - Reflexo do mundo: camera espelhada + RenderTexture.
// - Reflexo do proprio player: uma copia espelhada do personagem (abaixo do chao)
//   e renderizada com a matriz da camera principal. Funciona como o reflexo que
//   aparece quando o player esta em cima / bem perto da poca.
//
// Tudo o que e estatico vem dos objetos, nao do script:
// - A arma (e as maos) ficam na layer "Weapon" (8), que esta fora de reflectionLayers.
// - A copia do player fica na layer "PlayerMirror" (7), que a camera principal ignora.
// - As layers e as mascaras das cameras ficam no TagManager e na propria cena.
[RequireComponent(typeof(Renderer))]
public class PuddleMirror : MonoBehaviour
{
    [Header("Reflexo do Mundo")]
    [Tooltip("Camadas que aparecem no reflexo. Por padrao tudo, menos Water, UI, PlayerMirror e Weapon.")]
    public LayerMask reflectionLayers = ~((1 << 4) | (1 << 5) | (1 << 7) | (1 << 8));
    [Tooltip("Altura maxima da textura de reflexo. Menor = mais barato.")]
    public int textureHeight = 432;
    [Tooltip("Distancia maxima da camera para o reflexo continuar sendo atualizado.")]
    public float maxRenderDistance = 30f;
    [Tooltip("Atualiza o reflexo a cada N frames (usado apenas no modo dinamico).")]
    [Range(1, 6)] public int updateInterval = 2;
    [Tooltip("Nao renderiza o reflexo quando a poca esta fora da tela.")]
    public bool cullOffscreen = true;

    [Header("Reflexo Estatico")]
    [Tooltip("Renderiza a paisagem uma vez e reutiliza a textura. O reflexo do player continua dinamico.")]
    public bool staticReflection = true;
    [Tooltip("Recaptura quando a camera se afastar desta distancia (metros) do ponto capturado. 0 = nunca.")]
    public float refreshDistance = 6f;
    [Tooltip("Recaptura quando a camera girar mais que estes graus. 0 = nunca.")]
    public float refreshAngle = 20f;
    [Tooltip("Recaptura a cada N segundos, para acompanhar mudancas de luz/objetos moveis. 0 = nunca.")]
    public float refreshInterval = 8f;
    [Tooltip("Espera antes da primeira captura, para a cena terminar de carregar.")]
    public float captureDelay = 0.2f;

    [Header("Reflexo do Player")]
    [Tooltip("Mostra o proprio personagem refletido na poca.")]
    public bool selfReflection = true;
    [Tooltip("Distancia do player ate a poca para o reflexo do personagem aparecer.")]
    public float selfReflectionDistance = 4.5f;
    [Range(0f, 1f)] public float selfReflectionStrength = 0.95f;

    [Header("Posicionamento")]
    [Tooltip("Cola a poca na altura do terreno ao iniciar.")]
    public bool snapToTerrain = true;
    public float groundOffset = 0.06f;
    [Tooltip("Adapta a malha da poca ao relevo do terreno (evita cortes retos em encostas).")]
    public bool conformToTerrain = true;
    [Range(2, 64)] public int conformResolution = 16;

    private const int MirrorLayer = 7;

    private Camera mainCamera;
    private Camera reflectionCamera;
    private RenderTexture reflectionTexture;
    private Renderer puddleRenderer;
    private MaterialPropertyBlock propertyBlock;

    // Reflexo do player
    private Camera selfCamera;
    private RenderTexture selfTexture;
    private Transform playerTransform;
    private Transform playerModel;
    private Transform selfCopy;
    private Transform[] sourceBones;
    private Transform[] copyBones;
    private bool selfReady;
    private Mesh generatedMesh;

    // Controle de atualizacao do reflexo.
    private static int nextFrameOffset;
    private int frameOffset;
    private bool wasVisible;
    private readonly Plane[] frustumPlanes = new Plane[6];

    // Reflexo estatico: guarda quando/quem gerou a textura atual.
    private bool hasStaticCapture;
    private float visibleSince;
    private float lastCaptureTime;
    private Vector3 lastCapturePosition;
    private Quaternion lastCaptureRotation;

    private static readonly int ReflectionTexId = Shader.PropertyToID("_ReflectionTex");
    private static readonly int SelfTexId = Shader.PropertyToID("_SelfTex");

    void Awake()
    {
        puddleRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        if (mainCamera == null) mainCamera = Camera.main;

        if (snapToTerrain) SnapToGround();
        ConformMeshToTerrain();

        // Espalha as atualizacoes entre as pocas: cada uma cai em um frame diferente.
        frameOffset = nextFrameOffset++ % Mathf.Max(1, updateInterval);

        // A copia do player fica na layer PlayerMirror, que a camera principal ignora.
        if (mainCamera != null)
            mainCamera.cullingMask &= ~(1 << MirrorLayer);

        CreateSelfReflection();
        CreateReflectionCamera();
        ApplyTextures();
    }

    void LateUpdate()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null || reflectionCamera == null || reflectionTexture == null) return;

        // Nao gasta renderizacao quando o jogador nem esta perto.
        if (Vector3.Distance(mainCamera.transform.position, transform.position) > maxRenderDistance)
        {
            wasVisible = false;
            HideSelfCopy();
            return;
        }

        // Poca fora da tela nao precisa de reflexo neste frame.
        bool visible = !cullOffscreen || IsPuddleVisible();
        if (!visible)
        {
            wasVisible = false;
            HideSelfCopy();
            return;
        }

        if (!wasVisible) visibleSince = Time.time;
        wasVisible = true;

        if (staticReflection)
        {
            // A paisagem e capturada uma vez e reaproveitada. So recaptura quando
            // a camera anda/gira o bastante ou quando o tempo passa.
            if (ShouldCaptureStatic())
            {
                CaptureReflection();
                hasStaticCapture = true;
                lastCaptureTime = Time.time;
                lastCapturePosition = mainCamera.transform.position;
                lastCaptureRotation = mainCamera.transform.rotation;
            }
        }
        else
        {
            // Modo dinamico: atualiza em intervalos (alternados entre as pocas)
            // para nao renderizar a cena inteira varias vezes no mesmo frame.
            int interval = Mathf.Max(1, updateInterval);
            if ((Time.frameCount + frameOffset) % interval != 0)
            {
                RenderSelfReflection();
                return;
            }

            CaptureReflection();
        }

        RenderSelfReflection();
    }

    bool ShouldCaptureStatic()
    {
        if (!hasStaticCapture) return Time.time - visibleSince >= captureDelay;

        if (refreshDistance > 0f
            && Vector3.Distance(mainCamera.transform.position, lastCapturePosition) >= refreshDistance)
            return true;

        if (refreshAngle > 0f
            && Quaternion.Angle(mainCamera.transform.rotation, lastCaptureRotation) >= refreshAngle)
            return true;

        if (refreshInterval > 0f && Time.time - lastCaptureTime >= refreshInterval)
            return true;

        return false;
    }

    // Renderiza a paisagem espelhada dentro da RenderTexture da poca.
    void CaptureReflection()
    {
        Vector3 normal = PlaneNormal;
        Vector4 plane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, transform.position));
        Matrix4x4 reflectionMatrix = ReflectionMatrix(plane);

        // Pose espelhada (usada pelo culling e por qualquer sistema que leia o transform).
        Vector3 reflectedPosition = reflectionMatrix.MultiplyPoint3x4(mainCamera.transform.position);
        Vector3 reflectedForward = Vector3.Reflect(mainCamera.transform.forward, normal);
        Vector3 reflectedUp = Vector3.Reflect(mainCamera.transform.up, normal);
        reflectionCamera.transform.SetPositionAndRotation(
            reflectedPosition,
            Quaternion.LookRotation(reflectedForward, reflectedUp));

        reflectionCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMatrix;
        reflectionCamera.projectionMatrix = mainCamera.projectionMatrix;
        reflectionCamera.cullingMask = reflectionLayers;

        bool invertCulling = GL.invertCulling;
        GL.invertCulling = true;
        reflectionCamera.Render();
        GL.invertCulling = invertCulling;
    }

    // Testa se a poca esta dentro do frustum da camera principal.
    bool IsPuddleVisible()
    {
        if (mainCamera == null || puddleRenderer == null) return true;
        GeometryUtility.CalculateFrustumPlanes(mainCamera, frustumPlanes);
        return GeometryUtility.TestPlanesAABB(frustumPlanes, puddleRenderer.bounds);
    }

    void HideSelfCopy()
    {
        if (selfCopy != null && selfCopy.gameObject.activeSelf)
            selfCopy.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (reflectionCamera != null)
            Destroy(reflectionCamera.gameObject);

        if (selfCamera != null)
            Destroy(selfCamera.gameObject);

        if (selfCopy != null)
            Destroy(selfCopy.gameObject);

        if (generatedMesh != null)
            Destroy(generatedMesh);

        if (reflectionTexture != null)
        {
            reflectionTexture.Release();
            Destroy(reflectionTexture);
        }

        if (selfTexture != null)
        {
            selfTexture.Release();
            Destroy(selfTexture);
        }
    }

    void CreateReflectionCamera()
    {
        GameObject cameraObject = new GameObject(name + "_ReflectionCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.transform.SetParent(null);

        reflectionCamera = cameraObject.AddComponent<Camera>();
        reflectionCamera.enabled = false;
        reflectionCamera.clearFlags = CameraClearFlags.Skybox;
        reflectionCamera.backgroundColor = Color.black;
        reflectionCamera.allowHDR = false;
        reflectionCamera.allowMSAA = false;
        reflectionCamera.useOcclusionCulling = false;
        reflectionCamera.nearClipPlane = 0.03f;
        reflectionCamera.farClipPlane = 200f;
        reflectionCamera.depth = -50f;
        reflectionCamera.cullingMask = reflectionLayers;

        // O reflexo nao precisa de sombras nem de pos-processamento: o shader
        // ja escurece/ondula a imagem. Desligar isso evita refazer o shadow map
        // e o SSAO/bloom para a camera de reflexo.
        UniversalAdditionalCameraData reflectionData = reflectionCamera.GetUniversalAdditionalCameraData();
        reflectionData.renderShadows = false;
        reflectionData.renderPostProcessing = false;
        reflectionData.requiresDepthOption = CameraOverrideOption.Off;
        reflectionData.requiresColorOption = CameraOverrideOption.Off;
        reflectionData.volumeLayerMask = 0;

        reflectionTexture = CreateRenderTexture(name + "_ReflectionTex", RenderTextureFormat.Default);
        reflectionCamera.targetTexture = reflectionTexture;
    }

    // Cria a RenderTexture uma unica vez, ja no tamanho final (nao redimensiona
    // a cada frame; o formato da tela e fixo durante a partida).
    RenderTexture CreateRenderTexture(string textureName, RenderTextureFormat format)
    {
        int height = Mathf.Clamp(textureHeight, 128, 1080);
        float aspect = (mainCamera != null && mainCamera.aspect > 0.01f) ? mainCamera.aspect : 16f / 9f;
        int width = Mathf.RoundToInt(height * aspect);

        RenderTexture texture = new RenderTexture(width, height, 16, format);
        texture.name = textureName;
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.Create();
        return texture;
    }

    // ---- Reflexo do player ----

    void CreateSelfReflection()
    {
        if (!selfReflection) return;

        GameObject playerObject = GameObject.Find("Player");
        if (playerObject == null) return;

        playerTransform = playerObject.transform;

        // O modelo visual do player e o filho com o personagem (animator/skinned mesh).
        Transform candidate = playerTransform.Find("CharacterModel");
        if (candidate == null)
        {
            SkinnedMeshRenderer[] skinned = playerTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer renderer in skinned)
            {
                if (renderer.GetComponentInParent<Camera>() != null) continue;
                candidate = renderer.rootBone != null ? renderer.rootBone : renderer.transform;
                while (candidate.parent != null && candidate.parent != playerTransform)
                    candidate = candidate.parent;
                break;
            }
        }

        if (candidate == null) return;
        playerModel = candidate;

        selfCopy = Instantiate(playerModel, null);
        selfCopy.name = name + "_PlayerReflection";

        Animator copyAnimator = selfCopy.GetComponent<Animator>();
        if (copyAnimator != null) copyAnimator.enabled = false;

        foreach (MonoBehaviour behaviour in selfCopy.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;

        foreach (Collider collider in selfCopy.GetComponentsInChildren<Collider>(true))
            Destroy(collider);

        foreach (Renderer renderer in selfCopy.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.gameObject.layer = MirrorLayer;
        }

        sourceBones = playerModel.GetComponentsInChildren<Transform>(true);
        copyBones = selfCopy.GetComponentsInChildren<Transform>(true);

        GameObject cameraObject = new GameObject(name + "_SelfCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.transform.SetParent(null);

        selfCamera = cameraObject.AddComponent<Camera>();
        selfCamera.enabled = false;
        selfCamera.clearFlags = CameraClearFlags.SolidColor;
        selfCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        selfCamera.allowHDR = false;
        selfCamera.allowMSAA = false;
        selfCamera.useOcclusionCulling = false;
        selfCamera.depth = -50f;
        selfCamera.cullingMask = 1 << MirrorLayer;

        UniversalAdditionalCameraData selfData = selfCamera.GetUniversalAdditionalCameraData();
        selfData.renderShadows = false;
        selfData.renderPostProcessing = false;
        selfData.requiresDepthOption = CameraOverrideOption.Off;
        selfData.requiresColorOption = CameraOverrideOption.Off;
        selfData.volumeLayerMask = 0;

        selfTexture = CreateRenderTexture(name + "_SelfTex", RenderTextureFormat.ARGB32);
        selfCamera.targetTexture = selfTexture;

        selfReady = true;
    }

    void RenderSelfReflection()
    {
        if (!selfReady || playerTransform == null || playerModel == null) return;

        bool playerNear = Vector3.Distance(playerTransform.position, transform.position) <= selfReflectionDistance;

        if (!playerNear || selfCopy == null || selfCamera == null || selfTexture == null)
        {
            if (selfCopy != null && selfCopy.gameObject.activeSelf) selfCopy.gameObject.SetActive(false);
            return;
        }

        if (!selfCopy.gameObject.activeSelf) selfCopy.gameObject.SetActive(true);

        // Copia a pose atual do personagem (as ossadas locais).
        if (sourceBones != null && copyBones != null)
        {
            int count = Mathf.Min(sourceBones.Length, copyBones.Length);
            for (int i = 1; i < count; i++)
            {
                if (sourceBones[i] == null || copyBones[i] == null) continue;
                copyBones[i].localPosition = sourceBones[i].localPosition;
                copyBones[i].localRotation = sourceBones[i].localRotation;
                copyBones[i].localScale = sourceBones[i].localScale;
            }
        }

        // Espelha a posicao do personagem em relacao ao plano da poca.
        Vector3 normal = PlaneNormal;
        float distance = Vector3.Dot(normal, transform.position);
        Vector3 mirroredPosition = playerModel.position - 2f * (Vector3.Dot(normal, playerModel.position) - distance) * normal;

        // Um espelho no chao nao mostra o proprio player na pratica (ele fica
        // atras da camera espelhada). Para o efeito pedido, o reflexo e montado
        // no centro da poca: a copia espelhada fica logo abaixo da superficie,
        // no raio que sai da camera e passa pelo centro da poca.
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(transform.position);
        Ray ray = mainCamera.ScreenPointToRay(screenPoint);
        float planeHeight = Vector3.Dot(normal, transform.position);
        float targetHeight = planeHeight - 1.05f;
        float raydir = ray.direction.y;

        if (Mathf.Abs(raydir) > 0.0001f)
        {
            float t = (targetHeight - ray.origin.y) / raydir;
            mirroredPosition = ray.origin + ray.direction * t;
        }

        selfCopy.position = mirroredPosition;
        selfCopy.rotation = playerModel.rotation * Quaternion.Euler(0f, 0f, 180f);
        selfCopy.localScale = playerModel.lossyScale;

        // Renderiza somente a copia do player, com a mesma camera/projecao da tela.
        selfCamera.transform.SetPositionAndRotation(
            mainCamera.transform.position,
            mainCamera.transform.rotation);
        selfCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix;
        selfCamera.projectionMatrix = mainCamera.projectionMatrix;
        selfCamera.cullingMask = 1 << MirrorLayer;

        selfCamera.Render();
    }

    void ApplyTextures()
    {
        if (puddleRenderer == null) return;

        puddleRenderer.GetPropertyBlock(propertyBlock);
        if (reflectionTexture != null)
            propertyBlock.SetTexture(ReflectionTexId, reflectionTexture);
        if (selfTexture != null)
            propertyBlock.SetTexture(SelfTexId, selfTexture);
        else
            propertyBlock.SetFloat("_SelfStrength", 0f);
        if (selfTexture != null)
            propertyBlock.SetFloat("_SelfStrength", selfReflectionStrength);
        puddleRenderer.SetPropertyBlock(propertyBlock);
    }

    void SnapToGround()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        Vector3 position = transform.position;
        position.y = terrain.SampleHeight(position) + groundOffset;
        transform.position = position;
    }

    // Gera uma malha em grade que acompanha o relevo (a poca deixa de ser um
    // quadrado plano cortado pelo terreno nas encostas).
    void ConformMeshToTerrain()
    {
        if (!conformToTerrain) return;

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) return;

        int resolution = Mathf.Clamp(conformResolution, 2, 64);
        int side = resolution + 1;
        Vector3[] vertices = new Vector3[side * side];
        Vector2[] uvs = new Vector2[side * side];
        int[] triangles = new int[resolution * resolution * 6];

        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                float u = (float)x / resolution;
                float v = (float)y / resolution;

                Vector3 local = new Vector3(u - 0.5f, v - 0.5f, 0f);
                Vector3 world = transform.TransformPoint(local);
                world.y = terrain.SampleHeight(world) + groundOffset;

                vertices[y * side + x] = transform.InverseTransformPoint(world);
                uvs[y * side + x] = new Vector2(u, v);
            }
        }

        int index = 0;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = y * side + x;
                triangles[index++] = i;
                triangles[index++] = i + 1;
                triangles[index++] = i + side;
                triangles[index++] = i + 1;
                triangles[index++] = i + side + 1;
                triangles[index++] = i + side;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = name + "_ConformedMesh";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        if (generatedMesh != null) Destroy(generatedMesh);
        generatedMesh = mesh;
        meshFilter.sharedMesh = mesh;
    }

    // A malha da poca e um Quad rotacionado 90 graus em X: a normal da superficie
    // (para cima) e o -forward do transform, nao o up.
    Vector3 PlaneNormal
    {
        get { return -transform.forward; }
    }

    // Matriz que espelha o mundo em relacao ao plano (x,y,z,d): n.p + d = 0.
    static Matrix4x4 ReflectionMatrix(Vector4 plane)
    {
        Matrix4x4 matrix = Matrix4x4.zero;

        matrix.m00 = 1f - 2f * plane.x * plane.x;
        matrix.m01 = -2f * plane.x * plane.y;
        matrix.m02 = -2f * plane.x * plane.z;
        matrix.m03 = -2f * plane.w * plane.x;

        matrix.m10 = -2f * plane.y * plane.x;
        matrix.m11 = 1f - 2f * plane.y * plane.y;
        matrix.m12 = -2f * plane.y * plane.z;
        matrix.m13 = -2f * plane.w * plane.y;

        matrix.m20 = -2f * plane.z * plane.x;
        matrix.m21 = -2f * plane.z * plane.y;
        matrix.m22 = 1f - 2f * plane.z * plane.z;
        matrix.m23 = -2f * plane.w * plane.z;

        matrix.m33 = 1f;
        return matrix;
    }
}
