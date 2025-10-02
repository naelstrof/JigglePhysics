using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
#endif

namespace GatorDragonGames.JigglePhysics {

[Serializable]
public struct JiggleTransformCachedData {
    public Transform bone;
    public float normalizedDistanceFromRoot;
    public float lossyScale;
    public Vector3 restLocalPosition;
    public Vector4 restLocalRotation;
}

[Serializable]
public struct JiggleRigData {
    [SerializeField] public bool hasSerializedData;
    [SerializeField] public string serializedVersion;
    [SerializeField] public Transform rootBone;
    [SerializeField] public bool excludeRoot;
    [SerializeField] public JiggleTreeInputParameters jiggleTreeInputParameters;
    [SerializeField] public Transform[] excludedTransforms;
    [SerializeField, HideInInspector] public JiggleTransformCachedData[] transformCachedData;
    [SerializeField] public JiggleColliderSerializable[] jiggleColliders;
    
    [NonSerialized]
    private Dictionary<Transform, JiggleTransformCachedData> transformToCachedDataMap;

    private bool TryUpdateSerialization() {
        switch (serializedVersion) {
            case "v0.0.0": // Collision radius local space -> world space
                if (rootBone == null) {
                    return false;
                }
                var cachedScale = GetCache(rootBone);
                var scale = rootBone.lossyScale;
                var scaleSample = (scale.x + scale.y + scale.z)/3f;
                var scaleCorrection = cachedScale.lossyScale*(1f/(scaleSample*scaleSample));
                jiggleTreeInputParameters.collisionRadius.value *= scaleCorrection;
                serializedVersion = "v0.0.1";
                return true;
            case "v0.0.1": // rest pose is now serialized on author, generate if missing.
                var length = transformCachedData.Length;
                for (int i = 0; i < length; i++) {
                    var cachedData = transformCachedData[i];
                    var t = cachedData.bone;
                    if (!t) continue;
                    t.GetLocalPositionAndRotation(out var localPosition, out var localRotation);
                    cachedData.restLocalPosition = localPosition;
                    cachedData.restLocalRotation = new Vector4(localRotation.x, localRotation.y, localRotation.z, localRotation.w);
                    transformCachedData[i] = cachedData;
                }
                serializedVersion = "v0.0.2";
                return true;
            default:
                return false;
        }
    }

    public void ResampleRestPose() {
        var length = transformCachedData.Length;
        for (int i = 0; i < length; i++) {
            var cachedData = transformCachedData[i];
            var t = cachedData.bone;
            if (!t) continue;
            t.GetLocalPositionAndRotation(out var localPosition, out var localRotation);
            cachedData.restLocalPosition = localPosition;
            cachedData.restLocalRotation = new Vector4(localRotation.x, localRotation.y, localRotation.z, localRotation.w);
            transformCachedData[i] = cachedData;
        }
        RegenerateCacheLookup();
    }

    public void SnapToRestPose() {
        var length = transformCachedData.Length;
        for (int i = 0; i < length; i++) {
            var cachedData = transformCachedData[i];
            var t = cachedData.bone;
            if (!t || t == rootBone) continue;
            t.SetLocalPositionAndRotation(cachedData.restLocalPosition, new Quaternion(cachedData.restLocalRotation.x, cachedData.restLocalRotation.y, cachedData.restLocalRotation.z, cachedData.restLocalRotation.w));
        }
    }

    public void RegenerateCacheLookup() {
        transformToCachedDataMap = new Dictionary<Transform, JiggleTransformCachedData>();
        var count = transformCachedData.Length;
        for (int i = 0; i < count; i++) {
            var cachedData = transformCachedData[i];
            transformToCachedDataMap[cachedData.bone] = cachedData;
        }
    }

    public bool GetIsExcluded(Transform t) {
        var count = excludedTransforms.Length;
        for (int i = 0; i < count; i++) {
            if (excludedTransforms[i] == t) {
                return true;
            }
        }
        return false;
    }
    
    public void GetJiggleColliders(List<JiggleCollider> colliders) {
        colliders.Clear();
        var count = jiggleColliders.Length;
        for(int i=0;i<count;i++) {
            colliders.Add(jiggleColliders[i].collider);
        }
    }

    void ValidateCurve(ref AnimationCurve animationCurve) {
        if (animationCurve == null || animationCurve.length == 0) {
            animationCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        }
    }

