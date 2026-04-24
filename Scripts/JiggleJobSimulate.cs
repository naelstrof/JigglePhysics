using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GatorDragonGames.JigglePhysics {

[BurstCompile]
public struct JiggleJobSimulate : IJobFor {
    // TODO: doubles are strictly a bad way to track time, probably should be ints or longs.
    public double timeStamp;
    public float3 gravity;
    public int timeIncrements;
    
    public int sceneColliderCount;

    [ReadOnly] [NativeDisableParallelForRestriction]
    public NativeArray<JiggleTransform> inputPoses;

    [NativeDisableParallelForRestriction] public NativeArray<PoseData> outputPoses;
    [ReadOnly,NativeDisableParallelForRestriction] public NativeArray<JiggleCollider> personalColliders;
    [ReadOnly,NativeDisableParallelForRestriction] public NativeArray<JiggleCollider> sceneColliders;
    
    [ReadOnly] public NativeHashMap<int2,JiggleGridCell> broadPhaseMap;
    [ReadOnly] public NativeReference<JiggleGridCell> globalCell;

    public NativeArray<JiggleTreeJobData> jiggleTrees;

    private float deltaTimeSquared;

    public JiggleJobSimulate(JiggleMemoryBus bus, float fixedDeltaTime) {
        inputPoses = bus.simulateInputPoses;
        jiggleTrees = bus.jiggleTreeStructs;
        outputPoses = bus.simulationOutputPoseData;
        personalColliders = bus.personalColliders;
        sceneColliders = bus.sceneColliders;
        timeStamp = Time.timeAsDouble;
        broadPhaseMap = bus.broadPhaseMap;
        globalCell = bus.globalCell;
        gravity = Physics.gravity;
        sceneColliderCount = 0;
        deltaTimeSquared = fixedDeltaTime * fixedDeltaTime;
        timeIncrements = 1;
    }

    public void UpdateArrays(JiggleMemoryBus bus) {
        inputPoses = bus.simulateInputPoses;
        jiggleTrees = bus.jiggleTreeStructs;
        outputPoses = bus.simulationOutputPoseData;
        personalColliders = bus.personalColliders;
        sceneColliders = bus.sceneColliders;
        sceneColliderCount = bus.sceneColliderCount;
        broadPhaseMap = bus.broadPhaseMap;
        globalCell = bus.globalCell;
    }
    
    public void SetFixedDeltaTime(float fixedDeltaTime) {
        deltaTimeSquared = fixedDeltaTime * fixedDeltaTime;
    }


    private unsafe void Cache(ref JiggleTreeJobData tree) {
        float3 min = new float3(float.MaxValue);
        float3 max = new float3(float.MinValue);
        
        for (int i = 0; i < tree.pointCount; i++) {
            var point = tree.points[i];
            var parameters = tree.parameters[i];
            if (point.parentIndex == -1) {
                // virtual root particles
                var child = tree.points[point.childrenIndices[0]];
                var childChild = tree.points[child.childrenIndices[0]];
                var childPose = tree.GetInputPose(inputPoses, point.childrenIndices[0]);
                var childChildPose = tree.GetInputPose(inputPoses, child.childrenIndices[0]);
                if (!childChild.hasTransform) {
                    // edge case where it's a singular isolated root bone
                    point.pose = childPose.position - new float3(0f, 0.25f, 0f);
                    point.parentPose = childPose.position - new float3(0f, 0.5f, 0f);
                    point.desiredLengthToParent = 0.25f;
                } else {
                    var diff = childPose.position - childChildPose.position;
                    point.pose = childPose.position + diff;
                    point.parentPose = childPose.position + diff * 2f;
                    point.desiredLengthToParent = math.length(diff);
                }

                point.worldRadius = 0f;
                point.workingPosition = point.pose;
            } else if (point.hasTransform) {
                // "real" particles
                var inputPose = tree.GetInputPose(inputPoses, i);
                var parent = tree.points[point.parentIndex];
                point.pose = inputPose.position;
                point.parentPose = parent.pose;
                point.desiredLengthToParent = math.distance(point.pose, parent.pose);
                var averagePointScale = (math.abs(inputPose.scale.x) + math.abs(inputPose.scale.y) + math.abs(inputPose.scale.z)) / 3f;
                point.worldRadius = parameters.collisionRadius * averagePointScale;
                min = math.min(min, point.position-new float3(point.worldRadius));
                max = math.max(max, point.position+new float3(point.worldRadius));
            } else {
                // virtual end particles
                var parent = tree.points[point.parentIndex];
                point.pose = (parent.pose * 2f - parent.parentPose);
                point.parentPose = parent.pose;
                point.desiredLengthToParent = math.distance(point.pose, point.parentPose);
                point.worldRadius = 0f;
            }
            tree.points[i] = point;
        }

        tree.minExtentPosition = JiggleGridCell.GetKeyForPosition(min);
        tree.maxExtentPosition = JiggleGridCell.GetKeyForPosition(max);
    }

    private unsafe void VerletIntegrate(JiggleTreeJobData tree) {
        
        var rootPosition = tree.points[0].workingPosition;
        var rootLastPosition = tree.points[0].position;
        var rootDelta = rootPosition - rootLastPosition;
        
        for (int i = 0; i < tree.pointCount; i++) {
            var point = tree.points[i];
            var parameters = tree.parameters + i;
            if (point.parentIndex == -1) {
                continue;
            }
            point.lastPosition += rootDelta * parameters->ignoreRootMotion;
            point.position += rootDelta * parameters->ignoreRootMotion;
            tree.points[i] = point;
        }
        
        for (int i = 0; i < tree.pointCount; i++) {
            var point = tree.points[i];
            if (point.parentIndex == -1) {
                continue;
            }
            var parent = tree.points[point.parentIndex];

            //point->debug = pointLocalPosition;

            var inverseScaleFactor = (1f / timeIncrements);
            var delta = (point.position - point.lastPosition)*inverseScaleFactor;
            var parentDelta = (parent.position - parent.lastPosition)*inverseScaleFactor;
            var localSpaceVelocity = delta - parentDelta;
            var velocity = delta - localSpaceVelocity;
            if (parent.parentIndex != -1) {
                var parentParameters = tree.parameters+point.parentIndex;
                point.workingPosition = point.position
                                         + velocity * (1f - parentParameters->airDrag)
                                         +localSpaceVelocity * (1f - parentParameters->drag)
                                         + gravity * parentParameters->gravityMultiplier * deltaTimeSquared * inverseScaleFactor;
            } else {
                var parameters = tree.parameters + i;
                point.workingPosition = point.position
                                         + velocity * (1f - parameters->airDrag)
                                         +localSpaceVelocity * (1f - parameters->drag)
                                         + gravity * parameters->gravityMultiplier * deltaTimeSquared * inverseScaleFactor;
            }
            tree.points[i] = point;
        }
    }

    quaternion FromToRotationFromNormalizedVectors(float3 from, float3 to) {
        var axis = math.normalizesafe(math.cross(from, to), new float3(0f, 0f, 1f));
        var angle = math.acos(math.clamp(math.dot(from, to), -1f, 1f));
        return quaternion.AxisAngle(axis, angle);
    }

    float float3Angle(float3 a, float3 b) {
        return math.acos(math.dot(
                    math.normalizesafe(a, new float3(0,0,1)), 
                    math.normalizesafe(b, new float3(0,0,1))
                    ));
    }
    

    private unsafe float3 DoDepenetration(JiggleSimulatedPoint* point, JiggleSimulatedPoint* otherPoint, JigglePointParameters* otherPointParameters, JiggleCollider collider) {
        if (!collider.enabled || !point->hasTransform || !otherPoint->hasTransform || point->worldRadius == 0f || otherPoint->worldRadius == 0f) {
            return new float3(0f, 0f, 0f);
        }
        switch (collider.type) {
            case JiggleCollider.JiggleColliderType.Sphere: {
                var hardness = 1f;
                var colliderPosition = collider.localToWorldMatrix.c3.xyz;
                var pointPosition = point->workingPosition;
                var otherPosition = otherPoint->workingPosition;
                var pointRadius = point->worldRadius;
                var colliderRadius = collider.worldRadius;
                var combinedRadius = pointRadius + colliderRadius;
                var boneClosestPoint = GetClosestPointOnLineSegment(
                        colliderPosition,
                        pointPosition,
                        otherPosition,
                        out var tValue
                        );
                var sphere_diff = boneClosestPoint - colliderPosition;
                var sphere_distance = math.length(sphere_diff);
                var depenetrationMagnitude = combinedRadius - sphere_distance;
                if (depenetrationMagnitude <= 0f) {
                    return float3.zero;
                }
                var depenetrationDir = math.normalizesafe(sphere_diff, new float3(0, 0, 1));
                var depenetrationVector = depenetrationDir * depenetrationMagnitude;
                var pValue = math.clamp(2f - tValue * 2f, 0f, 1f);
                depenetrationVector *= hardness * pValue;
                // TODO: find decent rigidbody solve instead of just pushing them both naively
                sphere_diff = pointPosition - colliderPosition;
                sphere_distance = math.length(sphere_diff);
                depenetrationMagnitude = combinedRadius - sphere_distance;
                if (depenetrationMagnitude > 0f) {
                    depenetrationDir = math.normalizesafe(sphere_diff, new float3(0, 0, 1));
                    var depenetrationVector2 = depenetrationDir * depenetrationMagnitude;
                    depenetrationVector2 *= hardness;
                    var mag1 = math.length(depenetrationVector);
                    var mag2 = math.length(depenetrationVector2);
                    depenetrationVector = (depenetrationVector+depenetrationVector2)*0.5f;
                    depenetrationVector = math.normalizesafe(depenetrationVector, new float3(0,0,1)) * math.max(mag1, mag2);
                }
                if (!(otherPointParameters->angleElasticity == 1f
                      && otherPointParameters->rootElasticity == 1f
                      && otherPointParameters->lengthElasticity == 1f)) {
                    return depenetrationVector;
                }
                break;
            }
            case JiggleCollider.JiggleColliderType.Capsule: {
                var hardness = 1f;
                var colliderPosition = collider.localToWorldMatrix.c3.xyz;
                var colliderUp = collider.GetWorldAxis();
                var halfHeight = collider.worldHeight * 0.5f;
                var capsuleA = colliderPosition - colliderUp * halfHeight;
                var capsuleB = colliderPosition + colliderUp * halfHeight;
                var pointPosition = point->workingPosition;
                var otherPosition = otherPoint->workingPosition;
                var pointRadius = point->worldRadius;
                var colliderRadius = collider.worldRadius;
                var combinedRadius = pointRadius + colliderRadius;
                // Find closest points between capsule axis and bone segment
                ClosestPointsOnTwoSegments(capsuleA, capsuleB, pointPosition, otherPosition, out var closestOnCapsule, out var closestOnBone, out var tValueBone);
                var cap_diff = closestOnBone - closestOnCapsule;
                var cap_distance = math.length(cap_diff);
                var cap_depenetrationMagnitude = combinedRadius - cap_distance;
                if (cap_depenetrationMagnitude <= 0f) {
                    return float3.zero;
                }
                var cap_depenetrationDir = math.normalizesafe(cap_diff, new float3(0, 0, 1));
                var cap_depenetrationVector = cap_depenetrationDir * cap_depenetrationMagnitude;
                var cap_pValue = math.clamp(2f - tValueBone * 2f, 0f, 1f);
                cap_depenetrationVector *= hardness * cap_pValue;
                // Point-to-capsule direct check
                ClosestPointOnSegment(pointPosition, capsuleA, capsuleB, out var closestOnCapsuleToPoint);
                cap_diff = pointPosition - closestOnCapsuleToPoint;
                cap_distance = math.length(cap_diff);
                cap_depenetrationMagnitude = combinedRadius - cap_distance;
                if (cap_depenetrationMagnitude > 0f) {
                    cap_depenetrationDir = math.normalizesafe(cap_diff, new float3(0, 0, 1));
                    var cap_depenetrationVector2 = cap_depenetrationDir * cap_depenetrationMagnitude;
                    cap_depenetrationVector2 *= hardness;
                    var mag1 = math.length(cap_depenetrationVector);
                    var mag2 = math.length(cap_depenetrationVector2);
                    cap_depenetrationVector = (cap_depenetrationVector + cap_depenetrationVector2) * 0.5f;
                    cap_depenetrationVector = math.normalizesafe(cap_depenetrationVector, new float3(0, 0, 1)) * math.max(mag1, mag2);
                }
                if (!(otherPointParameters->angleElasticity == 1f
                      && otherPointParameters->rootElasticity == 1f
                      && otherPointParameters->lengthElasticity == 1f)) {
                    return cap_depenetrationVector;
                }
                break;
            }
            case JiggleCollider.JiggleColliderType.Plane: {
                var colliderPosition = collider.localToWorldMatrix.c3.xyz;
                var planeNormal = math.normalizesafe(collider.localToWorldMatrix.c1.xyz, new float3(0, 1, 0));
                var pointPosition = point->workingPosition;
                var pointRadius = point->worldRadius;
                var signedDistance = math.dot(pointPosition - colliderPosition, planeNormal);
                var penetration = pointRadius - signedDistance;
                if (penetration <= 0f) {
                    return float3.zero;
                }
                var plane_depenetrationVector = planeNormal * penetration;
                if (!(otherPointParameters->angleElasticity == 1f
                      && otherPointParameters->rootElasticity == 1f
                      && otherPointParameters->lengthElasticity == 1f)) {
                    return plane_depenetrationVector;
                }
                break;
            }
        }
        return new float3(0f, 0f, 0f);
    }

    private void ClosestPointOnSegment(float3 point, float3 segA, float3 segB, out float3 closest) {
        var ab = segB - segA;
        var lengthSq = math.dot(ab, ab);
        if (lengthSq == 0f) {
            closest = segA;
            return;
        }
        var t = math.clamp(math.dot(point - segA, ab) / lengthSq, 0f, 1f);
        closest = segA + t * ab;
    }

    private void ClosestPointsOnTwoSegments(float3 a0, float3 a1, float3 b0, float3 b1, out float3 closestA, out float3 closestB, out float tB) {
        var d1 = a1 - a0;
        var d2 = b1 - b0;
        var r = a0 - b0;
        var a = math.dot(d1, d1);
        var e = math.dot(d2, d2);
        var f = math.dot(d2, r);
        float s, t;
        if (a <= 1e-8f && e <= 1e-8f) {
            s = 0f; t = 0f;
        } else if (a <= 1e-8f) {
            s = 0f;
            t = math.clamp(f / e, 0f, 1f);
        } else {
            var c = math.dot(d1, r);
            if (e <= 1e-8f) {
                t = 0f;
                s = math.clamp(-c / a, 0f, 1f);
            } else {
                var b = math.dot(d1, d2);
                var denom = a * e - b * b;
                if (denom != 0f) {
                    s = math.clamp((b * f - c * e) / denom, 0f, 1f);
                } else {
                    s = 0f;
                }
                t = (b * s + f) / e;
                if (t < 0f) {
                    t = 0f;
                    s = math.clamp(-c / a, 0f, 1f);
                } else if (t > 1f) {
                    t = 1f;
                    s = math.clamp((b - c) / a, 0f, 1f);
                }
            }
        }
        closestA = a0 + d1 * s;
        closestB = b0 + d2 * t;
        tB = t;
    }
    
    private float3 GetClosestPointOnLineSegment(float3 inputPoint, float3 segmentPoint1, float3 segmentPoint2, out float tValue) {
        tValue = 0f;
        var segment = segmentPoint2 - segmentPoint1;
        var segmentLengthSq = math.dot(segment, segment);
        if (segmentLengthSq == 0f) {
            return segmentPoint1;
        }
        tValue = math.dot(inputPoint - segmentPoint1, segment) / segmentLengthSq;
        tValue = math.clamp(tValue, 0f, 1f);
        return segmentPoint1 + tValue * segment;
    }

    private unsafe void DepenetrateCollider(JiggleTreeJobData tree, JiggleSimulatedPoint* point, JiggleSimulatedPoint* parent, JigglePointParameters* pointParameters, JigglePointParameters* parentParameters, JiggleCollider collider) {
        var collisionDepenetration = new float3(0f, 0f, 0f);
        collisionDepenetration = DoDepenetration(point, parent, parentParameters, collider);
        var maxDepenetrationMagnitude = math.length(collisionDepenetration);
        for (int childIndex = 0; childIndex < point->childrenCount; childIndex++) {
            var child = tree.points + point->childrenIndices[childIndex];
            var newCollisionDepenetration = DoDepenetration(point, child, pointParameters, collider);
            maxDepenetrationMagnitude = math.max(maxDepenetrationMagnitude, math.length(newCollisionDepenetration));
            collisionDepenetration += newCollisionDepenetration;
        }
        collisionDepenetration = math.normalizesafe(collisionDepenetration, new float3(0,0,1)) * maxDepenetrationMagnitude;
        point->workingPosition += collisionDepenetration;
    }

    private unsafe bool ContainsIndex(int* array, int arrayCount, int index) {
        for (int i = 0; i < arrayCount; i++) {
            if (array[i] == index) {
                return true;
            }
        }
        return false;
    }
    private unsafe void Constrain(JiggleTreeJobData tree) {
        for (int i = 0; i < tree.pointCount; i++) {
            var point = tree.points+i;
            var pointParameters = tree.parameters + i;
            
            if (point->parentIndex == -1) {
                continue;
            }

            var parent = tree.points+point->parentIndex;
            var parentParameters = tree.parameters + point->parentIndex;

            #region Collisions

            var global = globalCell.Value;
            for (int index = 0; index < global.count; index++) {
                var sceneCollider = sceneColliders[global.colliderIndices[index]];
                DepenetrateCollider(tree, point, parent, pointParameters, parentParameters, sceneCollider);
            }

            // TODO: to convert a float to a grid location we just cast, but this always rounds towards zero. Probably should be a math.round()
            int2 min = tree.minExtentPosition;
            int2 max = tree.maxExtentPosition;
            for (int x = min.x; x <= max.x; x++) {
                for (int y = min.y; y <= max.y; y++) {
                    int2 grid = new int2(x, y);
                    if (broadPhaseMap.TryGetValue(grid, out var gridCell)) {
                        for (int index = 0; index < gridCell.count; index++) {
                            var sceneCollider = sceneColliders[gridCell.colliderIndices[index]];
                            DepenetrateCollider(tree, point, parent, pointParameters, parentParameters, sceneCollider);
                        }
                    }
                }
            }

            var endIndex = tree.colliderIndexOffset + tree.colliderCount;
            for (int index = (int)tree.colliderIndexOffset; index < endIndex; index++) {
                DepenetrateCollider(tree, point, parent, pointParameters, parentParameters, personalColliders[index]);
            }

            #endregion
            
            #region Special root particle solve

            if (parent->parentIndex == -1) {
                var child = tree.points[point->childrenIndices[0]];
                point->workingPosition = point->workingPosition = math.lerp(point->workingPosition, point->pose,
                    pointParameters->rootElasticity * pointParameters->rootElasticity);
                var head = point->pose;
                var tail = child.pose;
                var diffasdf = head - tail;
                parent->workingPosition = point->workingPosition + diffasdf;
                continue;
            }

            #endregion

            #region Back-propagated motion for collisions

            if (point->childrenCount > 0) {
                // Back-propagated motion specifically for collision enabled chains
                var child = tree.points+point->childrenIndices[0];
                if (child->hasTransform) {
                    var child_length_elasticity = pointParameters->lengthElasticity * pointParameters->lengthElasticity;
                    var parentToChildPose = child->pose - parent->pose;
                    var parentToChild = child->workingPosition - parent->workingPosition;
                    var parentToChildPoseNormalized = math.normalizesafe(parentToChildPose, new float3(0,0,1));
                    var parentToChildNormalized = math.normalizesafe(parentToChild, new float3(0,0,1));
                    var parentToChildRotCorrection = FromToRotationFromNormalizedVectors(parentToChildPoseNormalized, parentToChildNormalized);
                    var targetVect = point->pose - parent->pose;
                    var currentPointLength = math.length(point->workingPosition-parent->workingPosition);
                    targetVect = math.normalizesafe(targetVect, new float3(0,0,1)) * currentPointLength;
                    var targetPos = math.rotate(parentToChildRotCorrection, targetVect) + parent->workingPosition;

                    var targetFromChild = targetPos - child->workingPosition;
                    var targetfromChildDist = math.length(targetFromChild);
                    targetPos = child->workingPosition + math.lerp(
                        targetFromChild,
                        math.normalizesafe(targetFromChild, new float3(0,0,1)) * child->desiredLengthToParent,
                        child_length_elasticity
                        );

                    //var errorBackwardConstraint = math.length(point->workingPosition - targetPos);
                    //if (targetfromChildDist != 0) {
                    //    errorBackwardConstraint /= targetfromChildDist;
                    //}
                    //errorBackwardConstraint = math.min(errorBackwardConstraint, 1.0f);
                    //errorBackwardConstraint = math.pow(errorBackwardConstraint, parent->parameters.elasticitySoften);
                    var notFoldedBack = math.clamp(-math.dot(math.normalizesafe(parent->workingPosition - point->workingPosition), math.normalizesafe(child->workingPosition - point->workingPosition))+1f,0f,1f);
                    var childAngleElasticity = pointParameters->angleElasticity * pointParameters->angleElasticity;
                    point->workingPosition = math.lerp(point->workingPosition, targetPos, childAngleElasticity * notFoldedBack);

                    //point->workingPosition = math.lerp(point->workingPosition, backward_constraint, notFoldedBack);
                }
            }

            #endregion
            
            #region Angle Constraint
            
            var parentParentWorkingPosition = parent->parentPose;
            if (parent->parentIndex != -1) {
                var parentParent = tree.points+parent->parentIndex;
                parentParentWorkingPosition = parentParent->workingPosition;
            }
            var length_elasticity = parentParameters->lengthElasticity * parentParameters->lengthElasticity;
            var parentAimPose = math.normalizesafe(point->parentPose - parent->parentPose, new float3(0,0,1));
            var parentAim = math.normalizesafe(parent->workingPosition - parentParentWorkingPosition, new float3(0,0,1));
            if (parent->parentIndex != -1) {
                var parentParent = tree.points+parent->parentIndex;
                parentAim = math.normalizesafe(parent->workingPosition - parentParent->workingPosition, new float3(0,0,1));
            }

            var currentLength = math.length(point->workingPosition - parent->workingPosition);
            var from_to_rot = FromToRotationFromNormalizedVectors(parentAimPose, parentAim);
            var constraintTarget = math.rotate(from_to_rot, point->pose - point->parentPose);

            var desiredPosition = parent->workingPosition + constraintTarget;

            var error = math.distance(point->workingPosition, desiredPosition);
            if (currentLength != 0) {
                error /= currentLength;
            }
            error = math.min(error, 1.0f);
            error = math.pow(error, parentParameters->elasticitySoften);
            for (int j = 0; j < timeIncrements; j++) {
                point->workingPosition = math.lerp(point->workingPosition, desiredPosition, parentParameters->angleElasticity * error);
            }

            #endregion

            // TODO: Early out if collisions are disabled (or don't for a more accurate solve)

            //continue;

            #region Length Constraint

            var offsetFromParent = point->workingPosition - parent->workingPosition;
            var offsetFromParentNormalized = math.normalizesafe(offsetFromParent, new float3(0, 0, 1));
            point->workingPosition = parent->workingPosition + math.lerp(offsetFromParent, offsetFromParentNormalized * point->desiredLengthToParent, length_elasticity);

            #endregion
            
            #region Angle Limit Constraint

            if (parentParameters->angleLimited) {
                var angleLimitParentAimPose = math.normalizesafe(point->parentPose - parent->parentPose, new float3(0,0,1));
                var angleLimitParentAim = math.normalizesafe(parent->workingPosition - parentParentWorkingPosition, new float3(0,0,1));
                var angleLimitFromTo = FromToRotationFromNormalizedVectors(angleLimitParentAimPose, angleLimitParentAim);
                var angleLimitConstraintTarget = math.rotate(angleLimitFromTo, point->pose - point->parentPose);

                var angleLimitDesiredPosition = parent->workingPosition + angleLimitConstraintTarget;
                
                var currentAngleError = float3Angle(
                    math.normalizesafe(point->workingPosition - parent->workingPosition, new float3(0f, 0f, 1f)),
                    math.normalizesafe(angleLimitDesiredPosition - parent->workingPosition, new float3(0f, 0f, 1f))
                );
                
                var A = parentParameters->angleLimit * math.PI * 0.5f;

                if (currentAngleError > A) {
                    var C = float3Angle(
                        point->workingPosition - angleLimitDesiredPosition,
                        parent->workingPosition - angleLimitDesiredPosition
                    ); // known included angle C

                    var b = math.distance(parent->workingPosition, angleLimitDesiredPosition); // known side opposite angle B

                    var B = math.PI - A - C;
                
                    var oppositeCheck = math.sin(B);
                    var a = oppositeCheck == 0f ? 0f : b * math.sin(A) / oppositeCheck;

                    var correctionVector = angleLimitDesiredPosition - point->workingPosition;
                    var correctionDir = math.normalizesafe(correctionVector, new float3(0,0,1));
                    var correctionDistance = math.length(correctionVector);

                    var angleCorrectionDistance = math.max(0f, correctionDistance - a);
                    var angleCorrection =
                        (correctionDir * angleCorrectionDistance) * (1f - parentParameters->angleLimitSoften * 0.5f);
                    point->workingPosition += angleCorrection;
                }
                
            }

            #endregion
            
        }
    }

    private unsafe void FinishStep(JiggleTreeJobData tree) {
        for (int i = 0; i < tree.pointCount; i++) {
            var point = tree.points+i;
            point->lastPosition = point->position;
            point->position = point->workingPosition;
        }
    }

    private unsafe void ApplyPose(JiggleTreeJobData tree) {
        var rootPoint = tree.points[1];
        var rootSimulationPosition = rootPoint.position;
        var rootPose = rootPoint.pose;
        var rootParameters = tree.parameters[1];
        var rootParameterElasticity = 1f-(1f-rootParameters.rootElasticity) * rootParameters.airDrag;
        
        for (int i = 0; i < tree.pointCount; i++) {
            var point = tree.points+i;
            var parameters = tree.parameters + i;
            if (point->childrenCount <= 0) {
                continue;
            }

            var child = tree.points[point->childrenIndices[0]];

            var local_pose = point->pose;
            var local_child_pose = child.pose;
            var local_child_working_position = child.workingPosition;
            var local_working_position = point->workingPosition;

            if (point->parentIndex == -1) {
                continue;
            }

            float3 cachedAnimatedVector = new float3(0f);
            float3 simulatedVector = cachedAnimatedVector;

            if (point->childrenCount <= 1) {
                cachedAnimatedVector = math.normalizesafe(local_child_pose - local_pose, new float3(0,0,1));
                simulatedVector = math.normalizesafe(local_child_working_position - local_working_position, new float3(0,0,1));
            } else {
                var cachedAnimatedVectorSum = new float3(0f);
                var simulatedVectorSum = cachedAnimatedVectorSum;
                for (var j = 0; j < point->childrenCount; j++) {
                    var child_also = tree.points[point->childrenIndices[j]];
                    var local_child_pose_also = child_also.pose;
                    var local_child_working_position_also = child_also.workingPosition;
                    cachedAnimatedVectorSum += math.normalizesafe(local_child_pose_also - local_pose, new float3(0,0,1));
                    simulatedVectorSum +=
                        math.normalizesafe(local_child_working_position_also - local_working_position, new float3(0,0,1));
                }

                cachedAnimatedVector = math.normalizesafe(cachedAnimatedVectorSum * (1f / point->childrenCount), new float3(0,0,1));
                simulatedVector = math.normalizesafe(simulatedVectorSum * (1f / point->childrenCount), new float3(0,0,1));
            }

            var animPoseToPhysicsPose = math.slerp(quaternion.identity,
                FromToRotationFromNormalizedVectors(cachedAnimatedVector, simulatedVector), parameters->blend);

            var transform = new JiggleTransform() {
                isVirtual = !point->hasTransform,
                position = point->workingPosition,
                rotation = math.mul(animPoseToPhysicsPose, tree.GetInputPose(inputPoses, i).rotation),
            };
            tree.WriteOutputPose(outputPoses, i, transform, rootSimulationPosition - rootPose, rootSimulationPosition, rootParameterElasticity);
        }
    }

    private bool Validate(JiggleTreeJobData tree) {
        if (!tree.GetIsValid(out string failReason)) {
            throw new InvalidOperationException(failReason);
        }

        return true;
    }

    public void Execute(int index) {
        var tree = jiggleTrees[index];
        #if UNITY_EDITOR
        if (!Validate(tree)) {
            return;
        }
        #endif
        Cache(ref tree);
        VerletIntegrate(tree);
        Constrain(tree);
        FinishStep(tree);
        ApplyPose(tree);
        jiggleTrees[index].Sanitize();
    }
}

}