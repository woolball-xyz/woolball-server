using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Domain.Contracts;

namespace Domain.Utilities
{
    /// <summary>
    /// Static class for managing FFmpeg operations
    /// </summary>
    public static class FFmpegManager
    {
        private const double MinSegmentDuration = 20.0;
        private const double MaxSegmentDuration = 25.0;

        public static async IAsyncEnumerable<AudioSegment> BreakAudioFile(string path)
        {
            var duration = await GetDurationAsync(path);
            var currentPosition = 0.0;
            var segmentNumber = 1;

            while (currentPosition < duration)
            {
                var segmentEnd = await DetermineSegmentEnd(path, currentPosition, duration);

                var outputPath = FileUtils.CreateSegmentPath(path, segmentNumber);
                await CutSegment(path, outputPath, currentPosition, segmentEnd - currentPosition);

                if (segmentEnd >= duration)
                {
                    Console.WriteLine("Last segment sent");
                }

                yield return new AudioSegment
                {
                    FilePath = outputPath,
                    Order = segmentNumber.ToString(),
                    StartTime = currentPosition,
                    EndTime = segmentEnd,
                    IsLast = segmentEnd >= duration,
                };

                currentPosition = segmentEnd;
                segmentNumber++;
            }
        }

        public static async Task<double?> FindSilencePoint(
            string fileName,
            double startPosition,
            double maxPosition
        )
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException(nameof(fileName));

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{fileName}\" -af silencedetect=n=-30dB:d=0.5 -f null -",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                string output = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var matches = Regex.Matches(
                    output,
                    @"silence_start: (\d+\.?\d*)",
                    RegexOptions.Multiline
                );

                foreach (Match match in matches)
                {
                    if (
                        double.TryParse(
                            match.Groups[1].Value,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double silencePoint
                        )
                    )
                    {
                        if (silencePoint > startPosition && silencePoint <= maxPosition)
                        {
                            return silencePoint;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding silence point: {ex.Message}");
                return null;
            }
        }

        private static async Task<double> FindOptimalSegmentEnd(
            string fileName,
            double currentPosition,
            double totalDuration
        )
        {
            double currentEnd = currentPosition;
            double accumulatedDuration = 0;

            while (
                accumulatedDuration < MinSegmentDuration
                && currentEnd < totalDuration
                && currentEnd - currentPosition < MaxSegmentDuration
            )
            {
                var nextSilence = await FindSilencePoint(
                    fileName,
                    currentEnd,
                    Math.Min(currentPosition + MaxSegmentDuration, totalDuration)
                );

                if (
                    !nextSilence.HasValue
                    || nextSilence.Value - currentPosition > MaxSegmentDuration
                )
                {
                    var end = Math.Min(currentPosition + MaxSegmentDuration, totalDuration);
                    return end;
                }

                accumulatedDuration = nextSilence.Value - currentPosition;

                if (accumulatedDuration < MinSegmentDuration)
                {
                    currentEnd = nextSilence.Value;
                    continue;
                }

                return nextSilence.Value;
            }

            var finalEnd =
                currentEnd > currentPosition
                    ? currentEnd
                    : Math.Min(currentPosition + MaxSegmentDuration, totalDuration);

            return finalEnd;
        }

        public static async Task CutSegment(
            string inputFile,
            string outputFile,
            double startTime,
            double duration
        )
        {
            if (string.IsNullOrEmpty(inputFile))
                throw new ArgumentNullException(nameof(inputFile));

            if (string.IsNullOrEmpty(outputFile))
                throw new ArgumentNullException(nameof(outputFile));

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments =
                        $"-i \"{inputFile}\" -ss {startTime} -t {duration} -acodec copy \"{outputFile}\" -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception("Failed to cut audio segment");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error cutting audio segment: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the duration of an audio or video file in seconds using ffprobe
        /// </summary>
        /// <param name="filePath">Path to the input file</param>
        /// <returns>Duration in seconds, or -1 if duration cannot be determined</returns>
        public static async Task<double> GetDurationAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(
                    nameof(filePath),
                    "Input file path cannot be null or empty"
                );

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Input file not found", filePath);

            try
            {
                Console.WriteLine($"Determining segment end for file: {filePath}");
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments =
                        $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    if (double.TryParse(output.Trim(), out double duration))
                    {
                        return duration;
                    }
                }

                return -1;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get file duration: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if the audio file has audio tracks using ffprobe
        /// </summary>
        /// <param name="inputFilePath">Input audio file path</param>
        /// <returns>True if the file has audio tracks, False otherwise</returns>
        public static async Task<bool> HasAudioTracksAsync(string inputFilePath)
        {
            if (string.IsNullOrEmpty(inputFilePath))
                throw new ArgumentNullException(
                    nameof(inputFilePath),
                    "Input file path cannot be null or empty"
                );

            if (!File.Exists(inputFilePath))
                throw new FileNotFoundException("Input file not found", inputFilePath);

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments =
                        $"-v error -select_streams a -show_entries stream=codec_type -of csv=p=0 \"{inputFilePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    return Regex.IsMatch(output, "audio", RegexOptions.IgnoreCase);
                }

                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to check audio tracks: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Converts an audio file to WAV format
        /// </summary>
        /// <param name="inputFilePath">Input audio file path</param>
        /// <returns>Path to the converted WAV file</returns>
        public static async Task<string> ConvertToWavAsync(string inputFilePath)
        {
            if (string.IsNullOrEmpty(inputFilePath))
                throw new ArgumentNullException(
                    nameof(inputFilePath),
                    "Input file path cannot be null or empty"
                );

            if (!File.Exists(inputFilePath))
                throw new FileNotFoundException("Input file not found", inputFilePath);

            bool hasAudioTracks = await HasAudioTracksAsync(inputFilePath);
            if (!hasAudioTracks)
                throw new InvalidOperationException("The file has no valid audio tracks for conversion");

            string outputDirectory = Path.GetDirectoryName(inputFilePath);
            string outputFileName = $"{Path.GetFileNameWithoutExtension(inputFilePath)}.wav";
            string outputFilePath = Path.Combine(outputDirectory, outputFileName);

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments =
                        $"-i \"{inputFilePath}\" -acodec pcm_s16le -ar 44100 -ac 2 \"{outputFilePath}\" -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Error converting file to WAV: {error}");
                }

                if (!File.Exists(outputFilePath))
                {
                    throw new Exception("WAV file was not generated");
                }

                if (inputFilePath != outputFilePath)
                {
                    try
                    {
                        File.Delete(inputFilePath);
                        Console.WriteLine($"Original file deleted: {inputFilePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not delete original file: {ex.Message}");
                    }
                }

                return outputFilePath;
            }
            catch (Exception ex)
            {
                if (File.Exists(outputFilePath))
                {
                    try
                    {
                        File.Delete(outputFilePath);
                    }
                    catch
                    {
                    }
                }

                throw new Exception($"Failed to convert file to WAV: {ex.Message}", ex);
            }
        }

        private static async Task<double> DetermineSegmentEnd(
            string fileName,
            double currentPosition,
            double totalDuration
        )
        {
            var segmentEnd = await FindOptimalSegmentEnd(fileName, currentPosition, totalDuration);
            return segmentEnd;
        }
    }
}
