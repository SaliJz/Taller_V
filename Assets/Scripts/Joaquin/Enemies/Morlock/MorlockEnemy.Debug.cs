using UnityEngine;
using UnityEngine.AI;

public partial class MorlockEnemy : MonoBehaviour
{
    #region Variables de Depuración

    [Header("Debug Options")]
    [SerializeField] private bool showDetailsOptions = false;
    [SerializeField] private float worldLabelMaxDistance = 40f; // no dibujar etiquetas más lejos
    [SerializeField] private float worldLabelReferenceDistance = 10f; // distancia de referencia para escala 1
    [SerializeField] private int uiAreaWidth = 380;
    [SerializeField] private int uiAreaHeight = 400;
    [SerializeField] private int uiPadding = 10;

    private Camera cachedCamera = null;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle worldBoxStyle;
    private GUIStyle worldTextStyle;

    #endregion

    #region Debug Gizmos & OnGUI

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, optimalAttackDistance);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.forward * 2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, patrolRadius);

        // Visualizar rangos de teletransporte
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, defensiveTeleportActivationRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, defensiveTeleportRange);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, evasiveTeleportRadiusMin);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, evasiveTeleportRadiusMax);
    }

    /// <summary>
    /// Asegura e inicializa los GUIStyles y la cámara cacheada.
    /// </summary>
    private void EnsureGuiStyles()
    {
        if (titleStyle != null) return;

        cachedCamera = Camera.main;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = Color.white }
        };

        worldBoxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white, background = Texture2D.blackTexture },
            padding = new RectOffset(6, 6, 3, 3)
        };

        worldTextStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            padding = new RectOffset(6, 6, 3, 3)
        };
    }

    /// <summary>
    /// Dibuja un label en el mundo con escalado por distancia y culling por distancia.
    /// </summary>
    private void DrawWorldLabel(Vector3 worldPos, string text)
    {
        if (cachedCamera == null) cachedCamera = Camera.main;
        var cam = cachedCamera;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        if (screenPos.z <= 0) return;

        if (screenPos.z > worldLabelMaxDistance) return;

        float scale = Mathf.Clamp(worldLabelReferenceDistance / (screenPos.z + 0.0001f), 0.5f, 1.6f);

        Vector2 size = worldTextStyle.CalcSize(new GUIContent(text));
        size *= scale;

        Vector2 guiPoint = new Vector2(screenPos.x, Screen.height - screenPos.y);
        Rect rect = new Rect(guiPoint.x - size.x * 0.5f, guiPoint.y - size.y - 8f, size.x + 8f, size.y + 6f);

        GUI.Box(rect, GUIContent.none, worldBoxStyle);
        GUI.Label(rect, text, worldTextStyle);
    }

    /// <summary>
    /// Devuelve un target de teletransporte útil para debug:
    /// - Si hay waypoints, devuelve la posición del siguiente waypoint.
    /// - Si no hay waypoints, intenta devolver un punto aleatorio válido sobre el NavMesh dentro de patrolRadius.
    /// - Si no se encuentra NavMesh, devuelve la propia posición del enemigo como fallback.
    /// </summary>
    private Vector3 GetDebugTeleportTarget()
    {
        Vector3 target = transform.position;

        // 1) Si hay waypoints, tomar el siguiente (útil para debugging de patrulla)
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            int nextIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
            if (patrolWaypoints[nextIndex] != null)
            {
                target = patrolWaypoints[nextIndex].position;
            }
        }
        else
        {
            // 2) Intentar obtener un punto aleatorio válido sobre NavMesh dentro de patrolRadius
            Vector3 randomPoint;
            if (TryGetRandomPoint(transform.position, patrolRadius, out randomPoint))
            {
                // samplear para asegurarnos que quede sobre NavMesh exactamente
                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomPoint, out hit, 1.5f, NavMesh.AllAreas))
                {
                    target = hit.position;
                }
                else
                {
                    target = randomPoint;
                }
            }
            else
            {
                // 3) Fallback: si no hay NavMesh o no se pudo generar punto, usar posición actual
                target = transform.position;
            }
        }

        return target;
    }

    /// <summary>
    /// OnGUI para debug. Usa GUILayout para panel de control y DrawWorldLabel para etiqueta en escena.
    /// </summary>
    private void OnGUI()
    {
        if (!showDetailsOptions) return;

#if !UNITY_EDITOR
        if (!Debug.isDebugBuild) return;
#endif

        EnsureGuiStyles();

        Rect area = new Rect(uiPadding, uiPadding, uiAreaWidth, uiAreaHeight);
        GUILayout.BeginArea(area, GUI.skin.box);

        GUILayout.Label("MORLOCK - DEBUG", titleStyle);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Estado:", labelStyle, GUILayout.Width(140));
        GUILayout.Label(currentState.ToString(), labelStyle);
        GUILayout.EndHorizontal();

        if (enemyHealth != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("HP:", labelStyle, GUILayout.Width(140));
            GUILayout.Label($"{enemyHealth.CurrentHealth:F1} / {enemyHealth.MaxHealth:F1}", labelStyle);
            GUILayout.EndHorizontal();
        }

        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Dist. a Jugador (m):", labelStyle, GUILayout.Width(140));
            GUILayout.Label($"{dist:F2}", labelStyle);
            GUILayout.EndHorizontal();

            float calculatedDamage = 0f;
            try
            {
                calculatedDamage = CalculateDamageByDistance(dist);
            }
            catch
            {
                calculatedDamage = 0f;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Daño actual (por dist):", labelStyle, GUILayout.Width(140));
            GUILayout.Label($"{calculatedDamage:F2}", labelStyle);
            GUILayout.EndHorizontal();
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Fase de persecución:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{pursuitPhaseCount}/3", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Pursuit Timer:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{pursuitTeleportTimer:F2} / {pursuitTeleportCooldown:F2}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Defensive Timer:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{defensiveTeleportTimer:F2} / {defensiveTeleportCooldown:F2}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Evasive Timer:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{evasiveTeleportTimer:F2} / {evasiveTeleportCooldown:F2}", labelStyle);
        GUILayout.EndHorizontal();

        // Fire rate info
        float timeToNextShot = Mathf.Max(0f, (1f / Mathf.Max(0.0001f, fireRate)) - fireTimer);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Siguiente disparo (s):", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{timeToNextShot:F2}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Proj. daño / vel:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{projectileDamage:F1}-{maxDamageIncrease:F1} / {projectileSpeed:F1}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Teleport Now", GUILayout.Height(26)))
        {
            Vector3 target = GetDebugTeleportTarget();
            if (teleportCoroutine != null) StopCoroutine(teleportCoroutine);
            teleportCoroutine = StartCoroutine(TeleportRoutine(target, currentState));
        }

        if (GUILayout.Button("Shoot Now", GUILayout.Height(26)))
        {
            Shoot();
            fireTimer = 0f;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Timers", GUILayout.Height(22)))
        {
            fireTimer = 0f;
            pursuitTeleportTimer = 0f;
            defensiveTeleportTimer = 0f;
            evasiveTeleportTimer = 0f;
            patrolIdleTimer = 0f;
        }
        if (GUILayout.Button("Reset Phase", GUILayout.Height(22)))
        {
            pursuitPhaseCount = 0;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Force Pursuit TP", GUILayout.Height(22)))
        {
            PerformPursuitTeleport();
        }
        if (GUILayout.Button("Force Defensive TP", GUILayout.Height(22)))
        {
            PerformDefensiveTeleport();
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Force Evasive TP", GUILayout.Height(22)))
        {
            PerformEvasiveTeleport();
        }

        if (GUILayout.Button("Kill (debug)", GUILayout.Height(22)))
        {
            if (enemyHealth != null) enemyHealth.TakeDamage(9999f);
        }

        GUILayout.EndArea();

        string worldText = $"Morlock\nState: {currentState}\nHP: {(enemyHealth != null ? enemyHealth.CurrentHealth.ToString("F0") : "N/A")}\nPhase: {pursuitPhaseCount}";
        DrawWorldLabel(transform.position + Vector3.up * 2.0f, worldText);
    }

    #endregion
}