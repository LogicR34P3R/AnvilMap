using AutoMapper.QueryableExtensions;
using AnvilMap.Benchmarks.AutoMapperConfig;
using AnvilMap.Benchmarks.Db;
using AnvilMap.Benchmarks.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AnvilMap.Benchmarks.ParityTests;

// The concrete, checkable version of this project's "true SQL-translatable projection by
// construction" claim: capture each provider's generated SQL for the graph fixture, and
// specifically check whether a [MapCondition] property
// leaks into AutoMapper's ProjectTo (which can't translate a runtime .Condition() clause)
// the way AnvilMap's ProjectTo{Dest} avoids by construction (AM005: the property is
// omitted from the projection entirely, not just left unconditioned).
public sealed class ProjectionTranslationTests
{
    private static (SqliteConnection connection, DbContextOptions<BenchmarkDbContext> options) CreateDatabase()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<BenchmarkDbContext>().UseSqlite(connection).Options;

        using var setup = new BenchmarkDbContext(options);
        setup.Database.EnsureCreated();

        setup.Blogs.Add(new GraphBlog
        {
            Title = "Benchmarks",
            Posts = { new GraphPost { Headline = "Post 1", Comments = { new GraphComment { Author = "Reader", Text = "Nice post!" } } } },
        });

        setup.ConditionalRecords.AddRange(
            new ConditionalSource { Name = "Public", Secret = "public-secret", IsRestricted = false },
            new ConditionalSource { Name = "Restricted", Secret = "restricted-secret", IsRestricted = true });

        setup.SaveChanges();

        return (connection, options);
    }

    [Fact]
    public void Graph_BothProvidersTranslateToSql_NoClientEvalFallback()
    {
        var (connection, options) = CreateDatabase();
        using var connectionScope = connection;

        var autoMapperConfig = BenchmarkMapperFactory.CreateConfiguration();

        using var db = new BenchmarkDbContext(options);

        var generatedQuery = db.Blogs.ProjectToGraphBlogDto();
        var autoMapperQuery = db.Blogs.ProjectTo<GraphBlogDto>(autoMapperConfig);

        var generatedSql = generatedQuery.ToQueryString();
        var autoMapperSql = autoMapperQuery.ToQueryString();

        // Both execute without throwing "could not be translated" - EF Core 3+ throws
        // rather than silently falling back to client evaluation, so a successful ToList()
        // here is itself evidence of full SQL translation for this shape.
        var generatedResults = generatedQuery.ToList();
        var autoMapperResults = autoMapperQuery.ToList();

        Assert.Single(generatedResults);
        Assert.Single(autoMapperResults);
        Assert.Contains("SELECT", generatedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SELECT", autoMapperSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Conditional_AnvilMapProjection_OmitsConditionalColumnFromSql()
    {
        var (connection, options) = CreateDatabase();
        using var connectionScope = connection;

        using var db = new BenchmarkDbContext(options);

        var sql = db.ConditionalRecords.ProjectToConditionalDto().ToQueryString();

        // AM005: a [MapCondition] property is left out of the projection entirely, so the
        // generated SQL never selects the column at all - the restricted row's secret never
        // leaves the database, regardless of what the caller does with the result.
        Assert.DoesNotContain("Secret", sql);

        var results = db.ConditionalRecords.ProjectToConditionalDto().ToList();
        Assert.All(results, dto => Assert.Equal("", dto.Secret));
    }

    [Fact]
    public void Graph_InlineInProjection_SplicesIntoSqlInsteadOfCallingIt()
    {
        var (connection, options) = CreateDatabase();
        using var connectionScope = connection;

        using var db = new BenchmarkDbContext(options);

        // HeadlineLength's [MapUsing] opts into InlineInProjection - the converter's own body
        // (source.Headline.Length) is spliced into the projection, so EF Core's Sqlite provider
        // translates it to a real SQL length(...) call instead of AnvilMap emitting an opaque
        // call to ComputeHeadlineLength that the provider could never translate.
        var sql = db.Blogs.ProjectToGraphBlogDto().ToQueryString();

        Assert.Contains("length(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ComputeHeadlineLength", sql);

        var results = db.Blogs.ProjectToGraphBlogDto().ToList();
        Assert.Equal("Post 1".Length, results.Single().Posts.Single().HeadlineLength);
    }

    [Fact]
    public void Conditional_AutoMapperProjection_IgnoresConditionAndLeaksColumn()
    {
        var (connection, options) = CreateDatabase();
        using var connectionScope = connection;

        var autoMapperConfig = BenchmarkMapperFactory.CreateConfiguration();

        using var db = new BenchmarkDbContext(options);

        var sql = db.ConditionalRecords.ProjectTo<ConditionalDto>(autoMapperConfig).ToQueryString();

        // AutoMapper's .Condition(...) is a runtime Func<TSource, bool> - it cannot be
        // translated into the projection expression, so ProjectTo ignores it and maps the
        // property unconditionally. The restricted row's secret is selected and returned
        // exactly like the unrestricted row's.
        Assert.Contains("Secret", sql);

        var results = db.ConditionalRecords.ProjectTo<ConditionalDto>(autoMapperConfig)
            .OrderBy(d => d.Name)
            .ToList();

        Assert.Equal("restricted-secret", results.Single(d => d.Name == "Restricted").Secret);
        Assert.Equal("public-secret", results.Single(d => d.Name == "Public").Secret);
    }
}
