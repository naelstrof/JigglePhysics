using System;
using UnityEngine;

namespace GatorDragonGames.JigglePhysics {

public class JiggleUpdateExample : MonoBehaviour {
    [Header("OPTIONAL: For debug drawing, import Samples within the Package Manager for URP/HDRP/BuiltIn procedural materials to place here.")]
    [Space(10)]
    [SerializeField] private bool debugDraw;
    [SerializeField] private Material proceduralMaterial;
    [SerializeField] private Mesh sphereMesh;

    private double accumulatedTime;
    private double fixedTime;

    private void LateUpdate() {
        var time = Time.timeAsDouble;
        var fixedDeltaTime = Time.fixedDeltaTime;
        accumulatedTime += Time.deltaTime;
        if (accumulatedTime > fixedDeltaTime) {
            while (accumulatedTime > fixedDeltaTime) {
                fixedTime += fixedDeltaTime;
                accumulatedTime -= fixedDeltaTime;
            }
            JigglePhysics.ScheduleSimulate(fixedTime, time, fixedDeltaTime);
        }

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
