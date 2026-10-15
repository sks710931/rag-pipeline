namespace RagPipeline.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RagPipeline.Persistence.Entities;

public class RagPipelineDbContext : IdentityUserContext<ApplicationUser, Guid, IdentityUserClaim<Guid>, IdentityUserLogin<Guid>, IdentityUserToken<Guid>>
{
    public RagPipelineDbContext(DbContextOptions<RagPipelineDbContext> options)
        : base(options)
    {
    }

    public DbSet<DeploymentBootstrapState> DeploymentBootstrapStates => Set<DeploymentBootstrapState>();
    public DbSet<DeploymentOwner> DeploymentOwners => Set<DeploymentOwner>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<TenantProfile> TenantProfiles => Set<TenantProfile>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<AuthSessionRevocation> AuthSessionRevocations => Set<AuthSessionRevocation>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Ignore unused ASP.NET Identity tables
        builder.Ignore<IdentityUserClaim<Guid>>();
        builder.Ignore<IdentityUserLogin<Guid>>();

        // 1. asp_net_users
        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("asp_net_users");
            b.Property(u => u.Id).HasColumnName("id");
            b.Property(u => u.UserName).HasColumnName("user_name").HasMaxLength(256).IsRequired();
            b.Property(u => u.NormalizedUserName).HasColumnName("normalized_user_name").HasMaxLength(256).IsRequired();
            b.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
            b.Property(u => u.SecurityStamp).HasColumnName("security_stamp").IsRequired();
            b.Property(u => u.ConcurrencyStamp).HasColumnName("concurrency_stamp").IsRequired();
            b.Property(u => u.LockoutEnabled).HasColumnName("lockout_enabled").IsRequired();
            b.Property(u => u.LockoutEnd).HasColumnName("lockout_end");
            b.Property(u => u.AccessFailedCount).HasColumnName("access_failed_count").IsRequired();

            b.Ignore(u => u.Email);
            b.Ignore(u => u.NormalizedEmail);
            b.Ignore(u => u.EmailConfirmed);
            b.Ignore(u => u.PhoneNumber);
            b.Ignore(u => u.PhoneNumberConfirmed);
            b.Ignore(u => u.TwoFactorEnabled);

