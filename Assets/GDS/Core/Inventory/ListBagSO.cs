using UnityEngine;

namespace GDS.Core {
    [CreateAssetMenu(menuName = "SO/Core/ListBag")]
    public class ListBagSO : ScriptableObject {
        public ListBag Value = new() { Size = 10 };
    }
}
