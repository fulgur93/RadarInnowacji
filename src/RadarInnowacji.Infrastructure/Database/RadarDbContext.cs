using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Database;

/// <summary>
/// Kontekst danych Radar Innowacji. Dostawca jest wybierany w rejestracji DI,
/// co umożliwi przejście z SQLite na PostgreSQL bez zmiany encji.
/// </summary>
public sealed class RadarDbContext(DbContextOptions<RadarDbContext> options) : DbContext(options)
{
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<Discovery> Discoveries => Set<Discovery>();
    public DbSet<InnovationProject> InnovationProjects => Set<InnovationProject>();
    public DbSet<DiscoveryProject> DiscoveryProjects => Set<DiscoveryProject>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProjectCategory> ProjectCategories => Set<ProjectCategory>();
    public DbSet<ScanHistory> ScanHistory => Set<ScanHistory>();
    public DbSet<AiAnalysisRecord> AiAnalyses => Set<AiAnalysisRecord>();
    public DbSet<RepositoryInfo> Repositories => Set<RepositoryInfo>();
    public DbSet<ProjectEmbedding> ProjectEmbeddings => Set<ProjectEmbedding>();
    public DbSet<CategoryProposal> CategoryProposals => Set<CategoryProposal>();
    public DbSet<CategoryProposalProject> CategoryProposalProjects => Set<CategoryProposalProject>();
    public DbSet<CategoryHistory> CategoryHistories => Set<CategoryHistory>();
    public DbSet<ProjectMergeProposal> ProjectMergeProposals => Set<ProjectMergeProposal>();
    public DbSet<ProjectMergeHistory> ProjectMergeHistories => Set<ProjectMergeHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Source>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(ContentLimits.MaxTitleLength);
            entity.Property(x => x.Url).IsRequired().HasMaxLength(ContentLimits.MaxSourceUrlLength);
            entity.Property(x => x.Configuration).HasDefaultValue("{}");
            entity.Property(x => x.LastError).HasMaxLength(ContentLimits.MaxErrorLength);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => new { x.Radar, x.IsActive });
        });

        modelBuilder.Entity<Discovery>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(ContentLimits.MaxTitleLength);
            entity.Property(x => x.Description).HasMaxLength(ContentLimits.MaxDescriptionLength);
            entity.Property(x => x.RawContent).HasMaxLength(ContentLimits.MaxRawContentLength);
            entity.Property(x => x.SourceUrl).IsRequired().HasMaxLength(ContentLimits.MaxSourceUrlLength);
            entity.Property(x => x.AIAnalysis).HasMaxLength(ContentLimits.MaxDescriptionLength);
            entity.Property(x => x.ContentHash).HasMaxLength(128);

            entity.HasOne(x => x.Source)
                .WithMany(x => x.Discoveries)
                .HasForeignKey(x => x.SourceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.SourceId, x.ExternalId }).IsUnique();
            entity.HasIndex(x => x.ContentHash);
            entity.HasIndex(x => x.DiscoveredAt);
            entity.HasIndex(x => x.PublishedAt);
            entity.HasIndex(x => x.LastActivityAt);
            entity.HasIndex(x => x.ProcessingStatus);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.IsPotentialInnovation);
        });

        modelBuilder.Entity<RepositoryInfo>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Platform).IsRequired().HasMaxLength(32);
            entity.Property(x => x.ProjectId).IsRequired().HasMaxLength(ContentLimits.MaxTitleLength);
            entity.Property(x => x.FullName).IsRequired().HasMaxLength(ContentLimits.MaxTitleLength);
            entity.Property(x => x.License).HasMaxLength(200);
            entity.Property(x => x.HomepageUrl).HasMaxLength(ContentLimits.MaxSourceUrlLength);
            entity.Property(x => x.DemoUrl).HasMaxLength(ContentLimits.MaxSourceUrlLength);
            entity.Property(x => x.DefaultBranch).HasMaxLength(200);
            entity.Property(x => x.ReadmePath).HasMaxLength(500);
            entity.Property(x => x.Languages).HasMaxLength(4000).HasDefaultValue("{}");
            entity.Property(x => x.DetailsError).HasMaxLength(ContentLimits.MaxErrorLength);
            entity.Property(x => x.CanonicalKey).IsRequired().HasMaxLength(ContentLimits.MaxTitleLength).HasDefaultValue("");
            entity.Property(x => x.ReadmeHash).HasMaxLength(64);
            entity.Property(x => x.NoiseReason).HasMaxLength(300);

            entity.HasOne(x => x.Discovery)
                .WithOne(x => x.Repository)
                .HasForeignKey<RepositoryInfo>(x => x.DiscoveryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.DiscoveryId).IsUnique();
            entity.HasIndex(x => new { x.Platform, x.FullName });
            entity.HasIndex(x => x.DetailsFetchedAt);
            entity.HasIndex(x => x.Stars);
            entity.HasIndex(x => x.CanonicalKey);
            entity.HasIndex(x => x.ReadmeHash);
            entity.HasIndex(x => x.NoiseReason);
        });

        modelBuilder.Entity<InnovationProject>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(ContentLimits.MaxTitleLength);
            entity.Property(x => x.Description).HasMaxLength(ContentLimits.MaxDescriptionLength);
            entity.HasIndex(x => x.FreshnessScore);
            entity.HasIndex(x => x.FirstDiscoveredAt);
            entity.HasIndex(x => x.MergedIntoProjectId);
            entity.HasOne(x => x.MergedInto)
                .WithMany()
                .HasForeignKey(x => x.MergedIntoProjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DiscoveryProject>(entity =>
        {
            entity.HasKey(x => new { x.DiscoveryId, x.InnovationProjectId });
            entity.HasOne(x => x.Discovery)
                .WithMany(x => x.ProjectLinks)
                .HasForeignKey(x => x.DiscoveryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Project)
                .WithMany(x => x.DiscoveryLinks)
                .HasForeignKey(x => x.InnovationProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.InnovationProjectId);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(ContentLimits.MaxTitleLength);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.ParentCategoryId);
            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectCategory>(entity =>
        {
            entity.HasKey(x => new { x.InnovationProjectId, x.CategoryId });
            entity.HasOne(x => x.Project)
                .WithMany(x => x.CategoryLinks)
                .HasForeignKey(x => x.InnovationProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Category)
                .WithMany(x => x.ProjectLinks)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.CategoryId);
        });

        modelBuilder.Entity<ScanHistory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ErrorMessage).HasMaxLength(ContentLimits.MaxErrorLength);
            entity.HasOne(x => x.Source)
                .WithMany(x => x.Scans)
                .HasForeignKey(x => x.SourceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.SourceId, x.StartedAt });
        });

        modelBuilder.Entity<AiAnalysisRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Model).HasMaxLength(200);
            entity.Property(x => x.Reason).HasMaxLength(ContentLimits.MaxDescriptionLength);
            entity.Property(x => x.ResultJson).HasMaxLength(ContentLimits.MaxRawContentLength);
            entity.HasOne(x => x.Discovery)
                .WithMany(x => x.AiAnalyses)
                .HasForeignKey(x => x.DiscoveryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.DiscoveryId, x.AnalyzedAt });
        });

        modelBuilder.Entity<ProjectEmbedding>(entity =>
        {
            entity.HasKey(x => x.InnovationProjectId);
            entity.Property(x => x.Vector).IsRequired().HasDefaultValue("[]");
            entity.Property(x => x.Model).IsRequired().HasMaxLength(300);
            entity.HasOne(x => x.Project)
                .WithOne()
                .HasForeignKey<ProjectEmbedding>(x => x.InnovationProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.ClusterId);
            entity.HasIndex(x => x.Model);
        });

        modelBuilder.Entity<CategoryProposal>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(ContentLimits.MaxTitleLength);
            entity.Property(x => x.Description).HasMaxLength(ContentLimits.MaxDescriptionLength);
            entity.Property(x => x.Keywords).HasDefaultValue("[]");
            entity.Property(x => x.Signature).IsRequired().HasMaxLength(500);
            entity.HasIndex(x => x.Signature).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<CategoryProposalProject>(entity =>
        {
            entity.HasKey(x => new { x.CategoryProposalId, x.InnovationProjectId });
            entity.HasOne(x => x.Proposal)
                .WithMany(x => x.ProjectLinks)
                .HasForeignKey(x => x.CategoryProposalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.InnovationProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.InnovationProjectId);
        });

        modelBuilder.Entity<CategoryHistory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CategoryName).IsRequired().HasMaxLength(ContentLimits.MaxTitleLength);
            entity.Property(x => x.Change).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Details).HasMaxLength(600);
            entity.Property(x => x.Actor).IsRequired().HasMaxLength(50);
            entity.HasIndex(x => x.ChangedAt);
            entity.HasIndex(x => x.CategoryId);
        });

        modelBuilder.Entity<ProjectMergeProposal>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Signals).IsRequired().HasMaxLength(ContentLimits.MaxDescriptionLength);
            entity.Property(x => x.Signature).IsRequired().HasMaxLength(500);
            entity.Property(x => x.DecisionNote).HasMaxLength(ContentLimits.MaxErrorLength);
            entity.HasOne(x => x.SourceProject)
                .WithMany()
                .HasForeignKey(x => x.SourceProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.TargetProject)
                .WithMany()
                .HasForeignKey(x => x.TargetProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.Signature).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<ProjectMergeHistory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceTitle).IsRequired().HasMaxLength(ContentLimits.MaxTitleLength);
            entity.Property(x => x.TargetTitle).IsRequired().HasMaxLength(ContentLimits.MaxTitleLength);
            entity.Property(x => x.Details).IsRequired().HasMaxLength(ContentLimits.MaxDescriptionLength);
            entity.Property(x => x.Actor).IsRequired().HasMaxLength(50);
            entity.HasIndex(x => x.ChangedAt);
            entity.HasIndex(x => x.TargetProjectId);
        });
    }
}
