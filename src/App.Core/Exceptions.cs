namespace App.Core;

/// <summary>Port of Delphi <c>EEngineError</c> (ICEngine2z.pas).</summary>
public class EngineException : Exception
{
    public EngineException() { }

    public EngineException(string message) : base(message) { }

    public EngineException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Port of Delphi <c>ECFDError</c> (Manifolds.pas).</summary>
public class CfdException : Exception
{
    public CfdException() { }

    public CfdException(string message) : base(message) { }

    public CfdException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Port of Delphi <c>EEqbmError</c> (Eqbm.pas).</summary>
public class EquilibriumException : Exception
{
    public EquilibriumException() { }

    public EquilibriumException(string message) : base(message) { }

    public EquilibriumException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Port of Delphi <c>EGasPropsError</c> (GASPROPS.PAS).</summary>
public class GasPropertiesException : Exception
{
    public GasPropertiesException() { }

    public GasPropertiesException(string message) : base(message) { }

    public GasPropertiesException(string message, Exception innerException) : base(message, innerException) { }
}
