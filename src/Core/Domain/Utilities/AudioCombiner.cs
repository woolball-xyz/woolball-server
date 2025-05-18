using System.Text;

public class AudioCombiner
{
    public static string CombineWavBase64(List<string> base64WavFiles)
    {
        List<byte[]> audioDataList = new();
        int totalAudioLength = 0;
        byte[] referenceHeader = null;

        foreach (var base64 in base64WavFiles)
        {
            byte[] wavBytes = Convert.FromBase64String(base64);

            if (referenceHeader == null)
            {
                referenceHeader = new byte[44];
                Array.Copy(wavBytes, 0, referenceHeader, 0, 44);
            }

            byte[] audioData = ExtractAudioData(wavBytes);
            audioDataList.Add(audioData);
            totalAudioLength += audioData.Length;
        }

        byte[] combinedWav = CreateWavFile(audioDataList, totalAudioLength, referenceHeader);

        return Convert.ToBase64String(combinedWav);
    }

    private static byte[] ExtractAudioData(byte[] wavBytes)
    {
        const int headerSize = 44;
        byte[] audioData = new byte[wavBytes.Length - headerSize];
        Array.Copy(wavBytes, headerSize, audioData, 0, audioData.Length);
        return audioData;
    }

    private static byte[] CreateWavFile(
        List<byte[]> audioDataList,
        int totalAudioLength,
        byte[] referenceHeader
    )
    {
        const int headerSize = 44;
        int fileSize = headerSize + totalAudioLength;

        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream);

        // Write RIFF Header
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(fileSize - 8); // File size - 8 bytes for 'RIFF' and file size field
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        // Write fmt chunk
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // PCM format chunk size
        writer.Write((short)1); // Audio format: PCM
        writer.Write(BitConverter.ToInt16(referenceHeader, 22)); // Number of channels
        writer.Write(BitConverter.ToInt32(referenceHeader, 24)); // Sample rate
        writer.Write(BitConverter.ToInt32(referenceHeader, 28)); // Byte rate
        writer.Write(BitConverter.ToInt16(referenceHeader, 32)); // Block align
        writer.Write(BitConverter.ToInt16(referenceHeader, 34)); // Bits per sample

        // Write data chunk
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(totalAudioLength); // Total audio data length

        // Write audio data
        foreach (var audioData in audioDataList)
        {
            writer.Write(audioData);
        }

        return memoryStream.ToArray();
    }
}
