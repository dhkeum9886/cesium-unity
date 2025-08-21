using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Runtime Params")]
    public float speed = 80f;
    public float life = 5f;
    public float maxDistance = 300f;
    public LayerMask hitMask = ~0;    // 폭발 힘 적용 대상

    [Header("Explosion")]
    public GameObject explosionPrefab;
    public float explosionRadius = 6f;
    public float explosionForce = 1200f;
    public float explosionUpward = 0.5f;

    Rigidbody rb;
    Vector3 startPos;
    float spawnTime;
    GameObject owner;

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
    }

    void OnCollisionEnter(Collision c)
    {
        // 스스로(발사체 소유 차량)와의 충돌은 무시
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

        Destroy(gameObject);
    }

    // (선택) 장면에서 폭발 반경 표시
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
