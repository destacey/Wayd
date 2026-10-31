import type { ReactNode } from "react";
import Layout from "@theme/Layout";
import Heading from "@theme/Heading";
import Link from "@docusaurus/Link";
import type {
  ChangelogItem,
  ChangelogRelease,
} from "../../../plugins/changelog";
import styles from "./styles.module.css";

const TYPE_LABELS: Record<string, string> = {
  feat: "Feature",
  fix: "Fix",
  perf: "Performance",
  docs: "Docs",
  refactor: "Refactor",
  test: "Test",
  chore: "Chore",
  ci: "CI",
  build: "Build",
};


const dateFormat = new Intl.DateTimeFormat("en-US", {
  year: "numeric",
  month: "long",
  day: "numeric",
  timeZone: "UTC",
});

/** Renders `code` spans in a title; everything else stays plain text. */
function InlineText({ text }: { text: string }): ReactNode {
  return text
    .split(/(`[^`]+`)/)
    .map((part, i) =>
      part.startsWith("`") && part.endsWith("`") && part.length > 1 ? (
        <code key={i}>{part.slice(1, -1)}</code>
      ) : (
        part
      ),
    );
}

function Item({ item }: { item: ChangelogItem }): ReactNode {
  return (
    <li className={styles.item}>
      {item.type && (
        <span className={styles.type} data-type={item.type}>
          {TYPE_LABELS[item.type]}
        </span>
      )}
      {item.scope && <span className={styles.scope}>{item.scope}</span>}
      <span>
        <InlineText text={item.summary} />
      </span>
      {(item.pullRequest || item.authors.length > 0) && (
        <span className={styles.meta}>
          {item.pullRequest && (
            <Link href={item.pullRequest.url}>#{item.pullRequest.number}</Link>
          )}
          {item.authors.map((author) => (
            <Link key={author} href={`https://github.com/${author.replace(/\[bot\]$/, "")}`}>
              @{author}
            </Link>
          ))}
        </span>
      )}
    </li>
  );
}

function Release({ release }: { release: ChangelogRelease }): ReactNode {
  const id = `v${release.version}`;
  return (
    <section className={styles.release}>
      <div className={styles.releaseHeader}>
        <Heading as="h2" id={id}>
          <Link href={release.url}>{id}</Link>
        </Heading>
        {release.publishedAt && (
          <time dateTime={release.publishedAt} className={styles.date}>
            {dateFormat.format(new Date(release.publishedAt))}
          </time>
        )}
      </div>
      {release.items.length > 0 ? (
        <ul className={styles.items}>
          {release.items.map((item, i) => (
            <Item key={i} item={item} />
          ))}
        </ul>
      ) : (
        <p className={styles.empty}>No changes listed.</p>
      )}
      {release.compareUrl && (
        <Link href={release.compareUrl} className={styles.compare}>
          Compare with the previous release
        </Link>
      )}
    </section>
  );
}

/** The /changelog page: every Wayd release, newest first. */
export default function Changelog({
  releases,
}: {
  releases: ChangelogRelease[];
}): ReactNode {
  return (
    <Layout
      title="Changelog"
      description="What changed in each Wayd release."
    >
      <main className={`container margin-vert--lg ${styles.page}`}>
        <Heading as="h1">Changelog</Heading>
        <p className={styles.intro}>
          What changed in each Wayd release, newest first.
        </p>
        {releases.length > 0 ? (
          releases.map((release) => (
            <Release key={release.version} release={release} />
          ))
        ) : (
          <p>
            Releases could not be loaded. See the{" "}
            <Link href="https://github.com/destacey/Wayd/releases">
              releases on GitHub
            </Link>
            .
          </p>
        )}
      </main>
    </Layout>
  );
}
