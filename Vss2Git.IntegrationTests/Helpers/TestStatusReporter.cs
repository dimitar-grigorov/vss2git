namespace Hpdi.Vss2Git.IntegrationTests.Helpers;

/// <summary>
/// No-op status reporter for tests.
/// </summary>
public class TestStatusReporter : IStatusReporter
{
    public void Start() { }
    public void Stop() { }
}
