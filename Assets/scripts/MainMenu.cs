using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Menu inicial do jogo.
// A tela e a propria visao do personagem, mas com a camera mais baixa (sentado
// no chao) e a arma escondida. A fogueira fica na frente, com o botao Jogar a
// esquerda e o botao Creditos a direita.
// Ao clicar em Jogar, a camera e a arma sobem e o controle do player e liberado.
public class MainMenu : MonoBehaviour
{
    // Usado por outros sistemas (IA, regras, ciclo dia/noite) para nao comecar
    // a partida enquanto o jogador ainda esta no menu.
    public static bool IsOpen { get; private set; }

    [Header("Painel do Menu")]
    public GameObject menuRoot;
    public CanvasGroup menuGroup;

    [Header("Creditos")]
    public GameObject creditsRoot;
    public CanvasGroup creditsGroup;
    public RectTransform creditsViewport;
    public RectTransform creditsContent;
    public TMP_Text creditsLabel;
    public AudioSource creditsMusic;
    [Tooltip("Texto dos créditos. Pode editar à vontade neste campo.")]
    [TextArea(10, 40)]
    public string creditsText =
        "JOGOTIRO\n\n" +
        "Uma noite na floresta.\n\n\n" +
        "--- EQUIPE ---\n\n" +
        "Direção e Programação\n" +
        "Seu Nome\n\n" +
        "Arte e Modelagem\n" +
        "Seu Nome\n\n" +
        "Trilha Sonora\n" +
        "Adicione um AudioClip no AudioSource\n" +
        "do objeto MenuManager\n\n\n" +
        "--- AGRADECIMENTOS ---\n\n" +
        "A todos que sobreviveram\n" +
        "até o amanhecer.\n\n\n\n" +
        "Obrigado por jogar.";
    [Tooltip("Rolagem automática dos créditos, em pixels por segundo. 0 = só manual (roda do mouse ou setas).")]
    public float creditsScrollSpeed = 30f;
    [Tooltip("Ao chegar no fim, o texto volta para baixo e recomeça.")]
    public bool creditsLoop = true;
    public float creditsFadeDuration = 0.4f;

    [Header("Camera Sentada")]
    public Transform cameraTransform;
    public Transform gunTransform;
    [Tooltip("Quanto a camera desce (em metros) para parecer que o personagem esta sentado.")]
    public float seatedCameraDrop = 0.65f;
    [Tooltip("Quanto a arma desce (em metros) no menu, para ficar fora da tela.")]
    public float seatedGunDrop = 0.6f;
    [Tooltip("Inclinacao da camera no menu (graus, olhando para baixo).")]
    public float seatedPitch = 14f;
    [Tooltip("Tempo que a camera e a arma levam para subir ao clicar em Jogar.")]
    public float raiseDuration = 1.2f;

    [Header("Gameplay")]
    public PlayerController playerController;
    public PlayerShoot playerShoot;
    public PlayerAnimation playerAnimation;
    public HordeSpawner hordeSpawner;

    private Vector3 standingCameraPosition;
    private Vector3 standingGunPosition;
    private readonly Quaternion standingCameraRotation = Quaternion.identity;
    private bool starting;
    private bool creditsOpen;
    private float creditsOffset;
    private Coroutine creditsFade;

    void Awake()
    {
        IsOpen = true;

        if (cameraTransform == null)
        {
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            if (controller != null)
            {
                Camera camera = controller.GetComponentInChildren<Camera>();
                if (camera != null) cameraTransform = camera.transform;
            }
        }

        if (cameraTransform != null)
            standingCameraPosition = cameraTransform.localPosition;
        if (gunTransform != null)
            standingGunPosition = gunTransform.localPosition;

        // Bloqueia o gameplay antes dos Start() dos scripts rodarem.
        // (Um componente desabilitado antes do primeiro Start so roda o Start
        //  quando for habilitado de novo, ao clicar em Jogar.)
        if (playerController != null) playerController.enabled = false;
        if (playerShoot != null) playerShoot.enabled = false;
        if (playerAnimation != null) playerAnimation.enabled = false;
        if (hordeSpawner != null) hordeSpawner.enabled = false;

        if (creditsRoot != null) creditsRoot.SetActive(false);
    }

    void Start()
    {
        EnterMenuPose();
        SetGameplayHud(false);
        ShowCursor(true);

        if (creditsLabel != null) creditsLabel.text = creditsText;

        if (menuRoot != null) menuRoot.SetActive(true);
        if (menuGroup != null)
        {
            menuGroup.alpha = 1f;
            menuGroup.interactable = true;
            menuGroup.blocksRaycasts = true;
        }

        creditsOpen = false;
        starting = false;
    }

