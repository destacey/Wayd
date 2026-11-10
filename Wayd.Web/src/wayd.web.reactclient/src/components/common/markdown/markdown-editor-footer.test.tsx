import React from 'react'
import { render, screen } from '@testing-library/react'
import { MarkdownEditorFooter } from '.'

describe('MarkdownEditorFooter', () => {
  it('renders the "Markdown enabled" text', () => {
    render(<MarkdownEditorFooter currentLength={0} maxLength={100} />)

    const markdownText = screen.getByText('Markdown enabled')
    expect(markdownText).toBeInTheDocument()
    expect(markdownText).toHaveTextContent('Markdown enabled')
  })

  it('displays the current and max length correctly', () => {
    const currentLength = 50
    const maxLength = 100

    render(
      <MarkdownEditorFooter
        currentLength={currentLength}
        maxLength={maxLength}
      />,
    )

    const lengthText = screen.getByText(`${currentLength} / ${maxLength}`)
    expect(lengthText).toBeInTheDocument()
    expect(lengthText).toHaveTextContent('50 / 100')
  })

  it('applies the correct typography styles', () => {
    render(<MarkdownEditorFooter currentLength={25} maxLength={75} />)

    const markdownText = screen.getByText('Markdown enabled')
    const lengthText = screen.getByText('25 / 75')

    expect(markdownText).toHaveClass('ant-typography-secondary')
    expect(lengthText).toHaveClass('ant-typography-secondary')
  })

  it('handles edge cases for currentLength and maxLength', () => {
    render(<MarkdownEditorFooter currentLength={0} maxLength={0} />)

    const lengthText = screen.getByText('0 / 0')
    expect(lengthText).toBeInTheDocument()
  })
})