            b.HasIndex(u => u.NormalizedUserName).IsUnique().HasDatabaseName("ix_asp_net_users_normalized_user_name");
        });

        // 2. asp_net_user_tokens
        builder.Entity<IdentityUserToken<Guid>>(b =>
        {
            b.ToTable("asp_net_user_tokens");
            b.Property(t => t.UserId).HasColumnName("user_id");
            b.Property(t => t.LoginProvider).HasColumnName("login_provider").HasMaxLength(128);
            b.Property(t => t.Name).HasColumnName("name").HasMaxLength(128);
            b.Property(t => t.Value).HasColumnName("value");

            b.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // 3. deployment_bootstrap_state
        builder.Entity<DeploymentBootstrapState>(b =>
        {
            b.ToTable("deployment_bootstrap_state", t =>
            {
                t.HasCheckConstraint("chk_bootstrap_singleton", "id = 1");
                t.HasCheckConstraint("chk_bootstrap_status", "status IN ('UNINITIALIZED', 'BOOTSTRAPPING', 'SEALED')");
            });
            b.HasKey(s => s.Id);
            b.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(s => s.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            b.Property(s => s.BootstrapProofHash).HasColumnName("bootstrap_proof_hash").HasMaxLength(128);
            b.Property(s => s.ProofExpiresAt).HasColumnName("proof_expires_at");
            b.Property(s => s.ProofConsumedAt).HasColumnName("proof_consumed_at");
            b.Property(s => s.SealedAt).HasColumnName("sealed_at");
            b.Property(s => s.SealedByUserId).HasColumnName("sealed_by_user_id");

            b.HasOne(s => s.SealedByUser).WithMany().HasForeignKey(s => s.SealedByUserId).OnDelete(DeleteBehavior.SetNull);

            b.HasData(new DeploymentBootstrapState
            {
                Id = 1,
                Status = "UNINITIALIZED"
            });
        });

        // 4. deployment_owners
        builder.Entity<DeploymentOwner>(b =>
        {
            b.ToTable("deployment_owners");
            b.HasKey(o => o.Id);
            b.Property(o => o.Id).HasColumnName("id");
            b.Property(o => o.UserId).HasColumnName("user_id").IsRequired();
            b.Property(o => o.Role).HasColumnName("role").HasMaxLength(64).IsRequired();
            b.Property(o => o.AssignedAt).HasColumnName("assigned_at").IsRequired();
            b.Property(o => o.AssignedBy).HasColumnName("assigned_by");

            b.HasIndex(o => new { o.UserId, o.Role }).IsUnique().HasDatabaseName("ix_deployment_owners_user_id_role");
            b.HasOne(o => o.User).WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // 5. tenants
        builder.Entity<Tenant>(b =>
        {
            b.ToTable("tenants");
            b.HasKey(t => t.Id);
            b.Property(t => t.Id).HasColumnName("id");
            b.Property(t => t.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
            b.Property(t => t.Slug).HasColumnName("slug").HasMaxLength(64).IsRequired();
            b.Property(t => t.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            b.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();

            b.HasIndex(t => t.Slug).IsUnique().HasDatabaseName("ix_tenants_slug");
        });

        // 6. tenant_memberships
        builder.Entity<TenantMembership>(b =>
        {
            b.ToTable("tenant_memberships");
            b.HasKey(m => new { m.TenantId, m.UserId });
            b.Property(m => m.TenantId).HasColumnName("tenant_id");
            b.Property(m => m.UserId).HasColumnName("user_id");
            b.Property(m => m.Role).HasColumnName("role").HasMaxLength(64).IsRequired();
            b.Property(m => m.IsFirstOwner).HasColumnName("is_first_owner").IsRequired().HasDefaultValue(false);
            b.Property(m => m.JoinedAt).HasColumnName("joined_at").IsRequired();

            b.HasOne(m => m.Tenant).WithMany(t => t.Memberships).HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(m => m.TenantId).HasFilter("is_first_owner = true").IsUnique().HasDatabaseName("uq_first_owner");
        });

        // 7. tenant_profiles
        builder.Entity<TenantProfile>(b =>
        {
            b.ToTable("tenant_profiles");
            b.HasKey(p => p.TenantId);
            b.Property(p => p.TenantId).HasColumnName("tenant_id");
            b.Property(p => p.DisplayName).HasColumnName("display_name").HasMaxLength(128).IsRequired();
            b.Property(p => p.SettingsJson).HasColumnName("settings_json").HasColumnType("jsonb").HasDefaultValue("{}").IsRequired();
            b.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
            b.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();
            b.Property(p => p.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken().IsRequired();

            b.HasOne(p => p.Tenant).WithOne(t => t.Profile).HasForeignKey<TenantProfile>(p => p.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        // 8. auth_sessions
        builder.Entity<AuthSession>(b =>
        {
            b.ToTable("auth_sessions");
            b.HasKey(s => s.Id);
            b.Property(s => s.Id).HasColumnName("id");
            b.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
            b.Property(s => s.RefreshTokenFamilyId).HasColumnName("refresh_token_family_id").IsRequired();
            b.Property(s => s.RefreshTokenHash).HasColumnName("refresh_token_hash").HasMaxLength(128).IsRequired();
            b.Property(s => s.ExpiresAt).HasColumnName("expires_at").IsRequired();
            b.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
            b.Property(s => s.LastSeenAt).HasColumnName("last_seen_at").IsRequired();

            b.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // 9. auth_session_revocations
        builder.Entity<AuthSessionRevocation>(b =>
        {
            b.ToTable("auth_session_revocations");
            b.HasKey(r => r.SessionId);
            b.Property(r => r.SessionId).HasColumnName("session_id");
            b.Property(r => r.RevokedAt).HasColumnName("revoked_at").IsRequired();
            b.Property(r => r.Reason).HasColumnName("reason").HasMaxLength(64).IsRequired();

            b.HasOne(r => r.Session).WithOne(s => s.Revocation).HasForeignKey<AuthSessionRevocation>(r => r.SessionId).OnDelete(DeleteBehavior.Cascade);
        });

        // 10. audit_events
        builder.Entity<AuditEvent>(b =>
        {
            b.ToTable("audit_events");
            b.HasKey(a => a.Id);
            b.Property(a => a.Id).HasColumnName("id");
            b.Property(a => a.OccurredAt).HasColumnName("occurred_at").HasDefaultValueSql("clock_timestamp()").IsRequired();
            b.Property(a => a.ActorUserId).HasColumnName("actor_user_id");
            b.Property(a => a.ActorType).HasColumnName("actor_type").HasMaxLength(32).IsRequired();
            b.Property(a => a.DeploymentId).HasColumnName("deployment_id");
            b.Property(a => a.TenantId).HasColumnName("tenant_id");
            b.Property(a => a.Action).HasColumnName("action").HasMaxLength(128).IsRequired();
            b.Property(a => a.TargetResource).HasColumnName("target_resource").HasMaxLength(128).IsRequired();
            b.Property(a => a.TargetId).HasColumnName("target_id").HasMaxLength(128);
            b.Property(a => a.Outcome).HasColumnName("outcome").HasMaxLength(32).IsRequired();
            b.Property(a => a.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64);
            b.Property(a => a.ClientIp).HasColumnName("client_ip").HasColumnType("inet");
            b.Property(a => a.DetailsJson).HasColumnName("details_json").HasColumnType("jsonb");
        });
    }

    public Guid? ActiveTenantTransactionId { get; internal set; }

    public virtual async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTenantTransactionAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        ActiveTenantTransactionId = tenantId;

        await Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.current_tenant_id', {0}, true);",
            [tenantId.ToString()],
            cancellationToken);

        return new TenantDbContextTransaction(transaction, this);
    }

    public override void Dispose()
    {
        ActiveTenantTransactionId = null;
        base.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        ActiveTenantTransactionId = null;
        await base.DisposeAsync();
    }
}

