namespace ShowMeTheBet.Services;

/// <summary>
/// 룰렛 게임 실행 결과 DTO.
/// </summary>
public record RouletteSpinResult(
    bool Success,
    bool IsWin,
    string Label,
    decimal Multiplier,
    decimal WinAmount,
    decimal BalanceAfterSpin,
    string Message)
{
    public static RouletteSpinResult Fail(string message, decimal currentBalance = 0) =>
        new(false, false, "꽝", 0m, 0m, currentBalance, message);
}