    void Update()
    {
        if (!creditsOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseCredits();
            return;
        }

        if (creditsContent == null || creditsViewport == null) return;

        float viewportHeight = creditsViewport.rect.height;
        float contentHeight = creditsContent.rect.height;

        // Rolagem automatica.
        if (creditsScrollSpeed != 0f)
            creditsOffset += creditsScrollSpeed * Time.unscaledDeltaTime;

        // Rolagem manual: roda do mouse e setas.
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f)
            creditsOffset += wheel * 90f;
        if (Input.GetKey(KeyCode.DownArrow))
            creditsOffset += 600f * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.UpArrow))
            creditsOffset -= 600f * Time.unscaledDeltaTime;

        if (creditsOffset > contentHeight)
        {
            if (creditsLoop)
                creditsOffset = -viewportHeight;
            else
                creditsOffset = contentHeight;
        }
        if (creditsOffset < -viewportHeight)
            creditsOffset = -viewportHeight;

        ApplyCreditsPosition();
    }

    // ---- Menu -> Jogo ----

    public void Play()
    {
        if (starting || !IsOpen) return;
        starting = true;
        StartCoroutine(RaiseRoutine());
    }

    IEnumerator RaiseRoutine()
    {
        // Some com os botoes antes de levantar a camera.
        if (menuGroup != null)
        {
            menuGroup.interactable = false;
            menuGroup.blocksRaycasts = false;

            float fadeTime = 0.3f;
            float fade = 0f;
            while (fade < fadeTime)
            {
                fade += Time.unscaledDeltaTime;
                menuGroup.alpha = 1f - Mathf.Clamp01(fade / fadeTime);
                yield return null;
            }
            menuGroup.alpha = 0f;
        }

        if (menuRoot != null) menuRoot.SetActive(false);

        Vector3 cameraFrom = cameraTransform.localPosition;
        Vector3 gunFrom = gunTransform.localPosition;
        Quaternion rotationFrom = cameraTransform.localRotation;

        float elapsed = 0f;
        while (elapsed < raiseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / raiseDuration));

            cameraTransform.localPosition = Vector3.LerpUnclamped(cameraFrom, standingCameraPosition, t);
            cameraTransform.localRotation = Quaternion.SlerpUnclamped(rotationFrom, standingCameraRotation, t);
            gunTransform.localPosition = Vector3.LerpUnclamped(gunFrom, standingGunPosition, t);
            yield return null;
        }

        cameraTransform.localPosition = standingCameraPosition;
        cameraTransform.localRotation = standingCameraRotation;
        gunTransform.localPosition = standingGunPosition;

        // Agora sim o jogador controla as acoes.
        IsOpen = false;
        starting = false;

        if (playerController != null) playerController.enabled = true;
        if (playerShoot != null) playerShoot.enabled = true;
        if (playerAnimation != null) playerAnimation.enabled = true;
        // Habilitar o spawner faz o Start() dele rodar e as hordas comecarem.
        if (hordeSpawner != null) hordeSpawner.enabled = true;

        SetGameplayHud(true);
        ShowCursor(false);
    }

    // ---- Creditos ----

    public void OpenCredits()
    {
        if (creditsOpen || starting) return;
        creditsOpen = true;

        if (menuRoot != null) menuRoot.SetActive(false);
        if (creditsRoot != null) creditsRoot.SetActive(true);

        if (creditsLabel != null) creditsLabel.text = creditsText;
        ResetCreditsScroll();

        if (creditsGroup != null)
        {
            creditsGroup.alpha = 0f;
            if (creditsFade != null) StopCoroutine(creditsFade);
            creditsFade = StartCoroutine(FadeCredits(1f));
        }

        // A trilha so toca nos creditos (e para quando volta ao menu).
        if (creditsMusic != null && creditsMusic.clip != null)
        {
            creditsMusic.loop = true;
            creditsMusic.Play();
        }

        ShowCursor(true);
    }

    public void CloseCredits()
    {
        if (!creditsOpen) return;
        creditsOpen = false;

        if (creditsFade != null)
        {
            StopCoroutine(creditsFade);
            creditsFade = null;
        }

        if (creditsMusic != null) creditsMusic.Stop();
        if (creditsRoot != null) creditsRoot.SetActive(false);

        if (menuRoot != null) menuRoot.SetActive(true);
        if (menuGroup != null)
        {
            menuGroup.alpha = 1f;
            menuGroup.interactable = true;
            menuGroup.blocksRaycasts = true;
        }

        ShowCursor(true);
    }

    IEnumerator FadeCredits(float target)
    {
        float from = creditsGroup.alpha;
        float elapsed = 0f;
        while (elapsed < creditsFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            creditsGroup.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / creditsFadeDuration));
            yield return null;
        }
        creditsGroup.alpha = target;
        creditsFade = null;
    }

    // Ajusta o texto para a largura da tela e comeca com ele abaixo da area
    // visivel: assim os creditos sobem pela tela como no cinema.
    void ResetCreditsScroll()
    {
        if (creditsContent == null || creditsViewport == null) return;

        Canvas.ForceUpdateCanvases();

        float viewportHeight = creditsViewport.rect.height;
        float textHeight = creditsLabel != null ? creditsLabel.preferredHeight + 40f : 0f;

        creditsContent.sizeDelta = new Vector2(0f, textHeight);
        creditsOffset = -viewportHeight;
        ApplyCreditsPosition();
    }

    void ApplyCreditsPosition()
    {
        if (creditsContent == null) return;
        creditsContent.anchoredPosition = new Vector2(0f, creditsOffset);
    }

    // ---- Auxiliares ----

    void EnterMenuPose()
    {
        if (cameraTransform != null)
        {
            cameraTransform.localPosition = standingCameraPosition + Vector3.down * seatedCameraDrop;
            cameraTransform.localRotation = Quaternion.Euler(seatedPitch, 0f, 0f);
        }

        if (gunTransform != null)
            gunTransform.localPosition = standingGunPosition + Vector3.down * seatedGunDrop;
    }

    void SetGameplayHud(bool visible)
    {
        if (GameRulesManager.Instance != null)
            GameRulesManager.Instance.SetHudVisible(visible);
    }

    void ShowCursor(bool visible)
    {
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = visible;
    }
}
