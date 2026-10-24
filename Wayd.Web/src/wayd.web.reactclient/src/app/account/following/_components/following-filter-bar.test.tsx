import { fireEvent, render, screen } from '@testing-library/react'
import { FollowCountsDto, FollowView } from '@/src/services/wayd-api'
import FollowingFilterBar from './following-filter-bar'

const counts: FollowCountsDto = {
  total: 12,
  types: [{ ownerType: 'ppm.project', displayName: 'Project', count: 12 }],
  views: [
    { view: FollowView.Unhealthy, count: 1 },
    { view: FollowView.ChangedRecently, count: 4 },
    { view: FollowView.Closed, count: 3 },
  ],
}

describe('FollowingFilterBar', () => {
  it('combines a view with the chosen type', () => {
    const onChange = jest.fn()
    render(
      <FollowingFilterBar
        counts={counts}
        filter={{ ownerType: 'ppm.project' }}
        onChange={onChange}
      />,
    )

    fireEvent.click(screen.getByText('Changed this week (4)'))

    expect(onChange).toHaveBeenCalledWith({
      ownerType: 'ppm.project',
      view: FollowView.ChangedRecently,
    })
  })

  it('clears a view when its chip is unchecked, keeping the type', () => {
    const onChange = jest.fn()
    render(
      <FollowingFilterBar
        counts={counts}
        filter={{ ownerType: 'ppm.project', view: FollowView.Closed }}
        onChange={onChange}
      />,
    )

    fireEvent.click(screen.getByText('Closed or done (3)'))

    expect(onChange).toHaveBeenCalledWith({
      ownerType: 'ppm.project',
      view: undefined,
    })
  })

  it('shows the type select with counts', () => {
    render(
      <FollowingFilterBar counts={counts} filter={{}} onChange={jest.fn()} />,
    )

    expect(screen.getByText('Everything (12)')).toBeInTheDocument()
  })
})
