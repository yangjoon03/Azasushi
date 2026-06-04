using UnityEngine;

public class SushiAssembler : MonoBehaviour
{
    [Header("완성형 초밥 프리팹")]
    public GameObject otoroPrefab;
    public GameObject sabaPrefab;
    public GameObject salmonPrefab;
    public GameObject taiPrefab;

    private bool isComplete = false;

    void OnTriggerEnter(Collider other)
    {
        if (isComplete) return;

        // 슬라이스 회가 닿았는지 확인
        string tag = other.gameObject.tag;

        if (tag == "otoro" || tag == "saba" || tag == "salmon" || tag == "tai")
        {
            GameObject completedSushi = GetSushiPrefab(tag);
            if (completedSushi != null)
            {
                isComplete = true;
                CompleteSushi(completedSushi, other.gameObject);
            }
        }
    }

    GameObject GetSushiPrefab(string fishTag)
    {
        switch (fishTag)
        {
            case "otoro": return otoroPrefab;
            case "saba": return sabaPrefab;
            case "salmon": return salmonPrefab;
            case "tai": return taiPrefab;
            default: return null;
        }
    }

    void CompleteSushi(GameObject sushiPrefab, GameObject sliceObject)
    {
        // 완성형 초밥 생성
        GameObject sushi = Instantiate(
            sushiPrefab,
            transform.position,
            transform.rotation
        );

        // 물리 활성화
        Rigidbody rb = sushi.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = true;
            rb.isKinematic = false;
        }

        UnityEngine.Debug.Log("초밥 완성! : " + sliceObject.tag);

        // 슬라이스 회랑 rice3 제거
        Destroy(sliceObject);
        Destroy(gameObject);
    }
}