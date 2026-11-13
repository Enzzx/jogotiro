using UnityEngine;

public class SyncSkyboxRotation : MonoBehaviour
{
    public Transform mainCameraTransform; // Arraste a Main Camera aqui

    void LateUpdate()
    {
        if (mainCameraTransform != null)
        {
            // Copia apenas a rotacao da camera principal, mantendo a posicao fixa
            transform.rotation = mainCameraTransform.rotation;
        }
    }
}