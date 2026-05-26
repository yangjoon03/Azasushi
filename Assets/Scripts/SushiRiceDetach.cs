using UnityEngine;

public class SushiRiceDetach : MonoBehaviour
{
    private GameObject sushiRice;
    private int contactCount = 0;
    private bool isGrasped = false;
    private RiceBowl riceBowl;

    public void Init(GameObject rice, RiceBowl bowl)
    {
        sushiRice = rice;
        riceBowl = bowl;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<FingerBoneCollider>() != null)
        {
            contactCount++;
            if (contactCount >= 2 && !isGrasped)
            {
                isGrasped = true;
                UnityEngine.Debug.Log("√ π‰ ¡„±‚ ∞®¡ˆ!");
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<FingerBoneCollider>() != null)
        {
            contactCount = Mathf.Max(0, contactCount - 1);
            if (contactCount == 0 && isGrasped)
            {
                DetachFromHand();
            }
        }
    }

    void DetachFromHand()
    {
        sushiRice.transform.SetParent(null);

        Rigidbody rb = sushiRice.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = true;
            rb.isKinematic = false;
        }

        if (riceBowl != null) riceBowl.OnSushiDetached();

        UnityEngine.Debug.Log("√ π‰ µ¢æÓ∏Æ º’ø°º≠ ∫–∏Æ!");
        Destroy(this);
    }
}