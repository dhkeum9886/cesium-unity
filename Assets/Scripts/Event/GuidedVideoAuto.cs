using UnityEngine;
using UnityEngine.Video;

public class GuidedVideoAuto : MonoBehaviour
{
    [Header("Refs")]
    public VideoPlayer player;                    // GuidedUIPanel/VideoPlayer �Ҵ�
    public string resourcesPath = "video";        // Assets/Resources/video.mp4 �� "video"

    [Header("Behavior")]
    [Tooltip("�� �߻� ������ 0�ʺ��� �ٽ� ���")]
    public bool restartEachShot = true;

    private VideoClip _clip;      // ĳ��

    void Reset()
    {
        if (!player) player = GetComponentInChildren<VideoPlayer>(true);
    }

    void Awake()
    {
        EnsureClipLoaded();
    }

    /// <summary>����ü �߻� ���� ȣ���ϼ���.</summary>
    public void OnProjectileFired()
    {
        if (!player) return;

        // Ȥ�� Awake���� �ε� �������� ���� ������ �� �� �� ����
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
                Debug.LogError($"[GuidedVideoAuto] Resources.Load ����: '{resourcesPath}'. " +
                               "���/���ϸ� Ȯ�� (Assets/Resources/video.mp4 �� 'video').");
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
