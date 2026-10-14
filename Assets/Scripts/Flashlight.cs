using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Flashlight : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Press this key to toggle the flashlight. The switch animates automatically.")]
    public KeyCode ToggleKey = KeyCode.F;

    [Header("Light")]
    [Tooltip("The Spot Light this switch controls. If it's a child of the switch cube, it will be found automatically.")]
    public Light SpotLight;
    public bool StartOn;

    [Header("Beam Detection")]
    [Tooltip("How far the beam reaches. 0 = use the Spot Light's own Range.")]
    public float DetectionRange = 0f;
    [Tooltip("Thickness of the detection beam. Bigger = more forgiving aim, smaller = more precise.")]
    public float DetectionRadius = 0.25f;
    [Tooltip("Layers the beam can hit. Anything on these layers can also block the beam (walls), so keep walls included.")]
    public LayerMask DetectionMask = ~0;
    [Tooltip("Draw the beam as a line in the Scene view while the flashlight is on.")]
    public bool ShowDebugRay = true;

    [Header("Switch Visuals")]
    [Tooltip("The on/off switch cube. Defaults to this object if left empty.")]
    public Transform SwitchTransform;
    public Renderer SwitchRenderer;
    public Material OffMaterial; // red
    public Material OnMaterial;  // green

    [Header("Switch Movement")]
    [Tooltip("Local-space offset applied while ON. The switch slides back to its resting spot when OFF. Flip the sign if it moves the wrong way.")]
    public Vector3 OnLocalOffset = new Vector3(0f, 0f, 0.05f);
    public float SlideDuration = 0.1f;

    [Header("Sound")]
    public AudioClip ToggleSound;

    /// <summary>True while the flashlight is switched on.</summary>
    public bool IsOn => isOn;

    private bool isOn;
    private Vector3 restLocalPos;
    private Coroutine slideRoutine;
    private AudioSource audioSource;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private LightReactive currentTarget;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;

        if (SwitchTransform == null) SwitchTransform = transform;
        if (SwitchRenderer == null) SwitchRenderer = SwitchTransform.GetComponent<Renderer>();
        if (SpotLight == null) SpotLight = GetComponentInChildren<Light>();

        restLocalPos = SwitchTransform.localPosition;

        // Apply the starting state instantly (no slide, no sound).
        isOn = StartOn;
        SwitchTransform.localPosition = isOn ? restLocalPos + OnLocalOffset : restLocalPos;
        ApplyState();
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            Press();
        }

        if (isOn)
        {
            DetectBeam();
        }
    }

    private void OnDisable()
    {
        ClearTarget();
    }

    /// <summary>
    /// Toggles the flashlight. Called by the ToggleKey, but also public so
    /// anything else (an event, another script) can toggle it too.
    /// </summary>
    public void Press()
    {
        isOn = !isOn;
        ApplyState();

        if (!isOn)
        {
            ClearTarget(); // lights out: whatever was lit is no longer lit
        }

        if (ToggleSound != null)
        {
            audioSource.PlayOneShot(ToggleSound);
        }

        if (slideRoutine != null) StopCoroutine(slideRoutine);
        Vector3 target = isOn ? restLocalPos + OnLocalOffset : restLocalPos;
        slideRoutine = StartCoroutine(SlideTo(target));
    }

    private void ApplyState()
    {
        if (SpotLight != null) SpotLight.enabled = isOn;

        if (SwitchRenderer != null)
        {
            SwitchRenderer.material = isOn ? OnMaterial : OffMaterial;
        }
    }

    /// <summary>
    /// Casts a thick ray along the spotlight and tells the first thing it
    /// hits (if it has a LightReactive) that it's being lit. Walls and other
    /// solid objects block the beam. The player's own body is ignored.
    /// </summary>
    private void DetectBeam()
    {
        Transform origin = SpotLight != null ? SpotLight.transform : SwitchTransform;
        float range = DetectionRange > 0f ? DetectionRange : (SpotLight != null ? SpotLight.range : 10f);

        if (ShowDebugRay)
        {
            Debug.DrawRay(origin.position, origin.forward * range, Color.yellow);
        }

        int count = Physics.SphereCastNonAlloc(
            origin.position, DetectionRadius, origin.forward,
            hitBuffer, range, DetectionMask, QueryTriggerInteraction.Ignore);

        // Find the closest hit that isn't part of the player.
        int closestIndex = -1;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.collider.GetComponentInParent<MovePlayer2>() != null) continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestIndex = i;
            }
        }

        LightReactive newTarget = null;
        if (closestIndex >= 0)
        {
            newTarget = hitBuffer[closestIndex].collider.GetComponentInParent<LightReactive>();
        }

        if (newTarget != currentTarget)
        {
            if (currentTarget != null) currentTarget.LightExit();
            currentTarget = newTarget;
            if (currentTarget != null) currentTarget.LightEnter(this);
        }

        if (currentTarget != null)
        {
            currentTarget.LightStay(Time.deltaTime);
        }
    }

    private void ClearTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.LightExit();
            currentTarget = null;
        }
    }

    private IEnumerator SlideTo(Vector3 target)
    {
        Vector3 start = SwitchTransform.localPosition;

        if (SlideDuration <= 0f)
        {
            SwitchTransform.localPosition = target;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < SlideDuration)
        {
            elapsed += Time.deltaTime;
            SwitchTransform.localPosition = Vector3.Lerp(start, target, elapsed / SlideDuration);
            yield return null;
        }
        SwitchTransform.localPosition = target;
    }
}