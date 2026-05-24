# User Documentation

Bearcat is a self hosted tool (YOU host it :D) to manage your One Click Hoster (OCH) uploads.
You can create releases, define how they should be packed (currently RAW and 7Zip are supported),
define to which hosters they should be uploaded and if needed, on which link crypters you want to create link containers.
Bearcat will then from time to time check the online status of your uploads and will automatically repackage and reupload them, if needed.
It also supports pulling release related information from xrel.to, that helps you to copy paste all infos that you need to create a forum post from one single place.


# Supported Hosters, Link Crypters and Archivers
Currently the following OCHs are supported:

- Rapidgator
- DDownload
- GoFile
- Keep2Share
- Alfafile
- NitroFlare
- 1fichier

The following link crypters are supported:

- Hide.cx
- Keeplinks

The following archivers are supported:

- RAR
- 7Zip

# Getting Started

## Running Bearcat
Bearcat is self hosted, so you can run it on your own desktop machine, on a NAS, or on a server.

For macOS on Apple Silicon, the preferred local setup is the Bearcat Desktop app. It runs natively on ARM and starts the web application for you. The Docker image is still useful, but it is built as `linux/amd64` because the official RAR command line tools are only available for Linux x64.

For Windows, choose the setup based on where Bearcat should live. On a Windows Server or any always-on server machine, Docker is still the preferred setup as it will restart your container if it fails. For normal desktop use, the Bearcat Desktop app is the preferred setup because it makes the local web app visible through a tray icon and gives you a simple way to start and stop it.

Linux and NAS setups should use Docker.

Bearcat uses a PostgreSQL database to store releases, hosters, link crypters, configuration, and upload state. With Docker Compose, PostgreSQL is started together with Bearcat. With the Desktop app, you bring your own PostgreSQL server and enter the connection settings in the launcher.

[Running Bearcat with the Desktop App](use-the-desktop-launcher.md)

[Installing PostgreSQL for the Desktop App](install-postgresql-for-desktop.md)

[Running Bearcat in Docker](use-the-docker-image.md)


## Setting it up

As soon as Bearcat is running on your machine, you can start setting it up.

[Setup after installation](post-installation.md)

[Advanced configuration](advanced-configuration.md)


## More informations

[Upload lifecycle](upload-lifecycle.md)
