---
title: "Resolve Keep2Share Captchas in Bearcat"
description: "Resolve Keep2Share captcha challenges in Bearcat and reactivate your hoster account."
---

## "Unicorn" captcha challenges

Keep2Share sometimes requires a captcha when it does not trust your IP address.
Until you solve it, API calls fail. Bearcat notifies you and disables the hoster registration
to avoid further requests that could get the IP address banned.

![Notification for a Keep2Share captcha challenge](images/keep2share-captcha-challenge.png)

1. Open **Hoster registrations** and click the captcha button for your Keep2Share account.

   ![Captcha button on the hoster registration](images/keep2share-captcha-button.png)

2. Click **Get challenge** in the dialog, then open the returned link.

   ![Get a captcha challenge](images/keep2share-captcha-empty-dialog.png)
   ![Link to the captcha challenge](images/keep2share-captcha-link.png)

3. Solve the challenge and copy the token from the **Response** box.

   ![Response token after solving the captcha](images/keep2share-captcha-response.png)

4. Paste the token into **Captcha code** in Bearcat and click **Unlock**.

   ![Enter the captcha response in Bearcat](images/keep2share-resolve-captche-challenge.png)

After a successful login, Bearcat reactivates the hoster registration automatically.

![Successful Keep2Share login](images/keep2share-captcha-challenge-successful.png)
