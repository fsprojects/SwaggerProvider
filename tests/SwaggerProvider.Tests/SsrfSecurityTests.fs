namespace SwaggerProvider.Tests.SsrfSecurityTests

open System
open Xunit
open SwaggerProvider.Internal.SchemaReader

/// Tests for SSRF protection - Critical: Unknown URL schemes
/// These tests verify that only safe URL schemes are allowed
module UnknownSchemeTests =

    [<Fact>]
    let ``Reject file protocol to prevent local file access``() =
        task {
            // Test: file:// protocol should be rejected to prevent SSRF via local file access
            let fileUrl = "file:///etc/passwd"

            let! ex =
                Assert.ThrowsAsync<Exception>(fun () ->
                    task {
                        let! _ = readSchemaPath false "" fileUrl
                        return ()
                    })


            Assert.Contains("Unsupported URL scheme", ex.Message)
            Assert.Contains("file://", ex.Message)
        }

    [<Fact>]
    let ``Reject FTP protocol to prevent remote protocol access``() =
        task {
            // Test: ftp:// protocol should be rejected to prevent SSRF via FTP
            let ftp_url = "ftp://internal-server/schema.json"

            let! ex =
                Assert.ThrowsAsync<Exception>(fun () ->
                    task {
                        let! _ = readSchemaPath false "" ftp_url
                        return ()
                    })

            Assert.Contains("Unsupported URL scheme", ex.Message)
        }

    [<Fact>]
    let ``Reject Gopher protocol to prevent remote protocol access``() =
        task {
            // Test: gopher:// protocol should be rejected to prevent SSRF via Gopher
            let gopher_url = "gopher://internal-server/schema.json"

            let! ex =
                Assert.ThrowsAsync<Exception>(fun () ->
                    task {
                        let! _ = readSchemaPath false "" gopher_url
                        return ()
                    })

            Assert.Contains("Unsupported URL scheme", ex.Message)
        }

    [<Fact>]
    let ``Reject DICT protocol to prevent remote protocol access``() =
        task {
            // Test: dict:// protocol should be rejected to prevent SSRF via DICT
            let dict_url = "dict://internal-server/schema.json"

            let! ex =
                Assert.ThrowsAsync<Exception>(fun () ->
                    task {
                        let! _ = readSchemaPath false "" dict_url
                        return ()
                    })

            Assert.Contains("Unsupported URL scheme", ex.Message)
        }


/// Tests for SSRF protection - High: IPv6 private ranges
/// These tests verify that IPv6 loopback, link-local, ULA, multicast addresses are rejected
module IPv6SecurityTests =

    [<Fact>]
    let ``Reject IPv6 loopback address ::1``() =
        // Test: IPv6 loopback ::1 should be rejected to prevent access to localhost services
        let ipv6_loopback_uri = Uri("https://[::1]/schema.json")

        let thrown_exception =
            Assert.Throws<Exception>(fun () -> validateSchemaUrl false ipv6_loopback_uri)

        Assert.Contains("private or loopback IPv6 addresses", thrown_exception.Message)
        Assert.Contains("::1", thrown_exception.Message)

    [<Fact>]
    let ``Reject IPv6 link-local address fe80::1``() =
        // Test: IPv6 link-local fe80::1 should be rejected to prevent access to link-local services
        let ipv6_link_local_uri = Uri("https://[fe80::1]/schema.json")

        let thrown_exception =
            Assert.Throws<Exception>(fun () -> validateSchemaUrl false ipv6_link_local_uri)

        Assert.Contains("private or loopback IPv6 addresses", thrown_exception.Message)

    [<Fact>]
    let ``Reject IPv6 unique local address fd00::1``() =
        // Test: IPv6 ULA fd00::1 should be rejected to prevent access to private network ranges
        let ipv6_ula_uri = Uri("https://[fd00::1]/schema.json")

        let thrown_exception =
            Assert.Throws<Exception>(fun () -> validateSchemaUrl false ipv6_ula_uri)

        Assert.Contains("private or loopback IPv6 addresses", thrown_exception.Message)

    [<Fact>]
    let ``Reject IPv6 unique local address fc00::1``() =
        // Test: IPv6 ULA fc00::1 should be rejected to prevent access to private network ranges
        let ipv6_ula_fc_uri = Uri("https://[fc00::1]/schema.json")

        let thrown_exception =
            Assert.Throws<Exception>(fun () -> validateSchemaUrl false ipv6_ula_fc_uri)

        Assert.Contains("private or loopback IPv6 addresses", thrown_exception.Message)

    [<Fact>]
    let ``Reject IPv6 unspecified address ::``() =
        // Test: IPv6 unspecified address :: should be rejected to prevent access to localhost services
        let ipv6_unspecified_uri = Uri("https://[::]/schema.json")

        let thrown_exception =
            Assert.Throws<Exception>(fun () -> validateSchemaUrl false ipv6_unspecified_uri)

        Assert.Contains("private or loopback IPv6 addresses", thrown_exception.Message)

    [<Fact>]
    let ``Reject IPv6 multicast address ff02::1``() =
        // Test: IPv6 multicast ff02::1 should be rejected to prevent access to multicast addresses
        let ipv6_multicast_uri = Uri("https://[ff02::1]/schema.json")

        let thrown_exception =
            Assert.Throws<Exception>(fun () -> validateSchemaUrl false ipv6_multicast_uri)

        Assert.Contains("private or loopback IPv6 addresses", thrown_exception.Message)

    [<Fact>]
    let ``Reject IPv6 multicast address ff00::1``() =
        // Test: IPv6 multicast ff00::1 should be rejected to prevent access to multicast addresses
        let ipv6_multicast_ff00_uri = Uri("https://[ff00::1]/schema.json")

        let thrown_exception =
            Assert.Throws<Exception>(fun () -> validateSchemaUrl false ipv6_multicast_ff00_uri)

        Assert.Contains("private or loopback IPv6 addresses", thrown_exception.Message)

    [<Fact>]
    let ``Allow public IPv6 documentation address 2001:db8::1``() =
        // Test: Public IPv6 documentation range 2001:db8::1 should pass SSRF validation
        // (Note: May fail due to network access, but SSRF validation should pass)
        let public_ipv6_uri = Uri("https://[2001:db8::1]/schema.json")

        try
            validateSchemaUrl false public_ipv6_uri
        with
        | ex when ex.Message.Contains("private or loopback") ->
            // SSRF validation failed incorrectly
            Assert.True(false, $"Public IPv6 should not be blocked by SSRF validation: {ex.Message}")
        | _ ->
            // Other errors are also acceptable (network, etc.)
            ()



