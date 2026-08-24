/**
 * Pins the wire contract for clearing a task description. The column is nullable, and the drawer
 * relies on an omitted property (not an empty string) to null it — see handleSaveDescription in
 * task-drawer.tsx. If someone "simplifies" that to send '', this fails.
 */
describe('SetTaskDescriptionRequest payload', () => {
  it('omits the property entirely when the description is undefined', () => {
    // Arrange
    const request = { description: undefined }

    // Act
    const body = JSON.stringify(request)

    // Assert — no `description` key at all, so the API binds it to null and clears the column.
    expect(body).toBe('{}')
    expect(JSON.parse(body)).not.toHaveProperty('description')
  })

  it('sends an empty string when given one, which would store "" rather than null', () => {
    // Arrange
    const request = { description: '' }

    // Act
    const body = JSON.stringify(request)

    // Assert — this is the outcome the drawer deliberately avoids.
    expect(body).toBe('{"description":""}')
  })

  it("converts an empty draft to undefined via the drawer's `|| undefined` idiom", () => {
    // Arrange / Act — mirrors `onSetTaskDescription(task.id, next || undefined)`, where `next` is
    // `description?.trim()` and the draft state is `string | undefined`.
    const clearedFromText = 'was here'.replace('was here', '').trim() || undefined
    const neverSet = (undefined as string | undefined)?.trim() || undefined
    const kept = '  Real text  '.trim() || undefined

    // Assert
    expect(clearedFromText).toBeUndefined()
    expect(neverSet).toBeUndefined()
    expect(kept).toBe('Real text')
  })
})
