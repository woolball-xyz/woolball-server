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
    /// Validates if a language code is in the FLORES200 format (e.g., "eng_Latn", "rus_Cyrl")
    /// </summary>
    /// <param name="languageCode">The language code to validate</param>
    /// <returns>Whether the language code is valid</returns>
    public static bool ValidateLanguageCode(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
        {
            return false;
        }

        // FLORES200 format is typically "xxx_Yyyy" where:
        // - xxx is a 3-letter language code
        // - Yyyy is a 4-letter script code with first letter capitalized
        
        string[] parts = languageCode.Split('_');
        if (parts.Length != 2)
        {
            return false;
        }

        string langPart = parts[0];
        string scriptPart = parts[1];

        // Check if language part is a 3-letter code with lowercase letters
        if (langPart.Length != 3 || !langPart.All(c => c >= 'a' && c <= 'z'))
        {
            return false;
        }

        // Check if script part starts with uppercase and follows with lowercase
        if (scriptPart.Length < 4 || !char.IsUpper(scriptPart[0]))
        {
            return false;
        }

        for (int i = 1; i < scriptPart.Length; i++)
        {
            if (!char.IsLower(scriptPart[i]))
            {
                return false;
            }
        }

        return true;
    }
} 