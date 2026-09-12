namespace DyeFinder.Core;

// Game labels and material impressions remain distinct; no fitting occurs on export.
public sealed record CalibrationRecord(
    int SchemaVersion, DateTimeOffset CreatedUtc, string Sampler,
    string PanelImage, PointI PanelSize, PointI[] OriginalMarkers, string GeometrySource,
    Rgb[] Targets, double Threshold, bool PreferStable, Suggestion Suggestion,
    Rgb[]? ActualGameRgb, bool ActualValuesVerifiedAtSuggestedPosition,
    string Usefulness, string RenderingSettings, string MaterialAndAppearanceNotes, string SourceKind);
