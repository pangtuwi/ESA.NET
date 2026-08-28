using App.Ui.ViewModels;

namespace App.Tests;

public sealed class RunTimeGraphOptionsTests
{
    [Fact]
    public void LoadingVelocitySelectionChecksVelocityOnly()
    {
        var viewModel = new RunTimeGraphOptionsViewModel();

        viewModel.Load(showGasFlowVelocities: true);

        Assert.False(viewModel.Pressure);
        Assert.True(viewModel.Velocity);
        Assert.False(viewModel.MassTransfer);
        Assert.True(viewModel.ShowGasFlowVelocities);
    }

    [Fact]
    public void CancelDoesNotAcceptTheSelection()
    {
        var viewModel = new RunTimeGraphOptionsViewModel();
        var closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;

        viewModel.Load(showGasFlowVelocities: false);
        viewModel.Velocity = true;
        viewModel.CancelCommand.Execute(null);

        Assert.True(closed);
        Assert.False(viewModel.Accepted);
    }

    [Fact]
    public void AcceptReturnsTheSelectedVelocityMode()
    {
        var viewModel = new RunTimeGraphOptionsViewModel();

        viewModel.Load(showGasFlowVelocities: false);
        viewModel.Velocity = true;
        viewModel.AcceptCommand.Execute(null);

        Assert.True(viewModel.Accepted);
        Assert.True(viewModel.ShowGasFlowVelocities);
    }
}
