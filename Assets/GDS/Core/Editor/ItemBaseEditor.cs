using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace GDS.Core {

    [CustomEditor(typeof(ItemBase), true)]
    public class ItemBaseEditor : Editor {

        public override VisualElement CreateInspectorGUI() {
            if (target is not ItemBase b) return new VisualElement();

            var root = Dom.Div();
            EditorUtil.AddDefaultEditorStylesheet(root);

            var script = new PropertyField(serializedObject.FindProperty("m_Script")).Enabled(false);
            var name = new PropertyField(serializedObject.FindProperty("Name"));
            var icon = new PropertyField(serializedObject.FindProperty("Icon"));
            var stackable = new PropertyField(serializedObject.FindProperty("Stackable"));
            var maxStackSize = new PropertyField(serializedObject.FindProperty("MaxStackSize"));

            root.Add(script, name, icon, stackable, maxStackSize);

            var image = new Image().WithClass("icon-preview");

            script.style.marginRight = 64;
            name.style.marginRight = 64;
            icon.style.marginRight = 64;

            stackable.RegisterValueChangeCallback(evt => maxStackSize.SetVisible(evt.changedProperty.boolValue));
            icon.RegisterValueChangeCallback(evt => UpdatePreview(image, evt.changedProperty.objectReferenceValue as Sprite));

            var iterator = serializedObject.GetIterator();

            // step into the object
            if (iterator.NextVisible(true)) {
                do {
                    if (iterator.propertyPath == "m_Script") continue;
                    if (iterator.propertyPath == "Name") continue;
                    if (iterator.propertyPath == "Icon") continue;
                    if (iterator.propertyPath == "Stackable") continue;
                    if (iterator.propertyPath == "MaxStackSize") continue;

                    var propertyField = new PropertyField(iterator);
                    propertyField.BindProperty(iterator);
                    root.Add(propertyField);

                } while (iterator.NextVisible(false));
            }

            root.Add(image);

            return root;
        }

        private void UpdatePreview(Image img, Sprite sprite) => img.image = sprite != null ? sprite.texture : null;

    }

}