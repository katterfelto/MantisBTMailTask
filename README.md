# MantisBTMailTask
A windows service to process the outgoing mail queue for the MantisBT system.

I couldn't figure out how to solve the configuration issues I had with the PHP based mail sub-system in MantisBT. To solve it I fell back on my knowledge of C# and .NET to produce this hosted background service to cure the problem.

# How To Use
1. In your MantisBT config_inc.php file you need to include the following line, this will stop the Web app delivering the mail:
   ```php
   $g_email_send_using_cronjob = ON;
   ```
1. Build MantisBTMailTask, on Windows it will build a Windows service, on linux it will build a systemd daemon:
   ```dotnet
   dotnet publish -c release
   ```
1. Copy publish folder contents into the folder you want to install the service from (also copy the AppSettings.json file if it is not present).
1. Update the AppSettings.json file for you system requirements. NOTE only the minimum required configuration is included in the AppSettings.json in this repository.
1. Install the service depnding on your operating system:
   1. On Windows:
      ```cmd
      sc create MantisBTMailTask BinPath=<INSTALL FOLDER>\MantisBTMailTask.exe
      sc start MantisBTMailTask
      ```
   1. On Linux:
      ```
      ??? Not sure how to do this ???
      ```


