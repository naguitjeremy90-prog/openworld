using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace GDS.Core {

    [CustomPropertyDrawer(typeof(RequiredAttribute))]
    public class RequiredPropertyDrawer : PropertyDrawer {

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {

            var root = Dom.Div();
            EditorUtil.AddDefaultEditorStylesheet(root);

            var field = new PropertyField(property, property.displayName);
            var label = Dom.Label("required-field-label", "Required!");
            var border = Dom.Div("required-field-border");

            field.RegisterValueChangeCallback(_ => Update());

            root.Add(field, label, border).PickIgnoreAll();
            Update();

            void Update() {
                if (HasValue(property)) {
                    label.Hide();
                    border.Hide();
                    return;
                }

                label.Show();
                border.Show();
            }

            return root;
        }


        private bool HasValue(SerializedProperty prop) {
            if (prop == null) return true;
            return prop.propertyType switch {
                SerializedPropertyType.ObjectReference => prop.objectReferenceValue != null,
                SerializedPropertyType.String => !string.IsNullOrEmpty(prop.stringValue),
                SerializedPropertyType.Integer => prop.intValue != 0,
                SerializedPropertyType.Float => prop.floatValue != 0f,
                SerializedPropertyType.Boolean => prop.boolValue,
                _ => true
            };
        }
    }
}