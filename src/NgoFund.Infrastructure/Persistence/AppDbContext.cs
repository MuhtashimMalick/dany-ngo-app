using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NgoFund.Domain.Common;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Infrastructure.Identity;
using NgoFund.Infrastructure.Persistence.Seed;

namespace NgoFund.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for the system. Doubles as the Identity store
/// (users/roles/claims/tokens come from <see cref="IdentityDbContext{TUser,TRole,TKey}"/>) plus
/// every domain table. Entity configuration lives in one <c>IEntityTypeConfiguration&lt;T&gt;</c>
/// per entity under Persistence/Configurations, auto-applied below — this class stays free of
/// per-entity mapping code.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    // Identity/RBAC (Infrastructure-owned — link to ApplicationUser/ApplicationRole)
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Reference / configuration
    public DbSet<FundCategory> FundCategories => Set<FundCategory>();
    public DbSet<ApplicationCategory> ApplicationCategories => Set<ApplicationCategory>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    // Donors & donations
    public DbSet<Donor> Donors => Set<Donor>();
    public DbSet<Donation> Donations => Set<Donation>();

    // Applicants & applications
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<FundApplication> Applications => Set<FundApplication>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();
    public DbSet<ApplicationRemark> ApplicationRemarks => Set<ApplicationRemark>();
    public DbSet<HousingApplicationDetails> HousingApplicationDetails => Set<HousingApplicationDetails>();
    public DbSet<MarriageApplicationDetails> MarriageApplicationDetails => Set<MarriageApplicationDetails>();
    public DbSet<BusinessLoanApplicationDetails> BusinessLoanApplicationDetails => Set<BusinessLoanApplicationDetails>();
    public DbSet<EducationApplicationDetails> EducationApplicationDetails => Set<EducationApplicationDetails>();
    public DbSet<HealthApplicationDetails> HealthApplicationDetails => Set<HealthApplicationDetails>();
    public DbSet<ApplicationGuarantor> ApplicationGuarantors => Set<ApplicationGuarantor>();

    // Payments & ledger
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<FundTransaction> FundTransactions => Set<FundTransaction>();

    // Loans (qard al-hasan)
    public DbSet<LoanAgreement> LoanAgreements => Set<LoanAgreement>();
    public DbSet<LoanInstallment> LoanInstallments => Set<LoanInstallment>();
    public DbSet<LoanRepayment> LoanRepayments => Set<LoanRepayment>();

    // Documents
    public DbSet<Document> Documents => Set<Document>();

    /// <summary>
    /// Model-wide conventions instead of per-property configuration: every <see cref="decimal"/>
    /// column is money (`numeric(18,2)`), and every domain enum is stored as a short string so
    /// raw SQL reports stay human-readable — one line per enum type here beats a
    /// `.HasConversion&lt;string&gt;()` call scattered across every entity configuration.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HaveColumnType("numeric(18,2)");

        configurationBuilder.Properties<DonorType>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<PaymentMethod>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<DonationStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ApplicationStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ApplicationPriority>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<PaymentStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<Gender>().HaveConversion<string>().HaveMaxLength(10);
        configurationBuilder.Properties<MaritalStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<TransactionDirection>().HaveConversion<string>().HaveMaxLength(10);
        // 30, not 20: "LoanRepaymentReversal" (21 chars) is the longest current member, and this
        // leaves headroom for future reference types without another width migration.
        configurationBuilder.Properties<TransactionReferenceType>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<DocumentType>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<LoanAgreementStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<LoanInstallmentFrequency>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<LoanRepaymentStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<HouseStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ApplicationIntakeChannel>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ActivityVerb>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<FundEligibility>().HaveConversion<string>().HaveMaxLength(20);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Required by the fuzzy applicant-name search index (see ApplicantConfiguration).
        builder.HasPostgresExtension("pg_trgm");

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Rename the Identity tables to match this project's snake_case, business-named schema
        // instead of AspNetUsers/AspNetRoles/... — one place, not scattered per-entity config.
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<ApplicationRole>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        // Global soft-delete filter, applied once via reflection for every ISoftDeletable entity
        // instead of a per-entity HasQueryFilter call.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var property = System.Linq.Expressions.Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var notDeleted = System.Linq.Expressions.Expression.Not(property);
                var lambda = System.Linq.Expressions.Expression.Lambda(notDeleted, parameter);
                builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        FundCategorySeed.Apply(builder);
        ApplicationCategorySeed.Apply(builder);
        PermissionSeed.Apply(builder);
        RoleSeed.Apply(builder);
        AppSettingSeed.Apply(builder);
    }
}
