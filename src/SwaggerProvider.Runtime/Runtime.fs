namespace SwaggerProvider

// TypeProviderAssemblyAttribute in the runtime DLL, pointing to the design-time DLL
[<assembly:CompilerServices.TypeProviderAssembly("SwaggerProvider.DesignTime.dll")>]
do ()
