using UnityEngine;
using UnityEngine.UI;

public class MovePlayer2 : MonoBehaviour
{
    [Header("References")]
    public CharacterController Cc;
    public Transform cameraTransform;

    [Header("Movement")]
    public float Gravity;
    public float WalkSpeed;
    public float JumpSpeed;

    [Header("Grabbing")]
    public Transform Tray;
    public float GrabRange = 3f;
    public float ShootSpeed = 8f;
    [Tooltip("Where thrown objects launch from. Leave empty to just throw from wherever the Tray is.")]
    public Transform ThrowPoint;
    [Tooltip("Set this to Everything, then untick the Player's own layer, so the raycast can't hit your own body/arms.")]
    public LayerMask InteractionMask = ~0;

    private float yspeed;
    private Vector3 startPosition;
    private bool canDoubleJump;
    private Grabbable heldObject;

    private void Start()
    {
        startPosition = transform.position;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        HandleLook();
        HandleJumpAndGravity();
        HandleMovement();
        HandleInteraction();
    }

    private void HandleLook()
    {
        transform.Rotate(new Vector3(0, Input.GetAxis("Mouse X"), 0));
        cameraTransform.Rotate(new Vector3(-Input.GetAxis("Mouse Y"), 0, 0));
    }

    private void HandleJumpAndGravity()
    {
        if (Cc.isGrounded)
        {
            yspeed = -1;
            if (Input.GetKeyDown(KeyCode.Space))
            {
                yspeed = JumpSpeed;
            }
            canDoubleJump = true;
        }
        else
        {
            if (canDoubleJump && Input.GetKeyDown(KeyCode.Space))
            {
                yspeed = JumpSpeed;
                canDoubleJump = false;
            }

            if (yspeed > 0 && Input.GetKeyUp(KeyCode.Space))
            {
                yspeed *= 0.1f;
            }

            yspeed += Gravity * Time.deltaTime;
        }
    }

    private void HandleMovement()
    {
        Vector3 move = Vector3.zero;
        move += Input.GetAxis("Vertical") * transform.forward;
        move += Input.GetAxis("Horizontal") * transform.right;
        move = move.normalized * WalkSpeed;
        move += new Vector3(0, yspeed, 0);

        Cc.Move(move * Time.deltaTime);
    }

    /// <summary>
    /// Left click: raycast to press a Button, or grab a Grabbable (only if hands are empty).
    /// Right click: shoot whatever's currently on the Tray forward.
    /// </summary>
    private void HandleInteraction()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleGrabClick();
        }

        if (Input.GetMouseButtonDown(1))
        {
            HandleShootClick();
        }
    }

    private void HandleGrabClick()
    {
        // QueryTriggerInteraction.Ignore so trigger zones (which aren't meant
        // to be clickable) don't block the ray from reaching an item sitting
        // inside one, like a wrongly-placed bread stuck in the apple zone.
        if (!Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, GrabRange, InteractionMask, QueryTriggerInteraction.Ignore)) return;

        Debug.DrawLine(cameraTransform.position, hit.point, Color.red, 1f);
        Debug.Log("Raycast hit: " + hit.collider.name);

        // Belt-and-suspenders: also ignore anything that's part of the player,
        // in case the layer mask isn't set up on every child collider yet.
        if (hit.collider.transform.IsChildOf(transform)) return;

        // Clicking the tray itself while nothing's on it isn't an interaction.
        if (heldObject == null && hit.collider.transform.IsChildOf(Tray)) return;

        Button hitButton = hit.collider.GetComponentInParent<Button>();
        if (hitButton != null)
        {
            hitButton.Press();
            return;
        }

        if (heldObject != null) return; // Tray's already full

        // GetComponentInParent so this still works if the Collider lives on a
        // child mesh object rather than the same GameObject as the Grabbable script.
        Grabbable grabbable = hit.collider.GetComponentInParent<Grabbable>();
        if (grabbable != null)
        {
            Debug.Log("Grabbing: " + grabbable.name);
            grabbable.Grab(Tray);
            heldObject = grabbable;
        }
        else
        {
            Debug.Log("Hit object has no Grabbable component (checked self + parents).");
        }
    }

    private void HandleShootClick()
    {
        if (heldObject == null) return;

        // Snap to a fixed, centered launch point (if set) so throws always
        // come from in front of the player, regardless of where the Tray
        // is visually positioned.
        if (ThrowPoint != null)
        {
            heldObject.transform.position = ThrowPoint.position;
        }

        heldObject.Release(cameraTransform.forward * ShootSpeed);
        heldObject = null;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        ResetTrigger hitTrigger = hit.gameObject.GetComponent<ResetTrigger>();
        if (hitTrigger != null)
        {
            transform.position = startPosition;
            yspeed = -1;
        }
    }
}