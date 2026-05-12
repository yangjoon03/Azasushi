using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 손 전체의 파지(Grasp) 로직을 담당한다.
/// - 접촉 뼈대 수 집계
/// - 중력 반대 방향 지지 여부 판단
/// - 힘 강도(침투 깊이 기반) 계산
/// - 파지 확정 / 해제 이벤트 발행
/// </summary>
public class PhysicalHandGrasper : MonoBehaviour
{
    // ── 인스펙터 설정 ──────────────────────────────────────────────
    [Header("Grasp Thresholds")]
    [Tooltip("파지 확정에 필요한 최소 접촉 뼈대 수")]
    public int minContactBonesForGrasp = 3;

    [Tooltip("파지 확정에 필요한 최소 힘 강도 (0~1)")]
    public float minGraspForce = 0.15f;

    [Tooltip("중력 지지 판단 각도 허용 범위 (도) — 이 각도 이내면 '지지됨'으로 판정")]
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
    // 오브젝트별로 접촉 중인 뼈대 집합과 각 뼈대의 접촉 데이터 저장
    private Dictionary<GraspableObject, ObjectContactState> contactStates
        = new Dictionary<GraspableObject, ObjectContactState>();

    // 현재 파지 확정된 오브젝트
    private Dictionary<GraspableObject, GraspState> activeGrasps
        = new Dictionary<GraspableObject, GraspState>();

    // 손 전체의 뼈대 목록 (Awake 시 수집)
    private List<FingerBoneCollider> allBones = new List<FingerBoneCollider>();

    // ── 데이터 구조체 ──────────────────────────────────────────────
    private class ObjectContactState
    {
        // 뼈대 → 접촉 데이터
        public Dictionary<FingerBoneCollider, FingerBoneCollider.BoneContactData> boneContacts
            = new Dictionary<FingerBoneCollider, FingerBoneCollider.BoneContactData>();

        public int ContactCount => boneContacts.Count;
    }

    public class GraspState
    {
        public GraspableObject target;
        public Vector3 graspAnchorLocal;      // 파지 시작 시점 오브젝트 로컬 앵커 (손 기준)
        public Quaternion graspRotationOffset; // 파지 시작 시점 회전 오프셋
        public float graspForce;            // 0~1
        public bool isGravitySupported;    // 중력 반대 접촉 존재 여부
        public Vector3 graspCenter;           // 월드 좌표 파지 중심점
    }

    // ── Unity 생명주기 ─────────────────────────────────────────────
    private void Awake()
    {
        allBones.AddRange(GetComponentsInChildren<FingerBoneCollider>());
    }

