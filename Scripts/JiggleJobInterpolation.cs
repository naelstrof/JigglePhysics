using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GatorDragonGames.JigglePhysics {

[BurstCompile]
public struct JiggleJobInterpolation : IJobFor {
    [ReadOnly] public NativeArray<float3> realRootPositions;

    [ReadOnly] public NativeArray<PoseData> previousPoses;
    [ReadOnly] public NativeArray<PoseData> currentPoses;

    public double timeStamp;
    public double previousTimeStamp;
    public double currentTime;

    public NativeArray<JiggleTransform> outputInterpolatedPoses;
    
    public JiggleJobInterpolation(JiggleMemoryBus bus, double time, float fixedDeltaTime) {
        timeStamp = time - fixedDeltaTime;
        previousTimeStamp = timeStamp - fixedDeltaTime;
        currentTime = timeStamp;
        previousPoses = bus.interpolationPreviousPoseData;
        currentPoses = bus.interpolationCurrentPoseData;
        outputInterpolatedPoses = bus.interpolationOutputPoses;
        realRootPositions = bus.rootOutputPositions;
    }

    public void UpdateArrays(JiggleMemoryBus bus) {
        previousPoses = bus.interpolationPreviousPoseData;
        currentPoses = bus.interpolationCurrentPoseData;
        outputInterpolatedPoses = bus.interpolationOutputPoses;
        realRootPositions = bus.rootOutputPositions;
    }

    public void Execute(int index) {
        var prevPose = previousPoses[index];
        var newPose = currentPoses[index];

        var diff = timeStamp - previousTimeStamp;
        if (diff == 0) {
            throw new UnityException($"Time difference is zero ({timeStamp}-{previousTimeStamp}), cannot interpolate.");
        }

        var timeCorrection = (timeStamp - previousTimeStamp) * 2f;
        var t = (currentTime - timeCorrection - previousTimeStamp) / diff;
        var interPose = PoseData.Lerp(prevPose, newPose, (float)t);

        var snapToReal = realRootPositions[index] - interPose.rootPosition;
        interPose.pose.position += (snapToReal + interPose.rootOffset) * interPose.rootSnapStrength;
        outputInterpolatedPoses[index] = interPose.pose;
    }
}

}