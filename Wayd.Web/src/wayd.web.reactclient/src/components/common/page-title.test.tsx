import { render, screen } from '@testing-library/react'
import PageTitle from './page-title'

describe('PageTitle', () => {
  it('renders the title', () => {
    // Arrange / Act
    render(<PageTitle title="Delivery" />)

    // Assert
    expect(screen.getByText('Delivery')).toBeInTheDocument()
  })

  it('offers a help affordance beside the title when a tooltip is given', () => {
    // Arrange — beside rather than on the heading: a tooltip anchored to the title alone is
    // undiscoverable, since nothing about a heading says it can be hovered.
    // Act
    render(<PageTitle title="Delivery" tooltip="What this page shows." />)

    // Assert
    expect(screen.getByLabelText('About this page')).toBeInTheDocument()
  })

  it('shows no help affordance when there is nothing to explain', () => {
    // Arrange — an icon that opens an empty tooltip is worse than no icon.
    // Act
    render(<PageTitle title="Delivery" />)

    // Assert
    expect(screen.queryByLabelText('About this page')).not.toBeInTheDocument()
  })

  it('keeps the subtitle separate from the title', () => {
    // Arrange / Act
    render(<PageTitle title="Delivery" subtitle="All products · 31 nodes" />)

    // Assert
    expect(screen.getByText('All products · 31 nodes')).toBeInTheDocument()
  })
})
