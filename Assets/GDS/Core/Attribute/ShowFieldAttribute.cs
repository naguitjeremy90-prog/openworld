using UnityEngine;

namespace GDS.Core {
    public class ShowFieldAttribute : PropertyAttribute {
        public string attrName;
        public ShowFieldAttribute(string attrName) => this.attrName = attrName;
    }
}