import { render } from '@testing-library/react'
import ProductTagSettingsHistoryDrawer from './product-tag-settings-history-drawer'

const mockActivitiesQuery = jest.fn()

jest.mock('@/src/store/features/admin/system-settings-api', () => ({
  PRODUCT_TAG_SETTINGS_ACTIVITY_ID: 'product-tags',
  useGetProductTagSettingsActivitiesQuery: (
    arg: unknown,
    options: { skip: boolean },
  ) => mockActivitiesQuery(arg, options),
  useLazyGetProductTagSettingsActivitiesQuery: () => [jest.fn()],
}))

// The timeline and export button are covered on their own; here only when the
// drawer asks for the history is under test.
jest.mock('@/src/components/common/activities', () => ({
  ACTIVITY_LOG_PAGE_SIZE: 25,
  ActivityLogExportButton: () => null,
  ActivityLogTimeline: () => null,
  useActivityLog: () => ({ timelineProps: {} }),
}))

describe('ProductTagSettingsHistoryDrawer', () => {
  beforeEach(() => {
    mockActivitiesQuery.mockReset()
    mockActivitiesQuery.mockReturnValue({})
  })

  it('does not load the history while closed', () => {
    // Arrange / Act
    render(<ProductTagSettingsHistoryDrawer open={false} onClose={jest.fn()} />)

    // Assert
    expect(mockActivitiesQuery).toHaveBeenCalledWith(
      expect.objectContaining({ idOrKey: 'product-tags', page: 1 }),
      { skip: true },
    )
  })

  it('loads the history once opened', () => {
    // Arrange / Act
    render(<ProductTagSettingsHistoryDrawer open onClose={jest.fn()} />)

    // Assert
    expect(mockActivitiesQuery).toHaveBeenLastCalledWith(
      expect.objectContaining({ idOrKey: 'product-tags', page: 1 }),
      { skip: false },
    )
  })
})
