import { defineConfig } from "vitepress";

export default defineConfig({
  title: "SwaggerProvider",
  description: "F# OpenAPI Type Provider",
  head: [
    ["link", { rel: "icon", href: "/files/img/logo.png" }],
    [
      "link",
      {
        rel: "stylesheet",
        href: "https://fonts.googleapis.com/css2?family=Fira+Code:wght@400;500&display=swap",
      },
    ],
  ],
  themeConfig: {
    logo: "/files/img/logo.png",
    nav: [
      { text: "Home", link: "/" },
      { text: "NuGet", link: "https://www.nuget.org/packages/SwaggerProvider" },
      { text: "GitHub", link: "https://github.com/fsprojects/SwaggerProvider" },
    ],
    sidebar: [
      {
        text: "Documentation",
        items: [
          { text: "Getting Started", link: "/getting-started" },
          { text: "OpenApiClientProvider", link: "/OpenApiClientProvider" },
          { text: "Customization", link: "/Customization" },
          { text: "Release Notes", link: "/RELEASE_NOTES" },
        ],
      },
    ],
    socialLinks: [
      { icon: "github", link: "https://github.com/fsprojects/SwaggerProvider" },
    ],
    search: {
      provider: "local",
    },
  },
});
