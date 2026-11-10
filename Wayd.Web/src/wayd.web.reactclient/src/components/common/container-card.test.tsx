import type { Mock } from 'vitest'
import { render, screen } from '@testing-library/react'
import ContainerCard from './container-card'
import useTheme from '../contexts/theme'

// Mock the useTheme hook
vi.mock('../contexts/theme', () => ({
  default: vi.fn(),
}))

describe('ContainerCard', () => {
  beforeEach(() => {
    // Mock the theme context
    ;(useTheme as Mock).mockReturnValue({ badgeColor: '#2196f3' })
  })

  it('renders the title and children', () => {
    render(
      <ContainerCard title="Test Title">
        <div>Test Children</div>
      </ContainerCard>,
    )

    expect(screen.getByText('Test Title')).toBeInTheDocument()
    expect(screen.getByText('Test Children')).toBeInTheDocument()
  })

  it('renders the badge when count is greater than 0', () => {
    render(
      <ContainerCard title="Test Title" count={5}>
        <div>Test Children</div>
      </ContainerCard>,
    )

    expect(screen.getByText('5')).toBeInTheDocument()
  })

  it('does not render the badge when count is 0 or undefined', () => {
    const { rerender } = render(
      <ContainerCard title="Test Title" count={0}>
        <div>Test Children</div>
      </ContainerCard>,
    )

    expect(screen.queryByText('0')).not.toBeInTheDocument()

    rerender(
      <ContainerCard title="Test Title">
        <div>Test Children</div>
      </ContainerCard>,
    )

    expect(screen.queryByText('0')).not.toBeInTheDocument()
  })

  it('renders the actions if provided', () => {
    render(
      <ContainerCard title="Test Title" actions={<button>Action</button>}>
        <div>Test Children</div>
      </ContainerCard>,
    )

    expect(screen.getByText('Action')).toBeInTheDocument()
  })

  // TODO: fix this test
  //   it('applies the correct badge color from the theme', () => {
  //     render(
  //       <ContainerCard title="Test Title" count={8}>
  //         <div>Test Children</div>
  //       </ContainerCard>,
  //     )

  //     const badge = screen.getByText('8')
  //     expect(badge).toHaveStyle('background-color: #2196f3')
  //   })
})