    public void OnValidate() {
        jiggleTreeInputParameters.OnValidate();
        excludedTransforms ??= Array.Empty<Transform>();
        ValidateCurve(ref jiggleTreeInputParameters.stiffness.curve);
        ValidateCurve(ref jiggleTreeInputParameters.angleLimit.curve);
        ValidateCurve(ref jiggleTreeInputParameters.stretch.curve);
        ValidateCurve(ref jiggleTreeInputParameters.drag.curve);
        ValidateCurve(ref jiggleTreeInputParameters.airDrag.curve);
        ValidateCurve(ref jiggleTreeInputParameters.gravity.curve);
        ValidateCurve(ref jiggleTreeInputParameters.collisionRadius.curve);
        BuildNormalizedDistanceFromRootList();
        for (int i = 0; i < 100; i++) {
            if (!TryUpdateSerialization()) {
                break;
            }
        }
        if (jiggleColliders is { Length: > 32 }) {
            Debug.LogWarning("JigglePhysics: Maximum of 32 personal Jiggle Colliders are supported per tree. Extra colliders will be dropped.");
            Array.Resize(ref jiggleColliders, 32);
        }
    }
    public void BuildNormalizedDistanceFromRootList() {
        if (!rootBone) {
            return;
        }
        JigglePhysics.VisitForLength(rootBone, this, rootBone.position, 0f, out var totalLength);
        var data = new List<JiggleTransformCachedData>();
        VisitAndSetCacheData(data, rootBone, rootBone.position, 0f, totalLength);
        transformCachedData = data.ToArray();
        RegenerateCacheLookup();
    }
    
    public void VisitAndSetCacheData(List<JiggleTransformCachedData> data, Transform t, Vector3 lastPosition, float currentLength, float totalLength) {
        if (GetIsExcluded(t)) {
            return;
        }
        var validChildrenCount = GetValidChildrenCount(t);
        var scale = t.lossyScale;
        currentLength += Vector3.Distance(lastPosition, t.position);
        t.GetLocalPositionAndRotation(out var localPosition, out var localRotation);
        var position = t.position;
        data.Add(new JiggleTransformCachedData() {
            bone = t,
            restLocalPosition = localPosition,
            restLocalRotation = new Vector4(localRotation.x, localRotation.y, localRotation.z, localRotation.w),
            normalizedDistanceFromRoot = currentLength / totalLength,
            lossyScale = (scale.x + scale.y + scale.x)/3f,
        });
        for (int i = 0; i < validChildrenCount; i++) {
            var child = GetValidChild(t, i);
            VisitAndSetCacheData(data, child, position, currentLength, totalLength);
        }
    }

    public int GetValidChildrenCount(Transform t) {
        int count = 0;
        var childCount = t.childCount;
        for(int i=0;i<childCount;i++) {
            if (GetIsExcluded(t.GetChild(i))) continue;
            count++;
        }
        return count;
    }

    public Transform GetValidChild(Transform t, int index) {
        int count = 0;
        var childCount = t.childCount;
        for(int i=0;i<childCount;i++) {
            var child = t.GetChild(i);
            if (GetIsExcluded(child)) continue;
            if (count == index) {
                return child;
            }
            count++;
        }
        return null;
    }
    
    public void GetJiggleColliderTransforms(List<Transform> colliderTransforms) {
        colliderTransforms.Clear();
        var count = jiggleColliders.Length;
        for(int i=0;i<count;i++) {
            colliderTransforms.Add(jiggleColliders[i].transform);
        }
    }
    
    public bool GetHasRootTransformError() => !rootBone;
    public bool GetCacheIsValid() => transformCachedData is { Length: > 0 } && transformToCachedDataMap != null && transformToCachedDataMap.Count == transformCachedData.Length;
    public JiggleTransformCachedData GetCache(Transform t) {
        return transformToCachedDataMap[t];
    }

    /// <summary>
    /// Sends updated parameters to the jiggle tree on the jobs side. Uses the provided list to prevent allocations.
    /// </summary>
    /// <param name="tree">Tree to update</param>
    /// <param name="parameters">empty list purely used to prevent allocations</param>
    public void UpdateParameters(JiggleTree tree, List<JigglePointParameters> parameters) {
        parameters.Clear();
        var bones = tree.bones;
        if (bones == null) {
            return;
        }
        var boneCount = bones.Length;
        for (int i = 0; i < boneCount; i++) {
            var bone = bones[i];
            var cache = GetCache(bone);
            parameters.Add(GetJiggleBoneParameter(cache.normalizedDistanceFromRoot));
        }
        tree.SetParameters(parameters);
    }
    
    public JigglePointParameters GetJiggleBoneParameter(float normalizedDistanceFromRoot) {
        return jiggleTreeInputParameters.ToJigglePointParameters(normalizedDistanceFromRoot);
    }
    
