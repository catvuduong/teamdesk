import { readFileSync } from 'node:fs'
import { test, expect, type Page } from '@playwright/test'

async function signInAsAgent(page: Page) {
  await page.goto('/')
  // The API hashes passwords with bcrypt, which is slow by design, so the first sign-in can outlast the default 5s expect timeout
  const login = page.waitForResponse((response) => response.url().endsWith('/auth/login') && response.ok())
  await page.getByTestId('login-demo-agent').click()
  await login
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible({ timeout: 15_000 })
}

test('the at-risk quick filter shows only at-risk tickets', async ({ page }) => {
  await signInAsAgent(page)
  await page.goto('/tickets')

  await page.getByRole('button', { name: 'At risk' }).click()

  await expect(page).toHaveURL(/sla=at_risk/)
  // The seeded clocks move with time, so the filter may match nothing by the time this runs; either way no other state shows.
  // Locator assertions retry until the filtered list has replaced the unfiltered one.
  await expect(page.getByTestId('ticket-row').first().or(page.getByText('No tickets match these filters'))).toBeVisible()
  await expect(page.locator('[data-testid="sla-badge"]:not([data-state="at_risk"])')).toHaveCount(0)
})

test('the export button downloads the tickets as CSV', async ({ page }) => {
  await signInAsAgent(page)
  await page.goto('/tickets?priority=urgent')

  const downloaded = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Export CSV' }).click()
  const download = await downloaded

  expect(download.suggestedFilename()).toBe('tickets.csv')
  const text = readFileSync((await download.path())!, 'utf8')
  expect(text.split('\r\n')[0]).toBe(
    'Number,Title,Status,Priority,Category,Requester,Assignee,Created at,Updated at,Resolved at,SLA state',
  )
})
