using System;

namespace Zaster.Notifications;

/// <summary>
/// Fehler bei der Verbindung zu Home Assistant. Die Meldung ist für Nutzer gedacht und enthält nie den Token.
/// </summary>
public sealed class HomeAssistantException(string message, Exception? innerException = null)
    : Exception(message, innerException);
