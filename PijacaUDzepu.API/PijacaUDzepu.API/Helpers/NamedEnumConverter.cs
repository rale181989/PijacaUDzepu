using System.Text.Json;
using System.Text.Json.Serialization;

namespace PijacaUDzepu.API.Helpers;

// Enumi u JSON-u: pri PRIJEMU samo nazivi ("Courier"), ne brojevi (7) ni brojevi kao tekst ("7"), da ne prođe
// nepostojeća vrednost; pri SLANJU nikad ne puca (vrednost bez naziva, npr. stari red u bazi, ide kao broj),
// da jedan loš red ne obori ceo spisak. JsonStringEnumConverter(allowIntegerValues: false) bi pucao i pri slanju.
public class NamedEnumConverter : JsonConverterFactory
{
    private readonly JsonStringEnumConverter _inner = new();

    public override bool CanConvert(Type typeToConvert) => _inner.CanConvert(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(Strict<>).MakeGenericType(typeToConvert),
            _inner.CreateConverter(typeToConvert, options))!;

    private class Strict<T> : JsonConverter<T> where T : struct, Enum
    {
        private readonly JsonConverter<T> _lenient;

        public Strict(JsonConverter lenient) => _lenient = (JsonConverter<T>)lenient;

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected the name of a {typeof(T).Name} value.");
            var text = reader.GetString();
            if (string.IsNullOrWhiteSpace(text) || long.TryParse(text, out _))
                throw new JsonException($"Expected the name of a {typeof(T).Name} value.");
            return _lenient.Read(ref reader, typeToConvert, options);
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            _lenient.Write(writer, value, options);
    }
}
