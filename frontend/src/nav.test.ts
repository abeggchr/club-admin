import { describe, expect, it } from 'vitest'
import { navItems } from './nav'

describe('navItems', () => {
  it('lists Persons, Jubiliäen, Rechnungen and Buchhaltung at the start, in that order', () => {
    expect(navItems.filter((i) => i.align === 'start').map((i) => i.to)).toEqual([
      '/persons',
      '/jubilees',
      '/invoices',
      '/accounting',
    ])
  })

  it('puts Settings alone at the right end, after all other items', () => {
    const last = navItems[navItems.length - 1]
    expect(last.to).toBe('/settings')
    expect(last.align).toBe('end')
    expect(navItems.filter((i) => i.align === 'end')).toHaveLength(1)
  })
})
