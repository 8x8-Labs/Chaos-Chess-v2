using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// 매치를 시작하기 전에 양쪽이 합의해야 하는 초기 상태입니다.
///
/// 커맨드 릴레이는 "같은 초기 상태에서 같은 행동을 같은 순서로 적용하면 같은 결과"를 전제로 합니다.
/// 그런데 초기 판, 엘리트 변형, 카드 뽑기가 전부 클라이언트별 난수로 정해지고 있어
/// 그대로 두면 두 클라이언트가 서로 다른 판에서 대국하게 됩니다.
///
/// 그래서 한쪽(지금은 호스트, 6단계 이후에는 서버)이 이 값을 정해 내려주고
/// 받은 쪽은 뽑지 않고 그대로 씁니다.
///
/// CardQueue에 상대 몫은 담지 않습니다. 받는 쪽이 상대 카드를 미리 아는 것을 막기 위해서입니다.
/// (설계문서 5절이 시드 방식을 비권장한 이유와 같습니다.)
///
/// JsonUtility로 직렬화하므로 필드만 두고 프로퍼티는 쓰지 않습니다.
/// </summary>
[Serializable]
public class MatchSetup
{
    /// <summary>엔진 빌드가 서로 다르면 룰 판정이 갈릴 수 있으므로 매치를 열기 전에 확인합니다.</summary>
    public string EngineVersion;

    /// <summary>카드 DB가 다르면 상대 카드를 찾지 못해 대국 중에 터집니다. 시작 시점에 걸러냅니다.</summary>
    public string CardDatabaseHash;

    /// <summary>이번 매치의 초기 판입니다.</summary>
    public string InitialFen;

    /// <summary>받는 쪽이 이번 매치에서 순서대로 뽑게 될 카드의 AiCardId 목록입니다.</summary>
    public string[] CardQueue;

    /// <summary>
    /// 지금 이 클라이언트가 가진 카드 DB의 지문입니다.
    ///
    /// AiCardId만 모아 정렬한 뒤 해싱하므로, 카드 순서나 이름 변경에는 영향받지 않고
    /// "어떤 카드가 있는가"만 비교합니다. AiCardId가 없는 카드는 원격으로 주고받을 수 없으므로 제외합니다.
    /// </summary>
    public static string ComputeCardDatabaseHash()
    {
        CardRandomizerManager manager = CardRandomizerManager.Instance;
        if (manager == null || manager.AllCards == null)
            return string.Empty;

        List<string> ids = new List<string>();
        foreach (GameObject cardObject in manager.AllCards)
        {
            if (cardObject == null) continue;

            CardData cardData = cardObject.GetComponent<CardData>();
            if (cardData == null || cardData.DataSO == null) continue;

            string id = cardData.DataSO.AiCardId;
            if (string.IsNullOrWhiteSpace(id)) continue;

            ids.Add(id.Trim().ToLowerInvariant());
        }

        ids.Sort(StringComparer.Ordinal);

        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("|", ids)));
        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }

    /// <summary>
    /// 상대가 보낸 초기 상태가 이쪽에서 그대로 쓸 수 있는 것인지 확인합니다.
    /// 여기서 걸러내지 못하면 대국 중반에야 판이 어긋난 것을 알게 됩니다.
    /// </summary>
    public bool TryValidate(out string reason)
    {
        if (string.IsNullOrWhiteSpace(InitialFen))
        {
            reason = "초기 판(FEN)이 비어 있습니다.";
            return false;
        }

        if (EngineVersion != ChaosChessAiVersion.Version)
        {
            reason = $"엔진 버전이 다릅니다. 상대 {EngineVersion} / 이쪽 {ChaosChessAiVersion.Version}";
            return false;
        }

        string localHash = ComputeCardDatabaseHash();
        if (CardDatabaseHash != localHash)
        {
            reason = "카드 DB가 서로 다릅니다. 같은 빌드인지 확인해 주세요.";
            return false;
        }

        reason = null;
        return true;
    }

    public override string ToString()
    {
        int cards = CardQueue != null ? CardQueue.Length : 0;
        return $"MatchSetup(fen='{InitialFen}', cards={cards}, engine={EngineVersion})";
    }
}
