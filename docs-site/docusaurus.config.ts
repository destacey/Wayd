import { themes as prismThemes } from "prism-react-renderer";
import type { Config } from "@docusaurus/types";
import type * as Preset from "@docusaurus/preset-classic";

const config: Config = {
  title: "Wayd",
  tagline:
    "Work management for planning, managing, and delivering across teams",
  favicon: "img/favicon.ico",

  future: {
    v4: true,
  },

  url: "https://wayd.dev",
  baseUrl: process.env.DOCS_BASE_URL || "/",
  organizationName: "destacey",
  projectName: "Wayd",
  trailingSlash: false,

  onBrokenLinks: "throw",
  onBrokenAnchors: "throw",

  i18n: {
    defaultLocale: "en",
    locales: ["en"],
  },

  // Enable Mermaid diagram support
  markdown: {
    mermaid: true,
    hooks: {
      onBrokenMarkdownLinks: "throw",
    },
  },
  themes: ["@docusaurus/theme-mermaid"],

  plugins: [
    [
      "@docusaurus/plugin-client-redirects",
      {
        // Pages that moved, so links to their old addresses still arrive.
        redirects: [
          {
            from: "/docs/user-guide/work-management/allocation-report",
            to: "/docs/user-guide/work-management/work-distribution-report",
          },
        ],
      },
    ],
  ],

  presets: [
    [
      "classic",
      {
        docs: {
          // Point to the shared docs/ folder in the repo root
          path: "../docs",
          sidebarPath: "./sidebars.ts",
          routeBasePath: "docs",
          editUrl: "https://github.com/destacey/Wayd/edit/main/docs/",
          showLastUpdateTime: true,
        },
        blog: false, // Disable blog for now
        theme: {
          customCss: "./src/css/custom.css",
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    colorMode: {
      defaultMode: "light",
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: "Wayd",
      items: [
        {
          type: "docSidebar",
          sidebarId: "docsSidebar",
          position: "left",
          label: "Documentation",
        },
        {
          type: "docSidebar",
          sidebarId: "contributingSidebar",
          position: "left",
          label: "Contributing",
        },
        {
          type: "docSidebar",
          sidebarId: "referenceSidebar",
          position: "left",
          label: "Reference",
        },
        {
          href: "https://github.com/destacey/Wayd",
          label: "GitHub",
          position: "right",
        },
      ],
    },
    footer: {
      style: "dark",
      links: [
        {
          title: "Documentation",
          items: [
            { label: "Getting Started", to: "/docs/getting-started" },
            { label: "User Guide", to: "/docs/user-guide" },
            { label: "Contributing", to: "/docs/contributing" },
          ],
        },
        {
          title: "Reference",
          items: [
            { label: "API Reference", to: "/docs/reference/api" },
            {
              label: "Technology Stack",
              to: "/docs/reference/technology-stack",
            },
            { label: "Attribution", to: "/docs/reference/attribution" },
          ],
        },
        {
          title: "More",
          items: [
            {
              label: "GitHub",
              href: "https://github.com/destacey/Wayd",
            },
          ],
        },
      ],
      copyright: `Copyright \u00a9 ${new Date().getFullYear()} Wayd. Built with Docusaurus.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ["csharp", "bash", "json", "typescript"],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
