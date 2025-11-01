using UnityEngine;

// 발판으로서 필요한 동작을 담은 스크립트
public class Platform : MonoBehaviour
{
    public GameObject[] obstacles; // 장애물 오브젝트들
    private bool stepped = false;  // 플레이어 캐릭터가 밟았었는가

    // 중앙 2단 장애물(별도 오브젝트)
    public GameObject longObstacle; // Middle 위치 Long 장애물

    // 젤리 오브젝트들
    public GameObject[] jellies;

    // 코인 오브젝트들
    public GameObject[] silverCoins; // 1원
    public GameObject[] goldCoins;   // 10원

    public enum ObstaclePattern { Random = 0, ZigZag = 1, Tunnel = 2, Long = 3 }

    // 이번 플랫폼에서 사용할 픽업 종류
    public enum PickupMode { Jelly = 0, Silver = 1, Gold = 2 }
    private PickupMode pickupMode = PickupMode.Jelly;

    // 외부에서 호출해 픽업 종류 지정
    public void SetPickupMode(PickupMode mode) { pickupMode = mode; }

    // 컴포넌트가 활성화될때 마다 매번 실행되는 메서드
    private void OnEnable()
    {
        // 발판을 리셋하는 처리
        stepped = false;

        // 장애물 랜덤 활성화(기존)
        for (int i = 0; i < obstacles.Length; i++)
        {
            if (Random.Range(0, 3) == 0) obstacles[i].SetActive(true);
            else obstacles[i].SetActive(false);
        }

        // Long 장애물은 기본 비활성화
        if (longObstacle) longObstacle.SetActive(false);

        // 모든 픽업을 기본으로 끈다
        ResetAllPickups();

        // 선택된 픽업 종류에 대해서만 항상 켜야 하는 인덱스를 켠다
        AlwaysOnForSelectedPickup();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // 플랫폼 충돌 점수는 제거. 이제 점수는 젤리 코인으로만 획득
    }

    public void ApplyPattern(ObstaclePattern pattern, int step)
    {
        if (obstacles == null || obstacles.Length == 0) return;

        int n = obstacles.Length;

        // 다른 패턴일 때 Long 장애물 간섭 방지
        if (longObstacle) longObstacle.SetActive(false);

        switch (pattern)
        {
            case ObstaclePattern.Random:
                for (int i = 0; i < n; i++)
                    obstacles[i].SetActive(Random.Range(0, 3) == 0);
                break;

            case ObstaclePattern.ZigZag:
                for (int i = 0; i < n; i++)
                    obstacles[i].SetActive(i == (step % n)); // 한 칸씩 이동
                break;

            case ObstaclePattern.Tunnel:
                int hole = step % n; // 한 칸 비우고 나머지 채움. 빈 칸 이동
                for (int i = 0; i < n; i++)
                    obstacles[i].SetActive(i != hole);
                break;

            case ObstaclePattern.Long:
                for (int i = 0; i < n; i++)
                    obstacles[i].SetActive(false); // 먼저 모두 끔
                if (n > 0) obstacles[0].SetActive(true);
                if (n > 2) obstacles[2].SetActive(true);
                if (longObstacle) longObstacle.SetActive(true);
                break;
        }

        // 패턴 적용 후 픽업 활성화 규칙을 반영
        ApplyPickupRules();
    }

    // ===== 픽업 제어 =====

    // 모든 픽업 끄기
    private void ResetAllPickups()
    {
        SetAll(jellies, false);
        SetAll(silverCoins, false);
        SetAll(goldCoins, false);
    }

    // 선택된 픽업 종류에 대해 0,1,9,10 인덱스를 무조건 켠다
    private void AlwaysOnForSelectedPickup()
    {
        switch (pickupMode)
        {
            case PickupMode.Jelly:
                SafeSetActive(jellies, 0, true);
                SafeSetActive(jellies, 1, true);
                SafeSetActive(jellies, 9, true);
                SafeSetActive(jellies, 10, true);
                break;

            case PickupMode.Silver:
                SafeSetActive(silverCoins, 0, true);
                SafeSetActive(silverCoins, 1, true);
                SafeSetActive(silverCoins, 9, true);
                SafeSetActive(silverCoins, 10, true);
                break;

            case PickupMode.Gold:
                SafeSetActive(goldCoins, 0, true);
                SafeSetActive(goldCoins, 1, true);
                SafeSetActive(goldCoins, 9, true);
                SafeSetActive(goldCoins, 10, true);
                break;
        }
    }

    // 장애물 상태를 읽어 픽업 on off 규칙 적용
    private void ApplyPickupRules()
    {
        // 모두 끈 뒤 선택 타입의 항상 on 인덱스부터 켠다
        ResetAllPickups();
        AlwaysOnForSelectedPickup();

        bool ob0 = IsObstacleActive(0);
        bool ob1 = IsObstacleActive(1);
        bool ob2 = IsObstacleActive(2);
        bool longOn = longObstacle && longObstacle.activeSelf;

        // 규칙을 선택된 픽업 배열에 적용
        switch (pickupMode)
        {
            case PickupMode.Jelly:
                ApplyRuleToArray(jellies, ob0, ob1, ob2, longOn);
                break;
            case PickupMode.Silver:
                ApplyRuleToArray(silverCoins, ob0, ob1, ob2, longOn);
                break;
            case PickupMode.Gold:
                ApplyRuleToArray(goldCoins, ob0, ob1, ob2, longOn);
                break;
        }
    }

    // 공통 규칙을 지정된 배열에 적용
    private void ApplyRuleToArray(GameObject[] arr, bool ob0, bool ob1, bool ob2, bool longOn)
    {
        if (arr == null) return;

        // 0번 장애물 활성화 시 3 on, 2 off / 비활성화면 반대
        SafeSetActive(arr, 3, ob0);
        SafeSetActive(arr, 2, !ob0);

        // 1번 장애물 활성화 시 5 on, 4 off / 비활성화면 반대
        SafeSetActive(arr, 5, ob1);
        SafeSetActive(arr, 4, !ob1);

        // Long 장애물 활성화 시 4,5 off, 6 on
        if (longOn)
        {
            SafeSetActive(arr, 4, false);
            SafeSetActive(arr, 5, false);
            SafeSetActive(arr, 6, true);
        }

        // 2번 장애물 활성화 시 8 on, 7 off / 비활성화면 반대
        SafeSetActive(arr, 8, ob2);
        SafeSetActive(arr, 7, !ob2);
    }

    private bool IsObstacleActive(int idx)
    {
        if (obstacles == null) return false;
        if (idx < 0 || idx >= obstacles.Length) return false;
        return obstacles[idx] != null && obstacles[idx].activeSelf;
    }

    private void SetAll(GameObject[] arr, bool on)
    {
        if (arr == null) return;
        for (int i = 0; i < arr.Length; ++i)
            if (arr[i] != null) arr[i].SetActive(on);
    }

    private void SafeSetActive(GameObject[] arr, int idx, bool on)
    {
        if (arr == null) return;
        if (idx < 0 || idx >= arr.Length) return;
        if (arr[idx] == null) return;
        arr[idx].SetActive(on);
    }
}
