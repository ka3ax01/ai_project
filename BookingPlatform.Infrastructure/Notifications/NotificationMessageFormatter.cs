using System.Text.Json;

namespace BookingPlatform.Infrastructure.Notifications;

internal static class NotificationMessageFormatter
{
    public static string Format(string type, string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return "Notification";
        }

        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
            var root = document.RootElement;

            return type switch
            {
                "BookingCreated" => BuildBookingCreated(root),
                "BookingNeedsConfirmation" => BuildNeedsConfirmation(root),
                "BookingConfirmed" => "Booking confirmed",
                "BookingConfirmedAutomatically" => "Booking confirmed automatically",
                "BookingExpired" => "Booking expired and was cancelled",
                "BookingStarted" => "Booking started",
                "BookingCompleted" => "Booking completed",
                "BookingCancelled" => "Booking cancelled",
                "BookingNoShow" => "No-show recorded for your booking",
                _ => $"Notification: {type}"
            };
        }
        catch
        {
            return $"Notification: {type}";
        }
    }

    private static string BuildBookingCreated(JsonElement root)
    {
        var roomId = ReadString(root, "roomId");
        var start = ReadString(root, "startTimeUtc");
        var end = ReadString(root, "endTimeUtc");

        if (!string.IsNullOrWhiteSpace(roomId) && !string.IsNullOrWhiteSpace(start) && !string.IsNullOrWhiteSpace(end))
        {
            return $"Booking created: Room {roomId} {start}-{end}";
        }

        return "Booking created";
    }

    private static string BuildNeedsConfirmation(JsonElement root)
    {
        var confirmByUtc = ReadString(root, "confirmByUtc");
        return string.IsNullOrWhiteSpace(confirmByUtc)
            ? "Confirm booking before deadline"
            : $"Confirm booking before {confirmByUtc}";
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
    }
}
