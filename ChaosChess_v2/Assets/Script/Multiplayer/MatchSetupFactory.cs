using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 매치의 초기 상태를 정하는 쪽(지금은 호스트)이 쓰는 생성기입니다.
///
/// 서버가 없는 동안의 임시 구조입니다. 호스트가 자기 유리하게 카드를 뽑을 수 있다는 점은
/// 감수하고, 6단계 서버 검증에서 대체합니다(설계문서 5절).
///
/// **런 전용 상태에 기대지 않습니다.** 초기 판을 MapManager에서 가져오면 맵 그래프가
/// 클라이언트마다 다른 탓에 판이 갈리고, 애초에 맵·엘리트·ELO는 로그라이크 런 개념이라
/// 원격 대전이 의존해서는 안 됩니다(설계문서 8-1).
/// </summary>
public static class MatchSetupFactory
{
    /// <summary>
    /// 원격 대전의 시작 판입니다. 표준 초기 배치 하나로 고정합니다.
    /// MapManager.DefaultFEN을 읽지 않는 이유는 위 클래스 주석 참고.
    /// </summary>
    public const string MultiplayerInitialFen =
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    /// <summary>
    /// 각자에게 내려줄 카드 큐의 길이입니다.
    ///
    /// 초기 지급 4장(DefaultMaxCardCount) + 5턴(DefaultCardInterval)마다 1장이므로
    /// 300턴 분량입니다. 바닥나면 CardRandomizer가 경고만 남기고 더 주지 않습니다.
    /// </summary>
    public const int CardQueueLength = 64;

    /// <summary>
    /// 호스트 몫과 게스트 몫 두 벌을 만듭니다.
    ///
    /// 두 벌을 따로 뽑는 이유는 상대가 무슨 카드를 쥐게 될지 미리 알 수 없게 하기 위해서입니다.
    /// 한 벌을 나눠 쓰거나 시드만 내려주면 상대 손패를 계산할 수 있게 됩니다(설계문서 5절).
    /// 엔진 버전과 카드 DB 해시는 검증용이라 두 벌이 같은 값을 씁니다.
    /// </summary>
    public static void Build(out MatchSetup hostSetup, out MatchSetup guestSetup)
    {
        string engineVersion = ChaosChessAiVersion.Version;
        string cardHash = MatchSetup.ComputeCardDatabaseHash();
        List<string> cardIds = CollectRemotePlayableCardIds();

        hostSetup = Compose(engineVersion, cardHash, cardIds);
        guestSetup = Compose(engineVersion, cardHash, cardIds);

        Debug.Log($"[Match] 초기 상태를 만들었습니다. 카드 원본 {cardIds.Count}종 → 각자 {CardQueueLength}장");
    }

    private static MatchSetup Compose(string engineVersion, string cardHash, List<string> cardIds)
    {
        return new MatchSetup
        {
            EngineVersion = engineVersion,
            CardDatabaseHash = cardHash,
            InitialFen = MultiplayerInitialFen,
            CardQueue = BuildCardQueue(cardIds)
        };
    }

    /// <summary>
    /// 원격으로 주고받을 수 있는 카드의 AiCardId를 전부 모읍니다.
    ///
    /// 런 카드풀(PlayerState.CardPool)을 쓰지 않습니다. StartGame()이 InitializeRun()으로
    /// 카드풀을 비우기도 하고, 애초에 멀티에는 런 개념이 없습니다.
    /// AiCardId가 없는 카드는 메시지에 실을 수 없으므로 제외합니다.
    /// </summary>
    private static List<string> CollectRemotePlayableCardIds()
    {
        List<string> ids = new List<string>();

        CardRandomizerManager manager = CardRandomizerManager.Instance;
        if (manager == null || manager.AllCards == null)
        {
            Debug.LogError("[Match] 카드 목록을 찾지 못해 카드 큐를 만들 수 없습니다.");
            return ids;
        }

        foreach (GameObject cardObject in manager.AllCards)
        {
            if (cardObject == null) continue;

            CardData cardData = cardObject.GetComponent<CardData>();
            if (cardData == null || cardData.DataSO == null) continue;

            string id = cardData.DataSO.AiCardId;
            if (string.IsNullOrWhiteSpace(id)) continue;

            ids.Add(id);
        }

        return ids;
    }

    /// <summary>
    /// 카드 큐를 만듭니다. **블록 셔플**입니다 — 전체 목록을 섞은 한 블록을 필요한 길이만큼 이어 붙입니다.
    ///
    /// 매번 독립적으로 뽑으면 같은 카드가 연달아 나옵니다. 기존 GetRandomCardsFromPool은 손패와의
    /// 중복을 피하지만, 정하는 쪽은 받는 쪽의 손패 상태를 알 수 없어 같은 보장을 할 수 없습니다.
    /// 블록 안에서는 중복이 없으므로 한 바퀴 도는 동안은 고르게 나옵니다.
    /// </summary>
    private static string[] BuildCardQueue(List<string> cardIds)
    {
        if (cardIds.Count == 0)
            return new string[0];

        List<string> queue = new List<string>(CardQueueLength);
        List<string> block = new List<string>(cardIds);

        while (queue.Count < CardQueueLength)
        {
            Shuffle(block);

            foreach (string id in block)
            {
                queue.Add(id);
                if (queue.Count >= CardQueueLength) break;
            }
        }

        return queue.ToArray();
    }

    /// <summary>
    /// 피셔-예이츠 셔플입니다.
    ///
    /// UnityEngine.Random을 그대로 씁니다. 정하는 쪽이 자기 난수로 뽑아 결과 목록만 보내고
    /// 받는 쪽은 아예 뽑지 않으므로, 두 클라이언트의 난수가 일치할 필요가 없습니다(설계문서 5절).
    /// </summary>
    private static void Shuffle(List<string> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
