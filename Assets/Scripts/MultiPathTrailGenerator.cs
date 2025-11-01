using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 여러 개의 경로를 관리하고, 각 경로 종류(젤리/은/금)에 맞는 프리팹을
/// 일정 간격으로 배치하는 젤리길(코인도) 제너레이터.
/// - 인스펙터/씬뷰에서 실시간 편집(에디터 툴과 함께 사용)
/// - 마우스 클릭/드래그 + 좌표(시작/종료) 생성
/// - 각도 스냅(0/45/90/…)
/// - 경로 수정 시 전체 경로를 다시 그려(기존 생성물 제거 후 재생성)
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class MultiPathTrailGenerator : MonoBehaviour
{
    public enum PathKind { Jelly, SilverCoin, GoldCoin, Custom }

    [Serializable]
    public class PrefabMap
    {
        public PathKind kind = PathKind.Jelly;
        public GameObject prefab;
    }

    [Serializable]
    public class PathDef
    {
        public PathKind kind = PathKind.Jelly;
        public GameObject prefabOverride;
        public Color lineColor = new Color(1f, 0.9f, 0.1f, 1f);
        [Min(0.05f)] public float spacing = 1.0f;
        [Min(0f)] public float startOffset = 0f;
        public bool includeEndIfFits = true;
        public List<Vector3> points = new() { Vector3.zero, Vector3.right * 5f };
        public Transform pathSpawnRoot;

        // ▼ 추가: 영구 식별자 (인덱스 대신 GUID)
        [SerializeField, HideInInspector] public string pathGuid = "";

        [HideInInspector] public bool foldout = true;
    }

    [Header("Parent / Prefabs")]
    [Tooltip("모든 생성 오브젝트의 상위 부모. 비우면 자동으로 만들어요.")]
    public Transform globalSpawnRoot;

    [Tooltip("경로 종류별 기본 프리팹 매핑(경로 개별 prefabOverride가 더 우선)")]
    public List<PrefabMap> prefabMaps = new List<PrefabMap>();

    [Header("Realtime / Angle Snap")]
    [Tooltip("인스펙터/씬에서 값이 바뀌면 자동으로 전체 재생성")]
    public bool autoRegenOnEdit = true;

    [Tooltip("마우스 그리기 시 수평/수직/대각(45°)으로 각도 스냅")]
    public bool angleSnap = true;

    [Tooltip("스냅 각도(도 단위)")]
    public float[] snapAnglesDeg = new float[] { 0, 45, 90, 135, 180, 225, 270, 315 };

    [Tooltip("씬뷰 라인 두께(미리보기)")]
    public float gizmoLineThickness = 3f;

    [Header("Paths")]
    public List<PathDef> paths = new List<PathDef>();

    // 정리용 마커
    private const string kSpawnMarkerTag = "MultiPathTrail_Spawned";
    [Serializable] private class SpawnMarker : MonoBehaviour
    {
        public string tagName = kSpawnMarkerTag;
        public string pathGuid = "";
    }

    [Header("Generation Mode")]
    [Tooltip("에디터 미리보기 인스턴스를 씬에 저장하지 않음")]
    public bool dontSavePreviewInEditor = true;

    private void Awake()
    {
        EnsureGlobalRoot();
        // 런타임(플레이 중)엔 무조건 전체 재생성
        if (Application.isPlaying)
            RegenerateAll();
    }

    private void Reset()
    {
        EnsureGlobalRoot();

        if (paths.Count == 0)
        {
            var p = CreateEmptyPath(PathKind.Jelly);
            p.points = new List<Vector3>() { transform.position, transform.position + Vector3.right * 5f };
            paths.Add(p);
        }
        if (prefabMaps.Count == 0)
        {
            prefabMaps.Add(new PrefabMap(){ kind = PathKind.Jelly });
            prefabMaps.Add(new PrefabMap(){ kind = PathKind.SilverCoin });
            prefabMaps.Add(new PrefabMap(){ kind = PathKind.GoldCoin });
        }
    }

    private void OnEnable()
    {
        EnsureGlobalRoot();
        EnsurePathGuids();            // ▼ GUID 보장
    }

    private void OnValidate()
    {
        EnsureGlobalRoot();
        EnsurePathGuids();            // ▼ GUID 보장
        if (autoRegenOnEdit) RegenerateAll();
    }
    
    private void EnsurePathGuids()
    {
        foreach (var p in paths)
            if (string.IsNullOrEmpty(p.pathGuid))
                p.pathGuid = System.Guid.NewGuid().ToString("N");
    }

    private void EnsureGlobalRoot()
    {
        if (!globalSpawnRoot)
        {
            var t = transform.Find("SpawnRoot_AllPaths");
            if (t) globalSpawnRoot = t;
            else
            {
                var go = new GameObject("SpawnRoot_AllPaths");
                go.transform.SetParent(transform, false);
                globalSpawnRoot = go.transform;
            }
        }
    }

    // ========= Factory (에디터에서 new 대신 호출) =========
    public PathDef CreatePath(PathKind kind, Vector3 start, Vector3 end)
    {
        var p = new PathDef
        {
            kind = kind,
            spacing = 1.0f,
            startOffset = 0f,
            includeEndIfFits = true,
            points = new List<Vector3> { start, end }
        };
        p.lineColor = kind switch
        {
            PathKind.Jelly      => new Color(1f, 0.85f, 0.1f, 1f),
            PathKind.SilverCoin => new Color(0.75f, 0.85f, 1f, 1f),
            PathKind.GoldCoin   => new Color(1f, 0.9f, 0.3f, 1f),
            _                   => Color.white
        };
        return p;
    }
    public PathDef CreateEmptyPath(PathKind kind) => CreatePath(kind, transform.position, transform.position + Vector3.right * 5f);

    // ========= Public API =========
    public void RegenerateAll()
    {
        EnsureGlobalRoot();
        EnsurePathGuids();

        // ▼ 전역 스윕: 현 목록에 없는 고아 Path_* 루트 정리
        SweepOrphanPathRoots();

        // ▼ 각 경로별 루트 보장 + 자식 정리 후 생성
        for (int i = 0; i < paths.Count; i++)
        {
            EnsurePathSpawnRoot(i);
            ClearPath(i);     // GUID 기반 삭제
            GeneratePath(i);  // 다시 생성
        }
    }

    public void ClearAll()
    {
        for (int i = 0; i < paths.Count; i++) ClearPath(i);
    }

    public void RemovePath(int index)
    {
        if (index < 0 || index >= paths.Count) return;
        ClearPath(index);
        paths.RemoveAt(index);
        if (autoRegenOnEdit) RegenerateAll();
    }

    public void ClearPath(int index)
    {
        if (index < 0 || index >= paths.Count) return;
        var p = paths[index];
        if (!p.pathSpawnRoot) return;

        var del = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < p.pathSpawnRoot.childCount; i++)
        {
            var ch = p.pathSpawnRoot.GetChild(i);
            var mk = ch ? ch.GetComponent<SpawnMarker>() : null;

            // ▼ dontSavePreviewInEditor일 때: 에디터 미리보기는 전부 제거
            if (!Application.isPlaying && dontSavePreviewInEditor)
            {
                del.Add(ch.gameObject);
                continue;
            }

            // ▼ 런타임/퍼시스트 모드: GUID 일치하는 것만 제거
            if (mk != null && mk.tagName == kSpawnMarkerTag && mk.pathGuid == p.pathGuid)
                del.Add(ch.gameObject);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
            foreach (var go in del) Undo.DestroyObjectImmediate(go);
        else
            foreach (var go in del) Destroy(go);
#else
    foreach (var go in del) Destroy(go);
#endif
    }

    public void GeneratePath(int index)
    {
        if (index < 0 || index >= paths.Count) return;
        var p = paths[index];
        var prefab = ResolvePrefab(p);
        if (!prefab) { Debug.LogWarning($"Path {index} prefab missing"); return; }

        EnsurePathSpawnRoot(index);
        // ClearPath는 RegenerateAll에서 이미 호출됨. (중복호출 안전)

        var path = p.points;
        if (path == null || path.Count < 2) return;

        var cum = BuildCumulative(path, out var totalLen);
        if (totalLen <= 1e-5f) return;

        float step = Mathf.Max(0.05f, p.spacing);
        float t0 = Mathf.Clamp(p.startOffset, 0f, totalLen);
        int count = p.includeEndIfFits
            ? Mathf.FloorToInt((totalLen - t0) / step) + 1
            : Mathf.FloorToInt((totalLen - t0) / step);

        for (int i = 0; i < count; i++)
        {
            float d = t0 + i * step;
            if (d > totalLen + 1e-6f) break;

            var pos = EvaluateAtDistance(path, cum, d);

            GameObject go;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, p.pathSpawnRoot);
                go.transform.position = pos;

                // ▼ 에디터 미리보기는 저장 금지(씬에 남지 않음)
                if (dontSavePreviewInEditor)
                    go.hideFlags |= HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }
            else
#endif
            {
                go = Instantiate(prefab, pos, Quaternion.identity, p.pathSpawnRoot);
            }

            var mk = go.GetComponent<SpawnMarker>(); if (!mk) mk = go.AddComponent<SpawnMarker>();
            mk.tagName = kSpawnMarkerTag;
            mk.pathGuid = p.pathGuid;  // ▼ 인덱스 대신 GUID 박제
        }
    }

    public void EnsurePathSpawnRoot(int index)
    {
        if (index < 0 || index >= paths.Count) return;
        var p = paths[index];
        if (!p.pathSpawnRoot)
        {
            var go = new GameObject($"Path_{index:00}_{p.kind}");
            go.transform.SetParent(globalSpawnRoot, false);
            p.pathSpawnRoot = go.transform;
        }
    }

    public GameObject ResolvePrefab(PathDef p)
    {
        if (p.prefabOverride) return p.prefabOverride;
        foreach (var m in prefabMaps) if (m.kind == p.kind) return m.prefab;
        return null;
    }

    private void SweepOrphanPathRoots()
    {
        if (!globalSpawnRoot) return;

        // 현재 존재하는 모든 pathSpawnRoot 모음
        var liveRoots = new System.Collections.Generic.HashSet<Transform>();
        foreach (var p in paths) if (p.pathSpawnRoot) liveRoots.Add(p.pathSpawnRoot);

        // 전역 루트의 자식 중 liveRoots에 없는 것 → 고아로 삭제
        var toDelete = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < globalSpawnRoot.childCount; i++)
        {
            var ch = globalSpawnRoot.GetChild(i);
            if (!liveRoots.Contains(ch)) toDelete.Add(ch.gameObject);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
            foreach (var go in toDelete) Undo.DestroyObjectImmediate(go);
        else
            foreach (var go in toDelete) Destroy(go);
#else
    foreach (var go in toDelete) Destroy(go);
#endif
    }

    // ========= Path math (폴리라인 거리 보간) =========
    private List<float> BuildCumulative(List<Vector3> pts, out float total)
    {
        var cum = new List<float>(pts.Count);
        cum.Add(0f);
        total = 0f;
        for (int i = 1; i < pts.Count; i++)
        {
            float seg = Vector3.Distance(pts[i - 1], pts[i]);
            total += seg; cum.Add(total);
        }
        return cum;
    }

    private Vector3 EvaluateAtDistance(List<Vector3> pts, List<float> cum, float d)
    {
        d = Mathf.Clamp(d, 0f, cum[cum.Count - 1]);
        int hi = cum.BinarySearch(d);
        if (hi < 0) hi = ~hi;
        if (hi <= 0) return pts[0];
        if (hi >= cum.Count) return pts[pts.Count - 1];
        float d1 = cum[hi - 1], d2 = cum[hi];
        float u = Mathf.Approximately(d2, d1) ? 0f : (d - d1) / (d2 - d1);
        return Vector3.Lerp(pts[hi - 1], pts[hi], u);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (paths == null) return;
        for (int i = 0; i < paths.Count; i++)
        {
            var p = paths[i];
            if (p.points == null || p.points.Count < 2) continue;

            Handles.color = p.lineColor;
            for (int k = 1; k < p.points.Count; k++)
                Handles.DrawAAPolyLine(gizmoLineThickness, p.points[k - 1], p.points[k]);

            Gizmos.color = p.lineColor;
            foreach (var v in p.points) Gizmos.DrawSphere(v, 0.06f);
        }
    }
#endif
}
