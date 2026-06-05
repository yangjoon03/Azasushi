using UnityEngine;

public class RiceBowl : MonoBehaviour
{
    [Header("»ý¼ºÇÒ ÀÛÀº ¹ä µ¢¾î¸® ÇÁ¸®ÆÕ")]
    public GameObject riceBallPrefab;

    [Header("¼Õ ºÎÂø À§Ä¡")]
    public Transform leftHandAttachPoint;
    public Transform rightHandAttachPoint;

    [Header("¹ä µ¢¾î¸® Å©±â")]
    public Vector3 riceBallScale = new Vector3(0.05f, 0.05f, 0.05f);

    private GameObject currentRiceBall;
    private bool isSushiAttached = false;

    void OnTriggerEnter(Collider other)
    {
        if (currentRiceBall != null) return;
        if (isSushiAttached) return;

        if (other.GetComponent<FingerBoneCollider>() != null)
        {
            // ºÎ¸ð °èÃþ¿¡¼­ Right/Left Hand Ã£±â
            bool isRightHand = false;
            Transform current = other.transform;
            while (current != null)
            {
                if (current.name == "Right Hand")
                {
                    isRightHand = true;
                    break;
                }
                if (current.name == "Left Hand")
                {
                    isRightHand = false;
                    break;
                }
                current = current.parent;
            }

            UnityEngine.Debug.Log("¿À¸¥¼Õ ¿©ºÎ: " + isRightHand);

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

        UnityEngine.Debug.Log("rice2 ºÎÂø ¿Ï·á!");
    }

    public void OnRiceBallComplete()
    {
        currentRiceBall = null;
        isSushiAttached = true;
    }

    public void OnSushiDetached()
    {
        isSushiAttached = false;
    }
}