#if CESIUM_INSTALLED
using CesiumForUnity;
#endif
using UnityEngine;

#if CESIUM_INSTALLED
[RequireComponent(typeof(CesiumGlobeAnchor))]
#endif
[ExecuteAlways] // �����Ϳ����� ����
public class TargetMarker : MonoBehaviour
{
    [Header("Geodetic (deg / meters)")]
    [Tooltip("�浵 (Degrees)")]
    public double longitude;
    [Tooltip("���� (Degrees)")]
    public double latitude;
    [Tooltip("���� (Meters above ellipsoid/ground)")]
    public double height;

    [Header("Sync")]
    public bool autoSyncFromAnchor = true;  // ��Ŀ���ʵ� �ڵ� �ݿ�
    public bool autoPushToAnchor = false;   // �ʵ���Ŀ �ڵ� �ݿ�(���� ��)

#if CESIUM_INSTALLED
    CesiumGlobeAnchor _anchor;
#endif
    double _lastLon, _lastLat, _lastH;

    void OnEnable()
    {
#if CESIUM_INSTALLED
        _anchor = GetComponent<CesiumGlobeAnchor>();
#endif
        // ���� �� �� ����ȭ
        PullFromAnchor();
    }

    void Update()
    {
        // ������/��Ÿ�� ���� ����
        if (autoSyncFromAnchor) PullFromAnchor();
        else if (autoPushToAnchor) PushToAnchor();
    }

    public void PullFromAnchor()
    {
#if CESIUM_INSTALLED
        if (_anchor)
        {
            // ��Ŀ�� ���� ���� ��ǥ�� �о �ʵ忡 �ݿ�
            longitude = _anchor.longitude;
            latitude  = _anchor.latitude;
            height    = _anchor.height;

            _lastLon = longitude; _lastLat = latitude; _lastH = height;
        }
        else
        {
            Debug.LogWarning($"{name}: CesiumGlobeAnchor�� ���� ��/�浵 �ڵ���� �Ұ�.");
        }
#endif
    }

    public void PushToAnchor()
    {
#if CESIUM_INSTALLED
        if (_anchor)
        {
            // �ʵ� ���� ��Ŀ�� �Ἥ Ʈ������ ��ġ�� ����
            _anchor.longitude = longitude;
            _anchor.latitude  = latitude;
            _anchor.height    = height;
        }
        else
        {
            Debug.LogWarning($"{name}: CesiumGlobeAnchor�� ���� ���� Ǫ���� �� �����ϴ�.");
        }
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // �ν����Ϳ��� �ʵ尡 ����Ǹ� ��Ŀ�� Ǫ�� (����)
        if (autoPushToAnchor &&
            (longitude != _lastLon || latitude != _lastLat || height != _lastH))
        {
            PushToAnchor();
            _lastLon = longitude; _lastLat = latitude; _lastH = height;
        }
    }

    [ContextMenu("Anchor �� Fields (Pull)")]
    void CtxPull() => PullFromAnchor();

    [ContextMenu("Fields �� Anchor (Push)")]
    void CtxPush() => PushToAnchor();
#endif
}
