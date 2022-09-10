module Program

open Expecto

[<EntryPoint>]
let main args =
    let config =
        { defaultConfig with
            verbosity = Logging.LogLevel.Verbose
        }

    let asmName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name

    let fileName = $"bin/TestResults-%s{asmName}-{System.Environment.OSVersion}.xml"

    let writeResults = TestResults.writeNUnitSummary fileName
    let config = config.appendSummaryHandler writeResults
    runTestsInAssembly config args
