---
title: "Send Upload Notifications to Telegram"
description: "Send upload completions, errors, and other Bearcat notifications to a Telegram chat."
---

Bearcat can send upload completions, errors, and other notifications to your Telegram chat.
Each message identifies the release, upload or archive and links to its notification in Bearcat.

![telegram-forwarded-message.png](images/telegram-forwarded-message.png)

The link opens the notification details:

![telegram-notification-details.png](images/telegram-notification-details.png)

## Where to find it

Open **Telegram notifications** in the sidebar or go to `/telegram`.

## Setting up the bot

First, create your own Telegram bot:

1. In Telegram, open a chat with [@BotFather](https://t.me/BotFather) and send `/newbot`.
   Follow the prompts to pick a name and a username. BotFather gives you a **bot token**.

   ![telegram-botfather-token.png](images/telegram-botfather-token.png)

2. Paste that token into the **Bot token** field on the Telegram notifications page.
3. Enter a **Bearcat URL** that your phone can reach. If Bearcat is on a private network,
   your phone may need a VPN connection to open notification links.
4. Click **Save**.

![telegram-bot-setup.png](images/telegram-bot-setup.png)

Bearcat stores the token encrypted. Leave the field empty when editing other settings to keep
the current bot. Entering a new token replaces the bot and disconnects the chat; you must connect it again.

## Connecting a chat

1. In the **Recipient** section, click **Connect Telegram**. Bearcat generates a one-time link.

   ![telegram-connect-chat.png](images/telegram-connect-chat.png)

2. Click **Open Telegram** and press **Start** in the chat that opens. The link is valid for
   ten minutes.
3. Back in Bearcat, click **Check connection**. Once connected, the chat shows
   **Connected** and receives a short confirmation message.

   ![telegram-chat-connected.png](images/telegram-chat-connected.png)

Click **Send test notification** to check that messages reach the connected chat.

If you reload the page during setup, use **Check connection** or generate a new link to continue.

## Choosing which notifications get forwarded

Under **Forwarded notification types**, select **Info**, **Warning** and/or **Error**, then click **Save**.
Uncheck a type to stop forwarding it.

Only notifications created after you connect the chat are forwarded.

## Delivery status

The **Recipient** section shows the delivery status:

- how many notifications are still waiting to be delivered,
- how many could not be delivered after several attempts,
- when the last notification was delivered,
- the last error, if a delivery failed.

Bearcat retries failed deliveries, waiting longer between attempts. After several failures, it gives
up on that notification. This can happen if the bot is blocked or the chat is deleted.

## Disconnecting

**Disconnect** removes the connected chat and discards any notifications that are still waiting
to be delivered. Bearcat asks for confirmation first, because this cannot be undone. The bot
itself stays configured, so you can connect a new chat.
