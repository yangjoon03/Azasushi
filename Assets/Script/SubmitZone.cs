using UnityEngine;

public class SubmitZone : MonoBehaviour
{
    public CustomerOrderManager orderManager;

    private void OnTriggerEnter(Collider other)
    {
        TrySubmit(other);
    }

    void TrySubmit(Collider other)
    {
        if (orderManager == null)
        {
            Debug.LogWarning("Order Manager가 연결되지 않았습니다.");
            return;
        }

        GameObject sushiObject = FindWholeSushiObject(other.transform);

        if (sushiObject == null)
        {
            Debug.Log("완성 초밥 아님: " + other.name);
            return;
        }

        string submittedTag = sushiObject.tag;

        bool correct = orderManager.CheckSubmittedTag(submittedTag);

        Debug.Log("제출 초밥: " + sushiObject.name + " / Tag: " + submittedTag + " / Correct: " + correct);

        if (correct)
        {
            Debug.Log("정답 초밥 전체 삭제: " + sushiObject.name);

            sushiObject.SetActive(false);
            Destroy(sushiObject);
        }
    }

    GameObject FindWholeSushiObject(Transform start)
    {
        Transform current = start;
        GameObject foundSushi = null;

        while (current != null)
        {
            if (IsSushiTag(current.tag))
            {
                foundSushi = current.gameObject;
            }

            current = current.parent;
        }

        return foundSushi;
    }

    bool IsSushiTag(string tag)
    {
        return tag == "saba" ||
               tag == "tai" ||
               tag == "salmon" ||
               tag == "otoro";
    }
}