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

    [Serializable]
    public struct JiggleTreeCurvedFloat
    {
        // Base multiplier (0..1 unless otherwise noted by the caller)
        public float value;

        public bool curveEnabled;
        public AnimationCurve curve;

        private static readonly AnimationCurve kUnitCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float t01)
        {
            // t01 is assumed clamped by caller
            return curveEnabled ? value * curve.Evaluate(t01) : value;
        }

        public JiggleTreeCurvedFloat(float value)
        {
            this.value = value;
            curveEnabled = false;
            curve = kUnitCurve;
        }

        // Validation helpers let the runtime path stay branch-light.
        public void Ensure01()
        {
            value = Mathf.Clamp01(value);
        }

        public void EnsureNonNegative()
        {
            if (value < 0f) value = 0f;
        }
    }

    [Serializable]
    public struct JiggleTreeInputParameters
    {
        public bool advancedToggle;
        public bool collisionToggle;
        public bool angleLimitToggle;

        public JiggleTreeCurvedFloat stiffness;      // 0..1
        public float soften;                          // 0..1
        public JiggleTreeCurvedFloat angleLimit;     // 0..1
        public float angleLimitSoften;                // 0..1
        public float rootStretch;                     // 0..1
        public float ignoreRootMotion;                // 0..1
        public JiggleTreeCurvedFloat stretch;        // 0..1
        public JiggleTreeCurvedFloat drag;           // 0..1
        public JiggleTreeCurvedFloat airDrag;        // 0..1
        public JiggleTreeCurvedFloat gravity;        // arbitrary
        public JiggleTreeCurvedFloat collisionRadius;// >= 0
        public float blend;                           // 0..1

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JigglePointParameters ToJigglePointParameters(float normalizedDistanceFromRoot)
        {
            // Clamp once up front to keep curves happy and avoid repeated clamps.
            float t = Mathf.Clamp01(normalizedDistanceFromRoot);

            bool adv = advancedToggle;

            float stiff = stiffness.Evaluate(t);
            float dragVal = drag.Evaluate(t);
            float airVal = airDrag.Evaluate(t);
            float gravVal = gravity.Evaluate(t);

            float stretchVal = adv ? stretch.Evaluate(t) : 0f;
            float angleLimitVal = angleLimitToggle ? angleLimit.Evaluate(t) : 0f;
            float collisionVal = (collisionToggle && adv) ? collisionRadius.Evaluate(t) : 0f;

            // Replace Mathf.Pow(x,2) with x*x to save a function call.
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
                blend = 1f
            };
        }

        // Editor-time clamping so the runtime path can assume valid ranges.
        public void OnValidate()
        {
            collisionRadius.EnsureNonNegative();

            stiffness.Ensure01();
            angleLimit.Ensure01();
            drag.Ensure01();
            airDrag.Ensure01();
            stretch.Ensure01();

            rootStretch = Mathf.Clamp01(rootStretch);
            ignoreRootMotion = Mathf.Clamp01(ignoreRootMotion);
            soften = Mathf.Clamp01(soften);
            angleLimitSoften = Mathf.Clamp01(angleLimitSoften);
            blend = Mathf.Clamp01(blend);
        }
    }

}
