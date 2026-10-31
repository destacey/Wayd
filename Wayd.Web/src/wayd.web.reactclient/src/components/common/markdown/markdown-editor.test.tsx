import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import MarkdownEditor from './markdown-editor'

interface DynamicOptions {
  ssr?: boolean
  loading?: () => ReactNode
}

// The stub shows the props the document editor received and keeps the options it was loaded with,
// which are read through jest.requireMock because the module loads before this file's own bindings.
jest.mock('next/dynamic', () => {
  const captured: { options?: DynamicOptions } = {}
  return {
    __esModule: true,
    captured,
    default: (_loader: unknown, options: DynamicOptions) => {
      captured.options = options
      const Wysiwyg = (props: { value?: string; maxLength: number }) => (
        <div>
          wysiwyg {props.value} {props.maxLength}
        </div>
      )
      return Wysiwyg
    },
  }
})

const dynamicOptions = () =>
  jest.requireMock<{ captured: { options?: DynamicOptions } }>('next/dynamic')
    .captured.options

describe('MarkdownEditor', () => {
  it('shows the document editor with the props it was given', () => {
    // Arrange / Act
    render(<MarkdownEditor value="text" maxLength={100} />)

    // Assert
    expect(screen.getByText('wysiwyg text 100')).toBeInTheDocument()
  })

  it('loads the document editor on the client only, holding its space meanwhile', () => {
    // Arrange
    const options = dynamicOptions()

    // Act
    const { container } = render(<>{options?.loading?.()}</>)

    // Assert
    expect(options?.ssr).toBe(false)
    expect(container.firstElementChild).toHaveAttribute('aria-busy', 'true')
  })
})
