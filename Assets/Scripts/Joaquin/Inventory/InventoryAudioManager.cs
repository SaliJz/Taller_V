using UnityEngine;

/// <summary>
/// Gestor de audio para el sistema de inventario
/// </summary>
public class InventoryAudioManager : MonoBehaviour
{
    public static InventoryAudioManager Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Sonidos Generales")]
    [SerializeField] private AudioClip inventoryOpenSound;
    [SerializeField] private AudioClip inventoryCloseSound;
    [SerializeField] private AudioClip itemHoverSound;
    [SerializeField] private AudioClip itemClickSound;
    [SerializeField] private AudioClip categoryScrollSound;

    [Header("Sonidos por Rareza")]
    [SerializeField] private AudioClip normalSound;
    [SerializeField] private AudioClip rareSound;
    [SerializeField] private AudioClip superRareSound;

    [Header("Volúmenes")]
    [SerializeField] private float hoverVolume = 0.5f;
    [SerializeField] private float clickVolume = 0.7f;
    [SerializeField] private float rarityVolume = 0.8f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    public void PlayOpenSound() => PlaySound(inventoryOpenSound, 1f);
    public void PlayCloseSound() => PlaySound(inventoryCloseSound, 1f);
    public void PlayHoverSound() => PlaySound(itemHoverSound, hoverVolume);
    public void PlayClickSound() => PlaySound(itemClickSound, clickVolume);
    public void PlayScrollSound() => PlaySound(categoryScrollSound, hoverVolume);

    public void PlayRaritySound(ItemRarity rarity)
    {
        AudioClip clip = rarity switch
        {
            ItemRarity.Raro => rareSound,
            ItemRarity.SuperRaro => superRareSound,
            _ => normalSound
        };
        PlaySound(clip, rarityVolume);
    }

    private void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }
}