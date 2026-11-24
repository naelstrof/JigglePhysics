using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GatorDragonGames.JigglePhysics {
public static class JiggleRenderer {
    private static NativeArray<JiggleRenderInstancer.GPUChunk> sphereChunks;
    private static Bounds colliderBounds;
    private static int colliderCount;

    private static bool hasHandleRender;
    private static JobHandle handleRender;
    private static JiggleJobPrepareRender jobPrepareRender;
    private static JiggleRenderInstancer sphereInstancer;
    //private static JiggleRenderInstancer planeInstancer;

    public static void OnEnable(JiggleJobs job) {
        sphereInstancer = new JiggleRenderInstancer();
        //planeInstancer = new JiggleRenderInstancer();
        job.OnFinishSimulate += FlipData;
        jobPrepareRender = new JiggleJobPrepareRender() {
            personalColliders = job.GetPersonalColliders(out var _),
            sceneColliders = job.GetSceneColliders(out var _),
            outputPoses = job.GetInterpolatedOutputPoses(out var _),
            trees = job.GetTrees(out var _),
            sphereChunks = sphereChunks,
            sphereBounds = new NativeReference<Bounds>(Allocator.Persistent),
            sphereCount = new NativeReference<int>(Allocator.Persistent),
        };
    }
    
    private static float4 ColorToFloat4(Color color) {
        return new float4(color.r, color.g, color.b, color.a);
    }
    
    private static void FlipData(JiggleJobs job, double simulatedTime) {
        var sceneColliderCapacity = job.GetSceneColliderCapacity();
        var personalColliderCapacity = job.GetSceneColliderCapacity();
        var transformCapacity = job.GetTransformCapcity();
        
        int desiredChunkCount = sceneColliderCapacity + personalColliderCapacity + transformCapacity;
        if (desiredChunkCount == 0) {
            return;
        }

        if (!sphereChunks.IsCreated || sphereChunks.Length != desiredChunkCount) {
            var newSphereChunks = new NativeArray<JiggleRenderInstancer.GPUChunk>(desiredChunkCount, Allocator.Persistent);
            if (sphereChunks.IsCreated) {
                var oldLength = sphereChunks.Length;
                NativeArray<JiggleRenderInstancer.GPUChunk>.Copy(sphereChunks, newSphereChunks, oldLength);
                sphereChunks.Dispose();
            }
            sphereChunks = newSphereChunks;
        }
    }

    public static void PrepareRender(JiggleJobs job) {
        if (!sphereChunks.IsCreated) {
            return;
        }
        jobPrepareRender.sphereChunks = sphereChunks;
        jobPrepareRender.personalColliders = job.GetPersonalColliders(out jobPrepareRender.personalColliderCount);
        jobPrepareRender.sceneColliders = job.GetSceneColliders(out jobPrepareRender.sceneColliderCount);
        jobPrepareRender.outputPoses = job.GetInterpolatedOutputPoses(out jobPrepareRender.transformCount);
        jobPrepareRender.trees = job.GetTrees(out jobPrepareRender.treeCount);
        if (job.TryGetRenderDependencies(out var handle)) {
            handleRender = jobPrepareRender.Schedule(handle);
        }

        hasHandleRender = true;
    }

    public static void FinishRender(Material gpuInstanceMaterial, Mesh sphere) {
        if (!sphereChunks.IsCreated || !hasHandleRender) {
            return;
        }
        handleRender.Complete();
        sphereInstancer.Render(jobPrepareRender.sphereBounds.Value, sphere, gpuInstanceMaterial, sphereChunks, jobPrepareRender.sphereCount.Value);
    }

    public static void Dispose() {
        sphereInstancer?.Dispose();
        sphereInstancer = null;
        //planeInstancer?.Dispose();
        //planeInstancer = null;
        jobPrepareRender.Dispose();
        if (sphereChunks.IsCreated) {
            sphereChunks.Dispose();
        }
    }
}

}