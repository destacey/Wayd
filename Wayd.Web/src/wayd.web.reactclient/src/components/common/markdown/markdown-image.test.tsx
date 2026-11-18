// @vitest-environment jsdom
import { render, screen } from '@testing-library/react'
import { attachmentsGetContent } from '@/src/services/api/attachments'
import MarkdownImage from './markdown-image'

vi.mock(import('@/src/services/api/attachments'), () => ({
  attachmentsGetContent: vi.fn(),
  attachmentsUpload: vi.fn(),
}))

const getContent = vi.mocked(attachmentsGetContent)

describe('MarkdownImage', () => {
  beforeEach(() => {
    getContent.mockReset()
    URL.createObjectURL = vi.fn(() => 'blob:attachment')
  })

  it('loads an uploaded image through the API and shows it from memory', async () => {
    // Arrange
    getContent.mockResolvedValue(
      new Blob([new Uint8Array([1])], { type: 'image/png' }),
    )
    const id = '0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a01'

    // Act
    render(<MarkdownImage src={`/api/attachments/${id}`} alt="Diagram" />)

    // Assert
    const image = await screen.findByRole('img', { name: 'Diagram' })
    expect(image).toHaveAttribute('src', 'blob:attachment')
    expect(getContent).toHaveBeenCalledWith(id)
  })

  it('says an uploaded image is unavailable when it cannot be loaded', async () => {
    // Arrange — deleted, or on a record the viewer cannot read: the API answers 404 either way.
    getContent.mockRejectedValue({ status: 404 })

    // Act
    render(
      <MarkdownImage
        src="/api/attachments/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a02"
        alt="Diagram"
      />,
    )

    // Assert
    expect(await screen.findByText('[Diagram unavailable]')).toBeInTheDocument()
    expect(screen.queryByRole('img', { name: 'Diagram' })).toBeNull()
  })

  it('shows any other address as it is, without asking the API', () => {
    // Act
    render(<MarkdownImage src="https://wayd.dev/logo.png" alt="Logo" />)

    // Assert
    expect(screen.getByRole('img', { name: 'Logo' })).toHaveAttribute(
      'src',
      'https://wayd.dev/logo.png',
    )
    expect(getContent).not.toHaveBeenCalled()
  })
})
