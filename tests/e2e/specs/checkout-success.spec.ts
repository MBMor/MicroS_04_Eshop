import {
  expect,
  test,
  type Page,
} from '@playwright/test';

const productName =
  'E2E Mechanical Keyboard';

const customerUsername =
  process.env.E2E_CUSTOMER_USERNAME
  ?? 'alice.customer';

const customerPassword =
  process.env.E2E_CUSTOMER_PASSWORD
  ?? 'Alice123!';

async function signInAsCustomer(
  page: Page,
): Promise<void> {
  await page
    .getByRole(
      'button',
      {
        name:
          'Sign in',
      },
    )
    .click();

  await expect(
    page.locator('#username'),
  ).toBeVisible();

  await page
    .locator('#username')
    .fill(customerUsername);

  await page
    .locator('#password')
    .fill(customerPassword);

  await page
    .locator('#kc-login')
    .click();

  await expect(
    page.getByRole(
      'heading',
      {
        name:
          'Product Catalog',
      },
    ),
  ).toBeVisible();

  await expect(
    page.getByText(
      customerUsername,
      {
        exact:
          true,
      },
    ),
  ).toBeVisible();
}

test(
  'customer completes a successful checkout',
  async ({
    page,
  }) => {
    await page.goto('/');

    await expect(
      page.getByRole(
        'heading',
        {
          name:
            'Product Catalog',
        },
      ),
    ).toBeVisible();

    await signInAsCustomer(page);

const productsResponsePromise =
  page.waitForResponse(
    response =>
      response
        .url()
        .includes('/api/v1/products')
      && response
        .request()
        .method() === 'GET',
  );

await page
  .getByRole(
    'button',
    {
      name: 'Refresh products',
    },
  )
  .click();

const productsResponse =
  await productsResponsePromise;

const productsResponseBody =
  await productsResponse.text();

expect(
  productsResponse.ok(),
  [
    'Catalog request failed.',
    `Status: ${productsResponse.status()}`,
    `URL: ${productsResponse.url()}`,
    `Response: ${productsResponseBody}`,
  ].join('\n'),
).toBeTruthy();

expect(
  productsResponseBody,
).toContain(productName);

await expect(
  page.getByRole(
    'heading',
    {
      name: productName,
    },
  ),
).toBeVisible();

    await page
      .getByRole(
        'button',
        {
          name:
            'Add to basket',
        },
      )
      .click();

    await expect(
      page.getByText(
        `${productName} was added to the basket.`,
      ),
    ).toBeVisible();

    await page
      .getByRole(
        'link',
        {
          name:
            'Open basket',
        },
      )
      .click();

    await expect(
      page.getByRole(
        'heading',
        {
          name:
            'Basket',
        },
      ),
    ).toBeVisible();

    await expect(
      page.getByRole(
        'heading',
        {
          name:
            productName,
        },
      ),
    ).toBeVisible();

    await page
      .getByRole(
        'link',
        {
          name:
            'Continue to checkout',
        },
      )
      .click();

    await expect(
      page.getByRole(
        'heading',
        {
          name:
            'Checkout',
        },
      ),
    ).toBeVisible();

    await page
      .getByLabel('Email')
      .fill(
        'alice.customer@eshop.local',
      );

    await page
      .getByLabel(
        'Fake payment method',
      )
      .selectOption(
        'test-success',
      );

    await page
      .getByRole(
        'button',
        {
          name:
            'Create order',
        },
      )
      .click();

    await expect(page).toHaveURL(
      /\/orders\/[0-9a-f-]{36}$/i,
    );

    await expect(
      page.getByRole(
        'heading',
        {
          name:
            'Current status',
        },
      ),
    ).toBeVisible();

    await expect(
      page.getByText(
        'Confirmed',
        {
          exact:
            true,
        },
      ),
    ).toBeVisible({
      timeout:
        45_000,
    });

    await expect(
      page.getByText(
        'alice.customer@eshop.local',
        {
          exact:
            true,
        },
      ),
    ).toBeVisible();

    await expect(
      page.getByText(
        'test-success',
        {
          exact:
            true,
        },
      ),
    ).toBeVisible();
  },
);