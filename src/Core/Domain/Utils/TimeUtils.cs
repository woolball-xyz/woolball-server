namespace Domain.Utils;

public sealed class TimeUtils
{
    public static string FormatTimeToDuration(double time)
    {
        TimeSpan timeSpan = TimeSpan.FromSeconds(time);

        if (timeSpan.TotalSeconds < 60)
        {
            return $"0:{timeSpan.Seconds:D2}";
        }
        else if (timeSpan.TotalMinutes < 60)
        {
            return $"{timeSpan.Minutes:D1}:{timeSpan.Seconds:D2}";
        }
        else if (timeSpan.TotalHours < 24)
        {
            return $"{timeSpan.Hours:D1}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }
        else
        {
            int days = (int)timeSpan.TotalDays;
            int hours = timeSpan.Hours;
            int minutes = timeSpan.Minutes;
            int seconds = timeSpan.Seconds;
            return $"{days:D2}:{hours:D2}:{minutes:D2}:{seconds:D2}";
        }
    }
}
