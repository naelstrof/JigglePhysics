using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace GatorDragonGames.JigglePhysics {

[Serializable]
public struct JiggleColliderSerializable {
    public Transform transform;
    public JiggleCollider collider;

    public void OnDrawGizmosSelected() {
        if (transform == null) {
            return;
        }
        var position = transform.position;
        collider.Read(transform);
        Gizmos.color = new Color(0.854902f, 0.6470588f, 0.1254902f, 1f);
        switch (collider.type) {
            case JiggleCollider.JiggleColliderType.Sphere:
                Gizmos.DrawWireSphere(position, collider.worldRadius);
            break;
            case JiggleCollider.JiggleColliderType.Capsule: {
                var axisDir = (Vector3)collider.GetWorldAxis();
                var halfHeight = collider.worldHeight * 0.5f;
                var r = collider.worldRadius;
                var top = position + axisDir * halfHeight;
                var bottom = position - axisDir * halfHeight;
                Gizmos.DrawWireSphere(top, r);
                Gizmos.DrawWireSphere(bottom, r);
                // Draw connecting lines
                var right = Vector3.Cross(axisDir, Vector3.forward).normalized;
                if (right.magnitude < 0.01f) right = Vector3.Cross(axisDir, Vector3.right).normalized;
                var forward = Vector3.Cross(axisDir, right).normalized;
                Gizmos.DrawLine(top + right * r, bottom + right * r);
                Gizmos.DrawLine(top - right * r, bottom - right * r);
                Gizmos.DrawLine(top + forward * r, bottom + forward * r);
                Gizmos.DrawLine(top - forward * r, bottom - forward * r);
            }
            break;
            case JiggleCollider.JiggleColliderType.Plane: {
                var up = ((Vector4)collider.localToWorldMatrix.c1).normalized;
                var upDir = new Vector3(up.x, up.y, up.z);
                var right = Vector3.Cross(upDir, Vector3.forward).normalized;
                if (right.magnitude < 0.01f) right = Vector3.Cross(upDir, Vector3.right).normalized;
                var forward = Vector3.Cross(upDir, right).normalized;
                var size = 2f;
                var p1 = position + (right + forward) * size;
                var p2 = position + (right - forward) * size;
                var p3 = position + (-right - forward) * size;
                var p4 = position + (-right + forward) * size;
                Gizmos.DrawLine(p1, p2);
                Gizmos.DrawLine(p2, p3);
                Gizmos.DrawLine(p3, p4);
                Gizmos.DrawLine(p4, p1);
                Gizmos.DrawLine(p1, p3);
                Gizmos.DrawLine(p2, p4);
                // Draw normal arrow
                Gizmos.DrawLine(position, position + upDir * size * 0.5f);
            }
            break;
            case JiggleCollider.JiggleColliderType.Box: {
                var oldMatrix = Gizmos.matrix;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, (Vector3)(collider.boxExtents * 2f));
                Gizmos.matrix = oldMatrix;
            }
            break;
        }
    }
}

[Serializable]
public struct JiggleCollider {
    public enum JiggleColliderType {
        Sphere,
        Capsule,
        Plane,
        Box
    }

    public enum CapsuleAxis {
        X,
        Y,
        Z
    }

    [NonSerialized] public bool enabled;

    public JiggleColliderType type;

    public float radius;
    [NonSerialized] public float worldRadius;

    public float height;
    [NonSerialized] public float worldHeight;

    public CapsuleAxis capsuleAxis;

    public float3 boxExtents;
    [NonSerialized] public float3 worldBoxExtents;

    [NonSerialized] public float4x4 localToWorldMatrix;
    private float AverageScale(float4x4 matrix) {
        float sx = math.length(matrix.c0.xyz);
        float sy = math.length(matrix.c1.xyz);
        float sz = math.length(matrix.c2.xyz);
        return (sx + sy + sz) / 3f;
    }

    public float3 GetWorldAxis() {
        float3 col;
        switch (capsuleAxis) {
            case CapsuleAxis.X: col = localToWorldMatrix.c0.xyz; break;
            case CapsuleAxis.Z: col = localToWorldMatrix.c2.xyz; break;
            default: col = localToWorldMatrix.c1.xyz; break;
        }
        return math.normalizesafe(col, new float3(0, 1, 0));
    }

    public void Read(Transform transform) {
        Read(transform.localToWorldMatrix);
    }
    public void Read(TransformAccess transform) {
        Read(transform.localToWorldMatrix);
    }
    public void Read(float4x4 matrix) {
        localToWorldMatrix = matrix;
        var averageScale = AverageScale(localToWorldMatrix);
        worldRadius = math.max(0f, radius) * averageScale;
        worldHeight = math.max(0f, height) * averageScale;
        worldBoxExtents = new float3(
            math.max(0f, boxExtents.x) * math.length(matrix.c0.xyz),
            math.max(0f, boxExtents.y) * math.length(matrix.c1.xyz),
            math.max(0f, boxExtents.z) * math.length(matrix.c2.xyz)
        );
    }
}

}
