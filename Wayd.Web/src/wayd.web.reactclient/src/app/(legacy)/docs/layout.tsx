import './docs.css'
import { getDocsNavigation } from '@/src/services/docs'
import DocsLayoutShell from './docs-layout-shell'

export default function DocsLayout({
  children,
}: {
  children: React.ReactNode
}) {
  const navigation = getDocsNavigation()

  return (
    <DocsLayoutShell navigation={navigation}>
      {children}
    </DocsLayoutShell>
  )
}
