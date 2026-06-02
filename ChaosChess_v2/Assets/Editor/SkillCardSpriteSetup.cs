using UnityEditor;
using UnityEngine;

public static class SkillCardSpriteSetup
{
    private const string SheetPath = "Assets/Sprite/CardSprites/SkillCard-Sheet.png";
    private const string SOPath = "Assets/Script/Card/SO/";
    private const int SpriteCount = 50;

    private static readonly string[] CardSONames =
    {
        "AgileCard", "ArenaCard", "ATMineCard", "BlessingCard", "CastleKnight",
        "CaterpillarCard", "AimCard", "ChaoticKnightCard", "ChargeCard", "CheckmateDeclarationCard",
        "CobwebCard", "ConcentrationCard", "DarkHandCard", "DemocracyCard", "DesperadoCard",
        "DestroyerTankCards", "DimensionDisturbanceCard", "DimensionInstabilityCard", "FatherEnemyCard", "FireCard",
        "GaslightingCard", "GiantCard", "GodsMoveCard", "HoneyTrapCard", "JumpingPlatformCard",
        "LimitlessCard", "MagnetCard", "MissingPromotionCard", "MutinyCard", "ObeyOrderCard",
        "OverbearingCard", "FastMarchCard", "PeaceZoneCard", "PortalCard", "PositionSwapCard",
        "PsilocybinMushroomCard", "RampartCard", "ReviveCard", "ShuffleBoardCard", "StagFightCard",
        "SunsetBlade", "SyncCard", "TeleportCard", "TImeBomb", "TimeReversalCard",
        "TransmigrationCard", "WeirdCastlingCard", "WindmillCard", "ThunderclapFlashCard", "SneakPawnCard"
    };

    [MenuItem("Tools/Setup SkillCard Sprites")]
    public static void SetupSprites()
    {
        var sprites = new Sprite[SpriteCount];
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
        {
            if (asset is not Sprite sprite) continue;
            var parts = sprite.name.Split('_');
            if (int.TryParse(parts[^1], out int idx) && idx < SpriteCount)
                sprites[idx] = sprite;
        }

        int assigned = 0;
        for (int i = 0; i < CardSONames.Length; i++)
        {
            string path = SOPath + CardSONames[i] + ".asset";
            var cardSO = AssetDatabase.LoadAssetAtPath<CardDataSO>(path);
            if (cardSO == null)
            {
                Debug.LogWarning($"[SkillCardSpriteSetup] CardSO 없음: {path}");
                continue;
            }
            if (sprites[i] == null)
            {
                Debug.LogWarning($"[SkillCardSpriteSetup] 스프라이트 없음: 인덱스 {i}");
                continue;
            }
            cardSO.CardImage = sprites[i];
            EditorUtility.SetDirty(cardSO);
            assigned++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SkillCardSpriteSetup] 완료: {assigned}/{SpriteCount} 카드에 스프라이트 할당");
    }
}
