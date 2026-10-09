---
title: "Resolve Keep2Share Captchas in Bearcat"
description: "Solve a Keep2Share captcha and reactivate the hoster registration."
---

## "Unicorn" captcha challenges

Keep2Share sometimes requires a captcha when it does not trust your IP address.
Until you solve it, API calls fail. Bearcat disables the hoster registration and notifies you.
This prevents further requests that could trigger an IP ban.

![Notification for a Keep2Share captcha challenge](images/keep2share-captcha-challenge.png)

1. Open **Hoster registrations** and click the captcha button for your Keep2Share account.

   ![Captcha button on the hoster registration](images/keep2share-captcha-button.png)

2. Click **Get challenge** in the dialog, then open the link shown.

   ![Get a captcha challenge](images/keep2share-captcha-empty-dialog.png)
   ![Link to the captcha challenge](images/keep2share-captcha-link.png)

3. Solve the captcha and copy the token from the **Response** box.

   ![Response token after solving the captcha](images/keep2share-captcha-response.png)

4. Paste the token into **Captcha code** in Bearcat and click **Unlock**.

   ![Enter the captcha response in Bearcat](images/keep2share-resolve-captche-challenge.png)

After a successful login, Bearcat reactivates the hoster registration automatically.

![Successful Keep2Share login](images/keep2share-captcha-challenge-successful.png)
