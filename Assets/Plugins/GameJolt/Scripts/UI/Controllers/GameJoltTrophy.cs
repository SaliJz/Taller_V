using GameJolt.API;
using UnityEngine;

namespace GameJolt.UI.Controllers  
{
    public class GameJoltTrophy : MonoBehaviour
    {
        public static GameJoltTrophy Instance { get; private set; }

        [Header("Game Jolt Trophy IDs")]
        private const int TROPHY_ID_LOGIN = 285303;     
        private const int TROPHY_ID_SHOPPING = 285315; 
        private const int TROPHY_ID_BOSS1 = 285317;     
        private const int TROPHY_ID_BOSS2 = 285318;     
        private const int TROPHY_ID_COMPLETION = 285316; 

        private const int SHOPPING_GOAL = 5; 
        private int m_itemsPurchasedCount = 0;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void UnlockTrophy(int trophyID, string trophyName)
        {
            if (GameJoltUI.Instance == null)
            {
                Debug.LogWarning($"[GAME JOLT] Game Jolt UI/API no está inicializado. No se puede desbloquear {trophyName}.");
                return;
            }

            Debug.Log($"[GAME JOLT] Intentando desbloquear {trophyName} (ID: {trophyID})");

            Trophies.Unlock(trophyID, (bool success) =>
            {
                if (success)
                {
                    Debug.Log($"[GAME JOLT] Trofeo {trophyID} ({trophyName}) desbloqueado exitosamente!");
                }
                else
                {
                    Debug.LogError($"[GAME JOLT] Fallo al desbloquear trofeo {trophyID} ({trophyName}).");
                }
            });
        }

        public void AwardLoginTrophy()
        {
            UnlockTrophy(TROPHY_ID_LOGIN, "Login Trophy");
        }

        public void TrackItemPurchase()
        {
            m_itemsPurchasedCount++;
            Debug.Log($"[GAME JOLT] Items Comprados: {m_itemsPurchasedCount}/{SHOPPING_GOAL}");

            if (m_itemsPurchasedCount == SHOPPING_GOAL)
            {
                UnlockTrophy(TROPHY_ID_SHOPPING, "Shopping Trophy");
            }
            else if (m_itemsPurchasedCount > SHOPPING_GOAL)
            {
                UnlockTrophy(TROPHY_ID_SHOPPING, "Shopping Trophy");
            }
        }

        public void AwardBoss1Trophy()
        {
            UnlockTrophy(TROPHY_ID_BOSS1, "Boss I Defeated");
        }

        public void AwardBoss2Trophy()
        {
            UnlockTrophy(TROPHY_ID_BOSS2, "Boss II Defeated");
        }

        public void AwardGameCompletionTrophy()
        {
            UnlockTrophy(TROPHY_ID_COMPLETION, "Game Completed");
        }
    }
}