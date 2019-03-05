module Program

open Expecto

[<EntryPoint>]
let main args =
    use __ = APIsGuru.httpClient
    let config =
        { defaultConfig with
            verbosity = Logging.LogLevel.Verbose }
    runTestsInAssembly config args
