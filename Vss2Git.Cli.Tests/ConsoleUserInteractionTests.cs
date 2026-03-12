using FluentAssertions;

namespace Hpdi.Vss2Git.Cli.Tests;

public class ConsoleUserInteractionTests
{
    [Fact]
    public void ErrorCount_StartsAtZero()
    {
        var ui = new ConsoleUserInteraction(ignoreErrors: true, interactive: false, consoleLock: new object());

        ui.ErrorCount.Should().Be(0);
        ui.FatalErrorCount.Should().Be(0);
    }

    [Fact]
    public void ReportError_IncrementsErrorCount()
    {
        var ui = new ConsoleUserInteraction(ignoreErrors: true, interactive: false, consoleLock: new object());

        ui.ReportError("test error 1", ErrorActionOptions.AbortRetryIgnore);
        ui.ReportError("test error 2", ErrorActionOptions.AbortRetryIgnore);

        ui.ErrorCount.Should().Be(2);
    }

    [Fact]
    public void ShowFatalError_IncrementsFatalErrorCount()
    {
        var ui = new ConsoleUserInteraction(ignoreErrors: true, interactive: false, consoleLock: new object());

        ui.ShowFatalError("fatal error", new InvalidOperationException("test"));

        ui.FatalErrorCount.Should().Be(1);
    }

    [Fact]
    public void ReportError_IgnoreErrors_ReturnsIgnore()
    {
        var ui = new ConsoleUserInteraction(ignoreErrors: true, interactive: false, consoleLock: new object());

        var result = ui.ReportError("test error", ErrorActionOptions.AbortRetryIgnore);

        result.Should().Be(ErrorAction.Ignore);
        ui.ErrorCount.Should().Be(1, "error count should increment even when ignoring");
    }
}
