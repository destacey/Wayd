import {
  ProjectDeliverableStatusAlias,
  StatusCategory,
} from '@/src/services/wayd-api'
import { outcomeDeliverableStatusLabel } from './outcome-deliverable-status'

describe('outcomeDeliverableStatusLabel', () => {
  it.each([
    [
      ProjectDeliverableStatusAlias.NotStarted,
      StatusCategory.Proposed,
      'Not Started',
    ],
    [
      ProjectDeliverableStatusAlias.InProgress,
      StatusCategory.Active,
      'In Progress',
    ],
    [ProjectDeliverableStatusAlias.Accepted, StatusCategory.Done, 'Accepted'],
    [
      ProjectDeliverableStatusAlias.Descoped,
      StatusCategory.Removed,
      'Descoped',
    ],
  ])('names a status by its meaning (%s)', (alias, category, expected) => {
    // Arrange / Act
    const label = outcomeDeliverableStatusLabel(alias, category)

    // Assert
    expect(label).toBe(expected)
  })

  it('names a status with no meaning by its category', () => {
    // Arrange / Act
    const label = outcomeDeliverableStatusLabel(
      ProjectDeliverableStatusAlias.None,
      StatusCategory.Active,
    )

    // Assert
    expect(label).toBe('In progress')
  })
})
