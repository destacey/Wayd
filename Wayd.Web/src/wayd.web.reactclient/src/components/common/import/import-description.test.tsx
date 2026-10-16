import { render } from '@testing-library/react'
import { ImportDescription } from './import-description'

describe('ImportDescription', () => {
  it('shows each backticked span as code and the rest as text', () => {
    // Arrange
    const text = 'The `ImportId` of another row, or `1` for the first.'

    // Act
    const { container } = render(<ImportDescription text={text} />)

    // Assert
    const code = Array.from(container.querySelectorAll('code')).map(
      (element) => element.textContent,
    )
    expect(code).toEqual(['ImportId', '1'])
    expect(container.textContent).toBe(
      'The ImportId of another row, or 1 for the first.',
    )
  })

  it('shows text without backticks unchanged', () => {
    // Act
    const { container } = render(
      <ImportDescription text="Blank means today." />,
    )

    // Assert
    expect(container.querySelector('code')).toBeNull()
    expect(container.textContent).toBe('Blank means today.')
  })
})
