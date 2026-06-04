using Microsoft.EntityFrameworkCore;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Infrastructure.Data;

public class ToolingDbContext : DbContext
{
    public ToolingDbContext(DbContextOptions<ToolingDbContext> options) : base(options) { }

    public DbSet<ToolingRecord> ToolingRecords => Set<ToolingRecord>();
    public DbSet<ExtractionJob> ExtractionJobs => Set<ExtractionJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ToolingRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PartNumber);
            e.HasIndex(x => x.ToolListId);
            e.HasIndex(x => x.Operation);
            e.HasIndex(x => x.Machine);
            e.HasIndex(x => x.ExtractionJobId);
            e.HasIndex(x => x.SourceFileHash);
            e.HasIndex(x => x.WasAmendmentDetected);
            e.HasIndex(x => x.RevisionConflictDetected);
            e.HasIndex(x => new { x.PartNumber, x.Operation, x.Revision })
                .HasDatabaseName("IX_ToolingRecords_PartOpRev");
        });

        modelBuilder.Entity<ExtractionJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Records).WithOne().HasForeignKey(x => x.ExtractionJobId);
        });
    }
}
