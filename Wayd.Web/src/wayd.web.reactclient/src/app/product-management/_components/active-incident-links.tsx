'use client'

import { createMultiValueSetFilter } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import Link from 'next/link'
import { Fragment } from 'react'

/** One active incident a product or release row can open. */
export type ActiveIncidentLink = {
  key: number
  title?: string | null
  severityName?: string | null
}

type IncidentRow = {
  activeIncidents?: readonly ActiveIncidentLink[] | null
  activeIncidentSeverityName?: string | null
}

const severityNames = (row: IncidentRow): string[] => {
  const names = (row.activeIncidents ?? [])
    .map((incident) => incident.severityName)
    .filter((name): name is string => !!name)
  if (names.length > 0) return names
  return row.activeIncidentSeverityName ? [row.activeIncidentSeverityName] : []
}

/**
 * The incident column: each active incident is a link to its record. The text is the severity
 * name, which is what the column has always shown; the title is on the link for the reader who
 * has more than one.
 */
export function ActiveIncidentLinks({
  incidents,
}: {
  incidents?: readonly ActiveIncidentLink[] | null
}) {
  const links = incidents ?? []
  if (links.length === 0) return null

  return links.map((incident, index) => (
    <Fragment key={incident.key}>
      {index > 0 && ', '}
      <Link
        href={`/product-management/incidents/${incident.key}`}
        title={incident.title ?? undefined}
      >
        {incident.severityName || incident.title}
      </Link>
    </Fragment>
  ))
}

export function activeIncidentColumn<T extends IncidentRow>(): ColumnDef<
  T,
  any
> {
  return {
    id: 'activeIncident',
    accessorFn: (row) => severityNames(row).join(', '),
    header: 'Incident',
    size: 160,
    filterFn: createMultiValueSetFilter<T>(severityNames),
    meta: { filterType: 'set', multiValues: severityNames },
    cell: ({ row }) => (
      <ActiveIncidentLinks incidents={row.original.activeIncidents} />
    ),
  }
}
