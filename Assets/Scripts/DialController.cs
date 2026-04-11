using UnityEngine;
using System.Collections.Generic;

public class DialController : MonoBehaviour
{
    [Header("연결할 오브젝트들을 여기에 드래그하세요")]
    public List<GameObject> targets;

    private List<I_DialInteractable> interactables = new List<I_DialInteractable>();

    void Start()
    {
        // 시작할 때 각 오브젝트에서 인터페이스를 찾아 리스트에 보관 (캐싱)
        foreach (var obj in targets)
        {
            var found = obj.GetComponents<I_DialInteractable>();
            if (found != null) interactables.AddRange(found);
        }
    }

    void Update()
    {
        // 1. 현재 로컬 Y축 각도를 0~1 비율로 계산 (0~360도 기준)
        float currentAngle = transform.localEulerAngles.y;
        float progress = currentAngle / 360f;

        // 2. 리스트에 있는 모든 수신자에게 값 전달
        foreach (var item in interactables)
        {
            item.OnProgressChanged(progress);
        }
    }
}