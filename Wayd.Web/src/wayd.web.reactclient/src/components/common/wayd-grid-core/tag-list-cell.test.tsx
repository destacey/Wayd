import { render, screen } from '@testing-library/react'

import TagListCell from './tag-list-cell'

describe('TagListCell', () => {
  it('renders nothing for empty or missing values', () => {
    // Arrange / Act
    const { container: empty } = render(<TagListCell values={[]} />)
    const { container: missing } = render(<TagListCell values={undefined} />)

    // Assert
    expect(empty).toBeEmptyDOMElement()
    expect(missing).toBeEmptyDOMElement()
  })

  it('renders every value as a tag, in the given order', () => {
    // Arrange / Act — the row clips via CSS overflow, so all tags are in the
    // DOM regardless of column width; narrowing just fades the trailing ones.
    render(<TagListCell values={['Alice', 'Bob', 'Carol']} />)

    // Assert
    const tags = screen.getAllByText(/Alice|Bob|Carol/)
    expect(tags.map((t) => t.textContent)).toEqual(['Alice', 'Bob', 'Carol'])
  })
})
