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