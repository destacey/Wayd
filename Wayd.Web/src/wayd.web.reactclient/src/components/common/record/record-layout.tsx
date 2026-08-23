'use client'

import { Flex, Grid, Skeleton } from 'antd'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { ReactNode, Suspense, useCallback, useMemo } from 'react'
import styles from './record-layout.module.css'
import RecordSectionNav from './record-section-nav'
import SectionBoundary from './section-boundary'
import { RecordSection } from './types'

const { useBreakpoint } = Grid

export interface RecordLayoutProps {
  sections: RecordSection[]
  /** Report views. Grouped separately in the rail, addressable the same way. */
  reports?: RecordSection[]
  /** The section shown when the URL carries no `?section=`. */
  defaultSection: string
  /**
   * The identity bar — normally a `PageTitle`. Rendered full-bleed above the
   * rail rather than by the page, so the rail sits flush against the app sider.
   */
  header?: ReactNode
  children: (activeSection: string) => ReactNode
}

const SectionFallback = () => (
  <Skeleton active paragraph={{ rows: 4 }} />
)

const RecordLayoutInner = ({
  sections,
  reports = [],
  defaultSection,
  header,
  children,
}: RecordLayoutProps) => {
  const screens = useBreakpoint()
  const params = useSearchParams()
  const router = useRouter()
  const pathname = usePathname()

  const compact = !screens.md

  const all = useMemo(
    () => [...sections, ...reports],
    [sections, reports],
  )

  // An unknown or forbidden section falls back rather than rendering an empty
  // panel — sections are permission-gated, so a shared link can legitimately
  // reach someone who cannot see that section.
  const requested = params.get('section')
  const activeSection =
    requested && all.some((s) => s.id === requested)
      ? requested
      : defaultSection

  const goTo = useCallback(
    (id: string) => {
      const url =
        id === defaultSection ? pathname : `${pathname}?section=${id}`
      // replace, not push: Back returns to the list rather than stepping back
      // through every section visited. scroll:false or the router jumps to top.
      router.replace(url, { scroll: false })
    },
    [defaultSection, pathname, router],
  )

  const activeLabel = all.find((s) => s.id === activeSection)?.label

  const nav = (
    <RecordSectionNav
      sections={sections}
      reports={reports}
      activeSection={activeSection}
      onChange={goTo}
      compact={compact}
    />
  )

  const body = (
    <SectionBoundary
      // Clears a caught error when the user moves to another section.
      key={activeSection}
      sectionLabel={activeLabel}
      onLeave={
        activeSection === defaultSection ? undefined : () => goTo(defaultSection)
      }
    >
      <Suspense fallback={<SectionFallback />}>
        {children(activeSection)}
      </Suspense>
    </SectionBoundary>
  )

  return (
    <div className={styles.shell}>
      {header && <div className={styles.header}>{header}</div>}
      <div className={styles.body}>
        {compact ? (
          <div className={styles.content}>
            <Flex vertical gap="middle">
              {nav}
              {body}
            </Flex>
          </div>
        ) : (
          <>
            {nav}
            <div className={styles.content}>{body}</div>
          </>
        )}
      </div>
    </div>
  )
}

/**
 * The shared record page layout: a section rail (or a Select on small
 * screens), URL-addressable sections, and an error boundary per section.
 *
 * Sections are addressed with `?section={id}` rather than a hash — query
 * params are correct on first render, restore on back/forward without a
 * listener, and compose with other state later (`?section=risks&status=open`).
 *
 * The Suspense boundary is required: `useSearchParams` suspends a prerendered
 * route up to the nearest boundary. In development routes render on demand, so
 * a missing boundary only shows up in a production build.
 */
const RecordLayout = (props: RecordLayoutProps) => (
  <Suspense fallback={<SectionFallback />}>
    <RecordLayoutInner {...props} />
  </Suspense>
)

export default RecordLayout
