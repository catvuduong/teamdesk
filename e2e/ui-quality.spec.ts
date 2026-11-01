import { test, expect, type Page } from '@playwright/test'

async function signIn(page: Page, testId: string) {
  await page.goto('/')
  // The API hashes passwords with bcrypt, which is slow by design, so the first sign-in can outlast the default 5s expect timeout
  const login = page.waitForResponse((response) => response.url().endsWith('/auth/login') && response.ok())
  await page.getByTestId(testId).click()
  await login
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible({ timeout: 15_000 })
}

/** The page is no wider than the phone screen: nothing forces a sideways scroll. */
async function expectNoHorizontalScroll(page: Page, label: string) {
  const overflow = await page.evaluate(() => {
    const wide: string[] = []
    document.querySelectorAll('body *').forEach((element) => {
      const box = element.getBoundingClientRect()
      if (box.right > document.documentElement.clientWidth + 1 && !element.closest('.nav')) {
        wide.push(`${element.tagName.toLowerCase()}.${String((element as HTMLElement).className).trim()} right=${Math.round(box.right)}`)
      }
    })
    return { scroll: document.documentElement.scrollWidth, client: document.documentElement.clientWidth, wide: wide.slice(0, 6) }
  })
  expect(overflow.scroll, `${label} scrolls sideways; wide elements: ${overflow.wide.join(' | ') || 'none outside .nav'}`).toBeLessThanOrEqual(overflow.client)
}

test.describe('design system', () => {
  test('the stylesheet is applied to the login page', async ({ page }) => {
    await page.goto('/')
    await expect(page.getByTestId('health')).toContainText('api ok')

    const styles = await page.evaluate(() => {
      const button = document.querySelector('[data-testid="login-demo-agent"]')!
      const computed = getComputedStyle(button)
      return {
        background: computed.backgroundColor,
        font: getComputedStyle(document.body).fontFamily,
        accent: getComputedStyle(document.documentElement).getPropertyValue('--accent').trim(),
      }
    })

    expect(styles.accent).not.toBe('')
    expect(styles.background).not.toBe('rgba(0, 0, 0, 0)')
    expect(styles.font).toContain('system-ui')
  })

  test('the theme toggle switches between light and dark', async ({ page }) => {
    await signIn(page, 'login-demo-agent')

    const toggle = page.getByRole('button', { name: 'Switch to dark theme' })
    await toggle.click()
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
    await expect(page.getByRole('button', { name: 'Switch to light theme' })).toBeVisible()

    const dark = await page.evaluate(() => getComputedStyle(document.body).backgroundColor)
    await page.getByRole('button', { name: 'Switch to light theme' }).click()
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'light')

    const light = await page.evaluate(() => getComputedStyle(document.body).backgroundColor)
    expect(dark).not.toBe(light)
  })

  test('no page scrolls sideways at 390px for staff', async ({ page }) => {
    test.setTimeout(90_000)
    await page.setViewportSize({ width: 390, height: 800 })

    await page.goto('/')
    await expect(page.getByTestId('health')).toContainText('api ok')
    await expectNoHorizontalScroll(page, 'login')

    await signIn(page, 'login-demo-agent')
    for (const path of ['/dashboard', '/tickets', '/tickets/new', '/sla', '/users', '/kb']) {
      await page.goto(path)
      await page.getByRole('heading', { level: 1 }).first().waitFor()
      await expectNoHorizontalScroll(page, path)
    }
  })

  test('no page scrolls sideways at 390px for customers', async ({ page }) => {
    test.setTimeout(90_000)
    await page.setViewportSize({ width: 390, height: 800 })

    await signIn(page, 'login-demo-customer')
    for (const path of ['/tickets', '/tickets/new', '/kb']) {
      await page.goto(path)
      await page.getByRole('heading', { level: 1 }).first().waitFor()
      await expectNoHorizontalScroll(page, path)
    }
  })
})
