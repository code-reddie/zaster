using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Zaster.Authentication;
using Zaster.Database;

namespace Zaster;

internal sealed class Program
{
    internal static void Main()
    {
        var builder = WebApplication.CreateBuilder();

        builder.AddAuthentication();

        builder.Services.AddControllers();
        builder.Services.AddSwagger();
        builder.Services.AddAngularFrontend();
        builder.Services.AddDatabase(builder);

        var app = builder.Build();
        app.AddSwagger();
        app.AddAngularFrontend();
        app.AddAuthentication();
        app.AddDatabase();
        app.UseRouting();
        app.MapControllers();

        app.Run();
    }
}
