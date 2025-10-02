#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

using UnityEditor;
using UnityEditor.UIElements;

namespace GatorDragonGames.JigglePhysics {

[CanEditMultipleObjects]
[CustomEditor(typeof(JiggleRig), true)]
public class JiggleRigEditor : Editor {
    // Required to force the inspector to use UIToolkit. Otherwise it will use IMGUI.
    public override VisualElement CreateInspectorGUI() {
        var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetDatabase.GUIDToAssetPath("f271e6ba6c686f24eb8a1100efaa3edd"));
        var visualElement = new VisualElement();
        visualTreeAsset.CloneTree(visualElement);
        InspectorElement.FillDefaultInspector(visualElement, serializedObject, this);

        if (serializedObject.isEditingMultipleObjects) return visualElement;
        
        var targetRig = (JiggleRig)target;
        var targetObject = targetRig.gameObject;
        var assetType = PrefabUtility.GetPrefabAssetType(targetObject);
        var instanceStatus = PrefabUtility.GetPrefabInstanceStatus(targetObject);
        var isPrefabInstance = true;
        var prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(targetObject);
        if (targetObject != prefabRoot) {
            isPrefabInstance = false;
        } else {
            isPrefabInstance = instanceStatus != PrefabInstanceStatus.NotAPrefab;
            if (targetObject.transform.childCount > 0) isPrefabInstance = false;
        }

        bool isPrefabAsset = assetType != PrefabAssetType.NotAPrefab &&
                             instanceStatus == PrefabInstanceStatus.NotAPrefab;
        if (isPrefabInstance) {
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(targetObject);
            if (prefab.gameObject.transform.parent != null) {
                isPrefabAsset = false;
            }
        }

        var prefabWarning = visualElement.Q<VisualElement>("PrefabWarning");
        prefabWarning.style.display = DisplayStyle.None;
        if (!(isPrefabAsset || isPrefabInstance)) {
            prefabWarning.style.display = DisplayStyle.Flex;
            var warningText =
                "For best results, please use one of the default prefabs, or create your own using the project right click > create menu.\nYou can customize the settings without applying them to the prefab while retaining the ability to adjust the settings for all instances within the prefab if desired.";
            HelpBox helpBox = new HelpBox(warningText, HelpBoxMessageType.Warning);
            prefabWarning.Add(helpBox);
        }

        return visualElement;
    }
}

}

#endif
