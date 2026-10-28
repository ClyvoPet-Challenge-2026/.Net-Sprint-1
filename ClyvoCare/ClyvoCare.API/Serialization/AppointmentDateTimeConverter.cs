using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ClyvoCare.API.Serialization;

/// <summary>Exige fuso explícito para não interpretar o horário pelo fuso do servidor.</summary>
public sealed class AppointmentDateTimeConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Informe data e horário ISO 8601 com fuso horário.");
        var text = reader.GetString();
        if (text is null || !text.Contains('T') || !Regex.IsMatch(text, @"(Z|[+-]\d{2}:\d{2})$")
            || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
            throw new JsonException("Informe data e horário ISO 8601 com Z ou offset, por exemplo -03:00.");
        return value;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
