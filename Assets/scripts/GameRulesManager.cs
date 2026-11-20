using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro; // Necessario para controlar os textos TextMeshPro

public class GameRulesManager : MonoBehaviour
{
    public static GameRulesManager Instance;

    [Header("Configuracao da Vitoria (Tregua)")]
    public float minSurvivalTimeForEnding = 30f;
    public GameObject gunObject;

    [Header("Telas de Fim")]
    public GameObject losePanel;       // Tela preta: Voce perdeu
    public TMP_Text loseDeathsText;    // Contador de mortes mostrado na tela preta
    public GameObject winPanel;        // Tela branca: Voce ganhou
    public TMP_Text winTimeText;       // Tempo sobrevivido mostrado na tela branca

    [Header("Interface (HUD)")]
    public GameObject hudGroup;        // Opcional: pode arrastar um objeto pai com mira e textos
    public TMP_Text killsText;         // Arraste o KillsText aqui
    public TMP_Text ammoText;          // Arraste o AmmoText aqui
    public GameObject crosshairObject; // Arraste o Crosshair aqui

    [Header("Status Atual")]
    public int shotsFired = 0;
    public int killsCount = 0;
    public float survivalTime = 0f;
    public bool gameEnded = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (losePanel != null)
            losePanel.SetActive(false);
        if (winPanel != null)
            winPanel.SetActive(false);

        SetupBackButton(losePanel);
        SetupBackButton(winPanel);

        UpdateKillsUI();
    }

    void Update()
    {
        // No menu o tempo de sobrevivencia ainda nao comeca a contar.
        if (MainMenu.IsOpen) return;

        if (gameEnded) return;

        survivalTime += Time.deltaTime;
    }

    // Mostra/esconde a HUD (mira, municao e mortes) durante o menu.
    public void SetHudVisible(bool visible)
    {
        if (hudGroup != null) hudGroup.SetActive(visible);
        if (crosshairObject != null) crosshairObject.SetActive(visible);
        if (ammoText != null) ammoText.gameObject.SetActive(visible);
        if (killsText != null) killsText.gameObject.SetActive(visible);
    }

    public void RegisterShot()
    {
        shotsFired++;
    }

    public void RegisterKill()
    {
        killsCount++;
        UpdateKillsUI();
    }

    void UpdateKillsUI()
    {
        if (killsText != null)
        {
            // Duplo sentido: parece contagem de kills, mas sao suas proprias mortes
            killsText.text = "Mortes: " + killsCount;
        }
    }

    public void UpdateAmmoUI(int currentAmmo, int maxAmmo)
    {
        if (ammoText != null)
        {
            ammoText.text = currentAmmo + " / " + maxAmmo;
        }
    }

    public void OnPlayerCaught()
    {
        if (gameEnded) return;

        // Vitoria: foi pego sem matar ninguem e sobreviveu o tempo minimo.
        if (killsCount == 0 && survivalTime >= minSurvivalTimeForEnding)
            ShowWinScreen();
        else
            ShowLoseScreen();
    }

    void ShowLoseScreen()
    {
        EndGame();

        if (loseDeathsText != null)
            loseDeathsText.text = "Mortes: " + killsCount;

        if (losePanel != null)
            losePanel.SetActive(true);
    }

    void ShowWinScreen()
    {
        EndGame();

        if (winTimeText != null)
            winTimeText.text = "Tempo sobrevivido: " + FormatTime(survivalTime);

        if (winPanel != null)
            winPanel.SetActive(true);
    }

    // Congela o jogo e prepara a tela de fim (vitoria ou derrota).
    void EndGame()
    {
        gameEnded = true;

        if (gunObject != null) gunObject.SetActive(false);
        if (crosshairObject != null) crosshairObject.SetActive(false);
        if (killsText != null) killsText.gameObject.SetActive(false);
        if (ammoText != null) ammoText.gameObject.SetActive(false);
        if (hudGroup != null) hudGroup.SetActive(false);

        // Trava os controles do jogador (e os passos) na tela de fim.
        PlayerController playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null)
        {
            if (playerController.footstepAudioSource != null)
                playerController.footstepAudioSource.Stop();
            playerController.enabled = false;
        }

        PlayerShoot playerShoot = FindAnyObjectByType<PlayerShoot>();
        if (playerShoot != null) playerShoot.enabled = false;

        PlayerAnimation playerAnimation = FindAnyObjectByType<PlayerAnimation>();
        if (playerAnimation != null) playerAnimation.enabled = false;

        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude);
        foreach (EnemyAI enemy in enemies)
        {
            enemy.StopChasing();
        }

        HordeSpawner spawner = GetComponent<HordeSpawner>();
        if (spawner != null)
        {
            spawner.StopAllCoroutines();
            spawner.enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Pausa o mundo, mas deixa os botoes da interface funcionando.
        Time.timeScale = 0f;
    }

    // Liga o botao "Voltar ao Menu" da tela de fim.
    void SetupBackButton(GameObject panel)
    {
        if (panel == null) return;

        Transform backTransform = panel.transform.Find("BackButton");
        if (backTransform == null) return;

        Button backButton = backTransform.GetComponent<Button>();
        if (backButton == null) return;

        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(ReturnToMenu);
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return string.Format("{0:00}:{1:00}", totalSeconds / 60, totalSeconds % 60);
    }
}
