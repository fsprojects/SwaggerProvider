module Program

open Expecto

[<EntryPoint>]
let main args =
    let config =
        { defaultConfig with
            verbosity = Logging.LogLevel.Verbose }
    // TODO: Multiple results?
    let writeResults = TestResults.writeNUnitSummary ("bin/TestResults-1.xml", "Expecto.Tests")
    let config = config.appendSummaryHandler writeResults
    runTestsInAssembly config args
