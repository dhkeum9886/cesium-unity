# Unity Guided Projectile Implementation (유도탄 구현 가이드)

> 본 문서는 **누락 없이** 전체 내용을 담은 마크다운 버전입니다. 프로젝트에 바로 붙여 사용하실 수 있도록 코드 블록과 체크리스트를 포함했습니다.

---

## 0) 준비물/개요

- **새 프리팹**: `ProjectileGuided.prefab` (기존 `Projectile` 프리팹 복제)
- **새 스크립트**  
  - `ProjectileGuided.cs` : 유도/가속/명중 판정 + 발사 중 **중복 발사 금지** 콜백
  - `GuidedUIController.cs` : 우측 상단 UI, mp4 재생, 특정 시점에 표적 선택 UI 노출, **미선택 시 랜덤**
  - `TargetMarker.cs` (**선택**) : 표적의 위경도/고도를 인스펙터에서 입력 → **Cesium 앵커로 월드 좌표화**
- **씬 오브젝트**
  - **GuidedMuzzle(45°)** : 포구를 45° 올린 **빈 오브젝트**(또는 기존 Muzzle 자식으로 **X = -45** 회전)
  - **Target1, Target2** : 표적 트랜스폼(권장: `TargetMarker` 붙이거나, 미리 `CesiumGlobeAnchor`로 위치 지정)
  - **Canvas 프리팹**: `GuidedUIPanel.prefab` (Top-Right 앵커, **RawImage + VideoPlayer**, 버튼 그룹)

---

## 1) 프리팹/리짓바디 설정

1. `Projectile` 프리팹을 **복제** → `ProjectileGuided.prefab`
2. **Rigidbody** 설정
   - **Mass**: `0.2` (그대로)
   - **Interpolate** = `Interpolate`
   - **Collision Detection** = `Continuous Dynamic`
   - **Use Gravity**: 필요 시 `true`(포물선 시작). *유도 이후 가속을 크게 주면 중력 영향이 작아짐*
3. 기존 `Projectile (Script)` **제거**, 새 스크립트 **`ProjectileGuided`**를 붙임
4. **초기 속도 매우 느리게**: `speed`를 작게(예: **5**) 설정

---

## 2) 발사 포구 45° 만들기

- `Muzzle`를 **자식으로 하나 복제**해 **GuidedMuzzle** 생성  
- **Local Rotation X = -45** (위로 45°) 로 설정  
- 이후 **유도탄은 이 `GuidedMuzzle`에서 스폰**합니다. *(간단하고 확실)*

---

## 3) “발사 중 중복 발사 금지” 제어

- 발사체가 **살아있는 동안은 다시 못 쏘게**, 발사자(`ATVController` 등)에서 **참조를 들고 있다가** 발사체 **파괴 시 null**로 돌립니다.  
- `ProjectileGuided`가 **`OnEnded` 이벤트**로 알려줍니다.

---

## 4) 우측 상단 UI + mp4 + 표적 선택

- **Canvas**(`Screen Space - Overlay`), **우상단으로 앵커**한 패널(`RectTransform`)을 둡니다.
- **RawImage + VideoPlayer** 구성
  - `VideoPlayer` → Render Mode: **RenderTexture 권장**(또는 RawImage의 Texture 직접)
  - mp4 파일은 **`Assets/StreamingAssets/clip.mp4`**에 두고,  
    `VideoPlayer.url = Application.streamingAssetsPath + "/clip.mp4"`
- **버튼 그룹(처음엔 비활성화)** : “표적 1”, “표적 2” 버튼
- **노출 타이밍**: 재생 후 **N초 뒤** 버튼 그룹 `SetActive(true)`
- **자동 랜덤 선택**: `VideoPlayer.loopPointReached`(영상 종료) 시 아직 선택 안 했으면 **무작위**로 선택
- 이 패널은 프리팹 **`GuidedUIPanel.prefab`** 으로 만들어 두고 **런타임에 `Instantiate`** 합니다.

---

## 5) 표적(위경도 → 월드좌표) 준비 2가지 방법

