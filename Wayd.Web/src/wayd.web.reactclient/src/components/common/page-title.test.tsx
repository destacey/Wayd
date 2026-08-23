import React from 'react'
import { render, screen } from '@testing-library/react'
import PageTitle from './page-title'

describe('PageTitle', () => {
  it('renders the title and subtitle', () => {
    // Arrange / Act
    render(<PageTitle title="Platform Core" subtitle="Team Details" />)

    // Assert
    expect(screen.getByText('Platform Core')).toBeInTheDocument()
    expect(screen.getByText('Team Details')).toBeInTheDocument()
  })

  it('renders the record key as its own element, not part of the title', () => {
    // Arrange / Act
    render(<PageTitle title="Platform Core" recordKey="PLAT-CORE" />)

    // Assert — separate nodes so the key can be styled and copied on its own
    expect(screen.getByText('PLAT-CORE')).toBeInTheDocument()
    expect(screen.getByText('Platform Core')).toBeInTheDocument()
    expect(screen.queryByText('PLAT-CORE - Platform Core')).toBeNull()
  })

  it('omits the key chip when no record key is given', () => {
    // Arrange / Act
    const { container } = render(<PageTitle title="Employees" />)

    // Assert
    expect(container.textContent).toBe('Employees')
  })

  it('renders a circular avatar for a person', () => {
    // Arrange / Act
    render(
      <PageTitle
        title="Priya Raghunathan"
        avatar={{ kind: 'person', initials: 'PR' }}
      />,
    )

    // Assert
    expect(screen.getByTestId('record-avatar-person')).toBeInTheDocument()
    expect(screen.getByText('PR')).toBeInTheDocument()
  })

  it('renders a square avatar carrying the entity icon for a record', () => {
    // Arrange / Act
    render(
      <PageTitle
        title="Platform Core"
        avatar={{ kind: 'record', icon: <span>icon</span> }}
      />,
    )

    // Assert
    expect(screen.getByTestId('record-avatar-record')).toBeInTheDocument()
    expect(screen.queryByTestId('record-avatar-person')).toBeNull()
  })

  it('still renders tags and actions alongside the new slots', () => {
    // Arrange / Act
    render(
      <PageTitle
        title="Platform Core"
        recordKey="PLAT-CORE"
        avatar={{ kind: 'record', icon: <span>icon</span> }}
        tags={<span>Active</span>}
        actions={<button>Actions</button>}
      />,
    )

    // Assert
    expect(screen.getByText('Active')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Actions' })).toBeInTheDocument()
  })
})
