/** "Product by product": how a per-group import applies, as a heading. */
export const byGroup = (noun: string) =>
  `${noun.charAt(0).toUpperCase()}${noun.slice(1)} by ${noun}`
