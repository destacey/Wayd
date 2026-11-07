import { describeBulkOutcome } from './use-background-job-actions'

describe('describeBulkOutcome', () => {
  it('should report success when every job was acted on', () => {
    // Arrange / Act
    const result = describeBulkOutcome('Requeued', { total: 5, failed: 0 })

    // Assert
    expect(result).toEqual({ ok: true, text: 'Requeued 5 jobs.' })
  })

  it('should use the singular for one job', () => {
    // Arrange / Act
    const result = describeBulkOutcome('Deleted', { total: 1, failed: 0 })

    // Assert
    expect(result).toEqual({ ok: true, text: 'Deleted 1 job.' })
  })

  it('should report how many of the batch failed', () => {
    // Arrange / Act
    const result = describeBulkOutcome('Requeued', { total: 5, failed: 2 })

    // Assert
    expect(result.ok).toBe(false)
    expect(result.text).toBe(
      'Requeued 3 of 5 jobs; 2 failed. They may have changed state — refresh and try again.',
    )
  })
})
