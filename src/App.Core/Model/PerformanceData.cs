namespace App.Core.Model;

/// <summary>
/// Port of Delphi <c>TPerfData</c> (PerfData.pas). SPEC.md section 2 records that
/// <c>AddDataPoint</c> refuses points beyond
/// <see cref="EsaLimits.MaxPerformancePoints"/> and warns the user; that behaviour
/// is added with the rest of the business rules in phase 4.
/// </summary>
public sealed class PerformanceData
{
    public List<PerformancePoint> Points { get; } = [];
}
