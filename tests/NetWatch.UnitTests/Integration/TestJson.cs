using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetWatch.UnitTests.Integration;

/// <summary>
/// Serializer settings matching the API's.
///
/// The API writes enums as names; a client using the framework defaults would fail to
/// read "Switch" back into a <c>DeviceCategory</c>. Sharing one options instance keeps the
/// tests honest about what a real consumer has to configure.
/// </summary>
internal static class TestJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
