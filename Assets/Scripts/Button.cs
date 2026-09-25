using System.Collections;
using UnityEngine;

public class Button : MonoBehaviour
{
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
    public void Press()
    {
        isPressed = !isPressed;
        UpdateVisual();

        if (capRoutine != null) StopCoroutine(capRoutine);
        capRoutine = StartCoroutine(PunchCap());

        if (LinkedObject != null)
        {
            if (linkedRoutine != null) StopCoroutine(linkedRoutine);
            Vector3 target = isPressed ? linkedRestLocalPos + LinkedObjectOffset : linkedRestLocalPos;
            linkedRoutine = StartCoroutine(MoveOverTime(LinkedObject, LinkedObject.localPosition, target, LinkedObjectMoveDuration));
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