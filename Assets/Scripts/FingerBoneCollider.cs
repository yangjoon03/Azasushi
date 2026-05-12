using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 손가락 각 마디에 부착되는 콜라이더 컴포넌트.
/// 접촉 중인 GraspableObject들을 추적하고 PhysicalHandGrasper에 보고한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FingerBoneCollider : MonoBehaviour
{
    [Header("Bone Identity")]
    public HandSide handSide;
    public FingerType fingerType;
    public BoneSegment boneSegment;

    [Header("Contact Settings")]
    [Tooltip("접촉 유지를 위한 최소 침투 깊이 (m)")]
    public float minPenetrationDepth = 0.001f;

    // 이 뼈대가 현재 접촉 중인 오브젝트 → 접촉 데이터 맵
    private Dictionary<GraspableObject, BoneContactData> activeContacts
        = new Dictionary<GraspableObject, BoneContactData>();

    private PhysicalHandGrasper handGrasper;
    private Collider boneCollider;

    // ── 접촉 데이터 구조체 ─────────────────────────────────────────
    public struct BoneContactData
    {
        public Vector3 contactPoint;      // 월드 좌표 접촉점
        public Vector3 contactNormal;     // 오브젝트 표면 → 손 방향 법선
        public float penetrationDepth;  // 손이 오브젝트 안으로 들어간 깊이
        public float contactTime;       // 접촉 시작 시각
    }

    // ── Unity 생명주기 ─────────────────────────────────────────────
    private void Awake()
    {
        boneCollider = GetComponent<Collider>();
        boneCollider.isTrigger = false; // 물리 충돌 유지

        // 루트 방향으로 PhysicalHandGrasper 탐색
        handGrasper = GetComponentInParent<PhysicalHandGrasper>();
        if (handGrasper == null)
            Debug.LogWarning($"[FingerBoneCollider] {name}: PhysicalHandGrasper를 찾을 수 없습니다.");
    }

    private void OnCollisionEnter(Collision collision)
    {
        var graspable = collision.gameObject.GetComponent<GraspableObject>();
        if (graspable == null) return;

        var data = BuildContactData(collision);
        activeContacts[graspable] = data;
        handGrasper?.OnBoneContactEnter(this, graspable, data);
    }

    private void OnCollisionStay(Collision collision)
    {
        var graspable = collision.gameObject.GetComponent<GraspableObject>();
        if (graspable == null) return;

        var data = BuildContactData(collision);
        activeContacts[graspable] = data; // 매 프레임 갱신
        handGrasper?.OnBoneContactStay(this, graspable, data);
    }

    private void OnCollisionExit(Collision collision)
    {
        var graspable = collision.gameObject.GetComponent<GraspableObject>();
        if (graspable == null) return;

        activeContacts.Remove(graspable);
        handGrasper?.OnBoneContactExit(this, graspable);
    }

    // ── 유틸리티 ───────────────────────────────────────────────────
    private BoneContactData BuildContactData(Collision collision)
    {
        // 여러 접촉점 중 가장 깊은 것 사용
        ContactPoint best = collision.contacts[0];
        float maxDepth = 0f;
        foreach (var cp in collision.contacts)
        {
            if (cp.separation < maxDepth) // separation은 음수일수록 더 깊이 침투
            {
                maxDepth = cp.separation;
                best = cp;
            }
        }

        return new BoneContactData
        {
            contactPoint = best.point,
            contactNormal = best.normal,      // 오브젝트 → 손 방향
            penetrationDepth = Mathf.Abs(best.separation),
            contactTime = Time.time
        };
    }

    /// <summary>현재 접촉 중인 GraspableObject 목록 반환</summary>
    public IEnumerable<GraspableObject> GetContactedObjects()
        => activeContacts.Keys;

    /// <summary>특정 오브젝트와의 접촉 데이터 반환</summary>
    public bool TryGetContactData(GraspableObject obj, out BoneContactData data)
        => activeContacts.TryGetValue(obj, out data);

    /// <summary>이 뼈대의 월드 좌표</summary>
    public Vector3 WorldPosition => transform.position;
}

// ── 열거형 정의 ────────────────────────────────────────────────────
public enum HandSide { Left, Right }
public enum FingerType { Thumb, Index, Middle, Ring, Pinky }
public enum BoneSegment { Proximal, Middle, Distal, Tip }