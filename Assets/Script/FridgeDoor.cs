using System.Collections;
using UnityEngine;

public class FridgeDoor : MonoBehaviour
{
    public Vector3 closedRotation;
    public Vector3 openRotation;

    public float openSpeed = 3f;

    private bool isOpen = false;
    private bool isMoving = false;

    void Start()
    {
        closedRotation = transform.localEulerAngles;
    }

    public void ToggleDoor()
    {
        if (isMoving) return;

        isOpen = !isOpen;
        StopAllCoroutines();
        StartCoroutine(RotateDoor());
    }

    IEnumerator RotateDoor()
    {
        isMoving = true;

        Quaternion startRot = transform.localRotation;
        Quaternion targetRot = Quaternion.Euler(isOpen ? openRotation : closedRotation);

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * openSpeed;
            transform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        transform.localRotation = targetRot;
        isMoving = false;
    }
}