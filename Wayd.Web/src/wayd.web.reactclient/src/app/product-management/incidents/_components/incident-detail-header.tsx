'use client'

import { IncidentSeverityRank } from '@/src/app/product-management/incidents/_components/incident-severity-rank'
import { IncidentDto } from '@/src/services/wayd-api'
import type { ReactNode } from 'react'
import styles from '@/src/app/product-management/incidents/_components/incidents.module.css'

export interface IncidentDetailHeaderProps {
  incident: IncidentDto
  productMeta: string | null
  headerActions?: ReactNode
}

const IncidentDetailHeader = ({
  incident,
  productMeta,
  headerActions,
}: IncidentDetailHeaderProps) => (
  <header className={styles.detailHeader}>
    <div className={styles.detailHeaderTop}>
      <div className={styles.detailHeaderMain}>
        <p className={styles.meta}>
          INC-{incident.key}
          {productMeta ? ` · ${productMeta}` : null}
        </p>
        <h1 className={styles.titleRow}>
          <IncidentSeverityRank
            name={incident.severityLevel.name}
            color={incident.severityLevel.color}
          />
          <span>{incident.title}</span>
        </h1>
        {incident.description ? (
          <p className={styles.desc}>{incident.description}</p>
        ) : null}
      </div>
      {headerActions ? (
        <div className={styles.detailHeaderActions}>{headerActions}</div>
      ) : null}
    </div>
  </header>
)

export default IncidentDetailHeader
