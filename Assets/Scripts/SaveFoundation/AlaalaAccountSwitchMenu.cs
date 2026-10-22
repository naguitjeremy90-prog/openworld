using System;
using System.Threading.Tasks;
using Alaala.Authentication;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Alaala.Saving
{
    /// <summary>SceneMenu-only confirmation. Never saves, deletes or restores progress.</summary>
    public sealed class AlaalaAccountSwitchMenu : MonoBehaviour
    {
        private Button source, no, yes;
        private GameObject popup;
        private CanvasGroup menuGroup;
        private TMP_Text message;
        private bool wasInteractable, wasBlocking, switching;
        private AlaalaAccountSwitchOperation operation;
        private IAuthenticationService confirmationProvider;
        private AuthenticationSession confirmationSession;

        public static void Attach(MainMenuManager owner)
        {
            if (owner.gameObject.scene.path != AlaalaResumePolicy.MenuScene || owner.GetComponent<AlaalaAccountSwitchMenu>() != null) return;
            var view = owner.gameObject.AddComponent<AlaalaAccountSwitchMenu>();
            var canvas = Array.Find(owner.gameObject.scene.GetRootGameObjects(), x => x.name == "Canvas");
            if (canvas == null) return;
            view.source = canvas.transform.Find("MainMenuPanel/Logout")?.GetComponent<Button>();
            if (view.source == null) return;
            view.BuildPopup(canvas.transform);
        }

        // Uses this SceneMenu's actual settings panel, heading and Back button styles.
        private void BuildPopup(Transform canvas)
        {
            var shade = canvas.Find("SettingsPanel").GetComponent<Image>();
            var paper = canvas.Find("SettingsPanel/SettingsContent").GetComponent<Image>();
            var heading = Array.Find(paper.GetComponentsInChildren<TMP_Text>(true), x => x.name == "SettingsHeading");
            var back = Array.Find(paper.GetComponentsInChildren<Button>(true), x => x.name == "BackButton");
            var body = source.GetComponentInChildren<TMP_Text>(true);
            var buttonStyle = back != null ? back.GetComponentInChildren<TMP_Text>(true) : body;
            // A separate child group blocks mouse and keyboard on the original menu only
            // while the modal is open. Existing button navigation/appearance is untouched.
            menuGroup = source.transform.parent.GetComponent<CanvasGroup>();
            if (menuGroup == null) menuGroup = source.transform.parent.gameObject.AddComponent<CanvasGroup>();
            popup = Rect("AccountSwitchConfirmationPopup", canvas, Vector2.zero, Vector2.zero).gameObject;
            var overlay = popup.AddComponent<Image>(); CopyImage(shade, overlay);
            var full = (RectTransform)popup.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one; full.offsetMin = full.offsetMax = Vector2.zero;
            var panel = Rect("Panel", full, Vector2.zero, new Vector2(520, 260));
            CopyImage(paper, panel.gameObject.AddComponent<Image>());
            var title = Label("Title", panel, heading, "Kumpirmasyon", new Vector2(0, 85), new Vector2(460, 40));
            title.fontSize = heading.fontSize;
            message = Label("Message", panel, body, "Nais mo bang magpalit ng gumagamit?", new Vector2(0, 17), new Vector2(460, 80));
            message.color = heading.color;
            no = Choice("Hindi", panel, buttonStyle, new Vector2(-110, -80));
            yes = Choice("Oo", panel, buttonStyle, new Vector2(110, -80));
            var nav = no.navigation; nav.mode = Navigation.Mode.Explicit;
            nav.selectOnRight = nav.selectOnLeft = nav.selectOnUp = nav.selectOnDown = yes; no.navigation = nav;
            nav = yes.navigation; nav.mode = Navigation.Mode.Explicit;
            nav.selectOnRight = nav.selectOnLeft = nav.selectOnUp = nav.selectOnDown = no; yes.navigation = nav;
            no.onClick.AddListener(Cancel);
            yes.onClick.AddListener(Confirm);
            popup.SetActive(false);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static void CopyImage(Image from, Image to)
        { to.sprite = from.sprite; to.type = from.type; to.color = from.color; to.material = from.material; to.raycastTarget = true; }
        private static TMP_Text Label(string name, Transform parent, TMP_Text style, string text, Vector2 position, Vector2 size)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = style.font; label.fontSharedMaterial = style.fontSharedMaterial; label.fontSize = style.fontSize;
            label.fontStyle = style.fontStyle; label.color = style.color; label.alignment = TextAlignmentOptions.Center;
            label.text = text; label.raycastTarget = false; return label;
        }
        private static Button Choice(string text, Transform panel, TMP_Text style, Vector2 position)
        {
            var rect = Rect(text, panel, position, new Vector2(180, 48));
            var image = rect.gameObject.AddComponent<Image>(); image.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var label = Label("Text", rect, style, text, Vector2.zero, new Vector2(180, 48));
            // Match the settings Back button's text-only interaction treatment.
            button.targetGraphic = label;
            var original = style.GetComponentInParent<Button>();
            if (original != null) { button.colors = original.colors; button.transition = original.transition; }
            return button;
        }

        public void Show()
        {
            if (!isActiveAndEnabled || popup == null || popup.activeSelf || switching || AlaalaResumeCoordinator.IsBusy || AlaalaSaveFoundation.RestorationInProgress || StorySequenceCoordinator.IsStorySequenceActive) return;
            if (!Application.CanStreamedLevelBeLoaded(AlaalaResumePolicy.LoginScene))
            { GetComponent<AlaalaContinueMenu>()?.Message("Hindi mabuksan ang pahina ng pagpasok. Hindi binago ang iyong account."); return; }
            OpenPopup();
        }

        // Isolated UI construction/interaction can be checked in an Editor preview scene,
        // independently of the running player's build-scene cache.
        private void OpenPopup()
        {
            if (popup.activeSelf) return;
            wasInteractable = menuGroup.interactable; wasBlocking = menuGroup.blocksRaycasts;
            confirmationProvider = AuthenticationServices.Current; confirmationSession = confirmationProvider.Session;
            menuGroup.interactable = false; menuGroup.blocksRaycasts = false;
            popup.transform.SetAsLastSibling(); popup.SetActive(true); no.Select();
        }

        public void Cancel()
        {
            if (switching || popup == null || !popup.activeSelf) return;
            popup.SetActive(false); RestoreMenu(); source.Select();
            confirmationProvider = null; confirmationSession = null;
        }

        private async void Confirm()
        {
            if (!isActiveAndEnabled || switching || popup == null || !popup.activeSelf || AlaalaResumeCoordinator.IsBusy || AlaalaSaveFoundation.RestorationInProgress || StorySequenceCoordinator.IsStorySequenceActive) return;
            if (!ReferenceEquals(confirmationProvider, AuthenticationServices.Current) || !ReferenceEquals(confirmationSession, confirmationProvider.Session))
            { Cancel(); GetComponent<AlaalaContinueMenu>()?.Message("Nagbago ang account. Subukan muli ang Magpalit."); return; }
            switching = true; no.interactable = yes.interactable = false;
            var provider = AuthenticationServices.Current;
            operation = new AlaalaAccountSwitchOperation(
                () =>
                {
                    // Fence old callbacks before any reset can emit a UI-only event.
                    var menu = GetComponent<AlaalaContinueMenu>(); if (menu != null) menu.enabled = false;
                    AlaalaSaveRunContext.InvalidateRun();
                    AlaalaLocalAutosave.SuspendForAccountSwitch();
                    AlaalaResumeCoordinator.ClearAccountFailure();
                    AuthenticationServices.Logout(); // Existing provider increments generation and forgets SDK credentials.
                }, ClearRuntimeProgress, AlaalaLocalSaveStore.WaitForIdleAsync,
                () => this != null && isActiveAndEnabled && ReferenceEquals(provider, AuthenticationServices.Current) && provider.Session == null,
                async () =>
                {
                    var load = SceneManager.LoadSceneAsync(AlaalaResumePolicy.LoginScene);
                    if (load == null) throw new InvalidOperationException("Login scene load could not start.");
                    while (!load.isDone) await Task.Yield();
                });
            try
            {
                if (!await operation.ExecuteAsync() && this != null)
                    message.text = "Nagbago ang sesyon. Buksan muli ang laro upang ligtas na magpatuloy.";
            }
            catch (Exception exception)
            {
                // Keep the menu fenced after a partially completed logout. Never expose another
                // account to incomplete cleanup, and never repair this by starting/saving a game.
                if (this != null) message.text = "Hindi makabalik sa pagpasok. Buksan muli ang laro.";
                Debug.LogWarning("[AlaalaAccountSwitch] Could not finish switching users: " + exception.GetType().Name);
            }
        }

        private static void ClearRuntimeProgress()
        {
            if (GameplaySystemTutorialManager.HasInstance) GameplaySystemTutorialManager.Instance.ResetForNewGame();
            JournalEntryPresentationController.Instance?.ResetForNewGame();
            JournalHUDEntryAttention.Instance?.ResetForNewGame();
            TaskManager.Instance?.ResetForNewGame();
            SessionStoryState.ResetDataForNewGame();
            InventoryManager.Instance?.ClearOwnedItemsForNewGame();
            InventoryItemReadState.HydrateKnownItemsSilently(Array.Empty<string>());
            // ClearOwnedItems resets read keys using normal setters; discard those empty
            // metadata values as well, so the next account enters a genuinely fresh menu.
            SessionStoryState.ResetDataForNewGame();
            GameFlags.ResetForNewGame(); SleepInteraction.ResetForNewGame(); CameraFocusTrigger.ResetForNewGame();
            TaskStageTimelineWaypointSequence.ResetForNewGame();
            PlayerMovement.movementDone = false; TutorialState.movementDone = false; SpawnData.spawnPointName = "";
        }

        private void RestoreMenu()
        { if (menuGroup != null) { menuGroup.interactable = wasInteractable; menuGroup.blocksRaycasts = wasBlocking; } }
        private void OnDisable()
        { if (!switching && popup != null && popup.activeSelf) Cancel(); }
        private void OnDestroy()
        {
            if (!Application.isPlaying) return;
            // Scene-local popup is a sibling, not a persistent manager.
            if (popup != null) { if (!switching && popup.activeSelf) RestoreMenu(); Destroy(popup); }
        }
    }

    /// <summary>Single-use switch transaction; injected steps allow isolated Edit Mode checks.</summary>
    public sealed class AlaalaAccountSwitchOperation
    {
        private readonly Action revoke, clear;
        private readonly Func<Task> drain, navigate;
        private readonly Func<bool> stillCurrent;
        public bool Started { get; private set; }
        public AlaalaAccountSwitchOperation(Action revoke, Action clear, Func<Task> drain, Func<bool> stillCurrent, Func<Task> navigate)
        { this.revoke = revoke; this.clear = clear; this.drain = drain; this.stillCurrent = stillCurrent; this.navigate = navigate; }
        public async Task<bool> ExecuteAsync()
        {
            if (Started) return false;
            Started = true;
            revoke(); clear(); // No yield between revoking ownership and clearing account memory.
            await drain(); // Old transactions exit safely and clean only their temporary resources.
            if (!stillCurrent()) return false;
            await navigate(); return true;
        }
    }
}
