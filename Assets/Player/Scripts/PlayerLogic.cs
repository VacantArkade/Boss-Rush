using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public static class StaticInputManager
{
    public static PlayerInputActions input { get; private set; } = new PlayerInputActions();
}

public class PlayerLogic : MonoBehaviour
{
    // values set in inspector
    [SerializeField] float moveSpeed;
    [SerializeField] float jumpSpeed;
    [SerializeField] float gravity;
    [SerializeField] int maxEnergy;
    [SerializeField] int shootCost;

    // references set in inspector
    [SerializeField] GameObject sword;
    [SerializeField] GameObject projectile;
    [SerializeField] Animator animator;
    [SerializeField] LayerMask ground;

    [SerializeField] AudioClip jumpSound;
    [SerializeField] AudioClip swordSwingSound;
    [SerializeField] AudioClip dashSound;
    [SerializeField] AudioClip shootSound;

    // events
    public UnityEvent OnStartDash;
    public UnityEvent OnEndDash;
    public UnityEvent<int> OnInitializeEnergy;
    public UnityEvent<int, int> OnEnergyChanged; // change amount, new total amount

    // private references
    CharacterController controller;

    // movement variables
    Vector3 moveVelocity;
    bool grounded = false;
    public bool canControl = true;

    // combat variables
    bool aiming = false;
    float timeSinceLastMelee = 0;
    bool usingMouse = false;
    bool dead = false;

    int currentEnergy;

    // Start is called before the first frame update
    void Start()
    {
        controller = GetComponent<CharacterController>();
        OnInitializeEnergy?.Invoke(maxEnergy);
        ChangeEnergy(maxEnergy);
    }

    private void OnEnable()
    {
        StaticInputManager.input.Player.Jump.performed += Jump;
        StaticInputManager.input.Player.Melee.performed += Melee;
        StaticInputManager.input.Player.Dodge.performed += Dodge;
        StaticInputManager.input.Player.Aim.started += ctx => { aiming = true; usingMouse = ctx.control.parent.name == "Mouse" ? true : false; };
        StaticInputManager.input.Player.Aim.canceled += ctx => { aiming = false; usingMouse = ctx.control.parent.name == "Mouse" ? true : false; };
        StaticInputManager.input.Player.Shoot.performed += Shoot;

        StaticInputManager.input.Player.Enable();
    }

    private void OnDisable()
    {
        StaticInputManager.input.Player.Jump.performed -= Jump;
        StaticInputManager.input.Player.Melee.performed -= Melee;
        StaticInputManager.input.Player.Dodge.performed -= Dodge;
        StaticInputManager.input.Player.Shoot.performed -= Shoot;
        //StaticInputManager.input.Player.Aim.Dispose(); // ??

        StaticInputManager.input.Player.Disable();
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.timeScale == 0) return;

        if (dead)
            canControl = false;

        timeSinceLastMelee += Time.deltaTime;

        bool prevGrounded = grounded;
        grounded = controller.isGrounded;

        if(prevGrounded != grounded && grounded)
        {
            // landed
        }

        if(grounded && moveVelocity.y < 0) // stop falling when on ground
        {
            moveVelocity.y = 0;
        }

        if(canControl)
        {
            // get movement input and make it camera-relative
            Vector2 moveInput = StaticInputManager.input.Player.Move.ReadValue<Vector2>();
            Vector3 adjustedInput = GetCameraRelativeInput(moveInput);

            // face movement direction
            if (adjustedInput != Vector3.zero && !aiming)
                transform.forward = adjustedInput.normalized;

            // controls change if we're aiming
            if (aiming)
            {
                moveVelocity = new Vector3(0, moveVelocity.y, 0);

                // controls change further if we're using mouse/keyboard or controller
                if(usingMouse)
                {
                    // if using mouse, rotate to face mouse position

                    Vector2 mousePos = Mouse.current.position.value;

                    Ray mouseRay = Camera.main.ScreenPointToRay(mousePos);

                    if (Physics.Raycast(mouseRay, out RaycastHit hit, 1000, ground))
                    {
                        var aimDirection = (hit.point - transform.position).normalized;
                        aimDirection.y = 0;
                        transform.forward = aimDirection;
                    }
                }

                else
                {
                    // if using controller, face stick direction

                    if (adjustedInput != Vector3.zero)
                        transform.forward = adjustedInput.normalized;
                }

            }

            else
            {
                // if not aiming, set our target movement velocity based on input
                moveVelocity = new Vector3(adjustedInput.x * moveSpeed, moveVelocity.y, adjustedInput.z * moveSpeed);
            }
        }

