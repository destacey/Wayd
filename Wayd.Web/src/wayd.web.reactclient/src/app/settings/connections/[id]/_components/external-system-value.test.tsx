import { render, screen } from '@testing-library/react'
import ExternalSystemValue from './external-system-value'

describe('ExternalSystemValue', () => {
  it('links the system name to its address', () => {
    // Arrange
    const system = {
      id: '0198f3b0-0000-7000-8000-000000000001',
      name: 'contoso',
      baseUrl: 'https://dev.azure.com/contoso',
    }

    // Act
    render(<ExternalSystemValue system={system} />)

    // Assert
    const link = screen.getByRole('link', { name: 'contoso' })
    expect(link).toHaveAttribute('href', 'https://dev.azure.com/contoso')
    expect(link).toHaveAttribute('rel', 'noopener noreferrer')
  })

  it('shows the name alone for a system with no address', () => {
    // Arrange
    const system = {
      id: '0198f3b0-0000-7000-8000-000000000002',
      name: 'tenant-1',
    }

    // Act
    render(<ExternalSystemValue system={system} />)

    // Assert
    expect(screen.getByText('tenant-1')).toBeInTheDocument()
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })

  it('shows a dash for a connection with no system', () => {
    // Arrange / Act
    render(<ExternalSystemValue system={null} />)

    // Assert
    expect(screen.getByText('—')).toBeInTheDocument()
  })
})
