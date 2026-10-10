using libfintx.Globals;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

            services.AddScoped<FinTsService>();
        }
    }
}
