public sealed class PreparationGenerator : IPreparationGenerator
{
    public Task<PreparationGenerationResult> RunAsync(string inputDirectory, string outputDirectory, bool testOnly, bool skipBalancing)
        => GenerationClass.GeneratePreparationAsync(inputDirectory, outputDirectory, testOnly, skipBalancing);
}
