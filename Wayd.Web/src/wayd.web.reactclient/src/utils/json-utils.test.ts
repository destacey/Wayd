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

  beforeEach(() => {
    global.URL.createObjectURL = jest.fn(() => 'blob:mock-json-url')
    global.URL.revokeObjectURL = jest.fn()

    mockLink = {
      href: '',
      download: '',
      click: jest.fn(),
    }
    jest.spyOn(document, 'createElement').mockReturnValue(mockLink as any)
  })

  afterEach(() => {
    jest.restoreAllMocks()
  })

  describe('downloadJson', () => {
    it('creates a blob with json content and triggers download', () => {
      const jsonContent = JSON.stringify({ name: 'Alpha Team' }, null, 2)
      downloadJson(jsonContent, 'team.json')

      expect(global.URL.createObjectURL).toHaveBeenCalledWith(
        expect.objectContaining({
          type: 'application/json;charset=utf-8;',
        }),
      )
      expect(mockLink.href).toBe('blob:mock-json-url')
      expect(mockLink.download).toBe('team.json')
      expect(mockLink.click).toHaveBeenCalled()
      expect(global.URL.revokeObjectURL).toHaveBeenCalledWith(
        'blob:mock-json-url',
      )
    })
  })

  describe('downloadJsonWithTimestamp', () => {
    it('appends timestamp to filename and triggers download', () => {
      const jsonContent = JSON.stringify({ events: [] })
      downloadJsonWithTimestamp(jsonContent, 'team-alpha-activities')

      expect(mockLink.download).toBe('team-alpha-activities-2026-09-06.json')
      expect(mockLink.click).toHaveBeenCalled()
    })
  })
})

