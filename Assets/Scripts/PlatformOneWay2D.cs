using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 플랫폼을 "윗면만 충돌(아래/옆 통과)"로 자동 구성.
/// - PlatformEffector2D 보장
/// - 자식에 EdgeCollider2D("TopEdge") 한 줄만 만들어 윗면만 충돌
/// - SpriteRenderer/BoxCollider2D 크기를 참고해 윗변 길이 자동 세팅
/// 프리팹/씬 어디든 이 컴포넌트만 붙이면 됩니다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class PlatformOneWay2D : MonoBehaviour
{
    [Header("Size Reference (optional)")]
    [Tooltip("TopEdge 길이를 잡을 기준. 비우면 자동으로 SpriteRenderer → BoxCollider2D 순서로 사용.")]
    public SpriteRenderer sizeFromSprite;
    public BoxCollider2D   sizeFromBox;

    [Header("Offsets")]
    [Tooltip("윗변 Y 위치 보정(월드 좌표)")]
    public float topYWorldOffset = 0f;
    [Tooltip("윗변 좌/우를 살짝 안쪽으로 줄여 옆 스냅 방지(+오른쪽, -왼쪽)")]
    public float topXMargin = 0.03f;

    [Header("Effector")]
    [Range(10f, 180f)]
    [Tooltip("윗면으로 간주할 각도(도). 150~180 권장")]
    public float surfaceArc = 160f;
    [Tooltip("여러 콜라이더가 Effector에 묶일 때 그룹핑 사용 권장")]
    public bool useOneWayGrouping = true;

    const string kChildName = "TopEdge";
    PlatformEffector2D eff;
    EdgeCollider2D topEdge;

    void OnEnable()   => EnsureSetup();
    void OnValidate() => EnsureSetup();

    void EnsureSetup()
    {
        if (!isActiveAndEnabled) return;

        // 1) Effector 보장 및 세팅
        eff = GetComponent<PlatformEffector2D>();
        if (!eff) eff = gameObject.AddComponent<PlatformEffector2D>();
        eff.useOneWay = true;
        eff.useOneWayGrouping = useOneWayGrouping;
        eff.surfaceArc = surfaceArc;
        eff.rotationalOffset = 0f;
        eff.useSideBounce = false;
        eff.useSideFriction = false;

        // 2) TopEdge 객체 보장
        var tChild = transform.Find(kChildName);
        if (!tChild)
        {
            var go = new GameObject(kChildName);
            go.transform.SetParent(transform, false);
            tChild = go.transform;
        }
        topEdge = tChild.GetComponent<EdgeCollider2D>();
        if (!topEdge) topEdge = tChild.gameObject.AddComponent<EdgeCollider2D>();
        topEdge.usedByEffector = true;
        topEdge.isTrigger = false;

        // 3) 윗변 길이/위치 계산
        Bounds refBounds;
        if (!TryGetRefBounds(out refBounds))
        {
            // 기본 1유닛 폭
            Vector2 p0 = (Vector2)transform.position + new Vector2(-0.5f + topXMargin, topYWorldOffset);
            Vector2 p1 = (Vector2)transform.position + new Vector2( 0.5f - topXMargin, topYWorldOffset);
            SetEdgeWorld(p0, p1);
        }
        else
        {
            float left  = refBounds.min.x + topXMargin;
            float right = refBounds.max.x - topXMargin;
            float y     = refBounds.max.y + topYWorldOffset;
            SetEdgeWorld(new Vector2(left, y), new Vector2(right, y));
        }

        // 4) 다른 콜라이더는 Effector 미사용/트리거로 전환 → 옆/아래 충돌 제거
        foreach (var col in GetComponentsInChildren<Collider2D>(true))
        {
            if (col == topEdge) continue;
            if (col == sizeFromBox) continue; // 사이즈 참고용 Box는 남겨도 됨(충돌 제외만)
            col.usedByEffector = false;
            if (col is BoxCollider2D b) b.isTrigger = true;
        }
    }

    void SetEdgeWorld(Vector2 a, Vector2 b)
    {
        Vector2 la = (Vector2)topEdge.transform.InverseTransformPoint(a);
        Vector2 lb = (Vector2)topEdge.transform.InverseTransformPoint(b);
        if (topEdge.pointCount != 2 || topEdge.points[0] != la || topEdge.points[1] != lb)
            topEdge.points = new[] { la, lb };
    }

    bool TryGetRefBounds(out Bounds bounds)
    {
        // 우선순위: 수동 Sprite → 수동 Box → 자식 SpriteRenderer → 본체 BoxCollider2D
        if (sizeFromSprite && sizeFromSprite.sprite)
        { bounds = sizeFromSprite.bounds; return true; }

        if (sizeFromBox)
        { bounds = WorldBoundsFrom(sizeFromBox); return true; }

        var sr = GetComponentsInChildren<SpriteRenderer>()
                 .FirstOrDefault(s => s.enabled && s.sprite);
        if (sr)
        { bounds = sr.bounds; return true; }

        if (TryGetComponent(out BoxCollider2D box))
        { bounds = WorldBoundsFrom(box); return true; }

        bounds = default;
        return false;
    }

    Bounds WorldBoundsFrom(BoxCollider2D box)
    {
        var t = box.transform;
        var center = (Vector2)t.TransformPoint(box.offset);
        var size = Vector2.Scale(box.size, new Vector2(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y)));
        return new Bounds(center, size);
    }
}
