using UnityEngine;

public class FishLog : MonoBehaviour
{
    [Header("잘린 회 조각 프리팹")]
    public GameObject sashimiSlicePrefab;

    [Header("설정")]
    public float minCutSpeed = 0.3f;
    public float sliceThickness = 0.1f;
    public int maxSlices = 10;

    private int sliceCount = 0;
    private bool isCutting = false;
    private Vector3 lastContactPoint;

    public void TryCut(Vector3 knifeVelocity, Vector3 contactPoint)
    {
        if (isCutting) return;
        if (sliceCount >= maxSlices) return;
        if (knifeVelocity.magnitude < minCutSpeed) return;

        lastContactPoint = contactPoint;
        isCutting = true;
        PerformCut();
        isCutting = false;
    }

    void PerformCut()
    {
        sliceCount++;

        // 슬라이스 생성
        if (sashimiSlicePrefab != null)
        {
            Quaternion sliceRotation =
                transform.rotation * Quaternion.Euler(0f, 90f, 0f);
            GameObject slice = Instantiate(
                sashimiSlicePrefab,
                lastContactPoint,
                sliceRotation
            );
            Rigidbody sliceRb = slice.GetComponent<Rigidbody>();
            if (sliceRb != null)
            {
                Vector3 popDir = transform.forward + Vector3.up * 0.3f;
                sliceRb.AddForce(popDir * 0.3f, ForceMode.Impulse);
            }
        }

        // 스케일 줄이기
        Rigidbody rb = GetComponent<Rigidbody>();
        Vector3 savedPos = transform.position;
        bool wasKinematic = false;

        if (rb != null)
        {
            wasKinematic = rb.isKinematic;
            rb.isKinematic = true;
        }

        Vector3 newScale = transform.localScale;
        newScale.z = Mathf.Max(newScale.z - sliceThickness, 0.01f);
        transform.localScale = newScale;
        transform.position = savedPos;

        if (rb != null)
        {
            rb.isKinematic = wasKinematic;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 다 잘렸으면 제거
        if (sliceCount >= maxSlices)
        {
            GraspableObject graspable = GetComponent<GraspableObject>();
            if (graspable != null) graspable.enabled = false;
            Destroy(gameObject, 0.1f);
        }
    }
}