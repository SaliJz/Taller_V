using UnityEngine;

/// <summary>
/// Gestor de audio del sistema de inventario.
/// </summary>
public class InventoryAudioManager : MonoBehaviour
{
    #region Public Properties & Events

    public static InventoryAudioManager Instance { get; private set; }

    #endregion

    #region Inspector - References

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [Tooltip("Source dedicada a la musica de inventario (loop)")]
    [SerializeField] private AudioSource musicSource;

    #endregion

    #region Inspector - Sound Effects (Clips)

    [Header("Apertura / Cierre")]
    [SerializeField] private AudioClip inventoryOpenSound;
    [SerializeField] private AudioClip inventoryCloseSound;

    [Header("Hover de Slots")]
    [SerializeField] private AudioClip goldenSlotHoverSound;
    [SerializeField] private AudioClip commonSlotHoverSound;

    [Header("Interaccion")]
    [SerializeField] private AudioClip itemClickSound;
    [SerializeField] private AudioClip itemAddedSound;
    [Tooltip("Sonido al cambiar seleccion entre slots dorados")]
    [SerializeField] private AudioClip switchGoldenSlotSound;

    [Header("Sonidos por Rareza")]
    [SerializeField] private AudioClip normalSound;
    [SerializeField] private AudioClip rareSound;
    [SerializeField] private AudioClip superRareSound;

    #endregion

    #region Inspector - Music Settings

    [Header("Musica de Inventario")]
    [SerializeField] private AudioClip inventoryMusic;
    [SerializeField][Range(0f, 1f)] private float musicVolume = 0.4f;

    #endregion

    #region Inspector - Volume Settings

    [Header("Volumenes SFX")]
    [SerializeField][Range(0f, 1f)] private float hoverVolume = 0.5f;
    [SerializeField][Range(0f, 1f)] private float clickVolume = 0.7f;
    [SerializeField][Range(0f, 1f)] private float rarityVolume = 0.8f;
    [SerializeField][Range(0f, 1f)] private float addedVolume = 0.9f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
        }
    }

    #endregion

    #region Audio Playback API

    public void PlayOpenSound()
    {
        PlaySfx(inventoryOpenSound, 1f);
        PlayMusic();
    }

    public void PlayCloseSound()
    {
        PlaySfx(inventoryCloseSound, 1f);
        StopMusic();
    }

    public void PlayGoldenSlotHoverSound() => PlaySfx(goldenSlotHoverSound, hoverVolume);
    public void PlayCommonSlotHoverSound() => PlaySfx(commonSlotHoverSound, hoverVolume);
    public void PlaySwitchGoldenSlotSound() => PlaySfx(switchGoldenSlotSound, hoverVolume);

    public void PlayClickSound() => PlaySfx(itemClickSound, clickVolume);
    public void PlayItemAddedSound() => PlaySfx(itemAddedSound, addedVolume);

    public void PlayRaritySound(ItemRarity rarity)
    {
        AudioClip clip = rarity switch
        {
            ItemRarity.Raro => rareSound,
            ItemRarity.SuperRaro => superRareSound,
            _ => normalSound
        };
        PlaySfx(clip, rarityVolume);
    }

    #endregion

    #region Internal Audio Logic

    private void PlayMusic()
    {
        if (musicSource == null || inventoryMusic == null) return;
        musicSource.clip = inventoryMusic;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    private void StopMusic()
    {
        if (musicSource != null && musicSource.isPlaying) musicSource.Stop();
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip, volume);
        }
    }

    #endregion
}