- **A안(권장)**: 씬에 `Target1`/`Target2` 빈 오브젝트를 만들고 **`CesiumGlobeAnchor`** 로 위/경도/고도 입력 → 그 트랜스폼을 **직접 참조**해서 사용(월드좌표는 `transform.position`).
- **B안**: 위/경도/고도를 **스크립트에 숫자**로 보관하고, 런타임에 **`CesiumGlobeAnchor`를 임시로 만들어 좌표 변환**(아래 `TargetMarker` 코드 참고).

---

## 6) 코드

### 6-1) `ProjectileGuided.cs`

```csharp
using System;
using UnityEngine;

public class ProjectileGuided : MonoBehaviour
{
    [Header("Runtime Params")]
    public float speed = 5f;             // 초기 느린 속도
    public float life = 12f;             // 유도까지 고려해 약간 길게
    public float maxDistance = 1000f;
    public LayerMask hitMask = ~0;

    [Header("Explosion")]
    public GameObject explosionPrefab;
    public float explosionRadius = 6f;
    public float explosionForce = 1200f;
    public float explosionUpward = 0.5f;

    [Header("Guidance")]
    public float accelerate = 60f;       // 유도 시작 후 가속량(ForceMode.Acceleration)
    public float turnDuration = 0.35f;   // 표적 확정 후 이 시간 동안 방향을 표적쪽으로 회전
    public float hitDistance = 3f;       // 표적 도달 판정 반경
    public bool keepHoming = false;      // true면 계속 표적을 향해 조향(필요 시)

    Rigidbody rb;
    Vector3 startPos;
    float spawnTime;

    GameObject owner;
    bool targetLocked = false;
    Vector3 targetWorldPos;
    float turnEndTime = 0f;

    public Action<ProjectileGuided> OnEnded; // 발사체 수명 종료 알림(중복 발사 방지용)

    public void Init(GameObject owner, float speed, float life, float maxDist,
                     LayerMask hitMask, Vector3 inheritVelocity)
    {
        this.owner = owner;
        this.speed = speed;
        this.life = life;
        this.maxDistance = maxDist;
        this.hitMask = hitMask;

        if (!rb) rb = GetComponent<Rigidbody>();

        spawnTime = Time.time;
        startPos = transform.position;

        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // 45도 올린 Muzzle의 forward가 들어오도록 프리팹/스폰 포인트에서 회전 설정
        rb.linearVelocity = transform.forward * speed + inheritVelocity;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // 수명/거리 제한
        if (Time.time - spawnTime > life)
        {
            Explode(transform.position);
            return;
        }
        if ((transform.position - startPos).sqrMagnitude > maxDistance * maxDistance)
        {
            Explode(transform.position);
            return;
        }

        // 표적 확정 후 처리
        if (targetLocked)
        {
            // 표적과의 거리 체크
            float d = Vector3.Distance(transform.position, targetWorldPos);
            if (d <= hitDistance)
            {
                Explode(transform.position);
                return;
            }

            // 방향 맞추기 (한 번만 직선운동 원하면 turnDuration 후엔 회전 고정)
            if (keepHoming)
            {
                Vector3 dir = (targetWorldPos - transform.position).normalized;
                Quaternion to = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, to, 360f * Time.deltaTime);
            }
            else
            {
                if (Time.time < turnEndTime)
                {
                    Vector3 dir = (targetWorldPos - transform.position).normalized;
                    Quaternion to = Quaternion.LookRotation(dir, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, to,
                        Mathf.InverseLerp(turnEndTime - turnDuration, turnEndTime, Time.time));
                }
            }

            // 가속(직선 운동 강화)
            rb.AddForce(transform.forward * accelerate, ForceMode.Acceleration);
        }
    }

    void OnCollisionEnter(Collision c)
    {
        // 발사체 소유자와의 충돌 무시
        if (owner && c.collider.transform.IsChildOf(owner.transform)) return;

        Vector3 hitPos = (c.contactCount > 0) ? c.GetContact(0).point : transform.position;
        Explode(hitPos);
    }

    void Explode(Vector3 pos)
    {
        // 폭발 VFX
        if (explosionPrefab)
        {
            var fx = Instantiate(explosionPrefab, pos, Quaternion.identity);
            Destroy(fx, 5f);
        }

        // 물리 폭발력
        var cols = Physics.OverlapSphere(pos, explosionRadius, hitMask, QueryTriggerInteraction.Ignore);
        foreach (var col in cols)
        {
            var r = col.attachedRigidbody;
            if (r && (!owner || !r.transform.IsChildOf(owner.transform)))
                r.AddExplosionForce(explosionForce, pos, explosionRadius, explosionUpward, ForceMode.Impulse);
        }

        OnEnded?.Invoke(this);
        Destroy(gameObject);
    }

    public void AssignTargetWorld(Vector3 worldPos)
    {
        targetWorldPos = worldPos;
        targetLocked = true;
        turnEndTime = Time.time + turnDuration;
    }

    void OnDestroy()
    {
        OnEnded?.Invoke(this);
    }

    // 선택: 장면에서 폭발/도달 반경 확인
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);

        if (targetLocked)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(targetWorldPos, hitDistance);
        }
    }
}
```

