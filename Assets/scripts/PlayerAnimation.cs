using UnityEngine;

// Alimenta o Animator do personagem do jogador com a velocidade real de movimento.
[RequireComponent(typeof(CharacterController))]
public class PlayerAnimation : MonoBehaviour
{
    [Header("Referencias")]
    public Animator animator;
    public CharacterController controller;

    [Header("Ajustes")]
    [Tooltip("Velocidade abaixo da qual o personagem fica parado (Idle).")]
    public float idleThreshold = 0.4f;
    [Tooltip("Suavizacao da velocidade enviada ao Animator.")]
    public float damping = 0.1f;

    private Vector3 previousPosition;

    void Awake()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        previousPosition = transform.position;
    }

    void Update()
    {
        if (animator == null) return;

        // Velocidade informada pelo CharacterController (ja considera colisoes).
        Vector3 velocity = controller != null ? controller.velocity : Vector3.zero;
        velocity.y = 0f;

        // Fallback: deslocamento real do transform no frame. Garante que a animacao
        // funcione mesmo se controller.velocity vier zerada.
        Vector3 delta = transform.position - previousPosition;
        previousPosition = transform.position;
        delta.y = 0f;
        float movedSpeed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);

        float speed = Mathf.Max(velocity.magnitude, movedSpeed);
        if (speed < idleThreshold) speed = 0f;

        animator.SetFloat("Speed", speed, damping, Time.deltaTime);
    }
}
