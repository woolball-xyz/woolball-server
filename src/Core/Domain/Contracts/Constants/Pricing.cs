public static class Pricing
{
    // Process pricing ratio (always 50% of usage pricing)
    private const decimal ProcessPricingRatio = 0.5m;

    // Base pricing for transaction types
    private static readonly Dictionary<TransactionType, decimal> BasePricingMap =
        new()
        {
            { TransactionType.TextGenerationInputUsage, 0.00000375m },
            { TransactionType.TextGenerationOutputUsage, 0.000015m },
            { TransactionType.VisionInputUsage, 0.00000004m },
            { TransactionType.VisionOutputUsage, 0.00000006m },
            { TransactionType.TextToSpeechUsage, 0.0000015m },
            { TransactionType.TranslationUsage, 0.000001m },
            { TransactionType.ZeroShotUsage, 0.00000000045m },
            { TransactionType.imageZeroShotUsage, 0.00000000045m },
            { TransactionType.FacialEmotionUsage, 0.0025m },
            { TransactionType.SummarizationUsage, 0.00000002m },
            { TransactionType.imageClassificationUsage, 0.00006m },
            { TransactionType.SpeechToTextUsage, 0.0005m },
        };

    // Derived pricing map for internal use
    private static readonly Dictionary<
        TransactionType,
        (decimal UsagePricing, decimal ProcessPricing)
    > PricingMap = BuildPricingMap();

    // Build the full pricing map from base prices
    private static Dictionary<
        TransactionType,
        (decimal UsagePricing, decimal ProcessPricing)
    > BuildPricingMap()
    {
        var result =
            new Dictionary<TransactionType, (decimal UsagePricing, decimal ProcessPricing)>();

        foreach (var entry in BasePricingMap)
        {
            result[entry.Key] = (entry.Value, entry.Value * ProcessPricingRatio);
        }

        return result;
    }

    // Base pricing for specific models
    private static readonly Dictionary<string, decimal> BaseModelPricingMap =
        new()
        {
            { "in-HuggingFaceTB/SmolLM2-135M-Instruct", 0.00000000375m },
            { "out-HuggingFaceTB/SmolLM2-135M-Instruct", 0.000000015m },
            { "in-HuggingFaceTB/SmolLM2-360M-Instruct", 0.000000005m },
            { "out-HuggingFaceTB/SmolLM2-360M-Instruct", 0.00000001625m },
            { "in-Mozilla/Qwen2.5-0.5B-Instruc", 0.00000000625m },
            { "out-Mozilla/Qwen2.5-0.5B-Instruc", 0.0000000175m },
            { "in-onnx-community/Qwen2.5-Coder-0.5B-Instruct", 0.00000000625m },
            { "out-onnx-community/Qwen2.5-Coder-0.5B-Instruct", 0.00000001875m },
        };

    // Derived pricing map for models
    private static readonly Dictionary<
        string,
        (decimal UsagePricing, decimal ProcessPricing)
    > ModelPricingMap = BuildModelPricingMap();

    // Build the full model pricing map from base prices
    private static Dictionary<
        string,
        (decimal UsagePricing, decimal ProcessPricing)
    > BuildModelPricingMap()
    {
        var result = new Dictionary<string, (decimal UsagePricing, decimal ProcessPricing)>();

        foreach (var entry in BaseModelPricingMap)
        {
            result[entry.Key] = (entry.Value, entry.Value * ProcessPricingRatio);
        }

        return result;
    }

    public static decimal CalculatePricing(
        decimal characters,
        TransactionType transactionType,
        OperationType operationType,
        string model
    )
    {
        if (characters < 0)
            throw new ArgumentOutOfRangeException(
                nameof(characters),
                "Characters must be non-negative."
            );

        if (
            !string.IsNullOrEmpty(model) && ModelPricingMap.TryGetValue(model, out var modelPricing)
        )
            return operationType == OperationType.ChargeUser
                ? characters * modelPricing.UsagePricing
                : characters * modelPricing.ProcessPricing;

        if (!PricingMap.TryGetValue(transactionType, out var pricing))
            throw new ArgumentException($"Invalid transaction type: {transactionType}");

        return operationType == OperationType.ChargeUser
            ? characters * pricing.UsagePricing
            : characters * pricing.ProcessPricing;
    }

    public static decimal GetRate(
        TransactionType transactionType,
        OperationType operationType,
        string model
    )
    {
        if (
            !string.IsNullOrEmpty(model) && ModelPricingMap.TryGetValue(model, out var modelPricing)
        )
            return operationType == OperationType.ChargeUser
                ? modelPricing.UsagePricing
                : modelPricing.ProcessPricing;

        if (!PricingMap.TryGetValue(transactionType, out var pricing))
            throw new ArgumentException($"Invalid transaction type: {transactionType}");

        return operationType == OperationType.ChargeUser
            ? pricing.UsagePricing
            : pricing.ProcessPricing;
    }
}
