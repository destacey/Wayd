// OpenTelemetry initialization for the Node.js server.
// Imported only when OTEL_EXPORTER_OTLP_ENDPOINT is set.
//
// The exporter is chosen from OTEL_EXPORTER_OTLP_PROTOCOL. Aspire injects grpc for its dashboard.
// Datadog's OTLP intake is http/protobuf, and a gRPC exporter pointed at that URL exports nothing.

import http from 'node:http'
import { context, propagation } from '@opentelemetry/api'
import { getNodeAutoInstrumentations } from '@opentelemetry/auto-instrumentations-node'
import { NodeSDK } from '@opentelemetry/sdk-node'
import {
  AlwaysOnSampler,
  ParentBasedSampler,
} from '@opentelemetry/sdk-trace'
import { DropHealthCheckSampler } from './otel/drop-health-check-sampler'

const otlpEndpoint = process.env.OTEL_EXPORTER_OTLP_ENDPOINT
console.log(
  `Initializing OpenTelemetry with endpoint: ${otlpEndpoint} protocol: ${process.env.OTEL_EXPORTER_OTLP_PROTOCOL ?? 'http/protobuf'}`,
)

// Configure metric export intervals via environment variables.
// The SDK rejects an interval that is not longer than the timeout.
process.env.OTEL_METRIC_EXPORT_INTERVAL = '5000'
process.env.OTEL_METRIC_EXPORT_TIMEOUT = '3000'

// Node commits response headers through writeHead, including the implicit call
// from the first write. The active span then is the request being served, so
// the browser can continue that trace on the API calls it makes afterwards.
// The two overloads cannot be implemented as one function, so the assignment
// goes through a signature that covers both.
type WriteHead = (
  this: http.ServerResponse,
  statusCode: number,
  statusMessage?: string | http.OutgoingHttpHeaders | http.OutgoingHttpHeader[],
  headers?: http.OutgoingHttpHeaders | http.OutgoingHttpHeader[],
) => http.ServerResponse

const originalWriteHead = http.ServerResponse.prototype
  .writeHead as unknown as WriteHead
http.ServerResponse.prototype.writeHead = function writeHead(
  this: http.ServerResponse,
  statusCode: number,
  statusMessage?: string | http.OutgoingHttpHeaders | http.OutgoingHttpHeader[],
  headers?: http.OutgoingHttpHeaders | http.OutgoingHttpHeader[],
) {
  if (!this.headersSent && !this.getHeader('traceresponse')) {
    const carrier: Record<string, string> = {}
    propagation.inject(context.active(), carrier)
    if (carrier.traceparent) this.setHeader('traceresponse', carrier.traceparent)
  }
  return originalWriteHead.call(this, statusCode, statusMessage, headers)
} as typeof http.ServerResponse.prototype.writeHead

const dropHealthChecks = new DropHealthCheckSampler(new AlwaysOnSampler())

const sdk = new NodeSDK({
  serviceName: process.env.OTEL_SERVICE_NAME || 'wayd-client',
  sampler: new ParentBasedSampler({
    root: dropHealthChecks,
    remoteParentSampled: dropHealthChecks,
    localParentSampled: dropHealthChecks,
  }),
  instrumentations: [
    getNodeAutoInstrumentations({
      // Disable fs instrumentation to reduce noise
      '@opentelemetry/instrumentation-fs': {
        enabled: false,
      },
    }),
  ],
})

sdk.start()

// Graceful shutdown
process.on('SIGTERM', () => {
  sdk
    .shutdown()
    .then(() => console.log('OpenTelemetry SDK shut down successfully'))
    .catch((error) =>
      console.error('Error shutting down OpenTelemetry SDK', error),
    )
    .finally(() => process.exit(0))
})
