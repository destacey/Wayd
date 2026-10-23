import { isHealthCheckSpan } from './health-check'

describe('isHealthCheckSpan', () => {
  it('matches the health route from the target Next sets when the span starts', () => {
    // Arrange
    const spanName = 'GET'
    const attributes = { 'http.target': '/api/health' }

    // Act
    const ignored = isHealthCheckSpan(spanName, attributes)

    // Assert
    expect(ignored).toBe(true)
  })

  it('matches a health target that carries a query', () => {
    // Arrange
    const attributes = { 'http.target': '/api/health?ready=1' }

    // Act
    const ignored = isHealthCheckSpan('GET', attributes)

    // Assert
    expect(ignored).toBe(true)
  })

  it('matches the route once Next has renamed the span', () => {
    // Arrange
    const spanName = 'GET /api/health'

    // Act
    const ignored = isHealthCheckSpan(spanName, undefined)

    // Assert
    expect(ignored).toBe(true)
  })

  it('does not treat a longer path that starts with the probe as the probe', () => {
    // Arrange
    const attributes = { 'http.target': '/api/health-check' }

    // Act
    const ignored = isHealthCheckSpan('GET', attributes)

    // Assert
    expect(ignored).toBe(false)
  })

  it('leaves a page request that is not the probe', () => {
    // Arrange
    const attributes = {
      'http.target': '/organizations/team-of-teams/1',
      'http.route': '/organizations/team-of-teams/[key]',
    }

    // Act
    const ignored = isHealthCheckSpan('GET', attributes)

    // Assert
    expect(ignored).toBe(false)
  })
})
