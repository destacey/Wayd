import type { Mock } from 'vitest'
import React from 'react'
import { render } from '@testing-library/react'
import ResponsiveFlex from './responsive-flex'
import { Grid } from 'antd'

const { useBreakpoint } = Grid

vi.mock(import('antd'), async (importOriginal) => {
  const originalModule = await importOriginal()
  return {
    ...originalModule,
    Grid: {
      ...originalModule.Grid,
      useBreakpoint: vi.fn(),
    },
  }
})

describe('ResponsiveFlex', () => {
  it('renders children correctly', () => {
    ;(useBreakpoint as Mock).mockReturnValue({ md: true })
    const { getByText } = render(
      <ResponsiveFlex>
        <div>Child 1</div>
        <div>Child 2</div>
      </ResponsiveFlex>,
    )
    expect(getByText('Child 1')).toBeInTheDocument()
    expect(getByText('Child 2')).toBeInTheDocument()
  })

  it('sets vertical layout on small screens', () => {
    ;(useBreakpoint as Mock).mockReturnValue({ md: false })
    const { container } = render(
      <ResponsiveFlex>
        <div>Child</div>
      </ResponsiveFlex>,
    )
    expect(container.firstChild).toHaveClass('ant-flex-vertical')
  })

  it('sets horizontal layout on large screens', () => {
    ;(useBreakpoint as Mock).mockReturnValue({ md: true })
    const { container } = render(
      <ResponsiveFlex>
        <div>Child</div>
      </ResponsiveFlex>,
    )
    expect(container.firstChild).not.toHaveClass('ant-flex-vertical')
  })

  it('wraps each child in a shrinkable equal-width column on large screens', () => {
    ;(useBreakpoint as Mock).mockReturnValue({ md: true })
    const { getByText } = render(
      <ResponsiveFlex>
        <div>Child 1</div>
        <div>Child 2</div>
      </ResponsiveFlex>,
    )
    // Each child is wrapped so flex items can shrink below their content width
    // (prevents collapse, e.g. antd Descriptions tables wrapping per character).
    const wrapper = getByText('Child 1').parentElement
    expect(wrapper).toHaveStyle({ flex: 1, minWidth: 0 })
  })

  it('does not wrap children on small screens', () => {
    ;(useBreakpoint as Mock).mockReturnValue({ md: false })
    const { getByText, container } = render(
      <ResponsiveFlex>
        <div>Child 1</div>
        <div>Child 2</div>
      </ResponsiveFlex>,
    )
    // Children stack full-width directly inside the vertical Flex.
    expect(getByText('Child 1').parentElement).toBe(container.firstChild)
  })
})
