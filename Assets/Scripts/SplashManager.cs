using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SplashManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text loadingText;   // "로딩중" 표시용
    [SerializeField] private CanvasGroup fadeCanvas;         // 화면 전체를 덮는 캔버스그룹(알파 페이드)

    [Header("Timing")]
    [SerializeField] private float dotInterval = 0.5f;       // 점 변화 간격
    [SerializeField] private float splashWait = 5f;          // 스플래시 노출 시간
    [SerializeField] private float fadeOutDuration = 0.6f;   // 페이드아웃 시간

    private const string BaseText = "로딩중";
    private Coroutine textRoutine;

    private void Start()
    {

        // 초기 알파 (보이는 상태로 시작)
        if (fadeCanvas != null)
        {
            fadeCanvas.alpha = 1f;
            fadeCanvas.blocksRaycasts = true;
            fadeCanvas.interactable = true;
        }

        textRoutine = StartCoroutine(AnimateLoadingText());
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        // 3초 대기 (타임스케일 영향 없음)
        yield return new WaitForSecondsRealtime(splashWait);

        // 텍스트 코루틴 종료 및 최종 텍스트 고정(선택)
        if (textRoutine != null) StopCoroutine(textRoutine);
        if (loadingText != null) loadingText.text = $"{BaseText} . . .";

        // 페이드아웃
        yield return StartCoroutine(FadeOutRoutine());

        // Title 씬으로 이동
        SceneManager.LoadScene("Title");
    }

    private IEnumerator AnimateLoadingText()
    {
        int dots = 0; // 0~3 반복
        while (true)
        {
            if (loadingText != null)
            {
                switch (dots)
                {
                    case 0: loadingText.text = BaseText; break;
                    case 1: loadingText.text = $"{BaseText} ."; break;
                    case 2: loadingText.text = $"{BaseText} . ."; break;
                    case 3: loadingText.text = $"{BaseText} . . ."; break;
                }
            }

            dots = (dots + 1) % 4;
            yield return new WaitForSecondsRealtime(dotInterval);
        }
    }

    private IEnumerator FadeOutRoutine()
    {
        if (fadeCanvas == null) yield break;

        float t = 0f;
        float start = fadeCanvas.alpha;
        float end = 0f; // 알파 0으로 사라지게 할지 1로 어둡게 덮을지 선택 가능(여기선 0으로 페이드아웃)

        // 스플래시 화면에서 어둡게 사라지게 하려면 위의 end를 0f 대신 0f->1f로 변경하고
        // 시작 알파를 0f로 두는 등 연출에 맞게 조절하세요.

        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(start, end, t / fadeOutDuration);
            fadeCanvas.alpha = a;
            yield return null;
        }

        fadeCanvas.alpha = end;
        fadeCanvas.blocksRaycasts = false;
        fadeCanvas.interactable = false;
    }
}
