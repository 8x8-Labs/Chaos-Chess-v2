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
    SerializedProperty cardTier;

    // 기물 타입
    SerializedProperty pieceType;
    SerializedProperty targetColorPiece;
    SerializedProperty pieceLimitTurn;
    SerializedProperty requiredPieceCount;

    // 타일 타입
    SerializedProperty tileCount;
    SerializedProperty maintainTurn;
    SerializedProperty needEffectTileBase;
    SerializedProperty effectTileBase;
    SerializedProperty useMultipleEffectTileBases;
    SerializedProperty effectTileBases;
    SerializedProperty effectTileAnimationMode;
    SerializedProperty effectTileFrameInterval;
    SerializedProperty effectTileAnimationFrames;
    SerializedProperty restrictTiles;
    SerializedProperty blockedTiles;

    // 전역 타입
    SerializedProperty needTargetColor;
    SerializedProperty targetColor;
    SerializedProperty hasLimit;
    SerializedProperty limitTurn;
    SerializedProperty statusDisplayType;

    // VFX 연출
    SerializedProperty vfxApply;
    SerializedProperty vfxLoop;
    SerializedProperty vfxHook;
    SerializedProperty vfxRevert;
    SerializedProperty vfxPlayApplyAnim;
    SerializedProperty vfxPlayHookAnim;
    SerializedProperty vfxAnimStrength;
    SerializedProperty vfxAnimDuration;

    // 부가 정보
    SerializedProperty needAdditionalDescription;
    SerializedProperty descriptionType;
    SerializedProperty additionalDescriptionTitle;
    SerializedProperty pieceDescImage;
    SerializedProperty movementImage;
    SerializedProperty pieceDescContent;
    SerializedProperty additionalDescriptionContent;

    // 섹션 토글 상태
    bool showBaseInfo = true;
    bool showVFX = true;
    bool showTypeSettings = true;
    bool showAdditionalInfo = true;

    // 스타일 캐시
    GUIStyle headerStyle;
    GUIStyle sectionBoxStyle;
    GUIStyle subSectionBoxStyle;

    void OnEnable()
    {
        cardName = serializedObject.FindProperty("CardName");
        cardImage = serializedObject.FindProperty("CardImage");
        cardDescription = serializedObject.FindProperty("CardDescription");
        cardType = serializedObject.FindProperty("Type");
        cardTier = serializedObject.FindProperty("CardTier");

        pieceType = serializedObject.FindProperty("PieceType");
        targetColorPiece = serializedObject.FindProperty("PieceTargetColor");
        pieceLimitTurn = serializedObject.FindProperty("PieceLimitTurn");
        requiredPieceCount = serializedObject.FindProperty("RequiredPieceCount");

        tileCount = serializedObject.FindProperty("TileCount");
        maintainTurn = serializedObject.FindProperty("MaintainTurn");
        needEffectTileBase = serializedObject.FindProperty("NeedEffectTileBase");
        effectTileBase = serializedObject.FindProperty("EffectTileBase");
        useMultipleEffectTileBases = serializedObject.FindProperty("UseMultipleEffectTileBases");
        effectTileBases = serializedObject.FindProperty("EffectTileBases");
        effectTileAnimationMode = serializedObject.FindProperty("EffectTileAnimationMode");
        effectTileFrameInterval = serializedObject.FindProperty("EffectTileFrameInterval");
        effectTileAnimationFrames = serializedObject.FindProperty("EffectTileAnimationFrames");
        restrictTiles = serializedObject.FindProperty("RestrictTiles");
        blockedTiles = serializedObject.FindProperty("BlockedTiles");

        needTargetColor = serializedObject.FindProperty("NeedTargetColor");
        targetColor = serializedObject.FindProperty("GlobalTargetColor");
        hasLimit = serializedObject.FindProperty("HasLimit");
        limitTurn = serializedObject.FindProperty("LimitTurn");
        statusDisplayType = serializedObject.FindProperty("StatusDisplayType");

        SerializedProperty vfx = serializedObject.FindProperty("VFX");
        vfxApply = vfx.FindPropertyRelative("ApplyVFXPrefab");
        vfxLoop = vfx.FindPropertyRelative("LoopVFXPrefab");
        vfxHook = vfx.FindPropertyRelative("HookVFXPrefab");
        vfxRevert = vfx.FindPropertyRelative("RevertVFXPrefab");
        vfxPlayApplyAnim = vfx.FindPropertyRelative("PlayApplyAnim");
        vfxPlayHookAnim = vfx.FindPropertyRelative("PlayHookAnim");
        vfxAnimStrength = vfx.FindPropertyRelative("AnimStrength");
        vfxAnimDuration = vfx.FindPropertyRelative("AnimDuration");

        needAdditionalDescription = serializedObject.FindProperty("NeedAdditionalDescription");
        descriptionType = serializedObject.FindProperty("DescriptionType");
        additionalDescriptionTitle = serializedObject.FindProperty("AdditionalDescriptionTitle");
        pieceDescImage = serializedObject.FindProperty("PieceDescImage");
        movementImage = serializedObject.FindProperty("MovementImage");
        pieceDescContent = serializedObject.FindProperty("PieceDescContent");
        additionalDescriptionContent = serializedObject.FindProperty("AdditionalDescriptionContent");
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
                EditorGUILayout.PropertyField(cardTier, new GUIContent("카드 등급"));
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(statusDisplayType, new GUIContent("상태 표시 타입"));
                DrawStatusDisplayPreview();
            }
        }

        EditorGUILayout.Space(6);

        // ── VFX 연출 설정 ───────────────────────────────
        showVFX = DrawSectionHeader("VFX 연출 설정", showVFX);
        if (showVFX)
        {
            using (new EditorGUILayout.VerticalScope(sectionBoxStyle))
            {
                DrawVFXFields();
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
                    EditorGUILayout.PropertyField(additionalDescriptionTitle, new GUIContent("부가 설명 제목"));

                    var type = (AdditionalDescription)descriptionType.enumValueIndex;
                    if (type == AdditionalDescription.Piece)
                    {
                        EditorGUILayout.PropertyField(pieceDescImage, new GUIContent("기물 이미지"));
                        EditorGUILayout.PropertyField(movementImage, new GUIContent("행마법 이미지"));
                        EditorGUILayout.PropertyField(pieceDescContent, new GUIContent("기물 설명 내용"));
                    }
                    else if (type == AdditionalDescription.Rule)
                    {
                        EditorGUILayout.PropertyField(additionalDescriptionContent, new GUIContent("규칙 내용"));
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    // ── VFX 연출 필드 렌더링 ─────────────────────────────
    void DrawVFXFields()
    {
        HelpBox("효과 적용/유지/소멸 및 게임 훅 발동 시 재생할 파티클·트윈 연출입니다. 비워두면 해당 시점 연출은 생략됩니다.", MessageType.None);

        EditorGUILayout.Space(4);
        using (new EditorGUILayout.VerticalScope(subSectionBoxStyle))
        {
            EditorGUILayout.LabelField("파티클 프리팹", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(vfxApply, new GUIContent("적용 시 (1회)"));
            EditorGUILayout.PropertyField(vfxLoop, new GUIContent("유지 중 (루프)"));
            EditorGUILayout.PropertyField(vfxHook, new GUIContent("훅 발동 시 (1회)"));
            EditorGUILayout.PropertyField(vfxRevert, new GUIContent("소멸 시 (1회)"));
        }

        EditorGUILayout.Space(4);
        using (new EditorGUILayout.VerticalScope(subSectionBoxStyle))
        {
            EditorGUILayout.LabelField("기본 트윈 (펀치/스케일)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(vfxPlayApplyAnim, new GUIContent("적용 시 펀치"));
            EditorGUILayout.PropertyField(vfxPlayHookAnim, new GUIContent("훅 시 펀치"));
            EditorGUILayout.PropertyField(vfxAnimStrength, new GUIContent("펀치 세기"));
            EditorGUILayout.PropertyField(vfxAnimDuration, new GUIContent("펀치 진행 시간(초)"));
        }
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

        EditorGUILayout.Space(6);
        HelpBox("효과 범위를 타일로 표시하려면 아래에서 타일을 지정하세요. (예: 무하한 3x3 범위)", MessageType.None);
        DrawTileEffectFields();
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

        DrawTileEffectFields();

        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(restrictTiles, new GUIContent("타일 제한 사용"));
        if (restrictTiles.boolValue)
        {
            // 배열 크기가 64이 아닌 경우 자동 조정
            if (blockedTiles.arraySize != 64)
                blockedTiles.arraySize = 64;

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("선택 불가 타일 설정 (체크 = 선택 불가)", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);
            DrawTileGrid();
        }
    }

    void DrawTileEffectFields()
    {
        EditorGUILayout.Space(6);
        using (new EditorGUILayout.VerticalScope(subSectionBoxStyle))
        {
            EditorGUILayout.LabelField("이펙트 타일 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(needEffectTileBase, new GUIContent("이펙트 타일 사용"));

            if (!needEffectTileBase.boolValue)
                return;

            DrawTileEffectAnimationFields();
            if ((TileEffectAnimationMode)effectTileAnimationMode.enumValueIndex == TileEffectAnimationMode.None)
                DrawTileEffectBaseFields();
        }
    }

    void DrawTileEffectBaseFields()
    {
        EditorGUILayout.Space(4);
        using (new EditorGUILayout.VerticalScope(subSectionBoxStyle))
        {
            EditorGUILayout.LabelField("타일 베이스", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(useMultipleEffectTileBases, new GUIContent("다중 타일 베이스 사용"));
            if (useMultipleEffectTileBases.boolValue)
                EditorGUILayout.PropertyField(effectTileBases, new GUIContent("타일 베이스 목록"), true);
            else
                EditorGUILayout.PropertyField(effectTileBase, new GUIContent("타일 베이스"));
        }
    }

    void DrawTileEffectAnimationFields()
    {
        EditorGUILayout.Space(4);
        using (new EditorGUILayout.VerticalScope(subSectionBoxStyle))
        {
            EditorGUILayout.LabelField("애니메이션", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(effectTileAnimationMode, new GUIContent("애니메이션 방식"));

            var mode = (TileEffectAnimationMode)effectTileAnimationMode.enumValueIndex;
            if (mode == TileEffectAnimationMode.None)
            {
                EditorGUILayout.HelpBox("애니메이션을 사용하지 않으면 아래 타일 베이스가 표시됩니다.", MessageType.None);
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(effectTileAnimationFrames, new GUIContent("프레임"), true);

            if (mode == TileEffectAnimationMode.Time)
            {
                EditorGUILayout.PropertyField(effectTileFrameInterval, new GUIContent("프레임 간격(초)"));
            }
            else if (mode == TileEffectAnimationMode.Turn)
            {
                EditorGUILayout.HelpBox("0번 프레임 = 시작, 1번 프레임 = 1턴 경과", MessageType.None);
            }

            if (effectTileAnimationFrames.arraySize == 0)
                EditorGUILayout.HelpBox("프레임이 비어 있으면 기본 타일 베이스가 표시됩니다.", MessageType.Warning);

            EditorGUI.indentLevel--;
        }
    }

    void DrawTileGrid()
    {
        float cellSize = 28f;
        float labelWidth = 14f;

        // y=7(8랭크)부터 y=0(1랭크) 순서로 위→아래 렌더링
        for (int y = 7; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField((y + 1).ToString(), GUILayout.Width(labelWidth));

            for (int x = 0; x < 8; x++)
            {
                int idx = y * 8 + x;
                SerializedProperty cell = blockedTiles.GetArrayElementAtIndex(idx);

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = cell.boolValue ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.3f, 0.8f, 0.3f);
                bool newVal = GUILayout.Toggle(cell.boolValue, "", GUILayout.Width(cellSize), GUILayout.Height(cellSize));
                GUI.backgroundColor = prev;

                cell.boolValue = newVal;
            }
            EditorGUILayout.EndHorizontal();
        }

        // 파일(열) 레이블
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(labelWidth + 4);
        foreach (string file in new[] { "a", "b", "c", "d", "e", "f", "g", "h" })
            EditorGUILayout.LabelField(file, GUILayout.Width(cellSize));
        EditorGUILayout.EndHorizontal();
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

    void DrawStatusDisplayPreview()
    {
        var type = (ActiveEffectStatusType)statusDisplayType.enumValueIndex;
        string preview = type switch
        {
            ActiveEffectStatusType.Active => "활성",
            ActiveEffectStatusType.Installed => "설치",
            ActiveEffectStatusType.TurnBased => "3턴",
            ActiveEffectStatusType.CountBased => "3회",
            _ => "활성"
        };

        EditorGUILayout.HelpBox($"카드 활성화 UI 표시 예시: {preview}", MessageType.Info);
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

        if (subSectionBoxStyle == null)
        {
            subSectionBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(0, 0, 4, 4),
            };
        }
    }
}
