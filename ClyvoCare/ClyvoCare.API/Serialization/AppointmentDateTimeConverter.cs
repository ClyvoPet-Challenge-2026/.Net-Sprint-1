using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClyvoCare.API.Serialization;

public sealed class AppointmentDateTimeConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Date/time must be an ISO 8601 string with an explicit timezone.");
        var text = reader.GetString();
        bool hasOffset = text is not null && (text.EndsWith('Z')
            || (text.Length >= 6 && text[^6] is '+' or '-' && text[^3] == ':'));
        if (!hasOffset || !reader.TryGetDateTimeOffset(out var value))
            throw new JsonException("Date/time must be an ISO 8601 string with Z or an explicit offset.");
        return value;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
