using Bearcat.Domain.UseCases.ManageNotifications.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Bearcat.Infrastructure.Telegram;

public sealed class TelegramClient(IHttpClientFactory httpClientFactory) : ITelegramClient
{
    public async Task<TelegramBotIdentity> GetBotAsync(
        string botToken,
        CancellationToken cancellationToken
    )
    {
        var bot = await CreateClient(botToken).GetMe(cancellationToken);
        return new TelegramBotIdentity(bot.Username!);
    }

    public async Task SendMessageAsync(
        string botToken,
        long chatId,
        string text,
        CancellationToken cancellationToken
    )
    {
        await CreateClient(botToken)
            .SendMessage(chatId: chatId, text: text, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(
        string botToken,
        int offset,
        CancellationToken cancellationToken
    )
    {
        var updates = await CreateClient(botToken)
            .GetUpdates(
                offset: offset,
                timeout: 30,
                allowedUpdates: [UpdateType.Message],
                cancellationToken: cancellationToken
            );

        return updates
            .Select(update => new TelegramUpdate(
                update.Id,
                update.Message is null
                    ? null
                    : new TelegramChat(
                        update.Message.Chat.Id,
                        update.Message.Chat.FirstName,
                        update.Message.Chat.LastName,
                        update.Message.Chat.Username
                    ),
                update.Message?.Text
            ))
            .ToList();
    }

    private TelegramBotClient CreateClient(string botToken)
    {
        return new TelegramBotClient(botToken, httpClientFactory.CreateClient("telegram"));
    }
}
