using System;
using System.Collections.Generic;

namespace Hpdi.Vss2Git.IntegrationTests.Helpers;

/// <summary>
/// Test implementation of IUserInteraction that auto-ignores errors
/// and logs all interactions for assertion.
/// </summary>
public class TestUserInteraction : IUserInteraction
{
    private readonly List<string> _errors = new();
    private readonly List<string> _fatalErrors = new();
    private readonly List<string> _confirmations = new();

    public IReadOnlyList<string> Errors => _errors;
    public IReadOnlyList<string> FatalErrors => _fatalErrors;
    public IReadOnlyList<string> Confirmations => _confirmations;

    public ErrorAction ReportError(string message, ErrorActionOptions options)
    {
        _errors.Add(message);
        return ErrorAction.Ignore;
    }

    public bool Confirm(string message, string title)
    {
        _confirmations.Add($"{title}: {message}");
        return true; // Always confirm (e.g. overwrite non-empty dir)
    }

    public void ShowFatalError(string message, Exception exception)
    {
        _fatalErrors.Add(exception != null ? $"{message}: {exception.Message}" : message);
    }
}