    public Transform[] GetJiggleBoneTransforms() {
        return rootBone.GetComponentsInChildren<Transform>();
    }
    
    public bool IsValid(Transform root) => (rootBone && rootBone.IsChildOf(root));
    public static JiggleRigData Default() {
        return new JiggleRigData {
            rootBone = null,
            serializedVersion = "v0.0.0",
            hasSerializedData = true,
            excludeRoot = false,
            jiggleTreeInputParameters = JiggleTreeInputParameters.Default(),
            excludedTransforms = Array.Empty<Transform>(),
            transformCachedData = Array.Empty<JiggleTransformCachedData>(),
            jiggleColliders = Array.Empty<JiggleColliderSerializable>() 
        };
    }

    public void OnDrawGizmosSelected() {
        if (jiggleColliders != null) {
            var count = jiggleColliders.Length;
            for(int i=0;i<count;i++) {
                jiggleColliders[i].OnDrawGizmosSelected();
            }
        }
        
        if (!rootBone) return;
        Gizmos.color = new Color(0.9607844f, 0.9607844f, 0.9607844f, 1f);
        var jiggleTree = JigglePhysics.CreateJiggleTree(this, null);
        var points = jiggleTree.points;
        var parameters = jiggleTree.parameters;
        var pointCount = points.Length;
        var cam = Camera.current;
        for (var index = 0; index < pointCount; index++) {
            var simulatedPoint = points[index];
            if (simulatedPoint.parentIndex == -1) continue;
            if (!points[simulatedPoint.parentIndex].hasTransform) continue;
            DrawBone(points[simulatedPoint.parentIndex].position, simulatedPoint.position, jiggleTree.bones[index].lossyScale, parameters[simulatedPoint.parentIndex], cam);
        }
    }
    
