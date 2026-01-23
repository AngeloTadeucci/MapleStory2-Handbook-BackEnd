using System.Text.Json;

namespace GameParser.Tools;

public static class SerializeOptions {
    public static JsonSerializerOptions Options = new() {
        IncludeFields = true,
    };
}
