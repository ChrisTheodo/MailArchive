import type { Locator, Page } from '@playwright/test'

export function vInput(root: Page | Locator, testId: string): Locator {
  return root.getByTestId(testId).locator('input, textarea')
}

export async function fillField(root: Page | Locator, testId: string, value: string) {
  await vInput(root, testId).fill(value)
}
