using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

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

        // Prevent accidental cascade deletes between CRM tables
        var crmForeignKeys = builder.Model.GetEntityTypes()
            .Where(e => e.ClrType.Namespace == "AcxiomCRM.Models")
            .SelectMany(e => e.GetForeignKeys());

        foreach (var fk in crmForeignKeys)
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}