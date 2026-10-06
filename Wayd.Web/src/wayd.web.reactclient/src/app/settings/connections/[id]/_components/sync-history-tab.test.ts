import { parseDetailsJson } from './sync-history-tab'

jest.mock('@/src/store/features/app-integration/connections-api', () => ({}))
jest.mock('@/src/components/contexts/auth', () => ({}))
jest.mock('@/src/components/contexts/messaging', () => ({}))

describe('parseDetailsJson', () => {
  it('reads camelCase details as written', () => {
    const details = parseDetailsJson<{ workItemsProcessed: number }[]>(
      '[{"workItemsProcessed":7}]',
    )

    expect(details).toEqual([{ workItemsProcessed: 7 }])
  })

  it('reads PascalCase details recorded by older work syncs', () => {
    const details = parseDetailsJson<
      { internalWorkspaceId: string; workItemsProcessed: number }[]
    >('[{"InternalWorkspaceId":"ws-1","WorkItemsProcessed":7}]')

    expect(details).toEqual([
      { internalWorkspaceId: 'ws-1', workItemsProcessed: 7 },
    ])
  })

  it('lowers keys in nested objects', () => {
    const details = parseDetailsJson<{ breakdown: { orgTypeId: string }[] }>(
      '{"Breakdown":[{"OrgTypeId":"t-1"}]}',
    )

    expect(details).toEqual({ breakdown: [{ orgTypeId: 't-1' }] })
  })

  it('returns undefined for empty or malformed json', () => {
    expect(parseDetailsJson(null)).toBeUndefined()
    expect(parseDetailsJson('{not json')).toBeUndefined()
  })
})
