using Microsoft.EntityFrameworkCore;
using ShowMeTheBet.Models;

namespace ShowMeTheBet.Data;

/// <summary>
/// 베팅 시스템의 데이터베이스 컨텍스트 클래스
/// Entity Framework Core를 사용하여 데이터베이스와 상호작용합니다.
/// 
/// 관리하는 엔티티:
/// - User: 사용자 정보
/// - Match: 경기 정보
/// - Bet: 스포츠 베팅 정보
/// - GameBet: 게임 베팅 정보 (홀짝, 그래프 게임)
/// 
/// 주요 기능:
/// - 엔티티 설정 및 관계 정의
/// - 데이터베이스 스키마 구성
/// - 인덱스 및 제약 조건 설정
/// </summary>
public class BettingDbContext : DbContext
{
    #region Constructor
    /// <summary>
    /// BettingDbContext 생성자
    /// </summary>
    /// <param name="options">데이터베이스 옵션 (연결 문자열, 데이터베이스 프로바이더 등)</param>
    public BettingDbContext(DbContextOptions<BettingDbContext> options) : base(options)
    {
    }
    #endregion

    #region DbSets
    /// <summary>
    /// 사용자 정보 테이블
    /// </summary>
    public DbSet<User> Users { get; set; }
    
    /// <summary>
    /// 경기 정보 테이블
    /// </summary>
    public DbSet<Match> Matches { get; set; }
    
    /// <summary>
    /// 스포츠 베팅 정보 테이블
    /// </summary>
    public DbSet<Bet> Bets { get; set; }
    
    /// <summary>
    /// 게임 베팅 정보 테이블 (홀짝, 그래프 게임)
    /// </summary>
    public DbSet<GameBet> GameBets { get; set; }
    #endregion

    #region Model Configuration
    /// <summary>
    /// 엔티티 모델 구성을 오버라이드합니다.
    /// 각 엔티티의 속성, 관계, 인덱스, 제약 조건 등을 설정합니다.
    /// </summary>
    /// <param name="modelBuilder">모델 빌더</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ========== User 엔티티 설정 ==========
        modelBuilder.Entity<User>(entity =>
        {
            // 기본 키 설정
            entity.HasKey(e => e.Id);
            
            // 인덱스 설정 (중복 방지)
            entity.HasIndex(e => e.Username).IsUnique(); // 사용자명은 고유해야 함
            entity.HasIndex(e => e.Email).IsUnique();    // 이메일은 고유해야 함
            
            // 속성 설정
            entity.Property(e => e.Username)
                .IsRequired()           // 필수 필드
                .HasMaxLength(50);      // 최대 길이 50자
            
            entity.Property(e => e.Email)
                .IsRequired()           // 필수 필드
                .HasMaxLength(100);     // 최대 길이 100자
            
            entity.Property(e => e.PasswordHash)
                .IsRequired();          // 필수 필드 (BCrypt 해시)
            
            entity.Property(e => e.Balance)
                .HasPrecision(18, 2);   // 소수점 2자리까지 (금액)
        });

        // ========== Match 엔티티 설정 ==========
        modelBuilder.Entity<Match>(entity =>
        {
            // 기본 키 설정
            entity.HasKey(e => e.Id);
            
            // 속성 설정
            entity.Property(e => e.League)
                .IsRequired()           // 필수 필드
                .HasMaxLength(100);     // 최대 길이 100자
            
            entity.Property(e => e.HomeTeam)
                .IsRequired()           // 필수 필드
                .HasMaxLength(100);     // 최대 길이 100자
            
            entity.Property(e => e.AwayTeam)
                .IsRequired()           // 필수 필드
                .HasMaxLength(100);     // 최대 길이 100자
            
            // 배당률 설정 (소수점 2자리까지)
            entity.Property(e => e.HomeOdds).HasPrecision(5, 2);
            entity.Property(e => e.DrawOdds).HasPrecision(5, 2);
            entity.Property(e => e.AwayOdds).HasPrecision(5, 2);
        });

        // ========== Bet 엔티티 설정 ==========
        modelBuilder.Entity<Bet>(entity =>
        {
            // 기본 키 설정
            entity.HasKey(e => e.Id);
            
            // 속성 설정
            entity.Property(e => e.MatchInfo)
                .IsRequired()           // 필수 필드
                .HasMaxLength(200);     // 최대 길이 200자
            
            // 금액 관련 속성 설정 (소수점 2자리까지)
            entity.Property(e => e.Odds).HasPrecision(5, 2);        // 배당률
            entity.Property(e => e.Amount).HasPrecision(18, 2);     // 베팅 금액
            entity.Property(e => e.PotentialWin).HasPrecision(18, 2); // 예상 승리 금액
            
            // 관계 설정: User와의 관계 (1:N)
            // 한 사용자는 여러 베팅을 가질 수 있음
            entity.HasOne(e => e.User)
                .WithMany(u => u.Bets)              // User 엔티티의 Bets 컬렉션과 연결
                .HasForeignKey(e => e.UserId)       // 외래 키: UserId
                .OnDelete(DeleteBehavior.Cascade);  // 사용자 삭제 시 베팅도 함께 삭제
            
            // 관계 설정: Match와의 관계 (N:1)
            // 한 경기는 여러 베팅을 가질 수 있음
            entity.HasOne(e => e.Match)
                .WithMany(m => m.Bets)              // Match 엔티티의 Bets 컬렉션과 연결
                .HasForeignKey(e => e.MatchId)      // 외래 키: MatchId
                .OnDelete(DeleteBehavior.Restrict); // 경기 삭제 시 베팅은 삭제되지 않음 (제한)
        });

        // ========== GameBet 엔티티 설정 ==========
        modelBuilder.Entity<GameBet>(entity =>
        {
            // 기본 키 설정
            entity.HasKey(e => e.Id);
            
            // 속성 설정
            entity.Property(e => e.BetChoice)
                .IsRequired()           // 필수 필드
                .HasMaxLength(50);      // 최대 길이 50자 ("홀", "짝", 배수 등)
            
            // 금액 관련 속성 설정 (소수점 2자리까지)
            entity.Property(e => e.Amount).HasPrecision(18, 2);     // 베팅 금액
            entity.Property(e => e.Multiplier).HasPrecision(5, 2);  // 배수
            entity.Property(e => e.WinAmount).HasPrecision(18, 2);  // 승리 금액
            
            entity.Property(e => e.Result)
                .HasMaxLength(100);     // 최대 길이 100자 (결과 표시용)
            
            // 관계 설정: User와의 관계 (1:N)
            // 한 사용자는 여러 게임 베팅을 가질 수 있음
            entity.HasOne(e => e.User)
                .WithMany(u => u.GameBets)          // User 엔티티의 GameBets 컬렉션과 연결
                .HasForeignKey(e => e.UserId)       // 외래 키: UserId
                .OnDelete(DeleteBehavior.Cascade);  // 사용자 삭제 시 게임 베팅도 함께 삭제
        });
    }
    #endregion
}