        // add gravity because character controller doesn't do it automatically
        moveVelocity.y += gravity * Time.deltaTime;

        controller.Move((moveVelocity) * Time.deltaTime);

        // get ground velocity to use it in Animator
        Vector3 walkVelocity = moveVelocity;
        walkVelocity.y = 0;
        animator.SetFloat("speed", Mathf.Abs(walkVelocity.magnitude));
    }

    void Jump(InputAction.CallbackContext ctx)
    {
        if (!canControl)
            return;

        if (!grounded)
            return;

        moveVelocity.y = jumpSpeed;

        SoundEffectsManager.instance.PlayAudioClip(jumpSound, true);
    }

    void Melee(InputAction.CallbackContext ctx)
    {
        if (!canControl)
            return;

        if (!grounded)
            return;

        if (timeSinceLastMelee < 0.25f)
            return;

        if (aiming)
            return;

        StartCoroutine(HandleMelee());
    }

    IEnumerator HandleMelee()
    {
        timeSinceLastMelee = 0;
        sword.SetActive(true);
        sword.GetComponent<Animator>().SetTrigger("swing");
        SoundEffectsManager.instance.PlayAudioClip(swordSwingSound, true);
        yield return new WaitForSeconds(0.25f);
        sword.SetActive(false);
    }

    void Dodge(InputAction.CallbackContext ctx)
    {
        if (!canControl)
            return;

        StartCoroutine(HandleDash());
    }

    IEnumerator HandleDash()
    {
        OnStartDash?.Invoke();
        SoundEffectsManager.instance.PlayAudioClip(dashSound, true);
        canControl = false;
        moveVelocity = transform.forward * 20;
        yield return new WaitForSeconds(0.25f);
        moveVelocity = Vector3.zero;
        canControl = true;
        OnEndDash?.Invoke();
    }

    public void Shoot(InputAction.CallbackContext ctx)
    {
        if (!canControl)
            return;

        if (!aiming)
            return;

        if (currentEnergy < 20)
            return;

        GameObject p = Instantiate(projectile, transform.position, Quaternion.identity);
        p.transform.forward = transform.forward;

        ChangeEnergy(-1 * shootCost);

        SoundEffectsManager.instance.PlayAudioClip(shootSound, true);
    }

    public void ChangeEnergy(int amount)
    {
        currentEnergy += amount;

        if (currentEnergy > maxEnergy)
            currentEnergy = maxEnergy;

        if (currentEnergy <= 0)
            currentEnergy = 0;

        OnEnergyChanged?.Invoke(amount, currentEnergy);
    }

    Vector3 GetCameraRelativeInput(Vector2 input)
    {
        Transform cam = Camera.main.transform;

        Vector3 camRight = cam.right;
        camRight.y = 0;
        camRight.Normalize();
        Vector3 camForward = cam.forward;
        camForward.y = 0;
        camForward.Normalize();

        return input.x * camRight + input.y * camForward;
    }

    public void ApplyKnockback(Damage damage)
    {
        //Debug.Log("apply knockback");
        StartCoroutine( HandleKnockback(damage.direction * damage.knockbackForce));
    }

    IEnumerator HandleKnockback(Vector3 knockback)
    {
        //Debug.Log("handling knockback " + knockback.magnitude) ;
        moveVelocity = knockback;
        canControl = false;
        yield return new WaitForSeconds(0.25f);
        canControl = true;
        moveVelocity = Vector3.zero;
        //Debug.Log("stop knockback");
    }

    public void Death()
    {
        dead = true;
        canControl = false;
        moveVelocity = Vector3.zero;
        GameManager.instance.RestartLevel();
    }
}
