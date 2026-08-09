using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace GDS.Core {
    [CustomPropertyDrawer(typeof(SetSlot), true)]
    public class SetSlotPropertyDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            var root = new VisualElement();
            EditorUtil.AddDefaultEditorStylesheet(root);

            if (property.propertyType is SerializedPropertyType.ManagedReference) {
                if (property.managedReferenceValue == null) {
                    property.managedReferenceValue = new SetSlot();
                    property.serializedObject.ApplyModifiedProperties();
                }

                var slot = property.managedReferenceValue as SetSlot;
                var key = new PropertyField(property.FindPropertyRelative("Key"));
                var tags = new PropertyField(property.FindPropertyRelative("Tags"));
                var item = new PropertyField(property.FindPropertyRelative("Item")) { style = { flexGrow = 1 } };
                var clearButton = new Button(() => {
                    slot.Item = null;
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                }) {
                    text = "x", //"✖",
                    tooltip = "Clear",
                }.WithClass("btn-delete-item");

                var index = EditorUtil.GetPropertyIndex(property);
                if (index != -1) {
                    root.WithClass(index % 2 == 0 ? "even-row" : "odd-row");
                }

                var name = new Label() { bindingPath = "Key" }.WithClass("key-label");
                name.Bind(property.serializedObject);

                root.Add(name, key, tags, item);

                // Add custom fields
                var fields = EditorUtil.IterateChildren(property);
                foreach (var field in fields) {
                    if (field.name == "Key") continue;
                    if (field.name == "Tags") continue;
                    if (field.name == "Item") continue;
                    root.Add(new PropertyField(field));
                }

                if (slot.Item != null) root.Add(clearButton);
            }

            return root;
        }


    }
}