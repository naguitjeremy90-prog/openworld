using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(GameplaySystemTutorialVisual))]
public sealed class GameplaySystemTutorialVisualEditor : Editor
{
    private const string PrefabPath = "Assets/Resources/UI/GameplaySystemTutorialVisual.prefab";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("LIVE POSITION TUNING", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "While the game is running, change the position offsets above while watching the Game View.\n\n" +
            "+X = Right   -X = Left\n+Y = Up   -Y = Down\n\n" +
            "When you like the position, use the matching Save button below. Play Mode changes are normally temporary.",
            MessageType.Info);
        EditorGUILayout.HelpBox(
            "If the panel stops moving, screen clamping may be limiting that axis. " +
            "Saving stores your OFFSET, not the final clamped screen position.", MessageType.Info);

        GameplaySystemTutorialVisual destination;
        string reason;
        bool canSave = TryGetDestination(out destination, out reason);
        if (!canSave)
            EditorGUILayout.HelpBox(reason, MessageType.Info);
        using (new EditorGUI.DisabledScope(!canSave))
        {
            if (GUILayout.Button("Save Journal Initial Target Offset To Prefab")) SaveOffset("initialTargetOffset");
            if (GUILayout.Button("Save Journal Overview Offset To Prefab")) SaveOffset("journalOverviewOffset");
            if (GUILayout.Button("Save Journal Guided Offset To Prefab")) SaveOffset("journalGuidedOffset");
            if (GUILayout.Button("Save Inventory Initial Target Offset To Prefab")) SaveOffset("inventoryInitialTargetOffset");
            if (GUILayout.Button("Save Inventory General Offset To Prefab")) SaveOffset("inventoryTutorialOffset");
            if (GUILayout.Button("Save Inventory Item Details Offset To Prefab")) SaveOffset("inventoryItemDetailsOffset");
            if (GUILayout.Button("Save Pagsusuri Initial Target Offset To Prefab")) SaveOffset("pagsusuriTutorialOffset");
            if (GUILayout.Button("Save Pagsusuri Document Offset To Prefab")) SaveOffset("pagsusuriDocumentOffset");
            EditorGUILayout.Space();
            if (GUILayout.Button("Save Inventory Item Gap To Prefab")) SaveOffset("inventoryItemGap");
            if (GUILayout.Button("Save Inventory Edge Margin To Prefab")) SaveOffset("inventoryEdgeMargin");
            if (GUILayout.Button("Save Pointer Below Item Offset To Prefab")) SaveOffset("pointerBelowItemOffset");
            if (GUILayout.Button("Save Pointer Above Item Offset To Prefab")) SaveOffset("pointerAboveItemOffset");
        }
    }

    private bool TryGetDestination(out GameplaySystemTutorialVisual destination, out string reason)
    {
        destination = null;
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            reason = "Enter Play Mode and select the runtime GameplaySystemTutorialVisual to save a tuned offset.";
            return false;
        }

        var runtime = target as GameplaySystemTutorialVisual;
        if (targets.Length != 1 || runtime == null || Selection.objects.Length != 1 ||
            (Selection.activeObject != runtime && Selection.activeObject != runtime.gameObject) ||
            EditorUtility.IsPersistent(runtime) || !runtime.gameObject.scene.IsValid() ||
            EditorSceneManager.IsPreviewScene(runtime.gameObject.scene) || !Application.IsPlaying(runtime.gameObject) ||
            runtime.transform.parent == null ||
            runtime.transform.parent.GetComponent<GameplaySystemTutorialManager>() == null)
        {
            reason = "Select only the runtime visual under DontDestroyOnLoad > GameplaySystemTutorialManager.";
            return false;
        }

        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == PrefabPath)
        {
            reason = "Close GameplaySystemTutorialVisual Prefab Mode before saving a live offset.";
            return false;
        }

        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (root == null || !File.Exists(PrefabPath))
        {
            reason = "The destination prefab is missing: " + PrefabPath;
            return false;
        }
        destination = root.GetComponent<GameplaySystemTutorialVisual>();
        if (destination == null || !EditorUtility.IsPersistent(destination) ||
            AssetDatabase.GetAssetPath(destination) != PrefabPath)
        {
            reason = "The destination prefab must contain GameplaySystemTutorialVisual on its root.";
            return false;
        }

        // Refuse to commit pending authoring edits anywhere in this prefab.
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (EditorUtility.IsDirty(child.gameObject))
            {
                reason = "The destination prefab has unsaved edits. Save or discard them yourself before retrying.";
                return false;
            }
            foreach (Component component in child.GetComponents<Component>())
            {
                if (component != null && EditorUtility.IsDirty(component))
                {
                    reason = "The destination prefab has unsaved edits. Save or discard them yourself before retrying.";
                    return false;
                }
            }
        }
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(PrefabPath))
        {
            if (asset != null && EditorUtility.IsDirty(asset))
            {
                reason = "The destination prefab has unsaved edits. Save or discard them yourself before retrying.";
                return false;
            }
        }
        try
        {
            if (!AssetDatabase.IsOpenForEdit(PrefabPath) ||
                (File.GetAttributes(PrefabPath) & FileAttributes.ReadOnly) != 0)
            {
                reason = "The destination prefab is read-only or locked. Make it editable before retrying.";
                return false;
            }
        }
        catch (Exception exception)
        {
            reason = "Cannot check the destination prefab: " + exception.Message;
            return false;
        }
        var source = new SerializedObject(destination);
        foreach (string field in new[] { "initialTargetOffset", "journalGuidedOffset", "journalOverviewOffset",
            "inventoryInitialTargetOffset", "inventoryTutorialOffset", "inventoryItemDetailsOffset",
            "pagsusuriTutorialOffset", "pagsusuriDocumentOffset", "pointerBelowItemOffset", "pointerAboveItemOffset" })
        {
            if (!IsVector2(source.FindProperty(field)))
            {
                reason = "The destination prefab is missing the Vector2 field: " + field;
                return false;
            }
        }
        foreach (string field in new[] { "inventoryItemGap", "inventoryEdgeMargin" })
        {
            if (!IsFloat(source.FindProperty(field)))
            {
                reason = "The destination prefab is missing the float field: " + field;
                return false;
            }
        }
        reason = null;
        return true;
    }

    private void SaveOffset(string field)
    {
        string label;
        string coverage;
        // This is the complete write allowlist; all other properties are refused.
        switch (field)
        {
            case "initialTargetOffset":
                label = "Journal Initial Target Offset";
                coverage = "Controls only the initial Journal HUD tutorial and Journal closed-window fallback.";
                break;
            case "journalGuidedOffset":
                label = "Journal Guided Offset";
                coverage = "Controls Journal guided steps 2–5:\n• Mga Napansin\n• Mga Tao\n• Mga Bahagi\n" +
                    "• Mga Pagninilay\n\nDoes not control the initial Journal explanation or overview step.";
                break;
            case "journalOverviewOffset":
                label = "Journal Overview Offset";
                coverage = "Controls only the Journal overview (step 1).";
                break;
            case "inventoryTutorialOffset":
                label = "Inventory General Offset";
                coverage = "Controls later Inventory steps 3–4:\n• Quest-item explanation\n" +
                    "• Important-item/document explanation\n\nDoes not control initial HUD prompts or item-specific placement.";
                break;
            case "pagsusuriTutorialOffset":
                label = "Pagsusuri Initial Target Offset";
                coverage = "Controls only ordinary Pagsusuri (Clarity step 0).";
                break;
            case "inventoryInitialTargetOffset":
                label = "Inventory Initial Target Offset";
                coverage = "Controls Inventory's initial HUD tutorial, second HUD prompt (step 2), and closed-window fallback.";
                break;
            case "inventoryItemDetailsOffset":
                label = "Inventory Item Details Offset";
                coverage = "Adds displacement to the automatic item-relative placement of Mga Detalye ng Gamit (Inventory step 1).";
                break;
            case "pagsusuriDocumentOffset":
                label = "Pagsusuri Document Offset";
                coverage = "Controls only Pagsusuri sa mga Kasulatan (Clarity step 1).";
                break;
            case "inventoryItemGap":
                label = "Inventory Item Gap";
                coverage = "Controls the item-to-panel gap and automatic above/below selection for Inventory step 1.";
                break;
            case "inventoryEdgeMargin":
                label = "Inventory Edge Margin";
                coverage = "Controls screen-edge clearance and automatic above/below selection for Inventory step 1.";
                break;
            case "pointerBelowItemOffset":
                label = "Pointer Below Item Offset";
                coverage = "Controls the pointer's local offset when the Inventory step 1 panel is below the item.";
                break;
            case "pointerAboveItemOffset":
                label = "Pointer Above Item Offset";
                coverage = "Controls the pointer's local offset when the Inventory step 1 panel is above the item.";
                break;
            default:
                ShowFailure("That property is not an approved tutorial positioning property.");
                return;
        }

        // Flush the focused field and pending Inspector edits before taking the value.
        GUI.FocusControl(null);
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
        GameplaySystemTutorialVisual destination;
        string reason;
        if (!TryGetDestination(out destination, out reason)) { ShowFailure(reason); return; }
        var runtimeProperty = serializedObject.FindProperty(field);
        var prefabObject = new SerializedObject(destination);
        var prefabProperty = prefabObject.FindProperty(field);
        bool isScalar = field == "inventoryItemGap" || field == "inventoryEdgeMargin";
        if (isScalar
            ? !IsFloat(runtimeProperty) || !IsFloat(prefabProperty)
            : !IsVector2(runtimeProperty) || !IsVector2(prefabProperty))
        {
            ShowFailure("The selected positioning property is missing or has the wrong type.");
            return;
        }
        Vector2 tuned = isScalar ? Vector2.zero : runtimeProperty.vector2Value;
        Vector2 previous = isScalar ? Vector2.zero : prefabProperty.vector2Value;
        float tunedScalar = isScalar ? runtimeProperty.floatValue : 0f;
        float previousScalar = isScalar ? prefabProperty.floatValue : 0f;
        if (isScalar ? float.IsNaN(tunedScalar) || float.IsInfinity(tunedScalar) || tunedScalar < 0f
            : float.IsNaN(tuned.x) || float.IsNaN(tuned.y) || float.IsInfinity(tuned.x) || float.IsInfinity(tuned.y))
        {
            ShowFailure(isScalar ? "Enter a finite, nonnegative value before saving." : "Enter finite X and Y values before saving.");
            return;
        }
        string previousDisplay = isScalar ? Format(previousScalar) : Format(previous);
        string tunedDisplay = isScalar ? Format(tunedScalar) : Format(tuned);
        byte[] before;
        try { before = File.ReadAllBytes(PrefabPath); }
        catch (Exception exception) { ShowFailure("Cannot read the destination: " + exception.Message); return; }
        if (!EditorUtility.DisplayDialog("Save " + label + "?",
            coverage + "\n\nDestination:\n" + PrefabPath + "\n\nPrevious prefab value: " + previousDisplay +
            "\nCurrent runtime value: " + tunedDisplay + "\n\nOnly this property will be saved.", "Save", "Cancel"))
            return;

        // Recheck after confirmation; never overwrite a changed destination.
        if (!TryGetDestination(out destination, out reason)) { ShowFailure(reason); return; }
        try
        {
            if (!SameBytes(before, File.ReadAllBytes(PrefabPath)))
            {
                ShowFailure("The prefab changed during confirmation. Review it and try again.");
                return;
            }
        }
        catch (Exception exception) { ShowFailure("Cannot recheck the destination: " + exception.Message); return; }
        prefabObject = new SerializedObject(destination);
        prefabProperty = prefabObject.FindProperty(field);
        if (isScalar
            ? !IsFloat(prefabProperty) || !prefabProperty.floatValue.Equals(previousScalar)
            : !IsVector2(prefabProperty) || !prefabProperty.vector2Value.Equals(previous))
        {
            ShowFailure("The prefab positioning property changed during confirmation. Review it and try again.");
            return;
        }
        if (isScalar ? previousScalar.Equals(tunedScalar) : previous.Equals(tuned))
        {
            EditorUtility.DisplayDialog("Value already saved", label + " already equals " + tunedDisplay + ".", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Save " + label);
        try
        {
            if (isScalar)
                prefabProperty.floatValue = tunedScalar;
            else
                prefabProperty.vector2Value = tuned;
            prefabObject.ApplyModifiedProperties(); // Records asset-field Undo; no runtime state is copied.
            bool saved;
            PrefabUtility.SavePrefabAsset(destination.gameObject, out saved);
            if (!saved) throw new IOException("Unity could not save the prefab asset.");
            byte[] after = File.ReadAllBytes(PrefabPath);
            if (!(isScalar ? OnlyFloatChanged(before, after, field, tunedScalar) : OnlyOffsetChanged(before, after, field, tuned)))
                throw new IOException("Unity introduced unrelated serialization changes. The save was refused.");
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("Saved " + label + " " + previousDisplay + " → " + tunedDisplay +
                " to GameplaySystemTutorialVisual.prefab.");
            EditorUtility.DisplayDialog("Value saved", label + "\n" + previousDisplay + " → " + tunedDisplay +
                "\nSaved to GameplaySystemTutorialVisual.prefab.", "OK");
        }
        catch (Exception exception)
        {
            try
            {
                Undo.RevertAllDownToGroup(undoGroup);
                // Restore exact original bytes if a failed Unity save wrote a partial/churned file.
                if (!File.Exists(PrefabPath) || !SameBytes(before, File.ReadAllBytes(PrefabPath)))
                {
                    File.WriteAllBytes(PrefabPath, before);
                    AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
                }
                ShowFailure(exception.Message + "\nThe original prefab positioning property/file was restored.");
            }
            catch (Exception rollback)
            {
                ShowFailure(exception.Message + "\nRestoration failed: " + rollback.Message +
                    "\nInspect the prefab before continuing.");
            }
        }
        finally { Undo.IncrementCurrentGroup(); }
    }

    private static bool IsVector2(SerializedProperty property) =>
        property != null && property.propertyType == SerializedPropertyType.Vector2;

    private static bool IsFloat(SerializedProperty property) =>
        property != null && property.propertyType == SerializedPropertyType.Float;

    private static string Format(float value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    private static string Format(Vector2 value) => "(" +
        value.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ", " +
        value.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ")";

    private static bool SameBytes(byte[] first, byte[] second)
    {
        if (first.Length != second.Length) return false;
        for (int i = 0; i < first.Length; i++) if (first[i] != second[i]) return false;
        return true;
    }

    private static bool OnlyOffsetChanged(byte[] before, byte[] after, string field, Vector2 expected)
    {
        // Keep every other byte-equivalent text value, including line endings and authored transforms.
        var pattern = new Regex(@"(?m)^  " + Regex.Escape(field) + @": \{x: (?<x>[^,\r\n}]+), y: (?<y>[^\r\n}]+)\}");
        string original = Encoding.UTF8.GetString(before);
        string saved = Encoding.UTF8.GetString(after);
        if (pattern.Matches(original).Count != 1 || pattern.Matches(saved).Count != 1 ||
            pattern.Replace(original, "<selected offset>") != pattern.Replace(saved, "<selected offset>"))
            return false;
        Match value = pattern.Match(saved);
        float x, y;
        return float.TryParse(value.Groups["x"].Value, System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out x) &&
               float.TryParse(value.Groups["y"].Value, System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out y) &&
               x.Equals(expected.x) && y.Equals(expected.y);
    }

    private static bool OnlyFloatChanged(byte[] before, byte[] after, string field, float expected)
    {
        var pattern = new Regex(@"(?m)^  " + Regex.Escape(field) + @": (?<value>[^\r\n]+)");
        string original = Encoding.UTF8.GetString(before);
        string saved = Encoding.UTF8.GetString(after);
        if (pattern.Matches(original).Count != 1 || pattern.Matches(saved).Count != 1 ||
            pattern.Replace(original, "<selected value>") != pattern.Replace(saved, "<selected value>"))
            return false;
        float value;
        return float.TryParse(pattern.Match(saved).Groups["value"].Value, System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out value) && value.Equals(expected);
    }

    private static void ShowFailure(string message)
    {
        Debug.LogError("Live tutorial positioning value was not saved: " + message);
        EditorUtility.DisplayDialog("Cannot save tutorial positioning value", message, "OK");
    }
}
