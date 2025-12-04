using UnityEngine;

public class Boss2AnimationEventHandler : MonoBehaviour
{
    [SerializeField] private BloodKnightBoss mainBossScript;

    private void Awake()
    {
        if (mainBossScript == null) mainBossScript = GetComponentInParent<BloodKnightBoss>();
    }

    public void AnimEvent_SlashImpact()
    {
        //if (mainBossScript != null) mainBossScript.AnimEvent_SlashImpact();
    }

    public void AnimEvent_SodomaImpact()
    {
        //if (mainBossScript != null) mainBossScript.AnimEvent_SodomaImpact();
    }
}