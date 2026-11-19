using UnityEngine;

// A caixa de municao so cuida da coleta. O giro visual fica na animacao
// "AmmoSpin" (componente Animation no filho "Visual" do prefab).
public class AmmoPickup : MonoBehaviour
{
    public int ammoAmount = 8;

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