module Swagger.GitHub.Tests

open SwaggerProvider
open Xunit
open FsUnitTyped
open System
open System.Net.Http

[<Literal>]
let Schema = __SOURCE_DIRECTORY__ + "/../Schemas/v2/github.json"

[<Literal>]
let UserAgent =
    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"


[<Literal>]
let Host = "https://api.github.com"

type GitHub = SwaggerClientProvider<Schema, PreferAsync=true>

let github() =
    let client = GitHub.Client()
    client.HttpClient.BaseAddress <- Uri Host

    client.HttpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent)
    |> ignore

    client



type TaskGitHub = SwaggerClientProvider<Schema>

let taskGitHub() =
    let client = TaskGitHub.Client()
    client.HttpClient.BaseAddress <- Uri Host

    client.HttpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent)
    |> ignore

    client

[<Fact>] // Explicit
let ``Get fsprojects from GitHub``() =
    task {
        let! repos = github().OrgRepos("fsprojects")
        repos.Length |> shouldBeGreaterThan 0
    }

[<Fact>]
let ``Get fsproject from GitHub with Task``() =
    task {
        let! repos = taskGitHub().OrgRepos("fsprojects")
        repos.Length |> shouldBeGreaterThan 0
    }
