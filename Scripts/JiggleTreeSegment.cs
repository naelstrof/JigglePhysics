using UnityEngine;

namespace GatorDragonGames.JigglePhysics {

public class JiggleTreeSegment {

    public Transform transform { get; private set; }
    public JiggleTree jiggleTree { get; private set; }
    public JiggleTreeSegment parent { get; private set; }
    private JiggleRig behavior;
    public JiggleRigData jiggleRigData => behavior.GetJiggleRigData();

    public void SetParent(JiggleTreeSegment jiggleTree) {
        parent?.SetDirty();
        parent = jiggleTree;
        parent?.SetDirty();
        JigglePhysics.SetGlobalDirty();
    }

    public JiggleTreeSegment(JiggleRig behavior) {
        this.behavior = behavior;
        var rig = behavior.GetJiggleRigData();
        transform = rig.rootBone;
        jiggleTree = JigglePhysics.CreateJiggleTree(rig, null);
        jiggleTree.dirtied += OnDirty;
    }

    private void OnDirty(JiggleTree obj) {
        SetDirty();
    }

    public void UpdateParametersIfNeeded() {
        if (behavior.HasAnimatedParameters) {
            behavior.UpdateParameters();
        }
    }
    

    public void RegenerateJiggleTreeIfNeeded() {
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
