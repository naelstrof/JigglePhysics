using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GatorDragonGames.JigglePhysics {

public class JiggleRig : MonoBehaviour {
    [SerializeField] private JiggleRigData jiggleRigData;
    [SerializeField, Tooltip("Whether to check if parameters have been changed each frame.")] private bool animatedParameters = false;
    
    [NonSerialized] private JiggleTreeSegment segment;
    
    private static List<JigglePointParameters> parametersCache;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize() {
        parametersCache = new();
    }

    public JiggleRigData GetJiggleRigData() => jiggleRigData;

    public JiggleTreeInputParameters GetInputParameters() => jiggleRigData.jiggleTreeInputParameters;

    #if !JIGGLEPHYSICS_DISABLE_ON_ENABLE
    private void OnEnable() {
        if (jiggleRigData.rootBone == null) {
            throw new UnityException("Jiggle Rig enabled without a root bone assigned!");
        }

        jiggleRigData.RegenerateCacheLookup();

        segment ??= new JiggleTreeSegment(this);
        segment.SetDirty();
        JigglePhysics.AddJiggleTreeSegment(segment);
    }
    #endif
    
    #if !JIGGLEPHYSICS_DISABLE_ON_DISABLE
    private void OnDisable() {
        if (segment != null) {
            JigglePhysics.RemoveJiggleTreeSegment(segment);
        }
    }
    #endif

    public void OnInitialize() {
        OnEnable();
    }

    public void OnRemove() {
        OnDisable();
    }

    /// <summary>
    /// Immediately resamples the rest pose of the bones in the tree. This can be useful if you have modified the bones' transforms on initialization and want to control when the rest pose is sampled.
    /// </summary>
    public void ResampleRestPose() {
        segment.jiggleTree.ResampleRestPose();
    }

    /// <summary>
    /// Sends updated parameters to the jiggle tree on the jobs side. Uses the provided list to prevent allocations.
    /// </summary>
    public void UpdateParameters() {
        if (segment == null || segment.jiggleTree == null) {
            return;
        }
        jiggleRigData.UpdateParameters(segment.jiggleTree, parametersCache);
    }
    
    public bool GetHasAnimatedParameters => animatedParameters;

    private void OnValidate() {
        parametersCache ??= new();
        if (!jiggleRigData.hasSerializedData) {
            jiggleRigData = JiggleRigData.Default();
        }
        jiggleRigData.OnValidate();
        if (Application.isPlaying) {
            UpdateParameters();
        }
    }

    private void OnDrawGizmosSelected() {
        if (!isActiveAndEnabled) {
            return;
        }
        jiggleRigData.OnDrawGizmosSelected();
    }

}

}