    private void FixedUpdate()
    {
        // 매 물리 프레임마다 파지 조건 재평가
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

    //public void OnBoneContactExit(FingerBoneCollider bone, GraspableObject obj)
    //{
    //    if (!contactStates.TryGetValue(obj, out var state)) return;
    //    state.boneContacts.Remove(bone);

    //    if (state.ContactCount == 0)
    //    {
    //        contactStates.Remove(obj);
    //        TryReleaseGrasp(obj);
    //    }
    //}
    //public void OnBoneContactExit(FingerBoneCollider bone, GraspableObject obj)
    //{
    //    // 이미 파지 확정된 오브젝트는 OnCollisionExit 무시
    //    // (isKinematic 전환 시 Unity가 강제로 Exit 발생시키기 때문)
    //    if (activeGrasps.ContainsKey(obj)) return;

    //    if (!contactStates.TryGetValue(obj, out var state)) return;
    //    state.boneContacts.Remove(bone);

    //    if (state.ContactCount == 0)
    //    {
    //        contactStates.Remove(obj);
    //        TryReleaseGrasp(obj);
    //    }
    //}
    public void OnBoneContactExit(FingerBoneCollider bone, GraspableObject obj)
    {
        if (activeGrasps.ContainsKey(obj)) return;  // 유지

        if (!contactStates.TryGetValue(obj, out var state)) return;
        state.boneContacts.Remove(bone);

        if (state.ContactCount == 0)
        {
            contactStates.Remove(obj);
            TryReleaseGrasp(obj);
        }
    }

    // ── 파지 평가 ──────────────────────────────────────────────────
    //private void EvaluateAllGrasps()
    //{
    //    // 현재 접촉 중인 모든 오브젝트 평가
    //    foreach (var kvp in contactStates)
    //    {
    //        var obj   = kvp.Key;
    //        var state = kvp.Value;
    //        EvaluateGraspForObject(obj, state);
    //    }

    //    // 더 이상 접촉 없는 오브젝트 해제
    //    var toRelease = activeGrasps.Keys
    //        .Where(o => !contactStates.ContainsKey(o))
    //        .ToList();
    //    foreach (var o in toRelease) TryReleaseGrasp(o);
    //}
    // ── 파지 평가 ──────────────────────────────────────────────
    //private void EvaluateAllGrasps()          // 기존 메서드 전체 교체
    //{
    //    foreach (var kvp in contactStates)
    //        EvaluateGraspForObject(kvp.Key, kvp.Value);

    //    var toRelease = activeGrasps.Keys
    //        .Where(o => !contactStates.ContainsKey(o) && !IsHandNearObject(o))
    //        .ToList();
    //    foreach (var o in toRelease) TryReleaseGrasp(o);
    //}


    //private void EvaluateAllGrasps()
    //{
    //    foreach (var kvp in contactStates)
    //        EvaluateGraspForObject(kvp.Key, kvp.Value);

    //    var toRelease = activeGrasps.Keys
    //        .Where(o => !contactStates.ContainsKey(o) && !IsHandNearObject(o))
    //        .ToList();

    //    // 해제 대상 로그
    //    foreach (var o in activeGrasps.Keys)
    //    {
    //        bool inContact = contactStates.ContainsKey(o);
    //        bool handNear = IsHandNearObject(o);
    //        Debug.Log($"{o.name} — contactStates에 있음:{inContact}, 손 근처:{handNear}");
    //    }

    //    foreach (var o in toRelease) TryReleaseGrasp(o);
    //}
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

    /// <summary>
    /// 파지 중 손가락들이 오브젝트에서 충분히 멀어지면 true → 파지 해제
    /// </summary>
    //private bool IsFingerSpreadEnoughToRelease(GraspableObject obj)
    //{
    //    if (!activeGrasps.ContainsKey(obj)) return false;

    //    Vector3 objCenter = obj.transform.position;
    //    float totalDist = 0f;
    //    int count = 0;
    //    foreach (var bone in allBones)
    //    {
    //        totalDist += Vector3.Distance(bone.WorldPosition, objCenter);
    //        count++;
    //    }
    //    if (count == 0) return false;

    //    float avgDist = totalDist / count;

    //    // 오브젝트 크기의 1.5배 이상 거리면 손이 펼쳐진 것으로 판단
    //    float objRadius = obj.GetWorldBounds().extents.magnitude;
    //    return avgDist > objRadius * 1.5f;
    //}

    private bool IsFingerSpreadEnoughToRelease(GraspableObject obj)
    {
        if (!activeGrasps.ContainsKey(obj)) return false;

        // 오브젝트 Collider 기반 실제 크기 사용
        // extents.magnitude 대신 가장 긴 축의 절반 값만 사용
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

        // 오브젝트 실제 크기 + 5cm 여유만큼 떨어지면 해제
        return avgDist > objRadius + 0.01f;
    }

    private void EvaluateGraspForObject(GraspableObject obj, ObjectContactState state)
    {
        bool isAlreadyGrasped = activeGrasps.ContainsKey(obj);

        // 1. 접촉 뼈대 수 조건
        if (state.ContactCount < minContactBonesForGrasp)
        {
            if (isAlreadyGrasped) TryReleaseGrasp(obj);
            return;
        }

        // 2. 힘 강도 계산
        float force = CalculateGraspForce(obj, state);
        if (force < minGraspForce)
        {
            if (isAlreadyGrasped) TryReleaseGrasp(obj);
            return;
        }

        // 3. 중력 지지 방향 검사
        bool gravitySupported = CheckGravitySupport(state);

        // 4. 파지 방향 유효성 검사 (위에서 누르는 경우 차단)
        if (!IsGraspDirectionValid(obj, state))
        {
            if (isAlreadyGrasped) TryReleaseGrasp(obj);
            return;
        }

        // ── 파지 확정 ──
        if (!isAlreadyGrasped)
        {
            InitiateGrasp(obj, state, force, gravitySupported);
        }
        else
        {
            // 기존 파지 상태 갱신
            var gs = activeGrasps[obj];
            gs.graspForce = force;
            gs.isGravitySupported = gravitySupported;
        }
    }

    // ── 힘 계산: 손가락이 오브젝트 내부로 얼마나 들어왔나 ─────────
    private float CalculateGraspForce(GraspableObject obj, ObjectContactState state)
    {
        float totalForce = 0f;
        Bounds objBounds = obj.GetWorldBounds();

        foreach (var kvp in state.boneContacts)
        {
            var bone = kvp.Key;
            var data = kvp.Value;

            // 기본 힘: 침투 깊이 비례
            float depthForce = Mathf.Clamp01(data.penetrationDepth / maxPenetrationDepth);

            // 보너스 힘: 뼈대가 오브젝트 중심을 얼마나 가로질렀나
            float innerBonus = CalculateInnerPenetrationBonus(bone.WorldPosition, obj, objBounds);

            totalForce += depthForce + innerBonus * 0.5f;
        }

        // 평균화 후 0~1 클램프
        return Mathf.Clamp01(totalForce / Mathf.Max(1, state.ContactCount));
    }

    /// <summary>
    /// 뼈대 위치가 오브젝트 크기의 중심 절반 영역 안에 있으면 보너스
    /// (손을 오브젝트 크기보다 안쪽으로 넣었을 때 강한 힘 판정)
    /// </summary>
    private float CalculateInnerPenetrationBonus(Vector3 boneWorld,
                                                  GraspableObject obj,
                                                  Bounds bounds)
    {
        Vector3 localPos = obj.transform.InverseTransformPoint(boneWorld);
        Vector3 halfSize = bounds.extents * innerGraspRatio; // 내부 절반 영역

        float dx = Mathf.Clamp01(1f - Mathf.Abs(localPos.x) / halfSize.x);
        float dy = Mathf.Clamp01(1f - Mathf.Abs(localPos.y) / halfSize.y);
        float dz = Mathf.Clamp01(1f - Mathf.Abs(localPos.z) / halfSize.z);

        return (dx + dy + dz) / 3f;
    }

    // ── 중력 지지 판단 ─────────────────────────────────────────────
    /// <summary>
    /// 접촉 법선들 중 중력(아래→위 = Vector3.up)과 충분히 반대되는 것이 있으면 true.
    /// 즉, 아래에서 받쳐주는 접촉이 존재하면 오브젝트가 떨어지지 않는다.
    /// </summary>
    private bool CheckGravitySupport(ObjectContactState state)
    {
        Vector3 gravityDir = Physics.gravity.normalized; // 보통 (0,-1,0)

        foreach (var kvp in state.boneContacts)
        {
            // contactNormal: 오브젝트 표면 → 손 방향
            // 중력과 반대(위쪽)인 법선 = 아래에서 받쳐주는 접촉
            float angle = Vector3.Angle(-gravityDir, kvp.Value.contactNormal);
            if (angle < gravitySupportAngleThreshold)
                return true;
        }
        return false;
    }

    // ── 파지 방향 유효성 (위에서 누르기 차단) ─────────────────────
    /// <summary>
    /// 모든 접촉 법선이 "중력 방향"(아래쪽)에 가깝다면 위에서만 누르는 상황.
    /// 이 경우 파지 무효.  반대로 옆면 또는 아래에서의 접촉이 포함되면 유효.
    /// </summary>
    private bool IsGraspDirectionValid(GraspableObject obj, ObjectContactState state)
    {
        Vector3 gravityDir = Physics.gravity.normalized;
        int topOnlyContacts = 0;

        foreach (var kvp in state.boneContacts)
        {
            Vector3 normal = kvp.Value.contactNormal;
            // 법선이 중력과 같은 방향(위→아래) ≈ 위에서 누름
            float angle = Vector3.Angle(gravityDir, normal);
            if (angle < gravitySupportAngleThreshold)
                topOnlyContacts++;
        }

        // 위에서만 누르는 접촉이 전체의 80% 이상이면 무효
        float topRatio = (float)topOnlyContacts / state.ContactCount;
        return topRatio < 0.8f;
    }

    // ── 파지 시작 / 해제 ───────────────────────────────────────────
    private void InitiateGrasp(GraspableObject obj, ObjectContactState state,
                                float force, bool gravitySupported)
    {
        // 파지 중심점: 모든 접촉점의 평균
        Vector3 graspCenter = Vector3.zero;
        foreach (var kvp in state.boneContacts)
            graspCenter += kvp.Value.contactPoint;
        graspCenter /= state.ContactCount;

        // 파지 앵커: wristBone 기준 로컬 좌표로 저장
        // (wristBone이 없으면 루트 transform 사용)
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