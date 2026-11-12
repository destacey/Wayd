import { incidentStatusSteps } from './incident-status-steps'

const status = (
  id: string,
  name: string,
  order: number,
  isRetired = false,
) => ({ id, name, order, isRetired })

// Workflow order, not alias number. Canceled is last here because that is where
// the seeded incident workflow places it, after Closed.
const workflow = [
  status('canceled', 'Canceled', 5),
  status('open', 'Open', 1),
  status('closed', 'Closed', 4),
  status('responding', 'Responding', 2),
  status('resolved', 'Resolved', 3),
  status('retired', 'Old', 0, true),
]

describe('incidentStatusSteps', () => {
  it('follows workflow order and marks only the current status', () => {
    // Act
    const steps = incidentStatusSteps(workflow, 'responding')

    // Assert
    expect(steps.map((step) => step.title)).toEqual([
      'Open',
      'Responding',
      'Resolved',
      'Closed',
      'Canceled',
    ])
    expect(steps.map((step) => step.status)).toEqual([
      'wait',
      'process',
      'wait',
      'wait',
      'wait',
    ])
  })

  it('keeps Canceled in workflow order and marks it when it is current', () => {
    // Act
    const steps = incidentStatusSteps(workflow, 'canceled')

    // Assert
    expect(steps.map((step) => step.title)).toEqual([
      'Open',
      'Responding',
      'Resolved',
      'Closed',
      'Canceled',
    ])
    expect(steps.find((step) => step.title === 'Canceled')?.status).toBe(
      'process',
    )
    expect(steps.filter((step) => step.status === 'process')).toHaveLength(1)
  })

  it('keeps a retired status when the incident still holds it', () => {
    // Act
    const steps = incidentStatusSteps(workflow, 'retired')

    // Assert
    expect(steps.map((step) => step.title)).toEqual([
      'Old',
      'Open',
      'Responding',
      'Resolved',
      'Closed',
      'Canceled',
    ])
    expect(steps[0]).toEqual({ title: 'Old', status: 'process' })
  })
})
