# 🚙 Unity + Cesium + ATV 주행 프로젝트 매뉴얼

## 0) 새 프로젝트 & 필수 설정

-   **프로젝트 생성**
    -   템플릿: `3D (URP)` 또는 `High Definition 3D (HDRP)`
    -   이름/경로 지정 후 생성
-   **Input System 사용**
    -   `Edit → Project Settings → Player → Active Input Handling = Input System Package`
    -   (구식 `UnityEngine.Input` API는 사용하지 않음)
-   **(선택)** Cinemachine 설치 (카메라 팔로우를 쉽게 하려면)

------------------------------------------------------------------------

## 1) Cesium 1.15.4: 패널 열기 → 로그인 → 지형/영상 추가

-   **Cesium 패널 열기**
    -   `Window → Cesium` (또는 상단 `Cesium → Open Cesium Panel`)
-   **Cesium ion 로그인**
    -   패널 상단의 `Sign In` 버튼 → 계정 연동
-   **지형 추가**
    -   `Add to Scene → Cesium World Terrain` 선택 → `Add`
    -   Hierarchy에 `CesiumGeoreference` + `Cesium3DTileset` 생성
-   **영상(Imagery) 오버레이**
    -   `Add to Scene → Bing Maps Aerial` → `Add`
    -   `CesiumBingMapsRasterOverlay`가 타일셋에 자동 바인딩됨
-   **지형 물리 충돌**
    -   Hierarchy에서 `Cesium World Terrain` 선택
    -   Inspector → `Generate Physics Meshes = ON`
-   **원점/초기 위치 설정**
    -   `CesiumGeoreference` 선택
    -   Origin Lat/Lon/Height 입력 (예: `37.5665 / 126.9780 / 120`)
    -   버튼(`Set Origin` / `Recenter` / `Fly to Origin`)으로 뷰 확인

------------------------------------------------------------------------

## 2) (HDRP만) 기본 품질 권장치

-   **Global Volume**
    -   씬 루트에 생성 후 `Is Global = ON`
    -   Override:
        -   `Exposure`: Fixed → 최종 Automatic\
        -   `Tonemapping`: ACES\
        -   (선택) Fog(Volumetric), Bloom, Color Adjustments
-   **Main Camera**
    -   HDRP Additional Camera Data:
        -   Anti-Aliasing = **TAA**\
        -   Near = 0.3\~0.5 / Far = 10000\~20000
-   **HDRP Asset (품질)**
    -   Directional Shadow Distance = 300\~600 m
    -   Cascades = 3\~4
-   **URP**
    -   기본 세팅으로 충분, 카메라 Far = 10000\~20000만 확인

------------------------------------------------------------------------

## 3) Asset Store ATV 임포트 → 프리팹 준비

-   **폴더 구조**

        RTS_Modern_Combat_Vehicle_Pack_Free
         └─ ATV_N1
            ├─ 0_Mesh
            ├─ 0_Prefabs
            ├─ Materials
            │   ├─ LOD0
            │   └─ LOD1
            └─ Textures
                ├─ LOD0
                └─ LOD1

-   **머티리얼 파이프라인 전환**

    -   (HDRP) `Window → Rendering → Render Pipeline Converter`
    -   `Built-in to HDRP` → Initialize → Convert Project Materials
    -   차체: `HDRP/Lit` (Metallic 0.6\~0.9 / Smoothness 0.7\~0.85)
    -   유리: `Surface Type = Transparent` (+ Refraction)

-   **프리팹 Variant 생성**

    -   ATV 프리팹을 씬에 드래그
    -   Project 창에서 → `우클릭 → Create → Prefab Variant`
    -   Variant에만 물리/스크립트 추가

-   **차체 콜라이더**

    -   Rigidbody + MeshCollider(Non-convex) ❌
    -   자식에 BoxCollider 2\~4개 배치 (바퀴 영역 제외)

------------------------------------------------------------------------

## 4) 바퀴 리깅: WheelCollider 4개

-   **구조 예시**

        ATV (Variant 루트)
         └─ Wheels
            ├─ FL_WheelCol (WheelCollider)
            ├─ FR_WheelCol
            ├─ RL_WheelCol
            └─ RR_WheelCol

