using UnityEngine;

public enum GrabbableType
{
    Bread,
    Apple
}

/// <summary>
/// Put this on any object the player should be able to pick up and carry
/// (e.g. the Bread and Apple prefabs). Requires a Rigidbody + Collider.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class Grabbable : MonoBehaviour
{
    public GrabbableType Type;

    [Tooltip("How far above the Tray this object hovers while held.")]
    public float HoverHeight = 0.5f;

    [Header("Sound")]
    public AudioClip PickupSound;
    public AudioClip ThrowSound;

    private Rigidbody rb;
    private AudioSource audioSource;
    private Transform originalParent;
    private Vector3 originalWorldScale;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        originalWorldScale = transform.lossyScale; // the object's real, visual size
    }

    /// <summary>
    /// Called by the player when they pick this object up. It teleports to
    /// hover just above the given tray so the player can see what they're carrying.
    /// </summary>
    public void Grab(Transform tray)
    {
        originalParent = transform.parent;

        rb.isKinematic = true;
        rb.useGravity = false;

        transform.SetParent(tray, false);
        transform.localPosition = Vector3.up * HoverHeight;
        transform.localRotation = Quaternion.identity;
        transform.localScale = CompensatedLocalScale(tray);

        rb.position = transform.position;
        rb.rotation = transform.rotation;
        Physics.SyncTransforms();

        if (PickupSound != null)
        {
            audioSource.PlayOneShot(PickupSound);
        }
    }

    /// <summary>
    /// Called by the player when they drop or throw this object.
    /// Pass a velocity to throw it, or leave null to just drop it in place.
    /// </summary>
    public void Release(Vector3? throwVelocity = null)
    {
        Vector3 worldPos = transform.position;
        Quaternion worldRot = transform.rotation;

        transform.SetParent(originalParent, false);
        transform.position = worldPos;
        transform.rotation = worldRot;
        transform.localScale = CompensatedLocalScale(originalParent);

        // While kinematic, Unity can accumulate a stale internal velocity from
        // being teleported around by the player. Force the Rigidbody's actual
        // physics state to match the transform and clear that out BEFORE
        // switching back to dynamic, or physics can violently "correct" it
        // next step (which looks like the object launching upward on its own).
        rb.position = transform.position;
        rb.rotation = transform.rotation;
        Physics.SyncTransforms();

        rb.isKinematic = false;
        rb.useGravity = true;

        // Velocity can only be set once the body is no longer kinematic.
        rb.linearVelocity = throwVelocity ?? Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (ThrowSound != null)
        {
            audioSource.PlayOneShot(ThrowSound);
        }
    }

    /// <summary>
    /// Local scale needed under the given parent so this object's real
    /// (world) size stays the same, regardless of the parent's own scale.
    /// </summary>
    private Vector3 CompensatedLocalScale(Transform parent)
    {
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        return new Vector3(
            parentScale.x != 0 ? originalWorldScale.x / parentScale.x : originalWorldScale.x,
            parentScale.y != 0 ? originalWorldScale.y / parentScale.y : originalWorldScale.y,
            parentScale.z != 0 ? originalWorldScale.z / parentScale.z : originalWorldScale.z
        );
    }
}