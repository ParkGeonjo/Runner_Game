using UnityEngine;
using UnityEngine.UI;

// 로비 전용 개발자 패널 컨트롤러
public class DeveloperPanel : MonoBehaviour
{
    [Header("Open Close")]
    [SerializeField] private GameObject panelRoot;       // 개발자 패널 루트
    [SerializeField] private float holdSeconds = 5f;     // Z 키 홀드 시간

    [Header("UI Refs")]
    [SerializeField] private InputField addCoinsInput;   // 추가 코인 입력값
    [SerializeField] private Button addCoinsBt;          // 코인 추가 버튼
    [SerializeField] private Button resetCoinsBt;        // 코인 초기화 버튼
    [SerializeField] private Button resetUnlocksBt;      // 캐릭터 해금 초기화 버튼
    [SerializeField] private Button resetScoreBt;        // 점수 기록 초기화 버튼
    [SerializeField] private Button closeBt;             // 닫기 버튼
    [SerializeField] private Text panelCoinsText;        // 패널 내부 코인 표기
    [SerializeField] private Text lobbyCoinsText;        // 로비 화면 코인 표기

    // PlayerPrefs 키 상수
    private const string PREF_COINS_TITLE = "Player_Coins";  // TitleManager에서 사용
    private const string PREF_COINS_GAME  = "CoinsTotal";     // InGameManager에서 사용
    private const string PREF_SELECTED    = "SelectedCharacter";
    private const string PREF_BEST        = "BestScore";
    private const string PREF_UNLOCK_FMT  = "Character_{0}_IsUnlock";

    private float zHeld = 0f;              // Z 키 누른 시간 누적
    private bool panelOpen => panelRoot && panelRoot.activeSelf;

    private void Awake()
    {
        if (panelRoot) panelRoot.SetActive(false);

        if (addCoinsBt)      addCoinsBt.onClick.AddListener(AddCoins);
        if (resetCoinsBt)    resetCoinsBt.onClick.AddListener(ResetCoins);
        if (resetUnlocksBt)  resetUnlocksBt.onClick.AddListener(ResetUnlocks);
        if (resetScoreBt)    resetScoreBt.onClick.AddListener(ResetScore);
        if (closeBt)         closeBt.onClick.AddListener(ClosePanel);
    }

    private void Update()
    {
        // 패널이 열려 있을 때 Z 키로 닫기
        if (panelOpen)
        {
            if (Input.GetKeyDown(KeyCode.Z))
            {
                ClosePanel();
            }
            return;
        }

        // 패널이 닫혀 있을 때 Z 키를 holdSeconds 만큼 누르면 열기
        if (Input.GetKey(KeyCode.Z))
        {
            zHeld += Time.unscaledDeltaTime;
            if (zHeld >= holdSeconds)
            {
                OpenPanel();
                zHeld = 0f;
            }
        }
        else
        {
            zHeld = 0f;
        }
    }

    // 패널 열기
    private void OpenPanel()
    {
        if (!panelRoot) return;
        panelRoot.SetActive(true);
        RefreshCoinsUI();
    }

    // 패널 닫기
    private void ClosePanel()
    {
        if (!panelRoot) return;
        panelRoot.SetActive(false);
    }

    // 코인 추가
    private void AddCoins()
    {
        int add = 0;
        if (addCoinsInput && !string.IsNullOrEmpty(addCoinsInput.text))
            int.TryParse(addCoinsInput.text, out add);

        if (add <= 0) return;

        int coins = GetCoins();
        coins += add;
        SetCoins(coins);
        RefreshCoinsUI();
    }

    // 코인 초기화
    private void ResetCoins()
    {
        SetCoins(0);
        RefreshCoinsUI();
    }

    // 캐릭터 해금 초기화
    private void ResetUnlocks()
    {
        // 2 3 4를 잠금 false 로, 1은 true 로
        SetUnlock(1, true);
        SetUnlock(2, false);
        SetUnlock(3, false);
        SetUnlock(4, false);

        // 선택 캐릭터도 1로 되돌림
        PlayerPrefs.SetInt(PREF_SELECTED, 1);
        PlayerPrefs.Save();

        // 코인 라벨만 즉시 갱신
        RefreshCoinsUI();
    }

    // 점수 기록 초기화
    private void ResetScore()
    {
        PlayerPrefs.SetInt(PREF_BEST, 0);
        PlayerPrefs.Save();
    }

    // 현재 코인 가져오기  두 키 중 우선순위는 Title 키가 먼저
    private int GetCoins()
    {
        bool hasTitle = PlayerPrefs.HasKey(PREF_COINS_TITLE);
        bool hasGame  = PlayerPrefs.HasKey(PREF_COINS_GAME);

        if (hasTitle) return PlayerPrefs.GetInt(PREF_COINS_TITLE, 0);
        if (hasGame)  return PlayerPrefs.GetInt(PREF_COINS_GAME, 0);
        return 0;
    }

    // 코인 저장  두 키 모두 쓰기
    private void SetCoins(int value)
    {
        if (value < 0) value = 0;
        PlayerPrefs.SetInt(PREF_COINS_TITLE, value);
        PlayerPrefs.SetInt(PREF_COINS_GAME,  value);
        PlayerPrefs.Save();
    }

    // 캐릭터 해금 저장
    private void SetUnlock(int id, bool v)
    {
        string key = string.Format(PREF_UNLOCK_FMT, id);
        PlayerPrefs.SetString(key, v ? "true" : "false");
        PlayerPrefs.Save();
    }

    // 코인 라벨 즉시 갱신
    private void RefreshCoinsUI()
    {
        int coins = GetCoins();
        string txt = $"{coins.ToString("#,0")} 코인";
        if (panelCoinsText) panelCoinsText.text = txt;
        if (lobbyCoinsText) lobbyCoinsText.text = txt;
    }
}
