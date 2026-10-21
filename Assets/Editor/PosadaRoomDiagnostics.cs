#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in observations only: never creates managers or changes tutorial/story state.</summary>
[InitializeOnLoad]
internal static class PosadaRoomDiagnostics
{
    private const string Menu = "Tools/ALAALA/PosadaRoom Diagnostics/Enable Trace";
    private const string EnabledKey = "ALAALA.PosadaRoomDiagnostics.Enabled";
    private static readonly Dictionary<SceneEntrance, Action> entrances = new Dictionary<SceneEntrance, Action>();
    private static bool tracking;
    private static double nextSample;
    private static string managers, presentation;

    private static bool Enabled => SessionState.GetBool(EnabledKey, false);

    static PosadaRoomDiagnostics()
    {
        SceneManager.sceneLoaded += Loaded;
        SceneManager.sceneUnloaded += Unloaded;
        SceneManager.activeSceneChanged += ActiveSceneChanged;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        EditorApplication.update += Sample;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        SessionStoryState.FlagChanged += FlagChanged;
        if (Enabled && EditorApplication.isPlaying && HasPosada()) Begin("Observer restored after assembly reload");
    }

    [MenuItem(Menu)]
    private static void Toggle()
    {
        SessionState.SetBool(EnabledKey, !Enabled);
        if (Enabled && EditorApplication.isPlaying && HasPosada()) Begin("Trace enabled");
        else if (!Enabled) End();
        Debug.Log("[PosadaTrace] " + (Enabled ? "Enabled for manual PosadaRoom testing." : "Disabled."));
    }

    [MenuItem(Menu, true)]
    private static bool ValidateToggle() { UnityEditor.Menu.SetChecked(Menu, Enabled); return true; }

    private static bool HasPosada()
    {
        for (int i = 0; i < SceneManager.sceneCount; ++i)
            if (SceneManager.GetSceneAt(i).name == "PosadaRoom") return true;
        return false;
    }

    private static void Begin(string reason)
    {
        tracking = true; managers = presentation = null; nextSample = 0;
        // SessionStoryState resets its delegates on Play entry; rebind without changing any flags.
        SessionStoryState.FlagChanged -= FlagChanged;
        SessionStoryState.FlagChanged += FlagChanged;
        BindEntrances();
        Log(reason);
        Capture();
    }

    private static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (!Enabled || !EditorApplication.isPlaying) return;
        if (scene.name == "PosadaRoom") Begin("Scene LOADED: " + scene.path + ", handle=" + scene.handle + ", mode=" + mode);
        else if (tracking)
        {
            Log("Destination LOADED: " + scene.path + ", handle=" + scene.handle + ", mode=" + mode);
            Capture();
            if (!HasPosada()) End();
        }
    }

    private static void Unloaded(Scene scene)
    {
        if (!Enabled || !tracking) return;
        Log("Scene UNLOADED: " + scene.path + ", handle=" + scene.handle);
        UnbindEntrances();
    }

    private static void ActiveSceneChanged(Scene previous, Scene next)
    {
        if (Enabled && tracking) Log("Active scene: " + previous.name + " -> " + next.name);
    }

    private static void PlayModeChanged(PlayModeStateChange state)
    {
        if (Enabled && state == PlayModeStateChange.EnteredPlayMode && HasPosada()) Begin("Entered Play Mode in PosadaRoom");
        if (state != PlayModeStateChange.ExitingPlayMode && state != PlayModeStateChange.EnteredEditMode) return;
        if (Enabled && tracking) Log("Editor lifecycle: " + state);
        End();
    }

    private static void BeforeReload()
    {
        if (Enabled && tracking) Log("Editor ASSEMBLY RELOAD beginning; distinguish this from a scene load.");
        End();
    }

    private static void End() { UnbindEntrances(); tracking = false; managers = presentation = null; }

    private static void Sample()
    {
        if (!Enabled || !tracking || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextSample) return;
        nextSample = EditorApplication.timeSinceStartup + .5;
        Capture(); // Poll twice per second; emit only changed state, never frame-by-frame logs.
    }

    private static IEnumerable<T> Live<T>() where T : Component => Resources.FindObjectsOfTypeAll<T>()
        .Where(c => c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded);

    private static void Capture()
    {
        var tutorialManagers = Live<GameplaySystemTutorialManager>().ToArray();
        var observed = tutorialManagers.Cast<Component>().Concat(Live<StorySequenceCoordinator>())
            .Concat(Live<TaskManager>()).Concat(Live<InventoryManager>()).ToArray();
        string currentManagers = string.Join("; ", observed.Select(c => c.GetType().Name + "#" + c.GetEntityId() +
            "@" + c.gameObject.scene.name + ", active=" + c.gameObject.activeInHierarchy).OrderBy(s => s));
        if (currentManagers != managers)
        {
            Log("Manager instances changed (observed creation/removal or activation): " + currentManagers);
            managers = currentManagers;
        }
        string currentPresentation = string.Join("; ", tutorialManagers.Select(m => "manager#" + m.GetEntityId() +
            ", enabled=" + m.isActiveAndEnabled + ", activeTutorial=" + m.HasActiveTutorial +
            ", system=" + m.ActiveSystem + ", visible=" + (m.HasActiveTutorial && m.IsPresentationVisible) +
            ", journalStep=" + m.JournalStep)) + "; storyOwners=" + StorySequenceCoordinator.ActiveOwnerCount +
            ", conversation=" + (DialogueEditor.ConversationManager.Instance != null && DialogueEditor.ConversationManager.Instance.IsConversationActive) +
            ", paused=" + (Time.timeScale <= 0f) + ", spawnRoute=" + SpawnData.spawnPointName +
            ", seen[J/I/C]=" + GameplaySystemTutorialState.IsSeen(GameplaySystemId.Journal) + "/" +
            GameplaySystemTutorialState.IsSeen(GameplaySystemId.Inventory) + "/" + GameplaySystemTutorialState.IsSeen(GameplaySystemId.Clarity);
        if (currentPresentation != presentation) { Log("Tutorial/ownership state changed: " + currentPresentation); presentation = currentPresentation; }
    }

    private static void FlagChanged(string flag, bool value)
    {
        if (!Enabled || !tracking) return;
        if (flag.IndexOf("tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
            flag == "journal_unlocked" || flag == "inventory_unlocked" || flag == "clarity_unlocked")
            Log("Flag changed: " + flag + "=" + value);
    }

    private static void BindEntrances()
    {
        UnbindEntrances();
        foreach (var entrance in Live<SceneEntrance>().Where(e => e.gameObject.scene.name == "PosadaRoom"))
        {
            var owner = entrance;
            Action callback = () =>
            {
                if (!Enabled || !tracking || owner == null) return;
                var serialized = new SerializedObject(owner);
                Log("Transition REQUEST accepted: " + owner.name + " -> " + serialized.FindProperty("sceneName").stringValue);
            };
            entrances.Add(owner, callback);
            owner.EntranceAccepted += callback;
        }
    }

    private static void UnbindEntrances()
    {
        foreach (var pair in entrances) if (pair.Key != null) pair.Key.EntranceAccepted -= pair.Value;
        entrances.Clear();
    }

    private static void Log(string message) => Debug.Log("[PosadaTrace t=" + Time.realtimeSinceStartup.ToString("F1") +
        " scene=" + SceneManager.GetActiveScene().name + "] " + message);
}
#endif
