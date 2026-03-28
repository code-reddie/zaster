using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Zaster;

internal sealed class Program
{
    internal static void Main()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddControllers();
        builder.Services.AddCors(options =>
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

        // Add Swagger services
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();
        app.UseCors("AllowAngularFrontend");

        // Enable Swagger middleware
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseDefaultFiles(); // Looks for index.html
        app.UseStaticFiles();  // Allows access to files in wwwroot

        app.UseRouting();
        app.MapControllers();

        // Important: Redirects all unknown routes to Angular (index.html)
        app.MapFallbackToFile("index.html");
        
        app.Run();
    }
}
