using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace GatorDragonGames.JigglePhysics {

public static class JigglePhysics {
    private static Dictionary<Transform, JiggleTreeSegment> jiggleRootLookup;
    private static bool _globalDirty = true;
    private static readonly List<Transform> tempTransforms = new ();
    private static readonly List<Vector3> tempRestLocalPositions = new ();
    private static readonly List<Quaternion> tempRestLocalRotations = new ();
    private static readonly List<JiggleSimulatedPoint> tempPoints = new();
    private static readonly List<JigglePointParameters> tempParameters = new ();
    private static readonly List<JiggleCollider> tempColliders = new ();
    private static readonly List<Transform> tempColliderTransforms = new ();
    private static List<JiggleTreeSegment> rootJiggleTreeSegments;
    private static bool initializedRendering = false;
    private static int skips = 0;

    public const float MERGE_DISTANCE = 0.001f;

    private static JiggleJobs jobs;
    private static bool hasRunThisFrame;
    
    private static double accumulator;
    private static double lastFixedCurrentTime = 0f;

    public static void ScheduleSimulate(double realTime, float fixedDeltaTime) {
        if (hasRunThisFrame) {
            return;
        }

        if (!(realTime - lastFixedCurrentTime > fixedDeltaTime)) return;
        
        while (realTime - lastFixedCurrentTime > fixedDeltaTime) {
            lastFixedCurrentTime += fixedDeltaTime;
            skips = Mathf.Min(skips + 1, 1);
        }
            
        UpdateAnimatedParameters();

        jobs = GetJiggleJobs(lastFixedCurrentTime, fixedDeltaTime);
        jobs.Simulate(lastFixedCurrentTime, realTime, skips);
        skips = 0;
        hasRunThisFrame = true;
    }

    public static void SchedulePose(double currentTime) {
        hasRunThisFrame = false;
        jobs?.SchedulePoses(currentTime);
    }


    public static void CompletePose() {
        jobs?.CompletePoses();
    }

    public static void OnDrawGizmos() {
        if (!Application.isPlaying) {
            return;
        }

        jobs?.OnDrawGizmos();
    }
    private static List<JigglePointParameters> parametersCache;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize() {
        lastFixedCurrentTime = 0f;
        accumulator = 0;
        parametersCache = new();
        rootJiggleTreeSegments = new List<JiggleTreeSegment>();
        jiggleRootLookup = new Dictionary<Transform, JiggleTreeSegment>();
        initializedRendering = false;
        _globalDirty = true;
        jobs?.Dispose();
        jobs = new JiggleJobs(Time.fixedTimeAsDouble, Time.fixedDeltaTime);
    }

    public static void Dispose() {
        jobs?.Dispose();
        JiggleRenderer.Dispose();
        rootJiggleTreeSegments = new List<JiggleTreeSegment>();
        jiggleRootLookup = new Dictionary<Transform, JiggleTreeSegment>();
        _globalDirty = true;
        jobs = null;
    }

    public static void ScheduleRender() {
        if (!initializedRendering) {
            JiggleRenderer.OnEnable(jobs);
            initializedRendering = true;
        }
        JiggleRenderer.PrepareRender(jobs);
    }

    public static void CompleteRender(Material proceduralMaterial, Mesh sphere) {
        if (!initializedRendering) {
            JiggleRenderer.OnEnable(jobs);
            initializedRendering = true;
        }

        JiggleRenderer.FinishRender(proceduralMaterial, sphere);
    }
    
    public static void SetGlobalDirty() => _globalDirty = true;

    public static void AddJiggleCollider(JiggleColliderSerializable collider) {
        jobs.ScheduleAdd(collider);
    }

    /// <summary>
    /// Batch-add multiple colliders. Builds a HashSet of existing transforms once,
    /// then adds all new colliders with O(1) dedup instead of O(n) per collider.
    /// </summary>
    public static void AddJiggleColliders(List<JiggleColliderSerializable> colliders) {
        jobs.ScheduleAddBatch(colliders);
    }

    public static void RemoveJiggleCollider(JiggleColliderSerializable collider) {
        jobs?.ScheduleRemove(collider);
    }

    /// <summary>
    /// Batch-remove multiple colliders. Uses a HashSet for O(n+m) dedup
    /// instead of O(n*m) linear scans per collider.
    /// </summary>
    public static void RemoveJiggleColliders(List<JiggleColliderSerializable> colliders) {
        jobs?.ScheduleRemoveBatch(colliders);
    }

    public static void FreeOnComplete(IntPtr pointer) {
        jobs.FreeOnComplete(pointer);
    }
    
    public static void AddJiggleTreeSegment(JiggleTreeSegment jiggleTreeSegment) {
        if (!jiggleRootLookup.TryAdd(jiggleTreeSegment.transform, jiggleTreeSegment)) {
            Debug.LogError("Multiple Jiggle trees detected targeting the same root transform, Jiggle Physics doesn't support this.", jiggleTreeSegment.transform);
            return;
        }
        RemoveAddChildren(jiggleTreeSegment.transform);
        TryAddRootJiggleTreeSegment(jiggleTreeSegment);
        _globalDirty = true;
    }
    
    private static bool GetParentJiggleTreeSegment(Transform t, out JiggleTreeSegment parentJiggleTreeSegment) {
        var current = t;
        while (current.parent != null) {
            current = current.parent;
            if (jiggleRootLookup.TryGetValue(current, out var jiggleTreeSegment)) {
                parentJiggleTreeSegment = jiggleTreeSegment;
                return true;
            }
        }
        parentJiggleTreeSegment = null;
        return false;
    }
    
    private static bool GetJiggleTreeSegmentByBone(Transform t, out JiggleTreeSegment jiggleRoot) {
        if (jiggleRootLookup.TryGetValue(t, out JiggleTreeSegment root)) {
            jiggleRoot = root;
            return true;
        }
        jiggleRoot = null;
        return false;
    }

    private static bool TryAddRootJiggleTreeSegment(JiggleTreeSegment jiggleTreeSegment) {
        var foundParent = GetParentJiggleTreeSegment(jiggleTreeSegment.transform, out var parentJiggleTreeSegment);
        if (foundParent) {
            jiggleTreeSegment.SetParent(parentJiggleTreeSegment);
            parentJiggleTreeSegment.SetDirty();
            return false;
        } else {
            rootJiggleTreeSegments.Add(jiggleTreeSegment);
            return true;
        }
    }

    private static void RemoveAddChildren(Transform t) {
        foreach (Transform child in t) {
            if (jiggleRootLookup.TryGetValue(child, out var jiggleRootSegment)) {
                rootJiggleTreeSegments.Remove(jiggleRootSegment);
                TryAddRootJiggleTreeSegment(jiggleRootSegment);
            }
            RemoveAddChildren(child);
        }
    }
    
    private static JiggleJobs GetJiggleJobs(double fixedTime, float fixedDeltaTime) {
        if (!_globalDirty) {
            return jobs;
        }
        jobs ??= new JiggleJobs(fixedTime, fixedDeltaTime);
        jobs.SetFixedDeltaTime(fixedDeltaTime);
        GetJiggleTrees();
        _globalDirty = false;
        return jobs;
    }

    private static void GetJiggleTrees() {
        Profiler.BeginSample("JiggleRoot.GetJiggleTrees");
        // Prune any segments whose root Transform has been destroyed out-of-band
        // (e.g. asset bundle unload, scene teardown, or a rig whose OnDisable was
        // skipped) before attempting to regenerate their trees.
        for (int i = rootJiggleTreeSegments.Count - 1; i >= 0; i--) {
            var seg = rootJiggleTreeSegments[i];
            if (seg.transform == null || seg.jiggleRigData.rootBone == null) {
                if (seg.jiggleTree != null) ScheduleRemoveJiggleTree(seg.jiggleTree);
                jiggleRootLookup.Remove(seg.transform);
                rootJiggleTreeSegments.RemoveAt(i);
                continue;
            }
            var currentTree = seg.jiggleTree;
            if (currentTree is { dirty: false }) {
                continue;
            }
            seg.RegenerateJiggleTreeIfNeeded();
            jobs.ScheduleAdd(seg.jiggleTree);
        }
        Profiler.EndSample();
    }

    public static JiggleTree CreateJiggleTree(JiggleRigData jiggleRig, JiggleTree tree) {
        Profiler.BeginSample("JiggleTreeUtility.CreateJiggleTree");
        BuildTreeLists(jiggleRig);
        Profiler.EndSample();
        if (tree != null) {
            tree.Set(tempTransforms, tempPoints, tempParameters, tempColliderTransforms, tempColliders, tempRestLocalPositions, tempRestLocalRotations);
            return tree;
        } else {
            return new JiggleTree(tempTransforms, tempPoints, tempParameters, tempColliderTransforms, tempColliders, tempRestLocalPositions, tempRestLocalRotations);
        }
    }

    /// <summary>
    /// Recomputes the parameters of every point in the tree <paramref name="segment"/> belongs to, exactly as the tree
    /// was built: each bone takes the parameters of the rig that owns it (nested rigs included), and excluded roots stay
    /// rigid. Rebuilds the tree instead when its bones or personal colliders changed.
    /// </summary>
    public static void UpdateTreeParameters(JiggleTreeSegment segment) {
        var root = segment.root;
        var tree = root.jiggleTree;
        if (tree == null || tree.dirty || !root.transform || !root.jiggleRigData.rootBone) {
            return;
        }
        BuildTreeLists(root.jiggleRigData);
        if (!TempListsMatch(tree)) {
            root.SetDirty();
            return;
        }
        jobs?.CompleteRunningJobs(); // the simulate job reads the parameter buffer this overwrites
        tree.SetParameters(tempParameters);
    }

    // Whether the temp lists describe the same bones and personal colliders as the tree, point for point.
    private static bool TempListsMatch(JiggleTree tree) {
        var boneCount = tree.bones.Length;
        var colliderCount = tree.personalColliders.Length;
        if (tempTransforms.Count != boneCount || tempParameters.Count != tree.points.Length
            || tempColliders.Count != colliderCount || tempColliderTransforms.Count != tree.personalColliderTransforms.Length) {
            return false;
        }
        for (int i = 0; i < boneCount; i++) {
            if (tempTransforms[i] != tree.bones[i]) return false;
        }
        for (int i = 0; i < colliderCount; i++) {
            var a = tempColliders[i];
            var b = tree.personalColliders[i];
            if (tempColliderTransforms[i] != tree.personalColliderTransforms[i] || a.type != b.type || a.radius != b.radius
                || a.height != b.height || a.capsuleAxis != b.capsuleAxis) {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Carries the whole tree <paramref name="segment"/> belongs to along with an instant move of its bones, so the
    /// jump is not read as speed. Call it right after moving the character.
    /// </summary>
    /// <returns>False when the tree is not simulating yet; it then starts from the bones' current pose anyway.</returns>
    public static bool Teleport(JiggleTreeSegment segment) {
        var tree = segment.root.jiggleTree;
        if (tree == null || jobs == null) {
            return false;
        }
        return jobs.Teleport(tree);
    }

    private static readonly HashSet<JiggleTreeSegment> tempAnimatedRoots = new();

    private static void UpdateAnimatedParameters() {
        tempAnimatedRoots.Clear();
        foreach (var segment in jiggleRootLookup.Values) {
            // A rig destroyed without OnDisable stays in the lookup until GetJiggleTrees prunes it, which runs later.
            if (segment.HasAnimatedParameters && segment.transform) {
                tempAnimatedRoots.Add(segment.root);
            }
        }
        foreach (var root in tempAnimatedRoots) {
            UpdateTreeParameters(root);
        }
    }

    // Fills the temp lists with the tree that starts at this rig's root bone, in point order.
    private static void BuildTreeLists(JiggleRigData jiggleRig) {
        tempTransforms.Clear();
        tempPoints.Clear();
        tempParameters.Clear();
        tempRestLocalPositions.Clear();
        tempRestLocalRotations.Clear();
        jiggleRig.GetJiggleColliders(tempColliders);
        jiggleRig.GetJiggleColliderTransforms(tempColliderTransforms);
        if (!jiggleRig.GetCacheIsValid()) jiggleRig.BuildNormalizedDistanceFromRootList();
        var backProjection = Vector3.zero;
        var backProjectionChildCount = jiggleRig.GetValidChildrenCount(jiggleRig.rootBone);
        if (backProjectionChildCount != 0) {
            var pos = jiggleRig.rootBone.position;
            var childPos = jiggleRig.GetValidChild(jiggleRig.rootBone, 0).position;
            var diff = pos - childPos;
            backProjection = pos + diff;
        } else {
            backProjection = jiggleRig.rootBone.position + jiggleRig.rootBone.up * 0.25f;
        }
        tempPoints.Add(new JiggleSimulatedPoint() { // Back projected virtual root
            position = backProjection,
            lastPosition = backProjection,
            childrenCount = 0,
            parentIndex = -1,
            hasTransform = false,
            animated = false,
        });
        tempParameters.Add(jiggleRig.GetJiggleBoneParameter(0f));
        tempTransforms.Add(jiggleRig.rootBone);
        jiggleRig.rootBone.GetLocalPositionAndRotation(out var localPosition, out var localRotation);
        tempRestLocalPositions.Add(localPosition);
        tempRestLocalRotations.Add(localRotation);
        Visit(jiggleRig.rootBone, tempTransforms, tempPoints, tempParameters, tempRestLocalPositions, tempRestLocalRotations, 0, jiggleRig, backProjection, 0f, out int childIndex);
        if (childIndex != -1) {
            var rootPoint = tempPoints[0];
            AddChildToPoint(ref rootPoint, childIndex);
            tempPoints[0] = rootPoint;
        }
    }

    public static void VisitForLength(Transform t, JiggleRigData rig, Vector3 lastPosition, float currentLength, out float totalLength) {
        if (t == null || rig.GetIsExcluded(t)) {
            totalLength = Mathf.Max(currentLength, 0.001f);
            return;
        }
        currentLength += Vector3.Distance(lastPosition, t.position);
        totalLength = Mathf.Max(currentLength, 0.001f);
        var validChildrenCount = rig.GetValidChildrenCount(t);
        for (int i = 0; i < validChildrenCount; i++) {
            var child = rig.GetValidChild(t, i);
            VisitForLength(child, rig, t.position, currentLength, out var siblingMaxLength);
            totalLength = Mathf.Max(totalLength, siblingMaxLength);
        }
    }

    private static void Visit(Transform t, List<Transform> transforms, List<JiggleSimulatedPoint> points, List<JigglePointParameters> parameters, List<Vector3> restLocalPositions, List<Quaternion> restLocalRotations, int parentIndex, JiggleRigData lastJiggleRig, Vector3 lastPosition, float currentLength, out int newIndex) {
        if (t == null) {
            newIndex = -1;
            return;
        }
        if (Application.isPlaying && GetJiggleTreeSegmentByBone(t, out JiggleTreeSegment currentJiggleTreeSegment)) {
            lastJiggleRig = currentJiggleTreeSegment.jiggleRigData;
        }
        if (!lastJiggleRig.GetIsExcluded(t)) {
            var validChildrenCount = lastJiggleRig.GetValidChildrenCount(t);
            var currentPosition = t.position;
            var cache = lastJiggleRig.GetCache(t);
            if (Vector3.Distance(t.position, lastPosition) < MERGE_DISTANCE) {
                if (validChildrenCount > 0) {
                    for (int i = 0; i < validChildrenCount; i++) {
                        var child = lastJiggleRig.GetValidChild(t, i);
                        Visit(child, transforms, points, parameters, restLocalPositions, restLocalRotations, parentIndex, lastJiggleRig, lastPosition, currentLength, out int childIndex);
                        if (childIndex != -1) {
                            var record = points[parentIndex];
                            AddChildToPoint(ref record, childIndex);
                            points[parentIndex] = record;
                        }
                    }
                    newIndex = -1;
                } else {
                    transforms.Add(t);
                    restLocalPositions.Add(cache.restLocalPosition);
                    restLocalRotations.Add(new Quaternion(cache.restLocalRotation.x, cache.restLocalRotation.y, cache.restLocalRotation.z, cache.restLocalRotation.w));
                    var projDir = currentPosition - lastPosition;
                    if (projDir.sqrMagnitude < MERGE_DISTANCE * MERGE_DISTANCE) {
                        var parentPoint = points[parentIndex];
                        if (parentPoint.parentIndex >= 0) {
                            projDir = (Vector3)(parentPoint.position - points[parentPoint.parentIndex].position);
                        }
                        if (projDir.sqrMagnitude < MERGE_DISTANCE * MERGE_DISTANCE) {
                            projDir = t.up * 0.1f;
                        }
                    }
                    var tipPos = currentPosition + projDir;
                    points.Add(new JiggleSimulatedPoint() { // virtual projected tip
                        position = tipPos,
                        lastPosition = tipPos,
                        childrenCount = 0,
                        distanceFromRoot = currentLength,
                        parentIndex = parentIndex,
                        hasTransform = false,
                        animated = false,
                    });
                    parameters.Add(lastJiggleRig.GetJiggleBoneParameter(cache.normalizedDistanceFromRoot));
                    var record = points[parentIndex];
                    AddChildToPoint(ref record, points.Count - 1);
                    points[parentIndex] = record;
                    newIndex = points.Count - 1;
                }
                return;
            }
            transforms.Add(t);
            restLocalPositions.Add(cache.restLocalPosition);
            restLocalRotations.Add(new Quaternion(cache.restLocalRotation.x, cache.restLocalRotation.y, cache.restLocalRotation.z, cache.restLocalRotation.w));
            var parameter = lastJiggleRig.GetJiggleBoneParameter(cache.normalizedDistanceFromRoot);
            if ((lastJiggleRig.excludeRoot && t == lastJiggleRig.rootBone) || lastJiggleRig.GetIsExcluded(t)) {
                parameter = new JigglePointParameters() {
                    angleElasticity = 1f,
                    lengthElasticity = 1f,
                    rootElasticity = 1f,
                    elasticitySoften = 0f
                };
            }

            if (points[parentIndex].hasTransform) {
                currentLength += Vector3.Distance(lastPosition, t.position);
            }
            

            points.Add(new JiggleSimulatedPoint() { // Regular point
                position = currentPosition,
                lastPosition = currentPosition,
                childrenCount = 0,
                distanceFromRoot = currentLength,
                parentIndex = parentIndex,
                hasTransform = true,
                animated = true,
            });
            parameters.Add(parameter);
            newIndex = points.Count - 1;
            
            if (validChildrenCount == 0) {
                transforms.Add(t);
                restLocalPositions.Add(cache.restLocalPosition);
                restLocalRotations.Add(new Quaternion(cache.restLocalRotation.x, cache.restLocalRotation.y, cache.restLocalRotation.z, cache.restLocalRotation.w));
                points.Add(new JiggleSimulatedPoint() { // virtual projected tip
                    position = currentPosition + (currentPosition - lastPosition),
                    lastPosition = currentPosition + (currentPosition - lastPosition),
                    childrenCount = 0,
                    distanceFromRoot = currentLength,
                    parentIndex = newIndex,
                    hasTransform = false,
                    animated = false,
                });
                parameters.Add(lastJiggleRig.GetJiggleBoneParameter(cache.normalizedDistanceFromRoot));
                var record = points[newIndex];
                AddChildToPoint(ref record, points.Count - 1);
                points[newIndex] = record;
            } else {
                for (int i = 0; i < validChildrenCount; i++) {
                    var child = lastJiggleRig.GetValidChild(t, i);
                    Visit(child, transforms, points, parameters, restLocalPositions, restLocalRotations, newIndex, lastJiggleRig, currentPosition, currentLength, out int childIndex);
                    if (childIndex != -1) {
                        var record = points[newIndex];
                        AddChildToPoint(ref record, childIndex);
                        points[newIndex] = record;
                    }
                }
            }
        } else {
            newIndex = -1;
        }

    }

    private static unsafe void AddChildToPoint(ref JiggleSimulatedPoint point, int childIndex) {
        if (point.childrenCount>=JiggleSimulatedPoint.MAX_CHILDREN) {
            Debug.LogWarning($"JigglePhysics: Bone exceeded maximum of {JiggleSimulatedPoint.MAX_CHILDREN} children, extra children will be ignored.");
            return;
        }
        point.childrenIndices[point.childrenCount] = childIndex;
        point.childrenCount++;
    }
    
    public static void ScheduleRemoveJiggleTree(JiggleTree jiggleTree) {
        jobs?.ScheduleRemove(jiggleTree);
    }
    
    public static void RemoveJiggleTreeSegment(JiggleTreeSegment jiggleTreeSegment) {
        if (rootJiggleTreeSegments.Contains(jiggleTreeSegment)) {
            rootJiggleTreeSegments.Remove(jiggleTreeSegment);
        }

        jiggleRootLookup.Remove(jiggleTreeSegment.transform);

        // Re-parent orphaned children to the removed segment's parent
        foreach (var kvp in jiggleRootLookup) {
            if (kvp.Value.parent != jiggleTreeSegment) continue;
            kvp.Value.SetParent(jiggleTreeSegment.parent);
            if (kvp.Value.parent == null && !rootJiggleTreeSegments.Contains(kvp.Value)) {
                rootJiggleTreeSegments.Add(kvp.Value);
            }
        }

        jiggleTreeSegment.SetDirty();

        if (jiggleTreeSegment.parent != null) {
            jiggleTreeSegment.parent.SetDirty();
            jiggleTreeSegment.SetParent(null);
        }

        SetGlobalDirty();
    }

}

}