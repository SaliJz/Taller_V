using UnityEngine;

public class AnimationEventHandler : MonoBehaviour
{
    [SerializeField] private PlayerMeleeAttack playerMeleeAttack;

    public void CallAttackEndEvent()
    {
        if (playerMeleeAttack != null)
        {
            playerMeleeAttack.OnAttackAnimationEnd();
            Debug.Log("Anim Event: Llamada al Padre exitosa.");
        }
        else
        {
            Debug.LogError("Anim Event: ¡Falta la referencia a PlayerMeleeAttack!");
        }
    }
}