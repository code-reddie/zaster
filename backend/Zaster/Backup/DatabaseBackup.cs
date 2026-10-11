using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Zaster.Database;

namespace Zaster.Backup;

/// <summary>
/// Sicherung der ganzen Datenbank (alle Nutzer, Konten und Buchungen) als SQLite-Datei.
/// </summary>
internal static class DatabaseBackup
{
    /// <summary>
    /// Die Datei enthält die Daten aller Nutzer samt Passwort-Hashes. Herunterladen darf sie deshalb nur,
    /// wer Zaster eingerichtet hat: der Nutzer mit der kleinsten Id (der zuerst registrierte).
    /// </summary>
    public static async Task<bool> IsAllowedAsync(AppDbContext context, int userId, CancellationToken cancellationToken)
    {
        var firstUserId = await context.Users.MinAsync(u => (int?)u.Id, cancellationToken);
        return firstUserId == userId;
    }

    /// <summary>
    /// Schreibt eine konsistente Kopie der laufenden Datenbank nach <paramref name="path"/>.
    /// <c>VACUUM INTO</c> liest in einer Transaktion, gleichzeitige Schreibzugriffe landen also ganz oder gar nicht in der Kopie.
    /// Die Schlüssel für die FinTS-PINs liegen nicht in der Datenbank und sind nicht dabei.
    /// </summary>
    public static async Task WriteAsync(AppDbContext context, string path, CancellationToken cancellationToken)
    {
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "VACUUM INTO $path";
            command.Parameters.AddWithValue("$path", path);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }
}
