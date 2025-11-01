using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CoinPickup : MonoBehaviour
{
    [Header("Coin")]
    [SerializeField] private int coinValue = 1;            // 1 또는 10
    [SerializeField] private int scorePerCoin = 10;        // 1코인=10, 10코인=100 권장

    [Header("FX")]
    [SerializeField] private GameObject pickupFxPrefab;    // 코인 획득 이펙트 프리팹(스프라이트 애니메이션)
    [SerializeField] private Vector2 fxOffset = new Vector2(-0.5f, 0f); // 이펙트 위치 보정(요청사항: x -0.5)
    [SerializeField] private AudioClip pickupSfx;          // 코인 획득 사운드
    [SerializeField, Range(0f,1f)] private float sfxVolume = 1f;

    private bool consumed = false; // 중복 처리 방지
    private Rigidbody2D rb2d;      // 접촉 안정화용

    [SerializeField] private float magnetRadius = 6.0f;   // 끌림 시작 반경
    [SerializeField] private float magnetSpeed  = 14.0f;  // 끌림 속도(기본)
    [SerializeField] private float magnetAccel  = 22.0f;  // 근접 가속(가까울수록 좀 더 빠르게)

    private Vector3 _spawnLocalPos;
    private bool _spawnPosCaptured = false;

    private void Awake()
    {
        // 트리거 권장
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        // 접촉 안정화: 젤리와 동일하게 처리
        rb2d = GetComponent<Rigidbody2D>();
        if (rb2d == null) rb2d = gameObject.AddComponent<Rigidbody2D>();
        rb2d.isKinematic = true;
        rb2d.useFullKinematicContacts = true;
        rb2d.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        if (InGameManager.instance == null || InGameManager.instance.isGameover) return;
        if (!InGameManager.instance.MagnetActive) return;

        var player = InGameManager.instance.PlayerTransform;
        if (!player) return;

        // ★ 플레이어 "콜라이더 중심"으로 끌림 (오프셋 제거)
        Vector3 target = InGameManager.instance.PlayerMagnetCenter;
        Vector3 to = target - transform.position;
        float dist = to.magnitude;
        if (dist > magnetRadius) return;

        float t = Mathf.InverseLerp(magnetRadius, 0f, dist);
        float speed = magnetSpeed + magnetAccel * t;

        transform.position += to.normalized * speed * Time.deltaTime;
    }


    private void OnEnable()
    {
        // ★ 최초 한 번만 원본 로컬 좌표 캡처
        if (!_spawnPosCaptured)
        {
            _spawnLocalPos = transform.localPosition;
            _spawnPosCaptured = true;
        }
        // ★ 재활용 시 항상 원래 자리로 복귀
        transform.localPosition = _spawnLocalPos;

        // 재활용 시 다시 먹히도록 상태 리셋
        consumed = false;
    }

    private void OnTriggerEnter2D(Collider2D other) { TryConsume(other); }
    private void OnTriggerStay2D(Collider2D other)  { TryConsume(other); }

    private void TryConsume(Collider2D other)
    {
        if (consumed) return;
        if (InGameManager.instance == null) return;
        if (InGameManager.instance.isGameover) return;
        if (!other.CompareTag("Player")) return;

        consumed = true;

        // 점수 가산
        InGameManager.instance.AddScore(scorePerCoin);

        // 코인 가산
        InGameManager.instance.AddRunCoins(coinValue);

        // 이펙트
        if (pickupFxPrefab != null)
        {
            Vector3 pos = transform.position + (Vector3)fxOffset;
            var fx = Instantiate(pickupFxPrefab, pos, Quaternion.identity);
            float life = GetAnimLengthOrDefault(fx, 1.0f);
            Destroy(fx, life + 0.05f);
        }

        // SFX
        if (pickupSfx != null)
        {
            if (BgmManager.Instance != null)
            {
                // 코인 전용: 항상 2겹 레이어로 재생 → 다른 SFX보다 체감 2배
                BgmManager.Instance.PlaySfxCoin(pickupSfx, transform.position);
            }
            else
            {
                // 매니저가 없을 때도 비슷한 효과 유지(폴백): 로컬에서 2번 재생
                AudioSource.PlayClipAtPoint(pickupSfx, transform.position, 1f);
                AudioSource.PlayClipAtPoint(pickupSfx, transform.position, 1f);
            }
        }

        // 오브젝트 풀링을 위한 비활성화
        gameObject.SetActive(false);
    }

    // 이펙트 프리팹의 Animator에서 가장 긴 클립 길이를 읽어 수명으로 사용
    private float GetAnimLengthOrDefault(GameObject go, float fallback)
    {
        var animator = go.GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            var clips = animator.runtimeAnimatorController.animationClips;
            if (clips != null && clips.Length > 0)
            {
                float max = 0f;
                foreach (var c in clips) if (c != null && c.length > max) max = c.length;
                if (max > 0f) return max;
            }
        }
        return fallback;
    }

    // 에디터에서 10코인으로 바꿀 경우 점수 비율도 맞춰주기 편의성
    private void OnValidate()
    {
        if (coinValue == 10 && scorePerCoin < 100) scorePerCoin = 100;
        if (coinValue == 1  && scorePerCoin > 10)  scorePerCoin = 10;
        if (coinValue != 1 && coinValue != 10) coinValue = 1;
    }
}
