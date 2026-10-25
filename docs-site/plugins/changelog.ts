import type { LoadContext, Plugin } from "@docusaurus/types";

/** One change listed in a release's notes. */
export interface ChangelogItem {
  /** Conventional commit type (`feat`, `fix`, …), when the title carries one. */
  type?: string;
  /** Conventional commit scope, when the title carries one. */
  scope?: string;
  /** The change's title without its type and scope prefix. */
  summary: string;
  /** GitHub logins credited with the change. */
  authors: string[];
  /** The pull request that made the change. */
  pullRequest?: { number: number; url: string };
}

/** A published Wayd release and the changes it shipped. */
export interface ChangelogRelease {
  version: string;
  url: string;
  /** ISO 8601 instant the release was published. */
  publishedAt: string;
  items: ChangelogItem[];
  /** GitHub's comparison with the previous release. */
  compareUrl?: string;
}

const REPO = "destacey/Wayd";
const TYPES = new Set([
  "feat",
  "fix",
  "docs",
  "refactor",
  "test",
  "perf",
  "chore",
  "ci",
  "build",
]);

interface GitHubRelease {
  tag_name: string;
  html_url: string;
  published_at: string | null;
  draft: boolean;
  body: string | null;
}

async function fetchReleases(): Promise<GitHubRelease[]> {
  const headers: Record<string, string> = {
    Accept: "application/vnd.github+json",
    "User-Agent": "wayd-docs-site",
  };
  if (process.env.GITHUB_TOKEN) {
    headers.Authorization = `Bearer ${process.env.GITHUB_TOKEN}`;
  }

  const releases: GitHubRelease[] = [];
  for (let page = 1; ; page++) {
    const response = await fetch(
      `https://api.github.com/repos/${REPO}/releases?per_page=100&page=${page}`,
      { headers },
    );
    if (!response.ok) {
      throw new Error(
        `GitHub returned ${response.status} ${response.statusText} for the releases list`,
      );
    }
    const batch = (await response.json()) as GitHubRelease[];
    releases.push(...batch);
    if (batch.length < 100) return releases;
  }
}

/** Commit summaries start lowercase; on their own they read as sentences. */
const capitalize = (text: string) =>
  text.charAt(0).toUpperCase() + text.slice(1);

/**
 * Reads one bullet of GitHub's generated notes, in any of the shapes they
 * have taken: `title by @a, @b with @c in <pull url>`, or a bare title.
 */
export function parseItem(line: string): ChangelogItem {
  let text = line.replace(/^\s*[*-]\s+/, "").trim();

  let pullRequest: ChangelogItem["pullRequest"];
  const pull = text.match(/\s+in\s+(https:\/\/github\.com\/\S+\/pull\/(\d+))$/);
  if (pull) {
    pullRequest = { url: pull[1], number: Number(pull[2]) };
    text = text.slice(0, pull.index);
  }

  let authors: string[] = [];
  const by = text.match(/\s+by\s+(@[\w.\-[\]]+(?:(?:,\s*|\s+with\s+)@[\w.\-[\]]+)*)\s*$/);
  if (by) {
    authors = by[1].split(/,\s*|\s+with\s+/).map((a) => a.replace(/^@/, ""));
    text = text.slice(0, by.index);
  }

  const prefix = text.match(/^(\w+)(?:\(([^)]+)\))?!?:\s+/);
  if (prefix && TYPES.has(prefix[1].toLowerCase())) {
    return {
      type: prefix[1].toLowerCase(),
      scope: prefix[2],
      summary: capitalize(text.slice(prefix[0].length)),
      authors,
      pullRequest,
    };
  }
  return { summary: text, authors, pullRequest };
}

/** Turns a release's generated notes into the changes under "What's Changed". */
export function parseRelease(release: GitHubRelease): ChangelogRelease {
  const items: ChangelogItem[] = [];
  let compareUrl: string | undefined;
  let section = "";

  for (const line of (release.body ?? "").split(/\r?\n/)) {
    const heading = line.match(/^#+\s+(.*)$/);
    if (heading) {
      section = heading[1].trim();
      continue;
    }
    const compare = line.match(/^\*\*Full Changelog\*\*:\s*(\S+)/);
    if (compare) {
      compareUrl = compare[1];
      continue;
    }
    // Top-level bullets only: indented ones elaborate on the bullet above.
    if (section !== "New Contributors" && /^[*-]\s+/.test(line)) {
      items.push(parseItem(line));
    }
  }

  return {
    version: release.tag_name.replace(/^v/, ""),
    url: release.html_url,
    publishedAt: release.published_at ?? "",
    items,
    compareUrl,
  };
}

/**
 * Builds the /changelog page from the repository's GitHub releases at build
 * time. A failed fetch fails a CI build, so a deploy never publishes an empty
 * changelog; locally it only warns.
 */
export default function changelogPlugin(
  context: LoadContext,
): Plugin {
  return {
    name: "wayd-changelog",

    async loadContent(): Promise<ChangelogRelease[]> {
      try {
        const releases = await fetchReleases();
        return releases
          .filter((r) => !r.draft && /^v\d/.test(r.tag_name))
          .map(parseRelease)
          .sort((a, b) => b.publishedAt.localeCompare(a.publishedAt));
      } catch (error) {
        if (process.env.CI) throw error;
        console.warn(`[wayd-changelog] Releases not loaded: ${error}`);
        return [];
      }
    },

    async contentLoaded({ content, actions }) {
      const releases = await actions.createData(
        "releases.json",
        JSON.stringify(content as ChangelogRelease[]),
      );
      actions.addRoute({
        path: `${context.baseUrl}changelog`,
        component: "@site/src/components/Changelog",
        modules: { releases },
        exact: true,
      });
    },
  };
}
