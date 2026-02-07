using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace GatorDragonGames.JigglePhysics {

[BurstCompile]
public struct JiggleJobBulkTransformReset : IJobParallelForTransform {
    public NativeArray<JiggleTransform> restPoseTransforms;

    [ReadOnly] public NativeArray<JiggleTransform> previousLocalTransforms;

    public JiggleJobBulkTransformReset(JiggleMemoryBus bus) {
        restPoseTransforms = bus.restPoseTransforms;
        previousLocalTransforms = bus.previousLocalRestPoseTransforms;
    }

    public void UpdateArrays(JiggleMemoryBus bus) {
        restPoseTransforms = bus.restPoseTransforms;
        previousLocalTransforms = bus.previousLocalRestPoseTransforms;
    }

    [Flags]
    private enum ChangeFlags {
        None = 0,
        Position = 1,
        Rotation = 2,
        PositionAndRotation = 3,
    }
    private static ChangeFlags GetChangedFlags(float3 oldPosition, Vector3 newPosition, quaternion oldRotation, Quaternion newRotation) {
        ChangeFlags changed = ChangeFlags.None;
        changed |= (newPosition == (Vector3)oldPosition ? ChangeFlags.None : ChangeFlags.Position);
        changed |= (newRotation == (Quaternion)oldRotation ? ChangeFlags.None : ChangeFlags.Rotation);
        return changed;
    }

    public void Execute(int index, TransformAccess transform) {
        if (!transform.isValid) {
            return;
        }

        transform.GetLocalPositionAndRotation(out var localPosition, out var localRotation);
        var restTransform = restPoseTransforms[index];

        var localTransform = previousLocalTransforms[index];
        if (localTransform.isVirtual) {
            return;
        }
        
        switch(GetChangedFlags(localTransform.position, localPosition, localTransform.rotation, localRotation)) {
            case ChangeFlags.Position:
                transform.localRotation = restTransform.rotation;
                restTransform.position = localPosition;
                restPoseTransforms[index] = restTransform;
                break;
            case ChangeFlags.Rotation:
                transform.localPosition = restTransform.position;
                restTransform.rotation = localRotation;
                restPoseTransforms[index] = restTransform;
                break;
            case ChangeFlags.PositionAndRotation:
                restTransform.position = localPosition;
                restTransform.rotation = localRotation;
                restPoseTransforms[index] = restTransform;
                break;
            case ChangeFlags.None:
                transform.SetLocalPositionAndRotation(restTransform.position, restTransform.rotation);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ChangeFlags), "Unknown ChangeFlags (JiggleJobBulkTransformReset), this should never happen.");
        }
    }

}

}