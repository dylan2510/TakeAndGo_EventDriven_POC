using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Tag.Infrastructure;

public static class ServiceCollectionExtensions
{
    // for startup project to call in program.cs to inject
    public static IServiceCollection AddTagDb(this IServiceCollection services, string connectionString)
        => services.AddDbContext<TagDbContext>(opt => opt.UseNpgsql(connectionString));
}
