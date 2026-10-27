import { deskPhrase } from './desk-choice'

describe('deskPhrase', () => {
  it.each([
    ['IT', 'the IT desk'],
    ['Product delivery desk', 'the Product delivery desk'],
    ['Security Desk', 'the Security Desk'],
  ])('names %s as %s', (name, expected) => {
    // Act
    const phrase = deskPhrase(name)

    // Assert
    expect(phrase).toBe(expected)
  })
})
