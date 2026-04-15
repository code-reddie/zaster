using Microsoft.AspNetCore.Builder;

namespace Zaster.Authentication;

internal static class WebApplicationExtensions
{
    extension(WebApplication app)
    {
        public void AddAuthentication()
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }
    }
}
