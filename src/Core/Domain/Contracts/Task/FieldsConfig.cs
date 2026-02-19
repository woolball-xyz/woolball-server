using System.Collections.Generic;

public class FieldsConfig
{
    public List<string> MandatoryFields { get; set; } = new();
    public List<string> OptionalFields { get; set; } = new();
    public HashSet<string> AllowedModels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> AllowedDtypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
