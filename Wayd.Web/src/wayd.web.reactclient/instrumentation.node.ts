// OpenTelemetry initialization for the Node.js server.
// Imported only when OTEL_EXPORTER_OTLP_ENDPOINT is set.
//
// The exporter is chosen from OTEL_EXPORTER_OTLP_PROTOCOL. Aspire injects grpc for its dashboard.
// Datadog's OTLP intake is http/protobuf, and a gRPC exporter pointed at that URL exports nothing.

import { NodeSDK } from '@opentelemetry/sdk-node'
import { getNodeAutoInstrumentations } from '@opentelemetry/auto-instrumentations-node'

const otlpEndpoint = process.env.OTEL_EXPORTER_OTLP_ENDPOINT
console.log(
  `Initializing OpenTelemetry with endpoint: ${otlpEndpoint} protocol: ${process.env.OTEL_EXPORTER_OTLP_PROTOCOL ?? 'http/protobuf'}`,
)

// Configure metric export intervals via environment variables.
// The SDK rejects an interval that is not longer than the timeout.
process.env.OTEL_METRIC_EXPORT_INTERVAL = '5000'
process.env.OTEL_METRIC_EXPORT_TIMEOUT = '3000'

const sdk = new NodeSDK({
  serviceName: process.env.OTEL_SERVICE_NAME || 'wayd-client',
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
