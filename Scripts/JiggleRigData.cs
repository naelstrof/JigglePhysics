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
    
    private void VisitAndSetCacheData(List<JiggleTransformCachedData> data, Transform t, Vector3 lastPosition, float currentLength, float totalLength) {
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
            lossyScale = (scale.x + scale.y + scale.z)/3f,
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
            serializedVersion = "v0.0.2",
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
#endif
}
}
