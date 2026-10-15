using System.Collections;
using UnityEngine;

/// <summary>
/// Makes a Light flicker, and makes the glowing (emissive) material of the
/// fixture flicker along with it so the bulb/tube and the light stay in sync.
/// Put this on the fixture, or on the object with the Light. Two modes:
///  - Smooth: brightness wobbles continuously with Perlin noise (candle, fire).
///  - Broken: mostly steady, with sudden bursts of stuttering on/off
///    (a dying fluorescent tube, horror-style).
/// </summary>
public class FlickeringLight : MonoBehaviour
{
    public enum FlickerMode
    {
        Smooth,
        Broken
    }

    [Header("References")]
    [Tooltip("The light to flicker. Defaults to the Light on this object or its children.")]
    public Light TargetLight;
    [Tooltip("Renderers whose emission should flicker with the light (the bulb / tube / fixture mesh). Leave empty to use the Renderer on this object, if any.")]
    public Renderer[] EmissiveRenderers;

    [Header("Mode")]
    public FlickerMode Mode = FlickerMode.Broken;
    public bool FlickerOnStart = true;

    [Header("Brightness")]
    [Tooltip("Brightest the light gets. 0 = use whatever intensity the Light starts with.")]
    public float MaxIntensity = 0f;
    [Tooltip("Dimmest the light gets, as a fraction of max (0 = fully off, 1 = no flicker).")]
    [Range(0f, 1f)] public float MinIntensityFraction = 0.2f;

    [Header("Emission")]
    [Tooltip("Also flicker the emission of the renderers above.")]
    public bool FlickerEmission = true;
    [Tooltip("Name of the material's emission color property. '_EmissionColor' works for Standard, URP Lit and HDRP Lit.")]
    public string EmissionProperty = "_EmissionColor";
    [Tooltip("Scales the emission up or down without touching the material. 1 = exactly the material's own emission, 0.5 = half as bright. Lower this if the bar is too bright.")]
    [Min(0f)] public float EmissionStrength = 1f;

    [Header("Smooth Mode")]
    [Tooltip("How fast the brightness wobbles.")]
    public float SmoothSpeed = 8f;

    [Header("Broken Mode")]
    [Tooltip("Seconds of steady light between bursts of flickering (random between these two).")]
    public Vector2 SteadyTime = new Vector2(1f, 4f);
    [Tooltip("How many on/off stutters happen per burst (random between these two).")]
    public Vector2Int StuttersPerBurst = new Vector2Int(2, 6);
    [Tooltip("How long each individual stutter lasts, in seconds (random between these two).")]
    public Vector2 StutterLength = new Vector2(0.03f, 0.12f);

    [Header("Sound (Broken mode only, all optional)")]
    [Tooltip("Played once when the light starts flickering, e.g. a bulb popping or blowing.")]
    public AudioClip BulbPopSound;
    [Range(0f, 1f)] public float BulbPopVolume = 1f;
    [Tooltip("Loops the whole time the light is broken, including the steady moments between flickers (e.g. an electrical buzz). Should loop seamlessly.")]
    public AudioClip BrokenLoopSound;
    [Range(0f, 1f)] public float BrokenLoopVolume = 0.6f;
    [Tooltip("If ticked, the loop waits for the pop to finish before it starts. If unticked, they start together.")]
    public bool LoopStartsAfterPop = true;
    [Tooltip("Optional. Used for the pop and to copy 3D sound settings (distance, rolloff) onto the loop. One is created automatically if left empty.")]
    public AudioSource Audio;
    [Tooltip("Inside this distance the sounds are at full volume. Raise it if the light sounds too quiet from where you stand. Applies to both the pop and the loop.")]
    public float SoundMinDistance = 5f;
    [Tooltip("Beyond this distance the sounds can't be heard at all. Volume fades out in a straight line between Min and Max. Applies to both the pop and the loop.")]
    public float SoundMaxDistance = 30f;
    [Tooltip("Prints what the sound system is doing to the Console. Handy while setting up; untick when it works.")]
    public bool LogAudioDebug = true;

    private AudioSource loopSource;

    private float baseIntensity;
    private float noiseSeed;
    private bool flickering;
    private Coroutine brokenRoutine;

    // Emission bookkeeping: one original color per renderer, plus a reusable property block.
    // Stored as raw Vector4 (not Color) so no sRGB/linear conversion happens on the way in or out.
    private Vector4[] baseEmission;
    private int emissionId;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        if (TargetLight == null) TargetLight = GetComponentInChildren<Light>();
        if (Audio == null) Audio = GetComponent<AudioSource>();

