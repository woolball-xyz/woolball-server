namespace Domain.Contracts;

public class AudioSegment
{
    public string RequestId { get; set; }
    public string FilePath { get; set; }
    public string Order { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
}
