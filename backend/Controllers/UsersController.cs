using backend.Api.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize(Roles = "MANAGER")]
[Route("api/users")]
public sealed class UsersController(ClaimsDbContext db) : ControllerBase
{
    [HttpGet("officers")]
    [ProducesResponseType<IReadOnlyList<UserResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetOfficers(
        CancellationToken cancellationToken)
    {
        var officers = await db.Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.OFFICER)
            .OrderBy(user => user.FullName)
            .Select(user => new UserResponse(user.Id, user.FullName, user.Email, user.Role))
            .ToListAsync(cancellationToken);

        return Ok(officers);
    }
}
