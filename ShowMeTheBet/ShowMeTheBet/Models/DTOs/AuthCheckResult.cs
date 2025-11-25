namespace ShowMeTheBet.Models.DTOs;

/// <summary>
/// 인증 확인 API 응답을 위한 DTO (Data Transfer Object) 클래스
/// JavaScript Interop을 통해 API를 호출할 때 사용되는 데이터 구조
/// </summary>
public class AuthCheckResult
{
    /// <summary>
    /// API 호출 성공 여부
    /// </summary>
    public bool success { get; set; }
    
    /// <summary>
    /// 사용자 인증 상태 (로그인 여부)
    /// </summary>
    public bool authenticated { get; set; }
    
    /// <summary>
    /// 사용자 ID (인증된 경우에만 값이 있음)
    /// </summary>
    public int? userId { get; set; }
    
    /// <summary>
    /// 사용자명 (인증된 경우에만 값이 있음)
    /// </summary>
    public string? username { get; set; }
    
    /// <summary>
    /// 사용자 잔액 (인증된 경우에만 값이 있음)
    /// </summary>
    public decimal? balance { get; set; }
}

