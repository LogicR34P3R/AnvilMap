using GeneratedMapper.Sample.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeneratedMapper.Sample;

public sealed class SampleDbContext : DbContext
{
    public SampleDbContext(DbContextOptions<SampleDbContext> options) : base(options)
    {
    }

    public DbSet<Blog> Blogs => Set<Blog>();
    public DbSet<Post> Posts => Set<Post>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blog>()
            .HasMany(b => b.Posts)
            .WithOne()
            .HasForeignKey(p => p.BlogId);
    }
}
