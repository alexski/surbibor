using Microsoft.EntityFrameworkCore;
using Surbibor.Domain.Entities;
using Surbibor.Domain.Exceptions;

namespace Surbibor.Infrastructure.Services;

public class AuthService(SurbiborDbContext db)
{
    public async Task<User> RegisterAsync(string email, string username, string password, CancellationToken ct = default)
    {
        email = email.Trim().ToLowerInvariant();
        username = username.Trim();

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new ValidationException("A valid email is required.");
        }

        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
        {
            throw new ValidationException("Username must be at least 3 characters.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new ValidationException("Password must be at least 8 characters.");
        }

        var emailTaken = await db.Users.AnyAsync(u => u.Email == email, ct);
        if (emailTaken)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var usernameTaken = await db.Users.AnyAsync(u => u.Username == username, ct);
        if (usernameTaken)
        {
            throw new ConflictException("This username is already taken.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return user;
    }

    public async Task<User> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        email = email.Trim().ToLowerInvariant();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            throw new ValidationException("Invalid email or password.");
        }

        return user;
    }

    public async Task<User> ResetPasswordAsync(string email, string newPassword, string confirmPassword, CancellationToken ct = default)
    {
        email = email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            throw new ValidationException("Password must be at least 8 characters.");
        }

        if (newPassword != confirmPassword)
        {
            throw new ValidationException("Passwords do not match.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct)
            ?? throw new NotFoundException("No account found with this email.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await db.SaveChangesAsync(ct);

        return user;
    }
}
