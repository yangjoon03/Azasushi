using UnityEngine;

public class FishLog : MonoBehaviour
{
    [Header("잘린 회 조각 프리팹")]
    public GameObject sashimiSlicePrefab;

    [Header("설정")]
    public float minCutSpeed = 0.3f;
    public float sliceThickness = 0.03f;
    public int maxSlices = 10;
    public Vector3 lengthAxis = Vector3.forward;

    private int sliceCount = 0;
    private Vector3 fixedEndWorldPos;
    private bool isCutting = false;
    private Vector3 lastContactPoint;

    void Start()
    {
        fixedEndWorldPos = transform.position
            - transform.TransformDirection(lengthAxis)
            * (transform.localScale.z / 2f);
    }

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

        if (sashimiSlicePrefab != null)
        {
            // Y축으로 90도 회전 (원하는 각도로 숫자 바꿔도 돼)
            Quaternion sliceRotation = transform.rotation * Quaternion.Euler(0f, 90f, 0f);

            GameObject slice = Instantiate(
                sashimiSlicePrefab,
                lastContactPoint,
                sliceRotation
            );

            Rigidbody rb = slice.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 popDir = transform.TransformDirection(lengthAxis) + Vector3.up * 0.5f;
                rb.AddForce(popDir * 1.5f, ForceMode.Impulse);
            }
        }

        ShrinkFishLog();

        if (sliceCount >= maxSlices)
        {
            Destroy(gameObject);
        }
    }

    void ShrinkFishLog()
    {
        Vector3 newScale = transform.localScale;
        newScale.z -= sliceThickness;
        newScale.z = Mathf.Max(newScale.z, 0.01f);
        transform.localScale = newScale;

        Vector3 currentFixedEnd = transform.position
            - transform.TransformDirection(lengthAxis) * (newScale.z / 2f);
        transform.position += fixedEndWorldPos - currentFixedEnd;
    }

    Vector3 GetCutEndPosition()
    {
        return transform.position
               + transform.TransformDirection(lengthAxis) * (transform.localScale.z / 2f);
    }

    void OnDrawGizmos()
    {
        if (!UnityEngine.Application.isPlaying) return;
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(fixedEndWorldPos, 0.02f);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(GetCutEndPosition(), 0.02f);
    }
}