---

### 6-2) `GuidedUIController.cs`

```csharp
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class GuidedUIController : MonoBehaviour
{
    [Header("Wiring")]
    public RectTransform rootPanel;   // 우측 상단 패널
    public RawImage videoImage;
    public VideoPlayer videoPlayer;
    public GameObject selectorGroup;  // 표적 선택 버튼 그룹(처음엔 비활성)
    public Button target1Button;
    public Button target2Button;

    [Header("Timing")]
    public float selectorRevealTime = 2.5f; // 이 시간 뒤 버튼 노출
    public bool autoPickOnEnd = true;

    public Action<int> OnTargetSelected; // 1 또는 2로 콜백

    RenderTexture _rt;
    bool _selected = false;

    void Start()
    {
        selectorGroup.SetActive(false);

        // RenderTexture 세팅
        if (videoPlayer && videoImage)
        {
            _rt = new RenderTexture(1280, 720, 0);
            videoPlayer.targetTexture = _rt;
            videoImage.texture = _rt;

            // mp4는 StreamingAssets/clip.mp4 가정
            if (string.IsNullOrEmpty(videoPlayer.url))
                videoPlayer.url = System.IO.Path.Combine(Application.streamingAssetsPath, "clip.mp4");

            videoPlayer.isLooping = false;
            videoPlayer.loopPointReached += OnVideoEnded;
            videoPlayer.Play();
        }

        // 버튼 핸들러
        target1Button.onClick.AddListener(() => Select(1));
        target2Button.onClick.AddListener(() => Select(2));

        StartCoroutine(RevealAfter());
    }

    IEnumerator RevealAfter()
    {
        yield return new WaitForSeconds(selectorRevealTime);
        selectorGroup.SetActive(true);
    }

    void OnVideoEnded(VideoPlayer vp)
    {
        if (!_selected && autoPickOnEnd)
        {
            int rnd = UnityEngine.Random.value < 0.5f ? 1 : 2;
            Select(rnd);
        }
        CloseLater();
    }

    void Select(int idx)
    {
        if (_selected) return;
        _selected = true;
        OnTargetSelected?.Invoke(idx);
        selectorGroup.SetActive(false);
    }

    public void CloseNow()
    {
        if (_rt) _rt.Release();
        Destroy(gameObject);
    }

    public void CloseLater(float delay = 0.1f)
    {
        StartCoroutine(_close(delay));
        IEnumerator _close(float d)
        {
            yield return new WaitForSeconds(d);
            CloseNow();
        }
    }
}
```

---

### 6-3) `TargetMarker.cs` (선택: 위/경도/고도 → 앵커/월드)

