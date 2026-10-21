using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Alaala.Authentication
{
    [DisallowMultipleComponent]
    public sealed class PasswordVisibilityToggle : MonoBehaviour
    {
        [SerializeField] private TMP_InputField input;
        [SerializeField] private Button toggleButton;
        [SerializeField] private Image eye;
        [SerializeField] private Sprite showSprite;
        [SerializeField] private Sprite hideSprite;

        public bool IsPasswordVisible => input != null && input.contentType == TMP_InputField.ContentType.Standard;

        private void OnEnable()
        {
            if (toggleButton != null) toggleButton.onClick.AddListener(ToggleVisibility);
            HidePassword();
            SetInteractable(true);
        }

        private void OnDisable()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(ToggleVisibility);
            HidePassword();
        }

        public void ToggleVisibility()
        {
            if (!isActiveAndEnabled || input == null || !input.IsInteractable() ||
                toggleButton == null || !toggleButton.IsInteractable()) return;
            SetVisibility(!IsPasswordVisible);
            // Retain button focus: never activate the input or submit a form here.
        }

        public void HidePassword() => SetVisibility(false);

        private void SetVisibility(bool visible)
        {
            if (input == null) return;
            input.contentType = visible ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            input.ForceLabelUpdate();
            if (eye != null) eye.sprite = visible ? hideSprite : showSprite;
        }

        public void SetInteractable(bool value)
        {
            if (toggleButton == null) return;
            toggleButton.interactable = value && input != null && input.IsInteractable();
        }
    }
}
