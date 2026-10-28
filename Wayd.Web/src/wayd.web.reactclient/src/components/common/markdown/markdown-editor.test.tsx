import { render } from '@testing-library/react'
import rehypeSanitize from 'rehype-sanitize'
import MarkdownEditor from './markdown-editor'
import { sanitizeSchema } from './sanitize-schema'

let editorProps: Record<string, any> = {}

// The real editor loads through next/dynamic with SSR off and pulls in an ESM Prism chain jsdom
// cannot run, so the stub records the props the editor would receive.
jest.mock('next/dynamic', () => ({
  __esModule: true,
  default: () => {
    const Component = (props: Record<string, any>) => {
      editorProps = props
      return null
    }
    Component.displayName = 'MockMDEditor'
    return Component
  },
}))

describe('MarkdownEditor', () => {
  beforeEach(() => {
    editorProps = {}
  })

  it('sanitizes the preview with the same schema as MarkdownRenderer', () => {
    // Arrange / Act
    render(<MarkdownEditor value="text" maxLength={100} />)

    // Assert
    expect(editorProps.previewOptions.rehypePlugins).toContainEqual([
      rehypeSanitize,
      sanitizeSchema,
    ])
  })

  it('turns off the preview copy button so code blocks keep a single child', () => {
    // Arrange / Act
    render(<MarkdownEditor value="text" maxLength={100} />)

    // Assert
    expect(editorProps.previewOptions.disableCopy).toBe(true)
  })
})
