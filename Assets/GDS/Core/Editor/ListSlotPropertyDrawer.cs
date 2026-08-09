using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace GDS.Core {
    [CustomPropertyDrawer(typeof(ListSlot), true)]
    public class ListSlotPropertyDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            var index = EditorUtil.GetPropertyIndex(property);
            var root = new VisualElement();
            EditorUtil.AddDefaultEditorStylesheet(root);
            // Debug.Log(property.propertyType);
            if (property.propertyType is SerializedPropertyType.ManagedReference) {

                if (property.managedReferenceValue == null) {
                    property.managedReferenceValue = new ListSlot() { Index = index };
                    property.serializedObject.ApplyModifiedProperties();
                }

                var slot = property.managedReferenceValue as ListSlot;
                var item = new PropertyField(property.FindPropertyRelative("Item")) { style = { flexGrow = 1 } };

                if (index != slot.Index) {
                    slot.Index = index;
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                }

                root.WithClass(slot.Index % 2 == 0 ? "even-row" : "odd-row");
                root.Add(Dom.Label("index-label", slot.Index.ToString()));

                if (slot.Tags.Count > 0) {
                    var tags = Dom.Label("key-label", $"Tags: {slot.Tags.Select(t => t.name).CommaJoin()}");
                    root.Add(tags);
                }
                root.Add(item);

            }

            return root;
        }


    }
}