import { JobServerResponse } from '@/src/services/api/wayd.schemas'
import { type Instant, instantTime } from '@/src/utils/instant'

/**
 * How long a server may go without a heartbeat before it reads as unresponsive.
 * Matches Hangfire's default server timeout, after which the scheduler itself
 * treats the server as dead and removes it.
 */
export const STALE_HEARTBEAT_MS = 5 * 60 * 1000

export type SchedulerHealthStatus = 'loading' | 'healthy' | 'stale' | 'down'

export interface SchedulerHealth {
  status: SchedulerHealthStatus
  serverCount: number
  workerCount: number
  /** The most recent heartbeat across all servers. */
  latestHeartbeat?: Instant
}

/**
 * Whether the scheduler can run work: no servers means nothing runs, and a
 * newest heartbeat older than {@link STALE_HEARTBEAT_MS} means every server has
 * stopped reporting.
 */
export const summarizeSchedulerHealth = (
  servers: JobServerResponse[] | undefined,
  now: Date = new Date(),
): SchedulerHealth => {
  if (!servers) {
    return { status: 'loading', serverCount: 0, workerCount: 0 }
  }

  const workerCount = servers.reduce((sum, s) => sum + (s.workerCount ?? 0), 0)

  const latestHeartbeat = servers
    .map((s) => s.heartbeat)
    .filter((h): h is Instant => h != null)
    .reduce<Instant | undefined>(
      (latest, h) =>
        !latest || instantTime(h) > instantTime(latest) ? h : latest,
      undefined,
    )

  if (servers.length === 0) {
    return { status: 'down', serverCount: 0, workerCount: 0 }
  }

  const isStale =
    !latestHeartbeat ||
    now.getTime() - instantTime(latestHeartbeat) > STALE_HEARTBEAT_MS

  return {
    status: isStale ? 'stale' : 'healthy',
    serverCount: servers.length,
    workerCount,
    latestHeartbeat,
  }
}

/**
 * Whether a job in this state can be requeued. A running job cannot: the
 * requeue would start a second copy while the first keeps going, and the API
 * refuses it. Takes the state name the API reports ("Processing").
 */
export const isRequeueableState = (state: string | undefined): boolean =>
  state !== undefined && state !== 'Processing'

/**
 * The id of the job that started earliest, or undefined when fewer than two
 * jobs have a start time — with one job, "longest running" says nothing.
 */
export const findLongestRunningJobId = (
  jobs: { id: string; timestamp?: Date | string | null }[] | undefined,
): string | undefined => {
  const started = (jobs ?? []).filter((j) => j.timestamp != null)
  if (started.length < 2) return undefined

  return started.reduce((earliest, job) =>
    new Date(job.timestamp!) < new Date(earliest.timestamp!) ? job : earliest,
  ).id
}
