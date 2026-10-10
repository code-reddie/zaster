using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Zaster.Notifications;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void AddNotifications(WebApplicationBuilder builder)
        {
            services.Configure<HomeAssistantOptions>(builder.Configuration.GetSection(HomeAssistantOptions.SectionName));
            services.AddHttpClient<HomeAssistantClient>(client => client.Timeout = TimeSpan.FromSeconds(10));
        }
    }
}
