using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class JellyPickup : MonoBehaviour
{
    [SerializeField] private GameObject pickupFxPrefab; // 젤리 획득 이펙트 프리팹 스프라이트 애니메이션
    [SerializeField] private AudioClip pickupSfx;       // 젤리 획득 사운드
    [SerializeField, Range(0f,1f)] private float sfxVolume = 1f; // 사운드 볼륨
    [SerializeField] private int scoreValue = 1;        // 젤리 점수

    private bool consumed = false; // 중복 처리 방지
    private Rigidbody2D rb2d;      // 접촉 안정화용

    [SerializeField] private float magnetRadius = 6.0f;
    [SerializeField] private float magnetSpeed = 14.0f;
    [SerializeField] private float magnetAccel = 22.0f;

    private Vector3 _spawnLocalPos;
    private bool _spawnPosCaptured = false;

    private void Awake()
    {
        // 트리거 권장
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        // 접촉 안정화
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

        // 점수 추가
        InGameManager.instance.AddScore(scoreValue);

        // 이펙트 생성 위치 x 를 0.5 왼쪽으로 오프셋
        if (pickupFxPrefab != null)
        {
            var pos = transform.position - new Vector3(0.5f, 0f, 0f);
            var fx = Instantiate(pickupFxPrefab, pos, Quaternion.identity);
            float life = GetAnimLengthOrDefault(fx, 1.0f);
            Destroy(fx, life + 0.05f);
        }

        // 사운드 재생
        if (pickupSfx != null)
        {
            if (BgmManager.Instance != null) BgmManager.Instance.PlaySfx(pickupSfx, transform.position, sfxVolume);
            else AudioSource.PlayClipAtPoint(pickupSfx, transform.position, Mathf.Clamp01(sfxVolume));
        }

        // 젤리 비활성화 오브젝트 풀 재사용
        gameObject.SetActive(false);
    }

    // 이펙트 프리팹에 Animator가 있으면 클립 길이를 얻어온다
    private float GetAnimLengthOrDefault(GameObject go, float fallback)
    {
        var animator = go.GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            var clips = animator.runtimeAnimatorController.animationClips;
            if (clips != null && clips.Length > 0)
            {
                float max = 0f;
                for (int i = 0; i < clips.Length; i++)
                    if (clips[i] != null && clips[i].length > max) max = clips[i].length;
                if (max > 0f) return max;
            }
        }
        return fallback;
    }
}
