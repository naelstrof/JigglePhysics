using System.Collections.Generic;
using UnityEngine;

namespace GatorDragonGames.JigglePhysics {

public class JiggleTreeSegment {

    public Transform transform { get; private set; }
    public JiggleTree jiggleTree { get; private set; }
    public JiggleTreeSegment parent { get; private set; }
    private IJiggleParameterProvider jiggleProvider;
    public JiggleRigData jiggleRigData => jiggleProvider.GetJiggleRigData();
    public bool HasAnimatedParameters => jiggleProvider.HasAnimatedParameters;

    /// <summary>
    /// The segment that owns the simulated tree this one is merged into (itself when it has no parent).
    /// </summary>
    public JiggleTreeSegment root {
        get {
            var current = this;
            while (current.parent != null) {
                current = current.parent;
            }
            return current;
        }
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

    [System.Obsolete("Animated parameters are applied by JigglePhysics for every segment; call UpdateParameters() to push an edit.")]
    public void UpdateParametersIfNeeded() {
        if (HasAnimatedParameters) {
            JigglePhysics.UpdateTreeParameters(this);
        }
    }

    // A nested segment owns no tree of its own (its bones live in its root's tree), so parameters are always
    // recomputed for the whole tree; applying one rig's values to every bone breaks nested rigs and excluded roots.
    public void UpdateParameters() {
        JigglePhysics.UpdateTreeParameters(this);
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
