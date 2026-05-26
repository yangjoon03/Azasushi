using UnityEngine;

public class RiceBowl : MonoBehaviour
{
    [Header("생성할 작은 밥 덩어리 프리팹")]
    public GameObject riceBallPrefab;

    [Header("손 부착 위치")]
    public Transform rightHandAttachPoint;
    public Transform leftHandAttachPoint;

    [Header("밥 덩어리 크기")]
    public Vector3 riceBallScale = new Vector3(0.05f, 0.05f, 0.05f);

    private GameObject currentRiceBall;
    private bool isSushiAttached = false; // 초밥 덩어리가 손에 있는지

    void OnTriggerEnter(Collider other)
    {
        // 밥 덩어리 또는 초밥 덩어리가 손에 있으면 생성 금지
        if (currentRiceBall != null) return;
        if (isSushiAttached) return;

        if (other.GetComponent<FingerBoneCollider>() != null)
        {
            bool isRightHand = other.transform.root.name == "Right Hand";
            Transform attachPoint = isRightHand
                ? rightHandAttachPoint
                : leftHandAttachPoint;

            if (attachPoint != null)
            {
                SpawnRiceBall(attachPoint);
            }
        }
    }

    void SpawnRiceBall(Transform attachPoint)
    {
        if (riceBallPrefab == null) return;

        currentRiceBall = Instantiate(
            riceBallPrefab,
            attachPoint.position,
            Quaternion.identity
        );

        currentRiceBall.transform.SetParent(attachPoint);
        currentRiceBall.transform.localPosition = Vector3.zero;
        currentRiceBall.transform.localRotation = Quaternion.identity;
        currentRiceBall.transform.localScale = riceBallScale;

        Rigidbody rb = currentRiceBall.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = false;
        }

        RiceBall riceBall = currentRiceBall.GetComponent<RiceBall>();
        if (riceBall != null)
        {
            riceBall.SetRiceBowl(this);
        }

        UnityEngine.Debug.Log("rice2 부착 완료!");
    }

    // rice2 → rice3 변환 시 호출
    public void OnRiceBallComplete()
    {
        currentRiceBall = null;
        isSushiAttached = true; // 초밥 덩어리 손에 붙음
    }

    // rice3 손에서 분리 시 호출
    public void OnSushiDetached()
    {
        isSushiAttached = false; // 다시 생성 가능
    }
}