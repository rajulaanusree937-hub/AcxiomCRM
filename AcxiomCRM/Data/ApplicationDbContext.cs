using System.Text.Json;
using AcxiomCRM.Services;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ICurrentUserService? _currentUser;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options,
                                ICurrentUserService? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Store enums as readable text ("Qualified") instead of numbers (2)
    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<CustomerStatus>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<LeadStatus>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<LeadSource>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<Priority>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<OpportunityStage>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<OpportunityStatus>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<FollowUpType>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<FollowUpStatus>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<ActivityType>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<ActivityStatus>().HaveConversion<string>().HaveMaxLength(30);

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Uniqueness rules (17.5)
        builder.Entity<Customer>().HasIndex(c => c.CustomerCode).IsUnique();
        builder.Entity<Customer>().HasIndex(c => c.Email).IsUnique();
        builder.Entity<Customer>().HasIndex(c => c.Phone).IsUnique();
        builder.Entity<Lead>().HasIndex(l => l.LeadCode).IsUnique();
        // Audit table is protected by a DB trigger (append-only, 4.9)
        builder.Entity<AuditLog>().ToTable(tb => tb.HasTrigger("TR_AuditLogs_AppendOnly"));

        // Prevent accidental cascade deletes between CRM tables
        var crmForeignKeys = builder.Model.GetEntityTypes()
            .Where(e => e.ClrType.Namespace == "AcxiomCRM.Models")
            .SelectMany(e => e.GetForeignKeys());

        foreach (var fk in crmForeignKeys)
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
    // ===================== AUTOMATIC AUDIT TRAIL (4.9, 17.16) =====================

    // Only these business tables are audited. ApplicationUser is deliberately NOT here
    // (it contains PasswordHash) — user/security events are logged explicitly instead.
    private static readonly HashSet<Type> AuditedTypes = new()
    {
        typeof(Customer), typeof(Lead), typeof(Opportunity), typeof(FollowUp), typeof(Activity)
    };

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var pending = CaptureChanges();
        var result = await base.SaveChangesAsync(cancellationToken);

        // Second pass: now that new rows have their IDs, write the audit records
        if (pending.Count > 0)
        {
            foreach (var p in pending)
                AuditLogs.Add(p.ToAuditLog(_currentUser));
            await base.SaveChangesAsync(cancellationToken);
        }
        return result;
    }

    private List<PendingAudit> CaptureChanges()
    {
        ChangeTracker.DetectChanges();
        var list = new List<PendingAudit>();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (!AuditedTypes.Contains(entry.Entity.GetType())) continue;

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();

            foreach (var prop in entry.Properties)
            {
                var name = prop.Metadata.Name;
                switch (entry.State)
                {
                    case EntityState.Added:
                        newValues[name] = prop.CurrentValue;
                        break;
                    case EntityState.Deleted:
                        oldValues[name] = prop.OriginalValue;
                        break;
                    case EntityState.Modified when prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue):
                        oldValues[name] = prop.OriginalValue;   // only the fields that actually changed
                        newValues[name] = prop.CurrentValue;
                        break;
                }
            }

            if (entry.State == EntityState.Modified && newValues.Count == 0) continue;   // nothing really changed

            var action = entry.State switch
            {
                EntityState.Added => AuditActions.Create,
                EntityState.Deleted => AuditActions.Delete,
                _ => AuditActions.Update
            };


            list.Add(new PendingAudit(entry, action, oldValues, newValues));
        }
        return list;
    }

    private sealed record PendingAudit(EntityEntry Entry, string Action,
                                       Dictionary<string, object?> OldValues,
                                       Dictionary<string, object?> NewValues)
    {
        public AuditLog ToAuditLog(ICurrentUserService? user) => new()
        {
            UserId = user?.UserId,
            Action = Action,
            EntityName = Entry.Entity.GetType().Name,
            RecordId = Entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString(),
            OldValue = OldValues.Count == 0 ? null : JsonSerializer.Serialize(OldValues, AuditService.JsonOptions),
            NewValue = NewValues.Count == 0 ? null : JsonSerializer.Serialize(NewValues, AuditService.JsonOptions),
            CreatedDate = DateTime.UtcNow,
            IpAddress = user?.IpAddress
        };
    }
}