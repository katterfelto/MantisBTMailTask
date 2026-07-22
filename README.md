# MantisBTMailTask

A cross-platform background service (Windows Service / systemd daemon) that processes the outgoing mail queue for [MantisBT](https://www.mantisbt.org/).

## Background

MantisBT's PHP-based mail subsystem can be difficult to configure correctly in some environments. This project replaces it with a standalone .NET hosted service that reads pending notifications directly from the MantisBT database and sends them via email, making the process easier to configure, run, and debug.

The original implementation sent mail over SMTP; that version is preserved on the `SMTP_Client` branch. The current version uses `Microsoft.Graph` to send mail through Exchange Online.

## Prerequisites

- Access to the MantisBT database
- An Azure AD app registration with permissions to send mail via Microsoft Graph (`Mail.Send`), used to obtain the `TenantId`, `ClientId`, and `ClientSecret` below

## Installation

1. Disable mail delivery from the MantisBT web application by adding the following line to your `config_inc.php`, which defers mail sending to this service instead:
   ```php
   $g_email_send_using_cronjob = ON;
   ```
2. Download the latest release artifact from the [Releases](https://github.com/katterfelto/MantisBTMailTask/releases/latest) page:
   - `MantisBTMailTask-<version>-win-x64.zip` for Windows — a win-x64 build that includes `MantisBTMailTask.exe`
   - `MantisBTMailTask-<version>.zip` / `MantisBTMailTask-<version>.tgz` for Linux — a framework-dependent build (requires the matching .NET runtime to be installed) that also includes the sample systemd unit file

   These are built and published automatically by the project's GitHub Actions release workflow.
3. Extract the archive contents to the location you want to run the service from. The archive includes `appsettings.json` and, for Linux, the sample [`mantisbtmailtask.service`](mantisbtmailtask.service) unit file.
4. Update `appsettings.json` with your environment's configuration (see [Configuration](#configuration) below). Only the minimum required settings are included in the default file.
5. Install the service for your operating system:

   **Windows**
   ```cmd
   sc create MantisBTMailTask BinPath=<INSTALL FOLDER>\MantisBTMailTask.exe
   sc start MantisBTMailTask
   ```

   **Linux**

   A sample unit file is provided at [`mantisbtmailtask.service`](mantisbtmailtask.service). Copy the published files to `/opt/mantisbtmailtask` (or update the paths in the unit file to match your install folder), then run:
   ```bash
   sudo useradd --system --no-create-home --shell /usr/sbin/nologin mantisbtmailtask
   sudo cp mantisbtmailtask.service /etc/systemd/system/
   sudo systemctl daemon-reload
   sudo systemctl enable --now mantisbtmailtask
   sudo systemctl status mantisbtmailtask
   ```

## Configuration

Settings are read from `appsettings.json`.

| Section | Key | Description | Default |
|---|---|---|---|
| `MantisDB` | `Host` | MantisBT database hostname | `localhost` |
| `MantisDB` | `Port` | MantisBT database port | `3306` |
| `MantisDB` | `Database` | MantisBT database name | *(required)* |
| `MantisDB` | `Username` | MantisBT database username | *(required)* |
| `MantisDB` | `Password` | MantisBT database password | *(required)* |
| `MailServer` | `TenantId` | Azure AD tenant ID | *(required)* |
| `MailServer` | `ClientId` | Azure AD app registration client ID | *(required)* |
| `MailServer` | `ClientSecret` | Azure AD app registration client secret | *(required)* |
| `MailServer` | `From` | Mailbox address mail is sent from | *(required)* |
| `MailServer` | `ForceRemoveOnFailure` | Remove emails from the queue even if they fail to send | `false` |

## Building

If you want to build from source instead of using a released artifact:

1. Install the [.NET SDK](https://dotnet.microsoft.com/) version matching the one used by the [release workflow](.github/workflows/release.yml) (currently `10.0.x`).
2. Restore and build the solution:
   ```bash
   dotnet restore
   dotnet build --configuration Release
   ```
3. Publish the service. On Windows this produces a Windows Service host; on Linux it produces a systemd-compatible daemon:
   ```bash
   dotnet publish MantisBTMailTask/MantisBTMailTask.csproj --configuration Release --output publish
   ```
4. Copy the contents of the `publish` folder, along with [`mantisbtmailtask.service`](mantisbtmailtask.service) on Linux, to the location you want to run the service from, then continue from step 4 of [Installation](#installation).

## License

This project is licensed under the [MIT License](LICENSE).


