import { notFound } from 'next/navigation'
import { getDocBySlug } from '@/src/services/docs'
import DocsPageContent from '../docs-page-content'

interface DocsPageProps {
  params: Promise<{ slug?: string[] }>
}

export default async function DocsPage({ params }: DocsPageProps) {
  const { slug = [] } = await params

  // Determine the effective slug for loading the doc.
  // Always try with 'index' appended first for directories, then the slug itself.
  // This ensures we always know whether we loaded an index page or a leaf page,
  // which matters for resolving relative links.
  let doc = null
  let isIndex = false

  if (slug.length === 0) {
    doc = getDocBySlug(['index'])
    isIndex = true
  } else {
    // Try as directory index first
    doc = getDocBySlug([...slug, 'index'])
    if (doc) {
      isIndex = true
    } else {
      // Try as leaf page
      doc = getDocBySlug(slug)
    }
  }

  if (!doc) {
    notFound()
  }

  // Build the file-system slug for the remark plugin.
  // For index pages, append 'index' so the plugin knows the file is in the directory.
  // For leaf pages, the slug is the file path directly.
  const fileSlug = isIndex
    ? (slug.length === 0 ? ['index'] : [...slug, 'index'])
    : slug

  return <DocsPageContent content={doc.content} fileSlug={fileSlug} />
}

export async function generateMetadata({ params }: DocsPageProps) {
  const { slug = [] } = await params
  const doc = slug.length === 0
    ? getDocBySlug(['index'])
    : getDocBySlug([...slug, 'index']) || getDocBySlug(slug)

  return {
    title: doc ? `${doc.frontmatter.title} | Wayd Docs` : 'Wayd Documentation',
    description: doc?.frontmatter.description,
  }
}
