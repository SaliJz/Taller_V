using UnityEngine;

public class PlayerAudioController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioSource mainAudioSource;

    [Header("Damage SFX Configuration")]
    [SerializeField] private AudioClip damageClip;
    [Range(0f, 1f)][SerializeField] private float damageVolume = 1.0f;
    [Range(0f, 2f)][SerializeField] private float damagePitch = 1.0f;
    [Header("Variance")]
    [SerializeField] private bool useDamagePitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float damagePitchVariance = 0.1f;

    [Header("Death SFX Configuration")]
    [SerializeField] private AudioClip deathClip;
    [Range(0f, 1f)][SerializeField] private float deathVolume = 1.0f;
    [Range(0f, 2f)][SerializeField] private float deathPitch = 1.0f;
    [Header("Variance")]
    [SerializeField] private bool useDeathPitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float deathPitchVariance = 0.1f;

    [Header("Step SFX Configuration")]
    [SerializeField] private AudioClip footstepGenericClip;
    [SerializeField] private AudioClip footstepNivel1Clip;
    [SerializeField] private AudioClip footstepNivel2Clip;
    [SerializeField] private AudioClip footstepNivel3Clip;
    [Range(0f, 1f)][SerializeField] private float stepVolume = 1.0f;
    [Range(0f, 2f)][SerializeField] private float stepPitch = 1.0f;
    [Header("Variance")]
    [SerializeField] private bool useStepPitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float stepPitchVariance = 0.1f;

    [Header("Dash SFX Configuration")]
    [SerializeField] private AudioClip dashClip;
    [Range(0f, 1f)][SerializeField] private float dashVolume = 1.0f;
    [Range(0f, 2f)][SerializeField] private float dashPitch = 1.0f;
    [Header("Variance")]
    [SerializeField] private bool usePitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float pitchVariance = 0.1f;

    [Header("Melee SFX Configuration")]
    [SerializeField] private AudioClip basicSlashClip;
    [SerializeField] private AudioClip spinSlashClip;
    [SerializeField] private AudioClip heavySlashClip;
    [Range(0f, 1f)][SerializeField] private float meleeVolume = 1.0f;
    [Range(0f, 2f)][SerializeField] private float meleePitch = 1.0f;
    [Header("Variance")]
    [SerializeField] private bool useMeleePitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float meleePitchVariance = 0.1f;

    [Header("Hit SFX Configuration")]
    [SerializeField] private AudioClip hitClip;
    [Range(0f, 1f)][SerializeField] private float hitVolume = 0.85f;
    [Range(0f, 2f)][SerializeField] private float hitPitch = 1.0f;
    [Header("Hit Variance")]
    [SerializeField] private bool useHitPitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float hitPitchVariance = 0.1f;

    [Header("Range SFX Configuration")]
    [SerializeField] private AudioClip throwShieldClip;
    [SerializeField] private AudioClip catchShieldClip;
    [Range(0f, 1f)][SerializeField] private float rangeVolume = 1.0f;
    [Range(0f, 2f)][SerializeField] private float rangePitch = 1.0f;
    [Header("Variance")]
    [SerializeField] private bool useRangePitchVariance = true;
    [Range(0f, 0.5f)][SerializeField] private float rangePitchVariance = 0.1f;

    private void Awake()
    {
        if (mainAudioSource == null)
        {
            mainAudioSource = GetComponentInChildren<AudioSource>();
            if (mainAudioSource == null)
                Debug.LogError($"{name}: No se encontró AudioSource hijo.");
        }
    }

    public void PlayDamageSound()
    {
        PlayOneShotInternal(damageClip, damageVolume, damagePitch, useDamagePitchVariance, damagePitchVariance);
    }

    public void PlayDeathSound()
    {
        PlayOneShotInternal(deathClip, deathVolume, deathPitch, useDeathPitchVariance, deathPitchVariance);
    }

    public void PlayStepSound(int nivel)
    {
        AudioClip clipToPlay = null;
        switch (nivel)
        {
            case 0:
                clipToPlay = footstepGenericClip;
                break;
            case 1:
                clipToPlay = footstepNivel1Clip;
                break;
            case 2:
                clipToPlay = footstepNivel2Clip;
                break;
            case 3:
                clipToPlay = footstepNivel3Clip;
                break;
            default:
                Debug.LogWarning($"{name}: Nivel de paso '{nivel}' no válido para sonido de paso.");
                return;
        }

        PlayOneShotInternal(clipToPlay, stepVolume, stepPitch, useStepPitchVariance, stepPitchVariance);
    }

    public void PlayDashSound()
    {
        PlayOneShotInternal(dashClip, dashVolume, dashPitch, usePitchVariance, pitchVariance);
    }

    public void PlayMeleeSound(string attackType)
    {
        AudioClip clipToPlay = null;
        switch (attackType)
        {
            case "BasicSlash":
                clipToPlay = basicSlashClip;
                break;
            case "SpinSlash":
                clipToPlay = spinSlashClip;
                break;
            case "HeavySlash":
                clipToPlay = heavySlashClip;
                break;
            default:
                Debug.LogWarning($"{name}: Tipo de ataque '{attackType}' para sonido melee.");
                return;
        }

        PlayOneShotInternal(clipToPlay, meleeVolume, meleePitch, useMeleePitchVariance, meleePitchVariance);
    }

    public void PlayHitSound()
    {
        PlayOneShotInternal(hitClip, hitVolume, hitPitch, useHitPitchVariance, hitPitchVariance);
    }

    public void PlayThrowShieldSound()
    {
        PlayOneShotInternal(throwShieldClip, rangeVolume, rangePitch, useRangePitchVariance, rangePitchVariance);
    }

    public void PlayCatchShieldSound()
    {
        PlayOneShotInternal(catchShieldClip, rangeVolume, rangePitch, useRangePitchVariance, rangePitchVariance);
    }

    // Un método genérico privado para reutilizar la lógica de varianza
    private void PlayOneShotInternal(AudioClip clip, float volume, float basePitch, bool useVariance, float varianceAmount)
    {
        if (clip == null || mainAudioSource == null) return;

        if (useVariance)
        {
            mainAudioSource.pitch = basePitch + Random.Range(-varianceAmount, varianceAmount);
        }
        else
        {
            mainAudioSource.pitch = basePitch;
        }

        mainAudioSource.PlayOneShot(clip, volume);
    }
}