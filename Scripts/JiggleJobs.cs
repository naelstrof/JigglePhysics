using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace GatorDragonGames.JigglePhysics {

public class JiggleJobs {
    private JiggleMemoryBus _memoryBus;

    private JobHandle handlePersonalColliderRead;
    private bool hasHandlePersonalColliderRead;
    
    private JobHandle handleSceneColliderRead;
    private bool hasHandleSceneColliderRead;

    private JobHandle handleBulkRead;
    private bool hasHandleBulkRead;
    
    private JobHandle handleBulkReset;
    private bool hasHandleBulkReset;

    private JobHandle handleSimulate;
    private bool hasHandleSimulate;

    private JobHandle handleTransformWrite;
    private bool hasHandleTransformWrite;

    private JobHandle handleRootRead;
    private bool hasHandleRootRead;

    private JobHandle handleInterpolate;
    private bool hasHandleInterpolate;
    
    private JobHandle handleBroadPhaseClear;
    private bool hasHandleBroadPhaseClear;
    
    private JobHandle handleBroadPhase;
    private bool hasHandleBroadPhase;
    
    private JobHandle handleInputInterpolate;
    private bool hasHandleInputInterpolate;

    private JiggleJobBulkColliderTransformRead jobBulkPersonalColliderTransformRead;
    private JiggleJobBulkColliderTransformRead jobBulkSceneColliderTransformRead;
    private JiggleJobBulkTransformRead jobBulkTransformRead;
    private JiggleJobBulkTransformReset jobBulkTransformReset;
    private JiggleJobSimulate jobSimulate;
    private JiggleJobBulkReadRoots jobBulkReadRoots;
    private JiggleJobInterpolation jobInterpolation;
    private JiggleJobBroadPhaseClear jobBroadPhaseClear;
    private JiggleJobBroadPhase jobBroadPhase;
    private JiggleJobInputInterpolation jobInputInterpolation;

    private JiggleJobTransformWrite jobTransformWrite;

    private List<IntPtr> freePointers;

    public delegate void JiggleFinishSimulateAction(JiggleJobs job, double simulatedTime);
    public event JiggleFinishSimulateAction OnFinishSimulate;

    public JiggleJobs(double fixedTime, float fixedDeltaTime) {
        _memoryBus = new JiggleMemoryBus();
        jobSimulate = new JiggleJobSimulate(_memoryBus, fixedDeltaTime);
        jobBulkTransformRead = new JiggleJobBulkTransformRead(_memoryBus);
        jobBulkTransformReset = new JiggleJobBulkTransformReset(_memoryBus);
        jobBulkReadRoots = new JiggleJobBulkReadRoots(_memoryBus);
        jobInterpolation = new JiggleJobInterpolation(_memoryBus, fixedTime, fixedDeltaTime);
        jobBulkPersonalColliderTransformRead = new JiggleJobBulkColliderTransformRead(_memoryBus.personalColliders);
        jobBulkSceneColliderTransformRead = new JiggleJobBulkColliderTransformRead(_memoryBus.sceneColliders);
        jobTransformWrite = new JiggleJobTransformWrite(_memoryBus);
        jobBroadPhase = new JiggleJobBroadPhase(_memoryBus);
        jobBroadPhaseClear = new JiggleJobBroadPhaseClear(_memoryBus);
        jobInputInterpolation = new JiggleJobInputInterpolation(_memoryBus, fixedTime, fixedDeltaTime);
        freePointers = new List<IntPtr>();
    }

    public bool TryGetRenderDependencies(out JobHandle handle) {
        if (hasHandleSimulate && hasHandleInterpolate) {
            handle = JobHandle.CombineDependencies(handleSimulate, handleInterpolate);
            return true;
        }
        handle = default;
        return false;
    }
    
    public void SetFixedDeltaTime(float fixedDeltaTime) {
        jobSimulate.SetFixedDeltaTime(fixedDeltaTime);
        jobInterpolation.SetFixedDeltaTime(fixedDeltaTime);
        jobInputInterpolation.SetFixedDeltaTime(fixedDeltaTime);
    }

    public void Dispose() {
        if (hasHandleBulkRead) handleBulkRead.Complete();
        if (hasHandleBulkReset) handleBulkReset.Complete();
        if (hasHandleRootRead) handleRootRead.Complete();
        if (hasHandleSimulate) handleSimulate.Complete();
        if (hasHandleTransformWrite) handleTransformWrite.Complete();
        if (hasHandleInterpolate) handleInterpolate.Complete();
        if (hasHandlePersonalColliderRead) handlePersonalColliderRead.Complete();
        if (hasHandleSceneColliderRead) handleSceneColliderRead.Complete();
        if (hasHandleBroadPhase) handleBroadPhase.Complete();
        if (hasHandleBroadPhaseClear) handleBroadPhaseClear.Complete();
        if (hasHandleInputInterpolate) handleInputInterpolate.Complete();
        Free();
        _memoryBus.Dispose();
    }

    public JobHandle SchedulePoses(double timeAsDouble) {
        if (_memoryBus.transformCount == 0) {
            return default;
        }
        jobBulkTransformReset.UpdateArrays(_memoryBus);
        // TODO: This technically only needs to happen for root bones, as their positions are used for posing. Instead just doing a full reset because I'm lazy.
        if (hasHandleBulkReset && hasHandleTransformWrite) {
            handleBulkReset = jobBulkTransformReset.Schedule(_memoryBus.GetTransformAccessArray(), JobHandle.CombineDependencies(handleTransformWrite, handleBulkReset));
        } else {
            handleBulkReset = jobBulkTransformReset.Schedule(_memoryBus.GetTransformAccessArray());
        }
        hasHandleBulkReset = true;

        return SchedulePoses(handleBulkReset, timeAsDouble);
    }

    private JobHandle SchedulePoses(JobHandle dep, double timeAsDouble) {
        if (_memoryBus.transformCount == 0) {
            return dep;
        }

        jobBulkReadRoots.UpdateArrays(_memoryBus);
        jobInterpolation.UpdateArrays(_memoryBus);
        jobTransformWrite.UpdateArrays(_memoryBus);

        handleRootRead = jobBulkReadRoots.ScheduleReadOnly(_memoryBus.GetTransformRootAccessArray(), 128, dep);
        hasHandleRootRead = true;

        jobInterpolation.currentTime = timeAsDouble;
        handleInterpolate = jobInterpolation.ScheduleParallel(_memoryBus.transformCount, 128, handleRootRead);
        hasHandleInterpolate = true;

        handleTransformWrite = jobTransformWrite.Schedule(_memoryBus.GetTransformAccessArray(), handleInterpolate);

        hasHandleTransformWrite = true;
        return handleTransformWrite;
    }

    public void CompletePoses() {
        if (hasHandleTransformWrite) {
            handleTransformWrite.Complete();
        }
    }

    public void FreeOnComplete(IntPtr pointer) {
        freePointers.Add(pointer);
    }

    private void Free() {
        var freePointerCount = freePointers.Count;
        for (int i = 0; i < freePointerCount; i++) {
            unsafe {
                UnsafeUtility.Free((void*)freePointers[i], Allocator.Persistent);
            }
        }
        freePointers.Clear();
    }

    public void Simulate(double simulateTime, double realTime) {
        if (_memoryBus.transformCount == 0) {
            _memoryBus.CommitTrees();
            _memoryBus.CommitColliders();
            jobSimulate.UpdateArrays(_memoryBus);
            jobBulkTransformRead.UpdateArrays(_memoryBus);
            jobBulkReadRoots.UpdateArrays(_memoryBus);
            jobInterpolation.UpdateArrays(_memoryBus);
            jobBulkPersonalColliderTransformRead.UpdateArrays(_memoryBus.personalColliders);
            jobBulkSceneColliderTransformRead.UpdateArrays(_memoryBus.sceneColliders);
            jobTransformWrite.UpdateArrays(_memoryBus);
            jobBroadPhase.UpdateArrays(_memoryBus);
            jobBroadPhaseClear.UpdateArrays(_memoryBus);
            jobInputInterpolation.UpdateArrays(_memoryBus);
            return;
        }

        // TODO: Use an external monobehavior to update gravity?
        var gravity = Physics.gravity;
        if (hasHandleSimulate) {
            handleSimulate.Complete();
            Free();
            OnFinishSimulate?.Invoke(this, simulateTime);
        }


        _memoryBus.RotateBuffers();
        jobInterpolation.previousTimeStamp = jobInterpolation.timeStamp;
        jobInterpolation.timeStamp = jobSimulate.timeStamp;
        jobInputInterpolation.previousTimeStamp = jobInputInterpolation.timeStamp;
        jobInputInterpolation.timeStamp = realTime;
        jobInputInterpolation.currentTime = simulateTime;

        _memoryBus.CommitTrees();
        _memoryBus.CommitColliders();

        jobSimulate.UpdateArrays(_memoryBus);
        jobBulkTransformReset.UpdateArrays(_memoryBus);
        jobBulkTransformRead.UpdateArrays(_memoryBus);
        jobBulkPersonalColliderTransformRead.UpdateArrays(_memoryBus.personalColliders);
        jobBulkSceneColliderTransformRead.UpdateArrays(_memoryBus.sceneColliders);
        jobBroadPhase.UpdateArrays(_memoryBus);
        jobBroadPhaseClear.UpdateArrays(_memoryBus);
        jobInputInterpolation.UpdateArrays(_memoryBus);

        if (hasHandleSimulate) {
            handlePersonalColliderRead = jobBulkPersonalColliderTransformRead.ScheduleReadOnly( _memoryBus.GetPersonalColliderTransformAccessArray(), 128, handleSimulate);
            handleSceneColliderRead = jobBulkSceneColliderTransformRead.ScheduleReadOnly(_memoryBus.GetSceneColliderTransformAccessArray(), 128, handleSimulate);
        } else {
            handlePersonalColliderRead = jobBulkPersonalColliderTransformRead.ScheduleReadOnly( _memoryBus.GetPersonalColliderTransformAccessArray(), 128);
            handleSceneColliderRead = jobBulkSceneColliderTransformRead.ScheduleReadOnly(_memoryBus.GetSceneColliderTransformAccessArray(), 128);
        }

        hasHandlePersonalColliderRead = true;
        hasHandleSceneColliderRead = true;
        
        var colliderHandles = JobHandle.CombineDependencies(handlePersonalColliderRead, handleSceneColliderRead);
        
        handleBroadPhaseClear = jobBroadPhaseClear.Schedule();
        hasHandleBroadPhaseClear = true;
        handleBroadPhase = jobBroadPhase.Schedule(JobHandle.CombineDependencies(colliderHandles, handleBroadPhaseClear));
        hasHandleBroadPhase = true;
        
        handleBulkReset = jobBulkTransformReset.Schedule(_memoryBus.GetTransformAccessArray(), colliderHandles);
        hasHandleBulkReset = true;

        handleBulkRead = jobBulkTransformRead.ScheduleReadOnly(_memoryBus.GetTransformAccessArray(), 128, handleBulkReset);
        hasHandleBulkRead = true;

        handleInputInterpolate = jobInputInterpolation.ScheduleParallel(_memoryBus.transformCount, 128, handleBulkRead);
        hasHandleInputInterpolate = true;

        jobSimulate.gravity = gravity;
        jobSimulate.timeStamp = simulateTime;
        handleSimulate = jobSimulate.ScheduleParallel(_memoryBus.treeCount, 1, JobHandle.CombineDependencies(handleBroadPhase, handleInputInterpolate));
        hasHandleSimulate = true;
    }

    public void ScheduleAdd(JiggleTree tree) {
        _memoryBus.ScheduleAdd(tree);
    }

    public void ScheduleRemove(JiggleTree tree) {
        _memoryBus.ScheduleRemove(tree);
    }
    
    public void ScheduleAdd(JiggleColliderSerializable collider) {
        _memoryBus.ScheduleAdd(collider);
    }

    public void ScheduleRemove(JiggleColliderSerializable collider) {
        _memoryBus.ScheduleRemove(collider);
    }
    
    public void GetColliders(out JiggleCollider[] personalColliders, out JiggleCollider[] sceneColliders, out int personalColliderCount, out int sceneColliderCount) {
        _memoryBus.GetColliders(out personalColliders, out sceneColliders, out personalColliderCount, out sceneColliderCount);
    }
    
    public void GetResults(out JiggleTransform[] poses, out JiggleTreeJobData[] trees, out int poseCount, out int treeCount) {
        if (hasHandleSimulate) {
            handleSimulate.Complete();
        }

        if (hasHandleInterpolate) {
            handleInterpolate.Complete();
        }
        _memoryBus.GetResults(out poses, out trees, out poseCount, out treeCount);
    }
    
    public NativeArray<JiggleCollider> GetPersonalColliders(out int personalColliderCount) {
        return _memoryBus.GetPersonalColliders(out personalColliderCount);
    }

    public NativeArray<JiggleCollider> GetSceneColliders(out int sceneColliderCount) {
        return _memoryBus.GetSceneColliders(out sceneColliderCount);
    }

    public NativeArray<JiggleTransform> GetInterpolatedOutputPoses(out int poseCount) {
        return _memoryBus.GetInterpolatedOutputPoses(out poseCount);
    }

    public NativeArray<JiggleTreeJobData> GetTrees(out int treeCount) {
        return _memoryBus.GetTrees(out treeCount);
    }
    
    public int GetTransformCapcity() {
        return _memoryBus.transformCapacity;
    }
    public int GetTransformCount() {
        return _memoryBus.transformCount;
    }

    public int GetPersonalColliderCapacity() {
        return _memoryBus.personalColliderCapacity;
    }
    
    public int GetSceneColliderCapacity() {
        return _memoryBus.sceneColliderCapacity;
    }
    
    public int GetPersonalColliderCount() {
        return _memoryBus.personalColliderCount;
    }
    
    public int GetSceneColliderCount() {
        return _memoryBus.personalColliderCount;
    }

    public void OnDrawGizmos() {
        if (!hasHandleInterpolate || !hasHandleSimulate || !Application.isEditor) {
            return;
        }

        handleInterpolate.Complete();
        handleSimulate.Complete();
        _memoryBus.GetResults(out var poses, out var trees, out var poseCount, out var treeCount);
        for (int i = 0; i < treeCount; i++) {
            var tree = trees[i];
            for (int o = 0; o < tree.pointCount; o++) {
                unsafe {
                    var pose = poses[o+tree.transformIndexOffset];
                    var point = tree.points[o];
                    if (!pose.isVirtual) {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawWireSphere(pose.position, point.worldRadius);
                    } else {
                        //Gizmos.color = point.parentIndex == -1 ? Color.crimson : Color.magenta;
                        //Gizmos.DrawWireSphere(point.position, 0.025f);
                    }


                    if (point.childrenCount != 0) {
                        for (int j = 0; j < point.childrenCount; j++) {
                            //var childPoint = tree.points[point.childrenIndices[j]];
                            var childPose = poses[point.childrenIndices[j] + tree.transformIndexOffset];
                            if (!pose.isVirtual && !childPose.isVirtual) {
                                Gizmos.color = Color.cyan;
                                Gizmos.DrawLine(pose.position, childPose.position);
                            } else {
                                //Gizmos.color = Color.magenta;
                                //Gizmos.DrawLine(point.position, childPoint.position);
                            }
                        }
                    }
                }
            }
        }
    }
}

}