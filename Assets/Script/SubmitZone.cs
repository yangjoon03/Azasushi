using UnityEngine;

public class SubmitZone : MonoBehaviour
{
    public CustomerOrderManager orderManager;

    private void OnTriggerEnter(Collider other)
    {
        string submittedItem = other.gameObject.name.ToLower();

        // (Clone) 제거
        submittedItem = submittedItem.Replace("(clone)", "").Trim();

        // 정확히 일치하는 경우만 인정
        if (IsValidSushi(submittedItem))
        {
            orderManager.CheckSubmittedItem(submittedItem);
        }
        else
        {
            Debug.Log("Not a completed sushi: " + submittedItem);
        }
    }

    bool IsValidSushi(string name)
    {
        return name == "saba" ||
               name == "unagi" ||
               name == "tai" ||
               name == "salmon" ||
               name == "shrimp" ||
               name == "otoro";
    }
}