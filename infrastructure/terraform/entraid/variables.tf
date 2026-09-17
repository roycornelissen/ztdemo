variable "tenant_id" {
  description = "Object ID of the Entra ID tenant to deploy the app registrations into. Leave empty to use the tenant of the authenticated principal (az login context)."
  type        = string
  default     = null
}

variable "tenant_domain" {
  description = "Verified domain name of the Entra ID tenant, used for the 'Entra:Domain' setting in AccountsApi/PaymentsApi appsettings.json (Microsoft.Identity.Web MicrosoftIdentityOptions.Domain)."
  type        = string
  default     = "roadtoalm.com"
}

variable "app_suffix" {
  description = "Suffix used to name the app registrations, matching the suffix used for the rest of the Minibank deployment."
  type        = string
  default     = "minibank"
}

variable "accounts_api_identifier_uri" {
  description = "App ID URI exposed by the Accounts API. Used in client scope strings and accepted by the API as an audience."
  type        = string
  default     = "api://minibank-accounts-api"
}

variable "payments_api_identifier_uri" {
  description = "App ID URI exposed by the Payments API. Used in client scope strings and accepted by the API as an audience."
  type        = string
  default     = "api://minibank-payments-api"
}

variable "client_redirect_uris" {
  description = "Redirect URIs registered for the public client (MiniBankClient), used by MSAL's device code / interactive flows."
  type        = list(string)
  default = [
    "https://login.microsoftonline.com/common/oauth2/nativeclient",
    "http://localhost"
  ]
}

variable "swagger_ui_redirect_uris" {
  description = "Redirect URIs registered on the SPA platform for Swagger UI's browser-based authorization-code (+ PKCE) flow, one per API's fixed https launch profile port."
  type        = list(string)
  default = [
    "https://localhost:7137/swagger/oauth2-redirect.html", # AccountsApi
    "https://localhost:7296/swagger/oauth2-redirect.html", # PaymentsApi
  ]
}

variable "grant_admin_consent" {
  description = "Whether to pre-grant admin consent for the delegated permissions requested by MiniBankClient, so the device-code flow works without an interactive consent prompt. Requires the deploying principal to be a Global Administrator or Privileged Role Administrator."
  type        = bool
  default     = true
}
