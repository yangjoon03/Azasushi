using System.Collections;
using UnityEngine;
using TMPro;

public class CustomerSequence : MonoBehaviour
{
    public Animator animator;
    public float moveSpeed = 1.5f;
    public float walkTime = 3f;
    public float rotateSpeed = 180f;

    public float orderTimeLimit = 30f;
    public float respawnDelay = 1.5f;

    public CustomerOrderManager orderManager;
    public TextMeshPro customerNameText;

    private static int customerNumber = 0;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private Quaternion targetRotation;

    private Renderer[] renderers;
    private Collider[] colliders;

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (orderManager == null)
            orderManager = FindObjectOfType<CustomerOrderManager>();

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
            ShowCustomer(true);

            transform.position = startPosition;
            transform.rotation = startRotation;

            customerNumber++;

            if (customerNameText != null)
                customerNameText.text = "Customer" + customerNumber;

            orderManager.ClearOrder();

            animator.SetBool("isWalking", true);

            float timer = 0f;

            while (timer < walkTime)
            {
                transform.position += transform.forward * moveSpeed * Time.deltaTime;
                timer += Time.deltaTime;
                yield return null;
            }

            animator.SetBool("isWalking", false);

            targetRotation = Quaternion.Euler(0f, transform.eulerAngles.y - 90f, 0f);

            while (Quaternion.Angle(transform.rotation, targetRotation) > 0.5f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotateSpeed * Time.deltaTime
                );

                yield return null;
            }

            transform.rotation = targetRotation;

            orderManager.NewOrder();

            float orderTimer = 0f;

            while (orderTimer < orderTimeLimit)
            {
                if (!orderManager.hasActiveOrder)
                    break;

                orderTimer += Time.deltaTime;

                float timerRatio = orderTimer / orderTimeLimit;
                orderManager.UpdateTimer(timerRatio);

                yield return null;
            }

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