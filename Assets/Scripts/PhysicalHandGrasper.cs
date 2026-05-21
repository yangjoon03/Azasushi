using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 손 전체의 파지(Grasp) 로직을 담당한다.
///
/// ── 파지 확정 조건 (모두 충족해야 함) ──────────────────────────
///  1. 접촉 뼈대 수  >= minContactBonesForGrasp
///  2. 서로 다른 손가락 수 >= minDistinctFingers   ← 한 손가락 달라붙기 차단
///  3. 평균 침투 깊이 >= minPenetrationDepth        ← 근처 지나가기 차단 (핵심)
///  4. 위에서만 누르는 접촉 비율 < topOnlyRatioLimit
/// </summary>
public class PhysicalHandGrasper : MonoBehaviour
{
    // ── 인스펙터 ───────────────────────────────────────────────────
    [Header("Grasp Thresholds")]
    [Tooltip("파지 확정에 필요한 최소 접촉 뼈대 수 (뼈대 마디 기준)")]
    public int minContactBonesForGrasp = 3;

    [Tooltip("파지 확정에 필요한 최소 서로 다른 손가락 수 (한 손가락만 닿는 경우 차단)")]
    public int minDistinctFingers = 2;

    [Tooltip("파지 확정에 필요한 뼈대당 최소 평균 침투 깊이 (m). 핵심 게이팅 값.\n" +
             "너무 낮으면 스치기만 해도 파지됨. 권장: 0.003~0.006")]
    public float minPenetrationDepth = 0.004f;

    [Tooltip("중력 지지 판단 각도 허용 범위 (도)")]
    public float gravitySupportAngleThreshold = 60f;

    [Header("Release Thresholds")]
    [Tooltip("파지 해제: 접촉 뼈대가 Bounds 표면에서 이 거리 이상 멀어지면 해제 (m)")]
    public float releaseDistance = 0.03f;

    [Header("Grasp Direction")]
    [Tooltip("위에서만 누르는 접촉 비율이 이 값 이상이면 파지 무효 (0~1)")]
    [Range(0.5f, 1.0f)]
    public float topOnlyRatioLimit = 0.80f;

    [Header("Hand Root Bone")]
    public Transform wristBone;

    [Header("Debug")]
    [Tooltip("활성화 시 파지 평가 상세 로그 출력 (Console에서 확인)")]
    public bool debugLog = true;

    // ── 내부 상태 ──────────────────────────────────────────────────
    private Dictionary<GraspableObject, ObjectContactState> contactStates
        = new Dictionary<GraspableObject, ObjectContactState>();

    private Dictionary<GraspableObject, GraspState> activeGrasps
        = new Dictionary<GraspableObject, GraspState>();

    // ── 데이터 구조 ────────────────────────────────────────────────
    private class ObjectContactState
    {
        public Dictionary<FingerBoneCollider, FingerBoneCollider.BoneContactData> boneContacts
            = new Dictionary<FingerBoneCollider, FingerBoneCollider.BoneContactData>();

        public int ContactCount => boneContacts.Count;

        /// <summary>서로 다른 손가락(FingerType) 수</summary>
        public int DistinctFingerCount()
        {
            var fingers = new HashSet<FingerType>();
            foreach (var b in boneContacts.Keys)
                fingers.Add(b.fingerType);
            return fingers.Count;
        }

        /// <summary>접촉 중인 뼈대들의 평균 침투 깊이</summary>
        public float AveragePenetrationDepth()
        {
            if (boneContacts.Count == 0) return 0f;
            float sum = 0f;
            foreach (var d in boneContacts.Values)
                sum += d.penetrationDepth;
            return sum / boneContacts.Count;
        }
    }

    public class GraspState
    {
        public GraspableObject target;
        public Vector3 graspAnchorLocal;
        public Quaternion graspRotationOffset;
        public float graspForce;
        public bool isGravitySupported;
        public Vector3 graspCenter;
    }

    // ── Unity 생명주기 ─────────────────────────────────────────────
    private void FixedUpdate()
    {
        EvaluateAllGrasps();
    }