        if (TargetLight == null)
        {
            Debug.LogWarning(name + ": FlickeringLight needs a Light component (or one assigned in the Inspector).");
            enabled = false;
            return;
        }

        baseIntensity = MaxIntensity > 0f ? MaxIntensity : TargetLight.intensity;
        noiseSeed = Random.Range(0f, 1000f); // so several lights don't flicker in sync
        flickering = FlickerOnStart;

        SetupEmission();
        SetupAudio();
    }

    private void SetupAudio()
    {
        if (BulbPopSound == null && BrokenLoopSound == null) return;

        if (Audio == null)
        {
            Audio = gameObject.AddComponent<AudioSource>();
            Audio.spatialBlend = 1f; // a light in the world should sound like it's there
        }
        Audio.playOnAwake = false;

        // Unity's default 3D falloff drops volume to roughly 1/distance, which
        // makes a quiet buzz nearly inaudible from ~10 units away. Use a gentler
        // straight-line falloff instead.
        Audio.minDistance = SoundMinDistance;
        Audio.maxDistance = Mathf.Max(SoundMaxDistance, SoundMinDistance + 0.1f);
        Audio.rolloffMode = AudioRolloffMode.Linear;

        // The loop gets its own source (copying the 3D settings) so it can run
        // continuously while the pop and anything else plays on top of it.
        loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.playOnAwake = false;
        loopSource.loop = true;
        loopSource.clip = BrokenLoopSound;
        loopSource.volume = BrokenLoopVolume;
        loopSource.spatialBlend = Audio.spatialBlend;
        loopSource.minDistance = Audio.minDistance;
        loopSource.maxDistance = Audio.maxDistance;
        loopSource.rolloffMode = Audio.rolloffMode;
        loopSource.dopplerLevel = Audio.dopplerLevel;
        loopSource.spread = Audio.spread;
        // A Custom rolloff is a curve, not just a mode, so copy the curve itself too.
        loopSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Audio.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
        loopSource.outputAudioMixerGroup = Audio.outputAudioMixerGroup;
    }

    private void BeginBrokenAudio()
    {
        if (Audio != null && BulbPopSound != null)
        {
            Audio.PlayOneShot(BulbPopSound, BulbPopVolume);
        }

        if (BrokenLoopSound == null)
        {
            if (LogAudioDebug) Debug.LogWarning(name + ": 'Broken Loop Sound' is empty, so there is no loop to play. Drag a clip into that field.", this);
            return;
        }

        if (loopSource == null)
        {
            if (LogAudioDebug) Debug.LogWarning(name + ": the loop's AudioSource wasn't created. Was a Broken Loop Sound assigned before the game started?", this);
            return;
        }

        loopSource.Stop();
        loopSource.clip = BrokenLoopSound;
        loopSource.loop = true;
        loopSource.mute = false;
        loopSource.volume = BrokenLoopVolume;

        float delay = (LoopStartsAfterPop && BulbPopSound != null) ? BulbPopSound.length : 0f;
        if (delay > 0f) loopSource.PlayDelayed(delay);
        else loopSource.Play();

        if (LogAudioDebug)
        {
            Debug.Log(name + ": broken loop '" + BrokenLoopSound.name + "' starting in " + delay.ToString("F2")
                + "s at volume " + BrokenLoopVolume.ToString("F2") + ". (Bulb pop clip is " + (BulbPopSound != null ? BulbPopSound.length.ToString("F2") + "s long" : "not set") + ".)", this);
            StartCoroutine(ReportLoopState(delay + 0.5f));
        }
    }

    /// <summary>
    /// Debug only: a moment after the loop should have started, report whether
    /// Unity says it's actually playing, and how far the listener is from it.
    /// </summary>
    private IEnumerator ReportLoopState(float wait)
    {
        yield return new WaitForSeconds(wait);
        if (loopSource == null) yield break;

        AudioListener listener = FindFirstObjectByType<AudioListener>();
        string distance = listener != null
            ? Vector3.Distance(listener.transform.position, loopSource.transform.position).ToString("F1")
            : "no AudioListener found";

        Debug.Log(name + ": loop check -> isPlaying=" + loopSource.isPlaying
            + ", volume=" + loopSource.volume.ToString("F2")
            + ", mute=" + loopSource.mute
            + ", spatialBlend=" + loopSource.spatialBlend.ToString("F2")
            + ", maxDistance=" + loopSource.maxDistance.ToString("F0")
            + ", distance to listener=" + distance
            + ", clip length=" + (loopSource.clip != null ? loopSource.clip.length.ToString("F2") + "s" : "none"), this);
    }

    private void StopBrokenAudio()
    {
        if (loopSource != null) loopSource.Stop(); // also cancels a pending delayed start
    }

    private void SetupEmission()
    {
        if (!FlickerEmission) return;

        if (EmissiveRenderers == null || EmissiveRenderers.Length == 0)
        {
            Renderer own = GetComponent<Renderer>();
            EmissiveRenderers = own != null ? new[] { own } : new Renderer[0];
        }

        emissionId = Shader.PropertyToID(EmissionProperty);
        block = new MaterialPropertyBlock();
        baseEmission = new Vector4[EmissiveRenderers.Length];

        for (int i = 0; i < EmissiveRenderers.Length; i++)
        {
            Renderer r = EmissiveRenderers[i];
            if (r == null) continue;

            if (!r.sharedMaterial.HasProperty(emissionId))
            {
                Debug.LogWarning(name + ": material on " + r.name + " has no '" + EmissionProperty + "' property, so its emission can't flicker.", r);
                continue;
            }

            baseEmission[i] = r.sharedMaterial.GetVector(emissionId);
        }
    }

    private void OnEnable()
    {
        if (TargetLight != null && flickering) StartMode();
    }

    private void OnDisable()
    {
        if (brokenRoutine != null)
        {
            StopCoroutine(brokenRoutine);
            brokenRoutine = null;
        }
        StopBrokenAudio();
        if (TargetLight != null) SetLevel(1f);
    }

    private void Update()
    {
        if (!flickering || Mode != FlickerMode.Smooth) return;

        float noise = Mathf.PerlinNoise(noiseSeed, Time.time * SmoothSpeed); // 0..1
        SetLevel(Mathf.Lerp(MinIntensityFraction, 1f, noise));
    }

    /// <summary>Start flickering (call from a UnityEvent, e.g. when a button is pressed).</summary>
    public void StartFlickering()
    {
        if (flickering && brokenRoutine != null) return; // already going; don't pop again

        flickering = true;
        StartMode();
    }

    /// <summary>Stop flickering and return the light and emission to full steady brightness.</summary>
    public void StopFlickering()
    {
        flickering = false;
        if (brokenRoutine != null)
        {
            StopCoroutine(brokenRoutine);
            brokenRoutine = null;
        }
        StopBrokenAudio();
        SetLevel(1f);
    }

    private void StartMode()
    {
        if (brokenRoutine != null)
        {
            StopCoroutine(brokenRoutine);
            brokenRoutine = null;
        }

        if (Mode == FlickerMode.Broken)
        {
            brokenRoutine = StartCoroutine(BrokenLoop());
            BeginBrokenAudio();
        }
    }

    /// <summary>
    /// Single place that sets brightness, so the light and the emissive
    /// material always change together. level: 0 = off, 1 = full brightness.
    /// </summary>
    private void SetLevel(float level)
    {
        TargetLight.intensity = baseIntensity * level;

        if (!FlickerEmission || EmissiveRenderers == null) return;

        for (int i = 0; i < EmissiveRenderers.Length; i++)
        {
            Renderer r = EmissiveRenderers[i];
            if (r == null || !r.sharedMaterial.HasProperty(emissionId)) continue;

            r.GetPropertyBlock(block);
            // Scale RGB only; leave alpha alone.
            Vector4 b = baseEmission[i];
            float s = level * EmissionStrength;
            block.SetVector(emissionId, new Vector4(b.x * s, b.y * s, b.z * s, b.w));
            r.SetPropertyBlock(block);
        }
    }

    private IEnumerator BrokenLoop()
    {
        SetLevel(1f);

        while (true)
        {
            yield return new WaitForSeconds(Random.Range(SteadyTime.x, SteadyTime.y));

            int stutters = Random.Range(StuttersPerBurst.x, StuttersPerBurst.y + 1);
            for (int i = 0; i < stutters; i++)
            {
                // Cut out (to a random dim level, sometimes fully off).
                SetLevel(Random.Range(0f, MinIntensityFraction));
                yield return new WaitForSeconds(Random.Range(StutterLength.x, StutterLength.y));

                // Pop back on.
                SetLevel(1f);
                yield return new WaitForSeconds(Random.Range(StutterLength.x, StutterLength.y));
            }
        }
    }
}