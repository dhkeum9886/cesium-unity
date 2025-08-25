using System;
using UnityEngine;

public class ProjectileGuided : MonoBehaviour
{
    [Header("Runtime Params")]
    public float speed = 5f;             // 초기 느린 속도
    public float life = 12f;             // 유도까지 고려해 약간 길게
    public float maxDistance = 1000f;
    public LayerMask hitMask = ~0;

    [Header("Explosion")]
    public GameObject explosionPrefab;
    public float explosionRadius = 6f;
    public float explosionForce = 1200f;
    public float explosionUpward = 0.5f;

    [Header("Guidance")]
    public float accelerate = 60f;       // 유도 시작 후 가속량(ForceMode.Acceleration)
    public float turnDuration = 0.35f;   // 표적 확정 후 이 시간 동안 방향을 표적쪽으로 회전
    public float hitDistance = 3f;       // 표적 도달 판정 반경
    public bool keepHoming = false;      // true면 계속 표적을 향해 조향(필요 시)

    Rigidbody rb;
    Vector3 startPos;
    float spawnTime;

    GameObject owner;
    bool targetLocked = false;
    Vector3 targetWorldPos;
    float turnEndTime = 0f;

    public Action<ProjectileGuided> OnEnded; // 발사체 수명 종료 알림(중복 발사 방지용)

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
        // 45도 올린 Muzzle의 forward가 들어오도록 프리팹/스폰 포인트에서 회전 설정
        rb.linearVelocity = transform.forward * speed + inheritVelocity;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // 수명/거리 제한
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

        // 표적 확정 후 처리
        if (targetLocked)
        {
            // 표적과의 거리 체크
            float d = Vector3.Distance(transform.position, targetWorldPos);
            if (d <= hitDistance)
            {
                Explode(transform.position);
                return;
            }

            // 방향 맞추기 (한 번만 직선운동 원하면 turnDuration 후엔 회전 고정)
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

            // 가속(직선 운동 강화)
            rb.AddForce(transform.forward * accelerate, ForceMode.Acceleration);
        }
    }

    void OnCollisionEnter(Collision c)
    {
        // 발사체 소유자와의 충돌 무시
        if (owner && c.collider.transform.IsChildOf(owner.transform)) return;

        Vector3 hitPos = (c.contactCount > 0) ? c.GetContact(0).point : transform.position;
        Explode(hitPos);
    }

    void Explode(Vector3 pos)
    {
        // 폭발 VFX
        if (explosionPrefab)
        {
            var fx = Instantiate(explosionPrefab, pos, Quaternion.identity);
            Destroy(fx, 5f);
        }

        // 물리 폭발력
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

    // 선택: 장면에서 폭발/도달 반경 확인
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