    // ── 뼈대 콜백 ─────────────────────────────────────────────────
    public void OnBoneContactEnter(FingerBoneCollider bone,
                                    GraspableObject obj,
                                    FingerBoneCollider.BoneContactData data)
    {
        EnsureContactState(obj).boneContacts[bone] = data;
    }

    public void OnBoneContactStay(FingerBoneCollider bone,
                                   GraspableObject obj,
                                   FingerBoneCollider.BoneContactData data)
    {
        EnsureContactState(obj).boneContacts[bone] = data;
    }

    public void OnBoneContactExit(FingerBoneCollider bone, GraspableObject obj)
    {
        // 파지 확정 중에는 isKinematic 전환으로 인한 가짜 Exit 무시
        if (activeGrasps.ContainsKey(obj)) return;

        if (!contactStates.TryGetValue(obj, out var state)) return;
        state.boneContacts.Remove(bone);

        if (state.ContactCount == 0)
            contactStates.Remove(obj);
    }

    // ── 파지 평가 메인 ────────────────────────────────────────────
    private void EvaluateAllGrasps()
    {
        // 1. 새로운 파지 확정 시도
        foreach (var kvp in contactStates)
            EvaluateGraspForObject(kvp.Key, kvp.Value);

        // 2. 기존 파지 해제 판정
        var toRelease = activeGrasps.Keys
            .Where(ShouldRelease)
            .ToList();

        foreach (var o in toRelease)
            TryReleaseGrasp(o);
    }

    private void EvaluateGraspForObject(GraspableObject obj, ObjectContactState state)
    {
        bool isAlreadyGrasped = activeGrasps.ContainsKey(obj);

        // ── 조건 1: 접촉 뼈대 수 ──────────────────────────────────
        if (state.ContactCount < minContactBonesForGrasp)
        {
            if (debugLog && state.ContactCount > 0)
                Debug.Log($"[Grasp] {obj.name} — ❌ 뼈대 수 부족 {state.ContactCount}/{minContactBonesForGrasp}");
            if (isAlreadyGrasped) TryReleaseGrasp(obj);
            return;
        }

        // ── 조건 2: 서로 다른 손가락 수 ──────────────────────────
        int distinctFingers = state.DistinctFingerCount();
        if (distinctFingers < minDistinctFingers)
        {
            if (debugLog)
                Debug.Log($"[Grasp] {obj.name} — ❌ 손가락 수 부족 {distinctFingers}/{minDistinctFingers}");
            if (isAlreadyGrasped) TryReleaseGrasp(obj);
            return;
        }

        // ── 조건 3: 평균 침투 깊이 ────────────────────────────────
        float avgDepth = state.AveragePenetrationDepth();
        if (avgDepth < minPenetrationDepth)
        {
            if (debugLog)
                Debug.Log($"[Grasp] {obj.name} — ❌ 침투 깊이 부족 " +
                          $"{avgDepth * 1000:F2}mm / 필요 {minPenetrationDepth * 1000:F2}mm");
            if (isAlreadyGrasped) TryReleaseGrasp(obj);
            return;
        }

        // ── 조건 4: 파지 방향 ──────────────────────────────────────
        if (!IsGraspDirectionValid(state))
        {
            if (debugLog)
                Debug.Log($"[Grasp] {obj.name} — ❌ 방향 무효 (위에서만 누름)");
            if (isAlreadyGrasped) TryReleaseGrasp(obj);
            return;
        }

        // ── 파지 확정 ─────────────────────────────────────────────
        float force = Mathf.Clamp01(avgDepth / 0.015f);
        bool gravitySupported = CheckGravitySupport(state);

        if (!isAlreadyGrasped)
            InitiateGrasp(obj, state, force, gravitySupported);
        else
        {
            var gs = activeGrasps[obj];
            gs.graspForce = force;
            gs.isGravitySupported = gravitySupported;
        }
    }

