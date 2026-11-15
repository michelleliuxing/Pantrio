using Pantrio.Domain;

namespace Pantrio.Infrastructure.Data;

/// <summary>
/// The household the current request acts for, set once per request from the signed-in user's membership.
/// The DbContext's query filter and write guard read it; endpoints never filter by household themselves.
/// </summary>
public sealed class HouseholdContext
{
    public Guid? HouseholdId { get; private set; }
    public Guid? UserId { get; private set; }
    public HouseholdRole? Role { get; private set; }
    public bool CanDelete { get; private set; }

    public bool IsSet => HouseholdId is not null;

    public Guid RequiredHouseholdId => HouseholdId ?? throw new InvalidOperationException("No household context for this request.");

    public Guid RequiredUserId => UserId ?? throw new InvalidOperationException("No signed-in user for this request.");

    public void Set(Guid householdId, Guid userId, HouseholdRole role, bool canDelete)
    {
        if (HouseholdId is not null && HouseholdId != householdId)
        {
            throw new InvalidOperationException("Household context is already set for this request.");
        }

        HouseholdId = householdId;
        UserId = userId;
        Role = role;
        CanDelete = canDelete;
    }

    /// <summary>Owners always may delete; editors only with the can_delete grant; viewers never.</summary>
    public bool MayDelete => Role == HouseholdRole.Owner || (Role == HouseholdRole.Editor && CanDelete);

    public bool HasAtLeast(HouseholdRole role) => Role is { } r && Rank(r) >= Rank(role);

    private static int Rank(HouseholdRole role) => role switch
    {
        HouseholdRole.Owner => 3,
        HouseholdRole.Editor => 2,
        _ => 1,
    };
}

public sealed class CrossHouseholdWriteException(string message) : InvalidOperationException(message);
