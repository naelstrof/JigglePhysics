using GatorDragonGames.JigglePhysics;
using UnityEngine;

public interface IJiggleParameterProvider {
    public JiggleRigData GetJiggleRigData();
    public bool HasAnimatedParameters { get; }
}
