using System;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Microsoft.Rest.Azure.Authentication;

namespace AzureKeyVaultManagedStorageSamples
{
    /// <summary>
    /// Represents the Azure context of the client running the samples - tenant, subscription, client id and credentials.
    /// </summary>
    public sealed class ClientContext
    {
        private static ClientCredential _servicePrincipalCredential = null;
        private static DeviceCodeResult _deviceCode = null;

        #region construction
        public static ClientContext Build(string tenantId, string vaultMgmtAppId, string vaultMgmtAppSecret, string subscriptionId, string resourceGroupName, string location, string vaultName, string storageAccountName, string storageAccountResourceId)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(vaultMgmtAppId)) throw new ArgumentException(nameof(vaultMgmtAppId));
            if (String.IsNullOrWhiteSpace(vaultMgmtAppSecret)) throw new ArgumentException(nameof(vaultMgmtAppSecret));
            if (String.IsNullOrWhiteSpace(subscriptionId)) throw new ArgumentException(nameof(subscriptionId));
            if (String.IsNullOrWhiteSpace(resourceGroupName)) throw new ArgumentException(nameof(resourceGroupName));
            if (String.IsNullOrWhiteSpace(storageAccountName)) throw new ArgumentException(nameof(storageAccountName));
            if (String.IsNullOrWhiteSpace(storageAccountResourceId)) throw new ArgumentException(nameof(storageAccountResourceId)); 

            return new ClientContext
            {
                TenantId = tenantId,
                VaultMgmtApplicationId = vaultMgmtAppId,
                SubscriptionId = subscriptionId,
                ResourceGroupName = resourceGroupName,
                PreferredLocation = location ?? "southcentralus",
                VaultName = vaultName ?? "keyvaultsample",
                StorageAccountName = storageAccountName,
                StorageAccountResourceId = storageAccountResourceId
            };
        }
        #endregion

        #region properties
        public string TenantId { get; set; }

        public string VaultMgmtApplicationId { get; set; }

        public string SubscriptionId { get; set; }

        public string PreferredLocation { get; set; }

        public string VaultName { get; set; }

        public string ResourceGroupName { get; set; }

        public string StorageAccountName { get; set; }

        public string StorageAccountResourceId { get; set; }
        #endregion

        #region authentication helpers
        /// <summary>
        /// Returns a task representing the attempt to log in to Azure public as the specified
        /// service principal, with the specified credential.
        /// </summary>
        /// <param name="certificateThumbprint"></param>
        /// <returns></returns>
        public static Task<ServiceClientCredentials> GetServiceCredentialsAsync(string tenantId, string applicationId, string appSecret)
        {
            if (_servicePrincipalCredential == null)
            {
                _servicePrincipalCredential = new ClientCredential(applicationId, appSecret);
            }

            return ApplicationTokenProvider.LoginSilentAsync(
                tenantId,
                _servicePrincipalCredential,
                ActiveDirectoryServiceSettings.Azure,
                TokenCache.DefaultShared);
        }

        public static async Task<string> AcquireUserAccessTokenAsync(string authority, string resource, string scope)
        {
            var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
            if (_deviceCode == null)
            {
                _deviceCode = await context.AcquireDeviceCodeAsync(resource, SampleConstants.WellKnownClientId).ConfigureAwait(false);

                Console.WriteLine("############################################################################################");
                Console.WriteLine("To continue with the test run, please do the following:");
                Console.WriteLine($"1. Navigate to: {_deviceCode.VerificationUrl}");
                Console.WriteLine($"2. Insert the following user code: {_deviceCode.UserCode}");
                Console.WriteLine("3. Login with your username and password credentials.");
                Console.WriteLine("############################################################################################");
            }

            var result = await context.AcquireTokenByDeviceCodeAsync(_deviceCode).ConfigureAwait(false);
            return result.AccessToken;
        }
        #endregion
    }
}
