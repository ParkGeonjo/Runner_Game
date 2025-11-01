using UnityEngine;

// 게임 오브젝트를 계속 왼쪽으로 움직이는 스크립트
public class ScrollingObject : MonoBehaviour {
    public float speed = 10f; // 이동 속도

    private void Update()
    {
        if (!InGameManager.instance.isGameover)
        {
            float p = InGameManager.instance != null ? InGameManager.instance.Progress01 : 0f;
            float curSpeed = Mathf.Lerp(speed * 0.75f, speed, p); // 60초에 10 도달
            transform.Translate(Vector3.left * curSpeed * Time.deltaTime);
        }
    }
}