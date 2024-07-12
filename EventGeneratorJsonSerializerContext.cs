using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EventGenerator
{
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(string[]))]
    internal partial class EventGeneratorJsonSerializerContext : JsonSerializerContext
    {
    }
}
