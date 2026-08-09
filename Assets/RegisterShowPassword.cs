using TMPro;
using UnityEngine;

public class ShowRegisterPasswords : MonoBehaviour
{
    public TMP_InputField passwordInput;
    public TMP_InputField confirmPasswordInput;

    public void TogglePasswordVisibility(bool isVisible)
    {
        UpdateField(passwordInput, isVisible);
        UpdateField(confirmPasswordInput, isVisible);
    }

    private void UpdateField(TMP_InputField input, bool isVisible)
    {
        if (input == null) return;

        string currentText = input.text;

        input.inputType = isVisible
            ? TMP_InputField.InputType.Standard
            : TMP_InputField.InputType.Password;

        input.text = currentText;
        input.ForceLabelUpdate();
    }
}