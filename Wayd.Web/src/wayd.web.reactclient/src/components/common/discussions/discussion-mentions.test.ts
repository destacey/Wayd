import {
  fromComposerText,
  toComposerText,
  toDisplayMarkdown,
} from './discussion-mentions'

const dana = {
  id: '01a18a77-484d-790c-ba91-93acff2826c7',
  key: 42,
  name: 'Dana Ortiz',
}
const ann = { id: '01a18a77-484d-790c-ba91-93acff2826c8', name: 'Ann' }
const annLee = { id: '01a18a77-484d-790c-ba91-93acff2826c9', name: 'Ann Lee' }

describe('toDisplayMarkdown', () => {
  it('links a mention to the person under their current name', () => {
    // Arrange
    const body = `Over to @[Dana O](employee:${dana.id}).`

    // Act
    const markdown = toDisplayMarkdown(body, [dana])

    // Assert
    expect(markdown).toBe('Over to [@Dana Ortiz](/organizations/employees/42).')
  })

  it('leaves a mention the server did not name as plain text', () => {
    // Arrange
    const body = `Over to @[Pat Out](employee:${ann.id}).`

    // Act
    const markdown = toDisplayMarkdown(body, [dana])

    // Assert
    expect(markdown).toBe('Over to @Pat Out.')
  })

  it('escapes brackets in a name so the link stays a link', () => {
    // Arrange
    const odd = { ...dana, name: 'Dana [Finance]' }

    // Act
    const markdown = toDisplayMarkdown(`Hi @[Dana](employee:${dana.id})`, [odd])

    // Assert
    expect(markdown).toBe(
      'Hi [@Dana \\[Finance\\]](/organizations/employees/42)',
    )
  })
})

describe('toComposerText', () => {
  it('reads each mention as @Name and names who it mentions', () => {
    // Arrange
    const body = `Ask @[Dana O](employee:${dana.id}) twice: @[Dana O](employee:${dana.id})`

    // Act
    const { text, mentioned } = toComposerText(body, [dana])

    // Assert
    expect(text).toBe('Ask @Dana Ortiz twice: @Dana Ortiz')
    expect(mentioned).toEqual([{ id: dana.id, name: 'Dana Ortiz' }])
  })
})

describe('fromComposerText', () => {
  it('writes each picked person as a stored mention', () => {
    // Act
    const body = fromComposerText('Over to @Dana Ortiz, thanks', [dana])

    // Assert
    expect(body).toBe(`Over to @[Dana Ortiz](employee:${dana.id}), thanks`)
  })

  it('matches the longer of two names that share a start', () => {
    // Act
    const body = fromComposerText('@Ann Lee and @Ann', [ann, annLee])

    // Assert
    expect(body).toBe(
      `@[Ann Lee](employee:${annLee.id}) and @[Ann](employee:${ann.id})`,
    )
  })

  it('leaves a name that was typed but never picked as text', () => {
    // Act
    const body = fromComposerText('Ask @Annette', [ann])

    // Assert
    expect(body).toBe('Ask @Annette')
  })

  it('round-trips a stored message through the composer', () => {
    // Arrange
    const stored = `Ask @[Dana Ortiz](employee:${dana.id}) please`

    // Act
    const { text, mentioned } = toComposerText(stored, [dana])

    // Assert
    expect(fromComposerText(text, mentioned)).toBe(stored)
  })
})
