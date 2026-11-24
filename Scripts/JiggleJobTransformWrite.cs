using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace GatorDragonGames.JigglePhysics {

[BurstCompile]
public struct JiggleJobTransformWrite : IJobParallelForTransform {
    public NativeArray<JiggleTransform> previousLocalPoses;
    [ReadOnly] public NativeArray<JiggleTransform> inputInterpolatedPoses;

    public JiggleJobTransformWrite(JiggleMemoryBus bus) {
        previousLocalPoses = bus.previousLocalRestPoseTransforms;
        inputInterpolatedPoses = bus.interpolationOutputPoses;
    }

    public void UpdateArrays(JiggleMemoryBus bus) {
        previousLocalPoses = bus.previousLocalRestPoseTransforms;
        inputInterpolatedPoses = bus.interpolationOutputPoses;
    }

    public void Execute(int index, TransformAccess transform) {
        if (!transform.isValid) {
            return;
        }

        var pose = inputInterpolatedPoses[index];
        if (pose.isVirtual) {
            return;
        }
        if (pose.blend <= 0f) {
            return;
        }

        transform.GetPositionAndRotation(out var originalPosition, out var originalRotation);
        transform.SetPositionAndRotation(math.lerp(originalPosition, pose.position, pose.blend), math.slerp(originalRotation, pose.rotation, pose.blend));
        transform.GetLocalPositionAndRotation(out var localPosition, out var localRotation);

        var previousLocalPose = previousLocalPoses[index];
        previousLocalPose.position = localPosition;
        previousLocalPose.rotation = localRotation;
        previousLocalPoses[index] = previousLocalPose;
    }
}

}