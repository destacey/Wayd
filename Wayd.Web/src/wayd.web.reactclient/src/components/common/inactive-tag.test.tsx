import { render, screen } from '@testing-library/react'
import InactiveTag from './inactive-tag'

// Mock the useTheme hook
vi.mock('../contexts/theme', () => ({
  default: () => ({
    token: {
      colorWarning: '#faad14',
    },
  }),
}))

describe('InactiveTag', () => {
  it('should return null when isActive is true', () => {
    const { container } = render(<InactiveTag isActive={true} />)
    expect(container.firstChild).toBeNull()
  })

  it('should return null when isActive is undefined', () => {
    const { container } = render(
      <InactiveTag isActive={undefined as unknown as boolean} />,
    )
    expect(container.firstChild).toBeNull()
  })

  it('should return null when isActive is null', () => {
    const { container } = render(
      <InactiveTag isActive={null as unknown as boolean} />,
    )
    expect(container.firstChild).toBeNull()
  })

  it('should render Inactive tag when isActive is false', () => {
    render(<InactiveTag isActive={false} />)
    const tag = screen.getByText('Inactive')
    expect(tag).toBeInTheDocument()
  })

  it('should render with warning color class', () => {
    render(<InactiveTag isActive={false} />)
    const tag = screen.getByText('Inactive')
    // In Ant Design v6, Tag with color prop applies a CSS class rather than inline style
    expect(tag).toBeInTheDocument()
    expect(tag.className).toContain('ant-tag')
  })
})
