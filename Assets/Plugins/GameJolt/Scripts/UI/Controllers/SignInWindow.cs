using GameJolt.API;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameJolt.UI.Controllers
{
    public class SignInWindow : BaseWindow
    {
        public InputField UsernameField;
        public InputField TokenField;
        public Text ErrorMessage;
        public Toggle RememberMeToggle;
        public Toggle ShowTokenToggle;

        private Action<bool> signedInCallback;
        private Action<bool> userFetchedCallback;

        private const int TROPHY_ID_LOGIN = 285303;

        public override void Show(Action<bool> callback)
        {
            Show(callback, null);
        }

        public void Show(Action<bool> signedInCallback, Action<bool> userFetchedCallback)
        {
            ErrorMessage.enabled = false;
            Animator.SetTrigger("SignIn");
            this.signedInCallback = signedInCallback;
            this.userFetchedCallback = userFetchedCallback;
            string username, token;
            RememberMeToggle.isOn = API.GameJoltAPI.Instance.GetStoredUserCredentials(out username, out token);
            UsernameField.text = username;
            TokenField.text = token;
            ShowTokenToggle.isOn = false;
        }

        public override void Dismiss(bool success)
        {
            Animator.SetTrigger("Dismiss");
            if (signedInCallback != null)
            {
                signedInCallback(success);
                signedInCallback = null;
            }
        }

        public void Submit()
        {
            ErrorMessage.enabled = false;

            if (UsernameField.text.Trim() == string.Empty || TokenField.text.Trim() == string.Empty)
            {
                ErrorMessage.text = "Empty username and/or token.";
                ErrorMessage.enabled = true;
            }
            else
            {
                Animator.SetTrigger("Lock");
                Animator.SetTrigger("ShowLoadingIndicator");

                var user = new API.Objects.User(UsernameField.text.Trim(), TokenField.text.Trim());
                user.SignIn(signInSuccess => {
                    if (signInSuccess)
                    {
                        if (GameJoltTrophy.Instance != null)
                        {
                            GameJoltTrophy.Instance.AwardLoginTrophy();
                            Dismiss(true);
                        }
                        else
                        {
                            Debug.LogError("[GAME JOLT] GameJoltTrophy Instance no encontrado.");
                            Dismiss(true);
                        }
                    }
                    else
                    {
                        ErrorMessage.text = "Wrong username and/or token.";
                        ErrorMessage.enabled = true;
                        Dismiss(false);
                    }

                    Animator.SetTrigger("HideLoadingIndicator");
                    Animator.SetTrigger("Unlock");
                }, userFetchedSuccess => {
                    if (userFetchedCallback != null)
                    {
                        // This will potentially be called after a user dismissed the window..
                        userFetchedCallback(userFetchedSuccess);
                        userFetchedCallback = null;
                    }
                }, RememberMeToggle.isOn);
            }
        }

        public void ShowToken(bool show)
        {
            TokenField.contentType = show ? InputField.ContentType.Standard : InputField.ContentType.Password;
            TokenField.ActivateInputField();
        }

        public void CreateAccount()
        {
            Application.OpenURL("https://gamejolt.com/join");
        }
    }
}