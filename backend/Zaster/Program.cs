using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zaster.Auth;
using Zaster.Database;
using Zaster.FinTs;
using Zaster.Notifications;
using Zaster.Transactions;

namespace Zaster;

internal sealed class Program
{
    internal static void Main()
    {
        var builder = WebApplication.CreateBuilder();

        builder.AddAuthentication();

        builder.Services.AddProblemDetails();
        builder.Services.AddSwagger();
        builder.Services.AddDatabase(builder);
        builder.Services.AddFinTs(builder);
        builder.Services.AddNotifications(builder);
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<TransactionImporter>();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.AddSwagger();
        app.AddAngularFrontend();
        app.AddAuthentication();
        app.AddDatabase();
        app.MapControllers();

        app.Run();
    }
}
