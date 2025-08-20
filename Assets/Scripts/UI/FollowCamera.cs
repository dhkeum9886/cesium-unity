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
