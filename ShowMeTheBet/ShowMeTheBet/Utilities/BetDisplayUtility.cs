using ShowMeTheBet.Models;

namespace ShowMeTheBet.Utilities;

/// <summary>
/// 베팅 정보를 UI에 표시할 때 사용하는 순수 유틸리티 함수 모음.
/// </summary>
public static class BetDisplayUtility
{
    public static string GetBetTypeLabel(BetType type) => type switch
    {
        BetType.Home => "홈 승",
        BetType.Draw => "무승부",
        BetType.Away => "원정 승",
        _ => string.Empty
    };

    public static string GetBetStatusLabel(BetStatus status) => status switch
    {
        BetStatus.Pending => "대기중",
        BetStatus.Won => "승리",
        BetStatus.Lost => "패배",
        _ => string.Empty
    };
}

