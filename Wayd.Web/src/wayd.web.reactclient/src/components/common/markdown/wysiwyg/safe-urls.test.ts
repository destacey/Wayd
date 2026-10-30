import {
  isSafeImageSrc,
  isSafeLinkHref,
  toMarkdownDestination,
} from './safe-urls'

describe('isSafeLinkHref', () => {
  it.each([
    'https://wayd.dev',
    'http://wayd.dev/a?b=c',
    'mailto:someone@example.com',
    '/ppm/projects/12',
    '#section',
    'relative/page',
  ])('allows %s', (href) => {
    // Arrange / Act / Assert
    expect(isSafeLinkHref(href)).toBe(true)
  })

  it.each([
    'javascript:alert(1)',
    'JavaScript:alert(1)',
    ' javascript:alert(1)',
    'java\nscript:alert(1)',
    'java\tscript:alert(1)',
    'vbscript:msgbox(1)',
    'data:text/html,<script>alert(1)</script>',
    'file:///etc/passwd',
    'javascript&colon;alert(1)',
    'javascript&#58;alert(1)',
    'javascript%3Aalert(1)',
    '',
    undefined,
  ])('refuses %p', (href) => {
    // Arrange / Act / Assert
    expect(isSafeLinkHref(href)).toBe(false)
  })
})

describe('isSafeImageSrc', () => {
  it.each([
    ['https://wayd.dev/logo.png', true],
    ['http://wayd.dev/logo.png', true],
    ['data:image/png;base64,AAAA', false],
    ['file:///C:/image.png', false],
    ['javascript:alert(1)', false],
    ['/relative.png', false],
    [null, false],
  ])('%p is %p', (src, expected) => {
    // Arrange / Act / Assert
    expect(isSafeImageSrc(src)).toBe(expected)
  })
})

describe('toMarkdownDestination', () => {
  it.each([
    ['https://ex.com/my page', 'https://ex.com/my%20page'],
    ['https://ex.com/a)b', 'https://ex.com/a%29b'],
    ['https://ex.com/(x)', 'https://ex.com/%28x%29'],
    ['https://ex.com/<x>', 'https://ex.com/%3Cx%3E'],
    ['https://ex.com/a\tb', 'https://ex.com/a%09b'],
    ['https://ex.com/ok?a=1&b=2#c', 'https://ex.com/ok?a=1&b=2#c'],
  ])('writes %p as %p', (url, expected) => {
    // Arrange / Act / Assert
    expect(toMarkdownDestination(url)).toBe(expected)
  })
})
