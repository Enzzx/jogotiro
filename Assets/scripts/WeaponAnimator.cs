using UnityEngine;

// Toca as animacoes de tiro e recarga da arma do jogador.
public class WeaponAnimator : MonoBehaviour
{
    [Tooltip("Animator do modelo da arma (FPS_Anims).")]
    public Animator animator;

    [Tooltip("Nome do Trigger de tiro no Animator Controller.")]
    public string shootTrigger = "Shoot";

    [Tooltip("Nome do Trigger de recarga no Animator Controller.")]
    public string reloadTrigger = "Reload";

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    public void PlayShoot()
    {
        if (animator == null) return;
        animator.SetTrigger(shootTrigger);
    }

    public void PlayReload()
    {
        if (animator == null) return;
        animator.SetTrigger(reloadTrigger);
    }
}
