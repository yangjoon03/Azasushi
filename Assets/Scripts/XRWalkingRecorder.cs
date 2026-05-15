//using UnityEngine;
//using UnityEngine.InputSystem;
//using System.Collections;
//using System.Collections.Generic;

//public class XRHeadMotionWalker : MonoBehaviour
//{
//    enum State { Idle, Waiting, Recording, Playing }

//    [Header("참조")]
//    public Transform xrCamera;
//    public Transform rigRoot;

//    [Header("설정")]
//    public float moveSpeed = 1.5f;
//    public float recordDuration = 10f;
//    public float motionThreshold = 0.002f; // 움직임 감지 문턱값 (너무 낮으면 가만히 있어도 감지됨)

//    [SerializeField] private State currentState = State.Idle;
//    private List<float> _recordedSpeeds = new List<float>(); // 위치값이 아니라 '움직임 강도'를 기록
//    private Vector3 _lastLocalPos;
//    private int _currentFrame = 0;

//    void Start()
//    {
//        if (xrCamera == null) xrCamera = Camera.main.transform;
//        if (rigRoot == null) rigRoot = transform;
//        _lastLocalPos = xrCamera.localPosition;
//    }

//    void Update()
//    {
//        // M 키로 제자리걸음 패턴 기록
//        if (Keyboard.current.mKey.wasPressedThisFrame && currentState == State.Idle)
//        {
//            StartCoroutine(RecordHeadMotion());
//        }

//        if (currentState == State.Playing)
//        {
//            CheckMotionAndMove();
//        }
//    }

//    IEnumerator RecordHeadMotion()
//    {
//        currentState = State.Waiting;
//        Debug.Log("3초 뒤 녹화... 제자리 걸음을 해주세요!");
//        yield return new WaitForSeconds(3f);

//        currentState = State.Recording;
//        _recordedSpeeds.Clear();
//        float timer = 0f;
//        _lastLocalPos = xrCamera.localPosition;

//        while (timer < recordDuration)
//        {
//            // 프레임당 머리가 움직인 거리(속도)를 기록
//            float dist = Vector3.Distance(xrCamera.localPosition, _lastLocalPos);
//            _recordedSpeeds.Add(dist);

//            _lastLocalPos = xrCamera.localPosition;
//            timer += Time.deltaTime;
//            yield return null;
//        }

//        Debug.Log("패턴 등록 완료! 이제 움직이면 나갑니다.");
//        currentState = State.Playing;
//    }

//    void CheckMotionAndMove()
//    {
//        // 1. 현재 내 머리의 실제 움직임 계산
//        float currentInputMotion = Vector3.Distance(xrCamera.localPosition, _lastLocalPos);
//        _lastLocalPos = xrCamera.localPosition;

//        // 2. 현재 내 움직임이 일정 수치(문턱값) 이상일 때만 전진 로직 가동
//        if (currentInputMotion > motionThreshold)
//        {
//            // 3. 녹화된 패턴 데이터 순환 재생
//            float patternMotion = _recordedSpeeds[_currentFrame];
//            _currentFrame = (_currentFrame + 1) % _recordedSpeeds.Count;

//            // 4. 전진! 
//            // (내 실제 움직임 강도와 기록된 패턴 강도를 조합해서 자연스럽게 전진)
//            Vector3 forwardDir = xrCamera.forward;
//            forwardDir.y = 0;

//            // 실제 움직임과 패턴의 평균값을 동력으로 사용
//            float boost = (currentInputMotion + patternMotion) * 0.5f;
//            rigRoot.position += forwardDir.normalized * boost * moveSpeed * 100f;
//        }
//        else
//        {
//            // 가만히 있으면 프레임 인덱스도 멈추거나 서서히 초기화
//            // _currentFrame = 0; // 필요하다면 주석 해제 (멈추면 패턴 처음부터)
//        }
//    }
//}

using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
public class XRHeadMotionWalker : MonoBehaviour
{
    enum State { Idle, Waiting, Recording, Playing }

    [Header("참조")]
    public Transform xrCamera;
    public Transform rigRoot;

    [Header("설정")]
    public float moveSpeed = 1.5f;
    public float recordDuration = 10f;
    public float motionThreshold = 0.002f;

    [SerializeField] private State currentState = State.Idle;
    private List<float> _recordedSpeeds = new List<float>();
    private Vector3 _lastLocalPos;
    private int _currentFrame = 0;

    // 충돌 처리용
    private CharacterController _cc;
    private Vector3 _velocity = Vector3.zero; // 중력 포함 누적 속도

    void Start()
    {
        if (xrCamera == null) xrCamera = Camera.main.transform;
        if (rigRoot == null) rigRoot = transform;

        // ★ CharacterController는 rigRoot(이 컴포넌트가 붙은 오브젝트)에 있어야 함
        _cc = GetComponent<CharacterController>();
        _lastLocalPos = xrCamera.localPosition;
    }

    void Update()
    {
        if (Keyboard.current.mKey.wasPressedThisFrame && currentState == State.Idle)
        {
            StartCoroutine(RecordHeadMotion());
        }

        if (currentState == State.Playing)
        {
            CheckMotionAndMove();
        }

        // 중력 처리 (XR에서도 바닥 뚫림 방지에 필수)
        ApplyGravity();
    }

    void ApplyGravity()
    {
        if (_cc.isGrounded)
        {
            _velocity.y = -0.5f; // 바닥에 붙어 있게 살짝 눌러줌
        }
        else
        {
            _velocity.y += Physics.gravity.y * Time.deltaTime;
        }

        // 중력만 적용 (수평 이동은 CheckMotionAndMove에서 따로 처리)
        _cc.Move(new Vector3(0, _velocity.y, 0) * Time.deltaTime);
    }

    IEnumerator RecordHeadMotion()
    {
        currentState = State.Waiting;
        Debug.Log("3초 뒤 녹화... 제자리 걸음을 해주세요!");
        yield return new WaitForSeconds(3f);

        currentState = State.Recording;
        _recordedSpeeds.Clear();
        float timer = 0f;
        _lastLocalPos = xrCamera.localPosition;

        while (timer < recordDuration)
        {
            float dist = Vector3.Distance(xrCamera.localPosition, _lastLocalPos);
            _recordedSpeeds.Add(dist);
            _lastLocalPos = xrCamera.localPosition;
            timer += Time.deltaTime;
            yield return null;
        }

        Debug.Log("패턴 등록 완료! 이제 움직이면 나갑니다.");
        currentState = State.Playing;
    }

    void CheckMotionAndMove()
    {
        float currentInputMotion = Vector3.Distance(xrCamera.localPosition, _lastLocalPos);
        _lastLocalPos = xrCamera.localPosition;

        if (currentInputMotion > motionThreshold && _recordedSpeeds.Count > 0)
        {
            float patternMotion = _recordedSpeeds[_currentFrame];
            _currentFrame = (_currentFrame + 1) % _recordedSpeeds.Count;

            Vector3 forwardDir = xrCamera.forward;
            forwardDir.y = 0;

            float boost = (currentInputMotion + patternMotion) * 0.5f;

            // ★ Time.deltaTime 제거 — 기존처럼 * 100f 그대로 유지
            Vector3 moveVec = forwardDir.normalized * boost * moveSpeed * 100f;
            _cc.Move(moveVec);
        }
    }
}