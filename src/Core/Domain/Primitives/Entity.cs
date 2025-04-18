using System.ComponentModel.DataAnnotations;

namespace Domain.Primitives;

public abstract class Entity()
{
    [Timestamp]
    public byte[] Version { get; set; } = new byte[8];
}
