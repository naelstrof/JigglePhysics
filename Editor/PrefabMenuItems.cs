using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GatorDragonGames.JigglePhysics {

public static class PrefabMenuItems {
    
    [MenuItem("Assets/Create/JiggleRigPrefabs/Breast")]
    private static void CreateJiggleRigBreast() {
        CreatePrefabClone("dbd8a2cd6e96fef449d3b7ac5e6c75f8");
    }
    
    [MenuItem("Assets/Create/JiggleRigPrefabs/Tail")]
    private static void CreateJiggleRigTail() {
        CreatePrefabClone("4443ed55f755a7d45b440eea5c2b2ced");
    }

    private static void CreatePrefabClone(string GUID) {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(GUID));
        if (prefab == null) {
            Debug.LogError("No Prefab found.");
            return;
        }
        var clone = UnityEngine.Object.Instantiate(prefab);
        try {
            clone.name = $"{prefab.name}";
            string path = "Assets";
            var obj = Selection.activeObject;
            if (obj != null) {
                path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
                    path = Path.GetDirectoryName(path);
            }

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(path + $"/{clone.name}.prefab");
            var saved = PrefabUtility.SaveAsPrefabAsset(clone, assetPath);
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
        } finally {
            Object.DestroyImmediate(clone);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    
}

}
