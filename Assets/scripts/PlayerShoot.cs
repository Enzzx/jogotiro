using System.Collections;
using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    [Header("Configuracoes da Arma")]
    public Camera playerCamera;
    public float range = 80f;
    public float fireRate = 0.25f;

    [Header("Municao")]
    public int maxClipAmmo = 6;     // Capacidade do pente da arma
    public int currentAmmo = 6;     // Balas atualmente no pente
    public int reserveAmmo = 0;     // Balas guardadas na mochila
    public float reloadTime = 1.2f;

    [Header("Audio da Arma")]
    public AudioSource weaponAudioSource; // Arraste o 1o AudioSource do Player
    public AudioClip shootClip;           // Arraste o som de tiro
    public AudioClip reloadClip;          // Arraste o som de recarga

    private bool isReloading = false;
    private float nextTimeToFire = 0f;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        UpdateUI();
    }

    void Update()
    {
        if (GameRulesManager.Instance != null && GameRulesManager.Instance.gameEnded)
            return;

        if (isReloading) return;

        // Recarrega automaticamente se o pente zerar (e houver reserva) ou ao apertar R
        if ((currentAmmo <= 0 || (Input.GetKeyDown(KeyCode.R) && currentAmmo < maxClipAmmo)) && reserveAmmo > 0)
        {
            StartCoroutine(Reload());
            return;
        }

        if (Input.GetButtonDown("Fire1") && Time.time >= nextTimeToFire && currentAmmo > 0)
        {
            nextTimeToFire = Time.time + fireRate;
            Shoot();
        }
    }

    IEnumerator Reload()
    {
        isReloading = true;

        if (weaponAudioSource != null && reloadClip != null)
            weaponAudioSource.PlayOneShot(reloadClip);

        yield return new WaitForSeconds(reloadTime);

        int bulletsNeeded = maxClipAmmo - currentAmmo;
        int bulletsToLoad = Mathf.Min(bulletsNeeded, reserveAmmo);

        currentAmmo += bulletsToLoad;
        reserveAmmo -= bulletsToLoad;

        isReloading = false;
        UpdateUI();
    }

    void Shoot()
    {
        currentAmmo--;

        if (weaponAudioSource != null && shootClip != null)
            weaponAudioSource.PlayOneShot(shootClip);

        UpdateUI();

        if (GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.RegisterShot();
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            EnemyAI enemy = hit.transform.GetComponentInParent<EnemyAI>();
            if (enemy != null)
            {
                if (GameRulesManager.Instance != null)
                    GameRulesManager.Instance.RegisterKill();

                enemy.Die();
            }
        }
    }

    // Chamado pelos pacotes de municao ao passar por cima
    public void AddAmmo(int amount)
    {
        reserveAmmo += amount;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.UpdateAmmoUI(currentAmmo, reserveAmmo);
        }
    }
}