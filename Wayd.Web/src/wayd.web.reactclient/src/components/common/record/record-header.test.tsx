import React from 'react'
import { render, screen } from '@testing-library/react'
import RecordHeader from './record-header'

describe('RecordHeader', () => {
  it('renders the record key as its own element, not part of the name', () => {
    // Arrange / Act
    render(<RecordHeader name="Platform Core" recordKey="PLAT-CORE" />)

    // Assert — separate nodes so the key can be styled and copied on its own
    expect(screen.getByText('PLAT-CORE')).toBeInTheDocument()
    expect(screen.getByText('Platform Core')).toBeInTheDocument()
    expect(screen.queryByText('PLAT-CORE - Platform Core')).toBeNull()
  })

  it('omits the key chip when no record key is given', () => {
    // Arrange / Act
    const { container } = render(<RecordHeader name="Platform Core" />)

    // Assert
    expect(container.textContent).toBe('Platform Core')
  })

  it('renders a circular avatar for a person', () => {
    // Arrange / Act
    render(
      <RecordHeader
        name="Priya Raghunathan"
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
      <RecordHeader
        name="Platform Core"
        avatar={{ kind: 'record', icon: <span>icon</span> }}
      />,
    )

    // Assert
    expect(screen.getByTestId('record-avatar-record')).toBeInTheDocument()
    expect(screen.queryByTestId('record-avatar-person')).toBeNull()
  })

  it('renders the parent link and subtitle as one trail under the name', () => {
    // Arrange / Act
    render(
      <RecordHeader
        name="Priya Raghunathan"
        subtitle="Employee Details"
        parent={{ label: 'Employees', href: '/organizations/employees' }}
      />,
    )

    // Assert — the parent is a link back to the list, and the subtitle names
    // the current page; both sit below the record name rather than beside it.
    expect(screen.getByRole('link', { name: 'Employees' })).toHaveAttribute(
      'href',
      '/organizations/employees',
    )
    expect(screen.getByText('Employee Details')).toBeInTheDocument()
  })

  it('renders a subtitle alone when there is no parent', () => {
    // Arrange / Act
    render(<RecordHeader name="Platform Core" subtitle="Team Details" />)

    // Assert
    expect(screen.getByText('Team Details')).toBeInTheDocument()
    expect(screen.queryByRole('link')).toBeNull()
  })

  it('renders tags and actions alongside the identity', () => {
    // Arrange / Act
    render(
      <RecordHeader
        name="Platform Core"
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
