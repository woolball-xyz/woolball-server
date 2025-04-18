namespace Domain.Contracts;

public class AudioSegment
{
    public required string FilePath { get; set; }
    public required string Order { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
}
