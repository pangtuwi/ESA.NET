namespace App.Core.Model;

/// <summary>
/// Port of Delphi <c>TPoint</c> (Profiles.pas). The Delphi record carries a
/// <c>next</c> pointer; SPEC.md section 2 requires that pointer-linked records are
/// never persisted by memory copy, so the linked list becomes an ordinary ordered
/// collection on <see cref="CamProfile"/> and the pointer is dropped.
/// </summary>
/// <param name="X">Crank angle or normalised position.</param>
/// <param name="Y">Lift or area at <paramref name="X"/>.</param>
public readonly record struct ProfilePoint(double X, double Y);
