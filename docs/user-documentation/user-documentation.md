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

The following link crypters are supported:

- Hide.cx
- Keeplinks

The following archivers are supported:

- RAR
- 7Zip

# Getting Started

## Running Bearcat
Bearcat is self hosted, as such you can run it on your own machine, on a NAS or any Server, that has Docker support.
If want to run it outside of Docker, you can also clone the repository and run the .NET application directly.
But the recommended way is to use Docker, as it is the easiest way to get started and also ensures that you have a consistent environment across different machines.

Bearcat uses a PostgreSQL database to store all the information about releases, hosters, link crypters and so on.
If you use the provided docker compose file to run Bearcat, a PostgreSQL container will be started together with Bearcat and the application will automatically connect to it.

[Running Bearcat in Docker](use-the-docker-image.md)


## Setting it up

As soon as Bearcat is running on your machine, you can start setting it up.

[Setup after installation](post-installation.md)

[Advanced configuration](advanced-configuration.md)
