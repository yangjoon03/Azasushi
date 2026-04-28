using UnityEngine;
using UnityEngine.XR.Hands;
using System.Collections.Generic;

/// <summary>
/// OpenXR Hand Tracking 데이터를 읽어
/// 1) 각 FingerBoneCollider의 위치/회전을 XR 관절 데이터로 업데이트
/// 2) 손가락 움직임 벡터를 【이 손이 파지 중인】 오브젝트에만 전달  ← 양손 수정
/// 3) 손가락이 오브젝트로부터 멀어지면 접촉 해제를 강제 처리
/// </summary>
public class FingerDrivenGraspController : MonoBehaviour
{
    // ── 인스펙터 ──────────────────────────────────────────────────
    [Header("Hand Side")]
    public Handedness handedness = Handedness.Right;

    [Header("Bone Mapping")]
    [Tooltip("XRHandJointID와 FingerBoneCollider를 연결하는 매핑 목록")]
    public List<BoneMapping> boneMappings = new List<BoneMapping>();

    [Header("Separation Detection")]
    [Tooltip("이 거리 이상 뼈대가 오브젝트에서 떨어지면 접촉 해제 처리 (m)")]
    public float separationDistance = 0.03f;

    [Tooltip("접촉 해제 판정 주기 (초)")]
    public float separationCheckInterval = 0.05f;

    // ── 직렬화 가능 매핑 구조체 ───────────────────────────────────
    [System.Serializable]
    public struct BoneMapping
    {
        public XRHandJointID jointID;
        public FingerBoneCollider boneCollider;
    }

    // ── 내부 상태 ──────────────────────────────────────────────────
    private XRHandSubsystem handSubsystem;
    private PhysicalHandGrasper handGrasper;

    private Dictionary<FingerBoneCollider, Vector3> prevBonePositions
        = new Dictionary<FingerBoneCollider, Vector3>();

    private float separationTimer = 0f;

    // ── Unity 생명주기 ─────────────────────────────────────────────
    private void Awake()
    {
        handGrasper = GetComponent<PhysicalHandGrasper>();
        if (handGrasper == null)
            handGrasper = GetComponentInParent<PhysicalHandGrasper>();

        foreach (var mapping in boneMappings)
            if (mapping.boneCollider != null)
                prevBonePositions[mapping.boneCollider] = mapping.boneCollider.WorldPosition;
    }

    private void OnEnable()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
        {
            handSubsystem = subsystems[0];
            handSubsystem.updatedHands += OnHandsUpdated;
        }
        else
        {
            Debug.LogWarning("[FingerDrivenGraspController] XRHandSubsystem을 찾을 수 없습니다.");
        }
    }

    private void OnDisable()
    {
        if (handSubsystem != null)
            handSubsystem.updatedHands -= OnHandsUpdated;
    }

    private void Update()
    {
        separationTimer += Time.deltaTime;
        if (separationTimer >= separationCheckInterval)
        {
            separationTimer = 0f;
            CheckAndForceSeparation();
        }
    }

    // ── XR Hand 업데이트 콜백 ─────────────────────────────────────
    private void OnHandsUpdated(XRHandSubsystem subsystem,
                                XRHandSubsystem.UpdateSuccessFlags flags,
                                XRHandSubsystem.UpdateType updateType)
    {
        if (updateType != XRHandSubsystem.UpdateType.Dynamic) return;

        XRHand hand = handedness == Handedness.Right
            ? subsystem.rightHand
            : subsystem.leftHand;

        if (!hand.isTracked) return;

        UpdateBoneTransforms(hand);
        PropagateFingerDisplacement();
    }

    // ── 뼈대 트랜스폼 업데이트 ────────────────────────────────────
    private void UpdateBoneTransforms(XRHand hand)
    {
        foreach (var mapping in boneMappings)
        {
            if (mapping.boneCollider == null) continue;

            if (hand.GetJoint(mapping.jointID).TryGetPose(out Pose jointPose))
            {
                Transform t = mapping.boneCollider.transform;
                t.position = jointPose.position;
                t.rotation = jointPose.rotation;
            }
        }
    }

    // ── 손가락 이동 벡터 → GraspableObject 전달 ──────────────────
    /// <summary>
    /// [양손 수정] handGrasper.IsGrasping() 조건으로
    /// 이 손이 실제로 파지 중인 오브젝트에만 displacement를 적용한다.
    /// 반대 손이 잡은 오브젝트에는 절대 영향을 주지 않는다.
    /// </summary>
    private void PropagateFingerDisplacement()
    {
        var graspables = FindObjectsByType<GraspableObject>(FindObjectsSortMode.None);

        foreach (var graspable in graspables)
        {
            // ★ 변경: graspable.IsGrasped → handGrasper.IsGrasping(graspable)
            // 씬 전체의 파지 오브젝트가 아니라, 이 손이 잡은 것만 처리
            if (!handGrasper.IsGrasping(graspable)) continue;

            Vector3 avgDelta = Vector3.zero;
            int count = 0;

            foreach (var mapping in boneMappings)
            {
                if (mapping.boneCollider == null) continue;

                Vector3 currentPos = mapping.boneCollider.WorldPosition;
                if (prevBonePositions.TryGetValue(mapping.boneCollider, out Vector3 prev))
                {
                    if (IsContactingObject(mapping.boneCollider, graspable))
                    {
                        avgDelta += currentPos - prev;
                        count++;
                    }
                }
                prevBonePositions[mapping.boneCollider] = currentPos;
            }

            if (count > 0)
            {
                avgDelta /= count;
                if (avgDelta.magnitude < 0.05f)
                    graspable.ApplyFingerDisplacement(avgDelta);
            }
        }

        // 파지 중 오브젝트 없을 때도 이전 위치 갱신
        foreach (var mapping in boneMappings)
            if (mapping.boneCollider != null)
                prevBonePositions[mapping.boneCollider] = mapping.boneCollider.WorldPosition;
    }

    // ── 분리 감지 및 강제 접촉 해제 ──────────────────────────────
    /// <summary>
    /// [양손 수정] handGrasper.IsGrasping() 조건으로
    /// 이 손이 파지 중인 오브젝트에 대해서만 분리를 감지한다.
    /// 반대 손이 잡은 오브젝트의 분리를 이 손이 잘못 해제하는 일이 없다.
    /// </summary>
    private void CheckAndForceSeparation()
    {
        var graspables = FindObjectsByType<GraspableObject>(FindObjectsSortMode.None);

        foreach (var graspable in graspables)
        {
            // ★ 변경: 이 손이 잡고 있는 오브젝트만 분리 체크
            if (!handGrasper.IsGrasping(graspable)) continue;

            Bounds objBounds = graspable.GetWorldBounds();

            foreach (var mapping in boneMappings)
            {
                if (mapping.boneCollider == null) continue;

                Vector3 bonePos = mapping.boneCollider.WorldPosition;
                Vector3 closestPt = objBounds.ClosestPoint(bonePos);
                float dist = Vector3.Distance(bonePos, closestPt);

                if (dist > separationDistance)
                    ForceRemoveContact(mapping.boneCollider, graspable);
            }
        }
    }

    private void ForceRemoveContact(FingerBoneCollider bone, GraspableObject obj)
    {
        // 이 손의 handGrasper에만 Exit 통보 → 반대 손 파지에 영향 없음
        handGrasper?.OnBoneContactExit(bone, obj);
    }

    private bool IsContactingObject(FingerBoneCollider bone, GraspableObject obj)
    {
        foreach (var o in bone.GetContactedObjects())
            if (o == obj) return true;
        return false;
    }
}
