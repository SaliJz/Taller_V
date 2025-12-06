using UnityEngine;

/// <summary>
/// Gestor de audio para el sistema de inventario
/// </summary>
public class InventoryAudioManager : MonoBehaviour
{
    public static InventoryAudioManager Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Sonidos de Inventario")]
    [SerializeField] private AudioClip inventoryOpenSound;
    [SerializeField] private AudioClip inventoryCloseSound;
    [SerializeField] private AudioClip itemHoverSound;
    [SerializeField] private AudioClip itemClickSound;
    [SerializeField] private AudioClip itemSelectSound;
    [SerializeField] private AudioClip categoryScrollSound;

    [Header("Sonidos de Rareza")]
    [SerializeField] private AudioClip commonItemSound;
    [SerializeField] private AudioClip rareItemSound;
    [SerializeField] private AudioClip epicItemSound;
    [SerializeField] private AudioClip legendaryItemSound;

    [Header("Configuración")]
    [SerializeField] private float hoverVolume = 0.5f;
    [SerializeField] private float clickVolume = 0.7f;
    [SerializeField] private float rarityVolume = 0.8f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Configuración del AudioSource
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    /// <summary>
    /// Reproduce el sonido de apertura del inventario
    /// </summary>
    public void PlayOpenSound()
    {
        PlaySound(inventoryOpenSound, 1f);
    }

    /// <summary>
    /// Reproduce el sonido de cierre del inventario
    /// </summary>
    public void PlayCloseSound()
    {
        PlaySound(inventoryCloseSound, 1f);
    }

    /// <summary>
    /// Reproduce el sonido de hover sobre un ítem
    /// </summary>
    public void PlayHoverSound()
    {
        PlaySound(itemHoverSound, hoverVolume);
    }

    /// <summary>
    /// Reproduce el sonido de click en un ítem
    /// </summary>
    public void PlayClickSound()
    {
        PlaySound(itemClickSound, clickVolume);
    }

    /// <summary>
    /// Reproduce el sonido de selección de un ítem
    /// </summary>
    public void PlaySelectSound()
    {
        PlaySound(itemSelectSound, clickVolume);
    }

    /// <summary>
    /// Reproduce el sonido de scroll entre categorías
    /// </summary>
    public void PlayScrollSound()
    {
        PlaySound(categoryScrollSound, hoverVolume);
    }

    /// <summary>
    /// Reproduce el sonido según la rareza del ítem
    /// </summary>
    public void PlayRaritySound(ItemRarity rarity)
    {
        AudioClip clip = null;

        switch (rarity)
        {
            case ItemRarity.Normal:
                clip = commonItemSound;
                break;
            case ItemRarity.Raro:
                clip = rareItemSound;
                break;
            case ItemRarity.SuperRaro:
                clip = epicItemSound;
                break;
        }

        PlaySound(clip, rarityVolume);
    }

    /// <summary>
    /// Reproduce un sonido genérico
    /// </summary>
    private void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    /// <summary>
    /// Detiene todos los sonidos
    /// </summary>
    public void StopAllSounds()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }
}