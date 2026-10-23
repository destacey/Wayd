import type { Attributes, Context, Link, SpanKind } from '@opentelemetry/api'
import {
  SamplingDecision,
  type Sampler,
  type SamplingResult,
} from '@opentelemetry/sdk-trace-base'
import { isHealthCheckSpan } from './health-check'

export class DropHealthCheckSampler implements Sampler {
  constructor(private readonly inner: Sampler) {}

  shouldSample(
    parentContext: Context,
    traceId: string,
    spanName: string,
    spanKind: SpanKind,
    attributes: Attributes,
    links: Link[],
  ): SamplingResult {
    if (isHealthCheckSpan(spanName, attributes)) {
      return { decision: SamplingDecision.NOT_RECORD }
    }
    return this.inner.shouldSample(
      parentContext,
      traceId,
      spanName,
      spanKind,
      attributes,
      links,
    )
  }

  toString(): string {
    return `DropHealthCheckSampler{${this.inner.toString()}}`
  }
}
