import { render, screen } from '@testing-library/react'
import { InteractionStyle } from '@/src/services/wayd-api'
import { InteractionStyleTags } from './interaction-style'

describe('InteractionStyleTags', () => {
  it('says nothing was recorded rather than rendering an empty cell', () => {
    // Arrange / Act — a blank cell reads as "there are none", and a reader who believes that has
    // concluded the dependency is safe
    render(<InteractionStyleTags styles={undefined} />)

    // Assert
    expect(screen.getByText('Not recorded')).toBeInTheDocument()
  })

  it('treats an empty list the same as nothing recorded', () => {
    // Arrange / Act — the API folds an empty list to null, so the two must read alike
    render(<InteractionStyleTags styles={[]} />)

    // Assert
    expect(screen.getByText('Not recorded')).toBeInTheDocument()
  })

  it('renders the one recorded style', () => {
    // Arrange / Act
    render(<InteractionStyleTags styles={[InteractionStyle.Asynchronous]} />)

    // Assert
    expect(screen.getByText('Asynchronous')).toBeInTheDocument()
    expect(screen.queryByText('Synchronous')).not.toBeInTheDocument()
    expect(screen.queryByText('Not recorded')).not.toBeInTheDocument()
  })

  it('renders both in a fixed order, whatever order they arrive in', () => {
    // Arrange / Act — two rows holding the same pair of styles must not read differently
    render(
      <InteractionStyleTags
        styles={[InteractionStyle.Asynchronous, InteractionStyle.Synchronous]}
      />,
    )

    // Assert
    expect(
      screen
        .getAllByText(/^(Synchronous|Asynchronous)$/)
        .map((t) => t.textContent),
    ).toEqual(['Synchronous', 'Asynchronous'])
  })
})
