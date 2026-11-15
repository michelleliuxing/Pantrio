using System.Linq.Expressions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pantrio.Domain;
using Pantrio.Domain.Quantities;
using Pantrio.Domain.ShoppingList;

namespace Pantrio.Infrastructure.Data;

public sealed class PantrioDbContext(DbContextOptions<PantrioDbContext> options, HouseholdContext household)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Household> Households => Set<Household>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserLogin> UserLogins => Set<UserLogin>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<MealPlanEntry> MealPlanEntries => Set<MealPlanEntry>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();
    public DbSet<ListItemSourceRow> ListItemSources => Set<ListItemSourceRow>();
    public DbSet<AiUsage> AiUsage => Set<AiUsage>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public HouseholdContext Household => household;

    // Read by the query filters; EF parameterises it per query, so one model serves every household.
    // Guid.Empty matches nothing, so a request without a household sees no user data at all.
    private Guid CurrentHouseholdId => household.HouseholdId ?? Guid.Empty;

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<AisleCategory>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<MealSlot>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<HouseholdRole>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<MethodSource>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<ItemState>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<Dimension>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<decimal>().HaveColumnType("numeric");
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("pg_trgm");

        b.Entity<Household>(e =>
        {
            e.ToTable("household");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.TimeZone).HasMaxLength(64);
            e.PrimitiveCollection(x => x.HiddenMealSlots).ElementType(t => t.HasConversion<string>());
        });

        b.Entity<AppUser>(e =>
        {
            e.ToTable("app_user");
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.AvatarUrl).HasMaxLength(2048);
            e.HasIndex(x => x.Email);
            e.HasMany(x => x.Logins).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserLogin>(e =>
        {
            e.ToTable("user_login");
            e.Property(x => x.Provider).HasMaxLength(32);
            e.Property(x => x.Subject).HasMaxLength(255);
            e.HasIndex(x => new { x.Provider, x.Subject }).IsUnique();
        });

        b.Entity<HouseholdMember>(e =>
        {
            e.ToTable("household_member");
            e.HasKey(x => new { x.HouseholdId, x.UserId });
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.Household).WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Ingredient>(e =>
        {
            e.ToTable("ingredient");
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.NormalizedName).HasMaxLength(120);
            e.Property(x => x.DefaultUnit).HasMaxLength(24);
            e.HasIndex(x => new { x.HouseholdId, x.NormalizedName }).IsUnique();
            e.HasIndex(x => x.NormalizedName).HasMethod("gin").HasOperators("gin_trgm_ops");
            e.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Recipe>(e =>
        {
            e.ToTable("recipe");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(4000);
            e.Property(x => x.MethodText).HasMaxLength(20000);
            e.Property(x => x.SourceUrl).HasMaxLength(2048);
            e.HasIndex(x => new { x.HouseholdId, x.Name });
            e.HasMany(x => x.Ingredients).WithOne().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RecipeIngredient>(e =>
        {
            e.ToTable("recipe_ingredient");
            e.Property(x => x.Unit).HasMaxLength(24);
            e.Property(x => x.QuantityText).HasMaxLength(100);
            e.Property(x => x.Note).HasMaxLength(200);
            e.HasIndex(x => new { x.RecipeId, x.SortOrder });
            e.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<MealPlan>(e =>
        {
            e.ToTable("meal_plan");
            e.HasIndex(x => new { x.HouseholdId, x.WeekStart }).IsUnique();
            e.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.MealPlanId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MealPlanEntry>(e =>
        {
            e.ToTable("meal_plan_entry");
            e.HasIndex(x => x.MealPlanId);
            e.HasOne(x => x.Recipe).WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("ck_meal_plan_entry_slot",
                "(day IS NULL AND meal_slot IS NULL) OR (day IS NOT NULL AND meal_slot IS NOT NULL)"));
        });

        b.Entity<ShoppingListItem>(e =>
        {
            e.ToTable("shopping_list_item");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.UnitLabel).HasMaxLength(24);
            e.Property(x => x.ExtraQuantityText).HasMaxLength(60);
            e.HasIndex(x => new { x.HouseholdId, x.IngredientId, x.Dimension, x.UnitLabel })
                .IsUnique()
                .HasFilter("ingredient_id IS NOT NULL");
            e.HasMany(x => x.Sources).WithOne().HasForeignKey(x => x.ListItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Ingredient>().WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ListItemSourceRow>(e =>
        {
            e.ToTable("list_item_source");
            e.Property(x => x.RecipeName).HasMaxLength(200);
            e.Property(x => x.QuantityText).HasMaxLength(300);
            e.HasIndex(x => new { x.ListItemId, x.MealPlanId, x.MealPlanEntryId }).IsUnique();
            e.HasIndex(x => x.MealPlanId);
            e.HasOne<MealPlan>().WithMany().HasForeignKey(x => x.MealPlanId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AiUsage>(e =>
        {
            e.ToTable("ai_usage");
            e.Property(x => x.Purpose).HasMaxLength(64);
            e.Property(x => x.Model).HasMaxLength(100);
            e.HasIndex(x => new { x.HouseholdId, x.CreatedAt });
            e.HasIndex(x => x.CreatedAt);
        });

        b.Entity<DataProtectionKey>().ToTable("data_protection_key");

        // One place enforces household isolation for reads.
        foreach (var type in b.Model.GetEntityTypes().Where(t => typeof(IHouseholdScoped).IsAssignableFrom(t.ClrType)))
        {
            var param = Expression.Parameter(type.ClrType, "e");
            var body = Expression.Equal(
                Expression.Property(param, nameof(IHouseholdScoped.HouseholdId)),
                Expression.Property(Expression.Constant(this), nameof(CurrentHouseholdId)));
            b.Entity(type.ClrType).HasQueryFilter(Expression.Lambda(body, param));
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardHouseholdWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardHouseholdWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// One place enforces household isolation for writes: new rows are stamped with the current household,
    /// and any insert, update or delete of another household's row is refused.
    /// </summary>
    private void GuardHouseholdWrites()
    {
        foreach (var entry in ChangeTracker.Entries<IHouseholdScoped>())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached)
            {
                continue;
            }

            var current = household.HouseholdId
                ?? throw new CrossHouseholdWriteException($"Cannot write {entry.Metadata.ClrType.Name} without a household context.");

            if (entry.State == EntityState.Added && entry.Entity.HouseholdId == Guid.Empty)
            {
                entry.Entity.HouseholdId = current;
            }

            var original = entry.State == EntityState.Added
                ? entry.Entity.HouseholdId
                : (Guid)entry.Property(nameof(IHouseholdScoped.HouseholdId)).OriginalValue!;

            if (original != current || entry.Entity.HouseholdId != current)
            {
                throw new CrossHouseholdWriteException($"Refused to write {entry.Metadata.ClrType.Name} belonging to another household.");
            }
        }
    }
}
