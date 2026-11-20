using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using backend.Api.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using IdentityClaim = System.Security.Claims.Claim;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    ClaimsDbContext db,
    IConfiguration configuration,
    PasswordHasher<User> passwordHasher) : ControllerBase
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new ProblemDetails { Title = "Full name cannot be blank." });
        }

        var email = NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return Conflict(new ProblemDetails { Title = "An account with this email already exists." });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = string.Empty,
            Role = UserRole.CLAIMANT,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_email" })
        {
            return Conflict(new ProblemDetails { Title = "An account with this email already exists." });
        }

        return StatusCode(StatusCodes.Status201Created, CreateAuthResponse(user));
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(
            candidate => candidate.Email == email,
            cancellationToken);

        if (user is null)
        {
            return Unauthorized(new ProblemDetails { Title = "Invalid email or password." });
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new ProblemDetails { Title = "Invalid email or password." });
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(CreateAuthResponse(user));
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "claims-platform";
        var audience = configuration["Jwt:Audience"] ?? "claims-platform-client";
        var signingKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT signing key is not configured.");
        var expiresAt = DateTime.UtcNow.Add(TokenLifetime);
        IdentityClaim[] claims =
        [
            new IdentityClaim("user_id", user.Id.ToString()),
            new IdentityClaim("role", user.Role.ToString()),
            new IdentityClaim(JwtRegisteredClaimNames.Email, user.Email),
            new IdentityClaim(JwtRegisteredClaimNames.Name, user.FullName)
        ];
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: expiresAt,
            signingCredentials: credentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new AuthResponse(
            accessToken,
            expiresAt,
            new UserResponse(user.Id, user.FullName, user.Email, user.Role));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
