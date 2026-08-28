using App.Core;
using App.Core.Model;
using App.Ui.Dialogs;
using App.Ui.ViewModels;
using App.Ui.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// The Single Speed Simulation dialog: that it asks for what the original asks for, that
/// its graph radio buttons drive the check boxes the same way, and that Run is what starts
/// a simulation.
/// </summary>
public sealed class SimulateOptionsTests
{
    private static SimulateOptionsViewModel Opened(
        double speed = 4000, int cycles = 6, double massBalance = 1)
    {
        var viewModel = new SimulateOptionsViewModel();

        viewModel.Load(
            new SimulationSettings { CycleCount = cycles, MassBalance = massBalance }, speed);

        return viewModel;
    }

    [Fact]
    public void TheFormOpensOnTheValuesFromEsaIni()
    {
        // Delphi FormCreate fills the three edits from iniEngineSpeed, iniNoCycles and
        // iniMassBalance.
        var viewModel = Opened(speed: 5500, cycles: 8, massBalance: 0.5);

        Assert.Equal(5500, viewModel.EngineSpeed);
        Assert.Equal(8, viewModel.TotalCycles);
        Assert.Equal(0.5, viewModel.MassBalance);

        // And on Graphs On, as the form definition has it.
        Assert.True(viewModel.GraphsOn);
        Assert.Equal(new GraphSelection(true, true, true), viewModel.Graphs);
    }

    [Fact]
    public void GraphsOnTicksAllThreeAndGreysThemOut()
    {
        var viewModel = Opened();

        viewModel.Selection = true;
        viewModel.GraphsOn = true;

        Assert.Equal(new GraphSelection(true, true, true), viewModel.Graphs);
        Assert.False(viewModel.CanChooseGraphs);
    }

    [Fact]
    public void GraphsOffClearsAllThreeAndGreysThemOut()
    {
        var viewModel = Opened();

        viewModel.GraphsOff = true;

        Assert.Equal(new GraphSelection(false, false, false), viewModel.Graphs);
        Assert.False(viewModel.Graphs.Any);
        Assert.False(viewModel.CanChooseGraphs);
    }

    [Fact]
    public void SelectionClearsAllThreeAndHandsThemToTheOperator()
    {
        var viewModel = Opened();

        viewModel.Selection = true;

        Assert.Equal(new GraphSelection(false, false, false), viewModel.Graphs);
        Assert.True(viewModel.CanChooseGraphs);

        viewModel.ShowPressureVolume = true;

        Assert.Equal(new GraphSelection(false, true, false), viewModel.Graphs);
        Assert.True(viewModel.Graphs.Any);
    }

    [Fact]
    public void RunAcceptsAndCancelDoesNot()
    {
        var run = Opened();
        var cancel = Opened();
        var closes = 0;

        run.CloseRequested += (_, _) => closes++;
        cancel.CloseRequested += (_, _) => closes++;

        Assert.False(run.Accepted);

        run.RunCommand.Execute(null);
        cancel.CancelCommand.Execute(null);

        Assert.True(run.Accepted);
        Assert.False(cancel.Accepted);
        Assert.Equal(2, closes);
    }

    [Theory]
    [InlineData(900, 1250)]
    [InlineData(9000, 7000)]
    [InlineData(4000, 4000)]
    public void RunClampsTheSpeedToTheOriginalsRange(double typed, double expected)
    {
        // Delphi's FormClose refuses to close outside 1250 to 7000 and rewrites the box to
        // the limit - which traps Cancel as well, since FormClose runs either way. Clamping
        // on Run gets the same run without the dead end.
        var viewModel = Opened(speed: typed);

        viewModel.RunCommand.Execute(null);

        Assert.Equal(expected, viewModel.EngineSpeed);
    }

    [Fact]
    public void AnOutOfRangeSpeedIsNamedBeforeRunIsPressed()
    {
        var viewModel = Opened(speed: 9000);

        Assert.NotNull(viewModel.SpeedWarning);

        viewModel.EngineSpeed = 4000;

        Assert.Null(viewModel.SpeedWarning);
    }

    [AvaloniaFact]
    public void TheWindowCarriesTheOriginalsFieldsButtonsAndOptions()
    {
        var window = new SimulateOptionsWindow { DataContext = Opened() };

        Assert.Equal("Single Speed Simulation", SimulateOptionsViewModel.Title);

        foreach (var name in new[] { "EngineSpeedBox", "TotalCyclesBox", "MassBalanceBox" })
        {
            Assert.NotNull(window.FindControl<TextBox>(name));
        }

        foreach (var name in new[] { "GraphsOnButton", "GraphsOffButton", "SelectionButton" })
        {
            Assert.NotNull(window.FindControl<RadioButton>(name));
        }

        foreach (var name in new[] { "GasFlowBox", "PressureVolumeBox", "InCylinderBox" })
        {
            Assert.NotNull(window.FindControl<CheckBox>(name));
        }

        foreach (var name in new[] { "CancelButton", "RunButton" })
        {
            var button = window.FindControl<Button>(name);

            Assert.NotNull(button);
            Assert.NotNull(button.Command);
        }
    }

    [Fact]
    public async Task RunningOpensTheDialogFirstAndCancelAbandonsTheRun()
    {
        BaselinePaths.Require();

        var options = new StubSimulateOptions { Accept = false };

        var viewModel = TestServices.Resolve<MainWindowViewModel>(
            services => services.AddSingleton<ISimulateOptionsWindowService>(options));

        viewModel.CurrentEngine = TestServices.Resolve<IEngineLoader>()
            .Load(BaselinePaths.File("A2China.eng"));

        await viewModel.SinglePointSimulationCommand.ExecuteAsync(null);

        Assert.Equal(1, options.Opened);

        // Cancel means nothing ran: no trace, no torque point, no status.
        Assert.Null(viewModel.Trace);
        Assert.Empty(viewModel.Performance.Points);
        Assert.Empty(viewModel.RunStatus);
    }

    [Fact]
    public async Task WhatTheDialogReturnsIsWhatTheRunUses()
    {
        BaselinePaths.Require();

        var options = new StubSimulateOptions
        {
            EngineSpeed = 3000,
            TotalCycles = 4,
            MassBalance = 2,
            Graphs = new GraphSelection(GasFlow: false, PressureVolume: true, InCylinder: false),
        };

        var viewModel = TestServices.Resolve<MainWindowViewModel>(
            services => services.AddSingleton<ISimulateOptionsWindowService>(options));

        viewModel.CurrentEngine = TestServices.Resolve<IEngineLoader>()
            .Load(BaselinePaths.File("A2China.eng"));

        await viewModel.SinglePointSimulationCommand.ExecuteAsync(null);

        Assert.Equal(3000, viewModel.EngineSpeed);
        Assert.Equal(3000, viewModel.CurrentEngine!.Engine.Rpm);
        Assert.Equal(4, viewModel.Settings.CycleCount);
        Assert.Equal(2, viewModel.Settings.MassBalance);

        // Delphi FormClose hard-codes this one whatever ESA.ini said.
        Assert.Equal(1, viewModel.Settings.OneZoneCycleCount);

        // And only the quadrant that was asked for is drawn.
        Assert.NotNull(viewModel.PressureVolumeChart);
        Assert.Null(viewModel.GasFlowChart);
        Assert.Null(viewModel.InCylinderChart);
    }
}
