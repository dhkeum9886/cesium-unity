#if CESIUM_INSTALLED
using CesiumForUnity;
#endif
using UnityEngine;

#if CESIUM_INSTALLED
[RequireComponent(typeof(CesiumGlobeAnchor))]
#endif
[ExecuteAlways] // 에디터에서도 동작
public class TargetMarker : MonoBehaviour
{
    [Header("Geodetic (deg / meters)")]
    [Tooltip("경도 (Degrees)")]
    public double longitude;
    [Tooltip("위도 (Degrees)")]
    public double latitude;
    [Tooltip("높이 (Meters above ellipsoid/ground)")]
    public double height;

    [Header("Sync")]
    public bool autoSyncFromAnchor = true;  // 앵커→필드 자동 반영
    public bool autoPushToAnchor = false;   // 필드→앵커 자동 반영(보통 끔)

#if CESIUM_INSTALLED
    CesiumGlobeAnchor _anchor;
#endif
    double _lastLon, _lastLat, _lastH;

    void OnEnable()
    {
#if CESIUM_INSTALLED
        _anchor = GetComponent<CesiumGlobeAnchor>();
#endif
        // 최초 한 번 동기화
        PullFromAnchor();
    }

    void Update()
    {
        // 에디터/런타임 공통 동작
        if (autoSyncFromAnchor) PullFromAnchor();
        else if (autoPushToAnchor) PushToAnchor();
    }

    public void PullFromAnchor()
    {
#if CESIUM_INSTALLED
        if (_anchor)
        {
            // 앵커의 현재 지리 좌표를 읽어서 필드에 반영
            longitude = _anchor.longitude;
            latitude  = _anchor.latitude;
            height    = _anchor.height;

            _lastLon = longitude; _lastLat = latitude; _lastH = height;
        }
        else
        {
            Debug.LogWarning($"{name}: CesiumGlobeAnchor가 없어 위/경도 자동취득 불가.");
        }
#endif
    }

    public void PushToAnchor()
    {
#if CESIUM_INSTALLED
        if (_anchor)
        {
            // 필드 값을 앵커에 써서 트랜스폼 위치를 갱신
            _anchor.longitude = longitude;
            _anchor.latitude  = latitude;
            _anchor.height    = height;
        }
        else
        {
            Debug.LogWarning($"{name}: CesiumGlobeAnchor가 없어 값을 푸시할 수 없습니다.");
        }
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 인스펙터에서 필드가 변경되면 앵커로 푸시 (선택)
        if (autoPushToAnchor &&
            (longitude != _lastLon || latitude != _lastLat || height != _lastH))
        {
            PushToAnchor();
            _lastLon = longitude; _lastLat = latitude; _lastH = height;
        }
    }

    [ContextMenu("Anchor → Fields (Pull)")]
    void CtxPull() => PullFromAnchor();

    [ContextMenu("Fields → Anchor (Push)")]
    void CtxPush() => PushToAnchor();
#endif
}
