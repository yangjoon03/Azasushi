using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 손 전체의 파지(Grasp) 로직을 담당한다.
/// - 접촉 뼈대 수 집계
/// - 중력 반대 방향 지지 여부 판단
/// - 힘 강도(침투 깊이 기반) 계산
/// - 파지 확정 / 해제 이벤트 발행
/// 
/// 각 손(Left/Right)마다 독립 인스턴스가 붙으므로,
/// activeGrasps / contactStates 는 이 손 전용 상태이다.
/// → 양손이 같은 오브젝트를 잡아도 서로 독립적으로 파지·해제된다.
/// </summary>
public class PhysicalHandGrasper : MonoBehaviour
{
    // ── 인스펙터 설정 ──────────────────────────────────────────────
    [Header("Grasp Thresholds")]
    [Tooltip("파지 확정에 필요한 최소 접촉 뼈대 수")]
    public int minContactBonesForGrasp = 3;

    [Tooltip("파지 확정에 필요한 최소 힘 강도 (0~1)")]
    public float minGraspForce = 0.15f;

    [Tooltip("중력 지지 판단 각도 허용 범위 (도)")]
    public float gravitySupportAngleThreshold = 60f;

    [Header("Penetration → Force Mapping")]
    [Tooltip("이 침투 깊이(m) 이상이면 최대 힘으로 판정")]
    public float maxPenetrationDepth = 0.015f;

    [Header("Finger Spread Detection")]
    [Tooltip("손가락이 오브젝트 크기보다 더 안쪽으로 들어온 비율 기준 (0~1)")]
    public float innerGraspRatio = 0.5f;

    [Header("Hand Root Bone")]
    public Transform wristBone;

    // ── 내부 상태 ──────────────────────────────────────────────────
    private Dictionary<GraspableObject, ObjectContactState> contactStates
        = new Dictionary<GraspableObject, ObjectContactState>();

    private Dictionary<GraspableObject, GraspState> activeGrasps
        = new Dictionary<GraspableObject, GraspState>();

    private List<FingerBoneCollider> allBones = new List<FingerBoneCollider>();

    // ── 데이터 구조체 ──────────────────────────────────────────────
    private class ObjectContactState
    {
        public Dictionary<FingerBoneCollider, FingerBoneCollider.BoneContactData> boneContacts
            = new Dictionary<FingerBoneCollider, FingerBoneCollider.BoneContactData>();

        public int ContactCount => boneContacts.Count;
    }

    public class GraspState
    {
        public GraspableObject target;
        public Vector3 graspAnchorLocal;      // 파지 시작 시점 손 기준 로컬 앵커
        public Quaternion graspRotationOffset;   // 파지 시작 시점 회전 오프셋
        public float graspForce;            // 0~1
        public bool isGravitySupported;
        public Vector3 graspCenter;
    }

    // ── Unity 생명주기 ─────────────────────────────────────────────
    private void Awake()
    {
        allBones.AddRange(GetComponentsInChildren<FingerBoneCollider>());
    }

    private void FixedUpdate()
    {
        EvaluateAllGrasps();
    }

    // ── 뼈대에서 호출되는 콜백 ────────────────────────────────────
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
        // isKinematic 전환 시 Unity가 강제로 Exit를 발생시키므로,
        // 이미 파지 확정된 오브젝트의 Exit는 무시한다.
        if (activeGrasps.ContainsKey(obj)) return;

        if (!contactStates.TryGetValue(obj, out var state)) return;
        state.boneContacts.Remove(bone);

