using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Zaster.Backup;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void AddBackup(WebApplicationBuilder builder)
        {
            services.Configure<BackupOptions>(builder.Configuration.GetSection(BackupOptions.SectionName));
            services.AddScoped<NasBackup>();
            services.AddHostedService<ScheduledBackupService>();
        }
    }
}
