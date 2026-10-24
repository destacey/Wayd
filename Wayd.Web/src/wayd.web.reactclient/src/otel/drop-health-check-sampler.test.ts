import { ROOT_CONTEXT, SpanKind } from '@opentelemetry/api'
import { AlwaysOnSampler, SamplingDecision } from '@opentelemetry/sdk-trace'
import { DropHealthCheckSampler } from './drop-health-check-sampler'

describe('DropHealthCheckSampler', () => {
  const sampler = new DropHealthCheckSampler(new AlwaysOnSampler())

  it('does not record the health probe', () => {
    // Arrange
    const attributes = { 'http.target': '/api/health' }

    // Act
    const result = sampler.shouldSample(
      ROOT_CONTEXT,
      'trace',
      'GET',
      SpanKind.INTERNAL,
      attributes,
      [],
    )

    // Assert
    expect(result.decision).toBe(SamplingDecision.NOT_RECORD)
  })

  it('records a page request', () => {
    // Arrange
    const attributes = { 'http.target': '/organizations/team-of-teams/1' }

    // Act
    const result = sampler.shouldSample(
      ROOT_CONTEXT,
      'trace',
      'GET',
      SpanKind.INTERNAL,
      attributes,
      [],
    )

    // Assert
    expect(result.decision).toBe(SamplingDecision.RECORD_AND_SAMPLED)
  })
})
