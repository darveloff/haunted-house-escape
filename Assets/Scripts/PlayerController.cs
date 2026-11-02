using UnityEngine;
using UnityEngine.InputSystem;

// Camera-relative WASD movement with sprint + stamina.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 4.2f;
    public float sprintSpeed = 7f;
    public float turnSpeed = 720f;
    public float gravity = -20f;

    public float noiseRadius = 7f; // typical ghost hearing range, shown by the sprint ring

    public float maxStamina = 100f;
    public float staminaDrain = 25f;
    public float staminaRegen = 22f;

    public Animator animator;
    public Transform cameraTransform;

    CharacterController controller;
    float verticalVelocity;
    float stamina;
    float stepDistance;
    bool exhausted;
    bool dead;

    public float Stamina01 => stamina / maxStamina;
    public bool IsSprinting { get; private set; }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stamina = maxStamina;
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        if (dead) return;
        var gmState = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameManager.State.Playing;
        if (gmState == GameManager.State.Title || gmState == GameManager.State.Paused) return;
        bool gameOver = gmState == GameManager.State.Over;
        var k = Keyboard.current;

        Vector2 input = gameOver ? Vector2.zero : ReadMove();
        bool sprintHeld = !gameOver && ((k != null && k.leftShiftKey.isPressed) || VirtualInput.Sprint);

        Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
        forward.y = 0f; right.y = 0f;
        Vector3 move = Vector3.ClampMagnitude(forward.normalized * input.y + right.normalized * input.x, 1f);
        bool moving = move.sqrMagnitude > 0.01f;

        IsSprinting = sprintHeld && moving && !exhausted;
        stamina += (IsSprinting ? -staminaDrain : staminaRegen) * Time.deltaTime;
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
        if (stamina <= 0f) exhausted = true;
        else if (exhausted && stamina > maxStamina * 0.3f) exhausted = false;

        if (moving)
        {
            var targetRot = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        float speed = IsSprinting ? sprintSpeed : walkSpeed;
        controller.Move((move * speed + Vector3.up * verticalVelocity) * Time.deltaTime);

        if (moving)
        {
            stepDistance += move.magnitude * speed * Time.deltaTime;
            if (stepDistance > (IsSprinting ? 1.9f : 1.4f))
            {
                stepDistance = 0f;
                AudioManager.Instance?.Footstep(IsSprinting);
                // Sprinting is loud: show roughly how far ghosts can hear it.
                if (IsSprinting) WorldFX.Ring(transform.position + Vector3.up * 0.08f, new Color(0.6f, 0.85f, 1f, 0.55f), 1f, noiseRadius * 2f, 0.7f);
            }
        }

        if (animator != null)
            animator.SetFloat("Speed", move.magnitude * (IsSprinting ? 2f : 1f), 0.1f, Time.deltaTime);
    }

    public void Die()
    {
        if (dead) return;
        dead = true;
        if (animator != null) animator.SetTrigger("Die");
    }

    static Vector2 ReadMove()
    {
        Vector2 v = Vector2.zero;
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
        }
        if (Gamepad.current != null) v += Gamepad.current.leftStick.ReadValue();
        v += VirtualInput.Move;
        return Vector2.ClampMagnitude(v, 1f);
    }
}
