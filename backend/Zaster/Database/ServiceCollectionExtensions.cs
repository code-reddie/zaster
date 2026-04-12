using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Zaster.Database;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void AddDatabase(WebApplicationBuilder builder)
        {
            // 1. Load connection string from appsettings.json or specify directly
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                                   ?? "Data Source=zaster.db";

            // 2. Den DbContext registrieren (Standardmäßig "Scoped")
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(connectionString));
        }
    }
}
