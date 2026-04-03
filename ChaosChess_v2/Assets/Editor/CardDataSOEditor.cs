using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CardDataSO))]
public class CardDataSOEditor : Editor
{
    // 기본 정보
    SerializedProperty cardName;
    SerializedProperty cardImage;
    SerializedProperty cardDescription;
    SerializedProperty cardType;

    // 기물 타입
    SerializedProperty pieceType;
    SerializedProperty targetColorPiece;
    SerializedProperty pieceLimitTurn;
    SerializedProperty requiredPieceCount;

    // 타일 타입
    SerializedProperty tileCount;
    SerializedProperty maintainTurn;

    // 전역 타입
    SerializedProperty needTargetColor;
    SerializedProperty targetColor;
    SerializedProperty hasLimit;
    SerializedProperty limitTurn;

    // 부가 정보
    SerializedProperty needAdditionalDescription;
    SerializedProperty descriptionType;

    // 섹션 토글 상태
    bool showBaseInfo = true;
    bool showTypeSettings = true;
    bool showAdditionalInfo = true;

    // 스타일 캐시
    GUIStyle headerStyle;
    GUIStyle sectionBoxStyle;

    void OnEnable()
    {
        cardName = serializedObject.FindProperty("CardName");
        cardImage = serializedObject.FindProperty("CardImage");
        cardDescription = serializedObject.FindProperty("CardDescription");
        cardType = serializedObject.FindProperty("Type");

        pieceType = serializedObject.FindProperty("PieceType");
        targetColorPiece = serializedObject.FindProperty("PieceTargetColor");
        pieceLimitTurn = serializedObject.FindProperty("PieceLimitTurn");
        requiredPieceCount = serializedObject.FindProperty("RequiredPieceCount");

        tileCount = serializedObject.FindProperty("TileCount");
        maintainTurn = serializedObject.FindProperty("MaintainTurn");

        needTargetColor = serializedObject.FindProperty("NeedTargetColor");
        targetColor = serializedObject.FindProperty("GlobalTargetColor");
        hasLimit = serializedObject.FindProperty("HasLimit");
        limitTurn = serializedObject.FindProperty("LimitTurn");

        needAdditionalDescription = serializedObject.FindProperty("NeedAdditionalDescription");
        descriptionType = serializedObject.FindProperty("DescriptionType");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        InitStyles();

        // ── 기본 카드 정보 ──────────────────────────────
        showBaseInfo = DrawSectionHeader("기본 카드 정보", showBaseInfo);
        if (showBaseInfo)
        {
            using (new EditorGUILayout.VerticalScope(sectionBoxStyle))
            {
                EditorGUILayout.PropertyField(cardName, new GUIContent("카드 이름"));
                EditorGUILayout.PropertyField(cardImage, new GUIContent("카드 이미지"));
                EditorGUILayout.PropertyField(cardDescription, new GUIContent("카드 설명"));
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(cardType, new GUIContent("카드 타입"));
            }
        }

        EditorGUILayout.Space(6);

        // ── 타입별 설정 ─────────────────────────────────
        showTypeSettings = DrawSectionHeader(GetTypeSettingLabel((CardType)cardType.enumValueIndex), showTypeSettings);
        if (showTypeSettings)
        {
            using (new EditorGUILayout.VerticalScope(sectionBoxStyle))
            {
                DrawTypeSpecificFields((CardType)cardType.enumValueIndex);
            }
        }

        EditorGUILayout.Space(6);

        // ── 부가 정보 설정 ──────────────────────────────
        showAdditionalInfo = DrawSectionHeader("부가 정보 설정", showAdditionalInfo);
        if (showAdditionalInfo)
        {
            using (new EditorGUILayout.VerticalScope(sectionBoxStyle))
            {
                EditorGUILayout.PropertyField(needAdditionalDescription, new GUIContent("부가 설명 필요"));

                if (needAdditionalDescription.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(descriptionType, new GUIContent("설명 타입"));
                    EditorGUI.indentLevel--;
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    // ── 타입별 필드 렌더링 ───────────────────────────────
    void DrawTypeSpecificFields(CardType type)
    {
        switch (type)
        {
            case CardType.Piece:
                DrawPieceFields();
                break;

            case CardType.Tile:
                DrawTileFields();
                break;

            case CardType.Global:
                DrawGlobalFields();
                break;

            default:
                HelpBox("카드 타입을 선택하면 관련 설정이 표시됩니다.", MessageType.Info);
                break;
        }
    }

    void DrawPieceFields()
    {
        HelpBox("기물 카드: 특정 기물을 배치하거나 조합하는 카드입니다.", MessageType.None);
        EditorGUILayout.Space(2);
        EditorGUILayout.PropertyField(pieceType, new GUIContent("기물 종류"));
        EditorGUILayout.PropertyField(targetColorPiece, new GUIContent("대상 색상"));
        EditorGUILayout.PropertyField(pieceLimitTurn, new GUIContent("효과 유지 턴"));
        if (pieceLimitTurn.intValue == -1)
        {
            EditorGUILayout.HelpBox("-1 : 턴 제한 없이 계속 유지됩니다.", MessageType.Warning);
        }
        EditorGUILayout.PropertyField(requiredPieceCount, new GUIContent("필요 기물 수"));
    }

    void DrawTileFields()
    {
        HelpBox("타일 카드: 타일에 효과를 부여하는 카드입니다.", MessageType.None);
        EditorGUILayout.Space(2);
        EditorGUILayout.PropertyField(tileCount, new GUIContent("타일 개수"));

        EditorGUILayout.PropertyField(maintainTurn, new GUIContent("유지 턴 수"));
        if (maintainTurn.intValue == -1)
        {
            EditorGUILayout.HelpBox("-1 : 제한 없이 계속 유지됩니다.", MessageType.Warning);
        }
    }

    void DrawGlobalFields()
    {
        HelpBox("전역 카드: 게임 전체에 영향을 주는 카드입니다.", MessageType.None);
        EditorGUILayout.Space(2);
        EditorGUILayout.PropertyField(needTargetColor, new GUIContent("대상 색상 필요"));
        if (needTargetColor.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(targetColor, new GUIContent("대상 색상"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(hasLimit, new GUIContent("턴 제한 여부"));

        if (hasLimit.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(limitTurn, new GUIContent("제한 턴 수"));
            EditorGUI.indentLevel--;
        }
    }

    // ── UI 헬퍼 ─────────────────────────────────────────
    bool DrawSectionHeader(string label, bool foldout)
    {
        Rect rect = GUILayoutUtility.GetRect(16f, 24f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f, 1f));

        // 접기 화살표 + 라벨
        foldout = EditorGUI.Foldout(
            new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 4),
            foldout, label, true, headerStyle);

        EditorGUILayout.Space(2);
        return foldout;
    }

    void HelpBox(string msg, MessageType type)
    {
        EditorGUILayout.HelpBox(msg, type);
    }

    string GetTypeSettingLabel(CardType type) => type switch
    {
        CardType.Piece => "기물 타입 설정",
        CardType.Tile => "타일 타입 설정",
        CardType.Global => "전역 타입 설정",
        _ => "타입 설정",
    };

    void InitStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
            };
            headerStyle.normal.textColor = Color.white;
            headerStyle.onNormal.textColor = Color.white;
        }

        if (sectionBoxStyle == null)
        {
            sectionBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 8, 8),
            };
        }
    }
}