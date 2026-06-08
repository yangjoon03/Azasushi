using System.Collections;
using UnityEngine;

public class CustomerSequence : MonoBehaviour
{
    public CustomerOrderManager orderManager;

    public float moveSpeed = 1.5f;
    public float walkTime = 3f;
    public float rotateSpeed = 180f;
    public float orderTimeLimit = 30f;
    public float respawnDelay = 1.5f;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private Renderer[] renderers;
    private Collider[] colliders;

    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();

        StartCoroutine(CustomerLoop());
    }

    IEnumerator CustomerLoop()
    {
        while (true)
        {
            if (orderManager.IsCleared())
                yield break;

            ShowCustomer(true);

            transform.position = startPosition;
            transform.rotation = startRotation;

            orderManager.ClearOrder();

            float timer = 0f;

            while (timer < walkTime)
            {
                if (orderManager.IsCleared())
                    yield break;

                transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
                timer += Time.deltaTime;
                yield return null;
            }





            // 정확히 왼쪽 90도 회전

            Quaternion rotateStartRotation = transform.rotation;
            Quaternion rotateTargetRotation = rotateStartRotation * Quaternion.Euler(0f, -90f, 0f);

            float rotateTime = 0f;
            float rotateDuration = 0.5f;

            while (rotateTime < rotateDuration)
            {
                if (orderManager.IsCleared())
                    yield break;

                rotateTime += Time.deltaTime;

                transform.rotation = Quaternion.Slerp(
                    rotateStartRotation,
                    rotateTargetRotation,
                    rotateTime / rotateDuration
                );

                yield return null;
            }

            transform.rotation = rotateTargetRotation;

            if (orderManager.IsCleared())
                yield break;

            orderManager.NewOrder();

            float orderTimer = 0f;

            while (orderTimer < orderTimeLimit)
            {
                if (orderManager.IsCleared())
                    yield break;

                if (!orderManager.hasActiveOrder)
                    break;

                orderTimer += Time.deltaTime;
                orderManager.UpdateTimer(orderTimer / orderTimeLimit);

                yield return null;
            }

            if (orderManager.IsCleared())
                yield break;

            orderManager.ClearOrder();

            ShowCustomer(false);

            yield return new WaitForSeconds(respawnDelay);
        }
    }

    void ShowCustomer(bool show)
    {
        foreach (Renderer r in renderers)
            r.enabled = show;

        foreach (Collider c in colliders)
            c.enabled = show;
    }
}