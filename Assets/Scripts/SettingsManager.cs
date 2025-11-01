using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class SettingsManager : MonoBehaviour
{
    [Header("Open/Close")]
    [SerializeField] private Button openButton;       // 설정 열기 버튼
    [SerializeField] private GameObject panelRoot;    // 설정 패널 루트

    [Header("Tween")]
    [SerializeField] private float openScaleDuration = 0.28f;
    [SerializeField] private float closeScaleDuration = 0.22f;
    [SerializeField] private float closedScale = 0.0f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;

    [Header("Inside Panel")]
    [SerializeField] private Button closeButton;      // 닫기 버튼
    [SerializeField] private Button gotoTitleBt;      // 타이틀로
    [SerializeField] private Button gameExitBt;       // 종료

    [Header("Sliders")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Text musicText;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Text sfxText;
    [SerializeField] private Slider allSlider;
    [SerializeField] private Text allText;

    [Header("Scene / Fade")]
    [SerializeField] private string titleSceneName = "Title";
    [SerializeField] private Image fadePanel;         // 씬 전환용만 사용

    [Header("Audio")]
    [SerializeField] private AudioClip buttonSfx;

    private CanvasGroup cg;
    private bool isOpen;
    private Tween panelTween;
    private Vector3 panelOpenScale = Vector3.one;

    private const string PREF_MUSIC  = "VOL_MUSIC";
    private const string PREF_SFX    = "VOL_SFX";
    private const string PREF_MASTER = "VOL_MASTER";

    private void Awake()
    {
        // 패널은 스케일 애니메이션만 사용. 패널용 페이드 X
        if (panelRoot != null)
        {
            cg = panelRoot.GetComponent<CanvasGroup>();
            if (cg == null) cg = panelRoot.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            panelRoot.transform.localScale = Vector3.one * closedScale;
            panelRoot.SetActive(false);
        }

        if (fadePanel != null) SetFadeAlpha(0f); // 씬 전환용만 유지

        if (openButton)  openButton.onClick.AddListener(() => { PlayUiClick(); Open(); });
        if (closeButton) closeButton.onClick.AddListener(() => { PlayUiClick(); Close(); });
        if (gotoTitleBt) gotoTitleBt.onClick.AddListener(() => { PlayUiClick(); OnClickGotoTitle(); });
        if (gameExitBt)  gameExitBt.onClick.AddListener(() => { PlayUiClick(); QuitApp(); });

        if (musicSlider) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (sfxSlider)   sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        if (allSlider)   allSlider.onValueChanged.AddListener(OnAllChanged);
    }

    private void Start()
    {
        float defMusic  = BgmManager.Instance ? BgmManager.Instance.MusicVolume  : 0.6f;
        float defSfx    = BgmManager.Instance ? BgmManager.Instance.SfxVolume    : 1f;
        float defMaster = BgmManager.Instance ? BgmManager.Instance.MasterVolume : 1f;

        float music  = PlayerPrefs.GetFloat(PREF_MUSIC,  defMusic);
        float sfx    = PlayerPrefs.GetFloat(PREF_SFX,    defSfx);
        float master = PlayerPrefs.GetFloat(PREF_MASTER, defMaster);

        if (musicSlider) musicSlider.SetValueWithoutNotify(Mathf.Clamp01(music));
        if (sfxSlider)   sfxSlider.SetValueWithoutNotify(Mathf.Clamp01(sfx));
        if (allSlider)   allSlider.SetValueWithoutNotify(Mathf.Clamp01(master));

        ApplyVolumes();
        RefreshTexts();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            // ✅ 결과창/StartPanel이 떠 있으면 설정창 토글 금지
            if (IsAnyModalOpen()) return;

            if (!isOpen) { PlayUiClick(); Open(); }
            else { PlayUiClick(); Close(); }
        }

        // (선택) 버튼 비활성화 UX: 시작패널/결과창 열려 있으면 설정 버튼 누를 수 없게
        if (openButton) openButton.interactable = !IsAnyModalOpen();
    }

    private bool IsAnyModalOpen()
    {
        // 인게임 결과창 열림 차단
        if (InGameManager.instance != null && InGameManager.instance.IsResultOpen)
            return true;

        // 타이틀 씬의 StartPanel 열림 차단
        var tm = FindObjectOfType<TitleManager>();
        if (tm != null && tm.IsStartPanelOpen)
            return true;

        return false;
    }

    public void Open()
    {
        if (IsAnyModalOpen()) return;
        if (isOpen || panelRoot == null) return;

        // Main 씬에서만 게임 일시정지 + BGM 일시정지
        if (InGameManager.instance != null)
        {
            InGameManager.instance.SetPaused(true);
            if (BgmManager.Instance) BgmManager.Instance.PauseBgm(true, 0.2f); // ← Stop 대신 Pause
        }

        panelRoot.SetActive(true);
        if (panelTween != null && panelTween.IsActive()) panelTween.Kill();
        cg.interactable = false;
        cg.blocksRaycasts = true;

        panelRoot.transform.localScale = Vector3.zero;
        panelTween = panelRoot.transform
            .DOScale(1f, openScaleDuration)
            .SetEase(openEase)
            .SetUpdate(true)  // 타임스케일 0에서도 애니메이션
            .OnComplete(() =>
            {
                cg.interactable = true;
                cg.blocksRaycasts = true;
            });

        isOpen = true;
    }

    public void Close(bool resumeGameplay = true)
    {
        if (!isOpen || panelRoot == null) return;

        if (panelTween != null && panelTween.IsActive()) panelTween.Kill();
        cg.interactable = false;
        cg.blocksRaycasts = false;

        panelTween = panelRoot.transform
            .DOScale(0f, closeScaleDuration)
            .SetEase(closeEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                panelRoot.SetActive(false);

                // ✅ 기본은 기존처럼 재개 / 단, resumeGameplay==false면 재개하지 않음
                if (resumeGameplay && InGameManager.instance != null)
                {
                    InGameManager.instance.SetPaused(false);
                    if (BgmManager.Instance) BgmManager.Instance.ResumeBgm(true, 0.2f);
                }
            });

        isOpen = false;
    }

    // 씬 전환용 페이드 유틸(설정 패널엔 사용 안함)
    private void SetFadeAlpha(float a)
    {
        if (!fadePanel) return;
        var c = fadePanel.color; c.a = Mathf.Clamp01(a); fadePanel.color = c;
    }

    // 볼륨 이벤트
    private void OnMusicChanged(float v)
    {
        PlayerPrefs.SetFloat(PREF_MUSIC, Mathf.Clamp01(v));
        ApplyVolumes();
        RefreshTexts();
    }

    private void OnSfxChanged(float v)
    {
        PlayerPrefs.SetFloat(PREF_SFX, Mathf.Clamp01(v));
        ApplyVolumes();
        RefreshTexts();
    }

    private void OnAllChanged(float v)
    {
        PlayerPrefs.SetFloat(PREF_MASTER, Mathf.Clamp01(v));
        ApplyVolumes();
        RefreshTexts();
    }

    private void ApplyVolumes()
    {
        float music  = musicSlider ? musicSlider.value : 0.6f;
        float sfx    = sfxSlider   ? sfxSlider.value   : 1f;
        float master = allSlider   ? allSlider.value   : 1f;

        if (BgmManager.Instance)
        {
            BgmManager.Instance.MusicVolume  = music;
            BgmManager.Instance.SfxVolume    = sfx;
            BgmManager.Instance.MasterVolume = master;
        }
    }

    private void RefreshTexts()
    {
        if (musicText && musicSlider) musicText.text = Mathf.RoundToInt(musicSlider.value * 100f) + "%";
        if (sfxText   && sfxSlider)   sfxText.text   = Mathf.RoundToInt(sfxSlider.value   * 100f) + "%";
        if (allText   && allSlider)   allText.text   = Mathf.RoundToInt(allSlider.value   * 100f) + "%";
    }

    private void PlayUiClick()
    {
        if (!buttonSfx) return;
        if (BgmManager.Instance) BgmManager.Instance.PlayUiSfx(buttonSfx);
        else AudioSource.PlayClipAtPoint(buttonSfx, Vector3.zero);
    }

    private void OnClickGotoTitle()
    {
        if (InGameManager.instance != null)
        {
            // ✅ 설정창을 닫되, 게임은 재개하지 않는다
            Close(false);

            // ✅ 정지 상태 유지 + BGM 일시정지 보장
            InGameManager.instance.SetPaused(true);
            if (BgmManager.Instance) BgmManager.Instance.PauseBgm(true, 0.2f);

            // ✅ 결과창 표시 (확인 버튼에서 실제 로비 이동)
            InGameManager.instance.ShowResultPanel();
            return;
        }

        // 타이틀 씬이면 정상 이동
        SceneManager.LoadScene("Title");
    }

    private void QuitApp()
    {
        Application.Quit();
    }
}
