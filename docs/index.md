---
layout: home
hero:
  name: OpenAPI & Swagger F# Type Provider
  tagline: Strongly-typed HTTP clients from OpenAPI 3.0 and Swagger 2.0 schemas in JSON and YAML formats.
  image:
    src: /files/img/landing.png
    alt: SwaggerProvider
  actions:
    - theme: brand
      text: Getting Started
      link: /getting-started
    - theme: alt
      text: View on GitHub
      link: https://github.com/fsprojects/SwaggerProvider
features:
  - icon: "\u26A1"
    title: Compile-Time Types
    details: Types are generated at compile time directly from live or local schema files — no separate codegen step needed.
  - icon: "\uD83D\uDD04"
    title: OpenAPI 3.0 & Swagger 2.0
    details: Supports both specifications in JSON and YAML formats via Microsoft.OpenApi.
  - icon: "\uD83D\uDCE6"
    title: Works Everywhere
    details: Use in F# scripts, .NET projects, and F# Interactive — with full IntelliSense and type-checking.
  - icon: "\uD83D\uDD12"
    title: SSRF Protection
    details: Blocks HTTP and private IP addresses by default to prevent server-side request forgery attacks.
  - icon: "\u23F3"
    title: CancellationToken Support
    details: Every generated method accepts an optional CancellationToken for cancelling long-running requests.
  - icon: "\uD83D\uDD27"
    title: Fully Customizable
    details: Bring your own HttpClient, DelegatingHandler, or override JSON serialization.
---
