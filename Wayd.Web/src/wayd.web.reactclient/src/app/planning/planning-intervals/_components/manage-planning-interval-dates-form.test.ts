import { toFormErrors } from '@/src/utils'
import { fieldForError } from './manage-planning-interval-dates-form'

describe('fieldForError', () => {
  it('shows the PI start and end errors on its date range', () => {
    // Arrange
    const errors = {
      Start: ['Start is required'],
      End: ['End date must be greater than or equal to start date'],
    }

    // Act
    const fields = toFormErrors(errors, { fieldFor: fieldForError })

    // Assert
    expect(fields).toEqual([
      {
        name: 'dates',
        errors: [
          'Start is required',
          'End date must be greater than or equal to start date',
        ],
      },
    ])
  })

  it("shows an iteration's date errors on that row's date range", () => {
    // Arrange
    const errors = { 'Iterations[2].End': ['End is required'] }

    // Act
    const fields = toFormErrors(errors, { fieldFor: fieldForError })

    // Assert
    expect(fields).toEqual([
      { name: ['iterations', 2, 'dates'], errors: ['End is required'] },
    ])
  })

  it('leaves other errors to the default field names', () => {
    // Arrange / Act / Assert
    expect(fieldForError('iterations[0].name')).toBeUndefined()
  })
})
