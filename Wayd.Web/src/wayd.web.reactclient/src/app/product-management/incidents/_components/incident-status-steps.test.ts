import { ProductStatusAlias } from '@/src/services/wayd-api'
import { incidentStatusSteps } from './incident-status-steps'

const status = (
  id: string,
  name: string,
  order: number,
  aliasName: string | null,
  isRetired = false,
) => ({ id, name, order, isRetired, aliasName })

// Workflow order, not alias number. Canceled is last here because that is where
// the seeded incident workflow places it, after Closed.
const workflow = [
  status('canceled', 'Canceled', 5, ProductStatusAlias.Canceled),
  status('open', 'Open', 1, ProductStatusAlias.Open),
  status('closed', 'Closed', 4, ProductStatusAlias.Closed),
  status('responding', 'Responding', 2, ProductStatusAlias.Responding),
  status('resolved', 'Resolved', 3, ProductStatusAlias.Resolved),
  status('retired', 'Old', 0, null, true),
]

const noticedAt = '2026-11-12T16:00:00Z'
const startedAt = '2026-11-12T18:30:00Z'
const recoveredAt = '2026-11-12T20:15:00Z'

describe('incidentStatusSteps', () => {
  it('marks earlier statuses complete and keeps each stored time on its status', () => {
    // Arrange
    const instants = { noticedAt, startedAt }

    // Act
    const steps = incidentStatusSteps(workflow, 'responding', instants)

    // Assert
    expect(steps.map((step) => step.title)).toEqual([
      'Open',
      'Responding',
      'Resolved',
      'Closed',
      'Canceled',
    ])
    expect(steps.map((step) => step.status)).toEqual([
      'finish',
      'process',
      'wait',
      'wait',
      'wait',
    ])
    expect(steps.map((step) => step.at)).toEqual([
      noticedAt,
      startedAt,
      null,
      null,
      null,
    ])
  })

  it('puts recovery on resolved and leaves closed without a time', () => {
    // Act
    const steps = incidentStatusSteps(workflow, 'closed', {
      noticedAt,
      startedAt,
      recoveredAt,
    })

    // Assert
    expect(steps.find((step) => step.title === 'Resolved')).toEqual({
      title: 'Resolved',
      status: 'finish',
      at: recoveredAt,
    })
    expect(steps.find((step) => step.title === 'Closed')).toEqual({
      title: 'Closed',
      status: 'process',
      at: null,
    })
  })

  it('marks cancel as canceled and only the statuses that stored a time', () => {
    // Act
    const steps = incidentStatusSteps(workflow, 'canceled', { noticedAt })

    // Assert
    expect(steps.map((step) => step.title)).toEqual([
      'Open',
      'Responding',
      'Resolved',
      'Closed',
      'Canceled',
    ])
    expect(steps.map((step) => step.status)).toEqual([
      'finish',
      'wait',
      'wait',
      'wait',
      'error',
    ])
    expect(steps.find((step) => step.title === 'Canceled')?.at).toBeNull()
    expect(steps.find((step) => step.title === 'Responding')?.at).toBeNull()
  })

  it('counts a response and a recovery stored before cancel', () => {
    // Act
    const steps = incidentStatusSteps(workflow, 'canceled', {
      noticedAt,
      startedAt,
      recoveredAt,
    })

    // Assert
    expect(steps.map((step) => step.status)).toEqual([
      'finish',
      'finish',
      'finish',
      'wait',
      'error',
    ])
    expect(steps.map((step) => step.at)).toEqual([
      noticedAt,
      startedAt,
      recoveredAt,
      null,
      null,
    ])
  })

  it('marks a status with no alias complete once the incident has moved past it', () => {
    // Arrange
    const statuses = [
      status('triage', 'Triage', 1, null),
      status('open', 'Open', 2, ProductStatusAlias.Open),
    ]

    // Act
    const steps = incidentStatusSteps(statuses, 'open', { noticedAt })

    // Assert
    expect(steps.map((step) => step.status)).toEqual(['finish', 'process'])
    expect(steps[0].at).toBeNull()
    expect(steps[1].at).toBe(noticedAt)
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
    expect(steps[0]).toEqual({ title: 'Old', status: 'process', at: null })
    expect(steps.filter((step) => step.status === 'process')).toHaveLength(1)
  })
})
