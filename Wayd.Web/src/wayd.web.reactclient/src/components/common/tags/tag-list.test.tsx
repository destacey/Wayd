import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import TagList, { TagListItem } from './tag-list'

const tag = (id: string, label: string, qualifier?: string): TagListItem => ({
  id,
  label,
  qualifier,
})

describe('TagList', () => {
  it('shows each tag it is given', () => {
    // Arrange / Act
    render(
      <TagList
        tags={[tag('1', 'ios', 'Platform'), tag('2', 'pci-scope', 'Compliance')]}
      />,
    )

    // Assert
    expect(screen.getByText('ios')).toBeInTheDocument()
    expect(screen.getByText('pci-scope')).toBeInTheDocument()
  })

  it('shows the qualifier before the tag it belongs to', () => {
    // A bare "gold" does not say whether it is a tier, a platform or a
    // compliance scope.
    // Arrange / Act
    render(<TagList tags={[tag('1', 'gold', 'Tier')]} />)

    // Assert
    expect(screen.getByText(/Tier/)).toBeInTheDocument()
    expect(screen.getByText(/gold/)).toBeInTheDocument()
  })

  it('renders a tag with no qualifier, for areas whose tags have no axes', () => {
    // Arrange / Act
    render(<TagList tags={[tag('1', 'urgent')]} />)

    // Assert
    expect(screen.getByText('urgent')).toBeInTheDocument()
  })

  it('collapses past the visible cap so a header cannot be crowded out', () => {
    // Arrange / Act
    render(
      <TagList
        tags={[
          tag('1', 'web', 'Platform'),
          tag('2', 'ios', 'Platform'),
          tag('3', 'android', 'Platform'),
          tag('4', 'gold', 'Tier'),
          tag('5', 'pci-scope', 'Compliance'),
        ]}
        maxVisible={3}
      />,
    )

    // Assert
    expect(screen.getByText('+2')).toBeInTheDocument()
    expect(screen.queryByText('pci-scope')).not.toBeInTheDocument()
  })

  it('shows every tag when no cap is given', () => {
    // Arrange / Act
    render(<TagList tags={[tag('1', 'a'), tag('2', 'b'), tag('3', 'c')]} />)

    // Assert
    expect(screen.queryByText(/^\+/)).not.toBeInTheDocument()
  })

  it('draws nothing for an untagged record by default', () => {
    // A placeholder chip takes header space to say nothing, and every untagged
    // record would carry one.
    // Arrange / Act
    render(<TagList tags={[]} onManage={() => {}} />)

    // Assert
    expect(document.querySelectorAll('.ant-tag')).toHaveLength(0)
    expect(screen.getByRole('button', { name: 'Manage tags' })).toBeInTheDocument()
  })

  it('says so when asked, for a read-only view with no manage button', () => {
    // There, an empty row cannot be told from one still loading.
    // Arrange / Act
    render(<TagList tags={[]} emptyLabel="No tags" />)

    // Assert
    expect(screen.getByText('No tags')).toBeInTheDocument()
  })

  it('offers managing when a handler is given', async () => {
    // Arrange
    const onManage = jest.fn()
    render(<TagList tags={[]} onManage={onManage} />)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Manage tags' }))

    // Assert
    expect(onManage).toHaveBeenCalled()
  })

  it('stays read-only without one', () => {
    // A reader who cannot change tags still sees them.
    // Arrange / Act
    render(<TagList tags={[tag('1', 'ios', 'Platform')]} />)

    // Assert
    expect(screen.getByText('ios')).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Manage tags' }),
    ).not.toBeInTheDocument()
  })
})
