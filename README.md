# SqlClient.SniShadowCopy
Allows loading the Microsoft.SqlClient.Data native assemblies from the IIS shadow copy location

## Description
The `Microsoft.Data.SqlClient` component uses a native assembly to talk to SQL Server instances.
Unfortunately, it uses the `[DllImport]` attribute to P/Invoke into native code - this means that
the IIS process locks the file and there is no way to release the lock other than forcibly
shut down the worker process (i.e. restarting the Application Pools).
This behavior breaks the IIS xcopy deployment model.

This library takes advantage of Windows native DLL loading rules to load the SNI component from
a shadow copy location instead of the main application `/bin` folder.
The idea is to copy the native assembly in the same folder that is used for the shadow copy
of `Microsoft.Data.SqlClient.dll` early in the application startup sequence.

According to the Windows DLL loading rules, the first place where Windows looks for the library
module is the same folder as the loading assembly. This means that when the `[DllImport]` attribute
is activated, the native assembly will be loaded from the shadow copy location and there
will be no lock on the assembly in the application `/bin` folder.

## How to use it
In your project, add a reference to this library. Then, before invoking any `Microsoft.Data.SqlClient`
class or method, run the helper method:

```
SqlClient.SniShadowCopy.SniLoader.ForceShadowNativeAssembly();
```

To make sure this line is executed before any other data access, I suggest adding it in the 
`PreApplicationStartMethod` (more information [here](http://haacked.com/archive/2010/05/16/three-hidden-extensibility-gems-in-asp-net-4.aspx/)). 
I haven't tested putting it in the `global.asax` `Application_Start` method, 
but it should work if that there is no other data access before the call.