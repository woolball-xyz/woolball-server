public static class TextValidation
{
    /// <summary>
    /// Maximum text length allowed for processing (in characters)
    /// </summary>
    private const int MaxTextLength = 10000;

    /// <summary>
    /// Validates if text content is valid for processing
    /// </summary>
    /// <param name="text">Text content to validate</param>
    /// <returns>Whether the text content is valid</returns>
    public static bool ValidateTextContent(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (text.Length > MaxTextLength)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates if a language code is in the ISO 639-1 format
    /// </summary>
    /// <param name="languageCode">The language code to validate</param>
    /// <returns>Whether the language code is valid</returns>
    public static bool ValidateLanguageCode(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode) || languageCode.Length != 2)
        {
            return false;
        }

        // Check if it's a lowercase 2-letter code
        return languageCode.All(c => c >= 'a' && c <= 'z');
    }
} 