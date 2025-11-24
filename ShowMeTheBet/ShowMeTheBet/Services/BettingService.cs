using Microsoft.EntityFrameworkCore;
using ShowMeTheBet.Data;
using ShowMeTheBet.Models;

namespace ShowMeTheBet.Services;

public class BettingService
{
    private readonly BettingDbContext _context;
    private readonly AuthService _authService;

    public BettingService(BettingDbContext context, AuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    public async Task<List<Match>> GetMatchesAsync()
    {
        return await _context.Matches
            .Where(m => m.Status == MatchStatus.Upcoming || m.Status == MatchStatus.Live)
            .OrderBy(m => m.MatchTime)
            .ToListAsync();
    }
    
    public async Task<Match?> GetMatchAsync(int id)
    {
        return await _context.Matches.FindAsync(id);
    }

    public async Task<decimal> GetBalanceAsync()
    {
        if (_authService.CurrentUser == null) return 0;
        await _authService.RefreshUserAsync();
        return _authService.CurrentUser?.Balance ?? 0;
    }

    public async Task<bool> PlaceBetAsync(int matchId, BetType type, decimal amount)
    {
        // CurrentUser가 null이면 다시 로드 시도
        if (_authService.CurrentUser == null)
        {
            await _authService.LoadUserFromSessionAsync();
        }
        
        if (_authService.CurrentUser == null) return false;
        
        var match = await GetMatchAsync(matchId);
        if (match == null) return false;

        await _authService.RefreshUserAsync();
        var user = _authService.CurrentUser;
        if (user == null || user.Balance < amount) return false;

        var odds = type switch
        {
            BetType.Home => match.HomeOdds,
            BetType.Draw => match.DrawOdds,
            BetType.Away => match.AwayOdds,
            _ => 1m
        };

        var bet = new Bet
        {
            UserId = user.Id,
            MatchId = matchId,
            MatchInfo = $"{match.HomeTeam} vs {match.AwayTeam}",
            Type = type,
            Odds = odds,
            Amount = amount,
            PotentialWin = amount * odds,
            BetTime = DateTime.Now,
            Status = BetStatus.Pending
        };

        user.Balance -= amount;
        
        _context.Bets.Add(bet);
        await _context.SaveChangesAsync();
        
        await _authService.RefreshUserAsync();
        return true;
    }

    public async Task<List<Bet>> GetBetsAsync()
    {
        if (_authService.CurrentUser == null) return new List<Bet>();
        
        return await _context.Bets
            .Where(b => b.UserId == _authService.CurrentUser.Id)
            .Include(b => b.Match)
            .OrderByDescending(b => b.BetTime)
            .ToListAsync();
    }

    public async Task<bool> RemoveBetAsync(int betId)
    {
        if (_authService.CurrentUser == null) return false;
        
        var bet = await _context.Bets
            .FirstOrDefaultAsync(b => b.Id == betId && b.UserId == _authService.CurrentUser.Id);
            
        if (bet == null || bet.Status != BetStatus.Pending) return false;

        await _authService.RefreshUserAsync();
        var user = _authService.CurrentUser;
        if (user == null) return false;

        user.Balance += bet.Amount;
        _context.Bets.Remove(bet);
        await _context.SaveChangesAsync();
        
        await _authService.RefreshUserAsync();
        return true;
    }

    public async Task InitializeMatchesAsync()
    {
        if (await _context.Matches.AnyAsync()) return;

        var matches = new List<Match>
        {
            new Match
            {
                League = "K리그1",
                HomeTeam = "FC서울",
                AwayTeam = "수원삼성",
                MatchTime = DateTime.Now.AddHours(2),
                HomeOdds = 2.10m,
                DrawOdds = 3.20m,
                AwayOdds = 3.50m,
                Status = MatchStatus.Upcoming
            },
            new Match
            {
                League = "K리그1",
                HomeTeam = "울산현대",
                AwayTeam = "전북현대",
                MatchTime = DateTime.Now.AddHours(4),
                HomeOdds = 1.85m,
                DrawOdds = 3.40m,
                AwayOdds = 4.20m,
                Status = MatchStatus.Upcoming
            },
            new Match
            {
                League = "프리미어리그",
                HomeTeam = "맨체스터 유나이티드",
                AwayTeam = "리버풀",
                MatchTime = DateTime.Now.AddDays(1),
                HomeOdds = 2.50m,
                DrawOdds = 3.10m,
                AwayOdds = 2.80m,
                Status = MatchStatus.Upcoming
            },
            new Match
            {
                League = "프리미어리그",
                HomeTeam = "아스날",
                AwayTeam = "첼시",
                MatchTime = DateTime.Now.AddDays(1).AddHours(3),
                HomeOdds = 1.95m,
                DrawOdds = 3.30m,
                AwayOdds = 3.90m,
                Status = MatchStatus.Upcoming
            },
            new Match
            {
                League = "라리가",
                HomeTeam = "레알 마드리드",
                AwayTeam = "바르셀로나",
                MatchTime = DateTime.Now.AddDays(2),
                HomeOdds = 2.20m,
                DrawOdds = 3.50m,
                AwayOdds = 3.00m,
                Status = MatchStatus.Upcoming
            },
            new Match
            {
                League = "분데스리가",
                HomeTeam = "바이에른 뮌헨",
                AwayTeam = "도르트문트",
                MatchTime = DateTime.Now.AddDays(2).AddHours(5),
                HomeOdds = 1.75m,
                DrawOdds = 3.80m,
                AwayOdds = 4.50m,
                Status = MatchStatus.Upcoming
            }
        };

        _context.Matches.AddRange(matches);
        await _context.SaveChangesAsync();
    }

    public async Task<List<User>> GetUserRankingsAsync()
    {
        return await _context.Users
            .OrderByDescending(u => u.Balance)
            .ToListAsync();
    }
}

