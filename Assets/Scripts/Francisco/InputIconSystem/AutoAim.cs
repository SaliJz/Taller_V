using UnityEngine;
using UnityEngine.InputSystem;

public class AutoAim : MonoBehaviour
{
    [Header("Auto-Aim Settings")]
    [SerializeField] private bool enableAutoAim = true;
    [SerializeField] private bool onlyForGamepad = true;
    [SerializeField] private float autoAimRange = 25f;
    [SerializeField] private float autoAimAngle = 60f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private bool debugMode = false;

    [Header("Sticky Aim Settings")]
    [SerializeField] private bool enableStickyTarget = true;
    [SerializeField] private float stickyTargetDuration = 0.5f;

    [Header("Target FX Settings")]
    [SerializeField] private bool showTargetFX = true;
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private float arrowDistance = 1.0f;
    [SerializeField] private float arrowOffset = 1.0f;
    [SerializeField] private Color arrowColor = new Color(1f, 0f, 0f, 0.8f);
    [SerializeField] private float arrowSize = 0.2f;
    [SerializeField] private bool animateFX = true;
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float pulseScale = 1.2f;
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private bool billboardToCamera = true;

    private Transform currentTarget;
    private float lastTargetTime;
    private GameObject[] targetArrows = new GameObject[4];
    private bool fxInitialized = false;
    private Camera mainCamera;
    private GamepadPointer gamepadPointer;
    private bool originalShowTargetFX;

    public bool EnableAutoAim
    {
        get => enableAutoAim;
        set => enableAutoAim = value;
    }

    public bool OnlyForGamepad
    {
        get => onlyForGamepad;
        set => onlyForGamepad = value;
    }

    private void Start()
    {
        mainCamera = Camera.main;
        gamepadPointer = GamepadPointer.Instance;
        originalShowTargetFX = showTargetFX;

        if (showTargetFX)
        {
            InitializeTargetFX();
        }
    }

    private void Update()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        bool isGamepadActive = IsGamepadActiveDevice();

        bool shouldShowFX = originalShowTargetFX && enableAutoAim && (!onlyForGamepad || isGamepadActive);

        SetFXEnabled(shouldShowFX);

        Vector3 referenceForward = transform.forward;
        Vector3? gamepadAim = GetGamepadAimDirection();

        if (gamepadAim.HasValue)
        {
            referenceForward = gamepadAim.Value;
        }

        if (enableAutoAim)
        {
            FindBestTarget(transform.position, referenceForward, null);
        }