/// Tests for IPv4 private ranges
/// These tests verify that IPv4 loopback and private ranges are rejected
module IPv4PrivateRangeTests =

    [<Fact>]
    let ``Reject IPv4 loopback address 127.0.0.1``() =
        // Test: IPv4 loopback 127.0.0.1 should be rejected to prevent access to localhost services
        let loopback_uri = Uri("https://127.0.0.1/schema.json")

        let thrown_exception =
            Assert.Throws<Exception>(fun () -> validateSchemaUrl false loopback_uri)

        Assert.Contains("localhost/loopback", thrown_exception.Message)

    [<Fact>]
    let ``Reject IPv4 private range 10.0.0.0/8``() =
        // Test: IPv4 private range 10.0.0.1 should be rejected to prevent access to private networks
        let private_10_uri = Uri("https://10.0.0.1/schema.json")

        let thrown_exception =
            Assert.Throws<Exception>(fun () -> validateSchemaUrl false private_10_uri)

        Assert.Contains("private or link-local", thrown_exception.Message)

    [<Fact>]
    let ``Reject IPv4 private range 172.16.0.0/12``() =
        // Test: IPv4 private range 172.16.0.1 should be rejected to prevent access to private networks
        let private_172_uri = Uri("https://172.16.0.1/schema.json")

        let thrown_exception =
            Assert.Throws<Exception>(fun () -> validateSchemaUrl false private_172_uri)

        Assert.Contains("private or link-local", thrown_exception.Message)

    [<Fact>]
    let ``Reject IPv4 private range 172.31.255.255``() =
        // Test: IPv4 private range upper bound 172.31.255.255 should be rejected
        let private_172_upper_uri = Uri("https://172.31.255.255/schema.json")

        let thrown_exception =
            Assert.Throws<Exception>(fun () -> validateSchemaUrl false private_172_upper_uri)

        Assert.Contains("private or link-local", thrown_exception.Message)

    [<Fact>]
    let ``Reject IPv4 private range 192.168.0.0/16``() =
        // Test: IPv4 private range 192.168.1.1 should be rejected to prevent access to private networks
        let private_192_uri = Uri("https://192.168.1.1/schema.json")

        let thrown_exception =
            Assert.Throws<Exception>(fun () -> validateSchemaUrl false private_192_uri)

        Assert.Contains("private or link-local", thrown_exception.Message)

    [<Fact>]
    let ``Reject IPv4 link-local address 169.254.0.0/16``() =
        // Test: IPv4 link-local 169.254.0.1 should be rejected to prevent access to link-local services
        let link_local_uri = Uri("https://169.254.0.1/schema.json")

        let thrown_exception =
            Assert.Throws<Exception>(fun () -> validateSchemaUrl false link_local_uri)

        Assert.Contains("private or link-local", thrown_exception.Message)


