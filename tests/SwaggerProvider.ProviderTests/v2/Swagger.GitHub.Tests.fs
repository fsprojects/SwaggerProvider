module Swagger.GitHub.Tests

open SwaggerProvider
open Xunit
open FsUnitTyped
open System
open System.Net.Http

[<Literal>]
let Schema = __SOURCE_DIRECTORY__ + "/../Schemas/v2/github.json"

[<Literal>]
let Host = "https://api.github.com"

type GitHub = SwaggerClientProvider<Schema, PreferAsync=true>

let github() =
    let client = GitHub.Client()
    client.HttpClient.BaseAddress <- Uri Host

    "Mozilla/5.0 (iPad; U; CPU OS 3_2_1 like Mac OS X; en-us) AppleWebKit/531.21.10 (KHTML, like Gecko) Mobile/7B405"
    |> Headers.ProductInfoHeaderValue.Parse
    |> client.HttpClient.DefaultRequestHeaders.UserAgent.Add

    client



type TaskGitHub = SwaggerClientProvider<Schema>

let taskGitHub() =
    let client = TaskGitHub.Client()
    client.HttpClient.BaseAddress <- Uri Host

    "Mozilla/5.0 (iPad; U; CPU OS 3_2_1 like Mac OS X; en-us) AppleWebKit/531.21.10 (KHTML, like Gecko) Mobile/7B405"
    |> Headers.ProductInfoHeaderValue.Parse
    |> client.HttpClient.DefaultRequestHeaders.UserAgent.Add

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
