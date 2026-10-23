export async function register() {
  if (process.env.NEXT_RUNTIME === 'nodejs') {
    // Only import and initialize OpenTelemetry if OTLP endpoint is configured
    // This check happens before importing to avoid compiling heavy OTel dependencies
    if (process.env.OTEL_EXPORTER_OTLP_ENDPOINT) {
      await import('./instrumentation.node')
    } else {
      console.log(
        'OpenTelemetry disabled: OTEL_EXPORTER_OTLP_ENDPOINT not configured',
      )
    }
  }
}
