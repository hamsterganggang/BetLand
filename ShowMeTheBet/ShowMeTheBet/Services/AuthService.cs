using Microsoft.EntityFrameworkCore;
using ShowMeTheBet.Data;
using ShowMeTheBet.Models;
using BCrypt.Net;

namespace ShowMeTheBet.Services;

public class AuthService
{
    private readonly BettingDbContext _context;
    private User? _currentUser;

    public AuthService(BettingDbContext context)
    {
        _context = context;
    }

    public User? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;

    public async Task<bool> RegisterAsync(string username, string email, string password)
    {
        if (await _context.Users.AnyAsync(u => u.Username == username || u.Email == email))
        {
            return false;
        }

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Balance = 100000,
            CreatedAt = DateTime.Now
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return false;
        }

        _currentUser = user;
        return true;
    }

    public void Logout()
    {
        _currentUser = null;
    }

    public async Task<User?> GetUserAsync(int userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    public async Task RefreshUserAsync()
    {
        if (_currentUser != null)
        {
            _currentUser = await _context.Users.FindAsync(_currentUser.Id);
        }
    }
}

