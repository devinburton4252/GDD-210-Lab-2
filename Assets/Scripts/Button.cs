using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(AudioSource))]
public class Button : MonoBehaviour
{
    [Header("Sound")]
    [Tooltip("Played instead of anything else when the player clicks this while it's locked.")]
    public AudioClip LockedSound;
    [Tooltip("The button's own click, played every time it's successfully pressed (locked or already-moving doesn't count).")]
    public AudioClip ClickSound;
    [Tooltip("Played whenever the Linked Object actually starts moving, e.g. a shutter/gate sound.")]
    public AudioClip GateSound;

    private AudioSource audioSource;

    [Header("Events (optional, in addition to Linked Object above)")]
    [Tooltip("Fired when this button toggles into the pressed state.")]
    public UnityEvent OnPressed;
    [Tooltip("Fired when this button toggles back into the unpressed state.")]
    public UnityEvent OnUnpressed;

    [Header("Locking")]
    [Tooltip("If true, this button ignores Press() until it's unlocked — e.g. by a TriggerZone or PressurePlate elsewhere in the level.")]
    public bool StartLocked;

    [Tooltip("How many separate ReportUnlockCondition() calls are needed before this actually unlocks. Use 1 for a single gate, or one-per-monster if several must be fed first. Leave at 1 and just call Unlock() directly if you don't need counting.")]
    public int RequiredUnlockSignals = 1;

    private bool isLocked;
    private int unlockSignalsReceived;

    [Header("Visuals")]
    public Renderer ButtonRenderer;
    public Material UnpressedMaterial; // red
    public Material PressedMaterial;   // green

    [Header("Press Animation (the smaller square)")]
    [Tooltip("The inner button square that physically pushes in. Defaults to this object if left empty.")]
    public Transform ButtonCap;
    [Tooltip("Local-space offset applied while pressed in, e.g. (0, 0, -0.1) to sink it back into the base.")]
    public Vector3 PressLocalOffset = new Vector3(0f, 0f, -0.1f);
    public float PressInDuration = 0.08f;
    public float PressOutDuration = 0.15f;

    [Header("Linked Object")]
    [Tooltip("Something else that moves when this button is toggled, and stays moved until pressed again (e.g. a door or platform).")]
    public Transform LinkedObject;
    [Tooltip("Local-space offset applied to the Linked Object while this button is in the 'pressed' state. Use just one axis (X, Y or Z) for a simple slide.")]
    public Vector3 LinkedObjectOffset = new Vector3(0f, 1f, 0f);
    public float LinkedObjectMoveDuration = 0.3f;

    private bool isPressed;
    private Vector3 capRestLocalPos;
    private Vector3 linkedRestLocalPos;
    private Coroutine capRoutine;
    private Coroutine linkedRoutine;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;

        isLocked = StartLocked;

        if (ButtonCap == null) ButtonCap = transform;
        capRestLocalPos = ButtonCap.localPosition;

        if (LinkedObject != null)
        {
            linkedRestLocalPos = LinkedObject.localPosition;
        }

        UpdateVisual();
    }

    /// <summary>
    /// Called by whatever raycasts into this button (see MovePlayer2).
    /// Toggles pressed state, swaps material, plays the cap punch animation,
    /// and moves the Linked Object to/from its offset position.
    /// </summary>
    /// <summary>
    /// Call from a TriggerZone's OnZoneComplete, a PressurePlate's OnActivated,
    /// or anything else that should gate this button (e.g. "feed the monster
    /// in Room 1 before Room 2's button will do anything"). Immediately
    /// unlocks — use ReportUnlockCondition instead if several separate things
    /// all need to happen first.
    /// </summary>
    public void Lock() => isLocked = true;
    public void Unlock() => isLocked = false;

    /// <summary>
    /// Call this once per separate condition (e.g. once per monster's
    /// OnZoneComplete). Only actually unlocks once it's been called
    /// RequiredUnlockSignals times total.
    /// </summary>
    public void ReportUnlockCondition()
    {
        unlockSignalsReceived++;
        if (unlockSignalsReceived >= RequiredUnlockSignals)
        {
            Unlock();
        }
    }

    public void Press()
    {
        if (isLocked)
        {
            if (LockedSound != null)
            {
                audioSource.PlayOneShot(LockedSound);
            }
            return;
        }

        isPressed = !isPressed;
        UpdateVisual();

        if (ClickSound != null)
        {
            audioSource.PlayOneShot(ClickSound);
        }

        if (isPressed) OnPressed?.Invoke();
        else OnUnpressed?.Invoke();

        if (capRoutine != null) StopCoroutine(capRoutine);
        capRoutine = StartCoroutine(PunchCap());

        if (LinkedObject != null)
        {
            if (linkedRoutine != null) StopCoroutine(linkedRoutine);
            Vector3 target = isPressed ? linkedRestLocalPos + LinkedObjectOffset : linkedRestLocalPos;
            linkedRoutine = StartCoroutine(MoveOverTime(LinkedObject, LinkedObject.localPosition, target, LinkedObjectMoveDuration));

            if (GateSound != null)
            {
                audioSource.PlayOneShot(GateSound);
            }
        }
    }

    private void UpdateVisual()
    {
        if (ButtonRenderer == null) return;
        ButtonRenderer.material = isPressed ? PressedMaterial : UnpressedMaterial;
    }

    /// <summary>
    /// Pushes the cap in, then always brings it back to its resting position —
    /// this is just the tactile "click" feedback, independent of toggle state.
    /// </summary>
    private IEnumerator PunchCap()
    {
        Vector3 pressedLocalPos = capRestLocalPos + PressLocalOffset;

        yield return MoveOverTime(ButtonCap, capRestLocalPos, pressedLocalPos, PressInDuration);
        yield return MoveOverTime(ButtonCap, pressedLocalPos, capRestLocalPos, PressOutDuration);
    }

    private IEnumerator MoveOverTime(Transform t, Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            t.localPosition = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            t.localPosition = Vector3.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        t.localPosition = to;
    }
}