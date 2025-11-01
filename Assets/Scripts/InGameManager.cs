using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 게임 매니저
public class InGameManager : MonoBehaviour
{
    public static InGameManager instance;

    [Header("Runtime State")]
    public bool isGameover = false;
    private bool resultShown = false;
    private int score = 0;
    private int runCoins = 0;
    public bool isPaused { get; private set; }
    private float prevTimeScale = 1f;

    [Header("HUD")]
    [SerializeField] private Text scoreText;
    [SerializeField] private Text runCoinsText;

    [Header("HP")]
    // ★ hpMax는 선택 캐릭터에 따라 바뀜(아래 Awake에서 설정)
    [SerializeField] private int hpMax = 100;
    [SerializeField] private Slider hpSlider;
    // ★ 체력바(가로 길이 조절 대상). hpSlider의 Fill이 아니라 외곽 바 Rect를 추천
    [SerializeField] private RectTransform hpBarRect;

    // (이펙트) 색상 페이드용
    [SerializeField] private Image hpTickFx;          // 체력 감소 이펙트
    private Color hpFxBaseColor;                      // 원래 색
    private static readonly Color hpFxHitColor = new Color(1f, 0.9490196f, 0.572549f); // #FFF292
    private float hpFxTimer;

    [SerializeField] private Image lowHpPulse;        // 크기 펄스(스케일)
    [SerializeField] private Image dangerVignette;    // 충돌 경고(빨간 패널)
    [SerializeField] private Image lowHpBlinkPanel;   // 저체력 경고 패널(핑퐁 페이드)
    [SerializeField] private float lowHpThreshold = 0.2f;

    private int hp;
    private float hpTickTimer;
    private bool timeZeroPending;
    private bool invincible;
    private Coroutine invincibleCo;

    // 충돌 경고 중복 방지
    private Coroutine dangerCo;

    // 저체력 파형/깜빡임 타이머
    private float lowHpPhaseTime = 0f;   // 스케일(0→최대→즉시0)
    private float lowHpBlinkTime = 0f;   // 패널 페이드

    [Header("End Panel Roots")]
    [SerializeField] private GameObject endPanelRoot;
    [SerializeField] private RectTransform endTitle;
    [SerializeField] private RectTransform scoreGroup;
    [SerializeField] private Text endScoreText;
    [SerializeField] private RectTransform newRecordIcon;
    [SerializeField] private RectTransform coinGroup;
    [SerializeField] private Text endCoinText;
    [SerializeField] private Button checkButton;

    [Header("Result FX")]
    [SerializeField] private Transform resultFxSpin;
    [SerializeField] private float resultFxSpinSpeed = 30f;

