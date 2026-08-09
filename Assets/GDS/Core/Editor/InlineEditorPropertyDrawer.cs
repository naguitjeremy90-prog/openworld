using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Core {

    [CustomPropertyDrawer(typeof(InlineEditorAttribute))]
    public class InlineEditorPropertyDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            var attr = (InlineEditorAttribute)attribute;

            var root = new VisualElement().WithClass("custom-box");
            EditorUtil.AddDefaultEditorStylesheet(root);

            var propertyField = new PropertyField(property, property.displayName);
            var inlineContainer = new VisualElement();
            var foldout = new Foldout { text = "Details", name = "details-foldout", viewDataKey = property.name + "-details", value = false }.WithClass("custom-foldout");
            foldout.Add(inlineContainer);

            // (re)build the inline inspector
            void RebuildInline() {
                inlineContainer.Clear();
                if (property.objectReferenceValue is not ScriptableObject so) { foldout.SetVisible(false); return; }

                var inspector = new InspectorElement(so) { style = { paddingLeft = 0 } };
                inlineContainer.Add(inspector);
                foldout.SetVisible(true);
            }

            // React to changes on the PropertyField. When user changes the reference, re-draw the inspector.
            // PropertyField emits ChangeEvent<UnityEngine.Object> when the Object reference changes.
            propertyField.RegisterCallback<ChangeEvent<UnityEngine.Object>>(evt => {
                // Apply/Update the serialized property so property.objectReferenceValue is current
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
                RebuildInline();
            });

            // Initial draw (in case a reference is already assigned)
            property.serializedObject.Update();
            RebuildInline();

            root.Add(propertyField, foldout);

            return root;
        }
    }

}