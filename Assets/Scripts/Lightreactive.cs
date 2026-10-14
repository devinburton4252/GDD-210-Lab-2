using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Put this on anything that should know when the flashlight beam is hitting it
/// (a monster, a plant, a hidden symbol, etc.). The collider can be on this
/// object or a child.
///
/// Two ways to use it:
///  1. No code: hook up the OnLit / OnUnlit events in the Inspector.
///  2. Code: make a new script that inherits from LightReactive and override
///     HandleLit / HandleLitStay / HandleUnlit, e.g. a monster that attacks
///     the player after being lit for a few seconds.
/// </summary>
public class LightReactive : MonoBehaviour
{
    [Tooltip("Fired the moment the flashlight beam starts hitting this object.")]
    public UnityEvent OnLit;

    [Tooltip("Fired the moment the beam stops hitting this object (moved away, blocked, or flashlight turned off).")]
    public UnityEvent OnUnlit;

    [Header("Testing")]
    [Tooltip("Print a message to the Console whenever this object is lit or unlit.")]
    public bool LogToConsole = true;

    // Shown in the Inspector while playing so you can watch them change.
    // (Treat these as read-only; the Flashlight controls them.)
    [SerializeField] private bool isLit;
    [SerializeField] private float litTime;

    /// <summary>True while the beam is currently hitting this object.</summary>
    public bool IsLit => isLit;

    /// <summary>How many seconds the beam has been continuously hitting this object.</summary>
    public float LitTime => litTime;

    /// <summary>The flashlight currently lighting this object (null when unlit).</summary>
    public Flashlight Source { get; private set; }

    // Called by Flashlight. Not meant to be called from anywhere else.
    public void LightEnter(Flashlight source)
    {
        isLit = true;
        litTime = 0f;
        Source = source;

        if (LogToConsole) Debug.Log(name + " LIT", this);

        HandleLit(source);
        OnLit?.Invoke();
    }

    public void LightStay(float deltaTime)
    {
        litTime += deltaTime;
        HandleLitStay(deltaTime);
    }

    public void LightExit()
    {
        isLit = false;
        Source = null;

        if (LogToConsole) Debug.Log(name + " UNLIT (was lit for " + litTime.ToString("F1") + "s)", this);

        HandleUnlit();
        OnUnlit?.Invoke();
        litTime = 0f;
    }

    // Override these in your own scripts (e.g. Monster : LightReactive).
    protected virtual void HandleLit(Flashlight source) { }
    protected virtual void HandleLitStay(float deltaTime) { }
    protected virtual void HandleUnlit() { }

    private void OnDisable()
    {
        // If this object gets disabled/destroyed while lit, don't leave it stuck "lit".
        if (isLit)
        {
            isLit = false;
            Source = null;
            litTime = 0f;
        }
    }
}