using UnityEngine;
using GameJolt.UI;
using UnityEngine.SceneManagement;

public class MainScene : MonoBehaviour
{
    private void Start()
    {
        GameJoltUI.Instance.ShowSignIn((success) =>
        {
            if (success)
            {
                SceneManager.LoadScene("MainMenu");
            }
            else
            {
                Debug.Log("Error");
            }
        });
    }
}