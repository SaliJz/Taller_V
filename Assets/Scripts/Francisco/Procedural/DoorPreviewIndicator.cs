using UnityEngine;
using TMPro;

public class DoorPreviewIndicator : MonoBehaviour
{
    [Header("Visual Elements")]
    [SerializeField] private MeshRenderer doorRenderer;
    [SerializeField] private Light doorLight;
    [SerializeField] private TextMeshPro roomTypeText;
    [SerializeField] private GameObject iconContainer;

    [Header("Room Type Colors")]
    [SerializeField] private Color combatColor = new Color(1f, 0.2f, 0.2f);
    [SerializeField] private Color shopColor = new Color(1f, 0.8f, 0f);
    [SerializeField] private Color treasureColor = new Color(0.5f, 0f, 1f);
    [SerializeField] private Color normalColor = new Color(0.3f, 0.7f, 1f);
    [SerializeField] private Color bossColor = new Color(0.8f, 0f, 0f);
    [SerializeField] private Color challengeColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color gachaponColor = new Color(1f, 0.3f, 0.8f);

    [Header("Room Type Icons")]
    [SerializeField] private GameObject combatIcon;
    [SerializeField] private GameObject shopIcon;
    [SerializeField] private GameObject treasureIcon;
    [SerializeField] private GameObject normalIcon;
    [SerializeField] private GameObject bossIcon;
    [SerializeField] private GameObject challengeIcon;
    [SerializeField] private GameObject gachaponIcon;

    [Header("Settings")]
    [SerializeField] private float emissionIntensity = 2f;
    [SerializeField] private bool showText = true;
    [SerializeField] private bool showIcon = true;

    private Material doorMaterial;

    private DungeonGenerator dungeonGenerator;
    private ConnectionPoint connectionPoint;

    void Awake()
    {
        if (doorRenderer != null)
        {
            doorMaterial = doorRenderer.material;
        }

        HideAllIcons();
    }

    void Start()
    {
        dungeonGenerator = FindAnyObjectByType<DungeonGenerator>();
        connectionPoint = GetComponentInParent<ConnectionPoint>();

        if (connectionPoint != null && connectionPoint.isConnected)
        {
            ClearPreview();
            return;
        }

        UpdateDoorPreview();
    }

    public void SetRoomType(RoomType roomType)
    {
        UpdateVisuals(roomType);
    }

    public void UpdateDoorPreview()
    {
        if (dungeonGenerator == null || connectionPoint == null || connectionPoint.isConnected)
        {
            ClearPreview();
            return;
        }

        RoomType predictedType = dungeonGenerator.PredictNextRoomType(connectionPoint);

        UpdateVisuals(predictedType);
    }

    private void UpdateVisuals(RoomType roomType)
    {
        Color roomColor = GetColorForRoomType(roomType);
        string roomName = GetNameForRoomType(roomType);

        if (doorMaterial != null)
        {
            doorMaterial.SetColor("_EmissionColor", roomColor * emissionIntensity);
            doorMaterial.EnableKeyword("_EMISSION");
        }

        if (doorLight != null)
        {
            doorLight.color = roomColor;
            doorLight.enabled = true;
        }

        if (showText && roomTypeText != null)
        {
            roomTypeText.text = roomName;
            roomTypeText.color = roomColor;
            roomTypeText.gameObject.SetActive(true);
        }

        if (showIcon)
        {
            HideAllIcons();
            GameObject icon = GetIconForRoomType(roomType);
            if (icon != null)
            {
                icon.SetActive(true);
            }
        }
    }

    public void ClearPreview()
    {
        if (doorMaterial != null)
        {
            doorMaterial.SetColor("_EmissionColor", Color.black);
            doorMaterial.DisableKeyword("_EMISSION");
        }

        if (doorLight != null)
        {
            doorLight.enabled = false;
        }

        if (roomTypeText != null)
        {
            roomTypeText.gameObject.SetActive(false);
        }

        HideAllIcons();
    }

    private Color GetColorForRoomType(RoomType roomType)
    {
        return roomType switch
        {
            RoomType.Combat => combatColor,
            RoomType.Shop => shopColor,
            RoomType.Treasure => treasureColor,
            RoomType.Boss => bossColor,
            RoomType.Challenge => challengeColor,
            RoomType.Gachapon => gachaponColor,
            RoomType.Normal => normalColor,
            _ => normalColor
        };
    }

    private string GetNameForRoomType(RoomType roomType)
    {
        return roomType switch
        {
            RoomType.Combat => "COMBATE",
            RoomType.Shop => "TIENDA",
            RoomType.Treasure => "TESORO",
            RoomType.Boss => "JEFE",
            RoomType.Challenge => "DESAFÍO",
            RoomType.Gachapon => "GACHAPON",
            RoomType.Normal => "NORMAL",
            _ => "???"
        };
    }

    private GameObject GetIconForRoomType(RoomType roomType)
    {
        return roomType switch
        {
            RoomType.Combat => combatIcon,
            RoomType.Shop => shopIcon,
            RoomType.Treasure => treasureIcon,
            RoomType.Boss => bossIcon,
            RoomType.Challenge => challengeIcon,
            RoomType.Gachapon => gachaponIcon,
            RoomType.Normal => normalIcon,
            _ => null
        };
    }

    private void HideAllIcons()
    {
        if (combatIcon != null) combatIcon.SetActive(false);
        if (shopIcon != null) shopIcon.SetActive(false);
        if (treasureIcon != null) treasureIcon.SetActive(false);
        if (normalIcon != null) normalIcon.SetActive(false);
        if (bossIcon != null) bossIcon.SetActive(false);
        if (challengeIcon != null) challengeIcon.SetActive(false);
        if (gachaponIcon != null) gachaponIcon.SetActive(false);

        if (iconContainer != null) iconContainer.SetActive(false);
    }
}