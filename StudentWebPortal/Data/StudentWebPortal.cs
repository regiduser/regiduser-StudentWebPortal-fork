
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using StudentWebPortal.Model.Entity;

namespace StudentWebPortal.Data;

public sealed class StudentWebPortalContext(DbContextOptions<StudentWebPortalContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<HifzSession> HifzSessions => Set<HifzSession>();
    public DbSet<RevisionSession> RevisionSessions => Set<RevisionSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Student>()
            .HasIndex(s => s.Email)
            .IsUnique()
            .HasFilter("\"Email\" IS NOT NULL");

        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.HasIndex(a => new { a.StudentId, a.AttendanceDate }).IsUnique();
            entity.HasOne(a => a.Student)
                .WithMany()
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(a => a.SessionStatus)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(a => a.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
        });

        modelBuilder.Entity<HifzSession>()
            .Property(s => s.RowVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever();

        modelBuilder.Entity<RevisionSession>()
            .Property(s => s.RowVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever();
    }

    public override int SaveChanges()
    {
        ApplyAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditFields()
    {
        var now = DateTime.UtcNow;

        ApplyAudit(ChangeTracker.Entries<Student>(), (entity, state) =>
        {
            if (state == EntityState.Added)
                entity.CreatedAt = now;

            if (state == EntityState.Modified)
                entity.UpdatedAt = now;
        });

        ApplyAudit(ChangeTracker.Entries<Attendance>(), (entity, state) =>
        {
            if (state == EntityState.Added)
                entity.CreatedAt = now;

            if (state == EntityState.Modified)
                entity.UpdatedAt = now;

            if (state is EntityState.Added or EntityState.Modified)
                entity.RowVersion = Guid.NewGuid().ToByteArray();
        });

        ApplyAudit(ChangeTracker.Entries<HifzSession>(), (entity, state) =>
        {
            if (state == EntityState.Added)
                entity.CreatedAt = now;

            if (state is EntityState.Added or EntityState.Modified)
                entity.UpdatedAt = now;

            if (state is EntityState.Added or EntityState.Modified)
                entity.RowVersion = Guid.NewGuid().ToByteArray();
        });

        ApplyAudit(ChangeTracker.Entries<RevisionSession>(), (entity, state) =>
        {
            if (state == EntityState.Added)
                entity.CreatedAt = now;

            if (state is EntityState.Added or EntityState.Modified)
                entity.UpdatedAt = now;

            if (state is EntityState.Added or EntityState.Modified)
                entity.RowVersion = Guid.NewGuid().ToByteArray();
        });
    }

    private static void ApplyAudit<TEntity>(
        IEnumerable<EntityEntry<TEntity>> entries,
        Action<TEntity, EntityState> action)
        where TEntity : class
    {
        foreach (var entry in entries)
            action(entry.Entity, entry.State);
    }
}