```csharp
#if CESIUM_INSTALLED
using CesiumForUnity;
#endif
using UnityEngine;

/// 씬에 Target1/Target2 빈 오브젝트에 붙여,
/// 인스펙터에서 위/경도/고도 입력 → 월드 위치 고정
public class TargetMarker : MonoBehaviour
{
    [Tooltip("Degrees")]
    public double longitude;
    [Tooltip("Degrees")]
    public double latitude;
    [Tooltip("Meters above ellipsoid/ground (프로젝트 기준)")]
    public double height;

#if CESIUM_INSTALLED
    void Awake()
    {
        var anchor = GetComponent<CesiumGlobeAnchor>();
        if (anchor)
        {
            anchor.longitude = longitude;
            anchor.latitude = latitude;
            anchor.height = height;
            // Cesium이 transform.position을 갱신
        }
        else
        {
            Debug.LogWarning("CesiumGlobeAnchor가 없어요. 직접 월드 위치를 지정하거나 앵커를 추가하세요.");
        }
    }
#endif
}
```

> **참고**: Cesium 네임스페이스/필드명은 버전에 따라 조금 다를 수 있습니다. 사용 중인 버전의 `CesiumGlobeAnchor` 인스펙터에서 보이는 프로퍼티명에 맞춰 주세요. (*앵커를 쓰면 변환 코드를 직접 짤 필요가 없습니다.*)

---

## 7) 발사자(예: `ATVController`)에서 연결

아래처럼 “**한 발만**” 나가게 제어 + UI → 표적 선택 → 유도탄에 전달.

```csharp
using UnityEngine;
using UnityEngine.Video;

public class GuidedLauncher : MonoBehaviour
{
    public Transform guidedMuzzle;                   // 45도 올린 포구
    public GameObject guidedProjectilePrefab;        // ProjectileGuided 프리팹
    public LayerMask explosionMask = ~0;

    [Header("UI")]
    public GuidedUIController guidedUIPanelPrefab;   // 프리팹
    public float selectorRevealTime = 2.5f;
    public VideoClip guidanceClip;                   // 비워두면 StreamingAssets/clip.mp4 사용

    [Header("Targets")]
    public Transform target1; // Target1 오브젝트(권장: TargetMarker 또는 CesiumGlobeAnchor 보유)
    public Transform target2;

    [Header("Projectile Tuning")]
    public float initialSpeed = 5f;
    public float life = 12f;
    public float maxDistance = 1000f;

    Rigidbody ownerRb;
    ProjectileGuided active; // 살아있는 유도탄 참조

    void Awake()
    {
        ownerRb = GetComponent<Rigidbody>();
    }

    public void FireGuided()
    {
        // 이미 발사중이면 무시
        if (active) return;

        // 스폰
        var t = guidedMuzzle ? guidedMuzzle : transform;
        var go = Instantiate(guidedProjectilePrefab, t.position, t.rotation);
        active = go.GetComponent<ProjectileGuided>();
        active.OnEnded += OnProjectileEnded;

        var inherit = ownerRb ? ownerRb.linearVelocity : Vector3.zero;
        active.Init(gameObject, initialSpeed, life, maxDistance, explosionMask, inherit);

        // 우측 상단 UI
        var ui = Instantiate(guidedUIPanelPrefab);
        if (guidanceClip && ui.videoPlayer) ui.videoPlayer.clip = guidanceClip;
        ui.selectorRevealTime = selectorRevealTime;

        ui.OnTargetSelected += idx =>
        {
            var tr = (idx == 1) ? target1 : target2;
            if (tr == null)
            {
                Debug.LogWarning("표적 Transform이 비어있습니다.");
                return;
            }
            active.AssignTargetWorld(tr.position);
            ui.CloseLater(0.2f); // 선택 즉시 UI 정리
        };
    }

    void OnProjectileEnded(ProjectileGuided pg)
    {
        if (active == pg) active = null;
    }
}
```

> 발사 입력은 기존 Input System에서 **`FireGuided()`** 를 호출만 하면 됩니다. (예: **F키** 또는 게임패드 버튼)

---

## 8) 에디터에서 연결 체크리스트

