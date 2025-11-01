using UnityEngine;

// 발판을 생성하고 주기적으로 재배치하는 스크립트
public class PlatformSpawner : MonoBehaviour {
    public GameObject platformPrefab; // 생성할 발판의 원본 프리팹
    public int count = 3; // 생성할 발판의 개수

    public float timeBetSpawnMin = 0.5f; // 다음 배치까지의 시간 간격 최솟값
    public float timeBetSpawnMax = 2.25f; // 다음 배치까지의 시간 간격 최댓값
    private float timeBetSpawn; // 다음 배치까지의 시간 간격

    private float xPos = 20f; // 배치할 위치의 x 값

    private GameObject[] platforms; // 미리 생성한 발판들
    private int currentIndex = 0; // 사용할 현재 순번의 발판

    private Vector2 poolPosition = new Vector2(0, -25); // 초반에 생성된 발판들을 화면 밖에 숨겨둘 위치
    private float lastSpawnTime; // 마지막 배치 시점

    [SerializeField] private int patternSpan = 8; // 몇 개마다 다음 패턴으로
    private int spawnCount = 0;                   // 누적 스폰 수

    void Start()
    {
        platforms = new GameObject[count];
        for (int i = 0; i < count; i++)
            platforms[i] = Instantiate(platformPrefab, poolPosition, Quaternion.identity);

        lastSpawnTime = 0f;
        timeBetSpawn = 0f;
    }

    void Update()
    {
        if (InGameManager.instance.isGameover) return;

        if (Time.time >= lastSpawnTime + timeBetSpawn)
        {
            lastSpawnTime = Time.time;

            float p = InGameManager.instance != null ? InGameManager.instance.Progress01 : 0f;
            timeBetSpawn = Mathf.Lerp(timeBetSpawnMin, timeBetSpawnMax, p);

            float yPos = -5f;

            // 재활용
            platforms[currentIndex].SetActive(false);
            platforms[currentIndex].SetActive(true);

            // 이번 플랫폼에 사용할 픽업 타입 먼저 결정
            var plat = platforms[currentIndex].GetComponent<Platform>();
            if (plat != null)
            {
                int pick = Random.Range(0, 3); // 0 Jelly, 1 Silver, 2 Gold
                var mode = (Platform.PickupMode)pick;
                plat.SetPickupMode(mode);

                // 장애물 패턴 계산 및 적용
                int phase = (spawnCount / Mathf.Max(1, patternSpan)) % 3; // 0,1,2 반복
                plat.ApplyPattern((Platform.ObstaclePattern)phase, spawnCount);
            }

            platforms[currentIndex].transform.position = new Vector2(xPos, yPos);

            currentIndex++;
            if (currentIndex >= count) currentIndex = 0;

            spawnCount++;
        }
    }
}
