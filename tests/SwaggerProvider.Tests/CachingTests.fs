namespace SwaggerProvider.Tests.CachingTests

open System
open System.Threading
open Xunit
open FsUnitTyped
open SwaggerProvider.Caching

/// Unit tests for the in-memory ICache implementation used by Provider.OpenApiClient.fs
/// to cache generated provided types keyed by schema/parameter combination.
module InMemoryCacheTests =

    [<Fact>]
    let ``TryRetrieve returns None for a key that was never set``() =
        let cache = createInMemoryCache(TimeSpan.FromMinutes 5.0)
        cache.TryRetrieve("missing") |> shouldEqual None

    [<Fact>]
    let ``Set followed by TryRetrieve returns the stored value``() =
        let cache = createInMemoryCache(TimeSpan.FromMinutes 5.0)
        cache.Set("key1", 42)
        cache.TryRetrieve("key1") |> shouldEqual(Some 42)

    [<Fact>]
    let ``Set overwrites a previously stored value for the same key``() =
        let cache = createInMemoryCache(TimeSpan.FromMinutes 5.0)
        cache.Set("key1", "first")
        cache.Set("key1", "second")
        cache.TryRetrieve("key1") |> shouldEqual(Some "second")

    [<Fact>]
    let ``Remove deletes a stored value so TryRetrieve returns None``() =
        let cache = createInMemoryCache(TimeSpan.FromMinutes 5.0)
        cache.Set("key1", 1)
        cache.Remove("key1")
        cache.TryRetrieve("key1") |> shouldEqual None

    [<Fact>]
    let ``Remove on a missing key does not throw``() =
        let cache = createInMemoryCache(TimeSpan.FromMinutes 5.0)
        // Should be a no-op, not an exception.
        cache.Remove("never-set")

    [<Fact>]
    let ``GetOrAdd calls the factory once and returns its result on first access``() =
        let cache = createInMemoryCache(TimeSpan.FromMinutes 5.0)
        let mutable calls = 0

        let result =
            cache.GetOrAdd(
                "key1",
                fun () ->
                    calls <- calls + 1
                    "computed"
            )

        result |> shouldEqual "computed"
        calls |> shouldEqual 1

    [<Fact>]
    let ``GetOrAdd does not call the factory again once the value is cached``() =
        let cache = createInMemoryCache(TimeSpan.FromMinutes 5.0)
        let mutable calls = 0

        let factory() =
            calls <- calls + 1
            calls

        cache.GetOrAdd("key1", factory) |> shouldEqual 1
        // Second call should return the same cached value (1), not invoke the factory again (which would return 2).
        cache.GetOrAdd("key1", factory) |> shouldEqual 1
        calls |> shouldEqual 1

    [<Fact>]
    let ``GetOrAdd with different keys caches values independently``() =
        let cache = createInMemoryCache(TimeSpan.FromMinutes 5.0)
        cache.GetOrAdd("a", fun () -> "value-a") |> shouldEqual "value-a"
        cache.GetOrAdd("b", fun () -> "value-b") |> shouldEqual "value-b"
        cache.TryRetrieve("a") |> shouldEqual(Some "value-a")
        cache.TryRetrieve("b") |> shouldEqual(Some "value-b")

    [<Fact>]
    let ``TryRetrieve returns None once the expiration window has elapsed``() =
        let cache = createInMemoryCache(TimeSpan.FromMilliseconds 20.0)
        cache.Set("key1", "value")
        Thread.Sleep(200)
        cache.TryRetrieve("key1") |> shouldEqual None

    [<Fact>]
    let ``TryRetrieve with extendCacheExpiration=true keeps the entry alive past the original expiration``() =
        let cache = createInMemoryCache(TimeSpan.FromMilliseconds 500.0)
        cache.Set("key1", "value")
        // Read partway through the window and extend the expiration.
        Thread.Sleep(200)

        cache.TryRetrieve("key1", extendCacheExpiration = true)
        |> shouldEqual(Some "value")
        // Total elapsed time (200 + 350 = 550ms) exceeds the original 500ms window,
        // but the extension at 200ms should have reset the clock, so it should still be present.
        Thread.Sleep(350)
        cache.TryRetrieve("key1") |> shouldEqual(Some "value")

    [<Fact>]
    let ``TryRetrieve without extendCacheExpiration does not reset expiration``() =
        let cache = createInMemoryCache(TimeSpan.FromMilliseconds 100.0)
        cache.Set("key1", "value")
        Thread.Sleep(60)
        cache.TryRetrieve("key1") |> shouldEqual(Some "value")
        Thread.Sleep(80)
        // Original 100ms window has elapsed (60 + 80 = 140ms) without extension.
        cache.TryRetrieve("key1") |> shouldEqual None
