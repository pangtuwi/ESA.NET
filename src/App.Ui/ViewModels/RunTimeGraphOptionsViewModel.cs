using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App.Ui.ViewModels;

/// <summary>Choices in the run-time graph options dialog.</summary>
public sealed partial class RunTimeGraphOptionsViewModel : ObservableObject
{
    /// <summary>Whether the dialog was accepted.</summary>
    public bool Accepted { get; private set; }

    /// <summary>Raised when the dialog should close.</summary>
    public event EventHandler? CloseRequested;

    [ObservableProperty]
    private bool _pressure = true;

    [ObservableProperty]
    private bool _velocity;

    [ObservableProperty]
    private bool _massTransfer;

    public void Load(bool showGasFlowVelocities)
    {
        Pressure = !showGasFlowVelocities;
        Velocity = showGasFlowVelocities;
        MassTransfer = false;
        Accepted = false;
    }

    partial void OnPressureChanged(bool value)
    {
        if (value)
        {
            Velocity = false;
            MassTransfer = false;
        }
    }

    partial void OnVelocityChanged(bool value)
    {
        if (value)
        {
            Pressure = false;
            MassTransfer = false;
        }
    }

    partial void OnMassTransferChanged(bool value)
    {
        if (value)
        {
            Pressure = false;
            Velocity = false;
        }
    }

    public bool ShowGasFlowVelocities => Velocity;

    [RelayCommand]
    private void Accept()
    {
        Accepted = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
