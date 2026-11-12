'use client'

import EntityLink from '@/src/components/common/entity-link'
import { createMultiValueSetFilter } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { Popover, Tag } from 'antd'
import styles from './active-incident-links.module.css'

/** One active incident a product or release row can open. */
export type ActiveIncidentLink = {
  key: number
  title?: string | null
  severityName?: string | null
  severityColor?: string | null
  statusName?: string | null
}

type IncidentRow = {
  activeIncidents?: readonly ActiveIncidentLink[] | null
  activeIncidentSeverityName?: string | null
  activeIncidentSeverityColor?: string | null
}

/** The count the cell shows, so the set filter offers counts rather than severity names. */
const incidentCounts = (row: IncidentRow): string[] => {
  const count = row.activeIncidents?.length ?? 0
  return count > 0 ? [String(count)] : []
}

/**
 * A count of the active incidents on a product or release. Hovering or focusing it lists
 * each one — severity, name, and status — and each row opens that incident.
 */
export function ActiveIncidentPill({
  incidents,
  color,
}: {
  incidents?: readonly ActiveIncidentLink[] | null
  color?: string | null
}) {
  const links = incidents ?? []
  if (links.length === 0) return null

  const label =
    links.length === 1
      ? '1 active incident'
      : `${links.length} active incidents`

  return (
    <Popover
      trigger={['hover', 'focus']}
      title="Active incidents"
      content={
        <ul className={styles.list}>
          {links.map((incident) => (
            <li key={incident.key}>
              <EntityLink
                href={`/product-management/incidents/${incident.key}`}
                className={styles.item}
              >
                <span className={styles.severity}>
                  {incident.severityColor ? (
                    <b style={{ background: incident.severityColor }} aria-hidden />
                  ) : null}
                  {incident.severityName}
                </span>
                <span className={styles.name}>{incident.title}</span>
                <span className={styles.status}>{incident.statusName}</span>
              </EntityLink>
            </li>
          ))}
        </ul>
      }
    >
      <Tag
        color={color || undefined}
        aria-label={label}
        className={styles.pill}
        tabIndex={0}
      >
        {links.length}
      </Tag>
    </Popover>
  )
}

export function activeIncidentColumn<T extends IncidentRow>(): ColumnDef<
  T,
  any
> {
  return {
    id: 'activeIncident',
    accessorFn: (row) => row.activeIncidents?.length ?? 0,
    header: 'Incident',
    size: 110,
    filterFn: createMultiValueSetFilter<T>(incidentCounts),
    meta: { filterType: 'set', multiValues: incidentCounts },
    cell: ({ row }) => (
      <ActiveIncidentPill
        incidents={row.original.activeIncidents}
        color={row.original.activeIncidentSeverityColor}
      />
    ),
  }
}