-   **WheelCollider 기본값**

    -   Radius: ≈ 0.33 (모델 크기에 따라 조정)
    -   Suspension Distance: 0.2
    -   Spring: 22000 / Damper: 3500
    -   Forward/Sideways Friction: Stiffness 1.0 \~ 1.5

-   **(선택)** 휠 메쉬 연동

    -   컨트롤러 스크립트에 메쉬 Transform 연결 → 회전/스트로크 반영

------------------------------------------------------------------------

## 5) 입력 액션 (New Input System)

-   Project → `Create → Input Actions → VehicleControls.inputactions`
-   액션 구성:
    -   Move (Value / Vector2): WASD + Gamepad LeftStick
    -   Brake (Button): Space
    -   Handbrake (Button): LeftShift
    -   Reset (Button): R
-   ATV 루트 → `PlayerInput` 추가
    -   `VehicleControls.inputactions` 지정
    -   Behavior = `Invoke Unity Events`

------------------------------------------------------------------------

## 6) 스크립트 3종

### 6-1) ATVController.cs
ATV 루트에 부착 → WheelCollider/메쉬 연결

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class ATVController : MonoBehaviour
{
  [Header("Wheel Colliders")]
  public WheelCollider FL, FR, RL, RR;

  [Header("Wheel Meshes (optional)")]
  public Transform FLMesh, FRMesh, RLMesh, RRMesh;

  [Header("Vehicle")]
  public float maxSteerAngle = 22f;
  public float motorTorque = 900f;
  public float brakeTorque = 2500f;
  public float handbrakeTorque = 5000f;
  public float maxSpeedKPH = 90f;
  public float downForce = 50f;
  public bool rearWheelDrive = true;

  [Header("Ground Spawn")]
  public bool snapToGroundOnStart = true;
  public float groundRayStartHeight = 500f;
  public LayerMask groundMask = ~0;

  Rigidbody rb;
  Vector2 move;
  bool brake, handbrake, reset;

  void Awake()
  {
      rb = GetComponent<Rigidbody>();
      rb.interpolation = RigidbodyInterpolation.Interpolate;
      rb.centerOfMass = new Vector3(0, -0.3f, 0);
  }

  void Start()
  {
      if (snapToGroundOnStart) SnapToGround();
  }

  public void OnMove(InputValue v) => move = v.Get<Vector2>();
  public void OnBrake(InputValue v) => brake = v.isPressed;
  public void OnHandbrake(InputValue v) => handbrake = v.isPressed;
  public void OnReset(InputValue v) { if (v.isPressed) reset = true; }

  void FixedUpdate()
  {
      float speedKPH = rb.velocity.magnitude * 3.6f;
      float throttle = Mathf.Clamp(move.y, -1f, 1f);
      float steer = Mathf.Clamp(move.x, -1f, 1f);

      // Steer (front)
      FL.steerAngle = maxSteerAngle * steer;
      FR.steerAngle = maxSteerAngle * steer;

      // Torque
      float torque = 0f;
      if (speedKPH < maxSpeedKPH) torque = motorTorque * Mathf.Clamp01(throttle);

      if (throttle < 0f && speedKPH < maxSpeedKPH * 0.4f)
          torque = motorTorque * throttle; // reverse

      FL.motorTorque = FR.motorTorque = RL.motorTorque = RR.motorTorque = 0f;

      if (rearWheelDrive)
      {
          RL.motorTorque = torque;
          RR.motorTorque = torque;
      }
      else
      {
          float half = torque * 0.5f;
          FL.motorTorque = FR.motorTorque = RL.motorTorque = RR.motorTorque = half;
      }

      // Brakes
      float b = brake ? brakeTorque : 0f;
      if (handbrake) b = handbrakeTorque;
      FL.brakeTorque = FR.brakeTorque = RL.brakeTorque = RR.brakeTorque = b;

      // Downforce
      rb.AddForce(-transform.up * downForce * rb.velocity.magnitude);

      // Wheel visuals
      UpdateWheelVisuals(FL, FLMesh);
      UpdateWheelVisuals(FR, FRMesh);
      UpdateWheelVisuals(RL, RLMesh);
      UpdateWheelVisuals(RR, RRMesh);

      if (reset)
      {
          reset = false;
          ResetUpright();
      }
  }

  void UpdateWheelVisuals(WheelCollider col, Transform mesh)
  {
      if (col == null || mesh == null) return;
      col.GetWorldPose(out Vector3 pos, out Quaternion rot);
      mesh.SetPositionAndRotation(pos, rot);
  }

  void ResetUpright()
  {
      Vector3 p = transform.position;
      transform.SetPositionAndRotation(p + Vector3.up * 2f, Quaternion.LookRotation(transform.forward, Vector3.up));
      rb.velocity = Vector3.zero;
      rb.angularVelocity = Vector3.zero;
  }

  void SnapToGround()
  {
      Vector3 start = transform.position + Vector3.up * groundRayStartHeight;
      if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, groundRayStartHeight * 2f, groundMask))
      {
          float r = Mathf.Max(FL ? FL.radius : 0.3f, 0.3f);
          transform.position = hit.point + Vector3.up * (r + 0.2f);
      }
  }
}
```

### 6-2) FollowCamera.cs
메인 카메라에 부착, target = ATV 루트
``` csharp
using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 3f, -7f);
    public float followLerp = 8f;
    public float lookLerp = 12f;

    void LateUpdate()
    {
        if (!target) return;
        Vector3 desired = target.TransformPoint(offset);
        transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * followLerp);

        Vector3 dir = (target.position + Vector3.up * 1.5f) - transform.position;
        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * lookLerp);
    }
}

