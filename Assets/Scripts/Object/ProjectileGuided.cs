using System;
using UnityEngine;

public class ProjectileGuided : MonoBehaviour
{
    [Header("Runtime Params")]
    public float speed = 5f;             // �ʱ� ���� �ӵ�
    public float life = 12f;             // �������� ����� �ణ ���
    public float maxDistance = 1000f;
    public LayerMask hitMask = ~0;

    [Header("Explosion")]
    public GameObject explosionPrefab;
    public float explosionRadius = 6f;
    public float explosionForce = 1200f;
    public float explosionUpward = 0.5f;

    [Header("Guidance")]
    public float accelerate = 60f;       // ���� ���� �� ���ӷ�(ForceMode.Acceleration)
    public float turnDuration = 0.35f;   // ǥ�� Ȯ�� �� �� �ð� ���� ������ ǥ�������� ȸ��
    public float hitDistance = 3f;       // ǥ�� ���� ���� �ݰ�
    public bool keepHoming = false;      // true�� ��� ǥ���� ���� ����(�ʿ� ��)

    Rigidbody rb;
    Vector3 startPos;
    float spawnTime;

    GameObject owner;
    bool targetLocked = false;
    Vector3 targetWorldPos;
    float turnEndTime = 0f;

    public Action<ProjectileGuided> OnEnded; // �߻�ü ���� ���� �˸�(�ߺ� �߻� ������)

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
        // 45�� �ø� Muzzle�� forward�� �������� ������/���� ����Ʈ���� ȸ�� ����
        rb.linearVelocity = transform.forward * speed + inheritVelocity;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // ����/�Ÿ� ����
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

        // ǥ�� Ȯ�� �� ó��
        if (targetLocked)
        {
            // ǥ������ �Ÿ� üũ
            float d = Vector3.Distance(transform.position, targetWorldPos);
            if (d <= hitDistance)
            {
                Explode(transform.position);
                return;
            }

            // ���� ���߱� (�� ���� ����� ���ϸ� turnDuration �Ŀ� ȸ�� ����)
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

            // ����(���� � ��ȭ)
            rb.AddForce(transform.forward * accelerate, ForceMode.Acceleration);
        }
    }

    void OnCollisionEnter(Collision c)
    {
        // �߻�ü �����ڿ��� �浹 ����
        if (owner && c.collider.transform.IsChildOf(owner.transform)) return;

        Vector3 hitPos = (c.contactCount > 0) ? c.GetContact(0).point : transform.position;
        Explode(hitPos);
    }

    void Explode(Vector3 pos)
    {
        // ���� VFX
        if (explosionPrefab)
        {
            var fx = Instantiate(explosionPrefab, pos, Quaternion.identity);
            Destroy(fx, 5f);
        }

        // ���� ���߷�
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

    // ����: ��鿡�� ����/���� �ݰ� Ȯ��
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