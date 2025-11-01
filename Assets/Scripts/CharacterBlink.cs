using System.Collections;
using UnityEngine;

public class CharacterBlink : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private GameObject eye;      // 눈 오브젝트

    [Header("Timing")]
    [SerializeField] private float minInterval = 1f;   // 최소 대기
    [SerializeField] private float maxInterval = 3f;   // 최대 대기
    [SerializeField] private float blinkDuration = 0.1f; // 눈을 켜는 시간

    private Coroutine loopCo;

    private void Awake()
    {
        // Eye가 비어 있으면 자식에서 찾아 사용
        if (eye == null)
        {
            var t = transform.Find("Eye");
            if (t) eye = t.gameObject;
        }
        if (eye) eye.SetActive(false);
    }

    private void OnEnable()
    {
        if (loopCo == null) loopCo = StartCoroutine(BlinkLoop());
    }

    private void OnDisable()
    {
        if (loopCo != null) { StopCoroutine(loopCo); loopCo = null; }
        if (eye) eye.SetActive(false);
    }

    private IEnumerator BlinkLoop()
    {
        // 일시정지의 영향을 받지 않도록 실시간 대기 사용
        while (true)
        {
            float wait = Random.Range(minInterval, maxInterval);
            yield return new WaitForSecondsRealtime(wait);

            if (eye)
            {
                eye.SetActive(true);
                yield return new WaitForSecondsRealtime(blinkDuration);
                eye.SetActive(false);
            }
        }
    }
}
