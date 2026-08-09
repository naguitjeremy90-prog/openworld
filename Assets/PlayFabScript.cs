using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class PlayFabEmailAuth : MonoBehaviour
{
    [Header("Panels")]
    public GameObject loginPanel;
    public GameObject registerPanel;

    [Header("Register Panel")]
    public TMP_InputField regUsernameInput;
    public TMP_InputField regEmailInput;
    public TMP_InputField regPasswordInput;
    public TMP_InputField regConfirmPasswordInput;
    public TMP_Text regStatusText;

    [Header("Login Panel")]
    public TMP_InputField loginEmailInput;
    public TMP_InputField loginPasswordInput;
    public TMP_Text loginStatusText;

    public float messageDuration = 3f;

    public void RegisterUser()
    {
        if (string.IsNullOrWhiteSpace(regUsernameInput.text) ||
            string.IsNullOrWhiteSpace(regEmailInput.text) ||
            string.IsNullOrWhiteSpace(regPasswordInput.text) ||
            string.IsNullOrWhiteSpace(regConfirmPasswordInput.text))
        {
            ShowRegisterMessage("Please fill in all fields.");
            return;
        }

        if (regPasswordInput.text != regConfirmPasswordInput.text)
        {
            ShowRegisterMessage("Password do not match.");
            return;
        }

        var request = new RegisterPlayFabUserRequest
        {
            Username = regUsernameInput.text,
            Email = regEmailInput.text,
            Password = regPasswordInput.text,
            RequireBothUsernameAndEmail = true
        };

        regStatusText.text = "Registering...";
        PlayFabClientAPI.RegisterPlayFabUser(request, OnRegisterSuccess, OnRegisterError);
    }

    void OnRegisterSuccess(RegisterPlayFabUserResult result)
    {
        ShowRegisterMessage("Registered successfully!");
        Debug.Log("User registered!");

        registerPanel.SetActive(false);
        loginPanel.SetActive(true);

        regUsernameInput.text = "";
        regEmailInput.text = "";
        regPasswordInput.text = "";
        regConfirmPasswordInput.text = "";
    }

    void OnRegisterError(PlayFabError error)
    {
        ShowRegisterMessage(error.ErrorMessage);
        Debug.LogError(error.GenerateErrorReport());
    }

    public void LoginUser()
    {
        if (string.IsNullOrWhiteSpace(loginEmailInput.text) ||
            string.IsNullOrWhiteSpace(loginPasswordInput.text))
        {
            ShowLoginMessage("Please enter email and password.");
            return;
        }

        var request = new LoginWithEmailAddressRequest
        {
            Email = loginEmailInput.text,
            Password = loginPasswordInput.text
        };

        loginStatusText.text = "Logging in...";
        PlayFabClientAPI.LoginWithEmailAddress(request, OnLoginSuccess, OnLoginError);
    }

    void OnLoginSuccess(LoginResult result)
    {
        loginStatusText.text = "Login successful!";
        Debug.Log("User logged in!");
        SceneManager.LoadScene("MainMenu");
    }

    void OnLoginError(PlayFabError error)
    {
        ShowLoginMessage(error.ErrorMessage);
        Debug.LogError(error.GenerateErrorReport());
    }

    void ShowRegisterMessage(string message)
    {
        StopCoroutine(nameof(ClearRegisterMessage));
        regStatusText.text = message;
        StartCoroutine(ClearRegisterMessage());
    }

    void ShowLoginMessage(string message)
    {
        StopCoroutine(nameof(ClearLoginMessage));
        loginStatusText.text = message;
        StartCoroutine(ClearLoginMessage());
    }

    IEnumerator ClearRegisterMessage()
    {
        yield return new WaitForSeconds(messageDuration);
        regStatusText.text = "";
    }

    IEnumerator ClearLoginMessage()
    {
        yield return new WaitForSeconds(messageDuration);
        loginStatusText.text = "";
    }
}