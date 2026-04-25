using UnityEngine;

public class AnimEventRelay : MonoBehaviour
{
    [SerializeField] private StaticEnemy enemy;

    public void HandleAnimEvents(string eventName)
    {
        enemy?.HandleAnimEvents(eventName);
    }
}