```

### 6-3) SpawnAtLatLon.cs
선택적으로 ATV 루트에 부착, Lat/Lon/Height 좌표에서 시작
```csharp
using UnityEngine;
using CesiumForUnity;

public class SpawnAtLatLon : MonoBehaviour
{
    public double latitude = 37.5665, longitude = 126.9780, height = 150.0;
    void Start()
    {
        var a = GetComponent<CesiumGlobeAnchor>();
        if (a)
        {
            a.latitude = latitude;
            a.longitude = longitude;
            a.height = height;
            a.UpdatePositionFromLatitudeLongitudeHeight();
        }
    }
}

```

------------------------------------------------------------------------

## 7) 연결 체크리스트

-   **Cesium**
    -   World Terrain + Imagery 추가
    -   Tileset → Generate Physics Meshes = ON
    -   Georeference Origin 설정
-   **ATV (Variant)**
    -   Rigidbody(Mass 300\~450, Interpolate)
    -   PlayerInput + VehicleControls.inputactions 연결
    -   ATVController.cs → WheelCollider 4개 + (선택) Wheel Mesh
        Transform 연결
    -   차체 Collider 배치 확인
-   **카메라**
    -   FollowCamera.cs → target = ATV 루트
    -   Far Clip = 10000\~20000
-   **HDRP**
    -   Global Volume → Exposure/ACES 세팅

------------------------------------------------------------------------

## 8) 실행 & 조작

-   Play 모드 → 키보드 조작
    -   WASD: 주행
    -   Space: 브레이크
    -   LeftShift: 핸드브레이크
    -   R: 자세 리셋

------------------------------------------------------------------------

## 9) 트러블슈팅

-   **지형 뚫음/떠다님** → Generate Physics Meshes 확인, WheelCollider
    위치/Radius 조정
-   **씬뷰 밝기/어둠** → Exposure Fixed로 맞춘 뒤 Automatic
-   **입력 에러** → Input System Package 확인, PlayerInput 사용
-   **휠 안 돌아감** → Wheel Mesh Transform 연결 여부, Pivot 위치 확인
-   **메뉴 다르게 보임** → `Window → Cesium 패널` → Add to Scene 사용
    (1.15.4 UI 기준)

------------------------------------------------------------------------

## 10) 성능/퀄리티 팁

-   **Cesium Tileset**
    -   Maximum Screen Space Error: 4\~8 (낮으면 선명, 성능 비용↑)
    -   Maximum Cached Bytes: GPU/메모리에 맞게 증가
-   **그림자**
    -   Shadow Distance: 300\~600 m
    -   Cascades: 3\~4
-   **Anti-Aliasing**
    -   TAA 유지, 필요시 Sharpen 약간↑
-   **효과**
    -   SSR/Volumetrics → 컷신/정지 화면에만 적극 사용
    -   주행 중엔 최소화 권장
