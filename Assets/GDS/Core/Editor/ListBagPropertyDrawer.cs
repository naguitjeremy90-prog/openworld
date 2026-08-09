using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace GDS.Core {
    [CustomPropertyDrawer(typeof(ListBag), true)]
    public class ListBagPropertyDrawer : PropertyDrawer {

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {

            if (property.propertyType is not SerializedPropertyType.ManagedReference) {
                var propField = new PropertyField(property);
                propField.SetEnabled(!EditorApplication.isPlaying);
                return propField;
            }

            return Dom.Div();
        }


    }
}