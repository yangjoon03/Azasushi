using UnityEngine;
using UnityEngine.XR.Hands; // OpenXR Hand Tracking package
using System.Collections.Generic;

/// <summary>
/// OpenXR Hand Tracking 데이터를 읽어
/// 1) 각 FingerBoneCollider의 위치/회전을 XR 관절 데이터로 업데이트
/// 2) 손가락 움직임 벡터를 파지 중인 오브젝트에 전달
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

    // 뼈대별 이전 프레임 위치 (손가락 이동 벡터 계산용)
    private Dictionary<FingerBoneCollider, Vector3> prevBonePositions
        = new Dictionary<FingerBoneCollider, Vector3>();

    // 강제 접촉 해제 타이머
    private float separationTimer = 0f;

    // ── Unity 생명주기 ─────────────────────────────────────────────
    private void Awake()
    {
        handGrasper = GetComponent<PhysicalHandGrasper>();
        if (handGrasper == null)
            handGrasper = GetComponentInParent<PhysicalHandGrasper>();

        // 뼈대 초기 위치 기록
        foreach (var mapping in boneMappings)
            if (mapping.boneCollider != null)
                prevBonePositions[mapping.boneCollider] = mapping.boneCollider.WorldPosition;
    }

    private void OnEnable()
    {
        // XRHandSubsystem 취득
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
        // Physics 업데이트와 동기화
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
    private void PropagateFingerDisplacement()
    {
        // 현재 파지 중인 오브젝트가 없으면 스킵
        // (handGrasper 내부 상태는 직접 접근 대신 이벤트로 받거나
        //  여기서는 모든 GraspableObject를 씬에서 찾는 방식 사용)
        var graspables = FindObjectsByType<GraspableObject>(FindObjectsSortMode.None);

        foreach (var graspable in graspables)
        {
            if (!graspable.IsGrasped) continue;

            // 각 뼈대의 이동 벡터 평균
            Vector3 avgDelta = Vector3.zero;
            int count = 0;

            foreach (var mapping in boneMappings)
            {
                if (mapping.boneCollider == null) continue;

                Vector3 currentPos = mapping.boneCollider.WorldPosition;
                if (prevBonePositions.TryGetValue(mapping.boneCollider, out Vector3 prev))
                {
                    // 이 뼈대가 오브젝트와 접촉 중인지 확인
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
                // 너무 큰 델타는 트래킹 점프로 판단해 무시
                if (avgDelta.magnitude < 0.05f)
                    graspable.ApplyFingerDisplacement(avgDelta);
            }
        }

        // 파지 중인 오브젝트 없을 때도 이전 위치 갱신
        foreach (var mapping in boneMappings)
            if (mapping.boneCollider != null)
                prevBonePositions[mapping.boneCollider] = mapping.boneCollider.WorldPosition;
    }

    // ── 분리 감지 및 강제 접촉 해제 ──────────────────────────────
    /// <summary>
    /// 뼈대가 오브젝트 표면에서 separationDistance 이상 멀어지면
    /// OnCollisionExit이 발생하지 않은 경우에도 강제로 접촉 해제
    /// </summary>
    private void CheckAndForceSeparation()
    {
        var graspables = FindObjectsByType<GraspableObject>(FindObjectsSortMode.None);

        foreach (var graspable in graspables)
        {
            Bounds objBounds = graspable.GetWorldBounds();

            foreach (var mapping in boneMappings)
            {
                if (mapping.boneCollider == null) continue;

                Vector3 bonePos = mapping.boneCollider.WorldPosition;
                Vector3 closestPt = objBounds.ClosestPoint(bonePos);
                float dist = Vector3.Distance(bonePos, closestPt);

                // 충분히 멀어졌고, 아직 접촉 기록이 남아있으면 강제 해제
                if (dist > separationDistance)
                {
                    ForceRemoveContact(mapping.boneCollider, graspable);
                }
            }
        }
    }

    private void ForceRemoveContact(FingerBoneCollider bone, GraspableObject obj)
    {
        // Reflection 대신 내부 메서드를 통해 해제
        // FingerBoneCollider에서 직접 handGrasper에 통보
        handGrasper?.OnBoneContactExit(bone, obj);
    }

    private bool IsContactingObject(FingerBoneCollider bone, GraspableObject obj)
    {
        foreach (var o in bone.GetContactedObjects())
            if (o == obj) return true;
        return false;
    }
}