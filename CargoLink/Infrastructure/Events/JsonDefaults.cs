using System.Text.Json;
using System.Text.Json.Serialization;

namespace CargoLink.Infrastructure.Events;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Default = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
