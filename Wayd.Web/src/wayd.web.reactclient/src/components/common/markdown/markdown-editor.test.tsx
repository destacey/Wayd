import { render, screen } from '@testing-library/react'
import { useFeatureFlag } from '@/src/hooks'
import MarkdownEditor from './markdown-editor'

jest.mock('@/src/hooks', () => ({ useFeatureFlag: jest.fn() }))

// Each stub shows which editor rendered and the props it received.
jest.mock('./legacy-markdown-editor', () => ({
  __esModule: true,
  default: (props: { value?: string; maxLength: number }) => (
    <div>
      legacy {props.value} {props.maxLength}
    </div>
  ),
}))
jest.mock('next/dynamic', () => ({
  __esModule: true,
  default: () => {
    const Wysiwyg = (props: { value?: string; maxLength: number }) => (
      <div>
        wysiwyg {props.value} {props.maxLength}
      </div>
    )
    return Wysiwyg
  },
}))

const mockFlag = (flag: { isEnabled: boolean; isLoading: boolean }) =>
  jest.mocked(useFeatureFlag).mockReturnValue(flag)

describe('MarkdownEditor', () => {
  it('shows the document editor when the flag is on', () => {
    // Arrange
    mockFlag({ isEnabled: true, isLoading: false })

    // Act
    render(<MarkdownEditor value="text" maxLength={100} />)

    // Assert
    expect(screen.getByText('wysiwyg text 100')).toBeInTheDocument()
    // The name must match FeatureFlags.Names.WysiwygMarkdownEditor in Wayd.Common.Domain.
    expect(useFeatureFlag).toHaveBeenCalledWith('wysiwyg-markdown-editor')
  })

  it('shows the markdown text editor when the flag is off', () => {
    // Arrange
    mockFlag({ isEnabled: false, isLoading: false })

    // Act
    render(<MarkdownEditor value="text" maxLength={100} />)

    // Assert
    expect(screen.getByText('legacy text 100')).toBeInTheDocument()
  })

  it('holds the space but shows neither editor until the flags have loaded', () => {
    // Arrange
    mockFlag({ isEnabled: false, isLoading: true })

    // Act
    const { container } = render(
      <MarkdownEditor value="text" maxLength={100} />,
    )

    // Assert
    expect(screen.queryByText(/legacy|wysiwyg/)).toBeNull()
    expect(container.firstElementChild).toHaveAttribute('aria-busy', 'true')
  })
})