    [Header("Player Root")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private int minId = 1;
    [SerializeField] private int maxId = 4;

    [Header("Result Timing and Audio")]
    [SerializeField] private float deathShowSeconds = 3.0f; // 사망 연출 시청 후 바로 결과
    [SerializeField] private AudioClip resultOpenSfx;
    [SerializeField] private AudioClip uiClickSfx;
    [SerializeField, Range(0f, 1f)] private float resultSfxVolumeMul = 0.5f;

    [Header("Difficulty Ramp")]
    [SerializeField] private float rampDuration = 60f;
    private float elapsed;
    public float Progress01 => Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, rampDuration));

    // 저장 키
    private const string PREF_SELECTED = "SelectedCharacter";
    private const string PREF_BEST     = "BestScore";
    private const string PREF_COINS    = "Player_Coins";

    private GameObject[] players;
    
    public bool IsResultOpen => resultShown || (endPanelRoot != null && endPanelRoot.activeSelf);

    [SerializeField] private GameObject reviveFxPrefab;   // 부활 이펙트(선택)
    [SerializeField] private AudioClip reviveSfx;         // 부활 사운드(선택)
    [SerializeField, Range(0f, 1f)] private float reviveSfxVol = 0.9f;
    
    [Header("Heal Popup")]
    [SerializeField] private GameObject healPopupPrefab; // 기본 비활성화된 프리팹(필수)
    [SerializeField] private float healPopupRise = 1.0f;  // 위로 튀는 높이
    [SerializeField] private float healPopupShow = 0.5f;  // 보여주는 시간(초)
    [SerializeField] private float healPopupFade = 0.25f; // 페이드아웃 시간(초)

    private int selectedId = 1;
    private bool reviveUsed = false;
    private bool fallDeathLock = false; // 낙사 시 부활 금지

    // 공개 프로퍼티
    public int SelectedId => selectedId;
    public bool MagnetActive => selectedId == 3;

    // 활성 플레이어 Transform 캐시
    private Transform activePlayer;

    // 외부 접근용: 활성 플레이어 Transform (없으면 한 번만 찾아서 캐시)
    public Transform PlayerTransform
    {
        get
        {
            if (activePlayer == null)
            {
                var pc = FindObjectOfType<PlayerController>();
                if (pc != null) activePlayer = pc.transform;
            }
            return activePlayer;
        }
    }

    public Vector3 PlayerMagnetCenter
    {
        get
        {
            var t = PlayerTransform;
            if (!t) return Vector3.zero;
            var pc = t.GetComponent<PlayerController>();
            if (pc != null && pc.MainCollider != null)
                return pc.MainCollider.bounds.center; // ★ 콜라이더 중심 (월드)
            return t.position;
        }
    }

    [SerializeField] private GameObject reviveFxObject;   // ✅ 부활 이펙트 오브젝트(비활성 상태로 씬에 존재)
    [SerializeField] private float reviveDeathAnimSeconds = 1.2f; // 사망 모션 시청 시간(unscaled)
    [SerializeField] private float reviveFxSeconds       = 1.0f;  // 이펙트 재생 시간(unscaled)

    // 내부 상태
    private bool reviveAwaitGround = false;   // ✅ 착지 대기 중 부활
    private bool reviveWasCrashPending = false;

    // PlayerController가 Start()에서 자기 자신을 등록
    public void RegisterPlayer(PlayerController pc)
    {
        activePlayer = pc ? pc.transform : null;
    }

    private bool isRevivingFlow = false;    // 부활 전체 흐름 동안 입력 차단
    public bool IsReviving => isRevivingFlow;

    private void Awake()
    {
        if (instance == null) instance = this; else Debug.LogWarning("두개의 InGameManager 가 존재");
        Time.timeScale = 1f;

        if (endPanelRoot) endPanelRoot.SetActive(false);
        if (checkButton) checkButton.gameObject.SetActive(false);
        if (newRecordIcon) newRecordIcon.gameObject.SetActive(false);

        // ★ 선택 캐릭터/HP 세팅 (한 번만)
        selectedId = Mathf.Clamp(PlayerPrefs.GetInt(PREF_SELECTED, 1), minId, maxId);
        hpMax = GetHpByCharacter(selectedId);  // 1=50, 2=60, 3=70, 4=70

        // ★ 체력바 길이 = 체력*10
        if (hpBarRect != null)
            hpBarRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, hpMax * 10f);

        // HP 초기화 + 슬라이더 세팅
        hp = hpMax;
        if (hpSlider)
        {
            hpSlider.minValue = 0;
            hpSlider.maxValue = hpMax;
            hpSlider.value = hp;
        }

        // 이펙트 초기 상태
        if (dangerVignette) dangerVignette.canvasRenderer.SetAlpha(0f);
        if (lowHpPulse) lowHpPulse.transform.localScale = Vector3.zero;
        if (lowHpBlinkPanel) SetImageAlpha01(lowHpBlinkPanel, 0f);

        // 체력바 이펙트 기본 색 기억
        if (hpTickFx) hpFxBaseColor = hpTickFx.color;

        lowHpPhaseTime = 0f;
        lowHpBlinkTime = 0f;

        reviveUsed = false;
        fallDeathLock = false;

        // ★ 부활 이펙트 오브젝트를 항상 꺼두고 시작
        if (reviveFxObject) reviveFxObject.SetActive(false);
    }

    private void Start()
    {
        if (BgmManager.Instance != null) BgmManager.Instance.PlayGameBgm();

        runCoins = 0;
        RefreshRunCoinsUI();
        RefreshScoreUI();

        CachePlayers();
        ActivateSelected();
    }

    private void Update()
    {
        if (!isGameover) elapsed += Time.deltaTime;

        // 자연 감소: 1초마다 1 (요구사항: 유지)
        if (!isGameover && !isPaused)
        {
            hpTickTimer += Time.deltaTime;
            if (hpTickTimer >= 1f)
            {
                hpTickTimer -= 1f;
                AddHp(-1, false);
            }
        }

        if (hpSlider) hpSlider.value = hp;

        // ★ 체력바 이펙트: 알파 깜빡임 → "색상" 페이드 왕복 (기본색 ↔ #FFF292)
        if (hpTickFx)
        {
            const float cycle = 0.10f;                 // 기존 속도 유지(빠른 반짝임)
            hpFxTimer += Time.unscaledDeltaTime;
            if (hpFxTimer >= cycle) hpFxTimer -= cycle;

            float t = Mathf.PingPong(hpFxTimer / (cycle * 0.5f), 1f); // 0→1→0
            hpTickFx.color = Color.Lerp(hpFxBaseColor, hpFxHitColor, t);
        }

        // 저체력 경고(스케일+패널 페이드)는 기존 로직 유지
        float hpRate = Mathf.Clamp01(hpMax > 0 ? (float)hp / hpMax : 0f);
        if (hpRate <= lowHpThreshold && !isGameover)
        {
            // 펄스 최대 스케일: 20%에서 1.75, 0%에서 2.0
            float tMax = Mathf.Clamp01(hpRate / Mathf.Max(0.0001f, lowHpThreshold));
            float maxScale = Mathf.Lerp(2.0f, 1.75f, tMax);

            // saw: 0→max→0
            float dur = 0.9f;
            if (lowHpPulse)
            {
                lowHpPhaseTime += Time.unscaledDeltaTime;
                if (lowHpPhaseTime >= dur) lowHpPhaseTime -= dur;
                float u = Mathf.Clamp01(lowHpPhaseTime / dur);
                float scale = Mathf.Lerp(0f, maxScale, u);
                lowHpPulse.transform.localScale = new Vector3(scale, scale, 1f);
            }

            // 패널: 0→1→0 핑퐁 페이드(천천히)
            if (lowHpBlinkPanel)
            {
                const float blinkPeriod = 1.8f;
                lowHpBlinkTime += Time.unscaledDeltaTime;
                float u = Mathf.PingPong(lowHpBlinkTime / (blinkPeriod * 0.5f), 1f);
                SetImageAlpha01(lowHpBlinkPanel, u);
            }
        }
        else
        {
            if (lowHpPulse) lowHpPulse.transform.localScale = Vector3.zero;
            if (lowHpBlinkPanel) SetImageAlpha01(lowHpBlinkPanel, 0f);
            lowHpPhaseTime = 0f;
            lowHpBlinkTime = 0f;
        }

        if (resultShown && resultFxSpin != null)
            resultFxSpin.Rotate(0f, 0f, resultFxSpinSpeed * Time.unscaledDeltaTime);

        if (!isGameover && timeZeroPending)
        {
            var pc = FindObjectOfType<PlayerController>();
            if (pc && pc.IsGrounded)
            {
                timeZeroPending = false;
                pc.PlayTimeDeath();
            }
        }

        if (!isGameover && reviveAwaitGround)
        {
            var pc = FindObjectOfType<PlayerController>();
            if (pc && pc.IsGrounded)
            {
                reviveAwaitGround = false;
                StartCoroutine(CoRevive(reviveWasCrashPending));
            }
        }
    }

    public void AddScore(int add)
    {
        if (isGameover) return;
        score += add;
        RefreshScoreUI();
    }

    public void AddRunCoins(int amount)
    {
        if (isGameover) return;
        runCoins += Mathf.Max(0, amount);
        RefreshRunCoinsUI();
    }

    private void RefreshScoreUI()   { if (scoreText)    scoreText.text    = score.ToString("#,0"); }
    private void RefreshRunCoinsUI(){ if (runCoinsText) runCoinsText.text = runCoins.ToString("#,0"); }

    // 저체력 UI 즉시 정지
    private void StopLowHpUI()
    {
        if (lowHpPulse) lowHpPulse.transform.localScale = Vector3.zero;
        if (lowHpBlinkPanel) SetImageAlpha01(lowHpBlinkPanel, 0f);
        lowHpPhaseTime = 0f;
        lowHpBlinkTime = 0f;
    }

    // 체력 증감
    public void AddHp(int delta, bool isCrash)
    {
        if (isGameover) return;
        if (isCrash && invincible) return;

        int prev = hp;
        int candidate = hp + delta;

        // ── 충돌로 즉시 0 이하 ─────────────────────────────
        if (isCrash && candidate <= 0)
        {
            // 캐릭터4 부활 가능? (낙사 잠금 X + 아직 미사용)
            if (selectedId == 4 && !reviveUsed && !fallDeathLock)
            {
                hp = 0;               // 체력 0으로 확정
                StopLowHpUI();
                QueueRevive(true);    // ✅ 착지 후 부활
                return;
            }

            // 부활 불가 → 즉시 사망
            hp = 0;
            StopLowHpUI();
            var pc0 = FindObjectOfType<PlayerController>();
            pc0?.PlayCrashDeath();
            return;
        }

        // ── 여기까지 왔으면 생존 갱신 ──────────────────────
        hp = Mathf.Clamp(candidate, 0, hpMax);

        if (isCrash)
        {
            var pc = FindObjectOfType<PlayerController>();
            pc?.PlayCrash();
            StartInvincible(3f);

            if (dangerCo != null) StopCoroutine(dangerCo);
            dangerCo = StartCoroutine(FlashDangerSingle(0.2f));
        }

        // 0 도달(시간경과/기타) → 부활 체크
        if (hp == 0 && prev > 0)
        {
            if (selectedId == 4 && !reviveUsed && !fallDeathLock)
            {
                StopLowHpUI();
                QueueRevive(false);   // ✅ 착지 후 부활
                return;
            }

            var pc = FindObjectOfType<PlayerController>();
            bool grounded = pc ? pc.IsGrounded : true;
            if (!isCrash)
            {
                if (!grounded) timeZeroPending = true;
                else pc?.PlayTimeDeath();
            }
            else pc?.PlayCrashDeath();
        }
    }

    private void QueueRevive(bool wasCrash)
    {
        // ✅ 착지 대기 포함 '부활 중'으로 간주하여 입력 차단 시작
        isRevivingFlow = true;

        var pc = FindObjectOfType<PlayerController>();
        if (pc && pc.IsGrounded)
        {
            StartCoroutine(CoRevive(wasCrash));
        }
        else
        {
            reviveAwaitGround = true;
            reviveWasCrashPending = wasCrash;
        }
    }

    private IEnumerator CoRevive(bool wasCrash)
    {
        isRevivingFlow = true;
        reviveUsed = true;

        // 1) 전체 정지
        SetPaused(true);
        if (BgmManager.Instance) BgmManager.Instance.PauseBgm(withFade: false, fadeSeconds: 0f);

        // 2) 플레이어 애니메이터를 Unscaled 로 돌려 사망 모션만 재생
        var pc = FindObjectOfType<PlayerController>();
        Animator anim = pc ? pc.GetComponent<Animator>() : null;
        AnimatorUpdateMode prevMode = anim ? anim.updateMode : AnimatorUpdateMode.Normal;
        if (anim) anim.updateMode = AnimatorUpdateMode.UnscaledTime;

        // 사망 트리거만 쏨 (OnPlayerDead() 호출되는 PlayCrashDeath/PlayTimeDeath는 쓰지 않음)
        if (anim)
        {
            if (wasCrash) anim.SetTrigger("CrashDie");
            else anim.SetTrigger("Die");
        }

        // (옵션) 충돌 사망음만 살짝 재생하고 싶다면: pc.deathClip 사용
        if (wasCrash && pc && pc.deathClip)
            AudioSource.PlayClipAtPoint(pc.deathClip, Camera.main ? Camera.main.transform.position : Vector3.zero,
                BgmManager.Instance ? BgmManager.Instance.EffectiveSfxVolume : 1f);

        // 3) 사망 모션 시청 대기 (TimeScale=0이므로 Unscaled 대기)
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, reviveDeathAnimSeconds));

        // 4) 부활 이펙트/사운드 (Unscaled)
        // 위치 정렬(원하면 오프셋 추가)
        var pTr = PlayerTransform;
        if (reviveFxObject && pTr) reviveFxObject.transform.position = pTr.position;

        // 사운드
        if (reviveSfx)
        {
            if (BgmManager.Instance) BgmManager.Instance.PlayUiSfx(reviveSfx, reviveSfxVol);
            else AudioSource.PlayClipAtPoint(reviveSfx, Camera.main ? Camera.main.transform.position : Vector3.zero, reviveSfxVol);
        }

        float waitSec = reviveFxSeconds;

        if (reviveFxObject)
        {
            // Animator를 Unscaled 로, 0프레임부터 재생되도록 준비
            var fxAnim = reviveFxObject.GetComponent<Animator>();
            AnimatorUpdateMode prevFxMode = AnimatorUpdateMode.Normal;

            if (fxAnim)
            {
                prevFxMode = fxAnim.updateMode;
                fxAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
                fxAnim.Rebind();
                fxAnim.Update(0f);

                // 길이 자동 추정(가장 긴 클립)
                if (fxAnim.runtimeAnimatorController != null)
                {
                    var clips = fxAnim.runtimeAnimatorController.animationClips;
                    if (clips != null && clips.Length > 0)
                    {
                        float len = 0f;
                        foreach (var c in clips) if (c && c.length > len) len = c.length;
                        if (len > 0f) waitSec = len;
                    }
                }
            }

            // ✅ 이펙트 오브젝트 활성화 → 애니 재생 시작
            reviveFxObject.SetActive(false); // 혹시 켜져 있었다면 재시작 보장
            reviveFxObject.SetActive(true);

            // 끝까지 대기 (Unscaled)
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, waitSec));

            // 비활성 & 모드 복구
            reviveFxObject.SetActive(false);
            if (fxAnim) fxAnim.updateMode = prevFxMode;
        }
        else
        {
            // 오브젝트가 없으면 설정값만큼 대기
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, waitSec));
        }

        // 5) 실제 부활: HP 20, 무적 부여
        hp = Mathf.Clamp(20, 1, hpMax);
        if (hpSlider) hpSlider.value = hp;
        StartInvincible(2.5f);
        timeZeroPending = false; // 혹시 예약된 시간사망 대기 해제

        if (pc) pc.RestoreAfterRevive();

        // 애니메이터 모드 복구
        if (anim) anim.updateMode = prevMode;

        // 6) 재개
        isRevivingFlow = false;
        SetPaused(false);
        if (BgmManager.Instance) BgmManager.Instance.ResumeBgm(withFade: false, fadeSeconds: 0f);
    }

    public void OnPlayerDead()
    {
        if (isGameover) return;
        isGameover = true;
        isRevivingFlow = false;
    

        StopLowHpUI();

        if (BgmManager.Instance != null)
            BgmManager.Instance.PauseBgm(withFade: false, fadeSeconds: 0f);

        StartCoroutine(CoGameOverFlow());
    }

    public void FallDeath()
    {
        if (isGameover) return;
        fallDeathLock = true;   // 낙사 시 부활 금지
        isRevivingFlow = false;
        hp = 0;
        StopLowHpUI();

        // 즉시 충돌 사망 연출로 통일(별도 애니 없으면 CrashDie 사용)
        var pc = FindObjectOfType<PlayerController>();
        pc?.PlayCrashDeath();
    }

    private IEnumerator CoGameOverFlow()
    {
        float t = 0f;
        while (t < deathShowSeconds) { t += Time.deltaTime; yield return null; }
        ShowResultPanel();
    }

    public void ShowResultPanel()
    {
        if (resultShown) return;
        resultShown = true;

        if (resultOpenSfx != null)
            BgmManager.Instance?.PlayUiSfx(resultOpenSfx, resultSfxVolumeMul);

        if (endPanelRoot) endPanelRoot.SetActive(true);
        if (endTitle) StartCoroutine(CoScaleIn(endTitle, 0.18f));

        int prevBest = PlayerPrefs.GetInt(PREF_BEST, 0);
        bool isNewRecord = score > prevBest;
        if (isNewRecord) PlayerPrefs.SetInt(PREF_BEST, score);

        int have = PlayerPrefs.GetInt(PREF_COINS, 0);
        PlayerPrefs.SetInt(PREF_COINS, have + runCoins);
        PlayerPrefs.Save();

        StartCoroutine(CoFillNumber(
            scoreGroup, endScoreText, 0, score, 0.9f, () =>
            {
                if (isNewRecord)
                {
                    var scoreRT = endScoreText ? endScoreText.rectTransform : null;
                    if (scoreRT != null)
                    {
                        var p = scoreRT.anchoredPosition; p.x += 70f; scoreRT.anchoredPosition = p;
                    }
                    if (newRecordIcon)
                    {
                        newRecordIcon.gameObject.SetActive(true);
                        StartCoroutine(CoScaleIn(newRecordIcon, 0.18f));
                    }
                }

                StartCoroutine(CoFillNumber(
                    coinGroup, endCoinText, 0, runCoins, 0.9f, () =>
                    {
                        if (checkButton)
                        {
                            checkButton.gameObject.SetActive(true);
                            StartCoroutine(CoScaleIn((RectTransform)checkButton.transform, 0.18f));
                            checkButton.onClick.RemoveAllListeners();
                            checkButton.onClick.AddListener(() =>
                            {
                                if (uiClickSfx != null)
                                {
                                    if (BgmManager.Instance != null) BgmManager.Instance.PlayUiSfx(uiClickSfx);
                                    else AudioSource.PlayClipAtPoint(uiClickSfx, Camera.main ? Camera.main.transform.position : Vector3.zero);
                                }
                                GoToLobby();
                            });
                        }
                    }));
            }));

        SetPaused(true);
    }

    private IEnumerator CoFillNumber(RectTransform group, Text valueText, int from, int to, float seconds, System.Action onDone)
    {
        if (group) group.localScale = Vector3.one;
        if (valueText) valueText.text = from.ToString("#,0");

        float t = 0f; bool skipped = false;
        while (t < seconds && !skipped)
        {
            t += Time.unscaledDeltaTime;
            int v = Mathf.RoundToInt(Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds)));
            if (valueText) valueText.text = v.ToString("#,0");
            if (Input.GetMouseButtonDown(0) || Input.touchCount > 0) skipped = true;
            yield return null;
        }
        if (valueText) valueText.text = to.ToString("#,0");
        onDone?.Invoke();
    }

    private IEnumerator CoScaleIn(RectTransform rt, float seconds)
    {
        if (!rt) yield break;
        Vector3 start = Vector3.zero, end = Vector3.one;
        rt.localScale = start;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(start, end, t / seconds);
            yield return null;
        }
        rt.localScale = end;
    }

    private IEnumerator FlashDangerSingle(float seconds)
    {
        if (!dangerVignette) yield break;
        dangerVignette.canvasRenderer.SetAlpha(1f);
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        dangerVignette.canvasRenderer.SetAlpha(0f);
        dangerCo = null;
    }

    private void StartInvincible(float seconds)
    {
        if (invincibleCo != null) StopCoroutine(invincibleCo);
        invincibleCo = StartCoroutine(CoInvincible(seconds));
    }

    private IEnumerator CoInvincible(float seconds)
    {
        invincible = true;

        var pc = FindObjectOfType<PlayerController>();
        SpriteRenderer[] rends = pc ? pc.GetSpriteRenderers() : null;

        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(0.5f, 0.8f, Mathf.PingPong(t * 16f, 1f));
            if (rends != null)
            {
                for (int i = 0; i < rends.Length; i++)
                {
                    if (!rends[i]) continue;
                    var c = rends[i].color; c.a = a; rends[i].color = c;
                }
            }
            yield return null;
        }

        if (rends != null)
        {
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i]) continue;
                var c = rends[i].color; c.a = 1f; rends[i].color = c;
            }
        }

        invincible = false;
        invincibleCo = null;
    }

    private void GoToLobby()
    {
        if (BgmManager.Instance != null) BgmManager.Instance.PlayTitleBgm();
        SetPaused(false);
        SceneManager.LoadScene("Title");
    }

    public void SetPaused(bool pause)
    {
        if (isPaused == pause) return;
        isPaused = pause;
        if (pause) { prevTimeScale = Time.timeScale; Time.timeScale = 0f; }
        else       { Time.timeScale = (prevTimeScale <= 0f) ? 1f : prevTimeScale; }
    }

    private void CachePlayers()
    {
        if (playerRoot == null)
        {
            var rootGo = GameObject.Find("Player");
            if (rootGo) playerRoot = rootGo.transform;
        }
        players = new GameObject[maxId + 1];
        for (int i = minId; i <= maxId; i++)
        {
            Transform child = null;
            if (playerRoot) child = playerRoot.Find("Character (" + i + ")");
            if (child == null)
            {
                var go = GameObject.Find("Character (" + i + ")");
                if (go) child = go.transform;
            }
            players[i] = child ? child.gameObject : null;
        }
    }

    private void ActivateSelected()
    {
        int id = Mathf.Clamp(PlayerPrefs.GetInt(PREF_SELECTED, 1), minId, maxId);
        for (int i = minId; i <= maxId; i++)
            if (players[i]) players[i].SetActive(i == id);

        // ★ 추가: 활성 자식의 PlayerController/Transform 캐시
        if (players[id] != null)
        {
            var pc = players[id].GetComponentInChildren<PlayerController>(true);
            activePlayer = pc ? pc.transform : players[id].transform;
        }
    }

    // ★ 캐릭터별 HP 매핑
    private int GetHpByCharacter(int id)
    {
        switch (id)
        {
            case 2: return 60;
            case 3: return 70;
            case 4: return 70;
            default: return 50;
        }
    }

    // 공용 유틸
    private void SetImageAlpha01(Image img, float a01)
    {
        var c = img.color; c.a = Mathf.Clamp01(a01); img.color = c;
    }

    // 월드 좌표에서 회복 팝업을 띄우는 공용 API
    public void ShowHealPopup(Vector3 worldPos)
    {
        if (!healPopupPrefab) return;

        // 인스턴스 생성(월드에 두면 카메라 따라 움직이지 않음 / UI로 쓰고 싶으면 Canvas 하위로 붙여도 OK)
        var go = Instantiate(healPopupPrefab, worldPos, Quaternion.identity);

        // 스프라이트/이미지 모두 지원: Alpha 제어를 위해 공통적으로 색상 접근
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        var img = go.GetComponentInChildren<UnityEngine.UI.Image>();

        // 시작 상태: 약간 아래에서 시작 → 위로 상승하며 나타나기
        Vector3 start = worldPos;
        Vector3 end = worldPos + Vector3.up * healPopupRise;

        StartCoroutine(CoHealPopup(go, sr, img, start, end));
    }

    private System.Collections.IEnumerator CoHealPopup(GameObject go, SpriteRenderer sr, UnityEngine.UI.Image img, Vector3 start, Vector3 end)
    {
        // 등장(즉시 보이기)
        float t = 0f;
        float show = Mathf.Max(0.01f, healPopupShow);
        float fade = Mathf.Max(0.01f, healPopupFade);

        // 살짝 위로 튀는 이동 + 보여주기 구간
        while (t < show)
        {
            t += Time.unscaledDeltaTime; // 일시정지에도 UI는 자연스럽게
            float u = Mathf.Clamp01(t / show);
            go.transform.position = Vector3.Lerp(start, end, u);
            SetAlpha(sr, img, 1f);
            yield return null;
        }

        // 페이드아웃 구간
        t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / fade);
            SetAlpha(sr, img, a);
            yield return null;
        }

        Destroy(go);
    }

    private void SetAlpha(SpriteRenderer sr, UnityEngine.UI.Image img, float a)
    {
        if (sr)
        {
            var c = sr.color; c.a = a; sr.color = c;
        }
        if (img)
        {
            var c = img.color; c.a = a; img.color = c;
        }
    }
}
