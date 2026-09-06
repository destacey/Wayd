import dayjs from 'dayjs'

/**
 * Downloads a JSON string as a file in the browser.
 */
export const downloadJson = (jsonContent: string, filename: string): void => {
  const blob = new Blob([jsonContent], {
    type: 'application/json;charset=utf-8;',
  })
  const url = URL.createObjectURL(blob)

  const link = document.createElement('a')
  link.href = url
  link.download = filename
  link.click()

  URL.revokeObjectURL(url)
}

/**
 * Downloads a JSON string as a file with a current date timestamp suffix: `${baseFilename}-${YYYY-MM-DD}.json`.
 */
export const downloadJsonWithTimestamp = (
  jsonContent: string,
  baseFilename: string,
): void => {
  const filename = `${baseFilename}-${dayjs().format('YYYY-MM-DD')}.json`
  downloadJson(jsonContent, filename)
}

