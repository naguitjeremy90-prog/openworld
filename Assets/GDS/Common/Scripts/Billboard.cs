using UnityEngine;

namespace GDS.Common.Scripts {
    /// <summary>
    /// Forces the object to always face the camera. Used on world item sprites.
    /// </summary>
    public class Billboard : MonoBehaviour {
        void LateUpdate() {
            transform.forward = Camera.main.transform.forward;
        }
    }
}