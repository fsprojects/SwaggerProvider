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

let isRateLimitError(ex: exn) =
    ex.Message.Contains("rate limit")
    || ex.Message.Contains("403 (rate limit")

[<Fact>] // Explicit
let ``Get fsprojects from GitHub``() =
    task {
        try
            let! repos = github().OrgRepos("fsprojects")
            repos.Length |> shouldBeGreaterThan 0
        with
        | :? HttpRequestException as ex when isRateLimitError ex -> Assert.Skip("GitHub API rate limit exceeded - transient CI failure")
        | :? AggregateException as aex when isRateLimitError aex -> Assert.Skip("GitHub API rate limit exceeded - transient CI failure")
    }

[<Fact>]
let ``Get fsproject from GitHub with Task``() =
    task {
        try
            let! repos = taskGitHub().OrgRepos("fsprojects")
            repos.Length |> shouldBeGreaterThan 0
        with
        | :? HttpRequestException as ex when isRateLimitError ex -> Assert.Skip("GitHub API rate limit exceeded - transient CI failure")
        | :? AggregateException as aex when isRateLimitError aex -> Assert.Skip("GitHub API rate limit exceeded - transient CI failure")
    }
