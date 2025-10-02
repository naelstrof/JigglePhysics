#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GatorDragonGames.JigglePhysics {

[CustomPropertyDrawer(typeof(JiggleRigData))]
public class JiggleRigDataPropertyDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty property) {
        var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetDatabase.GUIDToAssetPath("3b91b5cf6b975bd4d83d8a940258c420"));
        var visualElement = new VisualElement();
        visualTreeAsset.CloneTree(visualElement);
        
        var rootElement = visualElement.Q<ObjectField>("RootField");
        rootElement.objectType = typeof(Transform);
        var rootProp = property.FindPropertyRelative(nameof(JiggleRigData.rootBone));
        rootElement.BindProperty(rootProp);
        rootElement.Q<Label>().text = "Root Transform";

        var excludeRootToggleElement = visualElement.Q<Toggle>("ExcludeRootToggle");
        excludeRootToggleElement.BindProperty(property.FindPropertyRelative(nameof(JiggleRigData.excludeRoot)));
        excludeRootToggleElement.Q<Label>().text = "Motionless Root";    
        excludeRootToggleElement.tooltip = "Exclude the root from the jiggle simulation. Use this to coalesce many branching jiggles.";

        var excludedTransformsElement = visualElement.Q<PropertyField>("IgnoredTransformsField");
        excludedTransformsElement.BindProperty(property.FindPropertyRelative(nameof(JiggleRigData.excludedTransforms)));

        var personalCollidersElement = visualElement.Q<PropertyField>("PersonalCollidersField");
        personalCollidersElement.BindProperty(property.FindPropertyRelative(nameof(JiggleRigData.jiggleColliders)));
        
        var container = visualElement.Q<VisualElement>("Contents");
        var rig = (JiggleRigData)property.boxedValue;
        container.Add(rig.GetInspectorVisualElement(property.FindPropertyRelative(nameof(JiggleRigData.jiggleTreeInputParameters))));
        
        var rootSection = visualElement.Q<VisualElement>("RootSection");
        excludeRootToggleElement.RegisterValueChangedCallback(evt => {
            rootSection.style.display = evt.newValue ? DisplayStyle.None : DisplayStyle.Flex;
        });

        var recursiveWarning = visualElement.Q<VisualElement>("PersonalColliderWarning");
        if (!property.serializedObject.isEditingMultipleObjects && Application.isPlaying) {
            var rootBone = (Transform)rootProp.objectReferenceValue;
            var isRecursiveRig = false;
            foreach (JiggleRig otherRig in Object.FindObjectsByType<JiggleRig>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                var otherRoot = otherRig.GetJiggleRigData().rootBone;
                if (rootBone != otherRoot && rootBone.IsChildOf(otherRoot)) {
                    isRecursiveRig = true;
                    break;
                }
            }

            if (isRecursiveRig) {
                recursiveWarning.style.display = DisplayStyle.Flex;
                var warningText = "Recursive rigs ignore colliders as they get merged with their parent rig. If colliders are needed, add them to the parent rig instead.";
                HelpBox helpBox = new HelpBox(warningText, HelpBoxMessageType.Warning);
                recursiveWarning.Add(helpBox);
            } else {
                recursiveWarning.style.display = DisplayStyle.None;
            }
        } else {
            recursiveWarning.style.display = DisplayStyle.None;
        }

        return visualElement;
    }
    
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        EditorGUI.LabelField(position, "Jiggle Physics doesn't support IMGUI inspectors, sorry!");
    }
}

}
#endif