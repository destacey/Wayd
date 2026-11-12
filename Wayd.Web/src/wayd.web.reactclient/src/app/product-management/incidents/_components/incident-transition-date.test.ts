import { IncidentWorkflowAction } from '@/src/services/wayd-api'
import { incidentTransitionDate } from './incident-transition-date'

const at = new Date('2026-11-12T16:00:00Z')

describe('incidentTransitionDate', () => {
  it('asks for response start when begin responding would write it', () => {
    // Arrange
    const open = { startedAt: null, recoveredAt: null }

    // Act
    const field = incidentTransitionDate(
      IncidentWorkflowAction.BeginResponding,
      open,
    )

    // Assert
    expect(field).toBe('startedAt')
  })

  it('asks for nothing when begin responding would leave the start alone', () => {
    // Arrange
    const responding = { startedAt: at, recoveredAt: null }

    // Act
    const field = incidentTransitionDate(
      IncidentWorkflowAction.BeginResponding,
      responding,
    )

    // Assert
    expect(field).toBeNull()
  })

  it('uses one instant for response start and recovery when both are empty', () => {
    // Arrange
    const open = { startedAt: null, recoveredAt: null }

    // Act
    const field = incidentTransitionDate(
      IncidentWorkflowAction.MarkRecovered,
      open,
    )

    // Assert
    expect(field).toBe('both')
  })

  it('asks only for the date mark recovered would fill', () => {
    // Arrange
    const started = { startedAt: at, recoveredAt: null }
    const recoveredOnly = { startedAt: null, recoveredAt: at }
    const both = { startedAt: at, recoveredAt: at }

    // Act / Assert
    expect(
      incidentTransitionDate(IncidentWorkflowAction.MarkRecovered, started),
    ).toBe('recoveredAt')
    expect(
      incidentTransitionDate(
        IncidentWorkflowAction.MarkRecovered,
        recoveredOnly,
      ),
    ).toBe('startedAt')
    expect(
      incidentTransitionDate(IncidentWorkflowAction.MarkRecovered, both),
    ).toBeNull()
  })

  it('asks for nothing when the action writes no date', () => {
    // Arrange
    const open = { startedAt: null, recoveredAt: null }

    // Act / Assert
    expect(
      incidentTransitionDate(IncidentWorkflowAction.Close, open),
    ).toBeNull()
    expect(
      incidentTransitionDate(IncidentWorkflowAction.Cancel, open),
    ).toBeNull()
  })
})
