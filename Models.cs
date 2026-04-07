namespace WinSpotlight;

public enum ResultCategory { App, File, Folder, Web, Math, Clipboard }

public class SearchResult
{
    public string         Title       { get; init; } = "";
    public string         Subtitle    { get; init; } = "";
    public ResultCategory Category    { get; init; }
    public string         ActionPath  { get; init; } = "";

    public string CategoryLabel => Category switch
    {
        ResultCategory.App       => "App",
        ResultCategory.File      => "File",
        ResultCategory.Folder    => "Folder",
        ResultCategory.Web       => "Web",
        ResultCategory.Math      => "Math",
        ResultCategory.Clipboard => "Clipboard",
        _                        => ""
    };

    // Emoji used when no file icon is available
    public string CategoryIcon => Category switch
    {
        ResultCategory.App       => "🖥",
        ResultCategory.File      => "📄",
        ResultCategory.Folder    => "📁",
        ResultCategory.Web       => "🌐",
        ResultCategory.Math      => "✕",   // overridden to "=" in template
        ResultCategory.Clipboard => "📋",
        _                        => "○"
    };

    // Math results show a special icon text
    public string DisplayIcon => Category == ResultCategory.Math ? "=" : CategoryIcon;
}
