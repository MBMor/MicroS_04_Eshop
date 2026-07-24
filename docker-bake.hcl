variable "IMAGE_TAG" {
  default = "ci"
}

group "default" {
  targets = [
    "api-gateway",
    "basket-service",
    "catalog-service",
    "inventory-service",
    "notifications-service",
    "orders-service",
    "payments-service",
    "rabbitmq-topology-initializer",
    "frontend"
  ]
}

target "_backend" {
  context    = "."
  dockerfile = "src/backend/Dockerfile"

  platforms = [
    "linux/amd64"
  ]

  args = {
    BUILD_CONFIGURATION = "Release"
  }
}

target "api-gateway" {
  inherits = ["_backend"]

  tags = [
    "eshop/api-gateway:${IMAGE_TAG}"
  ]

  args = {
    PROJECT_PATH = "src/backend/gateways/ApiGateway/ApiGateway.csproj"
    APP_DLL       = "ApiGateway.dll"
  }
}

target "basket-service" {
  inherits = ["_backend"]

  tags = [
    "eshop/basket-service:${IMAGE_TAG}"
  ]

  args = {
    PROJECT_PATH = "src/backend/services/BasketService/BasketService.csproj"
    APP_DLL       = "BasketService.dll"
  }
}

target "catalog-service" {
  inherits = ["_backend"]

  tags = [
    "eshop/catalog-service:${IMAGE_TAG}"
  ]

  args = {
    PROJECT_PATH = "src/backend/services/CatalogService/CatalogService.csproj"
    APP_DLL       = "CatalogService.dll"
  }
}

target "inventory-service" {
  inherits = ["_backend"]

  tags = [
    "eshop/inventory-service:${IMAGE_TAG}"
  ]

  args = {
    PROJECT_PATH = "src/backend/services/InventoryService/InventoryService.csproj"
    APP_DLL       = "InventoryService.dll"
  }
}

target "notifications-service" {
  inherits = ["_backend"]

  tags = [
    "eshop/notifications-service:${IMAGE_TAG}"
  ]

  args = {
    PROJECT_PATH = "src/backend/services/NotificationsService/NotificationsService.csproj"
    APP_DLL       = "NotificationsService.dll"
  }
}

target "orders-service" {
  inherits = ["_backend"]

  tags = [
    "eshop/orders-service:${IMAGE_TAG}"
  ]

  args = {
    PROJECT_PATH = "src/backend/services/OrdersService/OrdersService.csproj"
    APP_DLL       = "OrdersService.dll"
  }
}

target "payments-service" {
  inherits = ["_backend"]

  tags = [
    "eshop/payments-service:${IMAGE_TAG}"
  ]

  args = {
    PROJECT_PATH = "src/backend/services/PaymentsService/PaymentsService.csproj"
    APP_DLL       = "PaymentsService.dll"
  }
}

target "rabbitmq-topology-initializer" {
  inherits = ["_backend"]

  tags = [
    "eshop/rabbitmq-topology-initializer:${IMAGE_TAG}"
  ]

  args = {
    PROJECT_PATH = "src/backend/tools/RabbitMq.TopologyInitializer/RabbitMq.TopologyInitializer.csproj"
    APP_DLL       = "RabbitMq.TopologyInitializer.dll"
  }
}

target "frontend" {
  context    = "src/frontend"
  dockerfile = "Dockerfile"
  target     = "production"

  platforms = [
    "linux/amd64"
  ]

  tags = [
    "eshop/frontend:${IMAGE_TAG}"
  ]
}