/// Tests for hostname validation
/// These tests verify that localhost hostname and public hostnames are handled correctly
module HostnameValidationTests =

    [<Fact>]
    let ``Reject localhost hostname``() =
        // Test: localhost hostname should be rejected to prevent access to localhost services
        let localhost_uri = Uri("https://localhost/schema.json")

        let thrown_exception =
            Assert.Throws<Exception>(fun () -> validateSchemaUrl false localhost_uri)

        Assert.Contains("localhost/loopback", thrown_exception.Message)

    [<Fact>]
    let ``Allow valid public hostname api.example.com``() =
        // Test: Valid public hostname should pass SSRF validation
        // (Note: May fail due to network access, but SSRF validation should pass)
        let public_uri = Uri("https://api.example.com/schema.json")

        try
            validateSchemaUrl false public_uri
        with
        | ex when ex.Message.Contains("localhost") || ex.Message.Contains("private") ->
            Assert.Fail($"Public hostname should not be blocked by SSRF validation: {ex.Message}")
        | _ -> ()


/// Tests for relative file paths
/// These tests verify that relative file paths with __SOURCE_DIRECTORY__ work correctly
module RelativeFilePathTests =

    [<Fact>]
    let ``Allow relative file paths with __SOURCE_DIRECTORY__``() =
        task {
            // Test: Relative file paths using __SOURCE_DIRECTORY__ should work correctly
            // This ensures that development-time file references like:
            // let Schema = __SOURCE_DIRECTORY__ + "/../Schemas/v2/petstore.json"
            // are properly handled (not rejected by SSRF validation)
            let schemaPath = __SOURCE_DIRECTORY__ + "/../Schemas/v2/petstore.json"

            try
                let! _ = readSchemaPath false "" schemaPath
                () // If file exists, that's fine
            with
            | :? Swagger.OpenApiException ->
                // Swagger parsing errors are okay - means file was read
                ()
            | ex when ex.Message.Contains("Schema file not found") ->
                // File not found is okay - path was resolved correctly
                ()
            | ex when
                ex.Message.Contains("Unsupported URL scheme")
                || ex.Message.Contains("localhost")
                || ex.Message.Contains("private")
                ->
                // SSRF validation errors mean relative paths are being blocked - this is the bug we're checking for
                Assert.Fail($"Relative file paths should not be rejected by SSRF validation: {ex.Message}")
            | _ ->
                // Other errors (file reading issues, etc.) are acceptable
                ()
        }


/// Tests for disabled SSRF protection (development mode)
/// These tests verify that when SSRF protection is disabled, all addresses are allowed
module SsrfBypassTests =

    [<Fact>]
    let ``Allow IPv4 loopback when ignoreSsrfProtection is true``() =
        // Test: IPv4 loopback should be allowed when SSRF protection is disabled
        let loopback_uri = Uri("https://127.0.0.1/schema.json")
        // Should not throw when ignoreSsrfProtection=true
        validateSchemaUrl true loopback_uri

    [<Fact>]
    let ``Allow IPv6 loopback when ignoreSsrfProtection is true``() =
        // Test: IPv6 loopback should be allowed when SSRF protection is disabled
        let ipv6_loopback_uri = Uri("https://[::1]/schema.json")
        // Should not throw when ignoreSsrfProtection=true
        validateSchemaUrl true ipv6_loopback_uri

    [<Fact>]
    let ``Allow IPv4 private range when ignoreSsrfProtection is true``() =
        // Test: IPv4 private range should be allowed when SSRF protection is disabled
        let private_uri = Uri("https://192.168.1.1/schema.json")
        // Should not throw when ignoreSsrfProtection=true
        validateSchemaUrl true private_uri

    [<Fact>]
    let ``Allow IPv6 private range when ignoreSsrfProtection is true``() =
        // Test: IPv6 private range should be allowed when SSRF protection is disabled
        let ipv6_private_uri = Uri("https://[fd00::1]/schema.json")
        // Should not throw when ignoreSsrfProtection=true
        validateSchemaUrl true ipv6_private_uri

    [<Fact>]
    let ``Reject HTTP in production mode``() =
        // Test: HTTP should be rejected in production mode (HTTPS only)
        let http_url = Uri("http://api.example.com/schema.json")

        let thrown_exception =
            Assert.Throws<Exception>(fun () -> validateSchemaUrl false http_url)

        Assert.Contains("Only HTTPS URLs are allowed", thrown_exception.Message)

    [<Fact>]
    let ``Allow HTTP when ignoreSsrfProtection is true``() =
        // Test: HTTP should be allowed when SSRF protection is disabled (development mode)
        let http_url = Uri("http://localhost/schema.json")

        try
            validateSchemaUrl true http_url
        with
        | ex when ex.Message.Contains("Only HTTPS") ->
            Assert.True(false, $"HTTP should not be rejected by SSRF validation when disabled: {ex.Message}")
        | _ -> ()
