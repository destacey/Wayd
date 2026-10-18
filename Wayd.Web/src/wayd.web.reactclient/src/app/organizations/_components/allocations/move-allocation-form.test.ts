import { fieldForError } from './move-allocation-form'

describe('fieldForError', () => {
  it('shows a source team or role error on the From field', () => {
    // Arrange / Act / Assert
    expect(fieldForError('fromTeamId')).toBe('from')
    expect(fieldForError('fromRoleId')).toBe('from')
  })

  it('leaves other errors to the default field names', () => {
    // Arrange / Act / Assert
    expect(fieldForError('toTeamId')).toBeUndefined()
  })
})
