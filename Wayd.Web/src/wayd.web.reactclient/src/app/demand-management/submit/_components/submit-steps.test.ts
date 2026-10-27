import { stepWithError } from './submit-steps'

describe('stepWithError', () => {
  it.each([
    [['title'], 0],
    [[['needBy', 'basis']], 1],
    [[['needBy', 'reference'], 'channelId'], 0],
    [['somethingElse'], undefined],
  ] as const)('puts %j on step %s', (names, expected) => {
    // Act
    const step = stepWithError([...names] as any)

    // Assert
    expect(step).toBe(expected)
  })
})
