using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimentacao")]
    public float walkSpeed = 5.0f;
    public float gravity = -9.81f;

    [Header("Sensibilidade do Mouse")]
    public float mouseSensitivity = 200.0f;

    public AudioSource footstepAudioSource;

    private CharacterController controller;
    private Transform cameraTransform;
    private float verticalRotation = 0.0f;
    private float finalSpeed;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cameraTransform = GetComponentInChildren<Camera>().transform;

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


        if (footstepAudioSource != null)
        {
            bool isMoving = (Mathf.Abs(moveX) > 0.1f || Mathf.Abs(moveZ) > 0.1f) && controller.isGrounded;
            if (isMoving && !footstepAudioSource.isPlaying)
                footstepAudioSource.Play();
            else if (!isMoving && footstepAudioSource.isPlaying)
                footstepAudioSource.Stop();
        }

        if (Input.GetKey(KeyCode.LeftShift))
        {
            finalSpeed = walkSpeed * 1.5f;
        } else
        {
            finalSpeed = walkSpeed;
        }
        // Checa se está encostando no chão
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Movimento relativo à direção que o personagem está olhando
        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        controller.Move(move * finalSpeed * Time.deltaTime);

        // Aplicação de gravidade simples
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}