using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Zaster;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void AddSwagger()
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.Configure<RouteOptions>(options =>
            {
                options.LowercaseUrls = true;
            });

        }

        public void AddAngularFrontend()
        {
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAngularFrontend",
                policy =>
                {
                    policy.WithOrigins("http://localhost:4200")
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
            });
        }
    }
}
