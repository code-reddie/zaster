using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Zaster;

/// <summary>
/// Ort für Datenbank und Schlüssel. Im Container ist das <c>/data</c> (Volume),
/// lokal in Development der Ordner <c>data</c> im Projektordner, damit die App ohne Handarbeit startet.
/// </summary>
internal static class DataPaths
{
    public const string ContainerDataDirectory = "/data";

    public const string DevelopmentDataDirectory = "data";

    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// Datenordner aus <c>DataDirectory</c>; ohne Eintrag <c>data</c> (Development) bzw. <c>/data</c>.
        /// </summary>
        public string GetDataDirectory()
        {
            var configured = builder.Configuration["DataDirectory"];
            if (string.IsNullOrWhiteSpace(configured))
            {
                configured = builder.Environment.IsDevelopment() ? DevelopmentDataDirectory : ContainerDataDirectory;
            }

            return builder.ResolvePath(configured);
        }

        /// <summary>
        /// Relative Pfade gelten ab dem Projektordner (Content Root), nicht ab dem Arbeitsverzeichnis.
        /// </summary>
        public string ResolvePath(string path) =>
            Path.GetFullPath(path, builder.Environment.ContentRootPath);
    }
}
