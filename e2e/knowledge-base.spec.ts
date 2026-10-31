import { test, expect, type Page } from '@playwright/test'

async function signInAsCustomer(page: Page) {
  await page.goto('/')
  // The API hashes passwords with bcrypt, which is slow by design, so the first sign-in can outlast the default 5s expect timeout
  const login = page.waitForResponse((response) => response.url().endsWith('/auth/login') && response.ok())
  await page.getByTestId('login-demo-customer').click()
  await login
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible({ timeout: 15_000 })
}

test('a customer finds a help article from the new-ticket form', async ({ page }) => {
  await signInAsCustomer(page)
  await page.goto('/tickets/new')

  await page.getByLabel('Title').fill('Password reset link expired')

  const suggestion = page.getByTestId('ticket-suggestion').filter({ hasText: 'Reset your password' })
  await expect(suggestion).toBeVisible()

  // The article opens in a new tab, so the ticket draft stays in this one
  const article = page.waitForEvent('popup')
  await suggestion.click()
  const popup = await article

  await expect(popup).toHaveURL(/\/kb\/reset-your-password$/)
  await expect(popup.getByRole('heading', { level: 1, name: 'Reset your password' })).toBeVisible()
  await expect(popup.getByText('Use the reset link from the email we sent you.')).toBeVisible()
})
