import {
  BULK_JOB_CONCURRENCY,
  settleJobsInBatches,
} from './background-jobs-api'

describe('settleJobsInBatches', () => {
  it('should act on every job and count the failures', async () => {
    // Arrange
    const jobIds = ['1', '2', '3', '4', '5']
    const acted: string[] = []
    const action = async (jobId: string) => {
      acted.push(jobId)
      if (jobId === '2' || jobId === '4') throw new Error('changed state')
    }

    // Act
    const outcome = await settleJobsInBatches(jobIds, action)

    // Assert — one failure must not stop the jobs after it.
    expect(outcome).toEqual({ total: 5, failed: 2 })
    expect(acted).toEqual(jobIds)
  })

  it('should keep at most the batch size of requests in flight', async () => {
    // Arrange
    const jobIds = Array.from(
      { length: BULK_JOB_CONCURRENCY * 2 + 3 },
      (_, i) => String(i),
    )
    let inFlight = 0
    let peak = 0
    const action = async () => {
      inFlight++
      peak = Math.max(peak, inFlight)
      await Promise.resolve()
      inFlight--
    }

    // Act
    const outcome = await settleJobsInBatches(jobIds, action)

    // Assert
    expect(peak).toBe(BULK_JOB_CONCURRENCY)
    expect(outcome).toEqual({ total: jobIds.length, failed: 0 })
  })
})
