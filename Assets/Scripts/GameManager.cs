using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 게임 전체 상태와 점수 기록 방어구 언락 알림 등을 관리
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI")]
    public Text scoreText;   // 점수 표시
    public Text recordText;  // 최고점 표시
    public Text gameOverText; // 게임오버 텍스트

    [Header("Players in Scene")]
    [SerializeField] private Transform playerRoot; // 플레이어 루트 트랜스폼
    [SerializeField] private int minSkinId = 1;    // 스킨 아이디 최소값
    [SerializeField] private int maxSkinId = 5;    // 스킨 아이디 최대값

    [Header("Audio")]
    [SerializeField] private AudioClip armorBreakSfx;   // 방어구 파괴 사운드
    [SerializeField] private AudioClip armorPickupSfx;  // 방어구 획득 사운드
    [SerializeField] private AudioSource sfxAudioSource; // SFX 재생 오디오소스

    public bool isGameOver;   // 게임오버 상태

    public float score;       // 현재 점수
    public float record;      // 최고 점수

    private PlayerController playerController;  // 활성 플레이어 컨트롤러
    private GameObject[] playerSkins;           // 스킨 오브젝트 캐시
    private int selectedSkinId;                 // 선택된 스킨 아이디

    private const string PREF_SELECTED_SKIN = "SelectedSkin";  // 스킨 선택 키
    private const string PREF_BEST_SCORE = "Player_Record";    // 최고점 키

    public const string PREF_UNLOCK_FMT = "Skin_{0}_IsUnlock"; // 스킨 언락 키 포맷
    private const string PREF_JUST_FMT = "Skin_{0}_JustUnlocked"; // 사용하지 않는 잔존 키 포맷

    public bool isPaused { get; private set; } // 일시정지 상태
    private float prevTimeScale = 1f;          // 복원용 타임스케일

    private void Awake()
    {
        // 싱글톤 초기화
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad는 사용하지 않음

        // SFX 오디오소스 보장 및 2D 설정
        if (sfxAudioSource == null)
            sfxAudioSource = gameObject.AddComponent<AudioSource>();
        sfxAudioSource.playOnAwake = false;
        sfxAudioSource.spatialBlend = 0f;
        sfxAudioSource.dopplerLevel = 0f;
        sfxAudioSource.rolloffMode = AudioRolloffMode.Linear;
    }

    private void Start()
    {
        // 플레이어 캐시 및 선택 스킨 활성화
        CachePlayers();
        ActivateSelectedPlayer();

        // 초기 상태값
        isGameOver = false;
        score = 0f;

        // 최고점 로드 및 UI 반영
        record = PlayerPrefs.GetFloat(PREF_BEST_SCORE, 0f);
        UpdateScoreUI();
        UpdateRecordUI();

        // 게임오버 텍스트 비활성화
        if (gameOverText) gameOverText.gameObject.SetActive(false);
    }

    // 플레이어 오브젝트들을 찾고 캐시
    private void CachePlayers()
    {
        if (playerRoot == null)
        {
            var rootGo = GameObject.Find("Player");
            if (rootGo != null) playerRoot = rootGo.transform;
        }

        playerSkins = new GameObject[maxSkinId + 1];

        for (int id = minSkinId; id <= maxSkinId; id++)
        {
            GameObject go = null;

            if (playerRoot != null)
            {
                var child = playerRoot.Find($"Player ({id})");
                if (child != null) go = child.gameObject;
            }

            if (go == null)
            {
                var fallback = GameObject.Find($"Player ({id})");
                if (fallback != null) go = fallback;
            }

            playerSkins[id] = go;
        }
    }

    // 선택된 스킨을 활성화하고 관련 컴포넌트 설정
    private void ActivateSelectedPlayer()
    {
        selectedSkinId = Mathf.Clamp(PlayerPrefs.GetInt(PREF_SELECTED_SKIN, 1), minSkinId, maxSkinId);

        for (int id = minSkinId; id <= maxSkinId; id++)
        {
            if (playerSkins[id] != null) playerSkins[id].SetActive(false);
        }

        var activeGo = playerSkins[selectedSkinId];
        if (activeGo != null)
        {
            activeGo.SetActive(true);
            playerController = activeGo.GetComponent<PlayerController>();
        }
        else
        {
            Debug.LogWarning("[GameManager] Player (" + selectedSkinId + ") 오브젝트를 찾지 못했습니다.");
            playerController = FindFirstObjectByType<PlayerController>();
        }
    }

    // SFX 재생 헬퍼
    public void PlayOneShot(AudioClip clip, Vector3 at)
    {
        if (clip == null) return;
        float vol = BgmManager.Instance ? BgmManager.Instance.EffectiveSfxVolume : 1f;

        sfxAudioSource.volume = vol;
        sfxAudioSource.PlayOneShot(clip);
    }

    // 최고 기록 갱신 체크
    public void CheckRecord()
    {
        if (score > record)
        {
            PlayerPrefs.SetFloat(PREF_BEST_SCORE, score);
            PlayerPrefs.Save();

            record = PlayerPrefs.GetFloat(PREF_BEST_SCORE, 0f);
            UpdateRecordUI();
        }
    }

    // 점수 UI 갱신
    private void UpdateScoreUI()
    {
        if (scoreText) scoreText.text = "Score : " + score.ToString("0");
    }

    // 최고점 UI 갱신
    private void UpdateRecordUI()
    {
        if (recordText) recordText.text = "Best Score : " + record.ToString("0");
    }

    // 일시정지 설정
    public void SetPaused(bool pause)
    {
        if (isPaused == pause) return;
        isPaused = pause;

        if (pause)
        {
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = (prevTimeScale <= 0f) ? 1f : prevTimeScale;
        }
    }

    // 스킨 언락 여부 확인
    private bool IsUnlocked(int id)
    {
        string key = string.Format(PREF_UNLOCK_FMT, id);
        if (!PlayerPrefs.HasKey(key)) return false;
        string s = PlayerPrefs.GetString(key, "");
        if (!string.IsNullOrEmpty(s) && bool.TryParse(s, out bool b)) return b;
        return PlayerPrefs.GetInt(key, 0) != 0;
    }

    // 스킨 아이디를 이름으로 변환
    private string GetSkinNameById(int id)
    {
        return id switch
        {
            2 => "Leather Armor",
            3 => "Iron Armor",
            4 => "Alien",
            5 => "Bunny?",
            _ => "Skin " + id.ToString()
        };
    }
}
