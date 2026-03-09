// Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using geoback.Models;

namespace geoback.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Existing DbSets
        public DbSet<Facility> Facilities { get; set; }
        public DbSet<Milestone> Milestones { get; set; }
        public DbSet<DrawdownTranche> DrawdownTranches { get; set; }
        public DbSet<SiteVisitReport> SiteVisitReports { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<ApprovalTrailEntry> ApprovalTrailEntries { get; set; }
        public DbSet<ReportComment> ReportComments { get; set; }
        public DbSet<WorkProgress> WorkProgress { get; set; }
        public DbSet<Issue> Issues { get; set; }
        public DbSet<ReportPhoto> ReportPhotos { get; set; }
        
        // Authentication and Core Tables
        public DbSet<User> Users { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Checklist> Checklists { get; set; }
        
        // Comments table for QS reviews
        public DbSet<Comment> Comments { get; set; }

        // NEW: Locking system tables
        public DbSet<ReportLock> ReportLocks { get; set; }
        public DbSet<UserActiveLock> UserActiveLocks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Comment configuration (existing)
            modelBuilder.Entity<Comment>()
                .HasIndex(c => c.ReportId);

            modelBuilder.Entity<Comment>()
                .HasIndex(c => c.UserId);

            modelBuilder.Entity<Comment>()
                .HasIndex(c => c.CreatedAt);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Report)
                .WithMany(r => r.Comments)
                .HasForeignKey(c => c.ReportId)
                .OnDelete(DeleteBehavior.Cascade);

            // ReportLock configuration
            modelBuilder.Entity<ReportLock>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => e.ReportId).HasDatabaseName("idx_report");
                entity.HasIndex(e => e.UserId).HasDatabaseName("idx_user");
                entity.HasIndex(e => e.SessionId).HasDatabaseName("idx_session");
                entity.HasIndex(e => e.ExpiresAt).HasDatabaseName("idx_expires");
                entity.HasIndex(e => e.IsActive).HasDatabaseName("idx_active");

                entity.HasOne(e => e.Report)
                    .WithMany()
                    .HasForeignKey(e => e.ReportId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // UserActiveLock configuration
            modelBuilder.Entity<UserActiveLock>(entity =>
            {
                entity.HasKey(e => e.UserId);
                
                entity.HasIndex(e => new { e.UserId, e.ReportId }).HasDatabaseName("idx_user_report");
                entity.HasIndex(e => e.ExpiresAt).HasDatabaseName("idx_expires");

                entity.HasOne(e => e.Report)
                    .WithMany()
                    .HasForeignKey(e => e.ReportId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Add any other configurations you had here
        }
    }
}