        if (showTargetFX && fxInitialized)
        {
            UpdateTargetFX();
        }
    }

    private bool IsGamepadActiveDevice()
    {
        if (gamepadPointer == null) return false;

        InputDevice activeDevice = gamepadPointer.GetCurrentActiveDevice();
        Gamepad gamepad = gamepadPointer.GetCurrentGamepad();

        return activeDevice != null && activeDevice == gamepad;
    }

    private Vector3? GetGamepadAimDirection()
    {
        if (gamepadPointer == null) return null;

        if (!onlyForGamepad || IsGamepadActiveDevice())
        {
            Vector2 aimValue2D = gamepadPointer.GetAimDirectionValue();

            if (aimValue2D.magnitude > 0)
            {
                Vector3 aimDirection = new Vector3(aimValue2D.x, 0f, aimValue2D.y).normalized;
                return aimDirection;
            }
        }
        return null;
    }

    public Transform FindBestTarget(Vector3 playerPosition, Vector3 playerForward, Vector3? aimDirection = null)
    {
        if (!enableAutoAim) return null;

        Collider[] potentialTargets = Physics.OverlapSphere(playerPosition, autoAimRange, enemyLayer);

        if (potentialTargets.Length == 0)
        {
            currentTarget = null;
            return null;
        }

        Transform bestTarget = null;
        float bestScore = float.MaxValue;

        Vector3 referenceDirection = GetGamepadAimDirection() ?? (aimDirection ?? playerForward);

        foreach (Collider targetCollider in potentialTargets)
        {
            if (targetCollider == null || !targetCollider.gameObject.activeInHierarchy)
                continue;

            Vector3 directionToTarget = (targetCollider.transform.position - playerPosition).normalized;
            directionToTarget.y = 0f;

            if (directionToTarget.sqrMagnitude < 0.0001f)
                continue;

            float angleToTarget = Vector3.Angle(referenceDirection, directionToTarget);

            if (angleToTarget > autoAimAngle)
            {
                continue;
            }

            if (!IsEnemyValid(targetCollider))
            {
                continue;
            }

            float distance = Vector3.Distance(playerPosition, targetCollider.transform.position);
            float normalizedDistance = distance / autoAimRange;
            float normalizedAngle = angleToTarget / autoAimAngle;
            float score = (normalizedDistance * 0.6f) + (normalizedAngle * 0.4f);

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = targetCollider.transform;
            }
        }

        if (bestTarget != null)
        {
            currentTarget = bestTarget;
            lastTargetTime = Time.time;
        }
        else
        {
            currentTarget = null;
        }

        return bestTarget;
    }

    private bool IsTargetValid(Transform target, Vector3 playerPosition, Vector3 playerForward, Vector3? aimDirection)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        if (!IsEnemyValid(target.GetComponent<Collider>()))
            return false;

        float distance = Vector3.Distance(playerPosition, target.position);
        if (distance > autoAimRange * 1.2f)
            return false;

        Vector3 directionToTarget = (target.position - playerPosition).normalized;
        directionToTarget.y = 0f;

        Vector3 referenceDirection = GetGamepadAimDirection() ?? (aimDirection ?? playerForward);
        float angleToTarget = Vector3.Angle(referenceDirection, directionToTarget);

        return angleToTarget <= autoAimAngle * 1.5f;
    }

    private bool IsEnemyValid(Collider enemyCollider)
    {
        if (enemyCollider == null) return false;

        IDamageable damageable = enemyCollider.GetComponent<IDamageable>();
        if (damageable == null) return false;

        EnemyHealth enemyHealth = enemyCollider.GetComponent<EnemyHealth>();
        if (enemyHealth != null && enemyHealth.IsDead)
        {
            return false;
        }

        return true;
    }

    public Vector3 GetAimDirection(Vector3 playerPosition, Vector3 playerForward, Vector3? manualAimDirection, out bool foundTarget)
    {
        Transform target = currentTarget;

        Vector3? gamepadAim = GetGamepadAimDirection();
        Vector3 effectiveAimDirection = gamepadAim ?? (manualAimDirection ?? playerForward);

        if (enableStickyTarget && target != null && Time.time - lastTargetTime < stickyTargetDuration)
        {
            if (IsTargetValid(target, playerPosition, playerForward, effectiveAimDirection))
            {
                Vector3 directionToTarget = (target.position - playerPosition).normalized;
                directionToTarget.y = 0f;
                foundTarget = true;
                ReportDebug($"Usando sticky target para disparo: {target.name}", 1);
                return directionToTarget;
            }
        }

        target = FindBestTarget(playerPosition, playerForward, effectiveAimDirection);

        if (target != null)
        {
            Vector3 directionToTarget = (target.position - playerPosition).normalized;
            directionToTarget.y = 0f;
            foundTarget = true;
            return directionToTarget;
        }

        foundTarget = false;
        return effectiveAimDirection;
    }

    public void ClearTarget()
    {
        currentTarget = null;
        HideTargetFX();
        ReportDebug("Objetivo limpiado", 1);
    }

    public Transform GetCurrentTarget()
    {
        if (currentTarget != null && currentTarget.gameObject.activeInHierarchy)
        {
            return currentTarget;
        }
        return null;
    }

    #region Target FX Methods

    private void InitializeTargetFX()
    {
        if (fxInitialized) return;

        for (int i = 0; i < 4; i++)
        {
            GameObject arrow = CreateArrow(i);
            targetArrows[i] = arrow;
            arrow.SetActive(false);
        }

        fxInitialized = true;
        ReportDebug("Sistema de FX de objetivo inicializado", 1);
    }

    private GameObject CreateArrow(int index)
    {
        GameObject arrow;

        if (arrowPrefab != null)
        {
            arrow = Instantiate(arrowPrefab);
        }
        else
        {
            arrow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(arrow.GetComponent<Collider>());

            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = arrowColor;
            arrow.GetComponent<Renderer>().material = mat;

            Texture2D arrowTex = CreateArrowTexture();
            mat.mainTexture = arrowTex;
        }

        arrow.name = $"TargetArrow_{index}";
        arrow.transform.SetParent(transform);
        arrow.transform.localScale = Vector3.one * arrowSize;

        return arrow;
    }

    private Texture2D CreateArrowTexture()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float centerX = size / 2f;
                float normalizedY = y / (float)size;
                float width = (1f - normalizedY) * centerX * 0.8f;

                if (Mathf.Abs(x - centerX) <= width)
                {
                    bool isEdge = Mathf.Abs(x - centerX) > width - 2f || y < 2 || y > size - 3;
                    pixels[y * size + x] = isEdge ? new Color(0.5f, 0f, 0f, 1f) : arrowColor;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Point;

        return texture;
    }

    private void UpdateTargetFX()
    {
        Transform target = GetCurrentTarget();
        bool shouldShow = target != null && showTargetFX;

        for (int i = 0; i < targetArrows.Length; i++)
        {
            if (targetArrows[i] != null)
            {
                targetArrows[i].SetActive(shouldShow);
            }
        }

        if (!shouldShow || mainCamera == null) return;

        Vector3 targetPos = target.position + Vector3.up * arrowOffset;
        Vector3 toTarget = targetPos - mainCamera.transform.position;
        float distanceToTarget = toTarget.magnitude;

        Vector3 camRight = mainCamera.transform.right;
        Vector3 camUp = mainCamera.transform.up;

        camRight.Normalize();
        camUp.Normalize();

        float screenScale = distanceToTarget * 0.05f;
        float effectiveDistance = arrowDistance * screenScale;

        Vector3[] offsets = new Vector3[]
        {
            camUp * effectiveDistance,
            camRight * effectiveDistance,
            -camUp * effectiveDistance,
            -camRight * effectiveDistance
        };

        float pulseValue = 1f;
        if (animateFX)
        {
            pulseValue = 1f + Mathf.Sin(Time.time * pulseSpeed) * (pulseScale - 1f);
        }

        for (int i = 0; i < targetArrows.Length; i++)
        {
            if (targetArrows[i] == null) continue;

            Vector3 arrowPos = targetPos + (offsets[i] * pulseValue);
            targetArrows[i].transform.position = arrowPos;

            Vector3 toCam = mainCamera.transform.position - arrowPos;
            targetArrows[i].transform.rotation = Quaternion.LookRotation(-toCam);

            if (animateFX)
            {
                float spinAngle = Time.time * rotationSpeed;
                targetArrows[i].transform.Rotate(0f, 0f, spinAngle, Space.Self);
            }

            float finalScale = arrowSize * pulseValue * screenScale;
            targetArrows[i].transform.localScale = Vector3.one * finalScale;
        }
    }

    private void HideTargetFX()
    {
        if (!fxInitialized) return;

        for (int i = 0; i < targetArrows.Length; i++)
        {
            if (targetArrows[i] != null)
            {
                targetArrows[i].SetActive(false);
            }
        }
    }

    public void SetArrowColor(Color color)
    {
        arrowColor = color;

        if (!fxInitialized) return;

        for (int i = 0; i < targetArrows.Length; i++)
        {
            if (targetArrows[i] != null)
            {
                Renderer renderer = targetArrows[i].GetComponent<Renderer>();
                if (renderer != null && renderer.material != null)
                {
                    renderer.material.color = color;
                }
            }
        }
    }

    public void SetFXEnabled(bool enabled)
    {
        if (enabled != showTargetFX)
        {
            showTargetFX = enabled;
        }

        if (!enabled)
        {
            HideTargetFX();
        }
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        if (!debugMode) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, autoAimRange);

        Vector3 forward = transform.forward;
        Vector3 rightBoundary = Quaternion.Euler(0, autoAimAngle, 0) * forward * autoAimRange;
        Vector3 leftBoundary = Quaternion.Euler(0, -autoAimAngle, 0) * forward * autoAimRange;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);

        if (currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.position);
            Gizmos.DrawWireSphere(currentTarget.position, 1f);

            if (showTargetFX)
            {
                Gizmos.color = Color.red;
                Vector3 targetPos = currentTarget.position + Vector3.up * arrowOffset;
                Gizmos.DrawWireSphere(targetPos + Vector3.forward * arrowDistance, 0.2f);
                Gizmos.DrawWireSphere(targetPos + Vector3.right * arrowDistance, 0.2f);
                Gizmos.DrawWireSphere(targetPos + Vector3.back * arrowDistance, 0.2f);
                Gizmos.DrawWireSphere(targetPos + Vector3.left * arrowDistance, 0.2f);
            }
        }
    }

    private void OnDestroy()
    {
        if (fxInitialized)
        {
            for (int i = 0; i < targetArrows.Length; i++)
            {
                if (targetArrows[i] != null)
                {
                    Destroy(targetArrows[i]);
                }
            }
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[ShieldAutoAim] {message}");
                break;
            case 2:
                Debug.LogWarning($"[ShieldAutoAim] {message}");
                break;
            case 3:
                Debug.LogError($"[ShieldAutoAim] {message}");
                break;
        }
    }
}