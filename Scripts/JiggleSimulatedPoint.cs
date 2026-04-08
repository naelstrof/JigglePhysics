using System;
using Unity.Mathematics;

namespace GatorDragonGames.JigglePhysics {

public unsafe struct JiggleSimulatedPoint {
    public const int MAX_CHILDREN = 32;

    // Generated at runtime
    public float3 lastPosition;
    public float3 position;
    public float3 workingPosition;
    public float3 parentPose;
    public float3 pose;
    public float desiredLengthToParent;
    public bool animated;
    public float worldRadius;
    //public float3 debug;

    // Set at initialization
    public float distanceFromRoot;
    public int parentIndex;
    public fixed int childrenIndices[MAX_CHILDREN];
    public int childrenCount;
    public bool hasTransform;

    private static bool GetIsValid(float3 vector) {
        return !float.IsNaN(vector.x) && !float.IsNaN(vector.y) && !float.IsNaN(vector.z);
    }
    private static bool GetIsValid(float value) {
        return !float.IsNaN(value);
    }

    public bool GetIsValid(int pointCount, out string failReason) {
        if (!GetIsValid(lastPosition)) {
            failReason = "lastPosition is NaN";
            return false;
        }
        if (!GetIsValid(position)) {
            failReason = "position is NaN";
            return false;
        }
        if (!GetIsValid(workingPosition)) {
            failReason = "workingPosition is NaN";
            return false;
        }
        if (!GetIsValid(pose)) {
            failReason = "pose is NaN";
            return false;
        }
        if (!GetIsValid(parentPose)) {
            failReason = "parentPose is NaN";
            return false;
        }
        if (!GetIsValid(desiredLengthToParent)) {
            failReason = "desiredLengthToParent is NaN";
            return false;
        }
        if (!GetIsValid(worldRadius)) {
            failReason = "worldRadius is NaN";
            return false;
        }
        if (!GetIsValid(distanceFromRoot)) {
            failReason = "distanceFromRoot is NaN";
            return false;
        }
        if (childrenCount < 0 || childrenCount > MAX_CHILDREN) {
            failReason = "childrenCount is outside range";
            return false;
        }
        if (parentIndex < -1 || parentIndex >= pointCount) {
            failReason = "parentIndex is outside range";
            return false;
        }
        for (int i = 0; i < childrenCount; i++) {
            int childIndex = childrenIndices[i];
            if (childIndex < 0 || childIndex >= pointCount) {
                failReason = "childrenIndices is outside range";
                return false;
            }
        }
        failReason = "All good!";
        return true;
    }

    public override string ToString() {
        return $"(position: {position},\nlastPosition: {lastPosition},\n" +
               $"workingPosition: {workingPosition},\n" +
               $"parentPose: {parentPose},\npose: {pose},\ndesiredLengthToParent:{desiredLengthToParent},\n" +
               $"animated: {animated},\n parentIndex: {parentIndex},\n " +
               $"children: [{childrenIndices[0]}, ...],\n childrenCount: {childrenCount},\n hasTransform: {hasTransform})";
    }

    public void Sanitize() {
        if (!GetIsValid(lastPosition)) {
            lastPosition = float3.zero;
        }
        if (!GetIsValid(position)) {
            position = float3.zero;
        }
        if (!GetIsValid(workingPosition)) {
            workingPosition = float3.zero;
        }
        if (!GetIsValid(pose)) {
            pose = float3.zero;
        }
        if (!GetIsValid(parentPose)) {
            parentPose = float3.zero;
        }
        if (!GetIsValid(desiredLengthToParent)) {
            desiredLengthToParent = 0.1f;
        }
        if (!GetIsValid(worldRadius)) {
            worldRadius = 0.1f;
        }
        if (!GetIsValid(distanceFromRoot)) {
            distanceFromRoot = 0.1f;
        }
    }
}

}
