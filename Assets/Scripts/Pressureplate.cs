using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Put this on a trigger-collider GameObject (a flat box collider with
/// "Is Trigger" checked works well). Unlike TriggerZone, nothing is consumed —
/// this just tracks whether at least one qualifying object is currently
/// resting on/inside it, and fires events when that changes.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PressurePlate : MonoBehaviour
{
    [Tooltip("What's allowed to activate this plate (e.g. Player, and/or whatever layer your Bread/Apple are on).")]
    public LayerMask ActivationMask = ~0;

    [Header("Visuals")]
    public Renderer PlateRenderer;
    public Material InactiveMaterial; // e.g. red
    public Material ActiveMaterial;   // e.g. green

    [Header("Sound")]
    public AudioClip ActivateSound;
    public AudioClip DeactivateSound;

    [Tooltip("Fired the moment the plate goes from empty to weighted.")]
    public UnityEvent OnActivated;

    [Tooltip("Fired the moment the last qualifying object leaves.")]
    public UnityEvent OnDeactivated;

    private int weightCount;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        UpdateVisual();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsInMask(other.gameObject.layer)) return;

        weightCount++;
        if (weightCount == 1)
        {
            UpdateVisual();
            if (ActivateSound != null) audioSource.PlayOneShot(ActivateSound);
            OnActivated?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsInMask(other.gameObject.layer)) return;

        weightCount = Mathf.Max(0, weightCount - 1);
        if (weightCount == 0)
        {
            UpdateVisual();
            if (DeactivateSound != null) audioSource.PlayOneShot(DeactivateSound);
            OnDeactivated?.Invoke();
        }
    }

    private void UpdateVisual()
    {
        if (PlateRenderer == null) return;
        PlateRenderer.material = weightCount > 0 ? ActiveMaterial : InactiveMaterial;
    }

    private bool IsInMask(int layer)
    {
        return (ActivationMask.value & (1 << layer)) != 0;
    }
}