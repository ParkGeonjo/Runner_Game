using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Button openSelectButton;     // 패널 열기 버튼
    [SerializeField] private Button closeSelectButton;    // 패널 닫기 버튼
    [SerializeField] private Button startGameButton;      // 시작 버튼
    [SerializeField] private GameObject selectCharacterPanel;  // 캐릭터 선택 패널
    [SerializeField] private Transform CharactersContent;      // 아이템 컨테이너
    [SerializeField] private Image fadePanel;             // 화면 페이드
    [SerializeField] private float fadeDuration = 0.25f;  // 페이드 시간

    [Header("Characters")]
    [SerializeField] private int minCharacterId = 1;      // 캐릭터 최소 아이디
    [SerializeField] private int maxCharacterId = 4;      // 캐릭터 최대 아이디

    [Header("Tween")]
    [SerializeField] private float openScaleDuration = 0.28f;   // 열기 시간
    [SerializeField] private float closeScaleDuration = 0.22f;  // 닫기 시간
    [SerializeField] private float closedScale = 0.0f;          // 닫힌 스케일
    [SerializeField] private Ease openEase = Ease.OutBack;      // 열기 이징
    [SerializeField] private Ease closeEase = Ease.InBack;      // 닫기 이징

    [Header("Tooltip")]
    [SerializeField] private CanvasGroup toolTip;         // 가운데 툴팁 패널
    [SerializeField] private Text toolTipText;            // 툴팁 텍스트
    [SerializeField] private float tipFadeTime = 0.5f;    // 툴팁 페이드 시간
    [SerializeField] private float tipHoldTime = 0.7f;    // 툴팁 유지 시간

    [Header("Audio")]
    [SerializeField] public AudioClip buttonSfx;          // 버튼 사운드
    [SerializeField] private AudioClip startSfx;         // 게임 시작 사운드
    [SerializeField] private AudioClip purchaseSuccessSfx; // 쿠키 구매 사운드

    [Header("Lobby Preview")]
    [SerializeField] private Transform lobbyCharactersRoot; // 로비 캔버스의 Characters (자식: Character (1)...)
    
    [Header("Start Panel")]
    [SerializeField] private GameObject startPanelRoot;     // StartPanel 루트
    [SerializeField] private Transform startPanelCharacters; // StartPanel/Characters (자식: Character (1)...)
    [SerializeField] private RectTransform startMessage;     // StartPanel/Message
    [SerializeField] private Text startMessageText;          // StartPanel/Message/Text
    [SerializeField] private float startMessageScaleIn = 0.35f; // 메시지 스케일 인 시간
    [SerializeField] private Ease startMessageEase = Ease.OutBack;
    [SerializeField] private float startShowSeconds = 4f;      // StartPanel 노출 시간

    [Header("Character Lines")]
    [SerializeField, TextArea] private string[] lines1; // 캐릭터 1 대사들
    [SerializeField, TextArea] private string[] lines2; // 캐릭터 2 대사들
    [SerializeField, TextArea] private string[] lines3; // 캐릭터 3 대사들
    [SerializeField, TextArea] private string[] lines4; // 캐릭터 4 대사들

    private const string PREF_SELECTED   = "SelectedCharacter";           // 선택 저장
    private const string PREF_UNLOCK_FMT = "Character_{0}_IsUnlock";      // 언락 여부
    private const string PREF_COINS      = "Player_Coins";                // 코인 잔액
    private const string PREF_BEST       = "BestScore";                   // 점수 기록

    private readonly Dictionary<int, CharacterUI> Characters = new();     // UI 캐시
    private int selectedCharacterId;                                      // 현재 선택
    private bool isTransitioning;                                         // 전환 중

    private Vector3 panelOpenScale = Vector3.one; // 열렸을 때 스케일
    private Tween panelTween;                     // 패널 트윈
    private CanvasGroup selectCg;                 // 패널 상호작용 제어

    public bool IsStartPanelOpen => startPanelRoot != null && startPanelRoot.activeSelf;

    [SerializeField] private Text panelCoinsText; // Character Panel 안의 "보유량 N 코인"
    [SerializeField] private Text lobbyCoinsText; // 타이틀 로비 화면의 코인 표시
    [SerializeField] private Text lobbyScoreText; // 타이틀 로비 화면의 점수 표시

    private readonly Dictionary<int, int> priceMap = new Dictionary<int, int>
    {
        { 1, 0 }, { 2, 1000 }, { 3, 2500 }, { 4, 5000 }
    };

    #region Character UI holder
    private class CharacterUI
    {
        public int id;                 // 아이디
        public GameObject root;        // 루트
        public Image rootImage;        // 이미지
        public Text nameText;          // 이름
        public GameObject playerObj;   // 프리뷰
        public GameObject lockObj;     // 잠금표시(미사용 슬롯 유지)
        public Button selectButton;    // 선택 버튼
        public Text selectLabel;       // 라벨
        public string originalName;    // 원래 이름
    }
    #endregion

    private void Awake()
    {
        if (fadePanel != null) SetImageAlpha01(fadePanel, 0f);

        Time.timeScale = 1f;

        CollectCharacterItems();  // 항목 수집
        selectedCharacterId = Mathf.Clamp(PlayerPrefs.GetInt(PREF_SELECTED, 1), minCharacterId, maxCharacterId);
        EnsureUnlockPref(1, true); // 기본 캐릭터 해금

        // 선택 패널 초기 세팅
        if (selectCharacterPanel)
        {
            if (!selectCharacterPanel.TryGetComponent(out selectCg))
                selectCg = selectCharacterPanel.AddComponent<CanvasGroup>();
            selectCg.alpha = 1f; selectCg.interactable = false; selectCg.blocksRaycasts = false;
            selectCharacterPanel.transform.localScale = Vector3.one * closedScale;
            selectCharacterPanel.SetActive(false);
        }

        // 툴팁 초기 세팅
        if (toolTip)
        {
            toolTip.alpha = 0f;
            toolTip.gameObject.SetActive(false);
        }

        if (openSelectButton)  openSelectButton.onClick.AddListener(OnClickOpenSelect);
        if (closeSelectButton) closeSelectButton.onClick.AddListener(OnClickCloseSelect);
        if (startGameButton)   startGameButton.onClick.AddListener(OnClickStartGame);
    }

    private void Start()
    {
        // 씬 시작 시 코인 갱신
        RefreshCoinsUI();
        // 씬 시작 시 기록 갱신
        RefreshScoreUI();
        // 로비에 선택 캐릭터만 활성화
        RefreshLobbyPreview();
        // 선택 상태 반영
        ApplyAllCharacterVisuals();
    }

    private void OnEnable()
    {
        foreach (var kv in Characters)
        {
            int id = kv.Key;
            var ui = kv.Value;
            if (ui.selectButton != null)
            {
                ui.selectButton.onClick.RemoveAllListeners();
                ui.selectButton.onClick.AddListener(() => OnClickCharacter(id));
            }
        }
    }

    // 패널 열기
    private void OnClickOpenSelect()
    {
        if (isTransitioning) return;
        if (buttonSfx)
        {
            if (BgmManager.Instance) BgmManager.Instance.PlayUiSfx(buttonSfx);
            else AudioSource.PlayClipAtPoint(buttonSfx, Vector3.zero);
        }
        StartCoroutine(OpenSelectCharacterPanelRoutine());
    }

    // 패널 닫기
    private void OnClickCloseSelect()
    {
        if (isTransitioning) return;
        if (buttonSfx)
        {
            if (BgmManager.Instance) BgmManager.Instance.PlayUiSfx(buttonSfx);
            else AudioSource.PlayClipAtPoint(buttonSfx, Vector3.zero);
        }
        StartCoroutine(CloseSelectCharacterPanelRoutine());
        // 닫을 때 로비 코인 텍스트도 갱신
        RefreshCoinsUI();
        // 닫을 때 로비 프리뷰도 갱신
        RefreshLobbyPreview();
    }

    // 게임 시작
    private void OnClickStartGame()
    {
        if (isTransitioning) return;

        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.FadeOutCurrent(0.3f); // 로비 BGM 부드럽게 끄기
        }

        PlayerPrefs.SetInt(PREF_SELECTED, selectedCharacterId); // 선택 저장
        PlayerPrefs.Save();

        if (buttonSfx)
        {
            if (BgmManager.Instance) BgmManager.Instance.PlayUiSfx(buttonSfx);
            else AudioSource.PlayClipAtPoint(buttonSfx, Vector3.zero);
        }
        StartCoroutine(CoStartSequenceAndLoad("Main_2"));
    }

    // 캐릭터 버튼 클릭
    private void OnClickCharacter(int id)
    {
        // 미해금이면 구매 시도
        if (!IsUnlocked(id))
        {
            int price = GetPrice(id);
            int coins = PlayerPrefs.GetInt(PREF_COINS, 0);

            if (coins < price)
            {
                ShowTip("=  코인 부족!  =");
                return;
            }

            coins -= price;
            PlayerPrefs.SetInt(PREF_COINS, coins);
            SetUnlocked(id, true);            // 해금
            selectedCharacterId = id;         // 바로 선택
            PlayerPrefs.SetInt(PREF_SELECTED, selectedCharacterId);
            PlayerPrefs.Save();

            PlayPurchaseSuccessSfx();

            ApplyAllCharacterVisuals();
            RefreshCoinsUI();

            ShowTip("=  구매 성공!  =");
        }
        else
        {
            // 해금 상태면 선택만 갱신
            selectedCharacterId = id;
            PlayerPrefs.SetInt(PREF_SELECTED, selectedCharacterId);
            PlayerPrefs.Save();
        }

        if (buttonSfx)
        {
            if (BgmManager.Instance) BgmManager.Instance.PlayUiSfx(buttonSfx);
            else AudioSource.PlayClipAtPoint(buttonSfx, Vector3.zero);
        }
        ApplyAllCharacterVisuals();   // 시각 갱신
        RefreshLobbyPreview();        // 로비 미리보기 즉시 갱신
    }

    private void PlayPurchaseSuccessSfx()
    {
        if (!purchaseSuccessSfx) return;
        if (BgmManager.Instance) BgmManager.Instance.PlayUiSfx(purchaseSuccessSfx);
        else AudioSource.PlayClipAtPoint(purchaseSuccessSfx, Vector3.zero);
    }

    // 패널 열기 연출
    private IEnumerator OpenSelectCharacterPanelRoutine()
    {
        isTransitioning = true;
        ApplyAllCharacterVisuals();

        if (panelTween != null && panelTween.IsActive()) panelTween.Kill();
        selectCharacterPanel.SetActive(true);
        selectCharacterPanel.transform.localScale = Vector3.one * closedScale;

        selectCg.interactable = false;
        selectCg.blocksRaycasts = true;

        panelTween = selectCharacterPanel.transform
            .DOScale(panelOpenScale, openScaleDuration)
            .SetEase(openEase);

        yield return panelTween.WaitForCompletion();
        selectCg.interactable = true;
        selectCg.blocksRaycasts = true;
        isTransitioning = false;

        ApplyAllCharacterVisuals();
        RefreshCoinsUI(); // 열 때 갱신
    }

    // 패널 닫기 연출
    private IEnumerator CloseSelectCharacterPanelRoutine()
    {
        isTransitioning = true;
        if (!selectCharacterPanel.activeSelf) { isTransitioning = false; yield break; }

        if (panelTween != null && panelTween.IsActive()) panelTween.Kill();
        selectCg.interactable = false;
        selectCg.blocksRaycasts = false;

        panelTween = selectCharacterPanel.transform
            .DOScale(Vector3.one * closedScale, closeScaleDuration)
            .SetEase(closeEase);

        yield return panelTween.WaitForCompletion();
        selectCharacterPanel.SetActive(false);
        isTransitioning = false;
    }

    // 게임 시작 연출: 페이드아웃 → StartPanel 표시 → 4초 → 페이드아웃 → 씬 전환
    private IEnumerator CoStartSequenceAndLoad(string sceneName)
    {
        isTransitioning = true;

        // 1) 페이드 아웃
        yield return StartCoroutine(Fade(1f));

        // 2) StartPanel 셋업 후 페이드 인
        SetupStartPanel();
        yield return StartCoroutine(Fade(0f));

        // 3) 시작 사운드 재생
        if (startSfx) { if (BgmManager.Instance) BgmManager.Instance.PlayUiSfx(startSfx); else AudioSource.PlayClipAtPoint(startSfx, Vector3.zero); }

        // 4) 메시지 스케일 인
        if (startMessage)
        {
            startMessage.DOKill();
            yield return startMessage
                .DOScale(1f, startMessageScaleIn)
                .SetEase(startMessageEase)
                .SetUpdate(true)   // ★ 일시정지여도 진행
                .WaitForCompletion();
        }

        // 5) 지정 시간 노출
        float t = 0f;
        while (t < startShowSeconds) { t += Time.unscaledDeltaTime; yield return null; }

        // 6) 바로 씬 로드  페이드아웃 삭제
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;

        isTransitioning = false;
    }

    // StartPanel 구성: 선택 캐릭터만 활성화, 대사 설정
    private void SetupStartPanel()
    {
        if (!startPanelRoot) return;

        // Characters 내 선택 캐릭터만 활성화
        if (startPanelCharacters)
        {
            for (int i = 0; i < startPanelCharacters.childCount; i++)
            {
                var child = startPanelCharacters.GetChild(i);
                int id = ParseTrailingNumber(child.name); // "Character (n)"
                child.gameObject.SetActive(id == selectedCharacterId);
            }
        }

        // 대사 텍스트 적용
        if (startMessageText)
            startMessageText.text = GetRandomLine(selectedCharacterId); // 랜덤 대사 사용 중이면 이 함수를 사용

        // 패널, 메시지 활성화
        startPanelRoot.SetActive(true);

        if (startMessage)
        {
            // 비활성화되어 있어도 반드시 켬
            startMessage.gameObject.SetActive(true);
            startMessage.localScale = Vector3.zero; // 스케일 인 준비
        }
    }

    // 전체 페이드
    private IEnumerator Fade(float target01)
    {
        if (fadePanel == null) yield break;
        fadePanel.raycastTarget = true;
        float start = fadePanel.color.a;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            SetImageAlpha01(fadePanel, Mathf.Lerp(start, target01, t / fadeDuration));
            yield return null;
        }
        SetImageAlpha01(fadePanel, target01);
        fadePanel.raycastTarget = target01 > 0.001f;
    }

    // 캐릭터 목록의 시각 상태를 일괄 반영
    private void ApplyAllCharacterVisuals()
    {
        if (!IsUnlocked(selectedCharacterId)) selectedCharacterId = 1;

        foreach (var kv in Characters)
        {
            int id = kv.Key;
            var ui = kv.Value;

            bool unlocked = IsUnlocked(id);
            bool isSelected = unlocked && id == selectedCharacterId;

            // 배경 알파값만 조정
            if (ui.rootImage)
            {
                var c = ui.rootImage.color;
                c.a = isSelected ? 1f : (100f / 255f);
                ui.rootImage.color = c;
            }

            // 버튼 라벨
            if (ui.selectLabel)
            {
                if (!unlocked)
                    ui.selectLabel.text = $"{GetPrice(id).ToString("#,0")} 코인"; // 200 -> 200, 1,500 -> 1,500
                else
                    ui.selectLabel.text = isSelected ? "선택됨" : "쿠키 선택";
            }

            // 선택된 버튼의 그래픽 알파 150
            if (ui.selectButton)
            {
                var g = ui.selectButton.targetGraphic as Graphic;
                if (g != null)
                {
                    var c = g.color;
                    c.a = isSelected ? (150f / 255f) : 1f;
                    g.color = c;
                }
            }
        }
    }

    // 로비 프리뷰: 로비 Characters 하위에서 선택 캐릭터만 활성화
    private void RefreshLobbyPreview()
    {
        if (!lobbyCharactersRoot) return;
        for (int i = 0; i < lobbyCharactersRoot.childCount; i++)
        {
            var child = lobbyCharactersRoot.GetChild(i);
            int id = ParseTrailingNumber(child.name); // "Character (n)"
            bool active = (id == selectedCharacterId);
            child.gameObject.SetActive(active);
        }
    }

    // 항목 수집
    private void CollectCharacterItems()
    {
        if (CharactersContent == null)
        {
            Debug.LogError("CharactersContent 가 비어있음");
            return;
        }
        Characters.Clear();

        for (int i = 0; i < CharactersContent.childCount; i++)
        {
            var child = CharactersContent.GetChild(i);
            if (!child.name.StartsWith("Character")) continue;

            int id = ParseTrailingNumber(child.name);
            if (id < minCharacterId || id > maxCharacterId) continue;

            var ui = new CharacterUI
            {
                id = id,
                root = child.gameObject,
                rootImage = child.GetComponent<Image>(),
                nameText = child.Find("Name") ? child.Find("Name").GetComponent<Text>() : null,
                playerObj = child.Find("Character") ? child.Find("Character").gameObject : null,
                lockObj = child.Find("Eye") ? child.Find("Eye").gameObject : null,
            };

            var selBtTr = child.Find("SelectBt");
            if (selBtTr)
            {
                ui.selectButton = selBtTr.GetComponent<Button>();
                var labelTr = selBtTr.Find("Text (Legacy)");
                if (labelTr) ui.selectLabel = labelTr.GetComponent<Text>();
            }

            ui.originalName = ui.nameText ? ui.nameText.text : "Character " + id;
            if (id == 1) EnsureUnlockPref(1, true);
            Characters[id] = ui;
        }
    }

    // 유틸
    private void SetImageAlpha01(Image img, float a01)
    {
        var c = img.color; c.a = Mathf.Clamp01(a01); img.color = c;
    }

    private int ParseTrailingNumber(string name)
    {
        int num = 0;
        for (int i = name.Length - 1; i >= 0; --i)
        {
            if (char.IsDigit(name[i]))
            {
                int end = i, start = i;
                while (start >= 0 && char.IsDigit(name[start])) start--;
                start++;
                int.TryParse(name.Substring(start, end - start + 1), out num);
                break;
            }
        }
        return num;
    }

    // 언락 저장
    private void EnsureUnlockPref(int id, bool value)
    {
        string key = string.Format(PREF_UNLOCK_FMT, id);
        if (!PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.SetString(key, value ? "true" : "false");
            PlayerPrefs.Save();
        }
    }
    private bool IsUnlocked(int id)
    {
        if (id == 1) return true;
        string key = string.Format(PREF_UNLOCK_FMT, id);
        if (PlayerPrefs.HasKey(key))
        {
            string s = PlayerPrefs.GetString(key, "");
            if (!string.IsNullOrEmpty(s) && bool.TryParse(s, out bool b)) return b;
            int i = PlayerPrefs.GetInt(key, -999);
            if (i != -999) return i != 0;
        }
        return false;
    }
    private void SetUnlocked(int id, bool v)
    {
        string key = string.Format(PREF_UNLOCK_FMT, id);
        PlayerPrefs.SetString(key, v ? "true" : "false");
    }
    private int GetPrice(int id) => priceMap.TryGetValue(id, out var p) ? p : 0;

    // 툴팁 표시
    private void ShowTip(string message)
    {
        if (!toolTip) return;
        toolTipText.text = message;
        toolTip.gameObject.SetActive(true);
        toolTip.alpha = 1f;
        toolTip.DOKill();
        toolTip.DOFade(0f, tipFadeTime).SetDelay(tipHoldTime).OnComplete(() =>
        {
            toolTip.gameObject.SetActive(false);
        });
    }

    // "보유량 N 코인" 갱신 패널과 로비 동시 반영
    private void RefreshCoinsUI()
    {
        int coins = PlayerPrefs.GetInt(PREF_COINS, 0);
        if (panelCoinsText) panelCoinsText.text = $"{coins.ToString("#,0")} 코인";
        if (lobbyCoinsText) lobbyCoinsText.text = $"{coins.ToString("#,0")} 코인";
    }

    private void RefreshScoreUI()
    {
        int prevBest = PlayerPrefs.GetInt(PREF_BEST, 0);
        if (lobbyScoreText) lobbyScoreText.text = prevBest.ToString("#,0");
    }

    // 캐릭터 대사 반환
    private string GetRandomLine(int id)
    {
        string[] pool = id switch
        {
            2 => lines2,
            3 => lines3,
            4 => lines4,
            _ => lines1
        };

        if (pool == null || pool.Length == 0) return "";
        return pool[Random.Range(0, pool.Length)];
    }
}
