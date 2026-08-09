using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Core {
    [CustomPropertyDrawer(typeof(Item))]
    public class ItemPropertyDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            if (property.propertyType is not SerializedPropertyType.ManagedReference) {
                Debug.LogAssertion("Item should be a serialized reference");
                return new PropertyField(property);
            }
            var root = new VisualElement();
            EditorUtil.AddDefaultEditorStylesheet(root);

            if (property.managedReferenceValue == null) {

                var baseField = new ObjectField("Item") { objectType = typeof(ItemBase) };
                baseField.AddToClassList(ObjectField.alignedFieldUssClassName);
                baseField.RegisterValueChangedCallback((e) => {
                    if (e.newValue is not ItemBase b) return;
                    property.managedReferenceValue = b.CreateItem();
                    property.serializedObject.ApplyModifiedProperties();
                });

                root.Add(baseField);

                return root;
            }

            var item = property.managedReferenceValue as Item;
            if (item == null) {
                root.Add(new Label($"Unable to draw IItem using {GetType()}"));
                return root;
            }

            // Debug.Log($"should start drawing property {property.name}, item: {item}");
            var fieldsContainer = new VisualElement();
            var fields = EditorUtil.IterateChildren(property);
            var index = 0;
            foreach (var field in fields) {
                if (field.name == nameof(Item.Id) && !EditorApplication.isPlaying) continue;
                if (field.name == nameof(Item.StackSize) && !item.Base.Stackable) continue;
                var propField = new PropertyField(field);
                if (field.name == nameof(Item.Base)) propField.SetEnabled(false);
                fieldsContainer.Add(propField);

                if (index <= 2 && field.name != "Capacity") propField.style.marginRight = 64;

                index++;
            }


            root.Add(fieldsContainer);
            root.Add(IconPreview(item.Base));
            root.Add(ClearButton(property));





            fieldsContainer.style.flexGrow = 1;
            root.style.flexDirection = FlexDirection.Row;
            root.style.minHeight = 60;

            return root;
        }

        VisualElement IconPreview(ItemBase itemBase) {
            VisualElement el = itemBase.Icon == null
                ? new Label("No Icon")
                : new Image() { sprite = itemBase.Icon };
            el.AddToClassList("icon-preview");
            return el;
        }

        VisualElement ClearButton(SerializedProperty property) {
            var el = new Button(() => {
                property.managedReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
            }) { text = "x" };

            return el.WithClass("btn-delete-item");
        }

    }
}