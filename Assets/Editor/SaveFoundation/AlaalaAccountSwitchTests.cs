#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Alaala.Authentication;
using Alaala.Saving;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>No live logout, PlayFab requests, scene loads or player-save writes.</summary>
public static class AlaalaAccountSwitchTests
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static Task<string> running;
    public static string Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "STOP: Edit Mode only.";
        if (running != null && !running.IsCompleted) return "Already running.";
        running = Run(); return "Started isolated Magpalit checks and 183 save regression checks.";
    }
    public static string Status() => running == null ? "Not started." : !running.IsCompleted ? "Running." :
        running.IsFaulted ? "FAILED: " + running.Exception.GetBaseException() : running.Result;
    private static async Task<string> Run()
    {
        int count = 0;
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); count++; }
        var steps = new List<string>();
        var release = new TaskCompletionSource<bool>();
        bool current = true;
        var op = new AlaalaAccountSwitchOperation(() => steps.Add("revoke"), () => steps.Add("clear"),
            () => { steps.Add("drain"); return release.Task; }, () => current,
            () => { steps.Add("navigate"); return Task.CompletedTask; });
        var pending = op.ExecuteAsync();
        Check(op.Started && !pending.IsCompleted, "Switch waits for earlier storage operations");
        Check(string.Join(",", steps) == "revoke,clear,drain", "Ownership revoked and runtime cleared synchronously before await");
        Check(!await op.ExecuteAsync(), "Double Oo cannot admit a second logout");
        Check(!steps.Contains("navigate"), "Never load login before transaction drain");
        release.SetResult(true);
        Check(await pending && steps.Last() == "navigate", "Login follows completed cleanup/drain");
        Check(!await op.ExecuteAsync(), "A completed switch cannot run again");
        steps.Clear(); release = new TaskCompletionSource<bool>();
        op = new AlaalaAccountSwitchOperation(() => steps.Add("revoke"), () => steps.Add("clear"),
            () => release.Task, () => current, () => { steps.Add("navigate"); return Task.CompletedTask; });
        pending = op.ExecuteAsync(); current = false; release.SetResult(true);
        Check(!await pending && !steps.Contains("navigate"), "Superseded/destroyed UI cannot navigate after await");
        steps.Clear();
        op = new AlaalaAccountSwitchOperation(() => steps.Add("revoke"), () => throw new InvalidOperationException("fixture"),
            () => Task.CompletedTask, () => true, () => { steps.Add("navigate"); return Task.CompletedTask; });
        bool rejected = false;
        try { await op.ExecuteAsync(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && !steps.Contains("navigate"), "Cleanup failure cannot proceed with mixed account data");
        Check(!await op.ExecuteAsync(), "Failed cleanup remains fenced against duplicate execution");

        var sceneText = File.ReadAllText(AlaalaResumePolicy.MenuScene);
        Check(sceneText.Contains("m_MethodName: RequestSwitchUser") && !sceneText.Contains("m_MethodName: ExitGame"), "Saved Logout callback replaces Quit action");
        Check(sceneText.Contains("m_text: Magpalit"), "Developer caption preserved");
        Check(EditorBuildSettings.scenes.Any(x => x.enabled && x.path == AlaalaResumePolicy.LoginScene), "Existing FinalLoginPage available in build");
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var canvas = active.GetRootGameObjects().First(x => x.name == "Canvas").transform;
        var actualButton = canvas.Find("MainMenuPanel/Logout").GetComponent<Button>();
        Check(actualButton.transform.parent.GetComponentsInChildren<Button>(true).Length == 4, "Exactly four original main-menu buttons");
        var liveProvider = AuthenticationServices.Current; var login = liveProvider.Session;
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool wasDirty = active.isDirty;
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var fixture = new GameObject("SwitchFixture", typeof(RectTransform));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fixture, preview);
            var main = new GameObject("MainMenuPanel", typeof(RectTransform)); main.transform.SetParent(fixture.transform, false);
            var logout = UnityEngine.Object.Instantiate(actualButton.gameObject, main.transform).GetComponent<Button>();
            // Cloned events are never invoked. The fixture has no live MainMenuManager.
            logout.onClick = new Button.ButtonClickedEvent();
            UnityEngine.Object.Instantiate(canvas.Find("SettingsPanel").gameObject, fixture.transform).name = "SettingsPanel";
            var view = fixture.AddComponent<AlaalaAccountSwitchMenu>();
            typeof(AlaalaAccountSwitchMenu).GetField("source", Hidden).SetValue(view, logout);
            typeof(AlaalaAccountSwitchMenu).GetMethod("BuildPopup", Hidden).Invoke(view, new object[] { fixture.transform });
            var popup = fixture.transform.Find("AccountSwitchConfirmationPopup").gameObject;
            Check(!popup.activeSelf, "Confirmation starts closed");
            var panel = popup.transform.Find("Panel");
            var rect = panel.GetComponent<RectTransform>();
            Check(rect.anchorMin == new Vector2(.5f, .5f) && rect.anchoredPosition == Vector2.zero, "Popup is centered");
            Check(panel.Find("Title").GetComponent<TMP_Text>().text == "Kumpirmasyon", "Exact confirmation title");
            Check(panel.Find("Message").GetComponent<TMP_Text>().text == "Nais mo bang magpalit ng gumagamit?", "Exact confirmation message");
            var no = panel.Find("Hindi").GetComponent<Button>(); var yes = panel.Find("Oo").GetComponent<Button>();
            void Open() => typeof(AlaalaAccountSwitchMenu).GetMethod("OpenPopup", Hidden).Invoke(view, null);
            Check(no.GetComponentInChildren<TMP_Text>().text == "Hindi" && yes.GetComponentInChildren<TMP_Text>().text == "Oo", "Exact choices");
            Check(panel.GetComponent<Image>().color == canvas.Find("SettingsPanel/SettingsContent").GetComponent<Image>().color, "Actual settings paper appearance reused");
            Check(panel.Find("Title").GetComponent<TMP_Text>().font == canvas.Find("SettingsPanel/SettingsContent/SettingsHeading").GetComponent<TMP_Text>().font, "Actual SceneMenu heading font reused");
            Open();
            Check(popup.activeSelf && !main.GetComponent<CanvasGroup>().interactable && !main.GetComponent<CanvasGroup>().blocksRaycasts, "Modal blocks underlying keyboard and pointer interaction");
            Open();
            Check(fixture.GetComponentsInChildren<Transform>(true).Count(x => x.name == "AccountSwitchConfirmationPopup") == 1, "Repeated click creates no duplicate popup");
            no.onClick.Invoke();
            Check(!popup.activeSelf && main.GetComponent<CanvasGroup>().interactable && main.GetComponent<CanvasGroup>().blocksRaycasts, "Hindi closes and restores original menu interaction");
            Check(ReferenceEquals(liveProvider, AuthenticationServices.Current) && ReferenceEquals(login, liveProvider.Session), "Hindi leaves real authentication untouched");
            Open(); view.Cancel();
            Check(!popup.activeSelf, "Confirmation can be reopened after cancellation");
            Open();
            typeof(AlaalaAccountSwitchMenu).GetField("confirmationProvider", Hidden).SetValue(view, null);
            yes.onClick.Invoke();
            Check(!popup.activeSelf && ReferenceEquals(login, liveProvider.Session), "Stale confirmation cannot log out a different session");
            Check(typeof(AlaalaAccountSwitchMenu).GetField("operation", Hidden).GetValue(view) == null, "Stale Oo never starts cleanup/storage/navigation");
            view.enabled = false; view.Show();
            Check(!popup.activeSelf, "Disabled UI cannot open or submit account switching");
            view.enabled = true;
            Check(yes.onClick.GetPersistentEventCount() == 0 && no.onClick.GetPersistentEventCount() == 0, "No cloned persistent Quit/registration callbacks in popup");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selected);
        }
        Check(active.isDirty == wasDirty, "No scene asset dirtied by preview checks");
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Editor remains out of Play Mode");
        var regression = (Task<string>)typeof(AlaalaResumeTests).GetMethod("All", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        return count + " isolated Magpalit checks passed. " + await regression;
    }
}
#endif
