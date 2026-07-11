namespace Bearcat.Domain.UseCases.ManageNotifications.Telegram;

public interface ITelegramClient
{
    Task<TelegramBotIdentity> GetBotAsync(string botToken, CancellationToken cancellationToken);

    Task SendMessageAsync(
        string botToken,
        long chatId,
        string text,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(
        string botToken,
        int offset,
        CancellationToken cancellationToken
    );
}

public sealed record TelegramBotIdentity(string Username);

public sealed record TelegramUpdate(int UpdateId, TelegramChat? Chat, string? Text);

public sealed record TelegramChat(long Id, string? FirstName, string? LastName, string? Username);
