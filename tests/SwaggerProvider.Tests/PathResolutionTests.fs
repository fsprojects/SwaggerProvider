namespace SwaggerProvider.Tests.PathResolutionTests

open System
open System.IO
open System.Runtime.InteropServices
open Xunit
open SwaggerProvider.Internal.SchemaReader

/// Tests for path resolution logic
/// These tests verify that relative file paths are handled correctly across platforms
module PathResolutionTests =

    let isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

    [<Fact>]
    let ``getAbsolutePath handles paths with parent directory references after concatenation``() =
        // Test: When __SOURCE_DIRECTORY__ + "/../Schemas/..." is used, the result should be
        // treated as a valid path, not incorrectly parsed
        let resolutionFolder = if isWindows then "C:\\Users\\test\\project\\tests" else "/home/user/project/tests"
        // Simulate what happens when you do: __SOURCE_DIRECTORY__ + "/../Schemas/..."
        let concatenated = resolutionFolder + (if isWindows then "\\..\\Schemas\\v2\\petstore.json" else "/../Schemas/v2/petstore.json")
        
        let result = getAbsolutePath resolutionFolder concatenated
        
        // Should keep the path as-is (it's already a full path after concatenation)
        // Path.GetFullPath will normalize it later
        Assert.Contains("Schemas", result)
        Assert.Contains("petstore.json", result)

    [<Fact>]
    let ``getAbsolutePath handles simple relative paths``() =
        // Test: Simple relative paths should be combined with resolution folder
        let resolutionFolder = if isWindows then "C:\\Users\\test\\project" else "/home/user/project"
        let schemaPath = "../Schemas/v2/petstore.json"
        
        let result = getAbsolutePath resolutionFolder schemaPath
        
        // Should combine with resolution folder
        Assert.Contains("project", result)
        Assert.Contains("Schemas", result)

    [<Fact>]
    let ``getAbsolutePath handles current directory relative paths``() =
        // Test: Paths starting with ./ should be treated as relative
        let resolutionFolder = if isWindows then "C:\\Users\\test\\project" else "/home/user/project"
        let schemaPath = "./Schemas/v2/petstore.json"
        
        let result = getAbsolutePath resolutionFolder schemaPath
        
        // Should combine with resolution folder
        Assert.Contains("project", result)
        Assert.Contains("Schemas", result)

    [<Fact>]
    let ``getAbsolutePath handles absolute Unix paths``() =
        if not isWindows then
            // Test: Absolute Unix paths should be kept as-is
            let resolutionFolder = "/home/user/project"
            let schemaPath = "/etc/schemas/petstore.json"
            
            let result = getAbsolutePath resolutionFolder schemaPath
            
            // Should keep the absolute path
            Assert.Equal("/etc/schemas/petstore.json", result)

    [<Fact>]
    let ``getAbsolutePath handles absolute Windows paths with drive letter``() =
        if isWindows then
            // Test: Absolute Windows paths with drive should be kept as-is
            let resolutionFolder = "C:\\Users\\test\\project"
            let schemaPath = "D:\\Schemas\\petstore.json"
            
            let result = getAbsolutePath resolutionFolder schemaPath
            
            // Should keep the absolute path
            Assert.Equal("D:\\Schemas\\petstore.json", result)

    [<Fact>]
    let ``getAbsolutePath handles HTTP URLs``() =
        // Test: HTTP URLs should be kept as-is
        let resolutionFolder = if isWindows then "C:\\Users\\test\\project" else "/home/user/project"
        let schemaPath = "https://example.com/schema.json"
        
        let result = getAbsolutePath resolutionFolder schemaPath
        
        // Should keep the URL unchanged
        Assert.Equal("https://example.com/schema.json", result)

    [<Fact>]
    let ``getAbsolutePath concatenated with SOURCE_DIRECTORY works correctly``() =
        // Test: Simulates the common pattern: __SOURCE_DIRECTORY__ + "/../Schemas/..."
        // This should work correctly on both Windows and Unix
        let sourceDir = __SOURCE_DIRECTORY__
        let relativePart = "/../Schemas/v2/petstore.json"
        let combined = sourceDir + relativePart
        
        // This simulates what happens in test files
        let result = getAbsolutePath sourceDir combined
        
        // Should resolve to a path that contains Schemas
        // The exact result depends on whether the file exists, but it should at least
        // not throw an exception and should contain "Schemas"
        Assert.Contains("Schemas", result)
