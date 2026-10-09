---
title: "Keep2Share: Captchas lösen"
description: "Ein Keep2Share-Captcha lösen und die Hosterregistrierung wieder aktivieren."
---

## Captcha Challenge

Keep2Share verlangt manchmal ein Captcha, wenn es deiner IP-Adresse nicht vertraut.
Bis du es löst, schlagen API-Aufrufe fehl. Bearcat deaktiviert die Hosterregistrierung und benachrichtigt
dich. So verhindert es weitere Anfragen, die eine IP-Sperre auslösen könnten.

![Benachrichtigung über eine Captcha-Challenge von Keep2Share](../images/keep2share-captcha-challenge.png)

1. Öffne **Hosterregistrierungen** und klicke auf den Captcha-Button deines Keep2Share-Kontos.

   ![Captcha-Button an der Hosterregistrierung](../images/keep2share-captcha-button.png)

2. Klicke im Dialog auf **Challenge holen** und öffne den angezeigten Link.

   ![Captcha-Challenge holen](../images/keep2share-captcha-empty-dialog.png)
   ![Link zur Captcha-Challenge](../images/keep2share-captcha-link.png)

3. Löse das Captcha und kopiere den Token aus dem Feld **Response**.

   ![Token im Feld Response nach dem Lösen des Captchas](../images/keep2share-captcha-response.png)

4. Füge den Token in Bearcat unter **Captchacode** ein und klicke auf **Freischalten**.

   ![Captcha-Antwort in Bearcat eingeben](../images/keep2share-resolve-captche-challenge.png)

Nach einem erfolgreichen Login aktiviert Bearcat die Hosterregistrierung automatisch wieder.

![Erfolgreicher Login bei Keep2Share](../images/keep2share-captcha-challenge-successful.png)
