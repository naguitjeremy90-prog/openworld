using TMPro;
using UnityEngine;

public class RegisterValidator : MonoBehaviour
{
    public TMP_InputField passwordInput;
    public TMP_InputField confirmPasswordInput;

    public bool PasswordsMatch()
    {
        return passwordInput.text == confirmPasswordInput.text;
    }

    public void Register()
    {
        if (!PasswordsMatch())
        {
            Debug.Log("Passwords do not match.");
            return;
        }

        Debug.Log("Registration successful.");
    }
}