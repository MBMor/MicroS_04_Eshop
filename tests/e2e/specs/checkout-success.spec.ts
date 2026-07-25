import {
  expect,
  test,
  type Page,
  type Response,
} from '@playwright/test';

const productName =
  'E2E Mechanical Keyboard';

const customerUsername =
  process.env.E2E_CUSTOMER_USERNAME
  ?? 'alice.customer';

const customerPassword =
  process.env.E2E_CUSTOMER_PASSWORD
  ?? 'Alice123!';

const customerEmail =
  'alice.customer@eshop.local';

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

async function refreshProducts(
  page: Page,
): Promise<void> {
  const productsResponsePromise =
    page.waitForResponse(
      response =>
        response
          .url()
          .includes(
            '/api/v1/products',
          )
        && response
          .request()
          .method() === 'GET',
    );

  await page
    .getByRole(
      'button',
      {
        name:
          'Refresh products',
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
}

async function assertSuccessfulResponse(
  response: Response,
  errorTitle: string,
): Promise<void> {
  const responseBody =
    await response.text();

  expect(
    response.ok(),
    [
      errorTitle,
      `Status: ${response.status()}`,
      `URL: ${response.url()}`,
      `Response: ${responseBody}`,
    ].join('\n'),
  ).toBeTruthy();
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

    await refreshProducts(page);

    const productHeading =
      page.getByRole(
        'heading',
        {
          name:
            productName,
        },
      );

    await expect(
      productHeading,
    ).toBeVisible();

    const productCard =
      page.locator(
        'article',
        {
          has:
            productHeading,
        },
      );

    await expect(
      productCard,
    ).toBeVisible();

    const addToBasketResponsePromise =
      page.waitForResponse(
        response =>
          response
            .url()
            .includes(
              '/api/v1/basket/items',
            )
          && response
            .request()
            .method() === 'POST',
      );

    await productCard
      .getByRole(
        'button',
        {
          name:
            'Add to basket',
        },
      )
      .click();

    const addToBasketResponse =
      await addToBasketResponsePromise;

    await assertSuccessfulResponse(
      addToBasketResponse,
      'Add-to-basket request failed.',
    );

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
      .fill(customerEmail);

    await page
      .getByLabel(
        'Fake payment method',
      )
      .selectOption(
        'test-success',
      );

    const createOrderResponsePromise =
      page.waitForResponse(
        response =>
          response
            .url()
            .includes(
              '/api/v1/orders',
            )
          && response
            .request()
            .method() === 'POST',
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

    const createOrderResponse =
      await createOrderResponsePromise;

    await assertSuccessfulResponse(
      createOrderResponse,
      'Create-order request failed.',
    );

    await expect(page).toHaveURL(
      /\/orders\/[0-9a-f-]{36}$/i,
    );

    const currentStatusCard =
      page
        .getByRole(
          'heading',
          {
            name:
              'Current status',
          },
        )
        .locator('..');

    await expect(
      currentStatusCard,
    ).toBeVisible();

    await expect(
      currentStatusCard
        .getByText(
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
        customerEmail,
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