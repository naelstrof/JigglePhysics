using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace GatorDragonGames.JigglePhysics
{
    [Serializable]
    public struct JigglePointParameters
    {
        public float rootElasticity;
        public float angleElasticity;
        public bool angleLimited;
        public float angleLimit;
        public float angleLimitSoften;
        public float lengthElasticity;
        public float elasticitySoften;
        public float gravityMultiplier;
        public float blend;
        public float airDrag;
        public float drag;
        public float ignoreRootMotion;
        public float collisionRadius;
    }

    /// <summary>
    /// Editor-facing curve + multiplier.
    /// </summary>
    [Serializable]
    public struct JiggleTreeCurvedFloat
    {
        public float value;
        public bool curveEnabled;
        public AnimationCurve curve;

        private static readonly AnimationCurve kUnitCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float t01) => curveEnabled ? value * curve.Evaluate(t01) : value;

        public JiggleTreeCurvedFloat(float value)
        {
            this.value = value;
            curveEnabled = false;
            curve = kUnitCurve;
        }

        public void Ensure01() => value = Mathf.Clamp01(value);
        public void EnsureNonNegative() { if (value < 0f) value = 0f; }
        public void EnsureCurveValid() { if (curve == null) curve = kUnitCurve; }
    }

    /// <summary>
    /// Baked runtime scalar. Either constant, or a small LUT sampled on [0..1].
    /// </summary>
    [Serializable]
    public struct BakedScalar01
    {
        [SerializeField] private bool isConstant;
        [SerializeField] private float constant;

        // Quantized LUT for fast sampling. (Optional) Use half precision via ushort if you want.
        [SerializeField] private float[] lut; // size = resolution

        [SerializeField] private int resolution;

        public bool IsValid;

        public void Bake(JiggleTreeCurvedFloat src, int resolution, float minClamp = float.NegativeInfinity, float maxClamp = float.PositiveInfinity)
        {
            this.resolution = Mathf.Max(2, resolution);

            // If curve disabled, store as constant (no LUT allocation).
            if (!src.curveEnabled)
            {
                isConstant = true;
                constant = Mathf.Clamp(src.value, minClamp, maxClamp);
                lut = null;
                return;
            }

            isConstant = false;
            constant = 0f;

            if (lut == null || lut.Length != this.resolution)
                lut = new float[this.resolution];

            // Sample on [0..1] inclusive.
            float inv = 1f / (this.resolution - 1);
            for (int i = 0; i < this.resolution; i++)
            {
                float t = i * inv;
                float v = src.value * src.curve.Evaluate(t);
                lut[i] = Mathf.Clamp(v, minClamp, maxClamp);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Sample(float t01)
        {
            if (isConstant) return constant;

            // assume t01 already clamped by caller
            float x = t01 * (resolution - 1);
            int i = (int)x;
            int j = (i < resolution - 1) ? (i + 1) : i;
            float a = x - i;
            return lut[i] + (lut[j] - lut[i]) * a;
        }
    }

    /// <summary>
    /// A baked LUT of final JigglePointParameters over normalized distance.
    /// Runtime: index + lerp. No curves. Minimal branching.
    /// </summary>
    [Serializable]
    public struct BakedJiggleParamsLUT
    {
        [SerializeField] private int resolution;
        [SerializeField] private JigglePointParameters[] samples; // size = resolution

        public bool IsValid;

        public void EnsureAllocated(int res)
        {
            resolution = Mathf.Max(2, res);
            if (samples == null || samples.Length != resolution)
                samples = new JigglePointParameters[resolution];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly JigglePointParameters Sample(float t01)
        {
            float x = t01 * (resolution - 1);
            int i = (int)x;
            int j = (i < resolution - 1) ? (i + 1) : i;
            float a = x - i;

            ref readonly JigglePointParameters p0 = ref samples[i];
            ref readonly JigglePointParameters p1 = ref samples[j];

            JigglePointParameters o = p0;
            o.rootElasticity = p0.rootElasticity + (p1.rootElasticity - p0.rootElasticity) * a;
            o.angleElasticity = p0.angleElasticity + (p1.angleElasticity - p0.angleElasticity) * a;
            o.angleLimit = p0.angleLimit + (p1.angleLimit - p0.angleLimit) * a;
            o.angleLimitSoften = p0.angleLimitSoften + (p1.angleLimitSoften - p0.angleLimitSoften) * a;
            o.lengthElasticity = p0.lengthElasticity + (p1.lengthElasticity - p0.lengthElasticity) * a;
            o.elasticitySoften = p0.elasticitySoften + (p1.elasticitySoften - p0.elasticitySoften) * a;
            o.gravityMultiplier = p0.gravityMultiplier + (p1.gravityMultiplier - p0.gravityMultiplier) * a;
            o.blend = p0.blend + (p1.blend - p0.blend) * a;
            o.airDrag = p0.airDrag + (p1.airDrag - p0.airDrag) * a;
            o.drag = p0.drag + (p1.drag - p0.drag) * a;
            o.ignoreRootMotion = p0.ignoreRootMotion + (p1.ignoreRootMotion - p0.ignoreRootMotion) * a;
            o.collisionRadius = p0.collisionRadius + (p1.collisionRadius - p0.collisionRadius) * a;
            return o;
        }

        public void SetSample(int index, JigglePointParameters p) => samples[index] = p;
    }

    [Serializable]
    public struct JiggleTreeInputParameters
    {
        public bool advancedToggle;
        public bool collisionToggle;
        public bool angleLimitToggle;

        public JiggleTreeCurvedFloat stiffness;       // 0..1
        public float soften;                          // 0..1
        public JiggleTreeCurvedFloat angleLimit;      // 0..1
        public float angleLimitSoften;                // 0..1
        public float rootStretch;                     // 0..1
        public float ignoreRootMotion;                // 0..1
        public JiggleTreeCurvedFloat stretch;         // 0..1
        public JiggleTreeCurvedFloat drag;            // 0..1
        public JiggleTreeCurvedFloat airDrag;         // 0..1
        public JiggleTreeCurvedFloat gravity;         // arbitrary
        public JiggleTreeCurvedFloat collisionRadius; // >= 0
        public float blend;                           // 0..1

        [Header("Bake")]
        [SerializeField, Min(2)] private int bakeResolution; // e.g. 32/64/128

        // Persist baked data so runtime doesn’t rebake.
        [SerializeField, HideInInspector] private BakedJiggleParamsLUT baked;

        // Optionally store baked scalars too (useful if you prefer “compute at runtime but no curves”).
        // Not strictly needed if you bake final parameters LUT.
        [SerializeField, HideInInspector] private BakedScalar01 bakedStiffness;
        [SerializeField, HideInInspector] private BakedScalar01 bakedDrag;
        [SerializeField, HideInInspector] private BakedScalar01 bakedAirDrag;
        [SerializeField, HideInInspector] private BakedScalar01 bakedGravity;
        [SerializeField, HideInInspector] private BakedScalar01 bakedStretch;
        [SerializeField, HideInInspector] private BakedScalar01 bakedAngleLimit;
        [SerializeField, HideInInspector] private BakedScalar01 bakedCollisionRadius;

        // Cached constants derived from non-curved values (so runtime doesn’t recompute them).
        [SerializeField, HideInInspector] private float cachedSoftenSq;
        [SerializeField, HideInInspector] private float cachedRootElasticity;
        [SerializeField, HideInInspector] private float cachedIgnoreRootMotion;
        [SerializeField, HideInInspector] private float cachedBlend;
        [SerializeField, HideInInspector] private bool cachedAngleLimited;
        [SerializeField, HideInInspector] private float cachedAngleLimitSoften;

        /// <summary>
        /// Fast hot path: samples baked LUT.
        /// Caller passes normalizedDistanceFromRoot.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JigglePointParameters ToJigglePointParameters(float normalizedDistanceFromRoot)
        {
            // If baked is valid, this is the fastest path.
            if (baked.IsValid == false)
            {
                OnValidate();
            }
            return baked.Sample(normalizedDistanceFromRoot);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private JigglePointParameters SlowCompute(float t)
        {
            bool adv = advancedToggle;

            float stiff = stiffness.Evaluate(t);
            float dragVal = drag.Evaluate(t);
            float airVal = airDrag.Evaluate(t);
            float gravVal = gravity.Evaluate(t);

            float stretchVal = adv ? stretch.Evaluate(t) : 0f;
            float angleLimitVal = angleLimitToggle ? angleLimit.Evaluate(t) : 0f;
            float collisionVal = (collisionToggle && adv) ? collisionRadius.Evaluate(t) : 0f;

            float stiffSq = stiff * stiff;
            float oneMinusStr = 1f - stretchVal;
            float lengthElast = adv ? (oneMinusStr * oneMinusStr) : 1f;
            float softenSq = adv ? (soften * soften) : 0f;

            return new JigglePointParameters
            {
                rootElasticity = adv ? (1f - rootStretch) : 1f,
                angleElasticity = stiffSq,
                lengthElasticity = lengthElast,
                elasticitySoften = softenSq,
                ignoreRootMotion = adv ? ignoreRootMotion : 0f,
                gravityMultiplier = gravVal,
                angleLimited = angleLimitToggle,
                angleLimit = angleLimitVal,
                angleLimitSoften = angleLimitSoften,
                blend = 1f,
                drag = dragVal,
                airDrag = airVal,
                collisionRadius = collisionVal
            };
        }

        public static JiggleTreeInputParameters Default()
        {
            return new JiggleTreeInputParameters
            {
                stiffness = new JiggleTreeCurvedFloat(0.8f),
                angleLimit = new JiggleTreeCurvedFloat(0.5f),
                stretch = new JiggleTreeCurvedFloat(0.1f),
                rootStretch = 0f,
                drag = new JiggleTreeCurvedFloat(0.1f),
                airDrag = new JiggleTreeCurvedFloat(0f),
                ignoreRootMotion = 0f,
                gravity = new JiggleTreeCurvedFloat(1f),
                collisionRadius = new JiggleTreeCurvedFloat(0.1f),
                soften = 0f,
                angleLimitSoften = 0f,
                blend = 1f,
                bakeResolution = 64
            };
        }

        /// <summary>
        /// Call this from the owning MonoBehaviour/ScriptableObject OnValidate().
        /// Structs don’t get OnValidate automatically unless owned & forwarded.
        /// </summary>
        public void OnValidate()
        {
            // --- sanitize editor inputs ---
            collisionRadius.EnsureNonNegative();

            stiffness.Ensure01();
            angleLimit.Ensure01();
            drag.Ensure01();
            airDrag.Ensure01();
            stretch.Ensure01();

            stiffness.EnsureCurveValid();
            angleLimit.EnsureCurveValid();
            drag.EnsureCurveValid();
            airDrag.EnsureCurveValid();
            gravity.EnsureCurveValid();
            stretch.EnsureCurveValid();
            collisionRadius.EnsureCurveValid();

            rootStretch = Mathf.Clamp01(rootStretch);
            ignoreRootMotion = Mathf.Clamp01(ignoreRootMotion);
            soften = Mathf.Clamp01(soften);
            angleLimitSoften = Mathf.Clamp01(angleLimitSoften);
            blend = Mathf.Clamp01(blend);

            bakeResolution = Mathf.Clamp(bakeResolution == 0 ? 64 : bakeResolution, 2, 512);

            // --- cache non-curved derived values ---
            cachedSoftenSq = advancedToggle ? (soften * soften) : 0f;
            cachedRootElasticity = advancedToggle ? (1f - rootStretch) : 1f;
            cachedIgnoreRootMotion = advancedToggle ? ignoreRootMotion : 0f;
            cachedBlend = 1f; // you currently always set blend = 1f in output; keep it consistent
            cachedAngleLimited = angleLimitToggle;
            cachedAngleLimitSoften = angleLimitSoften;

            // --- bake scalars (optional, but useful if you prefer scalar sampling elsewhere) ---
            bakedStiffness.Bake(stiffness, bakeResolution, 0f, 1f);
            bakedDrag.Bake(drag, bakeResolution, 0f, 1f);
            bakedAirDrag.Bake(airDrag, bakeResolution, 0f, 1f);
            bakedGravity.Bake(gravity, bakeResolution); // arbitrary range
            bakedStretch.Bake(stretch, bakeResolution, 0f, 1f);
            bakedAngleLimit.Bake(angleLimit, bakeResolution, 0f, 1f);
            bakedCollisionRadius.Bake(collisionRadius, bakeResolution, 0f, float.PositiveInfinity);

            // --- bake FINAL params LUT (fastest runtime) ---
            baked.EnsureAllocated(bakeResolution);

            float inv = 1f / (bakeResolution - 1);
            bool adv = advancedToggle;
            bool angleLimited = angleLimitToggle;
            bool collisions = collisionToggle && adv;

            for (int i = 0; i < bakeResolution; i++)
            {
                float t = i * inv;

                float stiff = bakedStiffness.Sample(t);
                float stiffSq = stiff * stiff;

                float stretchVal = adv ? bakedStretch.Sample(t) : 0f;
                float oneMinusStr = 1f - stretchVal;
                float lengthElast = adv ? (oneMinusStr * oneMinusStr) : 1f;

                float angleLimitVal = angleLimited ? bakedAngleLimit.Sample(t) : 0f;
                float collisionVal = collisions ? bakedCollisionRadius.Sample(t) : 0f;

                baked.SetSample(i, new JigglePointParameters
                {
                    rootElasticity = cachedRootElasticity,
                    angleElasticity = stiffSq,
                    lengthElasticity = lengthElast,
                    elasticitySoften = cachedSoftenSq,
                    ignoreRootMotion = cachedIgnoreRootMotion,
                    gravityMultiplier = bakedGravity.Sample(t),
                    angleLimited = angleLimited,
                    angleLimit = angleLimitVal,
                    angleLimitSoften = cachedAngleLimitSoften,
                    blend = cachedBlend,
                    drag = bakedDrag.Sample(t),
                    airDrag = bakedAirDrag.Sample(t),
                    collisionRadius = collisionVal
                });
            }
            baked.IsValid = true;
        }
    }
}
