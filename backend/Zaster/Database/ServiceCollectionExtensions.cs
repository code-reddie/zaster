using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
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
            // Ohne eigenen Eintrag liegt die Datenbank im Datenordner (/data bzw. lokal ./data).
            var connection = new SqliteConnectionStringBuilder(
                builder.Configuration.GetConnectionString("DefaultConnection")
                ?? $"Data Source={Path.Combine(builder.GetDataDirectory(), "zaster.db")}");

            if (connection.DataSource != ":memory:" && !connection.DataSource.StartsWith("file:"))
            {
                connection.DataSource = builder.ResolvePath(connection.DataSource);
                // SQLite legt die Datei an, aber keine fehlenden Ordner.
                Directory.CreateDirectory(Path.GetDirectoryName(connection.DataSource)!);
            }

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(connection.ToString()));
        }
    }
}
