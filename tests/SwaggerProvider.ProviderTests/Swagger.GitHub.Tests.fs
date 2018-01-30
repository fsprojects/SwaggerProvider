﻿module Swagger.GitHub.Tests

open SwaggerProvider
open Expecto

let [<Literal>] Schema = __SOURCE_DIRECTORY__ + "/Schemas/GitHub.json"
let [<Literal>] Host = "https://api.github.com"
type GitHub = SwaggerProvider<Schema, AsyncInsteadOfTask = true>
let github = GitHub.Client(Host, Headers = [|"User-Agent","Mozilla/5.0 (iPad; U; CPU OS 3_2_1 like Mac OS X; en-us) AppleWebKit/531.21.10 (KHTML, like Gecko) Mobile/7B405"|])

type SyncGitHub = SwaggerProvider<Schema>
let syncGitHub = SyncGitHub.Client(Host, Headers = [|"User-Agent","Mozilla/5.0 (iPad; U; CPU OS 3_2_1 like Mac OS X; en-us) AppleWebKit/531.21.10 (KHTML, like Gecko) Mobile/7B405"|])

[<Tests>] // Explicit
let githubTest =
    ptestCaseAsync "All/Get fsprojects from GitHub" <| async {
        let! repos = github.OrgReposAsync("fsprojects")
        Expect.isGreaterThan repos.Length 0 "F# community is strong"
    }        

[<Tests>]       
let syncGitHubTest =
    ptestCase "All/Get fsproject from GitHub sync" <| fun _ -> 
        let length = syncGitHub.OrgRepos("fsprojects").Length
        Expect.isGreaterThan length 0 "F# community is strong"
