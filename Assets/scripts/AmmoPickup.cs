using UnityEngine;

public class AmmoPickup : MonoBehaviour
{
    public int ammoAmount = 8;
    public float rotateSpeed = 60f;

    void Update()
    {
        // Gira suavemente para chamar atencao no chao da floresta
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        // Verifica se quem encostou tem o script PlayerShoot
        PlayerShoot playerShoot = other.GetComponent<PlayerShoot>();
        if (playerShoot != null)
        {
            playerShoot.AddAmmo(ammoAmount);
            Destroy(gameObject);
        }
    }
}