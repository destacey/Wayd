import { ReleaseDto } from '@/src/services/wayd-api'
import { render, screen } from '@testing-library/react'
import ReleaseFacts from './release-facts'

jest.unmock('dayjs')

const release = (overrides: Partial<ReleaseDto> = {}): ReleaseDto =>
  ({
    id: 'release-1',
    key: 4,
    product: { id: 'product-1', key: 7, name: 'Wayd API' },
    version: '4.7.0',
    ...overrides,
  }) as ReleaseDto

/** The Cut row's value. Targeted by row because an unset Target renders "Not set" too. */
const cutValue = () => screen.getByText('Cut').parentElement as HTMLElement

describe('ReleaseFacts', () => {
  it('says a cut is still to come while the release has not shipped', () => {
    // Arrange / Act
    render(<ReleaseFacts release={release()} />)

    // Assert
    expect(cutValue()).toHaveTextContent('Not yet cut')
  })

  it('stops saying "not yet" about a cut once the release has shipped', () => {
    // Arrange — hand-entry and import both land here: released, with no cut recorded. Cutting is
    // refused after release, so "not yet" promises something that will never happen.
    const shipped = release({ releasedDate: '2026-04-03' as unknown as Date })

    // Act
    render(<ReleaseFacts release={shipped} />)

    // Assert — by row, since an unset Target says "Not set" too.
    expect(cutValue()).toHaveTextContent('Not set')
    expect(screen.queryByText('Not yet cut')).not.toBeInTheDocument()
  })

  it('shows the cut date when there is one', () => {
    // Arrange / Act
    const cut = release({ cutDate: '2026-04-01' as unknown as Date })
    render(<ReleaseFacts release={cut} />)

    // Assert
    expect(cutValue()).toHaveTextContent('Apr 1, 2026')
    expect(screen.queryByText('Not yet cut')).not.toBeInTheDocument()
  })
})
