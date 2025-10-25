using UnityEngine;
using UnityEngine.AI;

public partial class MorlockEnemy : MonoBehaviour
{
    #region Variables de Depuración

    [Header("Debug Options")]
    [SerializeField] private bool showDetailsOptions = false;
    [SerializeField] private float worldLabelMaxDistance = 40f;
    [SerializeField] private float worldLabelReferenceDistance = 10f;
    [SerializeField] private int uiAreaWidth = 380;
    [SerializeField] private int uiAreaHeight = 400;
    [SerializeField] private int uiPadding = 10;

    private Camera cachedCamera = null;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle worldBoxStyle;
    private GUIStyle worldTextStyle;

    // Variables de debug adicionales
    private float lastTeleportTime = 0f;
    private float lastShootTime = 0f;
    private int teleportCount = 0;
    private int shootCount = 0;

    #endregion

    #region Debug Gizmos & OnGUI

    private void OnDrawGizmos()
    {
        // Radio de detección
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Radio de ataque
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Radio de patrulla
        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, patrolRadius);

        // Dirección hacia adelante
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);

        // Rangos específicos de Pursue2
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, p2_activationRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, p2_teleportRange);

        // Rangos de Pursue3
        //Gizmos.color = Color.white;
        //Gizmos.DrawWireSphere(transform.position, p3_teleportRange);

        // Dibujar waypoints si existen
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            Gizmos.color = Color.blue;
            for (int i = 0; i < patrolWaypoints.Length; i++)
            {
                if (patrolWaypoints[i] != null)
                {
                    Gizmos.DrawWireSphere(patrolWaypoints[i].position, 0.5f);

                    // Dibujar línea al siguiente waypoint
                    int nextIndex = (i + 1) % patrolWaypoints.Length;
                    if (patrolWaypoints[nextIndex] != null)
                    {
                        Gizmos.DrawLine(patrolWaypoints[i].position, patrolWaypoints[nextIndex].position);
                    }
                }
            }
        }

        // Dibujar línea hacia el jugador si existe
        if (playerTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, playerTransform.position);
        }
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
    /// Devuelve un target de teletransporte útil para debug.
    /// </summary>
    private Vector3 GetDebugTeleportTarget()
    {
        Vector3 target = transform.position;

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
            Vector3 randomPoint;
            if (TryGetRandomPoint(transform.position, patrolRadius, out randomPoint))
            {
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

        // Estado actual
        GUILayout.BeginHorizontal();
        GUILayout.Label("Estado:", labelStyle, GUILayout.Width(140));
        GUILayout.Label(currentState.ToString(), labelStyle);
        GUILayout.EndHorizontal();

        // Health
        if (enemyHealth != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("HP:", labelStyle, GUILayout.Width(140));
            GUILayout.Label($"{enemyHealth.CurrentHealth:F1} / {enemyHealth.MaxHealth:F1}", labelStyle);
            GUILayout.EndHorizontal();
        }

        // Distancia al jugador
        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Dist. a Jugador (m):", labelStyle, GUILayout.Width(140));
            GUILayout.Label($"{dist:F2}", labelStyle);
            GUILayout.EndHorizontal();

            // Daño calculado
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

        GUILayout.Space(4);

        // Estadísticas de teleport y disparo
        GUILayout.BeginHorizontal();
        GUILayout.Label("Teleports realizados:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{teleportCount}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Último teleport (s):", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{Time.time - lastTeleportTime:F2}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Disparos realizados:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{shootCount}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Último disparo (s):", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{Time.time - lastShootTime:F2}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // Información de proyectil
        GUILayout.BeginHorizontal();
        GUILayout.Label("Proj. daño / vel:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{projectileDamage:F1}-{maxDamageIncrease:F1} / {projectileSpeed:F1}", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Fire Rate:", labelStyle, GUILayout.Width(140));
        GUILayout.Label($"{fireRate:F2} disp/s", labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // Botones de control
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Teleport Now", GUILayout.Height(26)))
        {
            Vector3 target = GetDebugTeleportTarget();
            TeleportToPosition(target);
        }

        if (GUILayout.Button("Shoot Now", GUILayout.Height(26)))
        {
            Shoot();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Stats", GUILayout.Height(22)))
        {
            teleportCount = 0;
            shootCount = 0;
            lastTeleportTime = 0f;
            lastShootTime = 0f;
        }
        if (GUILayout.Button("Force Pursue1", GUILayout.Height(22)))
        {
            ChangeState(MorlockState.Pursue1);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Force Pursue2", GUILayout.Height(22)))
        {
            ChangeState(MorlockState.Pursue2);
        }
        if (GUILayout.Button("Force Pursue3", GUILayout.Height(22)))
        {
            ChangeState(MorlockState.Pursue3);
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Kill (debug)", GUILayout.Height(22)))
        {
            if (enemyHealth != null) enemyHealth.TakeDamage(9999f);
        }

        GUILayout.EndArea();

        // Label en el mundo
        string worldText = $"Morlock\nState: {currentState}\nHP: {(enemyHealth != null ? enemyHealth.CurrentHealth.ToString("F0") : "N/A")}\nTP: {teleportCount} | Shoot: {shootCount}";
        DrawWorldLabel(transform.position + Vector3.up * 2.0f, worldText);
    }

    /// <summary>
    /// Llama a este método desde TeleportRoutine para registrar teleports
    /// </summary>
    private void RegisterTeleportForDebug()
    {
        teleportCount++;
        lastTeleportTime = Time.time;
    }

    /// <summary>
    /// Llama a este método desde Shoot() para registrar disparos
    /// </summary>
    private void RegisterShootForDebug()
    {
        shootCount++;
        lastShootTime = Time.time;
    }

    #endregion
}