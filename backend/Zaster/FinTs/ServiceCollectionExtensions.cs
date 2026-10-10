using System;
using System.IO;
using libfintx.Globals;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Zaster.FinTs;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void AddFinTs(WebApplicationBuilder builder)
        {
            var section = builder.Configuration.GetSection(FinTsOptions.SectionName);
            services.Configure<FinTsOptions>(section);

            var productId = section.GetValue<string>(nameof(FinTsOptions.ProductId));
            if (!string.IsNullOrWhiteSpace(productId))
            {
                FinTsGlobals.ProductId = productId;
            }

            var keyRingPath = section.GetValue<string>(nameof(FinTsOptions.KeyRingPath)) ?? new FinTsOptions().KeyRingPath;
            services.AddDataProtection()
                .SetApplicationName("Zaster")
                .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));

            services.AddSingleton<FinTsPinProtector>();
            services.AddScoped<FinTsService>();
            services.AddScoped<AccountSyncService>();
            services.TryAddSingleton(TimeProvider.System);
            services.AddHostedService<NightlySyncService>();
        }
    }
}
