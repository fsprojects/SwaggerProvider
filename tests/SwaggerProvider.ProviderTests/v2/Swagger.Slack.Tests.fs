module Swagger.Slack.Tests

open SwaggerProvider
open Xunit
open System

[<Literal>]
let Schema = __SOURCE_DIRECTORY__ + "/../Schemas/v2/slack.json"

[<Literal>]
let Host = "https://slack.com"

type Slack = SwaggerClientProvider<Schema>

[<Fact>]
let ``TP Slack Tests``() =
    let slack = Slack.Client()
    slack.HttpClient.BaseAddress <- Uri Host

    task {
        let _ = // do not await intentionally
            slack.ChatPostEphemeral(
                threadTs = None,
                blocks = "",
                attachments = "",
                asUser = None,
                parse = "",
                token = "",
                text = "Hello",
                user = "UNL9PF6P6",
                linkNames = None,
                channel = "CPDMKJR34"
            )

        return ()
    }
