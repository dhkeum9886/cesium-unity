using UnityEngine;
using UnityEngine.Video;

public class GuidedVideoAuto : MonoBehaviour
{
    [Header("Refs")]
    public VideoPlayer player;                    // GuidedUIPanel/VideoPlayer 할당
    public string resourcesPath = "video";        // Assets/Resources/video.mp4 → "video"

    [Header("Behavior")]
    [Tooltip("매 발사 때마다 0초부터 다시 재생")]
    public bool restartEachShot = true;

    private VideoClip _clip;      // 캐시

    void Reset()
    {
        if (!player) player = GetComponentInChildren<VideoPlayer>(true);
    }

    void Awake()
    {
        EnsureClipLoaded();
    }

    /// <summary>투사체 발사 직후 호출하세요.</summary>
    public void OnProjectileFired()
    {
        if (!player) return;

        // 혹시 Awake에서 로드 실패했을 수도 있으니 한 번 더 보장
        EnsureClipLoaded();

        if (!player.isPrepared)
        {
            player.prepareCompleted -= HandlePrepared;
            player.prepareCompleted += HandlePrepared;
            player.Prepare();
            return;
        }
        PlayInternal();
    }

    private void EnsureClipLoaded()
    {
        if (_clip == null)
        {
            _clip = Resources.Load<VideoClip>(resourcesPath);
            if (_clip == null)
            {
                Debug.LogError($"[GuidedVideoAuto] Resources.Load 실패: '{resourcesPath}'. " +
                               "경로/파일명 확인 (Assets/Resources/video.mp4 → 'video').");
                return;
            }
        }

        if (player != null)
        {
            player.source = VideoSource.VideoClip;
            if (player.clip != _clip)
                player.clip = _clip;
        }
    }

    private void HandlePrepared(VideoPlayer _)
    {
        player.prepareCompleted -= HandlePrepared;
        PlayInternal();
    }

    private void PlayInternal()
    {
        if (player == null) return;

        if (restartEachShot)
        {
            player.time = 0;
            player.frame = 0;
        }
        player.Play();
    }
}
