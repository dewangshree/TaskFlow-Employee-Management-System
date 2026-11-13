using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskFlow.Infrastructure.Messaging;

public static class MessagingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
