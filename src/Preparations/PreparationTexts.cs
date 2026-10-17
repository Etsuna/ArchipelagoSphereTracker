using ArchipelagoSphereTracker.src.Resources;

public static class PreparationTexts
{
    public static string State(PreparationState state) => state switch
    {
        PreparationState.Open => Resource.PrepStateOpen,
        PreparationState.Closed => Resource.PrepStateClosed,
        PreparationState.Validating => Resource.PrepStateValidating,
        PreparationState.Generating => Resource.PrepStateGenerating,
        PreparationState.Generated => Resource.PrepStateGenerated,
        PreparationState.Attaching => Resource.PrepAttach,
        _ => Resource.PrepStateCancelled
    };
    public static string Validation(string status) => status switch
    {
        "Valid" => Resource.PrepValid,
        "Pending" => Resource.PrepPending,
        _ => Resource.PrepInvalid
    };
}
