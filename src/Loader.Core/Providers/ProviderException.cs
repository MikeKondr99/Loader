namespace Loader.Core.Providers;

/// <summary>
/// Base exception for provider-level failures normalized by Loader.
/// </summary>
public abstract class ProviderException : Exception
{
    protected ProviderException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
