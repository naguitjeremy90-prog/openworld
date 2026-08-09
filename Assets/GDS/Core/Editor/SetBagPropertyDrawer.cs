using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace GDS.Core {
    [CustomPropertyDrawer(typeof(SetBag), true)]
    public class SetBagPropertyDrawer : PropertyDrawer {

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