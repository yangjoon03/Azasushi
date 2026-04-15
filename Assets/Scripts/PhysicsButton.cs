using UnityEngine;

public class PhysicsButton : MonoBehaviour
{
    [Header("Reference (only for logic, NOT physics)")]
    public Transform startPoint;

    [Header("Press Settings")]
    public float pressThreshold = 0.15f;

    private Rigidbody rb;
    private ConfigurableJoint joint;

    private bool isOn = false;
    private bool wasPressed = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        joint = GetComponent<ConfigurableJoint>();

        rb.useGravity = false;
        rb.isKinematic = false;

        SetupJoint();
    }

    void Start()
    {
        // 🔥 핵심: 시작 위치 보정 (처음 밀림 방지)
        if (startPoint != null)
        {
            transform.position = startPoint.position;
        }
    }

    void SetupJoint()
    {
        if (joint == null) return;

        joint.autoConfigureConnectedAnchor = true;

        // 🔥 로컬 기준 축 제한
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Limited;
        joint.zMotion = ConfigurableJointMotion.Locked;

        joint.angularXMotion = ConfigurableJointMotion.Locked;
        joint.angularYMotion = ConfigurableJointMotion.Locked;
        joint.angularZMotion = ConfigurableJointMotion.Locked;

        // 🔥 눌림 거리
        SoftJointLimit limit = new SoftJointLimit();
        limit.limit = 0.15f;
        joint.linearLimit = limit;

        // 🔥 스프링 (처짐 방지 핵심)
        JointDrive drive = new JointDrive();
        drive.positionSpring = 400f;
        drive.positionDamper = 30f;
        drive.maximumForce = Mathf.Infinity;

        joint.yDrive = drive;
    }

    void FixedUpdate()
    {
        CheckPress();
    }

    void CheckPress()
    {
        if (startPoint == null) return;

        // 🔥 startPoint 기준 로컬 위치
        Vector3 localPos =
            startPoint.InverseTransformPoint(transform.position);

        // -Y 눌림
        float pressAmount = -localPos.y;

        bool isPressed = pressAmount > pressThreshold;

        if (isPressed && !wasPressed)
        {
            Toggle();
        }

        wasPressed = isPressed;
    }

    void Toggle()
    {
        isOn = !isOn;

        Debug.Log(isOn ? "BUTTON ON" : "BUTTON OFF");
    }
}
