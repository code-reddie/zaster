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

        public void AddAngularFrontend()
        {
            app.UseCors("AllowAngularFrontend");
            app.UseDefaultFiles(); // Looks for index.html
            app.UseStaticFiles();  // Allows access to files in wwwroot
            app.MapFallbackToFile("index.html");  // Redirects all unknown routes to Angular (index.html)
        }
    }

}