### `ProjectileGuided.prefab`
- `ProjectileGuided` 스크립트가 붙어있는지
- **Explosion** 관련 필드에 기존 **ExplosionCommon 프리팹** 연결
- `speed`(초기 느림), `accelerate`, `turnDuration`, `hitDistance` 값 조정

### Launcher(ATV)
- `GuidedLauncher` **추가**
- `guidedMuzzle` = **45도 올린 포구**
- `guidedProjectilePrefab` = **ProjectileGuided.prefab**
- `target1`, `target2` = 씬의 **Target** 오브젝트

### UI 프리팹
- `GuidedUIPanel.prefab` 생성 후 `GuidedUIController` **연결**
- **RawImage**, **VideoPlayer**, **버튼 그룹/버튼 2개** 연결
- mp4는 **`Assets/StreamingAssets/clip.mp4`** (또는 **VideoClip** 할당)

### 표적
- `Target1`/`Target2`에 **CesiumGlobeAnchor** 또는 **TargetMarker**로 **위경도/고도 입력**
- 또는 직접 **월드 위치 배치**

---

## 9) 동작 흐름(요구사항 매핑)

- **포구 45°** : `GuidedMuzzle`를 45° 올려서 발사  
- **발사속도 느림** : `ProjectileGuided.speed`를 낮게  
- **중복 발사 금지** : `GuidedLauncher`가 `active` 참조로 제어(파괴 시 콜백으로 해제)  
- **UI + mp4 재생** : `GuidedUIController`가 **우측 상단**에 패널 열고 재생  
- **일정 시간 후 표적 선택 UI 노출 / 미선택 랜덤** : `selectorRevealTime`, `loopPointReached` 처리  
- **표적 정해지면 방향 전환 + 가속 직선운동** : `AssignTargetWorld` → `turnDuration` 동안 방향 맞추고 **가속**  
- **목표 도달 시 파괴** : `hitDistance` 이내 진입 → `Explode()`  

---

## 10) 팁/자주 생기는 문제

- **mp4 코덱**: Windows/Editor에선 대부분 재생되지만, 코덱/해상도 문제면 **H.264, 1080p 이하** 권장.
- **Cesium 좌표**: 가장 안전한 방법은 `CesiumGlobeAnchor`를 **씬에 직접 배치**해서 `Transform`을 쓰는 것.
- **중력과 포물선**: 시작은 **느린 속도**로 포물선, 표적 선택 후 **가속**이 커지면 거의 직선으로 들어갑니다. 원하면 **Use Gravity**를 끄세요.
- **관통 방지**: Rigidbody는 **Continuous Dynamic** 유지, **콜라이더** 적절한 크기 설정.
- (**버전 노트**) 구버전 Unity를 쓰면 `rb.linearVelocity` 대신 `rb.velocity` 사용이 필요할 수 있습니다.

---

## (부록) `GuidedUIPanel.prefab` 구조 예시

```
Canvas (Screen Space - Overlay)
└── GuidedUIPanel (RectTransform; Anchor: TopRight, Pivot 1,1; size 480x300)
    ├── VideoFrame (RawImage, AspectFitter=EnvelopeParent)
    ├── ButtonGroup (HorizontalLayoutGroup, active=false initially)
    │   ├── Button_Target1 (Text: "표적 1")
    │   └── Button_Target2 (Text: "표적 2")
    └── (Optional) Title/Text (UI Text or TMP)
```

- **Anchors**: `GuidedUIPanel`의 **Min/Max (1,1)**, **Pivot (1,1)**, 우상단 마진 `(top=20, right=20)`
- **VideoPlayer**: `GameObject`는 어디든 가능(일반적으로 `GuidedUIPanel` 루트에 붙이고, `targetTexture`를 `VideoFrame.RawImage`로 표시)

---

### 파일 배치 힌트
```
Assets/
├── Prefabs/
│   ├── ProjectileGuided.prefab
│   └── GuidedUIPanel.prefab
├── Scripts/
│   ├── ProjectileGuided.cs
│   ├── GuidedUIController.cs
│   └── TargetMarker.cs
└── StreamingAssets/
    └── clip.mp4
```

---

