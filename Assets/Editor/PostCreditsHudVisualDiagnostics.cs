#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Explicit, read-only captures. No runtime hooks, UI writes or manager creation.</summary>
internal static class PostCreditsHudVisualDiagnostics
{
    private const string Menu = "Tools/ALAALA/Post-Credits HUD/";
    private const string Folder = "Library/PostCreditsHudDiagnostics";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Serializable] private sealed class CaptureData
    {
        public string stage, utc, context;
        public List<Entry> entries = new List<Entry>();
    }
    [Serializable] private sealed class Entry
    {
        public string path, state;
        public int instanceId;
    }

    [MenuItem(Menu + "Capture A - After Epilogue")]
    private static void CaptureA() => Capture("A");

    [MenuItem(Menu + "Capture B - After Normal Reentry")]
    private static void CaptureB() => Capture("B");

    [MenuItem(Menu + "Capture A - After Epilogue", true)]
    [MenuItem(Menu + "Capture B - After Normal Reentry", true)]
    private static bool CanCapture() => EditorApplication.isPlaying &&
        SceneManager.GetActiveScene().name == "MaestroBenHouse" &&
        SessionStoryState.GetFlag("alaala_epilogue_complete") &&
        !StorySequenceCoordinator.IsStorySequenceActive;

    private static void Capture(string stage)
    {
        if (!CanCapture()) return;
        // Observe entire HUD canvases so sibling shortcut labels and layout parents
        // are included, not just icon descendants. Include inactive objects/duplicates.
        var roots = new HashSet<Transform>();
        foreach (var view in UnityEngine.Object.FindObjectsByType<GameplaySystemLockView>(FindObjectsInactive.Include))
        {
            if (view.gameObject.scene.name != "MaestroBenHouse") continue;
            var canvas = view.GetComponentInParent<Canvas>(true);
            roots.Add(canvas != null ? canvas.rootCanvas.transform : view.transform);
        }
        foreach (var rect in UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include))
        {
            if (rect.gameObject.scene.name != "MaestroBenHouse" ||
                (rect.name != "InventoryButton" && rect.name != "ClarityIcon")) continue;
            var canvas = rect.GetComponentInParent<Canvas>(true);
            roots.Add(canvas != null ? canvas.rootCanvas.transform : rect);
        }
        if (roots.Count == 0) { Debug.LogWarning("[PostCreditsHUD] No affected HUD roots found; no capture written."); return; }
        var data = new CaptureData { stage = stage, utc = DateTime.UtcNow.ToString("O"), context = Context() };
        var transforms = new HashSet<Transform>();
        foreach (var root in roots)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) transforms.Add(child);
            for (var parent = root.parent; parent != null; parent = parent.parent) transforms.Add(parent);
        }
        foreach (var target in transforms.OrderBy(Path, StringComparer.Ordinal))
            data.entries.Add(new Entry { path = Path(target), instanceId = target.GetInstanceID(), state = Observe(target) });
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/" + stage + ".json", JsonUtility.ToJson(data, true));
        Debug.Log("[PostCreditsHUD] Read-only capture " + stage + ": " + System.IO.Path.GetFullPath(Folder + "/" + stage + ".json"));
        if (stage == "B" && File.Exists(Folder + "/A.json")) Compare();
    }

    private static string Observe(Transform target)
    {
        var b = new StringBuilder();
        b.AppendLine("activeSelf=" + target.gameObject.activeSelf + "; activeInHierarchy=" + target.gameObject.activeInHierarchy);
        b.AppendLine("parent=" + Path(target.parent) + "; localPosition=" + target.localPosition.ToString("R") +
            "; localScale=" + target.localScale.ToString("R") + "; lossyScale=" + target.lossyScale.ToString("R") +
            "; localRotation=" + target.localRotation.ToString("R"));
        if (target is RectTransform r)
            b.AppendLine("anchors=" + r.anchorMin.ToString("R") + "/" + r.anchorMax.ToString("R") +
                "; pivot=" + r.pivot.ToString("R") + "; anchoredPosition=" + r.anchoredPosition3D.ToString("R") +
                "; sizeDelta=" + r.sizeDelta.ToString("R") + "; rect=" + r.rect.ToString("R") +
                "; drivenBy=" + Ref(r.drivenByObject));
        var parentCanvas = target.GetComponentInParent<Canvas>(true);
        b.AppendLine("canvas=" + Ref(parentCanvas) + "; rootCanvas=" + Ref(parentCanvas != null ? parentCanvas.rootCanvas : null));
        float effectiveAlpha = 1f;
        for (Transform ancestor = target; ancestor != null; ancestor = ancestor.parent)
        {
            bool ignoreParents = false;
            foreach (var group in ancestor.GetComponents<CanvasGroup>())
            {
                b.AppendLine("group " + Path(ancestor) + ": enabled=" + group.enabled + "; alpha=" + F(group.alpha) +
                    "; ignoreParents=" + group.ignoreParentGroups + "; interactable=" + group.interactable + "; raycasts=" + group.blocksRaycasts);
                if (!group.enabled) continue;
                effectiveAlpha *= group.alpha;
                ignoreParents |= group.ignoreParentGroups;
            }
            if (ignoreParents) break;
        }
        b.AppendLine("effectiveGroupAlpha=" + F(effectiveAlpha));
        foreach (var c in target.GetComponents<Component>())
        {
            if (c == null) { b.AppendLine("Missing component"); continue; }
            if (c is Graphic graphic)
                b.AppendLine(c.GetType().Name + ": enabled=" + graphic.enabled + "; color=" + graphic.color.ToString("R") +
                    "; rendererColor=" + graphic.canvasRenderer.GetColor().ToString("R") +
                    "; inheritedAlpha=" + F(graphic.canvasRenderer.GetInheritedAlpha()) + "; culled=" + graphic.canvasRenderer.cull);
            if (c is Canvas || c is CanvasScaler || c is LayoutGroup || c is LayoutElement || c is ContentSizeFitter || c is AspectRatioFitter || c is Selectable)
                b.AppendLine(c.GetType().Name + "=" + EditorJsonUtility.ToJson(c));
            if (c is MonoBehaviour behaviour && c.GetType().Assembly == typeof(GameplayHUDTarget).Assembly)
                b.AppendLine(c.GetType().Name + ": enabled=" + behaviour.enabled + "; " + StateFields(c));
            if (c is Animator animator)
            {
                b.AppendLine("Animator: enabled=" + animator.enabled + "; initialized=" + animator.isInitialized + "; controller=" + Ref(animator.runtimeAnimatorController));
                if (animator.isInitialized && animator.runtimeAnimatorController != null)
                    for (int layer = 0; layer < animator.layerCount; layer++)
                        b.AppendLine("layer " + layer + ": hash=" + animator.GetCurrentAnimatorStateInfo(layer).fullPathHash +
                            "; time=" + F(animator.GetCurrentAnimatorStateInfo(layer).normalizedTime) +
                            "; transitioning=" + animator.IsInTransition(layer));
            }
            if (c is Animation animation)
                foreach (AnimationState state in animation)
                    b.AppendLine("Animation " + state.name + ": enabled=" + animation.enabled + "; playing=" + animation.IsPlaying(state.name) + "; time=" + F(state.time));
        }
        return b.ToString();
    }

    private static string Context()
    {
        var b = new StringBuilder("screen=" + Screen.width + "x" + Screen.height + "; timeScale=" + F(Time.timeScale) +
            "; storyOwners=" + StorySequenceCoordinator.ActiveOwnerCount + "\n");
        // Inspect existing instances only; singleton getters may create managers.
        foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).OrderBy(c => Path(c.transform), StringComparer.Ordinal))
        {
            string type = component.GetType().Name;
            if (type == "StorySequenceCoordinator" || type == "NewAlaalaPostCreditsEpilogue" || type == "PostCreditsEpilogueDevelopmentTesting" ||
                type == "NewMakamisaPart3PopulationPresentation" || type == "GameplaySystemTutorialManager" ||
                type == "InventoryUI" || type == "ReconstructionJournalManager" || type == "ClarityManager" ||
                type == "FadeController" || type == "TaskNotificationUI")
                b.AppendLine(type + " " + Path(component.transform) + ": " + StateFields(component));
        }
        foreach (var director in UnityEngine.Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include))
            b.AppendLine("Director " + Path(director.transform) + ": enabled=" + director.enabled + "; state=" + director.state +
                "; time=" + director.time.ToString("R", CultureInfo.InvariantCulture) + "; asset=" + Ref(director.playableAsset));
        return b.ToString();
    }

    private static string StateFields(object owner)
    {
        var b = new StringBuilder();
        foreach (var field in owner.GetType().GetFields(Fields).OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            object value = field.GetValue(owner);
            if (value == null || value is bool || value is string || value is Enum || value is float || value is int || value is Vector3 || value is Color)
                b.Append(field.Name).Append('=').Append(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null").Append("; ");
            else if (value is Coroutine) b.Append(field.Name).Append("=coroutine present; ");
            else if (value is UnityEngine.Object obj) b.Append(field.Name).Append('=').Append(Ref(obj)).Append("; ");
            else if (value is StorySequenceToken token) b.Append(field.Name).Append('=').Append(token.OwnerLabel).Append(" valid=").Append(token.IsValid).Append("; ");
            else if (value is IDictionary dictionary)
                foreach (DictionaryEntry entry in dictionary)
                    if (entry.Value is StorySequenceToken active) b.Append("owner=").Append(active.OwnerLabel).Append(" valid=").Append(active.IsValid).Append("; ");
        }
        return b.ToString();
    }

    [MenuItem(Menu + "Compare A and B")]
    private static void Compare()
    {
        if (!File.Exists(Folder + "/A.json") || !File.Exists(Folder + "/B.json"))
        { Debug.LogWarning("[PostCreditsHUD] Capture A and B first."); return; }
        var a = JsonUtility.FromJson<CaptureData>(File.ReadAllText(Folder + "/A.json"));
        var b = JsonUtility.FromJson<CaptureData>(File.ReadAllText(Folder + "/B.json"));
        var first = a.entries.ToDictionary(e => e.path);
        var second = b.entries.ToDictionary(e => e.path);
        var report = new StringBuilder("A=" + a.utc + "\nB=" + b.utc +
            "\nInstance IDs are recorded in JSON and excluded from equality. Animation times and coroutine presence are observations, not proof of ownership.\n\nA context:\n" + a.context + "\nB context:\n" + b.context);
        foreach (var path in first.Keys.Union(second.Keys).OrderBy(p => p, StringComparer.Ordinal))
        {
            first.TryGetValue(path, out var before); second.TryGetValue(path, out var after);
            if (before != null && after != null && before.state == after.state) continue;
            report.AppendLine("\nCHANGED " + path + "\nA:\n" + (before?.state ?? "MISSING") + "\nB:\n" + (after?.state ?? "MISSING"));
        }
        File.WriteAllText(Folder + "/Comparison.txt", report.ToString());
        Debug.Log("[PostCreditsHUD] Comparison: " + System.IO.Path.GetFullPath(Folder + "/Comparison.txt"));
    }

    private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Ref(UnityEngine.Object obj) => obj == null ? "null" : obj is Component component ?
        Path(component.transform) + " (" + component.GetType().Name + ")" : obj.name + " (" + obj.GetType().Name + ")";
    private static string Path(Transform target) => target == null ? "null" :
        (target.parent != null ? Path(target.parent) : target.gameObject.scene.name) + "/" + target.name + "[" + target.GetSiblingIndex() + "]";
}
#endif
