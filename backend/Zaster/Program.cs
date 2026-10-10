using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Zaster.Authentication;
using Zaster.Database;
using Zaster.FinTs;
using Zaster.Import;
using Zaster.Notifications;

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
        builder.Services.AddFinTs(builder);
        builder.Services.AddNotifications(builder);
        builder.Services.AddScoped<TransactionImporter>();

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
