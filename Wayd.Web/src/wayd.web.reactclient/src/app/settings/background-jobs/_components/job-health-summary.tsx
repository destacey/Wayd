'use client'

import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  ExclamationCircleOutlined,
  InfoCircleOutlined,
  LoadingOutlined,
} from '@ant-design/icons'
import { Skeleton } from 'antd'
import { FC, ReactNode } from 'react'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import {
  JobServerResponse,
  JobStateFilter,
  JobStatisticsResponse,
} from '@/src/services/wayd-api'
import { formatTimeAgo } from '@/src/utils/time-ago'
import { SchedulerHealth, summarizeSchedulerHealth } from './job-health'
import styles from './job-health-summary.module.css'

export interface JobHealthSummaryProps {
  statistics: JobStatisticsResponse | undefined
  servers: JobServerResponse[] | undefined
  isLoading: boolean
  /** Opens the Jobs tab on a state, from its count. */
  onSelectState: (state: JobStateFilter) => void
}

const plural = (count: number, noun: string) =>
  `${count.toLocaleString()} ${noun}${count === 1 ? '' : 's'}`

const healthContent = (
  health: SchedulerHealth,
): { icon: ReactNode; title: string; detail: string } => {
  switch (health.status) {
    case 'down':
      return {
        icon: <CloseCircleOutlined />,
        title: 'No servers running',
        detail: 'Nothing will run until a server starts.',
      }
    case 'stale':
      return {
        icon: <ExclamationCircleOutlined />,
        title: 'Servers not responding',
        detail: health.latestHeartbeat
          ? `Last heartbeat ${formatTimeAgo(health.latestHeartbeat)}`
          : 'No heartbeat recorded',
      }
    case 'healthy':
      return {
        icon: <CheckCircleOutlined />,
        title: 'Scheduler healthy',
        detail: `${plural(health.serverCount, 'server')} · ${plural(health.workerCount, 'worker')} · heartbeat ${formatTimeAgo(health.latestHeartbeat!)}`,
      }
    default:
      return {
        icon: <LoadingOutlined />,
        title: 'Checking servers…',
        detail: '',
      }
  }
}

interface CountCellProps {
  label: string
  value: number
  tooltip: string
  onClick: () => void
  className?: string
  children?: ReactNode
}

const CountCell: FC<CountCellProps> = ({
  label,
  value,
  tooltip,
  onClick,
  className,
  children,
}) => (
  <WaydTooltip title={tooltip}>
    <button
      type="button"
      className={[styles.cell, className].filter(Boolean).join(' ')}
      onClick={onClick}
    >
      <span className={styles.label}>{label}</span>
      <span className={styles.value}>
        {value.toLocaleString()}
        {children}
      </span>
    </button>
  </WaydTooltip>
)

/**
 * One strip answering "is the scheduler working, and is anything stuck or
 * failing": server health, the live counts (each opening its job list), and
 * the all-time totals as a footnote.
 */
const JobHealthSummary: FC<JobHealthSummaryProps> = ({
  statistics,
  servers,
  isLoading,
  onSelectState,
}) => {
  const health = summarizeSchedulerHealth(servers)
  const { icon, title, detail } = healthContent(health)
  const current = statistics?.current
  const allTime = statistics?.allTime

  if (isLoading && !statistics) {
    return (
      <div className={styles.strip}>
        <div className={styles.cell}>
          <Skeleton active paragraph={false} />
        </div>
      </div>
    )
  }

  return (
    <section className={styles.strip} aria-label="Scheduler summary">
      <div
        className={`${styles.cell} ${styles.health} ${health.status === 'loading' ? '' : styles[health.status]}`}
      >
        <span className={styles.healthIcon} aria-hidden="true">
          {icon}
        </span>
        <span>
          {/* Only the title is live: the detail's "heartbeat 2 minutes ago" changes on every poll. */}
          <span className={styles.healthTitle} role="status">
            {title}
          </span>
          {detail && <span className={styles.label}>{detail}</span>}
        </span>
      </div>
      <CountCell
        label="Processing"
        value={current?.processing ?? 0}
        tooltip="Jobs a worker is executing right now."
        onClick={() => onSelectState(JobStateFilter.Processing)}
      >
        {health.workerCount > 0 && (
          <span className={styles.qualifier}>
            {' '}
            of {plural(health.workerCount, 'worker')}
          </span>
        )}
      </CountCell>
      <CountCell
        label="Enqueued"
        value={current?.enqueued ?? 0}
        tooltip="Jobs waiting for a free worker."
        onClick={() => onSelectState(JobStateFilter.Enqueued)}
      />
      <CountCell
        label="Scheduled"
        value={current?.scheduled ?? 0}
        tooltip="Jobs queued to run at a future time, including failed jobs waiting out a retry delay."
        onClick={() => onSelectState(JobStateFilter.Scheduled)}
      >
        {!!current?.retries && (
          <span className={styles.retrying}>
            {' '}
            · {current.retries.toLocaleString()} retrying
          </span>
        )}
      </CountCell>
      <CountCell
        label="Failed"
        value={current?.failed ?? 0}
        tooltip="Jobs that exhausted every retry. These persist until requeued or deleted."
        onClick={() => onSelectState(JobStateFilter.Failed)}
        className={current?.failed ? styles.failed : undefined}
      />
      <div className={`${styles.cell} ${styles.allTime}`}>
        <span className={styles.label}>
          All time{' '}
          <WaydTooltip title="Running totals kept by the scheduler's counters. They keep climbing after the job records themselves are purged.">
            <InfoCircleOutlined
              tabIndex={0}
              aria-label="About the all-time totals"
            />
          </WaydTooltip>
        </span>
        <span>
          {(allTime?.succeeded ?? 0).toLocaleString()} succeeded ·{' '}
          {(allTime?.deleted ?? 0).toLocaleString()} deleted
        </span>
      </div>
    </section>
  )
}

export default JobHealthSummary
