using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Put this on a trigger-collider GameObject (BoxCollider with "Is Trigger" checked).
/// When a Grabbable of the matching type enters — whether it's flying, rolling,
/// or still being carried by the player — it gets consumed, and a TMP label
/// updates to show how many more are needed.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class TriggerZone : MonoBehaviour
{
    public GrabbableType AcceptedType;

    [Tooltip("A sound is picked at random from this list each time a valid item is placed.")]
    public AudioClip[] PlacementSounds;

    private AudioSource audioSource;

    [Tooltip("How many of the accepted type this zone needs before it's complete.")]
    public int RequiredCount = 1;

    [Tooltip("Optional label shown before the number, e.g. 'Bread'. Leave blank to just show the number.")]
    public string Label;

    [Tooltip("TMP text (world-space, e.g. on a child Canvas) that shows the remaining count.")]
    public TMP_Text CountText;

    [Tooltip("Fired every time a valid item is placed.")]
    public UnityEvent OnItemPlaced;

    [Tooltip("Fired once when RequiredCount is reached.")]
    public UnityEvent OnZoneComplete;

    private int remaining;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        remaining = RequiredCount;
        UpdateText();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("TriggerZone saw: " + other.name + " | remaining=" + remaining);

        if (remaining <= 0) return; // Already complete — ignore anything further.

        // GetComponentInParent in case the Collider lives on a child mesh
        // rather than the same GameObject as the Grabbable script.
        Grabbable grabbable = other.GetComponentInParent<Grabbable>();
        if (grabbable == null)
        {
            Debug.Log("TriggerZone: " + other.name + " has no Grabbable component (checked self + parents).");
            return;
        }
        if (grabbable.Type != AcceptedType)
        {
            Debug.Log("TriggerZone: " + grabbable.name + " is type " + grabbable.Type + ", this zone wants " + AcceptedType);
            return;
        }

        // Destroying it while it's parented under the player's Tray is fine —
        // MovePlayer2 checks "heldObject == null" every frame, and Unity treats a
        // destroyed object as null, so the player's hands free up automatically.
        Destroy(grabbable.gameObject);

        remaining--;
        UpdateText();
        PlayRandomSound();
        OnItemPlaced?.Invoke();

        if (remaining <= 0)
        {
            OnZoneComplete?.Invoke();
        }
    }

    private void UpdateText()
    {
        if (CountText == null) return;
        CountText.text = string.IsNullOrEmpty(Label) ? remaining.ToString() : $"{Label}: {remaining}";
    }

    private void PlayRandomSound()
    {
        if (PlacementSounds == null || PlacementSounds.Length == 0) return;
        AudioClip clip = PlacementSounds[Random.Range(0, PlacementSounds.Length)];
        audioSource.PlayOneShot(clip);
    }
}