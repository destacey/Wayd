# Wayd Documentation Site

This site is built using [Docusaurus](https://docusaurus.io/) and deployed to GitHub Pages. It reads MDX content from the shared `docs/` folder at the repository root.

## Installation

```bash
npm install
```

## Local Development

```bash
npm start
```

This starts a local development server. Most changes are reflected live without restarting.

## Build

```bash
npm run build
```

Generates static content into the `build` directory.

## Search

Search comes from [`@easyops-cn/docusaurus-search-local`](https://github.com/easyops-cn/docusaurus-search-local), which builds the index (`build/search-index.json`) during `npm run build`. The index does not exist under `npm start`, so try search with `npm run build && npm run serve`.

## Deployment

The site is automatically deployed to GitHub Pages via the `deploy-docs.yml` GitHub Actions workflow when changes are pushed to `main` in the `docs/` or `docs-site/` directories.
