// 배경 음악을 전역으로 관리하고 씬 전환 시 크로스페이드로 부드럽게 전환
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BgmManager : MonoBehaviour
{
    public static BgmManager Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] private AudioClip titleBgm; // 타이틀 씬에서 재생할 음악
    [SerializeField] private AudioClip gameBgm;  // 게임 씬에서 재생할 음악

    [Header("Settings")]
    [SerializeField] private float crossfadeSeconds = 0.5f; // 음악 전환시 크로스페이드 시간
    [SerializeField] private string titleSceneName = "Title"; // 타이틀 씬 이름
    [SerializeField] private string gameSceneName = "Main";   // 게임 씬 이름

    private AudioSource srcA, srcB; // 교차 재생용 오디오 소스 두 개
    private AudioSource active;     // 현재 재생 중인 오디오 소스
    private Coroutine xfadeRoutine; // 크로스페이드 코루틴 핸들

    [SerializeField] private float bgmVolume = 0.5f;  // 배경 음악 기본 볼륨
    [SerializeField] private float sfxVolume = 1f;    // 효과음 볼륨
    [SerializeField] private float masterVolume = 1f; // 전체 볼륨

    public float MusicVolume { get => bgmVolume; set { bgmVolume = Mathf.Clamp01(value); ApplyVolumes(); } } // 음악 볼륨 프로퍼티
    public float SfxVolume { get => sfxVolume; set { sfxVolume = Mathf.Clamp01(value); } }                   // 효과음 볼륨 프로퍼티
    public float MasterVolume { get => masterVolume; set { masterVolume = Mathf.Clamp01(value); ApplyVolumes(); } } // 전체 볼륨 프로퍼티
    public float EffectiveSfxVolume => Mathf.Clamp01(sfxVolume * masterVolume);
    private float TargetBgmVolume => Mathf.Clamp01(bgmVolume * masterVolume); // 실제 적용되는 음악 볼륨

    private void Awake()
    {
        // 싱글톤 생성과 유지
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 듀얼 오디오 소스 생성과 기본 설정
        srcA = gameObject.AddComponent<AudioSource>();
        srcB = gameObject.AddComponent<AudioSource>();
        foreach (var s in new[] { srcA, srcB })
        {
            s.playOnAwake = false;
            s.loop = true;
            s.volume = 0f;
        }
        active = srcA;

        // 씬 로드 이벤트 구독
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        // 씬 로드 이벤트 해제
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 이름에 따라 자동으로 음악 선택
        if (scene.name == titleSceneName)
        {
            PlayBgm(titleBgm);
        }
        else if (scene.name == gameSceneName)
        {
            PlayBgm(gameBgm);
        }
    }

    private void ApplyVolumes()
    {
        // 현재 재생중인 소스와 보조 소스에 목표 볼륨 적용
        if (active != null && active.isPlaying) active.volume = TargetBgmVolume;
        var other = (active == srcA) ? srcB : srcA;
        if (other != null && other.isPlaying) other.volume = TargetBgmVolume;
    }

    // 같은 클립이면 재시작하지 않고 볼륨만 갱신
    public void PlayBgm(AudioClip clip, float? customFadeSeconds = null)
    {
        if (clip == null) return;
        if (active.clip == clip && active.isPlaying) { ApplyVolumes(); return; }
        if (xfadeRoutine != null) StopCoroutine(xfadeRoutine);
        xfadeRoutine = StartCoroutine(CoCrossfadeTo(clip, customFadeSeconds ?? crossfadeSeconds));
    }

    private IEnumerator CoCrossfadeTo(AudioClip next, float seconds)
    {
        // 두 소스를 사용하여 부드럽게 페이드 아웃 인 수행
        AudioSource from = active;
        AudioSource to = (active == srcA) ? srcB : srcA;

        to.clip = next; to.volume = 0f; to.loop = true; to.Play();

        float t = 0f; float fromStart = from.volume; float toStart = to.volume;
        while (t < seconds)
        {
            // 시간 스케일에 영향을 받지 않도록 처리
            t += Time.unscaledDeltaTime;
            float k = (seconds <= 0f) ? 1f : Mathf.Clamp01(t / seconds);
            if (from.clip != null) from.volume = Mathf.Lerp(fromStart, 0f, k);
            to.volume = Mathf.Lerp(toStart, TargetBgmVolume, k);
            yield return null;
        }
        if (from.clip != null) { from.Stop(); from.volume = 0f; }
        to.volume = TargetBgmVolume; active = to; xfadeRoutine = null;
    }

    public void FadeOutCurrent(float seconds = 0.6f)
    {
        if (active == null) return;
        if (xfadeRoutine != null) StopCoroutine(xfadeRoutine);
        xfadeRoutine = StartCoroutine(CoFadeOutOnly(seconds));
    }

    // 현재 트랙만 볼륨 0까지 내리고 정지
    private IEnumerator CoFadeOutOnly(float seconds)
    {
        var src = active;
        float startVol = src ? src.volume : 0f;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            if (src) src.volume = Mathf.Lerp(startVol, 0f, seconds <= 0f ? 1f : t / seconds);
            yield return null;
        }
        if (src) { src.Stop(); src.volume = 0f; }
        xfadeRoutine = null;
    }

    // 게임 BGM 명시적 재생
    public void PlayGameBgm(float? fadeSeconds = null)
    {
        if (gameBgm == null) return;
        PlayBgm(gameBgm, fadeSeconds);
    }

    // 로비 BGM 명시적 재생이 필요하면
    public void PlayTitleBgm(float? fadeSeconds = null)
    {
        if (titleBgm == null) return;
        PlayBgm(titleBgm, fadeSeconds);
    }

    public void PlaySfx(AudioClip clip, Vector3 worldPos, float mul = 1f)
    {
        if (!clip) return;
        var vol = Mathf.Clamp01(EffectiveSfxVolume * Mathf.Clamp01(mul));
        AudioSource.PlayClipAtPoint(clip, worldPos, vol);
    }

    // UI 클릭음처럼 화면 중앙에서 재생할 때 편의용
    public void PlayUiSfx(AudioClip clip, float mul = 1f)
    {
        PlaySfx(clip, Camera.main ? Camera.main.transform.position : Vector3.zero, mul);
    }

    private Coroutine pauseCo;
    private Coroutine resumeCo;

    // 일시정지. fadeSeconds 동안 볼륨을 0으로 내리고 Pause 호출
    public void PauseBgm(bool withFade = true, float fadeSeconds = 0.2f)
    {
        if (active == null) return;
        if (xfadeRoutine != null) StopCoroutine(xfadeRoutine);
        if (resumeCo != null) StopCoroutine(resumeCo);
        if (pauseCo != null) StopCoroutine(pauseCo);
        pauseCo = StartCoroutine(CoFadeDownAndPause(withFade ? fadeSeconds : 0f));
    }

    // 일시정지 해제. UnPause 후 fadeSeconds 동안 목표 볼륨까지 서서히 올림
    public void ResumeBgm(bool withFade = true, float fadeSeconds = 0.2f)
    {
        if (active == null) return;
        if (xfadeRoutine != null) StopCoroutine(xfadeRoutine);
        if (pauseCo != null) StopCoroutine(pauseCo);
        if (resumeCo != null) StopCoroutine(resumeCo);
        resumeCo = StartCoroutine(CoUnpauseAndFadeUp(withFade ? fadeSeconds : 0f));
    }

    private IEnumerator CoFadeDownAndPause(float seconds)
    {
        if (!active) yield break;
        float start = active.volume;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = seconds <= 0f ? 1f : Mathf.Clamp01(t / seconds);
            active.volume = Mathf.Lerp(start, 0f, k);
            yield return null;
        }
        active.volume = 0f;
        active.Pause();   // 재생 위치 유지
        pauseCo = null;
    }

    private IEnumerator CoUnpauseAndFadeUp(float seconds)
    {
        if (!active) yield break;
        active.UnPause();           // 끊긴 지점부터 이어서 재생
        float t = 0f;
        float targetVol = TargetBgmVolume;
        active.volume = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = seconds <= 0f ? 1f : Mathf.Clamp01(t / seconds);
            active.volume = Mathf.Lerp(0f, targetVol, k);
            yield return null;
        }
        active.volume = targetVol;
        resumeCo = null;
    }

    public void PlaySfxCoin(AudioClip clip, Vector3 worldPos)
    {
        if (!clip) return;
        StartCoroutine(CoPlayCoinLayered(clip, worldPos, EffectiveSfxVolume));
    }

    private IEnumerator CoPlayCoinLayered(AudioClip clip, Vector3 pos, float baseVol)
    {
        // 레이어 1
        AudioSource.PlayClipAtPoint(clip, pos, Mathf.Clamp01(baseVol));
        // 아주 미세한 프레임 지연 후 레이어 2(콤필터/위상 이슈 완화)
        yield return null; // 다음 프레임(~10~16ms)
        AudioSource.PlayClipAtPoint(clip, pos, Mathf.Clamp01(baseVol));
    }
}