    private static void DrawWireDisc(Vector3 center, Vector3 normal, float radius, int segmentCount = 32) {
        normal.Normalize();
        Vector3 up = normal;
        Vector3 forward = Vector3.Slerp(up, -up, 0.5f);
        Vector3 right = Vector3.Cross(up, forward).normalized * radius;

        float angleStep = 360f / segmentCount;
        Vector3 prevPoint = center + right;
        for (int i = 1; i <= segmentCount; i++) {
            float angle = angleStep * i;
            Quaternion rot = Quaternion.AngleAxis(angle, up);
            Vector3 nextPoint = center + rot * right;
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }
    
    private void DrawBone(Vector3 boneHead, Vector3 boneTail, Vector3 boneScale, JigglePointParameters jigglePointParameters, Camera cam) {
        var camForward = cam.transform.forward;
        var fixedScreenSize = 0.01f;
        var toCam = cam.transform.position - boneHead;
        var distance = toCam.magnitude;
        var scale = distance * fixedScreenSize;
        scale = jigglePointParameters.collisionRadius * (boneScale.x + boneScale.y + boneScale.z)/3f;
        DrawWireDisc(boneHead, camForward, scale);
        Gizmos.DrawLine(boneHead, boneTail);
        var boneDirection = (boneTail - boneHead).normalized;
        var angleLimitScale = 0.05f;
        DrawWireDisc(boneHead + boneDirection * (angleLimitScale * Mathf.Cos(jigglePointParameters.angleLimit * Mathf.Deg2Rad)),
            boneDirection,
            angleLimitScale * Mathf.Sin(jigglePointParameters.angleLimit * Mathf.Deg2Rad));
    }
#if UNITY_EDITOR
    public VisualElement GetInspectorVisualElement(SerializedProperty serializedProperty) {
        var visualElement = new VisualElement();
        var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetDatabase.GUIDToAssetPath("c35a2123f4d44dd469ccb24af7a0ce20"));
        visualTreeAsset.CloneTree(visualElement);
        SetCurvableSlider(
            visualElement,
            serializedProperty,
            "StiffnessControl",
            nameof(JiggleTreeInputParameters.stiffness),
            "Stiffness",
            0.2f,
            1f,
            "Stiffness controls how strongly the bone returns to its rest pose. A value of 1 makes it immovable, while a value of 0 makes it fall freely."
        );

        var angleLimitToggleElement = visualElement.Q<Toggle>("AngleLimitToggle");
        angleLimitToggleElement.BindProperty(serializedProperty.FindPropertyRelative(nameof(JiggleTreeInputParameters.angleLimitToggle)));
        angleLimitToggleElement.tooltip = "Enable or disable angle limit.";
        angleLimitToggleElement.Q<Label>().text = "Angle Limit";

        var angleLimitSection = visualElement.Q<VisualElement>("AngleLimitSection");
        angleLimitToggleElement.RegisterValueChangedCallback(evt => {
            angleLimitSection.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        });

        SetCurvableSlider(
            visualElement,
            serializedProperty,
            "AngleLimitControl",
            nameof(JiggleTreeInputParameters.angleLimit),
            "Angle Limit",
            0f,
            1f,
            "Angle Limit controls the maximum angle deviation from the bone's rest position. 0 means no limit, while 1 represents a 90 degree limit."
        );
        SetSlider(
            visualElement,
            serializedProperty,
            "AngleLimitSoftenSlider",
            nameof(JiggleTreeInputParameters.angleLimitSoften),
            "Angle Limit Soften",
            "Softens the angle limit to prevent hard stops, 0 is hard, 1 is soft."
        );
        SetSlider(
            visualElement,
            serializedProperty,
            "SoftenSlider",
            nameof(JiggleTreeInputParameters.soften),
            "Soften",
            "Weakens the stiffness of the bone when it's closer to the target pose. Prevents large deformations, while still looking very soft."
        );
        SetSlider(
            visualElement,
            serializedProperty,
            "IgnoreRootMotionSlider",
            nameof(JiggleTreeInputParameters.ignoreRootMotion),
            "Ignore Root Motion",
            "Prevents movement from root transform accelleration."
        );
        SetSlider(
            visualElement,
            serializedProperty,
            "RootStretchSlider",
            nameof(JiggleTreeInputParameters.rootStretch),
            "Root Stretch",
            "Allows the root bone to translate. 0 means the root bone is fixed in place, while 1 means it can stretch freely."
        );
        SetCurvableSlider(
            visualElement,
            serializedProperty,
            "StretchControl",
            nameof(JiggleTreeInputParameters.stretch),
            "Stretch",
            0f,
            1f,
            "Stretch controls the elasticity of the bone length, where 0 is no stretch and 1 is full stretch."
        );
        SetCurvableSlider(
            visualElement,
            serializedProperty,
            "DragControl",
            nameof(JiggleTreeInputParameters.drag),
            "Drag",
            0f,
            1f,
            "Drag controls the tendency for the bone to stop oscillating, where 0 is maximum oscillations and 1 is zero oscillations."
        );
        SetCurvableSlider(
            visualElement,
            serializedProperty,
            "AirDragControl",
            nameof(JiggleTreeInputParameters.airDrag),
            "Air Drag",
            0f,
            1f,
            "Air Drag controls how much resistance the bone experiences in air, 0 is no resistance and 1 is maximum resistance."
        );

        SetCurvableFloat(
            visualElement,
            serializedProperty,
            "GravityControl",
            nameof(JiggleTreeInputParameters.gravity),
            "Gravity", "The multiplier of the gravity of the physics.");

        SetCurvableFloat(
            visualElement,
            serializedProperty,
            "CollisionRadiusControl",
            nameof(JiggleTreeInputParameters.collisionRadius),
            "Collision Radius",
            "The radius used in collisions in meters. This is in world space, but will adjust in runtime if bones are scaled at runtime.",
            0f);

        var advancedToggleElement = visualElement.Q<Toggle>("AdvancedToggle");
        advancedToggleElement.BindProperty(
            serializedProperty.FindPropertyRelative(nameof(JiggleTreeInputParameters.advancedToggle)));
        advancedToggleElement.Q<Label>().text = "Advanced";

        var collisionToggleElement = visualElement.Q<Toggle>("CollisionToggle");
        collisionToggleElement.BindProperty(serializedProperty.FindPropertyRelative(nameof(JiggleTreeInputParameters.collisionToggle)));
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

    void SetSlider(VisualElement visualElement, SerializedProperty serializedProperty, string id, string parameter, string propertyName, string tooltip) {
        var sliderElement = visualElement.Q<Slider>(id);
        var property = serializedProperty.FindPropertyRelative(parameter);
        sliderElement.BindProperty(property);
        sliderElement.tooltip = tooltip;
        sliderElement.Q<Label>().text = propertyName;
    }

    void SetCurvableSlider(VisualElement visualElement, SerializedProperty serializedProperty, string id, string curvableFloatParameter, string propertyName, float min, float max, string tooltip) {
        var curvableFloatProperty = serializedProperty.FindPropertyRelative(curvableFloatParameter);
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

    void SetCurvableFloat(VisualElement visualElement, SerializedProperty serializedProperty, string id, string curvableFloatParameter, string propertyName, string tooltip, float? min = null, float? max = null) {
        var curvableFloatProperty = serializedProperty.FindPropertyRelative(curvableFloatParameter);
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
#endif
}
}
