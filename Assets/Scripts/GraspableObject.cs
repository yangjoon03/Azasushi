using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 손으로 잡힐 수 있는 오브젝트에 부착한다.
/// 파지 상태에 따라 Rigidbody를 제어하며 손을 따라다니게 한다.
/// 양손 파지 시 각 손의 graspForce 비율로 위치·회전 모두 가중 평균한다. ← 양손 수정
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

    [Header("Layer Settings")]
    public string graspedLayerName = "GraspedObject";
    private int originalLayer;

    // ── 내부 상태 ──────────────────────────────────────────────────
    private Rigidbody rb;
    private bool wasKinematic;
    private bool wasGravityEnabled;

    private Dictionary<PhysicalHandGrasper, PhysicalHandGrasper.GraspState> currentGrasps
        = new Dictionary<PhysicalHandGrasper, PhysicalHandGrasper.GraspState>();

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
        if (currentGrasps.Count == 0) return;

        if (currentGrasps.Count == 1)
            FollowHand(GetFirstGrasp().Key, GetFirstGrasp().Value);
        else
            FollowMultipleHands();
    }

    // ── 파지 이벤트 수신 ───────────────────────────────────────────
    public void OnGrasped(PhysicalHandGrasper.GraspState state, PhysicalHandGrasper grasper)
    {
        if (!allowTwoHandGrasp && currentGrasps.Count > 0) return;

        currentGrasps[grasper] = state;

        if (currentGrasps.Count == 1)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            fingerDrivenOffset = Vector3.zero;

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
            gameObject.layer = originalLayer;

            if (!wasKinematic)
            {
                rb.linearVelocity = grasper.GetComponent<Rigidbody>()?.linearVelocity ?? Vector3.zero;
                rb.angularVelocity = grasper.GetComponent<Rigidbody>()?.angularVelocity ?? Vector3.zero;
            }
        }
    }

    // ── 손 추종 물리 제어 (단일 파지) ────────────────────────────
    private void FollowHand(PhysicalHandGrasper grasper, PhysicalHandGrasper.GraspState graspState)
    {
        Transform anchor = grasper.wristBone != null ? grasper.wristBone : grasper.transform;

        Vector3 targetPosition = anchor.TransformPoint(graspState.graspAnchorLocal);
        Quaternion targetRotation = anchor.rotation * graspState.graspRotationOffset;

        rb.MovePosition(targetPosition);
        rb.MoveRotation(targetRotation);
    }

    // ── 다중 파지 추종 ────────────────────────────────────────────
    /// <summary>
    /// [양손 수정] 위치뿐 아니라 회전도 graspForce 비율로 가중 평균한다.
    /// 
    /// 기존 코드는 Slerp 비율이 항상 0.5 고정이어서,
    /// 한 손이 더 강하게 잡고 있어도 회전이 반반으로 분할됐다.
    /// 수정 후에는 force 비율(weightA)만큼 첫 번째 손의 회전에 가깝게 유지된다.
    /// 
    /// 3개 이상의 손(미래 확장)을 대비해 누적 Slerp 방식으로 구현한다.
    /// 각 스텝에서 Slerp 비율 = currentWeight / (currentWeight + nextWeight)
    /// 이렇게 하면 순서에 관계없이 force 가중 평균과 동일한 결과가 된다.
    /// </summary>
    private void FollowMultipleHands()
    {
        Vector3 avgPos = Vector3.zero;
        Quaternion avgRot = Quaternion.identity;
        float totalWeight = 0f;
        bool firstHand = true;

        foreach (var kvp in currentGrasps)
        {
            var grasper = kvp.Key;
            var graspState = kvp.Value;
            float weight = Mathf.Max(graspState.graspForce, 0.01f); // 0 방지

            Transform anchor = grasper.wristBone != null ? grasper.wristBone : grasper.transform;

            // ── 위치: force 가중합 ──
            Vector3 pos = anchor.TransformPoint(graspState.graspAnchorLocal);
            Quaternion rot = anchor.rotation * graspState.graspRotationOffset;

            avgPos += pos * weight;

            // ── 회전: force 비율 누적 Slerp ──
            // ★ 변경: 기존 고정 0.5 → weight / (totalWeight + weight) 비율
            if (firstHand)
            {
                avgRot = rot;
                firstHand = false;
            }
            else
            {
                float t = weight / (totalWeight + weight);
                avgRot = Quaternion.Slerp(avgRot, rot, t);
            }

            totalWeight += weight;
        }

        if (totalWeight > 0f)
            avgPos /= totalWeight;

        rb.MovePosition(Vector3.Lerp(rb.position, avgPos, Time.fixedDeltaTime * 60f));
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, avgRot, Time.fixedDeltaTime * 60f));
    }

    // ── PD 제어력 ─────────────────────────────────────────────────
    private void ApplyPDForce(Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 posError = targetPos - rb.position;
        Vector3 velError = -rb.linearVelocity;
        Vector3 force = posError * positionGain + velError * positionDamping;
        rb.AddForce(force, ForceMode.Force);

        Quaternion rotError = targetRot * Quaternion.Inverse(rb.rotation);
        rotError.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;

        Vector3 torque = axis * (angle * Mathf.Deg2Rad) * rotationGain
                       - rb.angularVelocity * rotationDamping;
        rb.AddTorque(torque, ForceMode.Force);
    }

    // ── 손가락 움직임 반영 ────────────────────────────────────────
    public void ApplyFingerDisplacement(Vector3 worldDelta)
        => fingerDrivenOffset += worldDelta;

    public void ResetFingerOffset()
        => fingerDrivenOffset = Vector3.zero;

    // ── Bounds 접근자 ─────────────────────────────────────────────
    public Bounds GetWorldBounds()
    {
        if (customBounds.size != Vector3.zero)
            return new Bounds(transform.TransformPoint(customBounds.center), customBounds.size);

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
