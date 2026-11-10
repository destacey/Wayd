// @vitest-environment jsdom
import type { MockInstance } from 'vitest'
import { downloadJson, downloadJsonWithTimestamp } from './json-utils'

describe('json-utils', () => {
  let mockLink: any
  let appendChildSpy: MockInstance
  let removeChildSpy: MockInstance

  beforeEach(() => {
    // Only Date is faked, so the filename's date is fixed and nothing else waits on fake timers.
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 8, 6, 12))
    global.URL.createObjectURL = vi.fn(() => 'blob:mock-json-url')
    global.URL.revokeObjectURL = vi.fn()

    mockLink = {
      href: '',
      download: '',
      click: vi.fn(),
    }
    vi.spyOn(document, 'createElement').mockReturnValue(mockLink as any)
    appendChildSpy = vi
      .spyOn(document.body, 'appendChild')
      .mockImplementation(() => mockLink as any)
    removeChildSpy = vi
      .spyOn(document.body, 'removeChild')
      .mockImplementation(() => mockLink as any)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('downloadJson', () => {
    it('creates a blob with json content and triggers download', () => {
      vi.useFakeTimers()

      const jsonContent = JSON.stringify({ name: 'Alpha Team' }, null, 2)
      downloadJson(jsonContent, 'team.json')

      expect(global.URL.createObjectURL).toHaveBeenCalledWith(
        expect.objectContaining({
          type: 'application/json;charset=utf-8;',
        }),
      )
      expect(mockLink.href).toBe('blob:mock-json-url')
      expect(mockLink.download).toBe('team.json')
      expect(appendChildSpy).toHaveBeenCalledWith(mockLink)
      expect(mockLink.click).toHaveBeenCalled()
      expect(removeChildSpy).toHaveBeenCalledWith(mockLink)

      // revokeObjectURL called after timeout
      expect(global.URL.revokeObjectURL).not.toHaveBeenCalled()
      vi.advanceTimersByTime(100)
      expect(global.URL.revokeObjectURL).toHaveBeenCalledWith(
        'blob:mock-json-url',
      )

      vi.useRealTimers()
    })
  })

  describe('downloadJsonWithTimestamp', () => {
    it('appends timestamp to filename and triggers download', () => {
      const jsonContent = JSON.stringify({ events: [] })
      downloadJsonWithTimestamp(jsonContent, 'team-alpha-activities')

      expect(mockLink.download).toBe('team-alpha-activities-2026-09-06.json')
      expect(appendChildSpy).toHaveBeenCalledWith(mockLink)
      expect(mockLink.click).toHaveBeenCalled()
      expect(removeChildSpy).toHaveBeenCalledWith(mockLink)
    })
  })
})
