namespace App.Core.Model;

/// <summary>
/// Port of the data held by Delphi <c>TEngine2z</c> (ICEngine2z.pas). The Delphi
/// class derives from <c>TRKF</c>; here the integrator state is composed rather
/// than inherited. All methods (<c>Run</c>, <c>Performance</c>, <c>InitVars</c>,
/// <c>getState</c>, the ODE derivative functions) are behaviour and arrive in
/// phase 4.
/// </summary>
/// <remarks>
/// The Delphi original exposed a single global instance <c>Engine2z</c>. That
/// global is deliberately not reproduced: the port forbids static mutable state,
/// so instances are constructed and passed explicitly.
/// </remarks>
public sealed class Engine
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Firing order, Delphi <c>FireOrder</c>.</summary>
    public string FireOrder { get; set; } = string.Empty;

    public IntegratorState Integration { get; } = new();

    public int ZoneCount { get; set; }

    public EngineState State { get; set; }

    public EngineState OldState { get; set; }

    /// <summary>Whether the two-zone model has been initialised, Delphi <c>INIT2Z</c>.</summary>
    public bool TwoZoneInitialised { get; set; }

    /// <summary>Delphi <c>TWOZOVERLAP</c>.</summary>
    public bool TwoZoneOverlap { get; set; }

    /// <summary>Delphi <c>SAVEMANFDATA</c>.</summary>
    public bool SaveManifoldData { get; set; }

    public bool VariableGamma { get; set; }

    /// <summary>Current crank angle, Delphi <c>CA</c>.</summary>
    public double CrankAngle { get; set; }

    /// <summary>Crank angle step, Delphi <c>dCA</c>.</summary>
    public double CrankAngleStep { get; set; }

    /// <summary>Engine speed in rev/min, Delphi <c>Nrpm</c>.</summary>
    public double Rpm { get; set; }

    /// <summary>Crank angular velocity in rad/s, Delphi <c>wcrank</c>.</summary>
    public double CrankAngularVelocity { get; set; }

    /// <summary>Cylinder count, Delphi <c>NCyl</c> (a Double in the original).</summary>
    public double CylinderCount { get; set; }

    public double Bore { get; set; }

    public double Stroke { get; set; }

    /// <summary>Compression ratio, Delphi <c>CR</c>.</summary>
    public double CompressionRatio { get; set; }

    public double ConrodLength { get; set; }

    /// <summary>Swept volume, Delphi <c>Vd</c>.</summary>
    public double SweptVolume { get; set; }

    public Gas Plenum { get; } = new();

    public Gas Exhaust { get; } = new();

    public Gas Cylinder { get; } = new();

    public Gas Atmosphere { get; } = new();

    public Manifolds Manifold { get; } = new();

    public WallTemperatureTable WallTemperature { get; } = new();

    public SpeedKeyedTable SparkAngle { get; } = new();

    /// <summary>Oil viscosity, Delphi <c>VOil</c>.</summary>
    public double OilViscosity { get; set; }

    public double WoshiniCoefficient { get; set; }

    /// <summary>Number of cycles to simulate, Delphi <c>NCycles</c>.</summary>
    public int CycleCount { get; set; }

    /// <summary>Timestep counter, Delphi <c>tstep</c>.</summary>
    public int TimeStep { get; set; }

    public double PlenumP6000 { get; set; }

    /// <summary>Cylinder pressure at inlet valve close, Delphi <c>PCylIVC</c>.</summary>
    public double PressureAtIvc { get; set; }

    /// <summary>Cylinder temperature at inlet valve close, Delphi <c>TCylIVC</c>.</summary>
    public double TemperatureAtIvc { get; set; }

    /// <summary>Cylinder volume at inlet valve close, Delphi <c>VCylIVC</c>.</summary>
    public double VolumeAtIvc { get; set; }

    /// <summary>Manifold pressure at inlet valve close, Delphi <c>PmanIVC</c>.</summary>
    public double ManifoldPressureAtIvc { get; set; }

    public double WorkDone { get; set; }

    public double HeatLoss { get; set; }

    /// <summary>Initial inlet valve pressure, Delphi <c>IPt</c>.</summary>
    public double InletValvePressure { get; set; }

    /// <summary>Initial exhaust valve pressure, Delphi <c>EPt</c>.</summary>
    public double ExhaustValvePressure { get; set; }

    /// <summary>Mass into the cylinder this step, Delphi <c>MIn</c>.</summary>
    public double MassIn { get; set; }

    /// <summary>Mass out of the cylinder this step, Delphi <c>Mout</c>.</summary>
    public double MassOut { get; set; }

    public double InletPressure { get; set; }

    public double ExhaustPressure { get; set; }

    public double InletVelocity { get; set; }

    public double ExhaustVelocity { get; set; }

    public double MassRecirculated { get; set; }

    public double NewAirMass { get; set; }

    /// <summary>Delphi <c>dPMass</c>.</summary>
    public double DPressureFromMass { get; set; }

    /// <summary>Forced exhaust gas recirculation, Delphi <c>ForcedEgr</c>.</summary>
    public double ForcedEgr { get; set; }

    public double InCylinderEgr { get; set; }

    public SpeciesValues Emissions { get; } = new();

    /// <summary>Pumping work, Delphi <c>PWork</c>.</summary>
    public double PumpingWork { get; set; }

    /// <summary>Indicated work, Delphi <c>WWork</c>.</summary>
    public double Work { get; set; }

    public double Fmep { get; set; }

    public double Imep { get; set; }

    public double Pmep { get; set; }

    public double Bmep { get; set; }

    public double Torque { get; set; }

    /// <summary>Brake power, Delphi <c>BPower</c>.</summary>
    public double BrakePower { get; set; }

    /// <summary>Indicated power, Delphi <c>IPower</c>.</summary>
    public double IndicatedPower { get; set; }

    /// <summary>Heat power, Delphi <c>HPower</c>.</summary>
    public double HeatPower { get; set; }

    /// <summary>Specific fuel consumption, Delphi <c>SFC</c>.</summary>
    public double Sfc { get; set; }

    /// <summary>Mechanical efficiency, Delphi <c>MEff</c>.</summary>
    public double MechanicalEfficiency { get; set; }

    /// <summary>Overall efficiency, Delphi <c>Eff</c>.</summary>
    public double Efficiency { get; set; }

    /// <summary>Thermal efficiency, Delphi <c>ThEff</c>.</summary>
    public double ThermalEfficiency { get; set; }

    /// <summary>Heat-release efficiency, Delphi <c>HEff</c>.</summary>
    public double HeatEfficiency { get; set; }

    /// <summary>Volumetric efficiency, Delphi <c>Veff</c>.</summary>
    public double VolumetricEfficiency { get; set; }

    /// <summary>Atmospheric reference mass, Delphi <c>MassAtm</c>.</summary>
    public double AtmosphericMass { get; set; }

    /// <summary>Fuel mass flow, Delphi <c>mf</c>.</summary>
    public double FuelMassFlow { get; set; }

    public double PeakPressure { get; set; }

    public double PeakTemperature { get; set; }

    /// <summary>Peak inlet velocity, Delphi <c>UIMax</c>.</summary>
    public double PeakInletVelocity { get; set; }

    /// <summary>Peak exhaust velocity, Delphi <c>UEMax</c>.</summary>
    public double PeakExhaustVelocity { get; set; }

    /// <summary>Cycle total of mass through the inlet valve, Delphi <c>TotalMInIV</c>.</summary>
    public double TotalMassInInletValve { get; set; }

    /// <summary>Cycle total of mass through the exhaust valve, Delphi <c>TotalMOutEV</c>.</summary>
    public double TotalMassOutExhaustValve { get; set; }

    public double TotalMass { get; set; }

    /// <summary>Burnt mass driven back out of the inlet during overlap, Delphi <c>MbOutInlet</c>.</summary>
    public double BurntMassOutInlet { get; set; }

    /// <summary>Unburnt mass lost to the exhaust during overlap, Delphi <c>MuOutExhaust</c>.</summary>
    public double UnburntMassOutExhaust { get; set; }

    public double ResidualFraction { get; set; }

    /// <summary>Burnt-zone heat release, Delphi <c>Qb</c>.</summary>
    public double Qb { get; set; }

    /// <summary>Unburnt-zone heat release, Delphi <c>Qu</c>.</summary>
    public double Qu { get; set; }

    /// <summary>Total heat release, Delphi <c>Q</c>.</summary>
    public double Q { get; set; }

    public double QFuel { get; set; }

    public double QHeat { get; set; }

    public double QWork { get; set; }

    /// <summary>Exhaust energy, Delphi <c>QExht</c>.</summary>
    public double QExhaust { get; set; }

    public double QPump { get; set; }

    /// <summary>Friction energy, Delphi <c>QFric</c>.</summary>
    public double QFriction { get; set; }

    /// <summary>Fuel energy, Delphi <c>FEnergy</c>.</summary>
    public double FuelEnergy { get; set; }
}
