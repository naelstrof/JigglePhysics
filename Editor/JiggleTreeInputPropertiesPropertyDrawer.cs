#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GatorDragonGames.JigglePhysics {

[CustomPropertyDrawer(typeof(JiggleTreeInputParameters))]
public class JiggleTreeInputPropertiesPropertyDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty property) {
        var visualElement = new VisualElement();
        var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetDatabase.GUIDToAssetPath("c35a2123f4d44dd469ccb24af7a0ce20"));
        visualTreeAsset.CloneTree(visualElement);
        SetCurvableSlider(
            visualElement,
            property,
            "StiffnessControl",
            nameof(JiggleTreeInputParameters.stiffness),
            "Stiffness",
            0.2f,
            1f,
            "Stiffness controls how strongly the bone returns to its rest pose. A value of 1 makes it immovable, while a value of 0 makes it fall freely."
        );

        var angleLimitToggleElement = visualElement.Q<Toggle>("AngleLimitToggle");
        angleLimitToggleElement.BindProperty(property.FindPropertyRelative(nameof(JiggleTreeInputParameters.angleLimitToggle)));
        angleLimitToggleElement.tooltip = "Enable or disable angle limit.";
        angleLimitToggleElement.Q<Label>().text = "Angle Limit";

        var angleLimitSection = visualElement.Q<VisualElement>("AngleLimitSection");
        angleLimitToggleElement.RegisterValueChangedCallback(evt => {
            angleLimitSection.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        });

        SetCurvableSlider(
            visualElement,
            property,
            "AngleLimitControl",
            nameof(JiggleTreeInputParameters.angleLimit),
            "Angle Limit",
            0f,
            1f,
            "Angle Limit controls the maximum angle deviation from the bone's rest position. 0 means no limit, while 1 represents a 90 degree limit."
        );
        SetSlider(
            visualElement,
            property,
            "AngleLimitSoftenSlider",
            nameof(JiggleTreeInputParameters.angleLimitSoften),
            "Angle Limit Soften",
            "Softens the angle limit to prevent hard stops, 0 is hard, 1 is soft."
        );
        SetSlider(
            visualElement,
            property,
            "SoftenSlider",
            nameof(JiggleTreeInputParameters.soften),
            "Soften",
            "Weakens the stiffness of the bone when it's closer to the target pose. Prevents large deformations, while still looking very soft."
        );
        SetSlider(
            visualElement,
            property,
            "IgnoreRootMotionSlider",
            nameof(JiggleTreeInputParameters.ignoreRootMotion),
            "Ignore Root Motion",
            "Prevents movement from root transform accelleration."
        );
        SetSlider(
            visualElement,
            property,
            "RootStretchSlider",
            nameof(JiggleTreeInputParameters.rootStretch),
            "Root Stretch",
            "Allows the root bone to translate. 0 means the root bone is fixed in place, while 1 means it can stretch freely."
        );
        SetCurvableSlider(
            visualElement,
            property,
            "StretchControl",
            nameof(JiggleTreeInputParameters.stretch),
            "Stretch",
            0f,
            1f,
            "Stretch controls the elasticity of the bone length, where 0 is no stretch and 1 is full stretch."
        );
        SetCurvableSlider(
            visualElement,
            property,
            "DragControl",
            nameof(JiggleTreeInputParameters.drag),
            "Drag",
            0f,
            1f,
            "Drag controls the tendency for the bone to stop oscillating, where 0 is maximum oscillations and 1 is zero oscillations."
        );
        SetCurvableSlider(
            visualElement,
            property,
            "AirDragControl",
            nameof(JiggleTreeInputParameters.airDrag),
            "Air Drag",
            0f,
            1f,
            "Air Drag controls how much resistance the bone experiences in air, 0 is no resistance and 1 is maximum resistance."
        );

        SetCurvableFloat(
            visualElement,
            property,
            "GravityControl",
            nameof(JiggleTreeInputParameters.gravity),
            "Gravity", "The multiplier of the gravity of the physics.");

        SetCurvableFloat(
            visualElement,
            property,
            "CollisionRadiusControl",
            nameof(JiggleTreeInputParameters.collisionRadius),
            "Collision Radius",
            "The radius used in collisions in meters. This is in world space, but will adjust in runtime if bones are scaled at runtime.",
            0f);

        var advancedToggleElement = visualElement.Q<Toggle>("AdvancedToggle");
        advancedToggleElement.BindProperty(
            property.FindPropertyRelative(nameof(JiggleTreeInputParameters.advancedToggle)));
        advancedToggleElement.Q<Label>().text = "Advanced";

        var collisionToggleElement = visualElement.Q<Toggle>("CollisionToggle");
        collisionToggleElement.BindProperty(property.FindPropertyRelative(nameof(JiggleTreeInputParameters.collisionToggle)));
        collisionToggleElement.tooltip = "Enable or disable collision with Jiggle Colliders";
        collisionToggleElement.Q<Label>().text = "Collision";

        var advancedSection = visualElement.Q<VisualElement>("AdvancedSection");
        var advancedSection2 = visualElement.Q<VisualElement>("AdvancedSection2");
        advancedToggleElement.RegisterValueChangedCallback(evt => {
            advancedSection.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            advancedSection2.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        });

        var collisionSection = visualElement.Q<VisualElement>("CollisionSection");
        collisionToggleElement.RegisterValueChangedCallback(evt => {
            collisionSection.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        });

        return visualElement;
    }
    
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        EditorGUI.LabelField(position, "Jiggle Physics doesn't support IMGUI inspectors, sorry!");
    }

    void SetSlider(VisualElement visualElement, SerializedProperty property, string id, string parameter, string propertyName, string tooltip) {
        var sliderElement = visualElement.Q<Slider>(id);
        var prop = property.FindPropertyRelative(parameter);
        sliderElement.BindProperty(prop);
        sliderElement.tooltip = tooltip;
        sliderElement.Q<Label>().text = propertyName;
    }

    void SetCurvableSlider(VisualElement visualElement, SerializedProperty property, string id, string curvableFloatParameter, string propertyName, float min, float max, string tooltip) {
        var curvableFloatProperty = property.FindPropertyRelative(curvableFloatParameter);
        var floatProperty = curvableFloatProperty.FindPropertyRelative(nameof(JiggleTreeCurvedFloat.value));
        var toggleProperty = curvableFloatProperty.FindPropertyRelative(nameof(JiggleTreeCurvedFloat.curveEnabled));
        var curveProperty = curvableFloatProperty.FindPropertyRelative(nameof(JiggleTreeCurvedFloat.curve));
        
        var sliderElement = visualElement.Q<VisualElement>(id);
        var sliderElementSlider = sliderElement.Q<Slider>("CurvableSlider");
        sliderElementSlider.BindProperty(floatProperty);
        sliderElementSlider.lowValue = min;
        sliderElementSlider.highValue = max;
        sliderElementSlider.tooltip = tooltip;
        sliderElementSlider.Q<Label>().text = propertyName;
        
        var curveElement = sliderElement.Q<CurveField>("CurvableCurve");
        curveElement.tooltip = tooltip;
        curveElement.ranges = new Rect(0f, 0f, 1f, 1f);
        curveElement.BindProperty(curveProperty);
        
        var toggle = sliderElement.Q<Toggle>("CurvableToggle");
        toggle.BindProperty(toggleProperty);
        toggle.tooltip = "Enable or disable curve sampling for this value based on the normalized distance from the root.";
        curveElement.style.display = toggleProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        toggle.RegisterValueChangedCallback(evt => {
            curveElement.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        });
    }

    void SetCurvableFloat(VisualElement visualElement, SerializedProperty property, string id, string curvableFloatParameter, string propertyName, string tooltip, float? min = null, float? max = null) {
        var curvableFloatProperty = property.FindPropertyRelative(curvableFloatParameter);
        var floatProperty = curvableFloatProperty.FindPropertyRelative(nameof(JiggleTreeCurvedFloat.value));
        var toggleProperty = curvableFloatProperty.FindPropertyRelative(nameof(JiggleTreeCurvedFloat.curveEnabled));
        var curveProperty = curvableFloatProperty.FindPropertyRelative(nameof(JiggleTreeCurvedFloat.curve));
        
        var sliderElement = visualElement.Q<VisualElement>(id);
        var curvableFloat = sliderElement.Q<FloatField>("CurvableFloat");
        curvableFloat.BindProperty(floatProperty);
        curvableFloat.tooltip = tooltip;
        curvableFloat.Q<Label>().text = propertyName;
        if (min != null || max != null) {
            curvableFloat.RegisterValueChangedCallback(evt => {
                float value = evt.newValue;
                if (min != null) {
                    value = Mathf.Max(value, min.Value);
                }

                if (max != null) {
                    value = Mathf.Max(value, max.Value);
                }

                curvableFloat.SetValueWithoutNotify(value);
            });
        }
        var curveElement = sliderElement.Q<CurveField>("CurvableCurve");
        curveElement.ranges = new Rect(0f, 0f, 1f, 1f);
        curveElement.BindProperty(curveProperty);
        
        var toggle = sliderElement.Q<Toggle>("CurvableToggle");
        toggle.BindProperty(toggleProperty);
        curveElement.style.display = toggleProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        toggle.RegisterValueChangedCallback(evt => {
            curveElement.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        });
    }
}

}
#endif