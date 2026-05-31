---
title: "Keep2Share"
---

## "Unicorn" captcha challenges

Keep2Share might require you to resolve captchas from time to time, if you are accessing it from a "not trustworthy" IP address.
In this case, all API calls fail until you resolved that captcha.

Bearcat will handle these errors by sending you a notification telling you to resolve a captcha challenge and automatically disable the hoster configuration to avoid getting the IP address banned.
![keep2share-captcha-challenge.png](images/keep2share-captcha-challenge.png)

If you then visit the Hoster Registrations page, you will see a new button:
![keep2share-captcha-button.png](images/keep2share-captcha-button.png)

Clicking on it will open a new dialog, with a "Get challenge" button.
This button will retrieve the URL for the captcha challenge from the keep2share API and asks you to open it and do the challenge.

![keep2share-captcha-empty-dialog](images/keep2share-captcha-empty-dialog.png)
![keep2share-captcha-link.png](images/keep2share-captcha-link.png)

Open the link, solve the challenge and copy the long token in the "Response" box:
![keep2share-captcha-response.png](images/keep2share-captcha-response.png)

Then paste the token into the "Captcha code" box and click "Unlock".
![keep2share-resolve-captche-challenge.png](images/keep2share-resolve-captche-challenge.png)

If everything went well, you will see a successful login notification and the Hoster Registration will be automatically reactivated.
![keep2share-captcha-challenge-successful.png](images/keep2share-captcha-challenge-successful.png)