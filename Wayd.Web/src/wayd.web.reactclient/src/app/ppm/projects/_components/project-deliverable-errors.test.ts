import { deliverableErrorMessage } from './project-deliverable-errors'

describe('deliverableErrorMessage', () => {
  it('says to reload when another change to the project saved first', () => {
    // Arrange
    const error = { status: 409, detail: 'Revision mismatch.' }

    // Act
    const message = deliverableErrorMessage(error, 'Failed.')

    // Assert
    expect(message).toBe(
      'The project changed since you opened it. Reload it and try again.',
    )
  })

  it('says the deliverable is gone when it was not found', () => {
    // Arrange
    const error = { status: 404, detail: 'Not found.' }

    // Act
    const message = deliverableErrorMessage(error, 'Failed.')

    // Assert
    expect(message).toBe(
      'This deliverable no longer exists. Reload the project.',
    )
  })

  it("repeats the server's detail for any other failure", () => {
    // Arrange
    const error = { status: 400, detail: 'The project is closed.' }

    // Act
    const message = deliverableErrorMessage(error, 'Failed.')

    // Assert
    expect(message).toBe('The project is closed.')
  })

  it('falls back when the server gave no detail', () => {
    // Arrange
    const error = { status: 500 }

    // Act
    const message = deliverableErrorMessage(error, 'Failed.')

    // Assert
    expect(message).toBe('Failed.')
  })
})
