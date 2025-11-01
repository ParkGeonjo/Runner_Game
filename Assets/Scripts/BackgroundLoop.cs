using System.Linq;
using UnityEngine;

// 배경 타일 2장을 화면에 끊김 없이 반복시키는 루프
// - ScrollingObject는 그대로 사용 (이 스크립트는 '재배치'만 담당)
// - 같은 부모 밑에 있는 형제 타일끼리 서로 이어붙입니다.
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundLoop : MonoBehaviour
{
    [Tooltip("비우면 Camera.main")]
    public Camera cam;

    [Tooltip("같이 이어질 타일들의 공통 부모(비우면 transform.parent)")]
    public Transform groupRoot;

    [Tooltip("틈(흰줄) 방지용 아주 작은 겹침(월드 유닛)")]
    public float epsilon = 0.001f;

    [Tooltip("N프레임마다 검사(1=매 프레임)")]
    public int checkEveryNFrames = 1;

    [Tooltip("카메라 왼쪽으로 이만큼 더 벗어나면 재배치(여유 마진)")]
    public float leftMargin = 0f;

    private SpriteRenderer sr;
    private int frame;

    private void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!groupRoot) groupRoot = transform.parent;
        sr = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (!cam || !sr || !sr.sprite) return;
        if ((frame++ % Mathf.Max(1, checkEveryNFrames)) != 0) return;

        // 1) 이 타일이 카메라 왼쪽 화면 밖으로 완전히 사라졌는지 체크
        Bounds b = sr.bounds;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float camLeft = cam.transform.position.x - halfW;

        if (b.max.x < camLeft - leftMargin)
        {
            // 2) 형제들 중 가장 오른쪽 끝(max.x)을 찾는다
            var siblings = (groupRoot ? groupRoot : transform.parent)
                .GetComponentsInChildren<BackgroundLoop>(true)
                .Where(t => t != this && t.sr && t.sr.sprite)
                .Select(t => t.sr.bounds.max.x);

            float rightmostX = siblings.Any() ? siblings.Max() : camLeft;

            // 3) 내 반폭만큼 더해서 '바로 이어 붙이기' (ε만큼 살짝 겹침)
            float myHalf = b.extents.x;
            float newCenterX = rightmostX + myHalf - epsilon;

            Vector3 pos = transform.position;
            pos.x = newCenterX;   // y/z는 기존 유지
            transform.position = pos;
        }
    }
}
