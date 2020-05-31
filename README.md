# MantisBTMailTask
A windows service to process the outgoing mail queue for the MantisBT system.

I couldn't figure out how to solve the configuration issues I had with the PHP based mail sub-system in MantisBT. To solve it I fell back on my knowledge of C# and .NET to produce this hosted background service to cure the problem.

In your MantisBT config_inc.php file you need to include the following line:

```php
$g_email_send_using_cronjob = ON;
```

This will stop the Web app delivering the mail.

Install, configure and start the MantisBTMailTask service and your mail should start to be delivered.

# Still To Do
* Make the polling frequency configurable.
* Add the code to build a Windows service.
* Investigate single app exe and reducing dll's.
* Can a Linux daemon be conditionally added to the csproj? 