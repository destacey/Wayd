'use client'

import { Button, Result } from 'antd'
import { Component, ErrorInfo, ReactNode } from 'react'

interface SectionBoundaryProps {
  children: ReactNode
  /** Shown in the failure message so the user knows what broke. */
  sectionLabel?: string
  /** Returns the user to the record's default section. */
  onLeave?: () => void
}

interface SectionBoundaryState {
  error: Error | null
}

/**
 * Catches render errors inside one section so the rest of the record page —
 * the identity bar, the section rail, the facts — keeps working and the user
 * can navigate away without reloading.
 *
 * Mount this with `key={activeSection}` so moving to another section clears a
 * caught error automatically; otherwise a failure would stick until reload.
 */
class SectionBoundary extends Component<
  SectionBoundaryProps,
  SectionBoundaryState
> {
  constructor(props: SectionBoundaryProps) {
    super(props)
    this.state = { error: null }
  }

  static getDerivedStateFromError(error: Error): SectionBoundaryState {
    return { error }
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    // The one place every section render error passes through — the natural
    // hook for telemetry once it is wired up.
    console.error(
      `Error rendering section "${this.props.sectionLabel ?? 'unknown'}"`,
      error,
      errorInfo.componentStack,
    )
  }

  private reset = () => this.setState({ error: null })

  render() {
    if (!this.state.error) return this.props.children

    const { sectionLabel, onLeave } = this.props

    return (
      <Result
        status="warning"
        title="This section could not be loaded"
        subTitle={
          sectionLabel
            ? `Something went wrong rendering ${sectionLabel}. The rest of this record is unaffected.`
            : 'Something went wrong. The rest of this record is unaffected.'
        }
        extra={[
          <Button key="retry" type="primary" onClick={this.reset}>
            Try again
          </Button>,
          onLeave && (
            <Button key="leave" onClick={onLeave}>
              Back to overview
            </Button>
          ),
        ].filter(Boolean)}
      />
    )
  }
}

export default SectionBoundary
