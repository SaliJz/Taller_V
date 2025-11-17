using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class SettingsPanel : MonoBehaviour
{
    public enum PanelDisplayType { AnimatedScale, Static, CanvasFade }

    #region [ References ]

    [Header("Panel Display Settings")]
    public PanelDisplayType displayType = PanelDisplayType.AnimatedScale;

    [Header("Canvas Group Reference")]
    [SerializeField] private CanvasGroup canvasGroup;

    [SerializeField] private AudioMixer masterMixer;
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("DOTween Settings")]
    [SerializeField] private float openCloseDuration = 0.2f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;

    [Header("Scale Animation Vectors")]
    [SerializeField] private Vector3 startScale = Vector3.zero;
    [SerializeField] private Vector3 endScale = Vector3.one;

    [Header("Focus Control")]
    [SerializeField] private GameObject firstSelectedButton;

    private const string MasterVolumeParam = "MasterVolume";
    private const string MusicVolumeParam = "MusicVolume";
    private const string SfxVolumeParam = "SfxVolume";

    #endregion

    #region [ Unity Methods ]

    private void Awake()
    {
        SubscribeToSliders();
    }

    private void Start()
    {
        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.localScale = startScale;
        }
        else if (displayType == PanelDisplayType.CanvasFade && canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
        LoadSettings();
    }

    private void OnDestroy()
    {
        UnsubscribeFromSliders();
    }

    #endregion

    #region [ DOTween Animation & State ]

    public void OpenPanel()
    {
        gameObject.SetActive(true);

        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.localScale = startScale;
            transform.DOScale(endScale, openCloseDuration)
                .SetEase(openEase)
                .SetUpdate(true)
                .OnComplete(() => SetInitialFocus());
        }
        else if (displayType == PanelDisplayType.CanvasFade && canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;

            canvasGroup.DOFade(1f, openCloseDuration)
                .SetEase(Ease.OutSine)
                .SetUpdate(true)
                .OnComplete(() => SetInitialFocus());

            transform.DOLocalMove(Vector3.zero, openCloseDuration).SetEase(Ease.OutSine).SetUpdate(true);
        }
        else
        {
            SetInitialFocus();
        }
    }

    public void ClosePanel()
    {
        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.DOScale(startScale, openCloseDuration)
                .SetEase(closeEase)
                .SetUpdate(true)
                .OnComplete(() => gameObject.SetActive(false));
        }
        else if (displayType == PanelDisplayType.CanvasFade && canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;

            canvasGroup.DOFade(0f, openCloseDuration)
                .SetEase(Ease.InSine)
                .SetUpdate(true)
                .OnComplete(() => gameObject.SetActive(false));
        }
        else
        {
            gameObject.SetActive(false);
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    #endregion

    #region [ Sliders Subscription ]
    private void SubscribeToSliders()
    {
        masterSlider?.onValueChanged.AddListener(SetMasterVolume);
        musicSlider?.onValueChanged.AddListener(SetMusicVolume);
        sfxSlider?.onValueChanged.AddListener(SetSfxVolume);
    }
    private void UnsubscribeFromSliders()
    {
        masterSlider?.onValueChanged.RemoveListener(SetMasterVolume);
        musicSlider?.onValueChanged.RemoveListener(SetMusicVolume);
        sfxSlider?.onValueChanged.RemoveListener(SetSfxVolume);
    }
    #endregion
    #region [ Volume Control ]
    private float LinearToDecibel(float linearValue)
    {
        linearValue = Mathf.Clamp01(linearValue);
        if (linearValue <= 0.0001f)
        {
            return -80f;
        }
        return 20f * Mathf.Log10(linearValue);
    }
    public void SetMasterVolume(float sliderValue) => SetVolume(MasterVolumeParam, sliderValue);
    public void SetMusicVolume(float sliderValue) => SetVolume(MusicVolumeParam, sliderValue);
    public void SetSfxVolume(float sliderValue) => SetVolume(SfxVolumeParam, sliderValue);
    private void SetVolume(string exposedParamName, float sliderValue)
    {
        if (masterMixer == null) return;
        float dbVolume = LinearToDecibel(sliderValue);
        masterMixer.SetFloat(exposedParamName, dbVolume);
        PlayerPrefs.SetFloat(exposedParamName, sliderValue);
        PlayerPrefs.Save();
    }
    #endregion
    #region [ Initialization and Load ]
    private void LoadSettings()
    {
        LoadVolume(masterSlider, MasterVolumeParam);
        LoadVolume(musicSlider, MusicVolumeParam);
        LoadVolume(sfxSlider, SfxVolumeParam);
    }
    private void LoadVolume(Slider slider, string paramName)
    {
        if (slider == null) return;
        float savedVolume = PlayerPrefs.GetFloat(paramName, 0.75f);
        slider.value = savedVolume;
        SetVolume(paramName, savedVolume);
    }
    #endregion
    #region [ Gamepad Focus Control ]
    private void SetInitialFocus()
    {
        if (firstSelectedButton == null) return;

        if (Gamepad.current == null) return;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        ResetInputDevices();
        EventSystem.current.SetSelectedGameObject(firstSelectedButton);
    }
    private void ResetInputDevices()
    {
        if (Gamepad.current != null)
        {
            InputSystem.ResetDevice(Gamepad.current);
        }
        if (Keyboard.current != null)
        {
            InputSystem.ResetDevice(Keyboard.current);
        }
        if (Mouse.current != null)
        {
            InputSystem.ResetDevice(Mouse.current);
        }
    }
    #endregion
}