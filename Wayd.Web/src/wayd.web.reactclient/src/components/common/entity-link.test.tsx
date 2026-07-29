import React from 'react'
import { render, screen } from '@testing-library/react'
import EntityLink from './entity-link'

describe('EntityLink', () => {
  it('renders a link with the given href and children', () => {
    // Arrange & Act
    render(<EntityLink href="/planning/risks/42">42 - Some Risk</EntityLink>)

    // Assert
    const link = screen.getByRole('link', { name: '42 - Some Risk' })
    expect(link).toHaveAttribute('href', '/planning/risks/42')
  })

  it('applies the quiet link style class', () => {
    // Arrange & Act
    render(<EntityLink href="/x">Title</EntityLink>)

    // Assert
    expect(screen.getByRole('link', { name: 'Title' }).className).toContain(
      'entityLink',
    )
  })

  it('merges a custom className with the quiet link class', () => {
    // Arrange & Act
    render(
      <EntityLink href="/x" className="custom">
        Title
      </EntityLink>,
    )

    // Assert
    const link = screen.getByRole('link', { name: 'Title' })
    expect(link.className).toContain('entityLink')
    expect(link.className).toContain('custom')
  })
})
