using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 손으로 잡힐 수 있는 오브젝트에 부착한다.
/// 파지 상태에 따라 Rigidbody를 제어하며 손을 따라다니게 한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GraspableObject : MonoBehaviour
{
    // ── 인스펙터 설정 ──────────────────────────────────────────────
    [Header("Follow Settings")]
    [Tooltip("파지 후 위치 추종 속도 (PD 제어 비례 게인)")]
    public float positionGain = 1000f;

    [Tooltip("파지 후 위치 추종 댐핑")]
    public float positionDamping = 100f;

    [Tooltip("파지 후 회전 추종 게인")]
    public float rotationGain = 500f;

    [Tooltip("파지 후 회전 댐핑")]
    public float rotationDamping = 50f;

    [Header("Multi-Hand Support")]
    [Tooltip("두 손으로 동시에 잡을 수 있는지 여부")]
    public bool allowTwoHandGrasp = true;

    [Header("Bounds")]
    [Tooltip("비어 있으면 자동으로 Renderer 기반 Bounds를 계산")]
    public Bounds customBounds;

    // ── 내부 상태 ──────────────────────────────────────────────────
    private Rigidbody rb;
    private bool wasKinematic;
    private bool wasGravityEnabled;

    // 현재 이 오브젝트를 잡고 있는 (grasper → graspState) 맵
    private Dictionary<PhysicalHandGrasper, PhysicalHandGrasper.GraspState> currentGrasps
        = new Dictionary<PhysicalHandGrasper, PhysicalHandGrasper.GraspState>();

    // 손가락 움직임에 의한 디스플레이스먼트 누적
    private Vector3 fingerDrivenOffset = Vector3.zero;

    // ── Unity 생명주기 ─────────────────────────────────────────────
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        wasKinematic = rb.isKinematic;
        wasGravityEnabled = rb.useGravity;
    }

    private void FixedUpdate()
    {
        Debug.Log($"FixedUpdate — currentGrasps.Count:{currentGrasps.Count}, isKinematic:{rb.isKinematic}");
        if (currentGrasps.Count == 0) return;

        if (currentGrasps.Count == 1)
        {
            var kvp = GetFirstGrasp();
            Debug.Log($"FollowHand 호출 — target:{kvp.Key.gameObject.name}, anchor:{kvp.Value.graspAnchorLocal}");
            FollowHand(kvp.Key, kvp.Value);
        }
        else
        {
            FollowMultipleHands();
        }
    }

    // ── 파지 이벤트 수신 ───────────────────────────────────────────
    /// <summary>PhysicalHandGrasper가 파지 확정 시 호출</summary>
    //public void OnGrasped(PhysicalHandGrasper.GraspState state, PhysicalHandGrasper grasper)
    //{
    //    if (!allowTwoHandGrasp && currentGrasps.Count > 0)
    //        return; // 두 손 파지 불허 시 무시

    //    currentGrasps[grasper] = state;

    //    // 첫 파지: Rigidbody를 Kinematic으로 전환해 손이 직접 이동시킴
    //    if (currentGrasps.Count == 1)
    //    {
    //        rb.isKinematic  = true;
    //        rb.useGravity   = false;
    //        fingerDrivenOffset = Vector3.zero;
    //    }
    //    Debug.Log($"OnGrasped 호출됨 — grasper: {grasper.gameObject.name}");
    //}

    ///// <summary>PhysicalHandGrasper가 파지 해제 시 호출</summary>
    //public void OnReleased(PhysicalHandGrasper.GraspState state)
    //{
    //    var grasper = FindGrasperByState(state);
    //    if (grasper == null) return;
    //    currentGrasps.Remove(grasper);

    //    if (currentGrasps.Count == 0)
    //    {
    //        // 완전 해제: 물리 복원 + 손 속도 전달
    //        rb.isKinematic  = wasKinematic;
    //        rb.useGravity   = wasGravityEnabled;

    //        if (!wasKinematic)
    //        {
    //            // 손의 속도를 오브젝트에 전달 (던지기 효과)
    //            rb.linearVelocity        = grasper.GetComponent<Rigidbody>()?.linearVelocity ?? Vector3.zero;
    //            rb.angularVelocity = grasper.GetComponent<Rigidbody>()?.angularVelocity ?? Vector3.zero;
    //        }
    //    }
    //}

    [Header("Layer Settings")]
    public string graspedLayerName = "GraspedObject";  // 파지 중 레이어
    private int originalLayer;                          // 원래 레이어 저장

    public void OnGrasped(PhysicalHandGrasper.GraspState state, PhysicalHandGrasper grasper)
    {
        if (!allowTwoHandGrasp && currentGrasps.Count > 0) return;

        currentGrasps[grasper] = state;

        if (currentGrasps.Count == 1)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            fingerDrivenOffset = Vector3.zero;

            // 파지 시 레이어 변경 → 손 뼈대와 충돌 차단
            originalLayer = gameObject.layer;
            gameObject.layer = LayerMask.NameToLayer(graspedLayerName);
        }
    }

    public void OnReleased(PhysicalHandGrasper.GraspState state)
    {
        var grasper = FindGrasperByState(state);
        if (grasper == null) return;
        currentGrasps.Remove(grasper);

        if (currentGrasps.Count == 0)
        {
            rb.isKinematic = wasKinematic;
            rb.useGravity = wasGravityEnabled;

            // 레이어 복원 → 손 뼈대와 충돌 재활성화
            gameObject.layer = originalLayer;

            if (!wasKinematic)
            {
                rb.linearVelocity = grasper.GetComponent<Rigidbody>()?.linearVelocity ?? Vector3.zero;
                rb.angularVelocity = grasper.GetComponent<Rigidbody>()?.angularVelocity ?? Vector3.zero;
            }
        }
    }

    // ── 손 추종 물리 제어 (단일 파지) ────────────────────────────
    //private void FollowHand(PhysicalHandGrasper grasper,
    //                         PhysicalHandGrasper.GraspState graspState)
    //{
    //    // 목표 위치: 파지 시점의 로컬 앵커를 손의 현재 월드 좌표로 변환
    //    Vector3 targetPosition = grasper.transform.TransformPoint(graspState.graspAnchorLocal);
    //    Debug.Log($"anchor(로컬):{graspState.graspAnchorLocal}, targetPos:{targetPosition}, 현재위치:{rb.position}");
    //    //rb.MovePosition(targetPosition);
    //    // 손가락 움직임에 의한 오프셋 추가
    //    targetPosition += fingerDrivenOffset;

    //    // 목표 회전
    //    Quaternion targetRotation = grasper.transform.rotation * graspState.graspRotationOffset;

    //    if (rb.isKinematic)
    //    {
    //        // Kinematic: MovePosition/MoveRotation으로 충돌 유지하며 이동
    //        rb.MovePosition(Vector3.Lerp(rb.position, targetPosition, Time.fixedDeltaTime * 60f));
    //        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * 60f));
    //    }
    //    else
    //    {
    //        // Non-kinematic: PD 제어력 적용
    //        ApplyPDForce(targetPosition, targetRotation);
    //    }
    //}



    private void FollowHand(PhysicalHandGrasper grasper, PhysicalHandGrasper.GraspState graspState)
    {
        Transform anchor = grasper.wristBone != null ? grasper.wristBone : grasper.transform;
        Debug.Log($"wristBone 현재 월드 위치: {anchor.position}"); // 손 움직일 때 이 값이 변해야 함

        Vector3 targetPosition = anchor.TransformPoint(graspState.graspAnchorLocal);
        Quaternion targetRotation = anchor.rotation * graspState.graspRotationOffset;

        rb.MovePosition(targetPosition);
        rb.MoveRotation(targetRotation);
    }

    // ── 다중 파지 추종 ────────────────────────────────────────────
    private void FollowMultipleHands()
    {
        Vector3 avgPos = Vector3.zero;
        Quaternion avgRot = Quaternion.identity;
        float totalWeight = 0f;
        int idx = 0;

        foreach (var kvp in currentGrasps)
        {
            var grasper = kvp.Key;
            var graspState = kvp.Value;
            float weight = graspState.graspForce;

            avgPos += grasper.transform.TransformPoint(graspState.graspAnchorLocal) * weight;
            totalWeight += weight;

            // 회전 평균 (Slerp 반복)
            Quaternion rot = grasper.transform.rotation * graspState.graspRotationOffset;
            avgRot = idx == 0 ? rot : Quaternion.Slerp(avgRot, rot, 0.5f);
            idx++;
        }

        if (totalWeight > 0f)
            avgPos /= totalWeight;

        rb.MovePosition(Vector3.Lerp(rb.position, avgPos, Time.fixedDeltaTime * 60f));
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, avgRot, Time.fixedDeltaTime * 60f));
    }

    // ── PD 제어력 ─────────────────────────────────────────────────
    private void ApplyPDForce(Vector3 targetPos, Quaternion targetRot)
    {
        // 위치 PD
        Vector3 posError = targetPos - rb.position;
        Vector3 velError = -rb.linearVelocity;
        Vector3 force = posError * positionGain + velError * positionDamping;
        rb.AddForce(force, ForceMode.Force);

        // 회전 PD
        Quaternion rotError = targetRot * Quaternion.Inverse(rb.rotation);
        rotError.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;

        Vector3 torque = axis * (angle * Mathf.Deg2Rad) * rotationGain
                       - rb.angularVelocity * rotationDamping;
        rb.AddTorque(torque, ForceMode.Force);
    }

    // ── 손가락 움직임 반영 ────────────────────────────────────────
    /// <summary>
    /// 손가락 뼈대의 위치 변화를 오브젝트 이동에 반영.
    /// FingerDrivenGraspController가 매 프레임 호출한다.
    /// </summary>
    public void ApplyFingerDisplacement(Vector3 worldDelta)
    {
        fingerDrivenOffset += worldDelta;
    }

    /// <summary>손가락 오프셋 초기화 (파지 시작 시)</summary>
    public void ResetFingerOffset() => fingerDrivenOffset = Vector3.zero;

    // ── Bounds 접근자 ─────────────────────────────────────────────
    public Bounds GetWorldBounds()
    {
        if (customBounds.size != Vector3.zero)
            return new Bounds(transform.TransformPoint(customBounds.center), customBounds.size);

        // Renderer 기반 자동 계산
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(transform.position, Vector3.one * 0.1f);

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);
        return combined;
    }

    // ── 유틸리티 ──────────────────────────────────────────────────
    private KeyValuePair<PhysicalHandGrasper, PhysicalHandGrasper.GraspState> GetFirstGrasp()
    {
        foreach (var kvp in currentGrasps) return kvp;
        return default;
    }

    private PhysicalHandGrasper FindGrasperByState(PhysicalHandGrasper.GraspState state)
    {
        foreach (var kvp in currentGrasps)
            if (kvp.Value == state) return kvp.Key;
        return null;
    }

    public bool IsGrasped => currentGrasps.Count > 0;
}