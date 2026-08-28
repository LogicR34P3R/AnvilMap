using AnvilMap.Benchmarks.Models;
using Microsoft.EntityFrameworkCore;

namespace AnvilMap.Benchmarks.Db;

// Mirrors samples/AnvilMap.Sample's SampleDbContext setup, against the Graph
// fixture, so ProjectionBenchmarks runs against a real query provider rather than
// IQueryable.AsEnumerable(), which would silently hide translation failures.
public sealed class BenchmarkDbContext : DbContext
{
    public BenchmarkDbContext(DbContextOptions<BenchmarkDbContext> options) : base(options)
    {
    }

    public DbSet<GraphBlog> Blogs => Set<GraphBlog>();
    public DbSet<GraphPost> Posts => Set<GraphPost>();
    public DbSet<GraphComment> Comments => Set<GraphComment>();

    // Not part of the throughput benchmarks - used by ProjectionTranslationTests to check
    // whether [MapCondition] properties leak into AutoMapper's ProjectTo the way this
    // project's "conditional mapping doesn't reach SQL projections" claim describes.
    public DbSet<ConditionalSource> ConditionalRecords => Set<ConditionalSource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GraphBlog>()
            .HasMany(b => b.Posts)
            .WithOne()
            .HasForeignKey(p => p.BlogId);

        modelBuilder.Entity<GraphPost>()
            .HasMany(p => p.Comments)
            .WithOne()
            .HasForeignKey(c => c.PostId);
    }
}
