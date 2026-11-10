import { fireEvent, render, screen } from '@testing-library/react'
import {
  JobServerResponse,
  JobStateFilter,
  JobStatisticsResponse,
} from '@/src/services/wayd-api'
import JobHealthSummary from './job-health-summary'

const statistics = {
  current: {
    processing: 3,
    enqueued: 12,
    scheduled: 5,
    failed: 7,
    succeeded: 40,
    deleted: 2,
    retries: 2,
    recurring: 9,
    servers: 2,
  },
  allTime: { succeeded: 48210, deleted: 312 },
} as unknown as JobStatisticsResponse

// The generated client types instants as Date, but they arrive as ISO strings.
const server = (workerCount: number, heartbeatMsAgo: number) =>
  ({
    name: `server-${workerCount}`,
    workerCount,
    queues: ['default'],
    heartbeat: new Date(
      Date.now() - heartbeatMsAgo,
    ).toISOString() as unknown as Date,
  }) as JobServerResponse

const renderSummary = (servers: JobServerResponse[] | undefined) => {
  const onSelectState = vi.fn()
  render(
    <JobHealthSummary
      statistics={statistics}
      servers={servers}
      isLoading={false}
      onSelectState={onSelectState}
    />,
  )
  return onSelectState
}

describe('JobHealthSummary', () => {
  it.each([
    ['Processing', JobStateFilter.Processing],
    ['Enqueued', JobStateFilter.Enqueued],
    ['Scheduled', JobStateFilter.Scheduled],
    ['Failed', JobStateFilter.Failed],
  ])('should open the %s jobs from its count', (label, expected) => {
    // Arrange
    const onSelectState = renderSummary([server(8, 0)])

    // Act
    fireEvent.click(screen.getByRole('button', { name: new RegExp(label) }))

    // Assert
    expect(onSelectState).toHaveBeenCalledTimes(1)
    expect(onSelectState).toHaveBeenCalledWith(expected)
  })

  it('should name each count with its qualifier', () => {
    // Arrange / Act
    renderSummary([server(8, 0), server(8, 0)])

    // Assert
    expect(
      screen.getByRole('button', { name: 'Processing 3 of 16 workers' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Scheduled 5 · 2 retrying' }),
    ).toBeInTheDocument()
  })

  it('should report a healthy scheduler with its servers and workers', () => {
    // Arrange / Act
    renderSummary([server(8, 0), server(4, 0)])

    // Assert
    expect(screen.getByRole('status')).toHaveTextContent('Scheduler healthy')
    expect(
      screen.getByText(/2 servers · 12 workers · heartbeat just now/),
    ).toBeInTheDocument()
  })

  it('should report unresponsive servers after five minutes without a heartbeat', () => {
    // Arrange / Act
    renderSummary([server(8, 6 * 60 * 1000)])

    // Assert
    expect(screen.getByRole('status')).toHaveTextContent(
      'Servers not responding',
    )
    expect(screen.getByText('Last heartbeat 6 minutes ago')).toBeInTheDocument()
  })

  it('should report that nothing will run when there are no servers', () => {
    // Arrange / Act
    renderSummary([])

    // Assert
    expect(screen.getByRole('status')).toHaveTextContent('No servers running')
    expect(
      screen.getByText('Nothing will run until a server starts.'),
    ).toBeInTheDocument()
  })

  it('should show the all-time totals', () => {
    // Arrange / Act
    renderSummary([server(8, 0)])

    // Assert
    expect(
      screen.getByText('48,210 succeeded · 312 deleted', { exact: false }),
    ).toBeInTheDocument()
  })
})
