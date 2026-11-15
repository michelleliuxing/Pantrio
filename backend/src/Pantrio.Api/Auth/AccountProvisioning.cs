using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Pantrio.Domain;
using Pantrio.Infrastructure.Data;
using Pantrio.Infrastructure.Seed;

namespace Pantrio.Api.Auth;

public sealed record ExternalIdentity(string Provider, string Subject, string Email, string DisplayName, string? AvatarUrl);

/// <summary>Signing in with an unknown identity signs the person up: a user, a household with them as owner, and a seeded catalogue.</summary>
public sealed class AccountProvisioning(PantrioDbContext db, HouseholdContext household, ILogger<AccountProvisioning> logger)
{
    public const string UserIdClaim = "pantrio:uid";

    public async Task<ClaimsPrincipal> SignInOrSignUpAsync(ExternalIdentity identity, CancellationToken ct)
    {
        var login = await db.UserLogins.SingleOrDefaultAsync(l => l.Provider == identity.Provider && l.Subject == identity.Subject, ct);
        AppUser user;
        if (login is null)
        {
            user = new AppUser { Email = identity.Email, DisplayName = identity.DisplayName, AvatarUrl = identity.AvatarUrl };
            user.Logins.Add(new UserLogin { Provider = identity.Provider, Subject = identity.Subject });
            db.Users.Add(user);
            logger.LogInformation("New user {UserId} via {Provider}", user.Id, identity.Provider);
        }
        else
        {
            user = await db.Users.SingleAsync(u => u.Id == login.UserId, ct);
            user.Email = identity.Email;
            user.DisplayName = identity.DisplayName;
            user.AvatarUrl = identity.AvatarUrl ?? user.AvatarUrl;
            user.LastSignInAt = DateTimeOffset.UtcNow;
        }

        var hasHousehold = login is not null && await db.HouseholdMembers.AnyAsync(m => m.UserId == user.Id, ct);
        if (!hasHousehold)
        {
            var firstName = identity.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "My";
            var home = new Household { Name = $"{firstName}'s household" };
            db.Households.Add(home);
            db.HouseholdMembers.Add(new HouseholdMember
            {
                HouseholdId = home.Id,
                UserId = user.Id,
                Role = HouseholdRole.Owner,
                CanDelete = true,
            });

            household.Set(home.Id, user.Id, HouseholdRole.Owner, canDelete: true);
            db.Ingredients.AddRange(CatalogueSeed.Create(home.Id));
        }

        await db.SaveChangesAsync(ct);

        var claims = new List<Claim>
        {
            new(UserIdClaim, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSetup.AppCookie));
    }
}
