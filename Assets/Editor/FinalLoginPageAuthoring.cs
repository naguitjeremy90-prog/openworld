using System;
using System.Linq;
using Alaala.Authentication;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Explicit, scene-scoped authoring only. Never runs on startup or enters Play Mode.
public static class FinalLoginPageAuthoring
{
    private const string ScenePath = "Assets/Scenes/FinalLoginPage.unity";
    private static TMP_FontAsset Font => TMP_Settings.defaultFontAsset;

    [MenuItem("ALAALA/Authentication/Build Empty Final Login Page")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode manually before authoring.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || scene.isDirty || scene.GetRootGameObjects().Any(g => g.GetComponent<Canvas>() != null || g.GetComponent<EventSystem>() != null))
            throw new InvalidOperationException("Open the saved, empty FinalLoginPage scene before building. Existing UI will not be replaced.");

        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight = .5f;
        var background = Rect("Background", canvasObject.transform, Vector2.zero, Vector2.zero);
        Stretch(background, 0);
        background.gameObject.AddComponent<Image>().color = new Color(.12f, .14f, .17f);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var content = Rect("Authentication", canvasObject.transform, Vector2.zero, new Vector2(500, 680));
        Text("Title", content, "ALAALA", new Vector2(0, 300), new Vector2(460, 48), 38);
        var subtitle = Text("Subtitle", content, "Maligayang Pagbabalik", new Vector2(0, 254), new Vector2(460, 36), 25);
        var login = Panel("LoginPanel", content, new Vector2(460, 480));
        var registration = Panel("RegistrationPanel", content, new Vector2(460, 480));
        var loginUsername = Field("Pangalan", "Username", login, 120, TMP_InputField.ContentType.Standard);
        var loginPassword = Field("Hudyat", "Password", login, 30, TMP_InputField.ContentType.Password);
        var loginButton = Button("Pumasok", "LoginButton", login, -65);
        var showRegistration = Button("Magparehistro", "ShowRegistrationButton", login, -125);
        var username = Field("Pangalan", "Username", registration, 170, TMP_InputField.ContentType.Standard);
        var email = Field("E-Liham", "Email", registration, 80, TMP_InputField.ContentType.EmailAddress);
        var password = Field("Hudyat", "Password", registration, -10, TMP_InputField.ContentType.Password);
        var confirmation = Field("Ulitin ang Hudyat", "ConfirmPassword", registration, -100, TMP_InputField.ContentType.Password);
        var registerButton = Button("Magparehistro", "RegistrationButton", registration, -160);
        var back = Button("Bumalik", "BackButton", registration, -214);
        var feedback = Text("FeedbackText", content, string.Empty, new Vector2(0, -301), new Vector2(480, 62), 21);
        feedback.color = new Color(1f, .86f, .57f);
        feedback.textWrappingMode = TextWrappingModes.Normal;

        var controller = content.gameObject.AddComponent<FinalLoginPageController>();
        var serialized = new SerializedObject(controller);
        Assign(serialized, "loginPanel", login.gameObject);
        Assign(serialized, "registrationPanel", registration.gameObject);
        Assign(serialized, "subtitle", subtitle);
        Assign(serialized, "feedbackText", feedback);
        Assign(serialized, "loginUsername", loginUsername);
        Assign(serialized, "loginPassword", loginPassword);
        Assign(serialized, "registrationUsername", username);
        Assign(serialized, "registrationEmail", email);
        Assign(serialized, "registrationPassword", password);
        Assign(serialized, "confirmationPassword", confirmation);
        Assign(serialized, "loginButton", loginButton);
        Assign(serialized, "showRegistrationButton", showRegistration);
        Assign(serialized, "registrationButton", registerButton);
        Assign(serialized, "backButton", back);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        NavigationLoop(loginUsername, loginPassword, loginButton, showRegistration);
        NavigationLoop(username, email, password, confirmation, registerButton, back);
        canvasObject.GetComponent<GraphicRaycaster>().blockingObjects = GraphicRaycaster.BlockingObjects.None;
        UnityEngine.Object.FindAnyObjectByType<EventSystem>().firstSelectedGameObject = loginUsername.gameObject;
        registration.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("FinalLoginPage was not saved.");
        // Append only: preserve SceneMenu as the existing first enabled scene.
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(
                Resources.FindObjectsOfTypeAll<EditorBuildSettings>(), "ProjectSettings/EditorBuildSettings.asset", true);
        }
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        result.SetParent(parent, false);
        result.anchorMin = result.anchorMax = result.pivot = new Vector2(.5f, .5f);
        result.anchoredPosition = position;
        result.sizeDelta = size;
        return result;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 size)
    {
        var panel = Rect(name, parent, Vector2.zero, size);
        panel.gameObject.AddComponent<Image>().color = new Color(.19f, .22f, .26f);
        return panel;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize)
    {
        var rect = Rect(name, parent, position, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Font;
        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.text = value;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = false;
        text.raycastTarget = false;
        return text;
    }

    private static TMP_InputField Field(string label, string name, Transform parent, float y, TMP_InputField.ContentType type)
    {
        Text(name + "Label", parent, label, new Vector2(0, y + 36), new Vector2(396, 28), 21).alignment = TextAlignmentOptions.MidlineLeft;
        var rect = Rect(name + "Input", parent, new Vector2(0, y), new Vector2(396, 44));
        var image = rect.gameObject.AddComponent<Image>(); image.color = Color.white;
        var input = rect.gameObject.AddComponent<TMP_InputField>();
        var viewport = Rect("Text Area", rect, Vector2.zero, Vector2.zero);
        Stretch(viewport, 9);
        viewport.gameObject.AddComponent<RectMask2D>();
        var text = Text("Text", viewport, string.Empty, Vector2.zero, Vector2.zero, 22);
        Stretch(text.rectTransform, 0);
        text.color = new Color(.1f, .12f, .15f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        var placeholder = Text("Placeholder", viewport, label, Vector2.zero, Vector2.zero, 22);
        Stretch(placeholder.rectTransform, 0);
        placeholder.color = new Color(.4f, .42f, .45f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.targetGraphic = image;
        input.contentType = type;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.asteriskChar = '\u2022';
        input.richText = false;
        input.characterLimit = 0; // Report lengths, rather than silently truncating credentials.
        input.customCaretColor = true;
        input.caretColor = Color.black;
        return input;
    }

    private static Button Button(string label, string name, Transform parent, float y)
    {
        var rect = Rect(name, parent, new Vector2(0, y), new Vector2(396, 44));
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.33f, .4f, .48f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var labelText = Text("Label", rect, label, Vector2.zero, Vector2.zero, 22);
        Stretch(labelText.rectTransform, 0);
        return button;
    }

    private static void Assign(SerializedObject target, string name, UnityEngine.Object value)
        => target.FindProperty(name).objectReferenceValue = value;

    private static void NavigationLoop(params Selectable[] items)
    {
        for (int i = 0; i < items.Length; i++)
            items[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = items[(i + items.Length - 1) % items.Length], selectOnDown = items[(i + 1) % items.Length] };
    }
}
