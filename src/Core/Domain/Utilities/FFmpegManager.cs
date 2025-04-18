using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Domain.Contracts;

namespace Domain.Utilities
{
    /// <summary>
    /// Classe estática para gerenciar operações FFmpeg
    /// </summary>
    public static class FFmpegManager
    {
        private const double MinSegmentDuration = 20.0; // Minimum segment duration in seconds
        private const double MaxSegmentDuration = 30.0; // Maximum segment duration in seconds

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

                yield return new AudioSegment
                {
                    FilePath = outputPath,
                    Order = segmentNumber.ToString(),
                    StartTime = currentPosition,
                    EndTime = segmentEnd,
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
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments =
                    $"-v warning -ss {startPosition} -i \"{fileName}\" -t {maxPosition - startPosition} -af silencedetect=noise=-25dB:d=0.10 -f null -",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            double? firstSilence = null;

            while (!process.StandardError.EndOfStream)
            {
                var line = await process.StandardError.ReadLineAsync();
                if (line?.Contains("silence_start") == true)
                {
                    var timeStr = line.Split("silence_start: ")[1].Split(' ')[0];
                    if (double.TryParse(timeStr, out var time))
                    {
                        var absoluteTime = startPosition + time;
                        if (absoluteTime > startPosition)
                        {
                            firstSilence = absoluteTime;
                            break;
                        }
                    }
                }
            }

            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch { }

            return firstSilence;
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
                // Procurar próximo ponto de silêncio
                var nextSilence = await FindSilencePoint(
                    fileName,
                    currentEnd,
                    Math.Min(currentPosition + MaxSegmentDuration, totalDuration)
                );

                // Se não encontrou silêncio ou atingiu o limite máximo
                if (
                    !nextSilence.HasValue
                    || nextSilence.Value - currentPosition > MaxSegmentDuration
                )
                {
                    var end = Math.Min(currentPosition + MaxSegmentDuration, totalDuration);
                    return end;
                }

                // Atualizar a duração acumulada
                accumulatedDuration = nextSilence.Value - currentPosition;

                // Se a duração acumulada é menor que o mínimo, continue procurando
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
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments =
                    $"-y -ss {startTime} -i \"{inputFile}\" -t {duration} -c copy -avoid_negative_ts 1 \"{outputFile}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg failed to cut segment. Exit code: {process.ExitCode}");
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
        /// Verifica se o arquivo de áudio possui faixas de áudio usando ffprobe
        /// </summary>
        /// <param name="inputFilePath">Caminho do arquivo de áudio de entrada</param>
        /// <returns>True se o arquivo possui faixas de áudio, False caso contrário</returns>
        public static async Task<bool> HasAudioTracksAsync(string inputFilePath)
        {
            if (string.IsNullOrEmpty(inputFilePath))
                throw new ArgumentNullException(
                    nameof(inputFilePath),
                    "O caminho do arquivo de entrada não pode ser nulo ou vazio"
                );

            if (!File.Exists(inputFilePath))
                throw new FileNotFoundException(
                    "O arquivo de entrada não foi encontrado",
                    inputFilePath
                );

            try
            {
                // Configura o processo ffprobe
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

                // Inicia o processo
                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                // Captura a saída
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                // Aguarda a conclusão do processo
                await process.WaitForExitAsync();

                // Verifica se o processo foi concluído com sucesso e se encontrou faixas de áudio
                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    // Verifica se a saída contém "audio"
                    return Regex.IsMatch(output, "audio", RegexOptions.IgnoreCase);
                }

                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Falha ao verificar faixas de áudio: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Converte um arquivo de áudio para o formato WAV
        /// </summary>
        /// <param name="inputFilePath">Caminho do arquivo de áudio de entrada</param>
        /// <returns>Caminho do arquivo WAV convertido</returns>
        public static async Task<string> ConvertToWavAsync(string inputFilePath)
        {
            if (string.IsNullOrEmpty(inputFilePath))
                throw new ArgumentNullException(
                    nameof(inputFilePath),
                    "O caminho do arquivo de entrada não pode ser nulo ou vazio"
                );

            if (!File.Exists(inputFilePath))
                throw new FileNotFoundException(
                    "O arquivo de entrada não foi encontrado",
                    inputFilePath
                );

            // Verifica se o arquivo possui faixas de áudio antes de prosseguir com a conversão
            bool hasAudioTracks = await HasAudioTracksAsync(inputFilePath);
            if (!hasAudioTracks)
                throw new InvalidOperationException(
                    "O arquivo não possui faixas de áudio válidas para conversão"
                );

            // Gera o caminho para o arquivo de saída WAV
            string outputDirectory = Path.GetDirectoryName(inputFilePath);
            string outputFileName = $"{Path.GetFileNameWithoutExtension(inputFilePath)}.wav";
            string outputFilePath = Path.Combine(outputDirectory, outputFileName);

            try
            {
                // Configura o processo FFmpeg
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

                // Inicia o processo
                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                // Captura a saída e erro
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                // Aguarda a conclusão do processo
                await process.WaitForExitAsync();

                // Verifica se o processo foi concluído com sucesso
                if (process.ExitCode != 0)
                {
                    throw new Exception($"Erro ao converter o arquivo para WAV: {error}");
                }

                // Verifica se o arquivo de saída foi criado
                if (!File.Exists(outputFilePath))
                {
                    throw new Exception("O arquivo WAV não foi gerado");
                }

                // Se a conversão foi bem-sucedida, exclui o arquivo original
                if (inputFilePath != outputFilePath)
                {
                    try
                    {
                        File.Delete(inputFilePath);
                        Console.WriteLine($"Arquivo original excluído: {inputFilePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"Aviso: Não foi possível excluir o arquivo original: {ex.Message}"
                        );
                        // Continua a execução mesmo se não conseguir excluir o arquivo original
                    }
                }

                return outputFilePath;
            }
            catch (Exception ex)
            {
                // Limpa o arquivo de saída se ele existir e ocorreu um erro
                if (File.Exists(outputFilePath))
                {
                    try
                    {
                        File.Delete(outputFilePath);
                    }
                    catch
                    {
                        // Ignora erros ao tentar excluir o arquivo
                    }
                }

                throw new Exception($"Falha ao converter o arquivo para WAV: {ex.Message}", ex);
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
