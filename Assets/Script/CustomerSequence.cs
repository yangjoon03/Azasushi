using System.Collections;
using UnityEngine;

public class CustomerSequence : MonoBehaviour
{
    public Animator animator;
    public float moveSpeed = 1.5f;
    public float walkTime = 3f;
    public float rotateSpeed = 180f;

    private Quaternion targetRotation;

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        StartCoroutine(CustomerRoutine());
    }

    IEnumerator CustomerRoutine()
    {
        // 걷기 시작
        animator.SetBool("isWalking", true);

        float timer = 0f;

        // 3초 동안 앞으로 이동
        while (timer < walkTime)
        {
            transform.position += transform.forward * moveSpeed * Time.deltaTime;
            timer += Time.deltaTime;
            yield return null;
        }

        // 걷기 종료
        animator.SetBool("isWalking", false);

        // 현재 방향 기준 왼쪽으로 90도 회전
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
    }
}