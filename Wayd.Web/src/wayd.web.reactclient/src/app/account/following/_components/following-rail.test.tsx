import { fireEvent, render, screen } from '@testing-library/react'
import { FollowCountsDto, FollowView } from '@/src/services/wayd-api'
import FollowingRail from './following-rail'

const counts: FollowCountsDto = {
  total: 47,
  types: [
    { ownerType: 'ppm.project', displayName: 'Project', count: 28 },
    { ownerType: 'ppm.program', displayName: 'Program', count: 6 },
  ],
  views: [
    { view: FollowView.Unhealthy, count: 2 },
    { view: FollowView.ChangedRecently, count: 7 },
    { view: FollowView.Closed, count: 9 },
  ],
}

describe('FollowingRail', () => {
  it('lists every type and view with its count', () => {
    render(<FollowingRail counts={counts} filter={{}} onChange={jest.fn()} />)

    expect(
      screen.getByRole('button', { name: 'Everything 47' }),
    ).toHaveAttribute('aria-pressed', 'true')
    expect(
      screen.getByRole('button', { name: 'Project 28' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Program 6' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Health is red 2' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Changed this week 7' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Closed or done 9' }),
    ).toBeInTheDocument()
  })

  it('selects a type on its own, clearing any view', () => {
    const onChange = jest.fn()
    render(
      <FollowingRail
        counts={counts}
        filter={{ view: FollowView.Closed }}
        onChange={onChange}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Project 28' }))

    expect(onChange).toHaveBeenCalledWith({ ownerType: 'ppm.project' })
  })

  it('selects a view on its own, clearing any type', () => {
    const onChange = jest.fn()
    render(
      <FollowingRail
        counts={counts}
        filter={{ ownerType: 'ppm.project' }}
        onChange={onChange}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Health is red 2' }))

    expect(onChange).toHaveBeenCalledWith({ view: FollowView.Unhealthy })
  })

  it('marks only the selected view, not the type underneath it', () => {
    render(
      <FollowingRail
        counts={counts}
        filter={{ view: FollowView.Closed }}
        onChange={jest.fn()}
      />,
    )

    expect(
      screen.getByRole('button', { name: 'Closed or done 9' }),
    ).toHaveAttribute('aria-pressed', 'true')
    expect(
      screen.getByRole('button', { name: 'Everything 47' }),
    ).toHaveAttribute('aria-pressed', 'false')
  })
})
