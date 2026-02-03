using UnityEngine;

public class SteppedAnimator : MonoBehaviour
{
    Animator anim;
    public float stepTime = 1/32f;
    float timer;

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        timer += Time.deltaTime;

        if(timer >= stepTime)
        {
            anim.Update(stepTime);
            timer = 0;
        }
    }
}
