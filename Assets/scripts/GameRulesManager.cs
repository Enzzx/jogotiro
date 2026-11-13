using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // Necessario para controlar os textos TextMeshPro

public class GameRulesManager : MonoBehaviour
{
    public static GameRulesManager Instance;

    [Header("Configuracao da Vitoria (Tregua)")]
    public float minSurvivalTimeForEnding = 30f;
    public GameObject gunObject;
    public GameObject endingPanel;

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
        if (endingPanel != null)
            endingPanel.SetActive(false);

        UpdateKillsUI();
    }

    void Update()
    {
        if (gameEnded)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartGame();
            }
            return;
        }

        survivalTime += Time.deltaTime;
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

        if (killsCount > 0)
        {
            RestartGame();
        }
        else if (survivalTime >= minSurvivalTimeForEnding)
        {
            TriggerTrueEnding();
        }
        else
        {
            RestartGame();
        }
    }

    void TriggerTrueEnding()
    {
        gameEnded = true;

        if (gunObject != null) gunObject.SetActive(false);
        if (crosshairObject != null) crosshairObject.SetActive(false);
        if (killsText != null) killsText.gameObject.SetActive(false);
        if (ammoText != null) ammoText.gameObject.SetActive(false);
        if (endingPanel != null) endingPanel.SetActive(true);

        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI enemy in enemies)
        {
            enemy.StopChasing();
        }

        HordeSpawner spawner = GetComponent<HordeSpawner>();
        if (spawner != null)
            spawner.StopAllCoroutines();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}