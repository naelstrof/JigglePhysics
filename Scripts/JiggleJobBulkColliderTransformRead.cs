using Unity.Burst;
using Unity.Collections;
using UnityEngine.Jobs;

namespace GatorDragonGames.JigglePhysics {

[BurstCompile]
public struct JiggleJobBulkColliderTransformRead : IJobParallelForTransform {
    public NativeArray<JiggleCollider> colliders;

    public JiggleJobBulkColliderTransformRead(NativeArray<JiggleCollider> colliders) {
        this.colliders = colliders;
    }

    public void UpdateArrays(NativeArray<JiggleCollider> colliders) {
        this.colliders = colliders;
    }

    public void Execute(int index, TransformAccess transform) {
        var collider = colliders[index];
        if (!transform.isValid || !collider.enabled) {
            return;
        }
        collider.Read(transform);
        colliders[index] = collider;
    }
}

}