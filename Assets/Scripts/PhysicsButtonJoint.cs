using UnityEngine;

[RequireComponent(typeof(ConfigurableJoint))]
public class PhysicsButtonJoint : MonoBehaviour
{
    [Header("--- Reference ---")]
    [Tooltip("버튼의 원래 시작 위치 (빈 오브젝트 권장)")]
    public Transform startPoint;

    [Header("--- Toggle Settings ---")]
    [Tooltip("얼마나 깊이 눌려야 토글이 발생할지 (0.1 = 10cm)")]
    public float pressThreshold = 0.12f;
    [Tooltip("물리적으로 최대로 들어갈 수 있는 한계")]
    public float pressLimit = 0.15f;
    [Tooltip("버튼이 ON 상태일 때 멈춰있을 위치")]
    public float fixedDepth = 0.1f;

    [Header("--- Spring Settings ---")]
    [Range(0, 10000)]
    public float springStrength = 1500f; // 강도
    [Range(0, 500)]
    public float springDamper = 50f;     // 저항

    private Rigidbody rb;
    private ConfigurableJoint joint;
    private bool isOn = false;
    private bool wasPressed = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        joint = GetComponent<ConfigurableJoint>();

        // 물리 설정 초기화
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        SetupJoint();
    }

    void Start()
    {
        if (startPoint != null)
        {
            transform.position = startPoint.position;
        }
    }

    void SetupJoint()
    {
        if (joint == null) return;

        // Y축(Vertical)만 리미트를 걸고 나머지는 고정
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Limited;
        joint.zMotion = ConfigurableJointMotion.Locked;

        joint.angularXMotion = ConfigurableJointMotion.Locked;
        joint.angularYMotion = ConfigurableJointMotion.Locked;
        joint.angularZMotion = ConfigurableJointMotion.Locked;

        // 최대 이동 범위 설정
        SoftJointLimit limit = new SoftJointLimit();
        limit.limit = pressLimit;
        joint.linearLimit = limit;

        ApplySpring();
    }

    // 인스펙터에서 값을 바꿔도 즉시 반영되게 하는 로직
    void ApplySpring()
    {
        JointDrive drive = new JointDrive();
        drive.positionSpring = springStrength;
        drive.positionDamper = springDamper;
        drive.maximumForce = Mathf.Infinity;

        joint.yDrive = drive;
    }

    void FixedUpdate()
    {
        if (startPoint == null) return;

        // 스프링 값 실시간 갱신 (테스트 용도, 성능 최적화가 필요하면 뺄 것)
        ApplySpring();

        // 1. 현재 눌린 거리 계산 (Local Y 축 기준)
        float currentPos = -startPoint.InverseTransformPoint(transform.position).y;

        // 2. 누름 감지 로직
        bool isPressed = currentPos > pressThreshold;

        if (isPressed && !wasPressed)
        {
            Toggle();
        }

        wasPressed = isPressed;
    }

    void Toggle()
    {
        isOn = !isOn;

        if (isOn)
        {
            // 눌린 상태 유지: 목표 지점을 fixedDepth로 설정
            joint.targetPosition = new Vector3(0, fixedDepth, 0);
            Debug.Log("<color=green>● 버튼 ON</color>");
        }
        else
        {
            // 튀어나온 상태 유지: 목표 지점을 0으로 설정
            joint.targetPosition = Vector3.zero;
            Debug.Log("<color=red>○ 버튼 OFF</color>");
        }
    }
}