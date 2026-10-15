using UnityEngine;

/// <summary>
/// Put this on the stretched cube with your water shader. When the player
/// walks in, it plays a splash once, then a looping "wading through water"
/// sound whose volume and pitch follow how fast the player is actually
/// moving. Standing still makes it fade out; walking at full speed makes it
/// loudest. Everything is tweakable with sliders in the Inspector.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WaterZone : MonoBehaviour
{
    [Header("Splash (plays once when entering)")]
    [Tooltip("One is picked at random each time. Add a few for variety, or just one.")]
    public AudioClip[] SplashSounds;
    [Range(0f, 1f)] public float SplashVolume = 1f;
    [Tooltip("Optional splash when leaving the water.")]
    public AudioClip[] ExitSounds;
    [Range(0f, 1f)] public float ExitVolume = 0.6f;
    [Tooltip("Minimum seconds between splashes, so stepping in and out along the edge doesn't spam it.")]
    public float SplashCooldown = 0.5f;

    [Header("Wading Loop")]
    [Tooltip("Should loop seamlessly. Plays while the player is in the water.")]
    public AudioClip WadingLoop;
    [Tooltip("Loudest the loop gets (at full walking speed).")]
    [Range(0f, 1f)] public float LoopMaxVolume = 0.8f;

    [Header("Speed Matching")]
    [Tooltip("0 = the loop plays at a constant volume and pitch no matter how fast you move. 1 = fully follows your speed (silent when standing still).")]
    [Range(0f, 1f)] public float SpeedMatching = 1f;
    [Tooltip("Speed that counts as 'full speed'. 0 = use the player's Walk Speed automatically.")]
    public float MaxSpeed = 0f;
    [Tooltip("Below this speed (units/sec) the player counts as standing still.")]
    public float StillThreshold = 0.1f;
    [Tooltip("How volume responds to speed. X = 0 (still) to 1 (full speed), Y = fraction of Loop Max Volume. Bend it to make the sound kick in sooner or later.")]
    public AnimationCurve SpeedToVolume = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Range(0.5f, 2f)] public float PitchAtSlowest = 0.9f;
    [Range(0.5f, 2f)] public float PitchAtFastest = 1.15f;
    [Tooltip("How quickly the loop fades in and out (volume per second).")]
    public float FadeSpeed = 4f;

    [Header("Setup")]
    [Tooltip("0 = plays at the same volume everywhere (recommended for a big water volume). 1 = fully 3D, coming from the center of the cube.")]
    [Range(0f, 1f)] public float SpatialBlend = 0f;
    [Tooltip("Trigger events with a CharacterController need a Rigidbody on one side. This adds a kinematic one to the water if it doesn't have one.")]
    public bool AutoAddKinematicRigidbody = true;

    private AudioSource splashSource;
    private AudioSource loopSource;
    private MovePlayer2 player;
    private float smoothedSpeed;
    private float lastSplashTime = -999f;

    private void Awake()
    {
        // The player has to be able to walk INTO the water, so it can't be solid.
        foreach (Collider c in GetComponents<Collider>())
        {
            c.isTrigger = true;
        }

        if (AutoAddKinematicRigidbody && GetComponentInParent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        splashSource = CreateSource(false);
        loopSource = CreateSource(true);
        loopSource.clip = WadingLoop;
        loopSource.volume = 0f;
    }

    private AudioSource CreateSource(bool loop)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = SpatialBlend;
        return s;
    }

    private void OnTriggerEnter(Collider other)
    {
        MovePlayer2 p = other.GetComponentInParent<MovePlayer2>();
        if (p == null) return;

        // Only react to the player's CharacterController, not any other collider on the player.
        if (p.Cc != null && other != p.Cc) return;
        if (player != null) return; // already inside

        player = p;
        smoothedSpeed = 0f;

        if (Time.time - lastSplashTime >= SplashCooldown)
        {
            PlayRandom(splashSource, SplashSounds, SplashVolume);
            lastSplashTime = Time.time;
        }

        if (WadingLoop != null && !loopSource.isPlaying)
        {
            loopSource.volume = 0f;
            loopSource.Play();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (player == null) return;

        MovePlayer2 p = other.GetComponentInParent<MovePlayer2>();
        if (p != player) return;
        if (p.Cc != null && other != p.Cc) return;

        player = null; // the loop fades itself out in Update

        if (Time.time - lastSplashTime >= SplashCooldown)
        {
            PlayRandom(splashSource, ExitSounds, ExitVolume);
            lastSplashTime = Time.time;
        }
    }

    private void Update()
    {
        if (WadingLoop == null) return;

        float targetVolume = 0f;

        if (player != null)
        {
            // Actual horizontal speed of the player (ignore falling/jumping).
            Vector3 v = player.Cc != null ? player.Cc.velocity : Vector3.zero;
            v.y = 0f;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, v.magnitude, 1f - Mathf.Exp(-12f * Time.deltaTime));

            float fullSpeed = MaxSpeed > 0f ? MaxSpeed : Mathf.Max(player.WalkSpeed, 0.01f);
            float t = smoothedSpeed < StillThreshold
                ? 0f
                : Mathf.Clamp01(Mathf.InverseLerp(StillThreshold, fullSpeed, smoothedSpeed));

            targetVolume = LoopMaxVolume * Mathf.Lerp(1f, SpeedToVolume.Evaluate(t), SpeedMatching);
            loopSource.pitch = Mathf.Lerp(PitchAtSlowest, PitchAtFastest, Mathf.Lerp(0.5f, t, SpeedMatching));
        }

        loopSource.volume = Mathf.MoveTowards(loopSource.volume, targetVolume, FadeSpeed * Time.deltaTime);

        if (player == null && loopSource.isPlaying && loopSource.volume <= 0.001f)
        {
            loopSource.Stop();
        }
    }

    private void OnDisable()
    {
        player = null;
        if (loopSource != null) loopSource.Stop();
    }

    private static void PlayRandom(AudioSource source, AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        source.PlayOneShot(clip, volume);
    }
}