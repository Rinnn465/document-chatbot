using Microsoft.EntityFrameworkCore;

namespace DocumentChatbot.Data;

public sealed class DocumentChatbotDbContext(DbContextOptions<DocumentChatbotDbContext> options)
    : DbContext(options)
{
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<CourseEntity> Courses => Set<CourseEntity>();
    public DbSet<CourseEnrollmentEntity> CourseEnrollments => Set<CourseEnrollmentEntity>();
    public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();
    public DbSet<ChatSessionEntity> ChatSessions => Set<ChatSessionEntity>();
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();
    public DbSet<CitationEntity> Citations => Set<CitationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoleEntity>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(x => x.RoleId);
            entity.Property(x => x.Name).HasMaxLength(30).IsUnicode(false);
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.Email).HasMaxLength(256).IsUnicode(false);
            entity.Property(x => x.DisplayName).HasMaxLength(150);
            entity.Property(x => x.PasswordHash).HasMaxLength(500);
            entity.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<CourseEntity>(entity =>
        {
            entity.ToTable("Courses");
            entity.HasKey(x => x.CourseId);
            entity.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<CourseEnrollmentEntity>(entity =>
        {
            entity.ToTable("CourseEnrollments");
            entity.HasKey(x => new { x.CourseId, x.StudentId });
        });

        modelBuilder.Entity<DocumentEntity>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(x => x.DocumentId);
            entity.Property(x => x.Title).HasMaxLength(255);
            entity.Property(x => x.Chapter).HasMaxLength(255);
            entity.Property(x => x.OriginalFileName).HasMaxLength(255);
            entity.Property(x => x.FileType).HasMaxLength(10).IsUnicode(false);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.ProcessingError).HasMaxLength(2000);
            entity.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId);
        });

        modelBuilder.Entity<ChatSessionEntity>(entity =>
        {
            entity.ToTable("ChatSessions");
            entity.HasKey(x => x.ChatSessionId);
            entity.Property(x => x.Title).HasMaxLength(255);
            entity.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.ChatSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessageEntity>(entity =>
        {
            entity.ToTable("ChatMessages");
            entity.HasKey(x => x.ChatMessageId);
            entity.Property(x => x.Role).HasMaxLength(10).IsUnicode(false);
            entity.Property(x => x.SentAtUtc).HasPrecision(7);
            entity.HasMany(x => x.Citations).WithOne().HasForeignKey(x => x.ChatMessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CitationEntity>(entity =>
        {
            entity.ToTable("Citations");
            entity.HasKey(x => x.CitationId);
            entity.Property(x => x.ChunkId).HasMaxLength(150).IsUnicode(false);
            entity.Property(x => x.Excerpt).HasMaxLength(1000);
            entity.Property(x => x.RelevanceScore).HasPrecision(6, 5);
            entity.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
