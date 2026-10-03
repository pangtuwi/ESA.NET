using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace App.Tests;

/// <summary>
/// The Run-Time Graph Options dialog: all three of the original's groups, with Y-axis
/// scaling (ISSUES.md A27).
/// </summary>
public sealed class RunTimeGraphOptionsTests
{
    private static RunTimeGraphOptionsViewModel Loaded(GraphOptions options)
    {
        var viewModel = new RunTimeGraphOptionsViewModel();
        viewModel.Load(options);
        return viewModel;
    }

    [Fact]
    public void LoadingShowsWhatIsInForce()
    {
        var viewModel = Loaded(GraphOptions.Default with
        {
            GasFlow = GasFlowGraph.Velocity,
            InCylinder = false,
            PressureVolumeAxis = new AxisLimits(0.5, 80),
        });

        Assert.False(viewModel.GasFlowPressure);
        Assert.True(viewModel.GasFlowVelocity);
        Assert.True(viewModel.PressureVolume);
        Assert.True(viewModel.InCylinderNothing);
        Assert.Equal("0.5", viewModel.PressureVolumeMinimum);
        Assert.Equal("80", viewModel.PressureVolumeMaximum);
        Assert.Equal(string.Empty, viewModel.GasFlowMaximum);
    }

    [Fact]
    public void TheGasFlowButtonsAreOneChoice()
    {
        var viewModel = Loaded(GraphOptions.Default);

        viewModel.GasFlowMassTransfer = true;

        Assert.Equal(GasFlowGraph.MassTransfer, viewModel.GasFlow);
        Assert.False(viewModel.GasFlowPressure);

        viewModel.GasFlowNothing = true;

        Assert.Equal(GasFlowGraph.Nothing, viewModel.GasFlow);
        Assert.False(viewModel.GasFlowMassTransfer);
    }

    [Fact]
    public void AcceptReturnsEverythingTheDialogHolds()
    {
        var viewModel = Loaded(GraphOptions.Default);

        viewModel.GasFlowVelocity = true;
        viewModel.PressureVolumeNothing = true;
        viewModel.GasFlowMinimum = "-200";
        viewModel.InCylinderMaximum = "90";
        viewModel.AcceptCommand.Execute(null);

        Assert.True(viewModel.Accepted);
        Assert.Equal(
            GraphOptions.Default with
            {
                GasFlow = GasFlowGraph.Velocity,
                PressureVolume = false,
                GasFlowAxis = new AxisLimits(-200, null),
                InCylinderAxis = new AxisLimits(null, 90),
            },
            viewModel.Options);
    }

    [Fact]
    public void CancelDoesNotAcceptTheSelection()
    {
        var viewModel = Loaded(GraphOptions.Default);
        var closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;

        viewModel.GasFlowVelocity = true;
        viewModel.CancelCommand.Execute(null);

        Assert.True(closed);
        Assert.False(viewModel.Accepted);
    }

    [Theory]
    [InlineData("abc", "")]
    [InlineData("", "1e400")]
    [InlineData("10", "10")]
    [InlineData("20", "10")]
    public void ALimitThatCannotBeUsedBlocksOk(string minimum, string maximum)
    {
        var viewModel = Loaded(GraphOptions.Default);

        viewModel.InCylinderMinimum = minimum;
        viewModel.InCylinderMaximum = maximum;

        Assert.NotNull(viewModel.Error);
        Assert.Contains("In Cylinder", viewModel.Error, StringComparison.Ordinal);
        Assert.False(viewModel.AcceptCommand.CanExecute(null));

        viewModel.InCylinderMinimum = string.Empty;
        viewModel.InCylinderMaximum = string.Empty;

        Assert.Null(viewModel.Error);
        Assert.True(viewModel.AcceptCommand.CanExecute(null));
    }

    /// <summary>
    /// The SimulateOptions dialog's chart choice is written in before a run. Unlike the
    /// original, a chosen gas-flow mode survives it; only Nothing becomes Pressure.
    /// </summary>
    [Fact]
    public void TheSingleSpeedDialogsChoiceKeepsTheGasFlowMode()
    {
        var velocity = GraphOptions.Default with { GasFlow = GasFlowGraph.Velocity };

        Assert.Equal(GasFlowGraph.Velocity, velocity.WithSelection(new(true, true, true)).GasFlow);
        Assert.Equal(GasFlowGraph.Nothing, velocity.WithSelection(new(false, true, true)).GasFlow);

        var nothing = GraphOptions.Default with { GasFlow = GasFlowGraph.Nothing };
        var selected = nothing.WithSelection(new(true, false, true));

        Assert.Equal(GasFlowGraph.Pressure, selected.GasFlow);
        Assert.False(selected.PressureVolume);
        Assert.True(selected.InCylinder);
    }

    /// <summary>Every control in the window is bound: none sits there doing nothing.</summary>
    [AvaloniaFact]
    public void EveryControlInTheWindowIsBound()
    {
        var viewModel = Loaded(GraphOptions.Default);
        var window = new RunTimeGraphOptionsWindow { DataContext = viewModel };

        window.Show();

        try
        {
            string[] radios =
            [
                "GasFlowNothing", "GasFlowPressure", "GasFlowVelocity", "GasFlowMassTransfer",
                "PressureVolumeNothing", "PressureVolumeOn", "InCylinderNothing", "InCylinderOn",
            ];

            foreach (var name in radios)
            {
                var radio = window.FindControl<RadioButton>(name);

                Assert.NotNull(radio);
                Assert.True(radio.IsEnabled, $"{name} is disabled");
            }

            // Clicking a radio button moves the view model.
            window.FindControl<RadioButton>("GasFlowMassTransfer")!.IsChecked = true;
            Assert.Equal(GasFlowGraph.MassTransfer, viewModel.GasFlow);

            window.FindControl<RadioButton>("PressureVolumeNothing")!.IsChecked = true;
            Assert.False(viewModel.PressureVolume);

            // And typing into a limit box reaches it.
            window.FindControl<TextBox>("GasFlowMaximumBox")!.Text = "300";
            Assert.Equal("300", viewModel.GasFlowMaximum);

            foreach (var name in new[] { "OkButton", "CancelButton" })
            {
                Assert.NotNull(window.FindControl<Button>(name)?.Command);
            }
        }
        finally
        {
            window.Close();
        }
    }
}
