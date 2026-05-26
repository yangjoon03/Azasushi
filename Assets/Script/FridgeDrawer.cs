using System.Collections;
using UnityEngine;

public class FridgeDrawer : MonoBehaviour
{
    public Vector3 openOffset = new Vector3(0, 0, -0.6f);
    public float moveSpeed = 3f;

    private Vector3 closedPosition;
    private bool isOpen = false;
    private bool isMoving = false;

    void Start()
    {
        closedPosition = transform.localPosition;
    }

    public void ToggleDrawer()
    {
        if (isMoving) return;

        isOpen = !isOpen;
        StopAllCoroutines();
        StartCoroutine(MoveDrawer());
    }

    IEnumerator MoveDrawer()
    {
        isMoving = true;

        Vector3 startPos = transform.localPosition;
        Vector3 targetPos = isOpen ? closedPosition + openOffset : closedPosition;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed;
            transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.localPosition = targetPos;
        isMoving = false;
    }
}