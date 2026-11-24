using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace GatorDragonGames.JigglePhysics {
public static class JiggleTreeStructExtensions {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JiggleTransform GetInputPose(this JiggleTreeJobData self, NativeArray<JiggleTransform> inputPoses,
        int index) {
        return inputPoses[index + (int)self.transformIndexOffset];
    }

    private static float3 SanitizeOutput(float3 v) {
        if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)) {
            return float3.zero;
        }
        return v;
    }
    
    private static bool IsNormalized(quaternion q, float epsilon = 1e-5f) {
        return math.abs(math.length(q.value) - 1f) < epsilon;
    }
    
    private static quaternion SanitizeOutput(quaternion v) {
        if (float.IsNaN(v.value.x) || float.IsNaN(v.value.y) || float.IsNaN(v.value.z) || float.IsNaN(v.value.w)) {
            return quaternion.identity;
        }
        return v;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteOutputPose(this JiggleTreeJobData self, NativeArray<PoseData> outputPoses, int index, JiggleTransform pose, float3 rootOffset, float3 rootPosition, float rootSnapStrength) {
        var old = outputPoses[index + (int)self.transformIndexOffset];
        if (old.pose.isVirtual) {
            return;
        }

        pose.isVirtual = false;
        pose.position = SanitizeOutput(pose.position);
        pose.scale = SanitizeOutput(pose.scale);
        pose.rotation = SanitizeOutput(pose.rotation);
        pose.blend = math.clamp(pose.blend, 0f, 1f);
        
        old.pose = pose;
        old.rootOffset = SanitizeOutput(rootOffset);
        old.rootPosition = SanitizeOutput(rootPosition);
        old.rootSnapStrength = rootSnapStrength;

        outputPoses[index + (int)self.transformIndexOffset] = old;
    }
}
}