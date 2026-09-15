# Well-known Microsoft Graph application, used to grant the MiniBankClient
# the delegated User.Read permission it requests before calling the APIs.
data "azuread_application_published_app_ids" "well_known" {}

data "azuread_service_principal" "msgraph" {
  client_id = data.azuread_application_published_app_ids.well_known.result["MicrosoftGraph"]
}

# ---------------------------------------------------------------------------
# Accounts API (src/AccountsApi) - exposes the Accounts.Read delegated scope.
# ---------------------------------------------------------------------------
resource "random_uuid" "accounts_read_scope" {}

resource "azuread_application" "accounts_api" {
  display_name     = "minibank-accounts-api-${var.app_suffix}"
  sign_in_audience = "AzureADMyOrg"

  identifier_uris = [var.accounts_api_identifier_uri]

  api {
    requested_access_token_version = 2

    oauth2_permission_scope {
      id                         = random_uuid.accounts_read_scope.result
      admin_consent_description  = "Allows the app to read the signed-in user's accounts."
      admin_consent_display_name = "Read accounts"
      user_consent_description   = "Allows the app to read your accounts."
      user_consent_display_name  = "Read your accounts"
      value                      = "Accounts.Read"
      type                       = "User"
      enabled                    = true
    }
  }
}

resource "azuread_service_principal" "accounts_api" {
  client_id = azuread_application.accounts_api.client_id
}

# ---------------------------------------------------------------------------
# Payments API (src/PaymentsApi) - exposes the Payment.Create delegated scope.
# ---------------------------------------------------------------------------
resource "random_uuid" "payments_create_scope" {}

resource "azuread_application" "payments_api" {
  display_name     = "minibank-payments-api-${var.app_suffix}"
  sign_in_audience = "AzureADMyOrg"

  identifier_uris = [var.payments_api_identifier_uri]

  api {
    requested_access_token_version = 2

    oauth2_permission_scope {
      id                         = random_uuid.payments_create_scope.result
      admin_consent_description  = "Allows the app to create payments on behalf of the signed-in user."
      admin_consent_display_name = "Create payments"
      user_consent_description   = "Allows the app to create payments on your behalf."
      user_consent_display_name  = "Create payments on your behalf"
      value                      = "Payment.Create"
      type                       = "User"
      enabled                    = true
    }
  }
}

resource "azuread_service_principal" "payments_api" {
  client_id = azuread_application.payments_api.client_id
}

# ---------------------------------------------------------------------------
# MiniBankClient (src/MiniBankClient) - public client using the MSAL device
# code flow, requesting user.read plus each API's exposed delegated scope.
# ---------------------------------------------------------------------------
resource "azuread_application" "client" {
  display_name                   = "minibank-client-${var.app_suffix}"
  sign_in_audience               = "AzureADMyOrg"
  fallback_public_client_enabled = true

  public_client {
    redirect_uris = var.client_redirect_uris
  }

  required_resource_access {
    resource_app_id = data.azuread_service_principal.msgraph.client_id

    resource_access {
      id   = "e1fe6dd8-ba31-4d61-89e7-88639da4683d" # User.Read (delegated)
      type = "Scope"
    }
  }

  required_resource_access {
    resource_app_id = azuread_application.accounts_api.client_id

    resource_access {
      id   = random_uuid.accounts_read_scope.result
      type = "Scope"
    }
  }

  required_resource_access {
    resource_app_id = azuread_application.payments_api.client_id

    resource_access {
      id   = random_uuid.payments_create_scope.result
      type = "Scope"
    }
  }
}

resource "azuread_service_principal" "client" {
  client_id = azuread_application.client.client_id
}

# ---------------------------------------------------------------------------
# Optional admin consent, so the device-code flow does not prompt for consent
# the first time MiniBankClient is run against the new tenant.
# ---------------------------------------------------------------------------
resource "azuread_service_principal_delegated_permission_grant" "client_to_msgraph" {
  count = var.grant_admin_consent ? 1 : 0

  service_principal_object_id          = azuread_service_principal.client.object_id
  resource_service_principal_object_id = data.azuread_service_principal.msgraph.object_id
  claim_values                         = ["User.Read"]
}

resource "azuread_service_principal_delegated_permission_grant" "client_to_accounts_api" {
  count = var.grant_admin_consent ? 1 : 0

  service_principal_object_id          = azuread_service_principal.client.object_id
  resource_service_principal_object_id = azuread_service_principal.accounts_api.object_id
  claim_values                         = ["Accounts.Read"]
}

resource "azuread_service_principal_delegated_permission_grant" "client_to_payments_api" {
  count = var.grant_admin_consent ? 1 : 0

  service_principal_object_id          = azuread_service_principal.client.object_id
  resource_service_principal_object_id = azuread_service_principal.payments_api.object_id
  claim_values                         = ["Payment.Create"]
}
