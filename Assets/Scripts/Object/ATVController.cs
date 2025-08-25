using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class ATVController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider FL;
    public WheelCollider FR;
    public WheelCollider RL;
    public WheelCollider RR;

    [Header("Wheel Meshes (optional)")]
    public Transform FLMesh;
    public Transform FRMesh;
    public Transform RLMesh;
    public Transform RRMesh;

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

    [Header("Fire")]
    public Transform muzzle;                // 1�ܰ迡�� ���� Muzzle
    public GameObject projectilePrefab;     // 3�ܰ迡�� ���� Projectile ������
    public float projectileSpeed = 80f;     // m/s
    public float projectileLife = 5f;       // ��
    public float projectileMaxDistance = 300f;
    public float fireCooldown = 0.15f;      // ���� ����
    public LayerMask projectileHitMask = ~0;// ���� ���� ������ �� ���̾�
    float _nextFireTime;

    Rigidbody rb;
    Vector2 move;
    bool brake, handbrake, reset;

    Vector3 _scenePlacedPos;
    Quaternion _scenePlacedRot;

    [Header("UI/Video")]
    public GuidedVideoAuto guidedVideoAuto;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.centerOfMass = new Vector3(0, -0.3f, 0);

        _scenePlacedPos = transform.position;
        _scenePlacedRot = transform.rotation;
    }

    void Start()
    {
        if (snapToGroundOnStart) SnapToGround();
    }

    public void OnMove(InputValue v) => move = v.Get<Vector2>();
    public void OnBrake(InputValue v) => brake = v.isPressed;
    public void OnHandbrake(InputValue v) => handbrake = v.isPressed;
    public void OnReset(InputValue v) { if (v.isPressed) reset = true; }
    public void OnFire(InputValue v)
    {
        if (v.isPressed)
            TryFire();
    }

    void TryFire()
    {
        if (!muzzle || !projectilePrefab) return;
        if (Time.time < _nextFireTime) return;
        _nextFireTime = Time.time + fireCooldown;

        Debug.Log("FIRE!");

        // ������ ����
        var go = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);

        // �ڱ� �ڽŰ� �浹 ����
        var projCol = go.GetComponent<Collider>();
        if (projCol)
        {
            foreach (var col in GetComponentsInChildren<Collider>())
                if (col) Physics.IgnoreCollision(projCol, col, true);
        }

        // �ʱ� �ӵ� ����(���� �ӵ� ��� + �ѱ� ����)
        var pr = go.GetComponent<Projectile>();
        var inheritVel = rb ? rb.linearVelocity : Vector3.zero;
        if (pr)
        {
            pr.Init(gameObject, projectileSpeed, projectileLife, projectileMaxDistance,
                    projectileHitMask, inheritVel);
        }
        else
        {
            // ���� Projectile ��ũ��Ʈ�� ���ٸ� �ּ����� �ʱ� �ӵ���
            var r = go.GetComponent<Rigidbody>();
            if (r) r.linearVelocity = muzzle.forward * projectileSpeed + inheritVel;
        }

        guidedVideoAuto?.OnProjectileFired();
    }

    void FixedUpdate()
    {
        float speedKPH = rb.linearVelocity.magnitude * 3.6f;
        float throttle = Mathf.Clamp(move.y, -1f, 1f);
        float steer = Mathf.Clamp(move.x, -1f, 1f);

        // Steer (front)
        if (FL && FR)
        {
            FL.steerAngle = maxSteerAngle * steer;
            FR.steerAngle = maxSteerAngle * steer;
        }

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
            float per = torque * 0.25f;
            //FL.motorTorque = FR.motorTorque = RL.motorTorque = RR.motorTorque = half;
            FL.motorTorque = FR.motorTorque = RL.motorTorque = RR.motorTorque = per;
        }

        // Brakes
        float b = brake ? brakeTorque : 0f;
        if (handbrake) b = handbrakeTorque;

        if (FL) FL.brakeTorque = b;
        if (FR) FR.brakeTorque = b;
        if (RL) RL.brakeTorque = b;
        if (RR) RR.brakeTorque = b;

        // Downforce
        rb.AddForce(-transform.up * downForce * rb.linearVelocity.magnitude);

        // Wheel visuals
        UpdateWheelVisuals(FL, FLMesh);
        UpdateWheelVisuals(FR, FRMesh);
        UpdateWheelVisuals(RL, RLMesh);
        UpdateWheelVisuals(RR, RRMesh);

        if (reset)
        {
            reset = false;
            //ResetUpright();
            ResetToScenePlacement();
        }
    }

    void ResetToScenePlacement()
    {
        // 물리 끄고 위치/자세 리셋
        rb.isKinematic = true;

        transform.SetPositionAndRotation(_scenePlacedPos, _scenePlacedRot);

        // 속도 초기화
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.isKinematic = false;
        rb.Sleep(); // 물리 안정화
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
        rb.linearVelocity = Vector3.zero;
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
