using System.Text.Json;

namespace Domain.Utilities;

public static class PrivateArgsHelper
{
    public static int GetInt(Dictionary<string, object> args, string key, int defaultValue = 0)
    {
        if (!args.TryGetValue(key, out var value))
            return defaultValue;

        return value switch
        {
            int intValue => intValue,
            JsonElement { ValueKind: JsonValueKind.Number } je => je.GetInt32(),
            JsonElement { ValueKind: JsonValueKind.String } je
                when int.TryParse(je.GetString(), out var parsed) => parsed,
            not null when int.TryParse(value.ToString(), out var parsed) => parsed,
            _ => defaultValue
        };
    }
}
