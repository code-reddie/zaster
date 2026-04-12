using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Zaster.Database;

namespace Zaster;

internal sealed class Program
{
    internal static void Main()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddControllers();
        builder.Services.AddSwagger();
        builder.Services.AddAngularFrontend();

        var app = builder.Build();
        app.AddSwagger();
        app.AddAngularFrontend();
        app.UseRouting();
        app.MapControllers();

        app.Run();
    }
}
