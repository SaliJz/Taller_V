#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SergioExperimentalRuntimeSmokeTest
{
    private const string ScenePath = "Assets/Scenes/Sergio/Scenes/ExperimentalScene2.unity";
    private const string EnemyName = "Sergio_ExperimentalWordEnemy";
    private const double MinValidationTime = 2.5d;
    private const double MaxValidationTime = 6d;

    private static double playModeStartTime;
    private static int? pendingExitCode;
    private static string pendingMessage;

    public static void Run()
    {
        pendingExitCode = null;
        pendingMessage = null;

        EditorApplication.update -= PollPlayMode;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("[SergioRuntimeSmokeTest] Escena experimental abierta. Entrando a Play Mode...");
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredPlayMode:
                playModeStartTime = EditorApplication.timeSinceStartup;
                EditorApplication.update -= PollPlayMode;
                EditorApplication.update += PollPlayMode;
                break;

            case PlayModeStateChange.EnteredEditMode:
                EditorApplication.update -= PollPlayMode;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

                if (pendingExitCode.HasValue)
                {
                    if (!string.IsNullOrEmpty(pendingMessage))
                    {
                        Debug.Log(pendingMessage);
                    }

                    EditorApplication.Exit(pendingExitCode.Value);
                    return;
                }

                Debug.LogError("[SergioRuntimeSmokeTest] Play Mode terminó sin un resultado explícito.");
                EditorApplication.Exit(1);
                break;
        }
    }

    private static void PollPlayMode()
    {
        double elapsed = EditorApplication.timeSinceStartup - playModeStartTime;

        if (elapsed < MinValidationTime)
        {
            return;
        }

        var enemy = GameObject.Find(EnemyName);
        if (enemy == null)
        {
            Finish(1, "[SergioRuntimeSmokeTest] No se encontró el enemigo experimental en runtime.");
            return;
        }

        var shooter = enemy.GetComponent<SergioExperimentalEnemyShooter>();
        var library = enemy.GetComponent<SergioExperimentalWordLibrary>();

        if (shooter == null || library == null)
        {
            Finish(1, "[SergioRuntimeSmokeTest] Faltan componentes principales en el enemigo experimental.");
            return;
        }

        var projectiles = Object.FindObjectsByType<SergioExperimentalProjectile>(FindObjectsSortMode.None);
        if (projectiles.Length == 0)
        {
            if (elapsed >= MaxValidationTime)
            {
                Finish(1, "[SergioRuntimeSmokeTest] No apareció ningún proyectil experimental en runtime.");
            }

            return;
        }

        SergioExperimentalProjectile projectile = projectiles[0];
        int letterObjects = projectile.transform.childCount;

        if (letterObjects > 0)
        {
            Finish(0, $"[SergioRuntimeSmokeTest] OK. Runtime validado con {projectiles.Length} proyectil(es) y {letterObjects} letra(s) visibles en el proyectil analizado.");
            return;
        }

        if (elapsed >= MaxValidationTime)
        {
            Finish(1, "[SergioRuntimeSmokeTest] El proyectil apareció, pero no generó letras visibles a tiempo.");
        }
    }

    private static void Finish(int exitCode, string message)
    {
        pendingExitCode = exitCode;
        pendingMessage = message;

        EditorApplication.update -= PollPlayMode;

        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
        }
        else
        {
            Debug.Log(message);
            EditorApplication.Exit(exitCode);
        }
    }
}
#endif
