using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace GatorDragonGames.JigglePhysics {
public unsafe struct JiggleTreeJobData {
    public bool Equals(JiggleTreeJobData other) {
        return GetHashCode() == other.GetHashCode();
    }

    public override bool Equals(object obj) {
        return obj is JiggleTreeJobData other && Equals(other);
    }

    public override int GetHashCode() {
        return unchecked((int)(long)points);
    }

    public static bool operator ==(JiggleTreeJobData left, JiggleTreeJobData right) => left.Equals(right);
    public static bool operator !=(JiggleTreeJobData left, JiggleTreeJobData right) => !left.Equals(right);

    public int rootID;
    public uint pointCount;
    public uint transformIndexOffset;
    public uint colliderIndexOffset;
    public uint colliderCount;
    public float extents;
    public JiggleSimulatedPoint* points;
    public JigglePointParameters* parameters;
    private const int MAX_POINTS = 10000;

    public JiggleTreeJobData(int rootID, int transformIndexOffset, int colliderIndexOffset, int colliderCount, JiggleSimulatedPoint[] inputPoints, JigglePointParameters[] inputParameters) {
        this.rootID = rootID;
        pointCount = (uint)inputPoints.Length;
        this.colliderIndexOffset = (uint)colliderIndexOffset;
        this.transformIndexOffset = (uint)transformIndexOffset;
        this.colliderCount = (uint)colliderCount;
        points = (JiggleSimulatedPoint*)UnsafeUtility.Malloc(
            Marshal.SizeOf<JiggleSimulatedPoint>() * pointCount,
            UnsafeUtility.AlignOf<JiggleSimulatedPoint>(),
            Allocator.Persistent
        );
        parameters = (JigglePointParameters*)UnsafeUtility.Malloc(
            Marshal.SizeOf<JigglePointParameters>() * pointCount,
            UnsafeUtility.AlignOf<JigglePointParameters>(),
            Allocator.Persistent
        );
        fixed (JiggleSimulatedPoint* src = inputPoints) {
            UnsafeUtility.MemCpy(points, src, sizeof(JiggleSimulatedPoint) * pointCount);
        }
        fixed (JigglePointParameters* src = inputParameters) {
            UnsafeUtility.MemCpy(parameters, src, sizeof(JigglePointParameters) * pointCount);
        }
        extents = 1f;
    }

    public void Set(int rootID, JiggleSimulatedPoint[] inputPoints, JigglePointParameters[] inputParameters) {
        this.rootID = rootID;
        if (inputPoints.Length != pointCount) {
            Dispose();
            pointCount = (uint)inputPoints.Length;
            points = (JiggleSimulatedPoint*)UnsafeUtility.Malloc(
                Marshal.SizeOf<JiggleSimulatedPoint>() * pointCount,
                UnsafeUtility.AlignOf<JiggleSimulatedPoint>(),
                Allocator.Persistent
            );
            parameters = (JigglePointParameters*)UnsafeUtility.Malloc(
                Marshal.SizeOf<JigglePointParameters>() * pointCount,
                UnsafeUtility.AlignOf<JigglePointParameters>(),
                Allocator.Persistent
            );
        }
        fixed (JiggleSimulatedPoint* src = inputPoints) {
            UnsafeUtility.MemCpy(points, src, sizeof(JiggleSimulatedPoint) * pointCount);
        }
        fixed (JigglePointParameters* src = inputParameters) {
            UnsafeUtility.MemCpy(parameters, src, sizeof(JigglePointParameters) * pointCount);
        }
    }

    public void SetParameters(JigglePointParameters[] inputParameters) {
        Assert.AreEqual(pointCount, inputParameters.Length);
        fixed (JigglePointParameters* src = inputParameters) {
            UnsafeUtility.MemCpy(parameters, src, sizeof(JigglePointParameters) * pointCount);
        }
    }

    public void Dispose() {
        if (points != null) {
            JigglePhysics.FreeOnComplete((IntPtr)points);
            points = null;
        }
        if (parameters != null) {
            JigglePhysics.FreeOnComplete((IntPtr)parameters);
            parameters = null;
        }
    }

    public void OnDrawGizmosSelected() {
        for (int i = 0; i < pointCount; i++) {
            var point = points[i];
            if (point.hasTransform) {
                Gizmos.DrawWireSphere(point.position, point.worldRadius);
            } else {
                if (point.parentIndex == -1) {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(point.pose, 0.05f);
                } else {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(point.position, 0.05f);
                }
            }
            if (point.childrenCount != 0) {
                var child = points[point.childrenIndices[0]];
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(point.position, child.position);
            }
        }
    }

    public void Sanitize() {
        for (int i = 0; i < pointCount; i++) {
            points[i].Sanitize();
        }
    }

    public bool GetIsValid(out string failReason) {
        if (pointCount == 0 || pointCount > 10000) {
            failReason = $"Invalid point count {pointCount}";
            return false;
        }
        if (points == null) {
            failReason = "Points pointer is null";
            return false;
        }
        if (parameters == null) {
            failReason = "Parameters pointer is null";
            return false;
        }
        for (int i = 0; i < pointCount; i++) {
            var point = points[i];
            if (!point.GetIsValid((int)pointCount, out failReason)) {
                return false;
            }
        }

        failReason = "All good!";
        return true;
    }
}
}