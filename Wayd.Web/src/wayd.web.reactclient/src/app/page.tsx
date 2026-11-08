'use client'

import { UnlinkedEmployeeAlert, WaydEmpty } from '@/src/components/common'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { Suspense } from 'react'
import AttentionStrip from './_components/home/attention-strip'
import HomeGreeting from './_components/home/home-greeting'
import styles from './_components/home/home.module.css'
import { useHomeLayout } from './_components/home/use-home-layout'
import WidgetCard from './_components/home/widget-card'
import { HomeWidget, widgetAnchorId } from './_components/home/widget-registry'

/** What an unlinked account is missing, so the page explains its gaps rather than looking broken. */
const UNLINKED_CONSEQUENCE =
  'Your projects, risks, tasks and team sprints, your notifications and the records you follow appear here once it is.'

const WidgetColumn = ({
  widgets,
  className,
}: {
  widgets: HomeWidget[]
  className: string
}) => (
  <div className={className}>
    {widgets.map(({ id, title, Component }) => (
      <section key={id} id={widgetAnchorId(id)} className={styles.widget}>
        {/* A lazily loaded widget shows its titled skeleton while its code arrives. */}
        <Suspense fallback={<WidgetCard title={title} loading />}>
          <Component title={title} />
        </Suspense>
      </section>
    ))}
  </div>
)

/**
 * The home page: a personal briefing built from the widget registry. The
 * frame renders at once and each widget loads, and shows a skeleton, on its own.
 */
const HomePage = () => {
  useDocumentTitle('Home')
  const { columns, tiles, hasLinkedEmployee } = useHomeLayout()
  const hasWidgets = columns.main.length + columns.side.length > 0

  return (
    <div className="page-gutters">
      <HomeGreeting />
      <UnlinkedEmployeeAlert consequence={UNLINKED_CONSEQUENCE} />
      <AttentionStrip tiles={tiles} />
      {hasWidgets ? (
        // With nothing for the side column, the main column takes the width.
        <div className={columns.side.length > 0 ? styles.columns : undefined}>
          <WidgetColumn widgets={columns.main} className={styles.column} />
          <WidgetColumn
            widgets={columns.side}
            className={`${styles.column} ${styles.side}`}
          />
        </div>
      ) : hasLinkedEmployee ? (
        // An unlinked account already has the banner saying what will appear.
        <WaydEmpty message="Nothing on the home page applies to your account yet. It fills as you are given access to the areas you work in." />
      ) : null}
    </div>
  )
}

export default HomePage
