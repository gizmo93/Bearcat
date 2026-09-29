using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.DetectUnreadableSecrets;
using Bearcat.Domain.UseCases.ManageProxyServers.ConnectionTest;
using Bearcat.Domain.UseCases.ManageProxyServers.Dto;
using Bearcat.Domain.UseCases.ManageProxyServers.Repositories;
using Bearcat.Domain.UseCases.ManageProxyServers.Validation;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageProxyServers;

public class ProxyServerService(
    IProxyServerWriteRepository writeRepository,
    ISecretProtector secretProtector,
    ITcpConnectionOpener tcpConnectionOpener,
    UnreadableSecretsNotificationService unreadableSecretsNotificationService,
    ILogger<ProxyServerService> logger
)
{
    public const int MinPort = 1;
    public const int MaxPort = 65535;

    private const int NameMaxLength = 100;
    private const int HostMaxLength = 255;
    private const int UsernameMaxLength = 255;

    private static readonly TimeSpan ConnectionTestTimeout = TimeSpan.FromSeconds(5);

    public async Task<ProxyServerSaveResult> CreateAsync(
        ProxyServerInput input,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedInput = Normalize(input);
        var validationErrors = await ValidateAsync(
            normalizedInput,
            excludedProxyServerId: null,
            hasStoredPassword: false,
            cancellationToken
        );

        if (validationErrors.Count > 0)
        {
            return ProxyServerSaveResult.Invalid(validationErrors);
        }

        var proxyServer = new ProxyServer
        {
            Name = normalizedInput.Name,
            ProxyType = normalizedInput.ProxyType,
            Host = normalizedInput.Host,
            Port = normalizedInput.Port,
            Username = normalizedInput.Username,
            EncryptedPassword = normalizedInput.Password is null
                ? null
                : secretProtector.Protect(normalizedInput.Password),
        };

        writeRepository.Add(proxyServer);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return ProxyServerSaveResult.Saved(proxyServer.Id);
    }

    public async Task<ProxyServerSaveResult> UpdateAsync(
        int proxyServerId,
        ProxyServerInput input,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedInput = Normalize(input);
        var proxyServer = await writeRepository.GetByIdAsync(proxyServerId, cancellationToken);
        var validationErrors = await ValidateAsync(
            normalizedInput,
            proxyServerId,
            hasStoredPassword: proxyServer.EncryptedPassword is not null,
            cancellationToken
        );

        if (validationErrors.Count > 0)
        {
            return ProxyServerSaveResult.Invalid(validationErrors);
        }

        proxyServer.Name = normalizedInput.Name;
        proxyServer.ProxyType = normalizedInput.ProxyType;
        proxyServer.Host = normalizedInput.Host;
        proxyServer.Port = normalizedInput.Port;
        proxyServer.Username = normalizedInput.Username;

        if (normalizedInput.Password is not null)
        {
            proxyServer.EncryptedPassword = secretProtector.Protect(normalizedInput.Password);
            proxyServer.HasUnreadableSecrets = false;
        }
        else if (normalizedInput.RemoveStoredPassword)
        {
            proxyServer.EncryptedPassword = null;
            proxyServer.HasUnreadableSecrets = false;
        }

        await writeRepository.SaveChangesAsync(cancellationToken);
        await unreadableSecretsNotificationService.ResolveNotificationWhenNoUnreadableSecretsRemainAsync(
            cancellationToken
        );

        return ProxyServerSaveResult.Saved(proxyServerId);
    }

    public async Task DeleteAsync(int proxyServerId, CancellationToken cancellationToken = default)
    {
        var proxyServer = await writeRepository.GetByIdAsync(proxyServerId, cancellationToken);
        writeRepository.Remove(proxyServer);
        await writeRepository.SaveChangesAsync(cancellationToken);
        await unreadableSecretsNotificationService.ResolveNotificationWhenNoUnreadableSecretsRemainAsync(
            cancellationToken
        );
    }

    public async Task<ProxyServerConnectionTestResult> TestConnectionAsync(
        int proxyServerId,
        CancellationToken cancellationToken = default
    )
    {
        var proxyServer = await writeRepository.GetByIdAsync(proxyServerId, cancellationToken);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );
        timeoutSource.CancelAfter(ConnectionTestTimeout);

        try
        {
            await tcpConnectionOpener.OpenConnectionAsync(
                proxyServer.Host,
                proxyServer.Port,
                timeoutSource.Token
            );

            return new ProxyServerConnectionTestResult(IsSuccess: true, ErrorMessage: null);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Connection test for proxy server {ProxyServerName} timed out",
                proxyServer.Name
            );

            return new ProxyServerConnectionTestResult(
                IsSuccess: false,
                ErrorMessage: $"The connection attempt timed out after {ConnectionTestTimeout.TotalSeconds:0} seconds."
            );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Connection test for proxy server {ProxyServerName} failed",
                proxyServer.Name
            );

            return new ProxyServerConnectionTestResult(
                IsSuccess: false,
                ErrorMessage: exception.Message
            );
        }
    }

    private static ProxyServerInput Normalize(ProxyServerInput input)
    {
        var username = input.Username?.Trim();

        return new ProxyServerInput(
            input.Name.Trim(),
            input.ProxyType,
            input.Host.Trim(),
            input.Port,
            string.IsNullOrEmpty(username) ? null : username,
            string.IsNullOrEmpty(input.Password) ? null : input.Password,
            input.RemoveStoredPassword
        );
    }

    private async Task<List<ProxyServerValidationError>> ValidateAsync(
        ProxyServerInput normalizedInput,
        int? excludedProxyServerId,
        bool hasStoredPassword,
        CancellationToken cancellationToken
    )
    {
        var validationErrors = new List<ProxyServerValidationError>();

        var nameError = await ValidateNameAsync(
            normalizedInput.Name,
            excludedProxyServerId,
            cancellationToken
        );
        if (nameError is not null)
        {
            validationErrors.Add(nameError.Value);
        }

        var hostError = ValidateHost(normalizedInput.Host);
        if (hostError is not null)
        {
            validationErrors.Add(hostError.Value);
        }

        if (normalizedInput.Port is < MinPort or > MaxPort)
        {
            validationErrors.Add(ProxyServerValidationError.PortOutOfRange);
        }

        if (normalizedInput.Username?.Length > UsernameMaxLength)
        {
            validationErrors.Add(ProxyServerValidationError.UsernameTooLong);
        }

        var hasPasswordAfterSave =
            normalizedInput.Password is not null
            || (hasStoredPassword && !normalizedInput.RemoveStoredPassword);

        if (hasPasswordAfterSave && normalizedInput.Username is null)
        {
            validationErrors.Add(ProxyServerValidationError.UsernameRequiredForPassword);
        }

        return validationErrors;
    }

    private async Task<ProxyServerValidationError?> ValidateNameAsync(
        string name,
        int? excludedProxyServerId,
        CancellationToken cancellationToken
    )
    {
        if (name.Length == 0)
        {
            return ProxyServerValidationError.NameRequired;
        }

        if (name.Length > NameMaxLength)
        {
            return ProxyServerValidationError.NameTooLong;
        }

        if (await writeRepository.NameExistsAsync(name, excludedProxyServerId, cancellationToken))
        {
            return ProxyServerValidationError.NameAlreadyExists;
        }

        return null;
    }

    private static ProxyServerValidationError? ValidateHost(string host)
    {
        if (host.Length == 0)
        {
            return ProxyServerValidationError.HostRequired;
        }

        if (host.Length > HostMaxLength)
        {
            return ProxyServerValidationError.HostTooLong;
        }

        if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
        {
            return ProxyServerValidationError.HostInvalid;
        }

        return null;
    }
}
