#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MultiPathTrailGenerator))]
public class MultiPathTrailGeneratorEditor : Editor
{
    MultiPathTrailGenerator G;
    bool mouseEditEnabled = false;  // 인스펙터 버튼으로 on/off
    int currentPath = 0;

    // 좌표 생성 입력
    Vector3 coordStart;
    Vector3 coordEnd;

    // 드로잉 상태
    bool isDrawing = false; // 현재 경로에 새 점을 그리고 있는가
    Vector3 drawingPreview; // 미리보기 점

    void OnEnable()
    {
        G = (MultiPathTrailGenerator)target;
        Tools.hidden = false;
    }
    void OnDisable() { Tools.hidden = false; }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("CookieRun Jelly Trail / Coin Path Tool", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("globalSpawnRoot"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("autoRegenOnEdit"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("angleSnap"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoLineThickness"));

        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("prefabMaps"), true);

        EditorGUILayout.Space(6);
        DrawMouseToggleGUI();

        EditorGUILayout.Space(6);
        DrawPathsListGUI();

        EditorGUILayout.Space(6);
        DrawCoordinateAdderGUI();

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Regenerate ALL", GUILayout.Height(26)))
            {
                Undo.RegisterFullObjectHierarchyUndo(G.gameObject, "Regenerate ALL");
                G.RegenerateAll();
            }
            if (GUILayout.Button("Clear ALL", GUILayout.Height(26)))
            {
                Undo.RegisterFullObjectHierarchyUndo(G.gameObject, "Clear ALL");
                G.ClearAll();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    void DrawMouseToggleGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Mouse Path Editing", EditorStyles.boldLabel);
            GUI.backgroundColor = mouseEditEnabled ? new Color(0.7f, 1f, 0.7f) : Color.white;
            if (GUILayout.Button(mouseEditEnabled ? "ON (Click to turn OFF)" : "OFF (Click to turn ON)", GUILayout.Height(24)))
            {
                mouseEditEnabled = !mouseEditEnabled;
                isDrawing = false;
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.HelpBox(
            "ON: 씬뷰에서 좌클릭으로 점 추가(Shift=끝에 추가 / 기본=근접 세그먼트에 삽입), 드래그로 미리보기, 우클릭으로 점 삭제.\n" +
            "각도 스냅: 수평/수직/대각(45°).", MessageType.Info);
    }

    void DrawPathsListGUI()
    {
        var paths = G.paths;
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Paths (Count: {paths.Count})", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Add (Jelly)", GUILayout.Width(110)))
            {
                Undo.RegisterCompleteObjectUndo(G, "Add Path");
                var p = G.CreateEmptyPath(MultiPathTrailGenerator.PathKind.Jelly);
                G.paths.Add(p);
                currentPath = G.paths.Count - 1;
                if (G.autoRegenOnEdit) G.RegenerateAll();
            }
            if (GUILayout.Button("+ Add (Silver)", GUILayout.Width(110)))
            {
                Undo.RegisterCompleteObjectUndo(G, "Add Path");
                var p = G.CreateEmptyPath(MultiPathTrailGenerator.PathKind.SilverCoin);
                G.paths.Add(p);
                currentPath = G.paths.Count - 1;
                if (G.autoRegenOnEdit) G.RegenerateAll();
            }
            if (GUILayout.Button("+ Add (Gold)", GUILayout.Width(110)))
            {
                Undo.RegisterCompleteObjectUndo(G, "Add Path");
                var p = G.CreateEmptyPath(MultiPathTrailGenerator.PathKind.GoldCoin);
                G.paths.Add(p);
                currentPath = G.paths.Count - 1;
                if (G.autoRegenOnEdit) G.RegenerateAll();
            }
        }

        if (paths.Count == 0) return;

        currentPath = Mathf.Clamp(currentPath, 0, paths.Count - 1);
        currentPath = EditorGUILayout.IntSlider("Current Path", currentPath, 0, paths.Count - 1);

        var pth = paths[currentPath];
        pth.foldout = EditorGUILayout.Foldout(pth.foldout, $"Path #{currentPath} ({pth.kind})", true);
        if (pth.foldout)
        {
            EditorGUI.indentLevel++;

            pth.kind = (MultiPathTrailGenerator.PathKind)EditorGUILayout.EnumPopup("Kind", pth.kind);
            pth.prefabOverride = (GameObject)EditorGUILayout.ObjectField("Prefab Override", pth.prefabOverride, typeof(GameObject), false);
            pth.lineColor = EditorGUILayout.ColorField("Line Color", pth.lineColor);
            pth.spacing = EditorGUILayout.Slider("Spacing", pth.spacing, 0.05f, 5f);
            pth.startOffset = EditorGUILayout.Slider("Start Offset", pth.startOffset, 0f, 5f);
            pth.includeEndIfFits = EditorGUILayout.Toggle("Include End If Fits", pth.includeEndIfFits);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Regenerate This", GUILayout.Height(22)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(G.gameObject, "Regen Path");
                    G.RegenerateAll(); // 요구사항: 하나 수정해도 전체 재생성
                }
                if (GUILayout.Button("Clear This", GUILayout.Height(22)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(G.gameObject, "Clear Path");
                    G.ClearPath(currentPath);
                }
                if (GUILayout.Button("Remove This", GUILayout.Height(22)))
                {
                    Undo.RegisterCompleteObjectUndo(G, "Remove Path");
                    G.RemovePath(currentPath);
                    currentPath = Mathf.Clamp(currentPath - 1, 0, Mathf.Max(0, G.paths.Count - 1));
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Points (World)", EditorStyles.boldLabel);
            for (int i = 0; i < pth.points.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    pth.points[i] = EditorGUILayout.Vector3Field($"P{i}", pth.points[i]);
                    if (GUILayout.Button("X", GUILayout.Width(24)))
                    {
                        Undo.RegisterCompleteObjectUndo(G, "Remove Point");
                        pth.points.RemoveAt(i);
                        i--;
                        if (G.autoRegenOnEdit) G.RegenerateAll();
                    }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Add Point (End)"))
                {
                    Undo.RegisterCompleteObjectUndo(G, "Add Point");
                    var last = pth.points.Count > 0 ? pth.points[pth.points.Count - 1] : G.transform.position;
                    pth.points.Add(last + Vector3.right * 1f);
                    if (G.autoRegenOnEdit) G.RegenerateAll();
                }
                if (GUILayout.Button("+ Insert Mid"))
                {
                    Undo.RegisterCompleteObjectUndo(G, "Insert Mid");
                    if (pth.points.Count >= 2)
                    {
                        int idx = Mathf.Max(0, pth.points.Count / 2);
                        var mid = (pth.points[idx] + pth.points[Mathf.Min(idx + 1, pth.points.Count - 1)]) * 0.5f;
                        pth.points.Insert(idx + 1, mid);
                    }
                    else pth.points.Add(G.transform.position + Vector3.right * 1f);
                    if (G.autoRegenOnEdit) G.RegenerateAll();
                }
            }
            EditorGUI.indentLevel--;
        }
    }

    void DrawCoordinateAdderGUI()
    {
        EditorGUILayout.LabelField("Add Straight Path by Coordinates", EditorStyles.boldLabel);
        coordStart = EditorGUILayout.Vector3Field("Start (World)", coordStart);
        coordEnd   = EditorGUILayout.Vector3Field("End (World)", coordEnd);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Jelly Path"))
                AddStraightPath(MultiPathTrailGenerator.PathKind.Jelly);
            if (GUILayout.Button("Add Silver Path"))
                AddStraightPath(MultiPathTrailGenerator.PathKind.SilverCoin);
            if (GUILayout.Button("Add Gold Path"))
                AddStraightPath(MultiPathTrailGenerator.PathKind.GoldCoin);
        }
    }

    void AddStraightPath(MultiPathTrailGenerator.PathKind kind)
    {
        Undo.RegisterCompleteObjectUndo(G, "Add Straight Path");
        var p = G.CreatePath(kind, coordStart, coordEnd);
        G.paths.Add(p);
        currentPath = G.paths.Count - 1;
        if (G.autoRegenOnEdit) G.RegenerateAll();
    }

    // ===== 씬뷰 마우스 상호작용 =====
    void OnSceneGUI()
    {
        if (!mouseEditEnabled) return;
        if (G.paths == null || G.paths.Count == 0) return;

        currentPath = Mathf.Clamp(currentPath, 0, G.paths.Count - 1);
        var p = G.paths[currentPath];
        if (p.points == null) p.points = new System.Collections.Generic.List<Vector3>();

        var evt = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlId);

        // 미리보기(마지막 점 기준 각도 스냅)
        if (p.points.Count > 0)
        {
            var mouseWorld = GetMouseWorldOnZ0(evt.mousePosition);
            drawingPreview = SnapFrom(p.points[p.points.Count - 1], mouseWorld);
            Handles.color = new Color(p.lineColor.r, p.lineColor.g, p.lineColor.b, 0.75f);
            Handles.DrawAAPolyLine(G.gizmoLineThickness, p.points[p.points.Count - 1], drawingPreview);
        }

        // 기존 라인 및 점 핸들
        Handles.color = p.lineColor;
        for (int i = 1; i < p.points.Count; i++)
            Handles.DrawAAPolyLine(G.gizmoLineThickness, p.points[i - 1], p.points[i]);
        for (int i = 0; i < p.points.Count; i++)
        {
            EditorGUI.BeginChangeCheck();
            var fmh_257_30_638959945086318221 = Quaternion.identity; var newPos = Handles.FreeMoveHandle(
                p.points[i],
                HandleUtility.GetHandleSize(p.points[i]) * 0.08f,
                Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RegisterCompleteObjectUndo(G, "Move Point");
                // 이동도 스냅 적용 (이전 점 기준)
                if (i > 0) newPos = SnapFrom(p.points[i - 1], newPos);
                p.points[i] = newPos;
                if (G.autoRegenOnEdit) G.RegenerateAll();
            }
            Handles.Label(p.points[i] + Vector3.up * 0.15f, $"P{i}", EditorStyles.boldLabel);
        }

        // 좌클릭: 점 추가(Shift=끝에 추가, 기본=근접 세그먼트에 삽입)
        if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt)
        {
            var hit = GetMouseWorldOnZ0(evt.mousePosition);
            if (p.points.Count == 0)
            {
                Undo.RegisterCompleteObjectUndo(G, "Add First Point");
                p.points.Add(hit);
            }
            else
            {
                var snapped = SnapFrom(p.points[p.points.Count - 1], hit);
                Undo.RegisterCompleteObjectUndo(G, "Add Point");
                if (evt.shift || p.points.Count < 2)
                    p.points.Add(snapped);
                else
                {
                    int seg = FindNearestSegment(p, hit);
                    p.points.Insert(Mathf.Clamp(seg + 1, 1, p.points.Count), snapped);
                }
            }
            evt.Use();
            if (G.autoRegenOnEdit) G.RegenerateAll();
        }
        // 우클릭: 가까운 점 삭제
        else if (evt.type == EventType.MouseDown && evt.button == 1)
        {
            int nearest = FindNearestPoint(p, evt.mousePosition);
            if (nearest >= 0)
            {
                Undo.RegisterCompleteObjectUndo(G, "Remove Point");
                p.points.RemoveAt(nearest);
                evt.Use();
                if (G.autoRegenOnEdit) G.RegenerateAll();
            }
        }
    }

    // ===== 유틸 =====
    Vector3 GetMouseWorldOnZ0(Vector2 mousePos)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        var plane = new Plane(Vector3.back, Vector3.zero); // Z=0
        if (plane.Raycast(ray, out float enter)) return ray.GetPoint(enter);
        return Vector3.zero;
    }

    Vector3 SnapFrom(Vector3 origin, Vector3 target)
    {
        if (!G.angleSnap) return target;
        Vector2 v = (Vector2)(target - origin);
        if (v.sqrMagnitude < 1e-6f) return target;

        float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        float best = 0f, min = float.MaxValue;
        foreach (var a in G.snapAnglesDeg)
        {
            float diff = Mathf.Abs(Mathf.DeltaAngle(ang, a));
            if (diff < min) { min = diff; best = a; }
        }
        float rad = best * Mathf.Deg2Rad;
        float len = v.magnitude;
        Vector2 snapped = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * len;
        return origin + (Vector3)snapped;
    }

    int FindNearestPoint(MultiPathTrailGenerator.PathDef p, Vector2 mousePos)
    {
        int idx = -1; float best = 15f; // 픽셀 기준
        for (int i = 0; i < p.points.Count; i++)
        {
            float d = Vector2.Distance(HandleUtility.WorldToGUIPoint(p.points[i]), mousePos);
            if (d < best) { best = d; idx = i; }
        }
        return idx;
    }

    int FindNearestSegment(MultiPathTrailGenerator.PathDef p, Vector3 worldPos)
    {
        int seg = 0; float best = float.MaxValue;
        for (int i = 0; i < p.points.Count - 1; i++)
        {
            float d = DistancePointToSegment(worldPos, p.points[i], p.points[i + 1]);
            if (d < best) { best = d; seg = i; }
        }
        return seg;
    }

    float DistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        var ap = p - a; var ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector3.Distance(a + ab * t, p);
    }
}
#endif
