using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimentacao")]
    public float walkSpeed = 5.0f;
    public float gravity = -9.81f;

    [Header("Sensibilidade do Mouse")]
    public float mouseSensitivity = 200.0f;

    [Header("Audio de Passos")]
    public AudioSource footstepAudioSource;
    public AudioClip walkClip;
    public AudioClip runClip;
    [Range(0f, 1f)] public float walkVolume = 0.45f;
    [Range(0f, 1f)] public float runVolume = 0.65f;

    private CharacterController controller;
    private Transform cameraTransform;
    private float verticalRotation = 0.0f;
    private float finalSpeed;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cameraTransform = GetComponentInChildren<Camera>().transform;

        if (footstepAudioSource != null)
        {
            footstepAudioSource.loop = true;
            footstepAudioSource.playOnAwake = false;
            if (footstepAudioSource.clip == null && walkClip != null)
                footstepAudioSource.clip = walkClip;
        }

        // Esconde e trava o cursor do mouse no centro da tela
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
    }

    void HandleMouseLook()
    {
        // Pega o movimento do mouse
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Rotação vertical (olhar para cima/baixo na câmera)
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -80.0f, 80.0f); // Limita o ângulo para não girar o pescoço 360°
        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);

        // Rotação horizontal (girar o corpo do personagem para os lados)
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleMovement()
    {
        // Entradas WASD
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");


        bool isSprinting = Input.GetKey(KeyCode.LeftShift);
        finalSpeed = isSprinting ? walkSpeed * 1.5f : walkSpeed;
        // Checa se está encostando no chão
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Movimento relativo à direção que o personagem está olhando
        Vector3 move = transform.right * moveX + transform.forward * moveZ;

        // Aplicação de gravidade simples
        velocity.y += gravity * Time.deltaTime;

        // Um unico Move por frame: assim controller.velocity mantem a velocidade
        // horizontal real, que o PlayerAnimation usa para escolher Idle/Walk/Run.
        controller.Move((move * finalSpeed + velocity) * Time.deltaTime);

        bool isMoving = new Vector2(moveX, moveZ).sqrMagnitude > 0.02f && controller.isGrounded;
        UpdateFootsteps(isMoving, isSprinting);
    }

    void UpdateFootsteps(bool isMoving, bool isSprinting)
    {
        if (footstepAudioSource == null) return;

        if (!isMoving)
        {
            if (footstepAudioSource.isPlaying) footstepAudioSource.Stop();
            return;
        }

        AudioClip desiredClip = isSprinting && runClip != null ? runClip : walkClip;
        if (desiredClip != null && footstepAudioSource.clip != desiredClip)
        {
            footstepAudioSource.clip = desiredClip;
            footstepAudioSource.Play();
        }
        else if (!footstepAudioSource.isPlaying)
        {
            footstepAudioSource.Play();
        }

        footstepAudioSource.volume = isSprinting ? runVolume : walkVolume;
    }
}