import {
  escapeCsv,
  generateCsv,
  downloadCsv,
  downloadCsvWithTimestamp,
} from './csv-utils'

jest.mock('dayjs', () => {
  const originalDayjs = jest.requireActual('dayjs')
  return jest.fn((date?: any) => {
    const instance = originalDayjs(date)
    return {
      ...instance,
      format: jest.fn((format: string) => {
        if (format === 'YYYY-MM-DD') {
          return '2025-12-21'
        }
        return instance.format(format)
      }),
    }
  })
})

describe('csv-utils', () => {
  describe('escapeCsv', () => {
    it('should return empty string for null or undefined', () => {
      expect(escapeCsv(null)).toBe('')
      expect(escapeCsv(undefined)).toBe('')
    })

    it('should return plain string unchanged if no special characters', () => {
      expect(escapeCsv('hello')).toBe('hello')
      expect(escapeCsv('test123')).toBe('test123')
    })

    it('should escape double quotes by doubling them', () => {
      expect(escapeCsv('hello "world"')).toBe('"hello ""world"""')
      expect(escapeCsv('"quoted"')).toBe('"""quoted"""')
    })

    it('should wrap values with comma in quotes', () => {
      expect(escapeCsv('hello,world')).toBe('"hello,world"')
    })

    it('should wrap values with newline in quotes', () => {
      expect(escapeCsv('hello\nworld')).toBe('"hello\nworld"')
    })

    it('should wrap values with carriage return in quotes', () => {
      expect(escapeCsv('hello\rworld')).toBe('"hello\rworld"')
    })

    it('should wrap values with quotes and other special chars', () => {
      expect(escapeCsv('hello "world", test')).toBe('"hello ""world"", test"')
    })

    it('should convert numbers to strings', () => {
      expect(escapeCsv(123)).toBe('123')
      expect(escapeCsv(45.67)).toBe('45.67')
    })

    it('should convert boolean to string', () => {
      expect(escapeCsv(true)).toBe('true')
      expect(escapeCsv(false)).toBe('false')
    })
  })

  describe('generateCsv', () => {
    it('should generate CSV with headers only', () => {
      const headers = ['Name', 'Age', 'City']
      const rows: unknown[][] = []
      const csv = generateCsv(headers, rows)

      expect(csv).toBe('Name,Age,City')
    })

    it('should generate CSV with headers and single row', () => {
      const headers = ['Name', 'Age', 'City']
      const rows = [['John', 30, 'NYC']]
      const csv = generateCsv(headers, rows)

      expect(csv).toBe('Name,Age,City\nJohn,30,NYC')
    })

    it('should generate CSV with multiple rows', () => {
      const headers = ['Name', 'Age']
      const rows = [
        ['John', 30],
        ['Jane', 25],
      ]
      const csv = generateCsv(headers, rows)

      expect(csv).toBe('Name,Age\nJohn,30\nJane,25')
    })

    it('should properly escape values in CSV', () => {
      const headers = ['Name', 'Description']
      const rows = [['John Doe', 'Works in "Tech" division']]
      const csv = generateCsv(headers, rows)

      expect(csv).toBe(
        'Name,Description\nJohn Doe,"Works in ""Tech"" division"',
      )
    })

    it('should handle values with commas', () => {
      const headers = ['Name', 'Address']
      const rows = [['John', 'New York, NY']]
      const csv = generateCsv(headers, rows)

      expect(csv).toBe('Name,Address\nJohn,"New York, NY"')
    })

    it('should handle null and undefined values', () => {
      const headers = ['Name', 'Age', 'City']
      const rows = [['John', null, undefined]]
      const csv = generateCsv(headers, rows)

      expect(csv).toBe('Name,Age,City\nJohn,,')
    })
  })

  describe('downloadCsv', () => {
    let mockLink: any

    beforeEach(() => {
      // Mock URL.createObjectURL and URL.revokeObjectURL
      global.URL.createObjectURL = jest.fn(() => 'blob:mock-url')
      global.URL.revokeObjectURL = jest.fn()

      // Mock document.createElement and element methods
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

    it('should create blob with correct content and type', () => {
      const csvContent = 'Name,Age\nJohn,30'
      downloadCsv(csvContent, 'test.csv')

      expect(global.URL.createObjectURL).toHaveBeenCalledWith(
        expect.objectContaining({
          type: 'text/csv;charset=utf-8;',
        }),
      )
    })

    it('should prepend a UTF-8 BOM so Excel decodes unicode (e.g. emoji) correctly', () => {
      // Arrange — capture the parts passed to the Blob constructor
      const blobParts: unknown[][] = []
      const RealBlob = global.Blob
      global.Blob = jest.fn((parts: unknown[], opts?: BlobPropertyBag) => {
        blobParts.push(parts)
        return new RealBlob(parts as BlobPart[], opts)
      }) as unknown as typeof Blob

      const BOM = '\uFEFF'
      try {
        // Act — content with a multibyte character (rocket emoji)
        downloadCsv('Team\nCore Services 🚀', 'test.csv')

        // Assert — the blob's first part is the UTF-8 BOM, and the content
        // (with the emoji intact) follows.
        const parts = blobParts[0]
        expect(parts[0]).toBe(BOM)
        expect(String(parts.join(''))).toContain('🚀')
      } finally {
        global.Blob = RealBlob
      }
    })

    it('should set link href and download attributes', () => {
      const csvContent = 'Name,Age\nJohn,30'
      const filename = 'test.csv'

      downloadCsv(csvContent, filename)

      expect(mockLink.href).toBe('blob:mock-url')
      expect(mockLink.download).toBe('test.csv')
    })

    it('should click the link to trigger download', () => {
      const csvContent = 'Name,Age\nJohn,30'
      downloadCsv(csvContent, 'test.csv')

      expect(mockLink.click).toHaveBeenCalled()
    })

    it('should revoke the object URL after download', () => {
      const csvContent = 'Name,Age\nJohn,30'
      downloadCsv(csvContent, 'test.csv')

      expect(global.URL.revokeObjectURL).toHaveBeenCalledWith('blob:mock-url')
    })
  })

  describe('downloadCsvWithTimestamp', () => {
    let mockLink: any

    beforeEach(() => {
      global.URL.createObjectURL = jest.fn(() => 'blob:mock-url')
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

    it('should include timestamp in filename', () => {
      const csvContent = 'Name,Age\nJohn,30'
      const baseFilename = 'project-tasks'

      downloadCsvWithTimestamp(csvContent, baseFilename)

      expect(mockLink.download).toBe('project-tasks-2025-12-21.csv')
    })

    it('should call downloadCsv with generated filename', () => {
      const csvContent = 'Name,Age\nJohn,30'
      const baseFilename = 'data-export'

      downloadCsvWithTimestamp(csvContent, baseFilename)

      expect(global.URL.createObjectURL).toHaveBeenCalled()
      expect(global.URL.revokeObjectURL).toHaveBeenCalled()
      expect(mockLink.download).toBe('data-export-2025-12-21.csv')
    })
  })
})
