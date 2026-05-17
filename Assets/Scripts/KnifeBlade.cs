using UnityEngine;

public class KnifeBlade : MonoBehaviour
{
    private Vector3 prevPosition;
    private Vector3 velocity;

    void Start()
    {
        prevPosition = transform.position;
    }

    void Update()
    {
        velocity = (transform.position - prevPosition) / Time.deltaTime;
        prevPosition = transform.position;
    }

    void OnTriggerEnter(Collider other)
    {
        FishLog fishLog = other.GetComponent<FishLog>();
        if (fishLog != null)
        {
            fishLog.TryCut(velocity, transform.position);
        }
    }
}