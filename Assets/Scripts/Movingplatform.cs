using System.Collections;
using UnityEngine;

/// <summary>
/// A platform, door, or gate that slides between its resting position and an
/// offset position. Doesn't decide for itself when to move — call
/// Extend() / Retract() / Toggle() / SetActive() from a PressurePlate's
/// UnityEvents, a Button's UnityEvents, or anything else.
/// Uses a kinematic Rigidbody + MovePosition so it correctly pushes/carries
/// physics objects (like items resting on it) as it moves.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class MovingPlatform : MonoBehaviour
{
    [Tooltip("Local-space offset from its resting position when active. Use one axis for a simple slide, e.g. (0, 3, 0) to rise 3 units.")]
    public Vector3 ActiveOffset = new Vector3(0f, 3f, 0f);
    public float MoveDuration = 1f;

    [Tooltip("Played every time this starts moving, in either direction (e.g. a mechanical rumble or shutter sound).")]
    public AudioClip MoveSound;

    private Rigidbody rb;
    private AudioSource audioSource;
    private Vector3 restPosition;
    private bool isActive;
    private Coroutine moveRoutine;
    private float moveSoundBusyUntil;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true; // moved by hand via MovePosition, not physics forces
        restPosition = rb.position;

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public void Extend() => SetActive(true);
    public void Retract() => SetActive(false);
    public void Toggle() => SetActive(!isActive);

    public void SetActive(bool active)
    {
        isActive = active;
        Vector3 target = isActive ? restPosition + ActiveOffset : restPosition;

        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveTo(target));

        // PlayOneShot doesn't track its own playback state, so a manual
        // cooldown stops rapid re-triggers (e.g. a flickering pressure plate)
        // from stacking the sound on top of itself before it's finished.
        if (MoveSound != null && Time.time >= moveSoundBusyUntil)
        {
            audioSource.PlayOneShot(MoveSound);
            moveSoundBusyUntil = Time.time + MoveSound.length;
        }
    }

    private IEnumerator MoveTo(Vector3 target)
    {
        Vector3 start = rb.position;
        float elapsed = 0f;

        while (elapsed < MoveDuration)
        {
            elapsed += Time.fixedDeltaTime;
            rb.MovePosition(Vector3.Lerp(start, target, elapsed / MoveDuration));
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(target);
    }
}