---
title: "Send Upload Notifications to Telegram"
description: "Forward Bearcat notifications to a Telegram chat so you get pinged on your phone without keeping the UI open."
---

Send Bearcat notifications to a Telegram chat, including upload completions and errors.
Each message identifies the release, upload or archive and links to its notification in Bearcat.

![telegram-forwarded-message.png](images/telegram-forwarded-message.png)

The link opens the notification details:

![telegram-notification-details.png](images/telegram-notification-details.png)

## Where to find it

Open **Telegram notifications** from the sidebar, or go to `/telegram`. The page has three parts:
the bot, the connected chat, and which notification types get forwarded.

## Setting up the bot

Bearcat talks to Telegram through a bot that you own. You only have to create it once.

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

After the bot is saved, connect the chat that should receive the notifications.

1. In the **Recipient** section, click **Connect Telegram**. Bearcat generates a one-time link.

   ![telegram-connect-chat.png](images/telegram-connect-chat.png)

2. Click **Open Telegram** and press **Start** in the chat that opens. The link is valid for
   ten minutes.
3. Back in Bearcat, click **Check connection**. Once the pairing went through, the chat shows up
   as **Connected** and the chat receives a short confirmation message.

   ![telegram-chat-connected.png](images/telegram-chat-connected.png)

Click **Send test notification** to check that messages reach the connected chat.

If you reload the page during setup, use **Check connection** or generate a new link to continue.

## Choosing which notifications get forwarded

Under **Forwarded notification types** you decide whether **Info**, **Warning** and **Error**
notifications are forwarded. Uncheck a type to stop forwarding it and click **Save**.

Only notifications created after you connect the chat are forwarded. Older notifications are not sent.

## Delivery status

The **Recipient** section shows a small status box so you can tell whether forwarding actually
works:

- how many notifications are still waiting to be delivered,
- how many were given up on after repeated failures,
- when the last notification was delivered,
- the last error, if a delivery failed.

Bearcat retries a failed delivery with an increasing delay. If it keeps failing (for example
because the bot was blocked or the chat was deleted), Bearcat stops retrying that notification
after several attempts. When all notifications have been delivered, the status box shows that
there are no pending deliveries.

## Disconnecting

**Disconnect** removes the connected chat and discards any notifications that are still waiting
to be delivered. Bearcat asks for confirmation first, because this cannot be undone. The bot
itself stays configured, so you can pair a new chat right away.
