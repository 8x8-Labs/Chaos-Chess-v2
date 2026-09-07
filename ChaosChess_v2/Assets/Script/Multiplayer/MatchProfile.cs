using System;
using UnityEngine;

/// <summary>
/// 대국 화면에 띄울 상대의 표시용 정보입니다.
///
/// 규칙 합의(<see cref="MatchSetup"/>)와 달리 양쪽 값이 <b>달라야 정상</b>이므로 검증하지 않고,
/// 핸드셰이크 왕복에 얹어 서로 주고받기만 합니다.
///
/// ⚠️ <b>클라이언트가 스스로 신고한 값이라 그대로 믿을 수 없습니다.</b> 상대가 아무 이름이나
/// 보낼 수 있습니다. 표시 전용이고 레이팅에 쓰이지 않으므로 지금은 감수하지만,
/// 설계문서 4절의 "클라이언트가 보낸 신원을 믿지 않는다" 원칙과는 어긋납니다.
/// 서버가 신원을 검증하는 6단계에서 대체해야 합니다.
///
/// 아바타는 <b>이미지가 아니라 URL</b>을 실어 보냅니다. 이미지 바이트를 넣으면 프레임 상한
/// (RelayMatchTransport.MaxPayloadBytes)을 훌쩍 넘고, 대국용 채널에 실을 성격도 아닙니다.
/// 받는 쪽이 URL로 1회 내려받아 씁니다.
///
/// JsonUtility로 직렬화하므로 필드만 두고 프로퍼티는 쓰지 않습니다.
/// </summary>
[Serializable]
public class MatchProfile
{
    /// <summary>화면에 표시할 이름입니다.</summary>
    public string DisplayName;

    /// <summary>아바타 이미지 주소입니다. 없으면 비어 있고, 받는 쪽은 기본 이미지를 씁니다.</summary>
    public string AvatarUrl;

    /// <summary>
    /// GPGS를 쓸 수 없는 환경에서 쓸 이름입니다.
    ///
    /// GPGS 호출은 <c>#if UNITY_ANDROID &amp;&amp; !UNITY_EDITOR</c> 안에만 있어
    /// <b>에디터(MPPM 포함)에서는 프로필을 가져올 수 없습니다.</b>
    /// 이름이 비어 대국 화면이 빈칸으로 남지 않도록 역할에서 만들어 씁니다.
    /// </summary>
    public static MatchProfile CreateFallback(MatchRole role)
    {
        return new MatchProfile
        {
            DisplayName = role == MatchRole.Host ? "플레이어 1" : "플레이어 2",
            AvatarUrl = string.Empty
        };
    }

    /// <summary>
    /// 이 클라이언트의 프로필을 만듭니다.
    ///
    /// 아직 GPGS에서 이름·아바타를 끌어오지 않습니다. 그 작업은 프로필 취득 단계의 몫이고,
    /// 여기가 그때 값을 채울 자리입니다. 지금은 폴백만 돌려줍니다.
    /// </summary>
    public static MatchProfile CreateLocal(MatchRole role)
    {
        MatchProfile profile = CreateFallback(role);

        GooglePlayAuthManager auth = GooglePlayAuthManager.Instance;
        if (auth != null && auth.IsAuthenticated && !string.IsNullOrWhiteSpace(auth.UserName))
            profile.DisplayName = auth.UserName;

        return profile;
    }

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(DisplayName) ? "이름 없음" : DisplayName;
    }
}
