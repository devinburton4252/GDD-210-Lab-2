using UnityEngine;

/// <summary>
/// Put this on a small trigger volume positioned right at the top surface of
/// a MovingPlatform, as a child of it (a thin Box Collider with "Is Trigger"
/// checked, roughly matching the platform's walkable area, works well).
/// While the player is standing inside this trigger, they get parented to
/// the platform so they ride along with it instead of sliding or lagging
/// behind. They're unparented the moment they step off.
/// </summary>
public class PlatformRider : MonoBehaviour
{
    [Tooltip("What actually moves — usually the platform itself. Defaults to this object's parent.")]
    public Transform PlatformRoot;

    private void Awake()
    {
        if (PlatformRoot == null) PlatformRoot = transform.parent;
    }

    private void OnTriggerEnter(Collider other)
    {
        MovePlayer2 player = other.GetComponentInParent<MovePlayer2>();
        if (player != null)
        {
            player.SetPlatform(PlatformRoot);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        MovePlayer2 player = other.GetComponentInParent<MovePlayer2>();
        if (player != null)
        {
            player.SetPlatform(null);
        }
    }
}