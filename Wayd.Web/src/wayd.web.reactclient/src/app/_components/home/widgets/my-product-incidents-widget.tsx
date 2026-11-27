'use client'

import { IncidentSeverityRank } from '@/src/app/product-management/incidents/_components/incident-severity-rank'
import { formatIncidentAge } from '@/src/app/product-management/incidents/_components/incident-format'
import { WaydList } from '@/src/components/common'
import EntityLink from '@/src/components/common/entity-link'
import { useGetMyIncidentsQuery } from '@/src/store/features/user-management/me-api'
import { Typography } from 'antd'
import { FC } from 'react'
import AttentionTile from '../attention-tile'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import { HomeTileProps, HomeWidgetProps } from '../widget-registry'
import { rowsToShow } from '../widget-settings'
import styles from '../home.module.css'

const { Text } = Typography

/** The most incidents the widget lists; past it, it says how many more there are. */
export const MY_PRODUCT_INCIDENTS_LIMIT = 5

const INCIDENTS_HREF = '/product-management/incidents'

/**
 * The active incidents on the products the caller's teams are linked to, most
 * severe first, then the longest active, as the API orders them.
 */
const MyProductIncidentsWidget: FC<HomeWidgetProps> = ({ title, settings }) => {
  const rows = rowsToShow(settings, MY_PRODUCT_INCIDENTS_LIMIT)
  const { data: incidents, isLoading, isError } = useGetMyIncidentsQuery()

  const body = () => {
    if (isError)
      return <WidgetError message="Could not load your products' incidents." />
    if (!incidents || incidents.length === 0) {
      return (
        <WidgetEmpty message="No active incidents on your teams' products. They appear here when one is opened on a product your team works on." />
      )
    }
    const more = incidents.length - rows
    return (
      <>
        <WaydList size="small">
          {incidents.slice(0, rows).map((incident) => (
            <WaydList.Item key={incident.id} className={styles.listItem}>
              <div className={styles.listItemBody}>
                <EntityLink
                  href={`/product-management/incidents/${incident.key}`}
                >
                  {incident.title}
                </EntityLink>
                <span className={styles.listItemMeta}>
                  <IncidentSeverityRank
                    name={incident.severityLevel.name}
                    color={incident.severityLevel.color}
                  />
                  <Text type="secondary">
                    {incident.status.name}, active{' '}
                    {formatIncidentAge(incident.noticedAt)}
                  </Text>
                  <Text type="secondary">
                    {incident.products.map((p) => p.name).join(', ')}
                  </Text>
                </span>
              </div>
            </WaydList.Item>
          ))}
        </WaydList>
        {more > 0 ? (
          <Text type="secondary" className={styles.widgetEmpty}>
            And {more} more.
          </Text>
        ) : null}
      </>
    )
  }

  return (
    <WidgetCard
      title={title}
      viewAllHref={INCIDENTS_HREF}
      loading={isLoading}
      failed={isError}
      empty={!incidents || incidents.length === 0}
      emptySummary="No active incidents"
    >
      {body()}
    </WidgetCard>
  )
}

/** Active incidents on the products the caller's teams are linked to. */
export const ActiveIncidentsTile: FC<HomeTileProps> = ({ title }) => {
  const { data: incidents, isLoading, isError } = useGetMyIncidentsQuery()
  return (
    <AttentionTile
      title={title}
      value={isError ? undefined : (incidents?.length ?? 0)}
      tooltip="Incidents still active (not closed, canceled or in a done status) on the products your teams work on."
      href={INCIDENTS_HREF}
      alert="error"
      loading={isLoading}
      context="on your products"
    />
  )
}

export default MyProductIncidentsWidget
