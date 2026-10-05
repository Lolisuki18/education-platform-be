using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace API.Models.Auth
{
    /// <summary>
    /// Accepts {"refreshToken": "..."} and, for clients written before this DTO existed, a bare JSON string.
    /// </summary>
    [JsonConverter(typeof(RefreshTokenRequestDtoConverter))]
    public class RefreshTokenRequestDto
    {
        [Required]
        [StringLength(512, MinimumLength = 1)]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RefreshTokenRequestDtoConverter : JsonConverter<RefreshTokenRequestDto>
    {
        public override RefreshTokenRequestDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return new RefreshTokenRequestDto { RefreshToken = reader.GetString() ?? string.Empty };

            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected a refresh token or an object containing refreshToken.");

            using var document = JsonDocument.ParseValue(ref reader);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.NameEquals("refreshToken") || property.Name.Equals("refreshToken", StringComparison.OrdinalIgnoreCase))
                {
                    return new RefreshTokenRequestDto
                    {
                        RefreshToken = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() ?? string.Empty : string.Empty
                    };
                }
            }

            return new RefreshTokenRequestDto();
        }

        public override void Write(Utf8JsonWriter writer, RefreshTokenRequestDto value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("refreshToken", value.RefreshToken);
            writer.WriteEndObject();
        }
    }
}
