'use client'

import { Flex, Grid, Skeleton, Typography } from 'antd'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { ReactNode, Suspense, useCallback, useMemo } from 'react'
import styles from './record-layout.module.css'
import RecordHeader, { RecordHeaderProps } from './record-header'
import RecordSectionNav from './record-section-nav'
import RecordFactsRail from './record-facts-rail'
import SectionBoundary from './section-boundary'
import { useFactsRail } from './use-facts-rail'
import { RecordSection } from './types'

const { useBreakpoint } = Grid
const { Title } = Typography

export interface RecordLayoutProps {
  sections: RecordSection[]
  /** Report views. Grouped separately in the rail, addressable the same way. */
  reports?: RecordSection[]
  /** The section shown when the URL carries no `?section=`. */
  defaultSection: string
  /**
   * The record this page is about. Rendered as the identity bar above the
   * rail, full-bleed, so the rail sits flush against the app sider.
   */
  record?: RecordHeaderProps
  /**
   * The record's stable facts — a stack of `LabeledContent`. Rendered as a
   * closeable panel beside the content, moving inline below `md`. Omit it and
   * no panel appears.
   */
  facts?: ReactNode
  /** Actions for the active section, shown beside its heading. */
  sectionActions?: ReactNode
  children: (activeSection: string) => ReactNode
}

const SectionFallback = () => (
  <Skeleton active paragraph={{ rows: 4 }} />
)

const RecordLayoutInner = ({
  sections,
  reports = [],
  defaultSection,
  record,
  facts,
  sectionActions,
  children,
}: RecordLayoutProps) => {
  const screens = useBreakpoint()
  const params = useSearchParams()
  const router = useRouter()
  const pathname = usePathname()
  const factsRail = useFactsRail(!!facts)

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

  // useSearchParams returns a fresh object every render, so depending on it
  // directly would rebuild goTo each time. The string is stable by value.
  const query = params.toString()

  const goTo = useCallback(
    (id: string) => {
      // Carry the rest of the query across. Sections are one axis of page
      // state among several — a selected team, an overview tab — and rebuilding
      // the URL from the section alone silently drops the others, resetting
      // them every time the user changes section.
      const next = new URLSearchParams(query)
      if (id === defaultSection) {
        next.delete('section')
      } else {
        next.set('section', id)
      }
      const nextQuery = next.toString()
      // replace, not push: Back returns to the list rather than stepping back
      // through every section visited. scroll:false or the router jumps to top.
      router.replace(nextQuery ? `${pathname}?${nextQuery}` : pathname, {
        scroll: false,
      })
    },
    [defaultSection, query, pathname, router],
  )

  const active = all.find((s) => s.id === activeSection)
  const activeLabel = active?.label

  // A rail holding one item spends its width saying there is nowhere to go, so
  // a record with a single section renders its content full width instead.
  const hasRail = all.length > 1

  const nav = hasRail ? (
    <RecordSectionNav
      sections={sections}
      reports={reports}
      activeSection={activeSection}
      onChange={goTo}
      compact={compact}
    />
  ) : null

  // The rail marks where you are, but the content needs its own heading —
  // without it a section opens as an unlabelled grid under the identity bar.
  //
  // Level 5 (16px) under the record name's 20px, leaving 14px for blocks
  // inside a section — so the ladder reads downward: record, section, block.
  //
  // With no rail there is nothing to echo, so a lone section skips the heading
  // unless it has actions that need a row to sit in.
  const sectionHeading =
    active?.hideHeading || (!hasRail && !sectionActions) ? null : (
      <Flex align="center" gap="small" className={styles.sectionHeading}>
        <Title level={5} style={{ margin: 0 }}>
          {activeLabel}
        </Title>
        {sectionActions && (
          <>
            <div style={{ flexGrow: 1 }} />
            {sectionActions}
          </>
        )}
      </Flex>
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

  const factsPanel =
    facts && factsRail.mode === 'panel' ? (
      <RecordFactsRail
        open={factsRail.open}
        onOpenChange={factsRail.setOpen}
        width={factsRail.width}
        onWidthChange={factsRail.setWidth}
      >
        {facts}
      </RecordFactsRail>
    ) : null

  return (
    <div className={styles.shell}>
      {record && (
        <div className={styles.header}>
          <RecordHeader
            {...record}
            factsToggle={
              factsRail.showToggle
                ? { open: factsRail.open, onToggle: factsRail.setOpen }
                : undefined
            }
          />
        </div>
      )}
      <div className={styles.body}>
        {compact ? (
          <div className={styles.content}>
            <Flex vertical gap="middle">
              {nav}
              <div>
                {sectionHeading}
                {body}
              </div>
              {/* After the section, as the panel is on desktop. Placing the
                  facts between the nav and the content pushed the content off
                  screen on a record with a long panel. */}
              {factsRail.mode === 'inline' && facts && (
                <Flex vertical gap={10}>
                  {facts}
                </Flex>
              )}
            </Flex>
          </div>
        ) : (
          <>
            {nav}
            <div className={styles.content}>
              {sectionHeading}
              {body}
            </div>
            {factsPanel}
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
