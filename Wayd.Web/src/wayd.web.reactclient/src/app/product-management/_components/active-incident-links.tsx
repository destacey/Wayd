'use client'

import { createMultiValueSetFilter } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { Popover, Tag } from 'antd'
import Link from 'next/link'
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

const severityNames = (row: IncidentRow): string[] => {
  const names = (row.activeIncidents ?? [])
    .map((incident) => incident.severityName)
    .filter((name): name is string => !!name)
  if (names.length > 0) return names
  return row.activeIncidentSeverityName ? [row.activeIncidentSeverityName] : []
}

/**
 * A count of the active incidents on a product or release. Hovering it lists each one —
 * severity, name, and status — and each row opens that incident.
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
      trigger="hover"
      title="Active incidents"
      content={
        <ul className={styles.list}>
          {links.map((incident) => (
            <li key={incident.key}>
              <Link
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
              </Link>
            </li>
          ))}
        </ul>
      }
    >
      <Tag color={color || undefined} aria-label={label} className={styles.pill}>
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
    accessorFn: (row) => severityNames(row).join(', '),
    header: 'Incident',
    size: 110,
    filterFn: createMultiValueSetFilter<T>(severityNames),
    meta: { filterType: 'set', multiValues: severityNames },
    cell: ({ row }) => (
      <ActiveIncidentPill
        incidents={row.original.activeIncidents}
        color={row.original.activeIncidentSeverityColor}
      />
    ),
  }
}
