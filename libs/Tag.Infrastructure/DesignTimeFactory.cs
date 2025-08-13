using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tag.Infrastructure;

public class DesignTimeFactory : IDesignTimeDbContextFactory<TagDbContext>
{
    public TagDbContext CreateDbContext(string[] args)
    {
        // Use env var if provided, else default to local Postgres
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__MainDb")
                 ?? "Host=localhost;Username=tag;Password=tagpw;Database=tagdb";

        var opts = new DbContextOptionsBuilder<TagDbContext>()
            .UseNpgsql(cs)
            .Options;

        return new TagDbContext(opts);
    }
}
