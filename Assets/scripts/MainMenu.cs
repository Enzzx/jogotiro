using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Menu inicial do jogo.
// A tela e a propria visao do personagem, mas com a camera mais baixa (sentado
// no chao) e a arma escondida. A fogueira fica na frente, com o botao Jogar a
// esquerda e o botao Sair a direita.
// Ao clicar em Jogar, a camera e a arma sobem e o controle do player e liberado.
public class MainMenu : MonoBehaviour
{
    // Usado por outros sistemas (IA, regras, ciclo dia/noite) para nao comecar
    // a partida enquanto o jogador ainda esta no menu.
    public static bool IsOpen { get; private set; }

    [Header("Painel do Menu")]
    public GameObject menuRoot;
    public CanvasGroup menuGroup;

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

    void Awake()
    {
        IsOpen = true;

        // Garante que o jogo volta a rodar normalmente caso venha de uma tela
        // de vitoria/derrota (onde o tempo fica pausado).
        Time.timeScale = 1f;

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
    }

    void Start()
    {
        EnterMenuPose();
        SetGameplayHud(false);
        ShowCursor(true);

        if (menuRoot != null) menuRoot.SetActive(true);
        if (menuGroup != null)
        {
            menuGroup.alpha = 1f;
            menuGroup.interactable = true;
            menuGroup.blocksRaycasts = true;
        }

        SetupQuitButton();
        starting = false;
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

    // ---- Sair ----

    // Liga o botao Sair do menu ao fechamento do jogo.
    void SetupQuitButton()
    {
        if (menuRoot == null) return;

        Transform quitTransform = menuRoot.transform.Find("QuitButton");
        if (quitTransform == null) return;

        Button quitButton = quitTransform.GetComponent<Button>();
        if (quitButton == null) return;

        quitButton.onClick.RemoveAllListeners();
        quitButton.onClick.AddListener(QuitGame);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
