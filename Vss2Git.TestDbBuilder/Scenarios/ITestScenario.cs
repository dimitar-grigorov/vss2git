namespace Hpdi.Vss2Git.TestDbBuilder.Scenarios;

public interface ITestScenario
{
    string Name { get; }
    string Description { get; }
    void Build(VssCommandRunner runner);
    void Verify(VssTestDatabaseVerifier verifier);
}
