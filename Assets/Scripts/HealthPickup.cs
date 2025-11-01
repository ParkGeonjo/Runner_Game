using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HealthPickup : MonoBehaviour
{
    [Header("Heal Amount")]
    [SerializeField] private int healValue = 15;

    [Header("FX (optional)")]
    [SerializeField] private GameObject pickupFxPrefab; // 회복 이펙트(선택)
    [SerializeField] private AudioClip pickupSfx;       // 회복 사운드(선택)
    [SerializeField, Range(0f,1f)] private float sfxVolume = 1f;

    // ========= Magnet (젤리/코인과 동일 설정) =========
    [Header("Magnet")]
    [SerializeField] private float magnetRadius = 6.0f;  // 끌림 시작 반경
    [SerializeField] private float magnetSpeed  = 14.0f; // 기본 속도
    [SerializeField] private float magnetAccel  = 22.0f; // 근접 가속

    private bool consumed;
    private Rigidbody2D rb2d;

    // 원래 스폰 위치 기억 → 재활성 시 복귀 (끌려 먹힌 자리에서 재스폰 방지)
    private Vector3 _spawnLocalPos;
    private bool _spawnPosCaptured = false;

    private void Awake()
    {
        // 트리거 권장 + 접촉 안정화(젤리/코인과 동일 패턴)
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        rb2d = GetComponent<Rigidbody2D>();
        if (!rb2d) rb2d = gameObject.AddComponent<Rigidbody2D>();
        rb2d.isKinematic = true;
        rb2d.useFullKinematicContacts = true;
        rb2d.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void OnEnable()
    {
        // 최초 한 번만 원본 로컬 좌표 캡처 → 재활용 시 원래 자리로 복귀
        if (!_spawnPosCaptured)
        {
            _spawnLocalPos = transform.localPosition;
            _spawnPosCaptured = true;
        }
        transform.localPosition = _spawnLocalPos;

        consumed = false;
    }

    private void Update()
    {
        // 자력: 캐릭터3번만 활성, 게임오버시 비활성
        if (InGameManager.instance == null || InGameManager.instance.isGameover) return;
        if (!InGameManager.instance.MagnetActive) return;

        var player = InGameManager.instance.PlayerTransform;
        if (!player) return;

        // ★ 플레이어 "콜라이더 중심"으로 끌림(정확한 중앙) 
        Vector3 target = InGameManager.instance.PlayerMagnetCenter; // 콜라이더 중심 제공
        Vector3 to = target - transform.position;
        float dist = to.magnitude;
        if (dist > magnetRadius) return;

        float t = Mathf.InverseLerp(magnetRadius, 0f, dist);
        float speed = magnetSpeed + magnetAccel * t;
        transform.position += to.normalized * speed * Time.deltaTime;
    }

    private void OnTriggerEnter2D(Collider2D other) { TryConsume(other); }
    private void OnTriggerStay2D(Collider2D other)  { TryConsume(other); }

    private void TryConsume(Collider2D other)
    {
        if (consumed) return;
        if (!other.CompareTag("Player")) return;
        if (InGameManager.instance == null || InGameManager.instance.isGameover) return;

        consumed = true;

        // 체력 회복 (+15)
        InGameManager.instance.AddHp(healValue, false);

        // 회복 팝업(UI 이미지) 요청 (획득 위치에서 위로 튀고 페이드아웃)
        InGameManager.instance.ShowHealPopup(transform.position);

        // 선택 이펙트/사운드
        if (pickupFxPrefab)
        {
            var fx = Instantiate(pickupFxPrefab, transform.position, Quaternion.identity);
            Destroy(fx, 1.2f);
        }
        if (pickupSfx)
        {
            if (BgmManager.Instance) BgmManager.Instance.PlaySfx(pickupSfx, transform.position, sfxVolume);
            else AudioSource.PlayClipAtPoint(pickupSfx, transform.position, Mathf.Clamp01(sfxVolume));
        }

        // 풀/재사용을 위해 비활성
        gameObject.SetActive(false);
    }
}
