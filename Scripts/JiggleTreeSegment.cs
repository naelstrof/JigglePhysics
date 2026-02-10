using System.Collections.Generic;
using UnityEngine;

namespace GatorDragonGames.JigglePhysics {

public class JiggleTreeSegment {

    public Transform transform { get; private set; }
    public JiggleTree jiggleTree { get; private set; }
    public JiggleTreeSegment parent { get; private set; }
    private IJiggleParameterProvider jiggleProvider;
    public JiggleRigData jiggleRigData => jiggleProvider.GetJiggleRigData();
    
    private static List<JigglePointParameters> parametersCache;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize() {
        parametersCache = new();
    }

    public void SetParent(JiggleTreeSegment jiggleTree) {
        parent?.SetDirty();
        parent = jiggleTree;
        parent?.SetDirty();
        JigglePhysics.SetGlobalDirty();
    }

    public JiggleTreeSegment(IJiggleParameterProvider jiggleProvider) {
        this.jiggleProvider = jiggleProvider;
        var rig = jiggleProvider.GetJiggleRigData();
        transform = rig.rootBone;
        JigglePhysics.SetGlobalDirty();
    }

    private void OnDirty(JiggleTree obj) {
        SetDirty();
    }

    public void UpdateParametersIfNeeded() {
        if (jiggleTree != null && jiggleProvider.HasAnimatedParameters) {
            jiggleRigData.UpdateParameters(jiggleTree, parametersCache);
        }
    }
    
    public void UpdateParameters() {
        if (jiggleTree != null) {
            jiggleRigData.UpdateParameters(jiggleTree, parametersCache);
        }
    }
    

    public void RegenerateJiggleTreeIfNeeded() {
        if (jiggleTree == null) {
            jiggleTree = JigglePhysics.CreateJiggleTree(jiggleRigData, jiggleTree);
            jiggleTree.dirtied += OnDirty;
            return;
        }
        if (jiggleTree.dirty) {
            jiggleTree.dirtied -= OnDirty;
            jiggleTree = JigglePhysics.CreateJiggleTree(jiggleRigData, jiggleTree);
            jiggleTree.dirtied += OnDirty;
        }
    }

    public void SetDirty() {
        if (jiggleTree is { dirty: false }) {
            JigglePhysics.ScheduleRemoveJiggleTree(jiggleTree);
            jiggleTree.SetDirty();
        }
        parent?.SetDirty();
        JigglePhysics.SetGlobalDirty();
    }

}

}
