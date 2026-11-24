using Unity.Mathematics;

namespace GatorDragonGames.JigglePhysics {

public struct JiggleTransform {
    public bool isVirtual;
    public float3 position;
    public quaternion rotation;
    public float3 scale;
    public float blend;

    public static JiggleTransform Lerp(JiggleTransform a, JiggleTransform b, float t) {
        return new JiggleTransform() {
            isVirtual = a.isVirtual,
            position = math.lerp(a.position, b.position, t),
            rotation = math.slerp(a.rotation, b.rotation, t),
            scale = math.lerp(a.scale, b.scale, t),
            blend = math.lerp(a.blend, b.blend, t),
        };
    }

    public override string ToString() {
        return $"Virtual: {isVirtual}, Position: {position}, Quaternion: {rotation}, Scale: {scale}, Blend: {blend}";
    }
}

}