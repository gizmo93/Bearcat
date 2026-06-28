namespace Bearcat.Abstractions.Updates;

public interface IAppVersionProvider
{
    string CurrentVersion { get; }
}
