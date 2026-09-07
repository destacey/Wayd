import { downloadJson, downloadJsonWithTimestamp } from './json-utils'

jest.mock('dayjs', () => {
  const originalDayjs = jest.requireActual('dayjs')
  return jest.fn((date?: any) => {
    const instance = originalDayjs(date)
    return {
      ...instance,
      format: jest.fn((format: string) => {
        if (format === 'YYYY-MM-DD') {
          return '2026-09-06'
        }
        return instance.format(format)
      }),
    }
  })
})

describe('json-utils', () => {
  let mockLink: any
  let appendChildSpy: jest.SpyInstance
  let removeChildSpy: jest.SpyInstance

  beforeEach(() => {
    global.URL.createObjectURL = jest.fn(() => 'blob:mock-json-url')
    global.URL.revokeObjectURL = jest.fn()

    mockLink = {
      href: '',
      download: '',
      click: jest.fn(),
    }
    jest.spyOn(document, 'createElement').mockReturnValue(mockLink as any)
    appendChildSpy = jest
      .spyOn(document.body, 'appendChild')
      .mockImplementation(() => mockLink as any)
    removeChildSpy = jest
      .spyOn(document.body, 'removeChild')
      .mockImplementation(() => mockLink as any)
  })

  afterEach(() => {
    jest.restoreAllMocks()
  })

  describe('downloadJson', () => {
    it('creates a blob with json content and triggers download', () => {
      jest.useFakeTimers()

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
      jest.advanceTimersByTime(100)
      expect(global.URL.revokeObjectURL).toHaveBeenCalledWith(
        'blob:mock-json-url',
      )

      jest.useRealTimers()
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

