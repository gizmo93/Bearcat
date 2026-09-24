namespace Bearcat.Domain.UseCases.ManageRemoteSourceAutomations;

public static class RemotePathNormalizer
{
    private const char Separator = '/';

    public static string Normalize(string remotePath)
    {
        if (string.IsNullOrWhiteSpace(remotePath))
        {
            throw new ArgumentException("Remote path is required.");
        }

        return Separator + remotePath.Trim().Trim(Separator);
    }
}
