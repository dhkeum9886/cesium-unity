using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class GuidedUIController : MonoBehaviour
{
    [Header("Wiring")]
    public RectTransform rootPanel;   // ���� ��� �г�
    public RawImage videoImage;
    public VideoPlayer videoPlayer;
    public GameObject selectorGroup;  // ǥ�� ���� ��ư �׷�(ó���� ��Ȱ��)
    public Button target1Button;
    public Button target2Button;

    [Header("Timing")]
    public float selectorRevealTime = 2.5f; // �� �ð� �� ��ư ����
    public bool autoPickOnEnd = true;

    public Action<int> OnTargetSelected; // 1 �Ǵ� 2�� �ݹ�

    RenderTexture _rt;
    bool _selected = false;

    void Start()
    {
        selectorGroup.SetActive(false);

        // RenderTexture ����
        if (videoPlayer && videoImage)
        {
            _rt = new RenderTexture(1280, 720, 0);
            videoPlayer.targetTexture = _rt;
            videoImage.texture = _rt;

            // mp4�� StreamingAssets/clip.mp4 ����
            if (string.IsNullOrEmpty(videoPlayer.url))
                videoPlayer.url = System.IO.Path.Combine(Application.streamingAssetsPath, "clip.mp4");

            videoPlayer.isLooping = false;
            videoPlayer.loopPointReached += OnVideoEnded;
            videoPlayer.Play();
        }

        // ��ư �ڵ鷯
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