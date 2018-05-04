using System.Collections.Generic;
using System.Net;

namespace AzureKeyVaultManagedStorageSamples
{
    public static class SampleConstants
    {
        /// <summary>
        /// Constants representing the configuration setting keys.
        /// </summary>
        public static class ConfigKeys
        {
            public static readonly string TenantId = "TenantId";
            public static readonly string VaultName = "VaultName";
            public static readonly string VaultLocation = "VaultLocation";
            public static readonly string ResourceGroupName = "ResourceGroupName";
            public static readonly string SubscriptionId = "SubscriptionId";

            // coordinates of the AD application used to access/manage the vault
            public static readonly string VaultMgmtAppId = "ApplicationId";
            public static readonly string VaultMgmtAppSecret = "ApplicationSecret";

            // coordinates of storage account
            public static readonly string StorageAccountName = "StorageAccountName";
            public static readonly string StorageAccountResourceId = "StorageAccountResourceId";
        }

        public static string WellKnownClientId
        {
            // Native AD app id with permissions in the subscription
            // Consider fetching it from configuration.
            get
            {
                return "54d5b1e9-5f5c-48f1-8483-d72471cbe7e7";
            }
        }

        public static string SasTemplateUri
        {
            // sample access token, using https protocol, of sas type 'account', valid for all services (blob, file, queue and table) with all permissions (racupwdl).
            // Obtained using standard Storage PowerShell cmdlets; alternatively, can be built following SAS syntax. 
            // See https://docs.microsoft.com/en-us/rest/api/storageservices/Constructing-an-Account-SAS for further details.
            get
            {
                return "?sv=2017-07-29&sig=t2Mp26dSyExqffAu5q8omPKaxWNPxx5fqKJjbi8fIsQ%3D&spr=https&st=2018-04-25T01%3A24%3A24Z&se=2018-05-26T01%3A24%3A24Z&srt=sco&ss=bfqt&sp=racupwdl";
            }
        }

        public enum SasType
        {
            // KV expects a case-matching value
            account,
            service
        }

        /// <summary>
        /// Predetermined policies for retrying KeyVault-bound requests.
        /// </summary>
        public static class RetryPolicies
        {
            /// <summary>
            /// status codes for retriable operations.
            /// </summary>
            public static HashSet<HttpStatusCode> SuccessStatusCodes
                = new HashSet<HttpStatusCode>(new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.Accepted, HttpStatusCode.NoContent });

            public static HashSet<HttpStatusCode> SoftDeleteRetriableStatusCodes
                = new HashSet<HttpStatusCode>(new List<HttpStatusCode> { HttpStatusCode.Conflict, HttpStatusCode.NotFound });

            public static HashSet<HttpStatusCode> AbortStatusCodes
                = new HashSet<HttpStatusCode>(new List<HttpStatusCode> { HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError, HttpStatusCode.Forbidden });


            /// <summary>
            /// Number of seconds to wait after a first, failed attempt to execute a soft-delete-related operation (such as delete, recover, purge).
            /// </summary>
            private static int SoftDeleteInitialBackoff = 15;
            private static int SoftDeleteMaxAttempts = 3;

            /// <summary>
            /// Standard retry policy for soft-delete-related operations which attempt to modify transitioning entities.
            /// </summary>
            public static RetryPolicy DefaultSoftDeleteRetryPolicy = new RetryPolicy(
                SoftDeleteInitialBackoff,
                SoftDeleteMaxAttempts,
                continueOn: SuccessStatusCodes,
                retryOn: SoftDeleteRetriableStatusCodes,
                abortOn: AbortStatusCodes);

            /// <summary>
            /// Standard retry policy for soft-delete-related operations which attempt to consume the outcome of an async operation.
            /// </summary>
            public static RetryPolicy WaitForAsyncDeletionRetryPolicy = new RetryPolicy(
                SoftDeleteInitialBackoff,
                SoftDeleteMaxAttempts,
                continueOn: new HashSet<HttpStatusCode> { HttpStatusCode.NotFound },                                            // keep spinning until entity is marked as deleted
                retryOn: new HashSet<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.Accepted, HttpStatusCode.Conflict },   // retry on success and conflicts
                abortOn: AbortStatusCodes);
        }
    }
}
