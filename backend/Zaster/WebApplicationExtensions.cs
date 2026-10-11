using Microsoft.AspNetCore.Builder;

namespace Zaster;

internal static class WebApplicationExtensions
{
    extension(WebApplication app)
    {
        public void AddSwagger()
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        /// <summary>
        /// Liefert das Angular-Build aus <c>wwwroot</c>. Im Development läuft das Frontend über
        /// <c>ng serve</c> mit Proxy auf <c>/api</c>, also ohne CORS.
        /// </summary>
        public void AddAngularFrontend()
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
            // Unbekannte Pfade gehen an Angular, außer unter /api: dort gibt es ein echtes 404.
            app.MapFallbackToFile("{*path:regex(^(?!api/).*$)}", "index.html");
        }
    }
}
