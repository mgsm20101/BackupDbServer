using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackupDbServer.Helpers
{
    public class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
    {
        public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
                return new TimeOnly(0, 0);

            if (TimeOnly.TryParse(value, out var result))
                return result;

            return new TimeOnly(0, 0);
        }

        public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("HH:mm:ss"));
        }
    }
}
