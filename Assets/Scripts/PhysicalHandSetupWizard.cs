using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// 에디터 메뉴에서 Physical Hand Grasp 시스템을 자동 셋업하는 유틸리티.
/// GameObject/XR/Setup Physical Hand Grasp System 메뉴로 실행.
/// </summary>
public class PhysicalHandSetupWizard : MonoBehaviour
{
    [MenuItem("GameObject/XR/Setup Physical Hand Grasp System", false, 10)]
    static void SetupPhysicalHandGraspSystem()
    {
        // 선택된 손 GameObject가 없으면 새로 생성
        GameObject handRoot = Selection.activeGameObject;
        if (handRoot == null)
        {
            handRoot = new GameObject("XR_PhysicalHand_Right");
            Undo.RegisterCreatedObjectUndo(handRoot, "Create Physical Hand");
        }

        // ── PhysicalHandGrasper 추가 ──
        var grasper = handRoot.GetComponent<PhysicalHandGrasper>()
                   ?? Undo.AddComponent<PhysicalHandGrasper>(handRoot);

        // ── FingerDrivenGraspController 추가 ──
        var controller = handRoot.GetComponent<FingerDrivenGraspController>()
                      ?? Undo.AddComponent<FingerDrivenGraspController>(handRoot);

        // Rigidbody (손 자체 속도 전달용, kinematic)
        var rb = handRoot.GetComponent<Rigidbody>()
              ?? Undo.AddComponent<Rigidbody>(handRoot);
        rb.isKinematic = true;
        rb.useGravity  = false;

        // ── 손가락 뼈대 계층 생성 ──
        CreateFingerBones(handRoot, controller);

        Selection.activeGameObject = handRoot;
        Debug.Log("[SetupWizard] Physical Hand Grasp System 셋업 완료!");
    }

    static void CreateFingerBones(GameObject handRoot,
                                   FingerDrivenGraspController controller)
    {
        controller.boneMappings.Clear();

        var fingers = new[]
        {
            (FingerType.Thumb,  new[] { BoneSegment.Proximal, BoneSegment.Distal, BoneSegment.Tip }),
            (FingerType.Index,  new[] { BoneSegment.Proximal, BoneSegment.Middle, BoneSegment.Distal, BoneSegment.Tip }),
            (FingerType.Middle, new[] { BoneSegment.Proximal, BoneSegment.Middle, BoneSegment.Distal, BoneSegment.Tip }),
            (FingerType.Ring,   new[] { BoneSegment.Proximal, BoneSegment.Middle, BoneSegment.Distal, BoneSegment.Tip }),
            (FingerType.Pinky,  new[] { BoneSegment.Proximal, BoneSegment.Middle, BoneSegment.Distal, BoneSegment.Tip }),
        };

        // FingerType → XRHandJointID 기본 매핑
        var jointMap = new System.Collections.Generic.Dictionary<(FingerType, BoneSegment),
                                                                   UnityEngine.XR.Hands.XRHandJointID>
        {
            { (FingerType.Thumb,  BoneSegment.Proximal), UnityEngine.XR.Hands.XRHandJointID.ThumbProximal   },
            { (FingerType.Thumb,  BoneSegment.Distal),   UnityEngine.XR.Hands.XRHandJointID.ThumbDistal     },
            { (FingerType.Thumb,  BoneSegment.Tip),      UnityEngine.XR.Hands.XRHandJointID.ThumbTip        },
            { (FingerType.Index,  BoneSegment.Proximal), UnityEngine.XR.Hands.XRHandJointID.IndexProximal   },
            { (FingerType.Index,  BoneSegment.Middle),   UnityEngine.XR.Hands.XRHandJointID.IndexIntermediate},
            { (FingerType.Index,  BoneSegment.Distal),   UnityEngine.XR.Hands.XRHandJointID.IndexDistal     },
            { (FingerType.Index,  BoneSegment.Tip),      UnityEngine.XR.Hands.XRHandJointID.IndexTip        },
            { (FingerType.Middle, BoneSegment.Proximal), UnityEngine.XR.Hands.XRHandJointID.MiddleProximal  },
            { (FingerType.Middle, BoneSegment.Middle),   UnityEngine.XR.Hands.XRHandJointID.MiddleIntermediate},
            { (FingerType.Middle, BoneSegment.Distal),   UnityEngine.XR.Hands.XRHandJointID.MiddleDistal    },
            { (FingerType.Middle, BoneSegment.Tip),      UnityEngine.XR.Hands.XRHandJointID.MiddleTip       },
            { (FingerType.Ring,   BoneSegment.Proximal), UnityEngine.XR.Hands.XRHandJointID.RingProximal    },
            { (FingerType.Ring,   BoneSegment.Middle),   UnityEngine.XR.Hands.XRHandJointID.RingIntermediate},
            { (FingerType.Ring,   BoneSegment.Distal),   UnityEngine.XR.Hands.XRHandJointID.RingDistal      },
            { (FingerType.Ring,   BoneSegment.Tip),      UnityEngine.XR.Hands.XRHandJointID.RingTip         },
            { (FingerType.Pinky,  BoneSegment.Proximal), UnityEngine.XR.Hands.XRHandJointID.LittleProximal  },
            { (FingerType.Pinky,  BoneSegment.Middle),   UnityEngine.XR.Hands.XRHandJointID.LittleIntermediate},
            { (FingerType.Pinky,  BoneSegment.Distal),   UnityEngine.XR.Hands.XRHandJointID.LittleDistal    },
            { (FingerType.Pinky,  BoneSegment.Tip),      UnityEngine.XR.Hands.XRHandJointID.LittleTip       },
        };

        foreach (var (fingerType, segments) in fingers)
        {
            // 손가락 부모 GameObject
            var fingerGO = new GameObject(fingerType.ToString());
            Undo.RegisterCreatedObjectUndo(fingerGO, "Create Finger");
            fingerGO.transform.SetParent(handRoot.transform, false);

            foreach (var segment in segments)
            {
                var boneGO = new GameObject($"{fingerType}_{segment}");
                Undo.RegisterCreatedObjectUndo(boneGO, "Create Bone");
                boneGO.transform.SetParent(fingerGO.transform, false);

                // CapsuleCollider — 마디 형태
                var col = boneGO.AddComponent<CapsuleCollider>();
                col.radius = 0.007f;
                col.height = 0.025f;
                col.isTrigger = false;

                // PhysicsLayer: Hand 레이어 사용 권장
                // boneGO.layer = LayerMask.NameToLayer("Hand");

                // FingerBoneCollider
                var fbc = boneGO.AddComponent<FingerBoneCollider>();
                fbc.fingerType  = fingerType;
                fbc.boneSegment = segment;
                fbc.handSide    = controller.handedness == UnityEngine.XR.Hands.Handedness.Right
                                ? HandSide.Right : HandSide.Left;

                // Rigidbody (뼈대별 — kinematic, 물리 충돌 감지 전용)
                var boneRb = boneGO.AddComponent<Rigidbody>();
                boneRb.isKinematic = true;
                boneRb.useGravity  = false;

                // BoneMapping 등록
                var key = (fingerType, segment);
                if (jointMap.TryGetValue(key, out var jointID))
                {
                    controller.boneMappings.Add(new FingerDrivenGraspController.BoneMapping
                    {
                        jointID      = jointID,
                        boneCollider = fbc
                    });
                }
            }
        }
    }
}
#endif

