# Juno API Service Principals
Juno API services use Azure Active Directory (AAD) Apps/Service principals to enable clients to authenticate with the API services.
Each individual API service will have its own AAD App/Service Principal associated with it. When users or automated clients access 
the API, they will be essentially logging into AAD to request a token that will then be provided to the API service. This allows the
scope of the user's access to be controlled by the definitions in the AAD Service Principal.

### General Workflow
* Client calls AAD and provides a certificate (in the HTTP headers).  
* AAD returns a JSON web token (JWT) to the client caller.  
* Client calls API service/Web App and provides JWT to service (in the HTTP headers).
* Client is authenticated and given the access privileges defined on the AAD Service Principal.

### Supported Authentication Scenarios
Juno API services support a few core authentication scenarios:
* User authentication using their own AAD account.
* Client application authentication where the client presents the required certificate.

In the end, the fundamental is the same. Whether a user account or a client application logs into AAD to get the rights to
identify as the AAD Service Principal, a JSON web token (JWT) will be provided to Juno API service endpoints (see section below
for examples of JSON web tokens).

### Defining the AAD Service Principal
The following steps should be used when creating an AAD App/Service Principal for Juno API services.

##### Create the AAD Service Principal Itself
In the Azure portal under **Azure Active Directory** -> **App Registrations** create an App/Service Principal following the
naming conventions for the Juno environment for which the principal will be used.

Examples:
``` csharp
// Environment = juno-dev01
// Principal used to authenticate with the Juno Agent API
juno-dev01-agentapi-principal

// Principal used to authenticate with the Juno Execution API
juno-dev01-executionapi-principal

// Principal used to authenticate with Key Vaults in the environment
juno-dev01-keyvault-principal

// Principal used by Juno providers to authenticate with the TIP service.
juno-dev01-tip-principal
```

![](..\Juno.Documentation\Images\AADPrincipal_NamingConventions.PNG)

##### Upload the Certificate(s) For Clients to Use For Authentication
Client applications must provide a certificate in order to authenticate with AAD as the Service Principal. AAD will provide a
JSON web token (JWT) if the user presents a certificate that is supported. All certificates that are used in the environment are
preserved/maintained in the Azure Key Vault for the environment. Note that owners of the AAD Service Principal can authenticate using 
their own personal AAD user account to get the JWT.

![](..\Juno.Documentation\Images\KeyVault_Certificates.PNG)

![](..\Juno.Documentation\Images\AADPrincipal_Certificates.PNG)

##### Configure Authentication Settings
In order for the AAD App/Service Principal to provide proper callbacks to users or client applications requesting an AAD login,
the Service Principal must be configured correctly.

* Add the 'Redirect URI' for the Azure Web App for the API (remember that each API uses a single principal).
* Set the 'Implicit grant' to provide both 'Access tokens' and 'ID tokens'.
* Set the 'Supported account types' to single tenant only.

![](..\Juno.Documentation\Images\AADPrincipal_AuthenticationSettings.PNG)

##### Request Headers and JSON Web Tokens (JWT)
The following examples provide a view into the way that JSON web tokens are passed from AAD to a calling application and from
the calling application to the API service.

* The call to AAD will provide a callback that contains the JWT (as form data).

  ![](..\Juno.Documentation\Images\JWT_RequestHeader.PNG)

* The JWT is then passed to the API service in the HTTP headers using the defacto standard 'Authentication' header.

  ``` json
  "Authentication": "BEARER eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsIng1dCI6InBpVmxsb1FE..."
  ```

  The The entire JWT is included in the 'Authentication' header. The token is base-64 encoded, so it can be easily decoded to view
  the actual contents of the token.

  https://jwt.ms

  ![](..\Juno.Documentation\Images\JWT_Decoded.PNG)

  ![](..\Juno.Documentation\Images\JWT_Decoded_Claims.PNG)

  *(Example of Decoded Token)*
  ``` json
  {
      "typ": "JWT",
      "alg": "RS256",
      "x5t": "piVlloQDSMKxh1m2ygqGSVdgFpA",
      "kid": "piVlloQDSMKxh1m2ygqGSVdgFpA"
  }.{
      "aud": "d41bcb5d-1c0f-47c0-95b8-a820c92b899b",
      "iss": "https://sts.windows.net/72f988bf-86f1-41af-91ab-2d7cd011db47/",
      "iat": 1578085596,
      "nbf": 1578085596,
      "exp": 1578089496,
      "aio": "AVQAq/8NAAAAgfaH1RD5RyCSjJDIxWwOw+9FY+16IuqIiVJ/l1TSZd78zcbWbFi9ROSZtMQY6m4Gxl7NY4VC8OUAZVfWm1wqueIbLttyOZ+iYx5JHdsRoYw=",
      "amr": [
        "wia",
        "mfa"
      ],
      "family_name": "DeYoung",
      "given_name": "Bryan",
      "in_corp": "true",
      "ipaddr": "131.107.159.12",
      "name": "Bryan DeYoung",
      "nonce": "e8ae46cdd38e495da0da64ea4fe270ea_20200103211616",
      "oid": "31ac7f18-3741-43ed-b7e9-e01130c3f26f",
      "onprem_sid": "S-1-5-21-2127521184-1604012920-1887927527-5952677",
      "sub": "ZTNAa35Ac8wShKi3bd3Uh0y9G0USp4Oprq7N3NjXYKE",
      "tid": "72f988bf-86f1-41af-91ab-2d7cd011db47",
      "unique_name": "brdeyo@microsoft.com",
      "upn": "brdeyo@microsoft.com",
      "uti": "u3c0FV_6RE-3H4NHfWtQAA",
      "ver": "1.0"
  }.[Signature]
   ```
