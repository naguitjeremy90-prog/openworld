using System;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Alaala.Saving
{
    public sealed class AlaalaContinueMenu : MonoBehaviour
    {
        private Button button;
        private TMP_Text feedback;
        private AlaalaSaveCatalog catalog;
        private Authentication.AuthenticationSession observedLogin;
        private int discoveryVersion;
        private bool redirecting, configured;
        internal static bool Production => Application.isPlaying && !Application.isEditor && !Debug.isDebugBuild;

        public static AlaalaContinueMenu Attach(MainMenuManager owner)
        {
            if (owner.gameObject.scene.path != AlaalaResumePolicy.MenuScene) return null;
            var view = owner.GetComponent<AlaalaContinueMenu>() ?? owner.gameObject.AddComponent<AlaalaContinueMenu>();
            view.Configure(owner); return view;
        }

        private void Configure(MainMenuManager owner)
        {
            if (configured) return;
            configured = true;
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include).Where(b => b.gameObject.scene == gameObject.scene &&
                Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i => b.onClick.GetPersistentTarget(i) == owner && b.onClick.GetPersistentMethodName(i) == "ContinueGame")).ToArray();
            if (buttons.Length == 1) button = buttons[0];
            foreach (var candidate in buttons) candidate.interactable = false;
            catalog = Resources.Load<AlaalaSaveCatalog>("SaveFoundation/AlaalaSaveCatalog");
            if (button != null)
            {
                var label = button.GetComponentInChildren<TMP_Text>(true);
                var canvas = button.GetComponentInParent<Canvas>();
                var root = new GameObject("SaveFeedback", typeof(RectTransform), typeof(TextMeshProUGUI));
                root.transform.SetParent(canvas.transform, false);
                feedback = root.GetComponent<TextMeshProUGUI>();
                if (label != null) { feedback.font = label.font; feedback.color = label.color; }
                feedback.fontSize = 20; feedback.alignment = TextAlignmentOptions.Center; feedback.raycastTarget = false;
                var rect = feedback.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
                rect.pivot = new Vector2(.5f, 0); rect.anchoredPosition = new Vector2(0, 20); rect.sizeDelta = new Vector2(1000, 65);
                Message("");
            }
        }

        private void Start() { observedLogin = Authentication.AuthenticationServices.Current.Session; _ = RefreshAsync(); }
        private void OnEnable() { AlaalaResumeCoordinator.Finished += OnRestoreFinished; }
        private void OnDisable() { discoveryVersion++; if (button != null) button.interactable = false; AlaalaResumeCoordinator.Finished -= OnRestoreFinished; }
        private void Update()
        {
            var login = Authentication.AuthenticationServices.Current.Session;
            if (!ReferenceEquals(login, observedLogin)) { observedLogin = login; _ = RefreshAsync(); }
            if (button != null && AlaalaResumeCoordinator.IsBusy) button.interactable = false;
        }
        private void OnRestoreFinished() { _ = RefreshAsync(); }
        public void Message(string message)
        { if (feedback != null) { feedback.text = message ?? ""; feedback.gameObject.SetActive(!string.IsNullOrEmpty(message)); } }

        private async Task RefreshAsync()
        {
            int version = ++discoveryVersion;
            if (button == null) return;
            button.interactable = false;
            if (AlaalaResumeCoordinator.IsBusy) return;
            if (!Production) { Message(""); return; }
            if (!AlaalaSaveOwner.TryGetAuthenticated(out var owner))
            {
                Message("Pumasok muna sa iyong account.");
                if (!redirecting) { redirecting = true; _ = SceneManager.LoadSceneAsync(AlaalaResumePolicy.LoginScene); }
                return;
            }
            var login = Authentication.AuthenticationServices.Current.Session;
            if (!AlaalaLocalSaveStore.TryOpenAuthenticated(catalog, out var store, out _)) { Message("Hindi mabuksan ang naitalang laro."); return; }
            Message("Sinusuri ang naitalang laro...");
            try
            {
                var result = await store.InspectSaveAvailabilityAsync();
                if (this == null || !isActiveAndEnabled || discoveryVersion != version || !ReferenceEquals(login, Authentication.AuthenticationServices.Current.Session)) return;
                bool available = AlaalaResumePolicy.CanOfferContinue(result, catalog, owner, out var text);
                if (available && !AlaalaResumePolicy.FreshMenu(out _))
                { available = false; text = "Buksan muli ang laro upang ligtas na maibalik ang naitalang sesyon."; }
                button.interactable = available;
                Message(AlaalaResumeCoordinator.LastFailureMessage ?? text);
            }
            catch (Exception)
            { if (this != null && isActiveAndEnabled && discoveryVersion == version && ReferenceEquals(login, Authentication.AuthenticationServices.Current.Session)) Message("Hindi masuri ang naitalang laro. Subukan muli."); }
        }

        public void Continue()
        {
            if (button == null || !button.interactable || AlaalaResumeCoordinator.IsBusy) return;
            button.interactable = false; discoveryVersion++;
            Message("Ibinabalik ang naitalang laro...");
            if (!AlaalaResumeCoordinator.TryBegin(this)) _ = RefreshAsync();
        }
    }
}