        if (state.ContactCount == 0)
        {
            contactStates.Remove(obj);
            TryReleaseGrasp(obj);
        }
    }

    // ── 파지 평가 ──────────────────────────────────────────────────
    private void EvaluateAllGrasps()
    {
        foreach (var kvp in contactStates)
            EvaluateGraspForObject(kvp.Key, kvp.Value);

        // 파지 해제: contactStates 없음 OR 손가락이 충분히 펼쳐짐
        var toRelease = activeGrasps.Keys
            .Where(o => !contactStates.ContainsKey(o) || IsFingerSpreadEnoughToRelease(o))
            .ToList();

        foreach (var o in toRelease) TryReleaseGrasp(o);
    }

    private bool IsFingerSpreadEnoughToRelease(GraspableObject obj)
    {
        if (!activeGrasps.ContainsKey(obj)) return false;

        Bounds bounds = obj.GetWorldBounds();
        float objRadius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);

        Vector3 objCenter = obj.transform.position;
        float totalDist = 0f;
        int count = 0;

        foreach (var bone in allBones)
        {
            totalDist += Vector3.Distance(bone.WorldPosition, objCenter);
            count++;
        }
        if (count == 0) return false;

        float avgDist = totalDist / count;
        return avgDist > objRadius + 0.01f;
    }

    private void EvaluateGraspForObject(GraspableObject obj, ObjectContactState state)
    {
        bool isAlreadyGrasped = activeGrasps.ContainsKey(obj);

        if (state.ContactCount < minContactBonesForGrasp)
        {
            if (isAlreadyGrasped) TryReleaseGrasp(obj);
            return;
        }

        float force = CalculateGraspForce(obj, state);
        if (force < minGraspForce)
        {
            if (isAlreadyGrasped) TryReleaseGrasp(obj);
            return;
        }

        bool gravitySupported = CheckGravitySupport(state);

        if (!IsGraspDirectionValid(obj, state))
        {
            if (isAlreadyGrasped) TryReleaseGrasp(obj);
            return;
        }

        if (!isAlreadyGrasped)
            InitiateGrasp(obj, state, force, gravitySupported);
        else
        {
            var gs = activeGrasps[obj];
            gs.graspForce = force;
            gs.isGravitySupported = gravitySupported;
        }
    }

    // ── 힘 계산 ────────────────────────────────────────────────────
    private float CalculateGraspForce(GraspableObject obj, ObjectContactState state)
    {
        float totalForce = 0f;
        Bounds objBounds = obj.GetWorldBounds();

        foreach (var kvp in state.boneContacts)
        {
            var bone = kvp.Key;
            var data = kvp.Value;

            float depthForce = Mathf.Clamp01(data.penetrationDepth / maxPenetrationDepth);
            float innerBonus = CalculateInnerPenetrationBonus(bone.WorldPosition, obj, objBounds);
            totalForce += depthForce + innerBonus * 0.5f;
        }

        return Mathf.Clamp01(totalForce / Mathf.Max(1, state.ContactCount));
    }

    private float CalculateInnerPenetrationBonus(Vector3 boneWorld,
                                                 GraspableObject obj,
                                                 Bounds bounds)
    {
        Vector3 localPos = obj.transform.InverseTransformPoint(boneWorld);
        Vector3 halfSize = bounds.extents * innerGraspRatio;

        float dx = Mathf.Clamp01(1f - Mathf.Abs(localPos.x) / halfSize.x);
        float dy = Mathf.Clamp01(1f - Mathf.Abs(localPos.y) / halfSize.y);
        float dz = Mathf.Clamp01(1f - Mathf.Abs(localPos.z) / halfSize.z);

        return (dx + dy + dz) / 3f;
    }

    // ── 중력 지지 판단 ─────────────────────────────────────────────
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

    // ── 파지 방향 유효성 ───────────────────────────────────────────
    private bool IsGraspDirectionValid(GraspableObject obj, ObjectContactState state)
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
        return topRatio < 0.8f;
    }

    // ── 파지 시작 / 해제 ───────────────────────────────────────────
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

        Debug.Log($"[Grasp] {obj.name} 파지 확정 — 접촉 뼈대:{state.ContactCount}, " +
                  $"힘:{force:F2}, 중력지지:{gravitySupported}");
    }

    private void TryReleaseGrasp(GraspableObject obj)
    {
        if (!activeGrasps.TryGetValue(obj, out var gs)) return;
        activeGrasps.Remove(obj);
        obj.OnReleased(gs);
        Debug.Log($"[Grasp] {obj.name} 파지 해제");
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
