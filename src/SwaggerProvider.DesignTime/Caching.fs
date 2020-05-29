module SwaggerProvider.Caching

open System
open System.Collections.Concurrent
open System.IO

// https://github.com/fsharp/FSharp.Data/blob/master/src/CommonRuntime/IO.fs

#if LOGGING_ENABLED

let private logLock = obj()
let mutable private indentation = 0

let private appendToLogMultiple logFile lines = lock logLock <| fun () ->
    let path = __SOURCE_DIRECTORY__ + "/../../" + logFile
    use stream = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)
    use writer = new StreamWriter(stream)
    for (line:string) in lines do
        writer.WriteLine(line.Replace("\r", null).Replace("\n","\\n"))
    writer.Flush()

let private appendToLog logFile line = 
    appendToLogMultiple logFile [line]

let internal log str =
#if TIMESTAMPS_IN_LOG
    "[" + DateTime.Now.TimeOfDay.ToString() + "] " + String(' ', indentation * 2) + str
#else
    String(' ', indentation * 2) + str
#endif
    |> appendToLog "log.txt"

let internal logWithStackTrace (str:string) =
    let stackTrace = 
        Environment.StackTrace.Split '\n'
        |> Seq.skip 3
        |> Seq.truncate 5
        |> Seq.map (fun s -> s.TrimEnd())
        |> Seq.toList
    str::stackTrace |> appendToLogMultiple "log.txt"

open System.Diagnostics
open System.Threading
  
let internal logTime category (instance:string) =

    log (sprintf "%s %s" category instance)
    Interlocked.Increment &indentation |> ignore

    let s = Stopwatch()
    s.Start()

    { new IDisposable with
        member __.Dispose() =
            s.Stop()
            Interlocked.Decrement &indentation |> ignore
            log (sprintf "Finished %s [%dms]" category s.ElapsedMilliseconds)
            let instance = instance.Replace("\r", null).Replace("\n","\\n")
            sprintf "%s|%s|%d" category instance s.ElapsedMilliseconds
            |> appendToLog "log.csv" }

#else

let internal dummyDisposable = { new IDisposable with member __.Dispose() = () }
let inline internal log (_:string) = ()
let inline internal logWithStackTrace (_:string) = ()
let inline internal logTime (_:string) (_:string) = dummyDisposable

#endif

// https://github.com/fsharp/FSharp.Data/blob/master/src/CommonRuntime/Caching.fs

type ICache<'TKey, 'TValue> = 
  abstract Set : key:'TKey * value:'TValue -> unit
  abstract TryRetrieve : key:'TKey * ?extendCacheExpiration:bool -> 'TValue option
  abstract Remove : key:'TKey -> unit
  abstract GetOrAdd : key:'TKey * valueFactory:(unit -> 'TValue) -> 'TValue

/// Creates a cache that uses in-memory collection
let createInMemoryCache (expiration:TimeSpan) = 
    let dict = ConcurrentDictionary<'TKey_,'TValue*DateTime>()
    let rec invalidationFunction key = 
        async { 
            do! Async.Sleep (int expiration.TotalMilliseconds) 
            match dict.TryGetValue(key) with
            | true, (_, timestamp) -> 
                if DateTime.UtcNow - timestamp >= expiration then
                    match dict.TryRemove(key) with
                    | true, _ -> log (sprintf "Cache expired: %O" key)
                    | _ -> ()
                else
                    do! invalidationFunction key
            | _ -> ()
        }
    { new ICache<_,_> with
        member __.Set(key, value) =
            dict.[key] <- (value, DateTime.UtcNow)
            invalidationFunction key |> Async.Start
        member x.TryRetrieve(key, ?extendCacheExpiration) =
            match dict.TryGetValue(key) with
            | true, (value, timestamp) when DateTime.UtcNow - timestamp < expiration -> 
                if extendCacheExpiration = Some true then 
                    dict.[key] <- (value, DateTime.UtcNow)
                Some value
            | _ -> None
        member __.Remove(key) = 
            match dict.TryRemove(key) with
            | true, _ -> log (sprintf "Explicitly removed from cache: %O" key)
            | _ -> ()
        member __.GetOrAdd(key, valueFactory) =
            let res, _ = dict.GetOrAdd(key, fun k -> valueFactory(), DateTime.UtcNow)
            invalidationFunction key |> Async.Start
            res
    }
