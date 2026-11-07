import { JobServerResponse } from '@/src/services/wayd-api'
import {
  findLongestRunningJobId,
  isRequeueableState,
  summarizeSchedulerHealth,
} from './job-health'

const now = new Date('2026-11-07T12:00:00Z')

// The generated client types instants as Date, but they arrive as ISO strings.
const server = (workerCount: number, heartbeat?: string): JobServerResponse =>
  ({
    name: `server-${workerCount}`,
    workerCount,
    queues: ['default'],
    heartbeat: heartbeat as unknown as Date,
  }) as JobServerResponse

describe('job-health', () => {
  describe('summarizeSchedulerHealth', () => {
    it('should report loading before the servers arrive', () => {
      // Arrange / Act
      const health = summarizeSchedulerHealth(undefined, now)

      // Assert
      expect(health.status).toBe('loading')
    })

    it('should report down when no servers are running', () => {
      // Arrange / Act
      const health = summarizeSchedulerHealth([], now)

      // Assert
      expect(health).toEqual({ status: 'down', serverCount: 0, workerCount: 0 })
    })

    it('should report healthy with totals and the newest heartbeat', () => {
      // Arrange
      const servers = [
        server(8, '2026-11-07T11:58:00Z'),
        server(4, '2026-11-07T11:59:30Z'),
      ]

      // Act
      const health = summarizeSchedulerHealth(servers, now)

      // Assert
      expect(health).toEqual({
        status: 'healthy',
        serverCount: 2,
        workerCount: 12,
        latestHeartbeat: '2026-11-07T11:59:30Z',
      })
    })

    it('should report stale when the newest heartbeat is over five minutes old', () => {
      // Arrange
      const servers = [
        server(8, '2026-11-07T11:50:00Z'),
        server(8, '2026-11-07T11:54:59Z'),
      ]

      // Act
      const health = summarizeSchedulerHealth(servers, now)

      // Assert
      expect(health.status).toBe('stale')
      expect(health.latestHeartbeat).toBe('2026-11-07T11:54:59Z')
    })

    it('should stay healthy at exactly five minutes since the heartbeat', () => {
      // Arrange
      const servers = [server(8, '2026-11-07T11:55:00Z')]

      // Act
      const health = summarizeSchedulerHealth(servers, now)

      // Assert
      expect(health.status).toBe('healthy')
    })

    it('should report stale when no server has a heartbeat', () => {
      // Arrange
      const servers = [server(8)]

      // Act
      const health = summarizeSchedulerHealth(servers, now)

      // Assert
      expect(health.status).toBe('stale')
    })
  })

  describe('isRequeueableState', () => {
    it.each([
      ['Processing', false],
      ['Failed', true],
      ['Scheduled', true],
      ['Succeeded', true],
      [undefined, false],
    ])('should treat %s as requeueable: %s', (state, expected) => {
      // Arrange / Act
      const result = isRequeueableState(state)

      // Assert
      expect(result).toBe(expected)
    })
  })

  describe('findLongestRunningJobId', () => {
    it('should return the job that started earliest', () => {
      // Arrange
      const jobs = [
        { id: 'a', timestamp: '2026-11-07T11:58:00Z' },
        { id: 'b', timestamp: '2026-11-07T11:20:00Z' },
        { id: 'c', timestamp: '2026-11-07T11:40:00Z' },
      ]

      // Act
      const result = findLongestRunningJobId(jobs)

      // Assert
      expect(result).toBe('b')
    })

    it('should return nothing for a single job', () => {
      // Arrange
      const jobs = [{ id: 'a', timestamp: '2026-11-07T11:58:00Z' }]

      // Act
      const result = findLongestRunningJobId(jobs)

      // Assert
      expect(result).toBeUndefined()
    })

    it('should ignore jobs without a start time', () => {
      // Arrange
      const jobs = [
        { id: 'a', timestamp: null },
        { id: 'b', timestamp: '2026-11-07T11:58:00Z' },
        { id: 'c', timestamp: '2026-11-07T11:59:00Z' },
      ]

      // Act
      const result = findLongestRunningJobId(jobs)

      // Assert
      expect(result).toBe('b')
    })
  })
})
