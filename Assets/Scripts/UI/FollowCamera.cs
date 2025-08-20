using UnityEngine;

// CinemachineFollowCamera 으로 대체, 비활성화 함
public class FollowCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 15f, -30f);
    public float followLerp = 5f;
    public float lookLerp = 5f;

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
