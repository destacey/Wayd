import React from 'react'
import { render, screen } from '@testing-library/react'
import HealthCheckTag, {
  HealthCheckDetailsData,
  HealthCheckStatusTagData,
} from './health-check-tag'

jest.mock('@ant-design/icons', () => ({
  FlagFilled: ({ style, 'aria-label': ariaLabel }: { style?: React.CSSProperties; 'aria-label'?: string }) => (
    <span data-testid="flag-icon" style={style} aria-label={ariaLabel} />
  ),
}))

jest.mock('../markdown', () => ({
  MarkdownRenderer: ({ markdown }: { markdown?: string }) => (
    <div data-testid="markdown-renderer">{markdown}</div>
  ),
}))

jest.mock('antd', () => {
  const actual = jest.requireActual('antd')

  return {
    ...actual,
    Popover: ({
      children,
      content,
      onOpenChange,
      styles,
    }: {
      children: any
      content: any
      onOpenChange?: (open: boolean) => void
      styles?: { content?: React.CSSProperties }
    }) => (
      <div>
        <div onMouseEnter={() => onOpenChange?.(true)}>{children}</div>
        <div data-testid="health-check-popover-content" style={styles?.content}>
          {typeof content === 'function' ? content() : content}
        </div>
      </div>
    ),
  }
})

describe('HealthCheckTag', () => {
  const healthCheck: HealthCheckStatusTagData = {
    status: { name: 'Healthy' },
    expiration: new Date(2026, 0, 15, 14, 30),
  }

  it('should return null when health check is not provided', () => {
    const { container } = render(<HealthCheckTag healthCheck={null} />)
    expect(container.firstChild).toBeNull()
  })

  it('should render status tag with mapped color', () => {
    const { container } = render(<HealthCheckTag healthCheck={healthCheck} />)
    expect(screen.getByText('Healthy')).toBeInTheDocument()
    expect(container.querySelector('.ant-tag-success')).toBeInTheDocument()
  })

  it('should render summary content when details are not provided', () => {
    render(<HealthCheckTag healthCheck={healthCheck} />)
    expect(screen.getByText('Expires On')).toBeInTheDocument()
    expect(screen.getByText(/2026-01-15/)).toBeInTheDocument()
  })

  it('should render loading indicator when loading details', () => {
    const { container } = render(
      <HealthCheckTag healthCheck={healthCheck} isLoading />,
    )
    expect(container.querySelector('.ant-spin')).toBeInTheDocument()
  })

  it('should render detailed content when details are provided', () => {
    const details: HealthCheckDetailsData = {
      ...healthCheck,
      reportedBy: { name: 'Jane Smith' },
      reportedOn: new Date(2026, 0, 10, 9, 0),
      note: 'Project remains on track',
    }

    render(<HealthCheckTag healthCheck={healthCheck} details={details} />)

    expect(screen.getByText('Reported By')).toBeInTheDocument()
    expect(screen.getByText('Jane Smith')).toBeInTheDocument()
    expect(screen.getByText('Reported On')).toBeInTheDocument()
    expect(screen.getByText(/Jan 10, 2026/)).toBeInTheDocument()
    expect(screen.getByText('Expires On')).toBeInTheDocument()
    expect(screen.getByTestId('markdown-renderer')).toHaveTextContent(
      'Project remains on track',
    )
  })

  it('should use a responsive detailed popover width when notes are provided', () => {
    const details: HealthCheckDetailsData = {
      ...healthCheck,
      reportedBy: { name: 'Jane Smith' },
      reportedOn: new Date(2026, 0, 10, 9, 0),
      note: 'Project remains on track',
    }

    render(<HealthCheckTag healthCheck={healthCheck} details={details} />)

    expect(screen.getByTestId('health-check-popover-content')).toHaveStyle({
      width: 'min(420px, calc(100vw - 32px))',
    })
  })

  it('should use a compact responsive popover width without notes', () => {
    render(<HealthCheckTag healthCheck={healthCheck} />)

    expect(screen.getByTestId('health-check-popover-content')).toHaveStyle({
      width: 'min(280px, calc(100vw - 32px))',
    })
  })
})

describe('HealthCheckTag variant=flag', () => {
  const healthCheck: HealthCheckStatusTagData = {
    status: { name: 'Healthy' },
    expiration: new Date(2026, 0, 15, 14, 30),
  }

  it('should render a flag icon instead of a tag', () => {
    render(<HealthCheckTag healthCheck={healthCheck} variant="flag" />)
    expect(screen.getByTestId('flag-icon')).toBeInTheDocument()
    expect(screen.queryByRole('group')).toBeNull()
  })

  it('should use success color for Healthy', () => {
    render(<HealthCheckTag healthCheck={healthCheck} variant="flag" />)
    expect(screen.getByTestId('flag-icon')).toHaveStyle({
      color: 'var(--ant-color-success)',
    })
  })

  it('should use warning color for At Risk', () => {
    render(
      <HealthCheckTag
        healthCheck={{ ...healthCheck, status: { name: 'At Risk' } }}
        variant="flag"
      />,
    )
    expect(screen.getByTestId('flag-icon')).toHaveStyle({
      color: 'var(--ant-color-warning)',
    })
  })

  it('should use error color for Unhealthy', () => {
    render(
      <HealthCheckTag
        healthCheck={{ ...healthCheck, status: { name: 'Unhealthy' } }}
        variant="flag"
      />,
    )
    expect(screen.getByTestId('flag-icon')).toHaveStyle({
      color: 'var(--ant-color-error)',
    })
  })

  it('should use disabled color for unknown status', () => {
    render(
      <HealthCheckTag
        healthCheck={{ ...healthCheck, status: { name: 'Unknown' } }}
        variant="flag"
      />,
    )
    expect(screen.getByTestId('flag-icon')).toHaveStyle({
      color: 'var(--ant-color-text-disabled)',
    })
  })

  it('should set aria-label with the status name', () => {
    render(<HealthCheckTag healthCheck={healthCheck} variant="flag" />)
    expect(screen.getByTestId('flag-icon')).toHaveAttribute(
      'aria-label',
      'Health: Healthy',
    )
  })

  it('should still render popover content on hover', () => {
    render(<HealthCheckTag healthCheck={healthCheck} variant="flag" />)
    expect(screen.getByText('Expires On')).toBeInTheDocument()
  })

  it('should return null when health check is not provided', () => {
    const { container } = render(
      <HealthCheckTag healthCheck={null} variant="flag" />,
    )
    expect(container.firstChild).toBeNull()
  })
})
