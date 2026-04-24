using System;
using UnityEngine;

namespace GatorDragonGames.JigglePhysics {

public class JiggleUpdateExample : MonoBehaviour {
    [Header("OPTIONAL: For debug drawing, import Samples within the Package Manager for URP/HDRP/BuiltIn procedural materials to place here.")]
    [Space(10)]
    [SerializeField] private bool debugDraw;
    [SerializeField] private Material proceduralMaterial;
    [SerializeField] private Mesh sphereMesh;

    private void LateUpdate() {
        var time = Time.timeAsDouble;
        var fixedTime = Time.fixedTimeAsDouble;

        JigglePhysics.ScheduleSimulate(fixedTime, time, Time.fixedDeltaTime);
        
        JigglePhysics.SchedulePose(time);
        if (debugDraw) {
            JigglePhysics.ScheduleRender();
        }
        
        JigglePhysics.CompletePose();
        if (debugDraw) {
            JigglePhysics.CompleteRender(proceduralMaterial, sphereMesh);
        }
    }

    void OnApplicationQuit() {
        JigglePhysics.Dispose();
    }

    private void OnDrawGizmos() {
        JigglePhysics.OnDrawGizmos();
    }
}

}
