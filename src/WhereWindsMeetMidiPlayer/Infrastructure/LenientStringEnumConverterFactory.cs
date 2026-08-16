using System.Text.Json;
using System.Text.Json.Serialization;

namespace WhereWindsMeetMidiPlayer.Infrastructure;

/// <summary>
/// Enum converter that writes camelCase strings (like JsonStringEnumConverter) but never throws
/// on read: unknown strings map to the enum's default value. Settings written by a newer app
/// version (with new enum members) must not crash an older build reading the same file — that
/// exact failure took down startups when 1.3.0's "chromaticFfxiv37" reached pre-FFXIV exes.
/// </summary>
public sealed class LenientStringEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(LenientEnumConverter<>).MakeGenericType(typeToConvert))!;

    private sealed class LenientEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number when reader.TryGetInt32(out var number):
                    return (T)Enum.ToObject(typeof(T), number);
                case JsonTokenType.String:
                    var text = reader.GetString();
                    if (!string.IsNullOrWhiteSpace(text) && Enum.TryParse(text, ignoreCase: true, out T parsed))
                        return parsed;
                    return default;
                default:
                    return default;
            }
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WriteStringValue(JsonNamingPolicy.CamelCase.ConvertName(value.ToString()));
    }
}
