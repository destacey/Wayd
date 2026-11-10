import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import ViewAllLink from './view-all-link'

describe('ViewAllLink', () => {
  it('links to the full list when given an href', () => {
    // Arrange / Act
    render(<ViewAllLink href="/account/following" />)

    // Assert
    expect(screen.getByRole('link', { name: 'View all' })).toHaveAttribute(
      'href',
      '/account/following',
    )
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('renders a button that runs the handler when given onClick', async () => {
    // Arrange
    const onClick = vi.fn()
    render(<ViewAllLink onClick={onClick} />)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'View all' }))

    // Assert
    expect(onClick).toHaveBeenCalledTimes(1)
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })

  it('names how many there are when given a count', () => {
    // Arrange / Act
    render(<ViewAllLink href="/ppm/projects" count={23} />)

    // Assert
    expect(
      screen.getByRole('link', { name: 'View all 23' }),
    ).toBeInTheDocument()
  })
})
