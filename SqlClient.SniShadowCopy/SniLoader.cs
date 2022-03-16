using System;
using System.IO;
using System.Linq;

namespace SqlClient.SniShadowCopy
{
    public static class SniLoader
    {
        /// <summary>
        /// The Microsoft.Data.SqlClient component uses a native assembly to talk to SQL Server instances.
        /// Unfortunately, it uses the [DllImport] attribute to P/Invoke into native code - this means that
        /// the IIS process locks the file and there is no way to release the lock other than forcibly
        /// shut down the worker process (i.e. restarting the Application Pools).
        /// This behavior breaks the IIS xcopy deployment model.
        ///
        /// This library takes advantage of Windows native DLL loading rules to load the SNI component from
        /// a shadow copy location instead of the main application /bin folder.
        /// The idea is to copy the native assembly in the same folder that is used for the shadow copy
        /// of Microsoft.Data.SqlClient early in the application startup sequence (ideally, in the
        /// PreApplicationStartMethod handler).
        ///
        /// According to the Windows DLL loading rules, the first place where Windows looks for the library
        /// module is the same folder as the loading assembly. This means that when the [DllImport] attribute
        /// is activated, the native assembly will be loaded from the shadow copy location and there
        /// will be no lock on the assembly in the application /bin folder.
        /// </summary>
        public static void ForceShadowNativeAssembly()
        {
            // Find out the process bitness and choose the appropriate native assembly
            var moduleName = Environment.Is64BitProcess
                ? "Microsoft.Data.SqlClient.SNI.x64.dll"
                : "Microsoft.Data.SqlClient.SNI.x86.dll";

            System.Diagnostics.Debug.WriteLine(
                String.Format("SniLoader::ForceShadowNativeAssembly - Process is '{0}'",
                    Environment.Is64BitProcess ? "x64" : "x86"));

            // Retrieve the main assembly location for the current AppDomain
            AppDomain appDomain = AppDomain.CurrentDomain;

            System.Diagnostics.Debug.WriteLine(
                String.Format("SniLoader::ForceShadowNativeAssembly - AppDomain private path is '{0}'",
                    appDomain.RelativeSearchPath));

            // Look for the main Microsoft.Data.SqlClient assembly in the shadow copy path
            string sqlClientAssemblyName = "Microsoft.Data.SqlClient.dll";
            var sqlClientShadowAssembly = Directory.GetFiles(
                appDomain.DynamicDirectory, sqlClientAssemblyName,
                SearchOption.AllDirectories)
				.FirstOrDefault();

            if (String.IsNullOrEmpty(sqlClientShadowAssembly))
            {
                // Assembly not found, let's bail out
                System.Diagnostics.Debug.WriteLine(String.Format(
                    "SniLoader::ForceShadowNativeAssembly - Shadow assembly for {0} not found", sqlClientAssemblyName));

                return;
            }

            // Assembly found
            System.Diagnostics.Debug.WriteLine(String.Format(
                "SniLoader::ForceShadowNativeAssembly - Shadow assembly for {0} is '{1}'",
                sqlClientAssemblyName, sqlClientShadowAssembly));

            // Extract the directory information from the shadow assembly path
            var sqlClientShadowPath = Path.GetDirectoryName(sqlClientShadowAssembly);

            if (String.IsNullOrEmpty(sqlClientShadowPath))
            {
                // This shouldn't happen, if we're here something's wrong
                System.Diagnostics.Debug.WriteLine(String.Format(
                    "SniLoader::ForceShadowNativeAssembly - Shadow path for {0} is null or empty", sqlClientAssemblyName));

                return;
            }

            System.Diagnostics.Debug.WriteLine(String.Format(
                "SniLoader::ForceShadowNativeAssembly - Shadow path for {0} is '{1}'",
                    sqlClientAssemblyName, sqlClientShadowPath));

            // Compute the source and target paths for the native assembly
            var sourceFile = Path.Combine(appDomain.RelativeSearchPath, moduleName);
            var targetFile = Path.Combine(sqlClientShadowPath, moduleName);

            // Make sure the source file exists
            if (!File.Exists(sourceFile))
            {
                System.Diagnostics.Debug.WriteLine(String.Format(
                    "SniLoader::ForceShadowNativeAssembly - Source file '{0}' not found",
                    sourceFile));

                return;
            }

            // Make sure the target file does not exist
            if (File.Exists(targetFile))
            {
                System.Diagnostics.Debug.WriteLine(String.Format(
                    "SniLoader::ForceShadowNativeAssembly - Target file '{0}' already exists, nothing to do",
					targetFile));

                return;
            }

            // Copy the native assembly under the shadow path
            System.Diagnostics.Debug.WriteLine(String.Format(
				"SniLoader::ForceShadowNativeAssembly - Copying '{0}' to '{1}'",
                sourceFile, targetFile));

            File.Copy(sourceFile, targetFile);
        }
    }
}
