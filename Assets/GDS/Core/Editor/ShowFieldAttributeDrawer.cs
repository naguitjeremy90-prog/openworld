using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace GDS.Core {

    [CustomPropertyDrawer(typeof(ShowFieldAttribute))]
    public class ShowFieldAttributeDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            var name = (attribute as ShowFieldAttribute).attrName;
            var prop = property.FindPropertyRelative(name);
            if (prop == null) return Dom.Label("Could not find property: " + $"{name}".Red());

            return new PropertyField(prop);
        }
    }

}