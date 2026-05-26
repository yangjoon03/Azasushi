using UnityEngine;

public class RiceBall : MonoBehaviour
{
    [Header("ÃÊ¹ä ¹ä µ¢¾î¸® ÇÁ¸®ÆÕ")]
    public GameObject sushiRicePrefab;

    [Header("ÃÊ¹ä µ¢¾î¸® Å©±â")]
    public Vector3 sushiRiceScale = new Vector3(0.05f, 0.05f, 0.05f);

    [Header("¼³Á¤")]
    public int requiredSqueezes = 2;

    private int squeezeCount = 0;
    private bool isGrasped = false;
    private bool isComplete = false;
    private RiceBowl riceBowl;
    private int contactCount = 0;

    // »ý¼ºµÈ ÃÊ¹ä µ¢¾î¸®
    private GameObject spawnedSushiRice;

    public void SetRiceBowl(RiceBowl bowl)
    {
        riceBowl = bowl;
    }

    void OnTriggerEnter(Collider other)
    {
        if (isComplete) return;
        if (other.GetComponent<FingerBoneCollider>() != null)
        {
            contactCount++;
            if (contactCount >= 2 && !isGrasped)
            {
                isGrasped = true;
                UnityEngine.Debug.Log("Áã±â °¨Áö!");
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (isComplete) return;
        if (other.GetComponent<FingerBoneCollider>() != null)
        {
            contactCount = Mathf.Max(0, contactCount - 1);
            if (contactCount == 0 && isGrasped)
            {
                isGrasped = false;
                squeezeCount++;
                UnityEngine.Debug.Log("Áã¾ú´Ù Æñ´Ù È½¼ö: " + squeezeCount);

                if (squeezeCount >= requiredSqueezes)
                {
                    TransformToSushiRice();
                }
            }
        }
    }

    void TransformToSushiRice()
    {
        isComplete = true;

        if (sushiRicePrefab != null)
        {
            // ºÎ¸ð(¼Õ)¿¡ ºÎÂøµÈ Ã¤·Î »ý¼º
            spawnedSushiRice = Instantiate(
                sushiRicePrefab,
                transform.position,
                transform.rotation
            );

            spawnedSushiRice.transform.localScale = sushiRiceScale;

            // ¼Õ¿¡ ºÎÂø
            spawnedSushiRice.transform.SetParent(transform.parent);
            spawnedSushiRice.transform.localPosition = Vector3.zero;

            // ¹°¸® ºñÈ°¼ºÈ­ (¼Õ¿¡ ºÙ¾îÀÖ´Â µ¿¾È)
            Rigidbody rb = spawnedSushiRice.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            // ¶¼¾î³»´Â ±â´É Ãß°¡
            SushiRiceDetach detach = spawnedSushiRice.AddComponent<SushiRiceDetach>();
            detach.Init(spawnedSushiRice, riceBowl);

            UnityEngine.Debug.Log("ÃÊ¹ä µ¢¾î¸® ¼Õ¿¡ ºÎÂø ¿Ï·á!");
        }

        if (riceBowl != null) riceBowl.OnRiceBallComplete();
        Destroy(gameObject);
    }
}