    // ── 파지 해제 판정 ────────────────────────────────────────────
    /// <summary>
    /// 접촉 뼈대의 50% 이상이 Bounds 표면에서 releaseDistance 이상 멀어지면 해제.
    /// isKinematic 전환으로 contactStates가 비워지지 않으므로 거리 기반으로 판정.
    /// </summary>
    private bool ShouldRelease(GraspableObject obj)
    {
        if (!contactStates.TryGetValue(obj, out var state)) return true;
        if (state.ContactCount == 0) return true;

        Bounds bounds = obj.GetWorldBounds();
        int farCount = 0;

        foreach (var bone in state.boneContacts.Keys)
        {
            Vector3 closest = bounds.ClosestPoint(bone.WorldPosition);
            if (Vector3.Distance(bone.WorldPosition, closest) > releaseDistance)
                farCount++;
        }

        return farCount >= Mathf.CeilToInt(state.ContactCount * 0.5f);
    }

    // ── 파지 방향 유효성 ─────────────────────────────────────────
    private bool IsGraspDirectionValid(ObjectContactState state)
    {
        Vector3 gravityDir = Physics.gravity.normalized;
        int topOnlyContacts = 0;

        foreach (var kvp in state.boneContacts)
        {
            float angle = Vector3.Angle(gravityDir, kvp.Value.contactNormal);
            if (angle < gravitySupportAngleThreshold)
                topOnlyContacts++;
        }

        float topRatio = (float)topOnlyContacts / state.ContactCount;
        return topRatio < topOnlyRatioLimit;
    }

    // ── 중력 지지 판단 ────────────────────────────────────────────
    private bool CheckGravitySupport(ObjectContactState state)
    {
        Vector3 gravityDir = Physics.gravity.normalized;

        foreach (var kvp in state.boneContacts)
        {
            float angle = Vector3.Angle(-gravityDir, kvp.Value.contactNormal);
            if (angle < gravitySupportAngleThreshold)
                return true;
        }
        return false;
    }

    // ── 파지 시작 ─────────────────────────────────────────────────
    private void InitiateGrasp(GraspableObject obj, ObjectContactState state,
                                float force, bool gravitySupported)
    {
        Vector3 graspCenter = Vector3.zero;
        foreach (var kvp in state.boneContacts)
            graspCenter += kvp.Value.contactPoint;
        graspCenter /= state.ContactCount;

        Transform anchor = wristBone != null ? wristBone : transform;
        Vector3 localAnchor = anchor.InverseTransformPoint(obj.transform.position);
        Quaternion rotOffset = Quaternion.Inverse(anchor.rotation) * obj.transform.rotation;

        var graspState = new GraspState
        {
            target = obj,
            graspAnchorLocal = localAnchor,
            graspRotationOffset = rotOffset,
            graspForce = force,
            isGravitySupported = gravitySupported,
            graspCenter = graspCenter
        };

        activeGrasps[obj] = graspState;
        obj.OnGrasped(graspState, this);

        Debug.Log($"[Grasp] ✅ {obj.name} 파지 확정 — " +
                  $"뼈대:{state.ContactCount}, 손가락:{state.DistinctFingerCount()}, " +
                  $"깊이:{state.AveragePenetrationDepth() * 1000:F2}mm, " +
                  $"힘:{force:F2}, 중력지지:{gravitySupported}");
    }

    private void TryReleaseGrasp(GraspableObject obj)
    {
        if (!activeGrasps.TryGetValue(obj, out var gs)) return;
        activeGrasps.Remove(obj);
        obj.OnReleased(gs);
        Debug.Log($"[Grasp] ❌ {obj.name} 파지 해제");
    }

    // ── 외부 접근자 ───────────────────────────────────────────────
    public bool IsGrasping(GraspableObject obj) => activeGrasps.ContainsKey(obj);

    public GraspState GetGraspState(GraspableObject obj)
        => activeGrasps.TryGetValue(obj, out var gs) ? gs : null;

    // ── 헬퍼 ──────────────────────────────────────────────────────
    private ObjectContactState EnsureContactState(GraspableObject obj)
    {
        if (!contactStates.TryGetValue(obj, out var s))
        {
            s = new ObjectContactState();
            contactStates[obj] = s;
        }
        return s;
    }
}