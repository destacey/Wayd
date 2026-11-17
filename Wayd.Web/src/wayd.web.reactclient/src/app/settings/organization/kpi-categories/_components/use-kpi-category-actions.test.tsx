import { KpiCategoryDto } from '@/src/services/api/wayd.schemas'
import { renderHook } from '@testing-library/react'
import useKpiCategoryActions from './use-kpi-category-actions'

const mockHasPermissionClaim = vi.fn()
vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({ hasPermissionClaim: mockHasPermissionClaim }),
}))

vi.mock('@/src/store/features/common/kpis-api', () => ({
  useSetKpiCategoryActiveMutation: () => [vi.fn()],
  useDeleteKpiCategoryMutation: () => [vi.fn()],
}))

vi.mock('./kpi-category-form', () => ({ default: () => null }))
vi.mock('@/src/components/common/kpi/scorecard/kpi-confirm-form', () => ({
  default: () => null,
}))

const category = (overrides: Partial<KpiCategoryDto> = {}): KpiCategoryDto => ({
  id: 'cat-1',
  name: 'Flow',
  order: 0,
  isActive: true,
  kpiCount: 0,
  ...overrides,
})

const keysFor = (c: KpiCategoryDto) =>
  renderHook(() => useKpiCategoryActions())
    .result.current.getActionItems(c)
    .map((item) => item?.key)

describe('useKpiCategoryActions', () => {
  beforeEach(() => {
    mockHasPermissionClaim.mockReset()
    mockHasPermissionClaim.mockReturnValue(true)
  })

  it('offers nothing without the KPI administrator permission', () => {
    mockHasPermissionClaim.mockReturnValue(false)

    expect(keysFor(category())).toEqual([])
    expect(mockHasPermissionClaim).toHaveBeenCalledWith(
      'Permissions.Kpis.Administer',
    )
  })

  it('offers Delete only while no KPI has the category', () => {
    expect(keysFor(category({ kpiCount: 0 }))).toContain('delete')
    expect(keysFor(category({ kpiCount: 2 }))).not.toContain('delete')
  })

  it('offers Deactivate on an active category and Activate on an inactive one', () => {
    expect(keysFor(category({ isActive: true }))).toContain('deactivate')
    expect(keysFor(category({ isActive: false }))).toEqual(
      expect.arrayContaining(['activate']),
    )
    expect(keysFor(category({ isActive: false }))).not.toContain('deactivate')
  })
})
