using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Translation specific task handler
/// </summary>
public class TranslationTaskHandler : ITaskHandler
{
    public FieldsConfig GetFieldsConfig()
    {
        return new FieldsConfig
        {
            MandatoryFields = new List<string> { "input", "srcLang", "tgtLang" },
            OptionalFields = new List<string> { "model" },
            AllowedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Xenova/nllb-200-distilled-600M",
            },
            AllowedDtypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "q8" },
        };
    }

    public Task LoadInput(TaskRequest request)
    {
        // No special input loading needed for translation
        return Task.CompletedTask;
    }

    public Task ProcessInput(TaskRequest request)
    {
        if (request.Kwargs.ContainsKey("input") && request.Kwargs["input"] is string text)
        {
            if (!TextValidation.ValidateTextContent(text))
            {
                throw new InvalidOperationException("Invalid text content for translation");
            }
        }

        if (request.Kwargs.ContainsKey("srcLang") && request.Kwargs["srcLang"] is string sourceLang)
        {
            if (!TextValidation.ValidateLanguageCode(sourceLang))
            {
                throw new InvalidOperationException("Invalid source language code");
            }
        }

        if (request.Kwargs.ContainsKey("tgtLang") && request.Kwargs["tgtLang"] is string targetLang)
        {
            if (!TextValidation.ValidateLanguageCode(targetLang))
            {
                throw new InvalidOperationException("Invalid target language code");
            }
        }

        return Task.CompletedTask;
    }
}