/// <summary>
/// ====================================================================
/// 시스템 아키텍처 요약 및 씬 셋업 체크리스트
/// ====================================================================
///
/// [컴포넌트 구성]
///
///  XR_PhysicalHand_Right (GameObject)
///  ├─ PhysicalHandGrasper          ← 파지 판정 & 상태 관리
///  ├─ FingerDrivenGraspController  ← XR Hand 데이터 수신 & 손가락 이동 전달
///  ├─ Rigidbody (kinematic)        ← 속도 전달용
///  └─ Thumb/Index/Middle/Ring/Pinky (자식 GameObject들)
///      └─ [FingerType]_[Segment] (GameObject)
///          ├─ CapsuleCollider      ← 물리 충돌
///          ├─ Rigidbody (kinematic)
///          └─ FingerBoneCollider   ← 접촉 감지 & 보고
///
///  GraspTarget (잡힐 오브젝트)
///  ├─ GraspableObject              ← 파지 상태 수신 & 물리 제어
///  ├─ Rigidbody
///  └─ Collider(s)                  ← 오브젝트 형태
///
/// [파지 판정 흐름]
///
///  1. FingerBoneCollider.OnCollisionEnter/Stay
///     → PhysicalHandGrasper.OnBoneContactEnter/Stay 호출
///
///  2. PhysicalHandGrasper.EvaluateGraspForObject
///     ① 접촉 뼈대 수 ≥ minContactBonesForGrasp (기본 3)
///     ② 힘 강도 ≥ minGraspForce (기본 0.15)
///        - 침투 깊이 기반 + 내부 침투 보너스
///        - 손이 오브젝트 크기보다 안쪽 → 추가 힘 인정
///     ③ 중력 지지 법선 존재 여부 (아래서 받치는 접촉)
///     ④ 파지 방향 유효성: 위에서만 누르는 경우(80%↑) → 파지 차단
///        - 쟁반 위에서 잡기 → 차단
///        - 쟁반 아래서 받치기 → 허용
///        - 양 옆에서 잡기 → 허용
///
///  3. InitiateGrasp → GraspableObject.OnGrasped
///     - Rigidbody Kinematic 전환
///     - 파지 앵커(손 로컬 좌표) 저장
///
///  4. FixedUpdate: FollowHand
///     - MovePosition/MoveRotation으로 손 추종
///     - FingerDrivenGraspController에서 손가락 이동 벡터 추가 전달
///
///  5. 분리 감지
///     a. OnCollisionExit → 즉시 접촉 해제
///     b. CheckAndForceSeparation (0.05초 주기)
///        - Bounds.ClosestPoint 기반 거리 > 0.03m → 강제 해제
///
/// [Layer 설정 권장]
///  - Hand: 손가락 뼈대 콜라이더
///  - Graspable: 잡힐 오브젝트
///  - Physics Matrix: Hand ↔ Graspable 만 충돌 허용
///    (Hand ↔ Hand 충돌 비활성화로 자기 충돌 방지)
///
/// [필요 패키지]
///  - com.unity.xr.hands (OpenXR Hand Tracking)
///  - com.unity.xr.openxr
///
/// ====================================================================
/// </summary>
public class SystemArchitectureDoc { }
