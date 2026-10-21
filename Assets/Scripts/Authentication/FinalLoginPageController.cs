using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Alaala.Authentication
{
    [DisallowMultipleComponent]
    public sealed class FinalLoginPageController : MonoBehaviour
    {
        public const string MainMenuScenePath = "Assets/Scenes/SceneMenu.unity";
        public const string UnavailableMessage = "Pansamantalang hindi magamit\nang serbisyo. Subukan muli.";

        [SerializeField] private GameObject loginPanel, registrationPanel;
        [SerializeField] private TMP_Text subtitle, feedbackText;
        [SerializeField] private TMP_InputField loginUsername, loginPassword;
        [SerializeField] private TMP_InputField registrationUsername, registrationEmail, registrationPassword, confirmationPassword;
        [SerializeField] private Button loginButton, showRegistrationButton, registrationButton, backButton;
        [Header("Registration confirmation")]
        [SerializeField] private GameObject registrationConfirmationPopup;
        [SerializeField] private TMP_Text registrationConfirmationUsername;
        [SerializeField] private Button confirmRegistrationButton, cancelRegistrationButton;
        [Header("Password display")]
        [SerializeField] private PasswordVisibilityToggle loginPasswordVisibility, registrationPasswordVisibility, confirmationPasswordVisibility;
        private bool busy, registering, navigating;
        private bool awaitingRegistrationConfirmation;
        private string pendingRegistrationUsername;
        private CancellationTokenSource pending;
        private IAuthenticationService pendingService;
        private int generation;

        private void OnEnable()
        {
            loginButton.onClick.AddListener(SubmitLogin);
            showRegistrationButton.onClick.AddListener(ShowRegistration);
            registrationButton.onClick.AddListener(SubmitRegistration);
            backButton.onClick.AddListener(ShowLogin);
            loginPassword.onSubmit.AddListener(SubmitLoginFromField);
            confirmationPassword.onSubmit.AddListener(SubmitRegistrationFromField);
            if (confirmRegistrationButton != null) confirmRegistrationButton.onClick.AddListener(ConfirmRegistration);
            if (cancelRegistrationButton != null) cancelRegistrationButton.onClick.AddListener(CancelRegistrationConfirmation);
            HideRegistrationConfirmation();
            SetBusy(false);
            ShowLogin();
        }

        private void OnDisable()
        {
            generation++;
            pending?.Cancel();
            // A completed provider callback may be awaiting its UI continuation. Reject that login too.
            if (!navigating && pending != null) pendingService?.Logout();
            loginButton.onClick.RemoveListener(SubmitLogin);
            showRegistrationButton.onClick.RemoveListener(ShowRegistration);
            registrationButton.onClick.RemoveListener(SubmitRegistration);
            backButton.onClick.RemoveListener(ShowLogin);
            loginPassword.onSubmit.RemoveListener(SubmitLoginFromField);
            confirmationPassword.onSubmit.RemoveListener(SubmitRegistrationFromField);
            if (confirmRegistrationButton != null) confirmRegistrationButton.onClick.RemoveListener(ConfirmRegistration);
            if (cancelRegistrationButton != null) cancelRegistrationButton.onClick.RemoveListener(CancelRegistrationConfirmation);
            HideRegistrationConfirmation();
            ClearPasswords();
        }

        private void ShowLogin() => ShowMode(false);
        private void ShowRegistration() => ShowMode(true);
        private void ShowMode(bool registration)
        {
            if (busy || awaitingRegistrationConfirmation) return;
            registering = registration;
            ClearPasswords();
            loginPanel.SetActive(!registration);
            registrationPanel.SetActive(registration);
            subtitle.text = registration ? "Magparehistro" : "Mabuhay!";
            feedbackText.text = string.Empty;
            (registration ? registrationUsername : loginUsername).Select();
        }

        private void SubmitLoginFromField(string _) => SubmitLogin();
        private void SubmitRegistrationFromField(string _) => SubmitRegistration();
        private async void SubmitLogin() => await SubmitAsync(false);
        private void SubmitRegistration()
        {
            if (busy || navigating || awaitingRegistrationConfirmation || !registering || !isActiveAndEnabled) return;
            if (!ValidateRegistrationForConfirmation()) return;
            pendingRegistrationUsername = registrationUsername.text.Trim();
            registrationConfirmationUsername.text = "Pangalan: " + pendingRegistrationUsername;
            feedbackText.text = string.Empty;
            awaitingRegistrationConfirmation = true;
            registrationConfirmationPopup.SetActive(true);
            SetBusy(false); // Lock the form behind the modal without starting a request.
            cancelRegistrationButton.Select();
        }

        private bool ValidateRegistrationForConfirmation()
        {
            var validation = AuthenticationValidation.Register(registrationUsername.text.Trim(), registrationEmail.text.Trim(),
                registrationPassword.text, confirmationPassword.text);
            if (validation.IsValid) return true;
            HideRegistrationConfirmation();
            SetBusy(false);
            feedbackText.text = validation.Message;
            FocusInvalidField(validation.Field, true);
            return false;
        }

        private async void ConfirmRegistration()
        {
            if (!awaitingRegistrationConfirmation || busy || navigating || !registering || !isActiveAndEnabled) return;
            if (!ValidateRegistrationForConfirmation()) return;
            string username = registrationUsername.text.Trim();
            if (!string.Equals(username, pendingRegistrationUsername, StringComparison.Ordinal))
            {
                // Even programmatic edits require confirmation of the newly displayed name.
                pendingRegistrationUsername = username;
                registrationConfirmationUsername.text = "Pangalan: " + username;
                return;
            }
            HideRegistrationConfirmation();
            await SubmitAsync(true, true);
        }

        private void CancelRegistrationConfirmation()
        {
            if (!awaitingRegistrationConfirmation || busy || !isActiveAndEnabled) return;
            HideRegistrationConfirmation();
            SetBusy(false);
            registrationButton.Select();
        }

        private void HideRegistrationConfirmation()
        {
            awaitingRegistrationConfirmation = false;
            pendingRegistrationUsername = null;
            if (registrationConfirmationUsername != null) registrationConfirmationUsername.text = string.Empty;
            if (registrationConfirmationPopup != null) registrationConfirmationPopup.SetActive(false);
        }

        private async Task SubmitAsync(bool registration, bool confirmedRegistration = false)
        {
            if (busy || navigating || awaitingRegistrationConfirmation || !isActiveAndEnabled || registering != registration ||
                (registration && !confirmedRegistration)) return;
            string username = (registration ? registrationUsername : loginUsername).text.Trim();
            string password = (registration ? registrationPassword : loginPassword).text;
            string email = registration ? registrationEmail.text.Trim() : string.Empty;
            var validation = registration
                ? AuthenticationValidation.Register(username, email, password, confirmationPassword.text)
                : AuthenticationValidation.Login(username, password);
            if (!validation.IsValid)
            {
                feedbackText.text = validation.Message;
                FocusInvalidField(validation.Field, registration);
                return;
            }

            // Set before awaiting; pointer/keyboard submissions share this guard.
            SetBusy(true);
            feedbackText.text = "Nag-uugnay...";
            ClearPasswords();
            var cancellation = new CancellationTokenSource();
            pending = cancellation;
            int requestGeneration = ++generation;
            IAuthenticationService service = AuthenticationServices.Current;
            pendingService = service;
            try
            {
                AuthenticationResult result = registration
                    ? await service.RegisterAsync(username, email, password, cancellation.Token)
                    : await service.LoginAsync(username, password, cancellation.Token);
                if (cancellation.IsCancellationRequested || this == null || !isActiveAndEnabled || requestGeneration != generation) return;
                if (!ReferenceEquals(service, AuthenticationServices.Current)) return;
                if (registration && result != null && result.Status == AuthenticationStatus.Registered)
                {
                    ClearPasswords();
                    SetBusy(false);
                    loginUsername.SetTextWithoutNotify(username);
                    ShowLogin();
                    feedbackText.text = "Matagumpay ang pagpaparehistro.";
                }
                // Registration cannot enter this login-only navigation branch.
                else if (!registration && result != null && result.Status == AuthenticationStatus.Success && result.Session != null &&
                    ReferenceEquals(service, AuthenticationServices.Current) && ReferenceEquals(service.Session, result.Session))
                    HandleAuthenticated(result.Session);
                else
                    feedbackText.text = Feedback(result?.Status ?? AuthenticationStatus.ServiceUnavailable);
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                // Do not print backend exception messages: they may contain request credentials.
                if (this != null && isActiveAndEnabled && requestGeneration == generation)
                    feedbackText.text = UnavailableMessage;
            }
            finally
            {
                password = null;
                if (ReferenceEquals(pending, cancellation)) { pending = null; pendingService = null; }
                cancellation.Dispose();
                if (this != null && requestGeneration == generation)
                {
                    ClearPasswords();
                    SetBusy(navigating);
                }
            }
        }

        private void HandleAuthenticated(AuthenticationSession session)
        {
            if (session == null || !ReferenceEquals(AuthenticationServices.Current.Session, session)) return;
            if (!Application.CanStreamedLevelBeLoaded(MainMenuScenePath))
            { feedbackText.text = "Hindi mabuksan ang pangunahing menu."; return; }
            // No NewGame call, save reset, or progression mutation at the authentication boundary.
            navigating = true;
            try
            {
                if (SceneManager.LoadSceneAsync(MainMenuScenePath, LoadSceneMode.Single) == null)
                    throw new InvalidOperationException();
            }
            catch (Exception)
            {
                navigating = false;
                feedbackText.text = "Hindi mabuksan ang pangunahing menu.";
            }
        }

        private static string Feedback(AuthenticationStatus status)
        {
            switch (status)
            {
                case AuthenticationStatus.InvalidCredentials: return "Mali ang pangalan o hudyat.";
                case AuthenticationStatus.UsernameAlreadyTaken: return "Gamit na ang pangalan o e-liham.";
                case AuthenticationStatus.InvalidEmail: return "Hindi wasto ang e-liham.";
                case AuthenticationStatus.InvalidInput: return "Suriin ang mga inilagay mong detalye.";
                case AuthenticationStatus.NetworkError: return "Hindi makakonekta.\nSubukan muli.";
                default: return UnavailableMessage;
            }
        }

        private void FocusInvalidField(AuthenticationField field, bool registration)
        {
            TMP_InputField input;
            switch (field)
            {
                case AuthenticationField.Email: input = registrationEmail; break;
                case AuthenticationField.Password: input = registration ? registrationPassword : loginPassword; break;
                case AuthenticationField.Confirmation: input = confirmationPassword; break;
                default: input = registration ? registrationUsername : loginUsername; break;
            }
            input.Select();
            input.ActivateInputField();
        }

        private void ClearPasswords()
        {
            if (loginPasswordVisibility != null) loginPasswordVisibility.HidePassword();
            if (registrationPasswordVisibility != null) registrationPasswordVisibility.HidePassword();
            if (confirmationPasswordVisibility != null) confirmationPasswordVisibility.HidePassword();
            loginPassword.SetTextWithoutNotify(string.Empty);
            registrationPassword.SetTextWithoutNotify(string.Empty);
            confirmationPassword.SetTextWithoutNotify(string.Empty);
        }

        private void SetBusy(bool value)
        {
            busy = value;
            bool allowFormInput = !value && !awaitingRegistrationConfirmation;
            loginButton.interactable = showRegistrationButton.interactable = registrationButton.interactable = backButton.interactable = allowFormInput;
            loginUsername.interactable = loginPassword.interactable = registrationUsername.interactable = registrationEmail.interactable =
                registrationPassword.interactable = confirmationPassword.interactable = allowFormInput;
            if (confirmRegistrationButton != null) confirmRegistrationButton.interactable = awaitingRegistrationConfirmation && !value;
            if (cancelRegistrationButton != null) cancelRegistrationButton.interactable = awaitingRegistrationConfirmation && !value;
            if (loginPasswordVisibility != null) loginPasswordVisibility.SetInteractable(allowFormInput);
            if (registrationPasswordVisibility != null) registrationPasswordVisibility.SetInteractable(allowFormInput);
            if (confirmationPasswordVisibility != null) confirmationPasswordVisibility.SetInteractable(allowFormInput);
        }
    }
}
