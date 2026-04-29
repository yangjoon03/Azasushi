using UnityEngine;

public class SubmitZone : MonoBehaviour
{
    public CustomerOrderManager orderManager;

    private void OnTriggerEnter(Collider other)
    {
        string itemName = DetectItemType(other.gameObject);

        if (itemName == "")
        {
            return;
        }

        orderManager.CheckSubmittedItem(itemName);
    }

    string DetectItemType(GameObject obj)
    {
        if (obj.GetComponent<SphereCollider>() != null)
        {
            return "Sphere";
        }

        if (obj.GetComponent<CapsuleCollider>() != null)
        {
            return "Capsule";
        }

        if (obj.GetComponent<BoxCollider>() != null)
        {
            return "Cube";
        }

        return "";
    }
}