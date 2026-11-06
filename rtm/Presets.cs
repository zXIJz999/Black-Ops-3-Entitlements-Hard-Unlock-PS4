namespace Bo3Rtm;

internal sealed record Preset(string Name, string Description, string Command);

internal static class Presets
{
    public static readonly Preset Entitlements = new(
        "Write entitlements",
        "Write category 5 keys 1..25, including modtools_callingcard (key 25).",
        "UNLOCK_ENT");
}
