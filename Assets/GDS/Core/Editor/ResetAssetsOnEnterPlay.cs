
using UnityEditor;
using UnityEngine;

namespace GDS.Core {

    public static class ResetAssetsOnEnterPlay {
        [InitializeOnEnterPlayMode]
        static void OnEnterPlayMode() {
            foreach (var guid in AssetDatabase.FindAssets("l:ResetOnEnterPlay")) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so is ICanReset resettable) { resettable.Reset(); }
            }
        }
    }
}