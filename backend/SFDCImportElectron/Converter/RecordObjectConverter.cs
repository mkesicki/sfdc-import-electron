using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SFDCImportElectron.Model;
using System;

namespace SFDCImportElectron.Converter
{
    internal class RecordObjectConverter : CustomCreationConverter<Record>
    {
        public override Record Create(Type objectType)
        {
            return new Record();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            // Write properties.
            var propertyInfos = value.GetType().GetProperties();
            foreach (var propertyInfo in propertyInfos)
            {
                // Skip the children & fields property.
                if (propertyInfo.Name == "children" || propertyInfo.Name == "fields") continue;

                writer.WritePropertyName(propertyInfo.Name);
                var propertyValue = propertyInfo.GetValue(value);
                serializer.Serialize(writer, propertyValue);
            }

            // Write dictionary key-value pairs.
            var record = (Record)value;
            if (record.children != null)
            {
                foreach (var kvp in record.children)
                {
                    writer.WritePropertyName(kvp.Key);
                    serializer.Serialize(writer, kvp.Value);
                }
            }

            if (record.fields != null)
            {
                foreach (var kvp in record.fields)
                {
                    writer.WritePropertyName(kvp.Key);
                    serializer.Serialize(writer, kvp.Value);
                }
            }
            writer.WriteEndObject();
        }
        public override bool CanWrite
        {
            get { return true; }
        }

    }
}
