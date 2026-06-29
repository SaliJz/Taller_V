using UnityEngine;
using Steamworks;

#region Configuracion
public class SteamManager : MonoBehaviour
{
    private static SteamManager instance;
    private static AppId_t gameAppId = new AppId_t(4858720);
    private bool isInitialized;

    public static bool Initialized => instance != null && instance.isInitialized;
    #endregion

    #region Ciclo De Vida
    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (SteamAPI.RestartAppIfNecessary(gameAppId))
        {
            Application.Quit();
            return;
        }

        InitializeSteam();
    }

    private void Update()
    {
        if (isInitialized)
        {
            SteamAPI.RunCallbacks();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            if (isInitialized)
            {
                SteamAPI.Shutdown();
            }
            instance = null;
        }
    }
    #endregion

    #region Inicializacion
    private void InitializeSteam()
    {
        if (!Packsize.Test())
        {
            HandleInitFailure("Packsize Test failed");
            return;
        }

        if (!DllCheck.Test())
        {
            HandleInitFailure("DllCheck Test failed");
            return;
        }

        try
        {
            isInitialized = SteamAPI.Init();
        }
        catch (System.Exception e)
        {
            HandleInitFailure(e.Message);
            return;
        }

        if (!isInitialized)
        {
            HandleInitFailure("SteamAPI.Init() returned false");
        }
    }

    private void HandleInitFailure(string reason)
    {
        Debug.LogError($"[Steamworks] Init failed: {reason}");
        isInitialized = false;
    }
    #endregion
}