namespace AST.Companion;

public enum AsterState
{
    Idle,
    DeliveringItem,
    Progression,
    Useful,
    Trap,
    Hint,
    Offline,
    Reconnect
}

public sealed record AsterNotification(
    AsterState State,
    string Title,
    string Subtitle,
    string Detail,
    TimeSpan Duration);

public static class AsterReactions
{
    public static AsterState FromItem(CompanionItem item) => item.Flag switch
    {
        "3" => AsterState.Progression,
        "1" => AsterState.Progression,
        "2" => AsterState.Useful,
        "4" => AsterState.Trap,
        _ => AsterState.DeliveringItem
    };

    public static string Label(AsterState state) => state switch
    {
        AsterState.Progression => "Progression !",
        AsterState.Useful => "Objet utile !",
        AsterState.Trap => "Piège !",
        AsterState.Hint => "Indice !",
        AsterState.Reconnect => "De retour !",
        AsterState.Offline => "Petite pause…",
        _ => "Nouvel objet !"
    };
}
