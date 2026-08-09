using TMPro;
using UnityEngine;

public class ShowPassword : MonoBehaviour
{
    public TMP_InputField passwordInput;

    public void TogglePasswordVisibility(bool isVisible)
    {
        passwordInput.inputType = isVisible
            ? TMP_InputField.InputType.Standard
            : TMP_InputField.InputType.Password;

        string current = passwordInput.text;
        passwordInput.text = "";
        passwordInput.text = current;

        passwordInput.ForceLabelUpdate();
        passwordInput.ActivateInputField();
    }
}