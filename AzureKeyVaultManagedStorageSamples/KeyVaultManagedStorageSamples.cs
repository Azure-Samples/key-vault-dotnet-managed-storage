using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Rest.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace AzureKeyVaultManagedStorageSamples
{
    public sealed class KeyVaultManagedStorageSamples : KeyVaultSampleBase
    {
        /// <summary>
        /// Demonstrates management of KeyVault-managed storage accounts.
        /// </summary>
        /// <returns>Task representing the asynchronous execution of this method.</returns>
        internal static async Task DemonstrateStorageAccountManagementAsync()
        {
            // This sample demonstrates the management operations applicable to
            // KeyVault-managed storage accounts, and performs the following workflow:
            //
            // - list existing storage accounts in a vault
            // - if the expected sample storage account does not exist, it will be created
            // - attempt to retrieve an existing managed storage account
            // - update the storage account, rotating the key on demand
            // - backup a managed storage account
            // - delete a managed storage account
            // - if the vault is soft-delete enabled:
            //      - retrieve a deleted managed storage account
            //      - recover the deleted managed storage account
            //      - delete the managed storage account 
            //      - permanently delete the managed storage account (purge)
            // - restore a managed storage account from a backup

            // instantiate the samples object
            var sample = new KeyVaultManagedStorageSamples();
            var rgName = sample.context.ResourceGroupName;
            var vaultName = sample.context.VaultName;
            var managedStorageName = "msakmgmtsample";
            var storageAccountName = sample.context.StorageAccountName;
            var storageAccountResId = sample.context.StorageAccountResourceId;

            // retrieve the vault or create one if it doesn't exist
            var vault = await sample.CreateOrRetrieveVaultAsync(rgName, vaultName, enableSoftDelete: true, enablePurgeProtection: false);
            var vaultUri = vault.Properties.VaultUri;
            Console.WriteLine("Operating with vault name '{0}' in resource group '{1}' and location '{2}'; storage account '{3}' (resId '{4}')", vaultName, rgName, vault.Location, storageAccountName, storageAccountResId);

            try
            {
                // list msas
                List<StorageAccountItem> msaList = new List<StorageAccountItem>();
                AzureOperationResponse<IPage<StorageAccountItem>> pageResponse;
                bool msaExists = false;

                // outer loop, retrieving storage accounts one page at a time
                for (pageResponse = await sample.DataClient.GetStorageAccountsWithHttpMessagesAsync(vault.Properties.VaultUri).ConfigureAwait(false);
                    ;
                    pageResponse = await sample.DataClient.GetStorageAccountsNextWithHttpMessagesAsync(pageResponse.Body.NextPageLink).ConfigureAwait(false))
                {
                    // inner loop, looking for a matching name
                    for (var it= pageResponse.Body.GetEnumerator(); it.MoveNext(); )
                    {
                        msaExists = (it.Current.Identifier.Name == managedStorageName);
                        if (msaExists)
                            break;
                    }

                    // break if found, or reached the last page
                    if (msaExists
                        || null == pageResponse.Body.NextPageLink)
                        break;
                }

                AzureOperationResponse<StorageBundle> retrievedMsaResponse;
                string regenPeriodStr;
                if (msaExists)
                {
                    // get msa from vault
                    Console.Write("Retrieving managed storage account - first attempt...");
                    retrievedMsaResponse = await sample.DataClient.GetStorageAccountWithHttpMessagesAsync(vaultUri, managedStorageName).ConfigureAwait(false);
                    Console.WriteLine("done.");
                }
                else
                {
                    // create msa: set Key1 as active, enable auto-regeneratio with a period of 30 days.
                    Console.Write("Creating a managed storage account...");
                    regenPeriodStr = System.Xml.XmlConvert.ToString(TimeSpan.FromDays(30.0));
                    var createdMsaResponse = await sample.DataClient.SetStorageAccountWithHttpMessagesAsync(vaultUri, managedStorageName,
                        storageAccountResId, "key1", autoRegenerateKey: true, regenerationPeriod: regenPeriodStr)
                        .ConfigureAwait(false);
                    Console.WriteLine("done.");

                    // confirm creation, retrieve msa
                    Console.Write("Retrieving managed storage account - second attempt...");
                    retrievedMsaResponse = await sample.DataClient.GetStorageAccountWithHttpMessagesAsync(vaultUri, managedStorageName).ConfigureAwait(false);
                    Console.WriteLine("done.");
                }

                // update msa: regenerate key1 on demand, set key2 as active, enable auto-regeneration with a period of 60 days.
                Console.Write("Updating managed storage account...");
                regenPeriodStr = System.Xml.XmlConvert.ToString(TimeSpan.FromDays(60.0));
                var updatedMsaResponse = await sample.DataClient.UpdateStorageAccountWithHttpMessagesAsync(vaultUri, managedStorageName, 
                    "key2", autoRegenerateKey: true, regenerationPeriod: regenPeriodStr)
                    .ConfigureAwait(false);
                Console.WriteLine("done.");

                // backup msa
                Console.Write("Backing up managed storage account...");
                var msaBackupResponse = await sample.DataClient.BackupStorageAccountWithHttpMessagesAsync(vaultUri, managedStorageName).ConfigureAwait(false);
                Console.WriteLine("done.");

                // delete msa
                Console.Write("Deleting managed storage account...");
                await sample.DataClient.DeleteStorageAccountWithHttpMessagesAsync(vaultUri, managedStorageName).ConfigureAwait(false);
                Console.WriteLine("done.");

                // if s/d enabled
                if (vault.Properties.EnableSoftDelete.HasValue
                    && vault.Properties.EnableSoftDelete.Value)
                {
                    Console.Write("Retrieving deleted managed storage account...");
                    AzureOperationResponse<DeletedStorageBundle> deletedMsaResponse = null;
                    await RetryHttpRequestAsync(
                        async () => { return deletedMsaResponse = await sample.DataClient.GetDeletedStorageAccountWithHttpMessagesAsync(vaultUri, managedStorageName).ConfigureAwait(false); },
                        "get deleted managed storage account",
                        SampleConstants.RetryPolicies.DefaultSoftDeleteRetryPolicy)
                        .ConfigureAwait(false);
                    Console.WriteLine("done.");

                    // recover msa
                    Console.Write("Recovering deleted managed storage account...");
                    var recoveredMsaResponse = await sample.DataClient.RecoverDeletedStorageAccountWithHttpMessagesAsync(vaultUri, managedStorageName).ConfigureAwait(false);
                    Console.WriteLine("done.");

                    // confirm recovery
                    Console.Write("Retrieving recovered managed storage account...");
                    await RetryHttpRequestAsync(
                        async () => { return retrievedMsaResponse = await sample.DataClient.GetStorageAccountWithHttpMessagesAsync(vaultUri, managedStorageName).ConfigureAwait(false); },
                        "retrieve deleted managed storage account",
                        SampleConstants.RetryPolicies.DefaultSoftDeleteRetryPolicy)
                        .ConfigureAwait(false);
                    Console.WriteLine("done.");

                    // delete msa
                    Console.Write("Deleting managed storage account (pass #2)...");
                    await sample.DataClient.DeleteStorageAccountWithHttpMessagesAsync(vaultUri, managedStorageName).ConfigureAwait(false);
                    Console.WriteLine("done.");

                    // retrieve deleted msa
                    Console.Write("Retrieving the deleted managed storage account (pass #2)...");
                    await RetryHttpRequestAsync(
                        async () => { return deletedMsaResponse = await sample.DataClient.GetDeletedStorageAccountWithHttpMessagesAsync(vaultUri, managedStorageName).ConfigureAwait(false); },
                        "get deleted managed storage account",
                        SampleConstants.RetryPolicies.DefaultSoftDeleteRetryPolicy)
                        .ConfigureAwait(false);
                    Console.WriteLine("done.");

                    // purge msa
                    Console.Write("Purging deleted managed storage account...");
                    await sample.DataClient.PurgeDeletedStorageAccountWithHttpMessagesAsync(vaultUri, managedStorageName).ConfigureAwait(false);
                    Console.WriteLine("done.");
                }

                // restore msak from backup; use retry to wait for the completion of the purge operation (if necessary)
                Console.Write("Restoring managed storage account from backup...");
                await RetryHttpRequestAsync(
                    async () => { return await sample.DataClient.RestoreStorageAccountWithHttpMessagesAsync(vaultUri, msaBackupResponse.Body.Value).ConfigureAwait(false); },
                    "restore deleted managed storage account",
                    SampleConstants.RetryPolicies.DefaultSoftDeleteRetryPolicy)
                    .ConfigureAwait(false);
                Console.WriteLine("done.");
            }
            catch (KeyVaultErrorException kvee)
            {
                Console.WriteLine("Unexpected KeyVault exception encountered: {0} ({1})", kvee.Message, kvee.Response.Content);

                throw;
            }
            catch (CloudException ce)
            {
                Console.WriteLine("Unexpected ARM exception encountered: {0}", ce.Message);

                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected exception encountered: {0}", e.Message);

                throw;
            }
        }

        /// <summary>
        /// Demonstrates management of SAS definitions for KeyVault-managed storage accounts.
        /// </summary>
        /// <returns>Task representing the asynchronous execution of this method.</returns>
        internal static async Task DemonstrateSASManagementAndUsageAsync()
        {
            // This sample demonstrates the management operations applicable to
            // SAS definitions corresponding to KeyVault-managed storage accounts, 
            // and performs the following workflow:
            //
            // - list existing storage accounts in a vault
            // - if the expected sample storage account does not exist, it will be created
            // - attempt to retrieve an existing managed storage account
            // - list existing SAS definitions associated with the managed storage account
            // - if the expected sample SAS definition does not exist, it will be created
            // - retrieve the SAS definition
            // - retrieve a SAS token based on the SAS definition
            // - delete a SAS definition
            // - if the vault is soft-delete enabled:
            //      - retrieve a deleted SAS definition
            //      - recover the deleted managed storage account
            //
            // Note that the sample attempts to reuse the same managed storage account as
            // in the DemonstrateStorageAccountManagementAsync method.
            //
            // Also note that SAS definitions may not be backed up individually, nor may they be purged.

            // instantiate the samples object
            var sample = new KeyVaultManagedStorageSamples();
            var rgName = sample.context.ResourceGroupName;
            var vaultName = sample.context.VaultName;
            var managedStorageName = "msakmgmtsample";
            var managedSasDefName = "sassample";
            var storageAccountName = sample.context.StorageAccountName;
            var storageAccountResId = sample.context.StorageAccountResourceId;

            // retrieve the vault or create one if it doesn't exist
            var vault = await sample.CreateOrRetrieveVaultAsync(rgName, vaultName, enableSoftDelete: true, enablePurgeProtection: false);
            var vaultUri = vault.Properties.VaultUri;
            Console.WriteLine("Operating with vault name '{0}' in resource group '{1}' and location '{2}'; storage account '{3}' (resId '{4}')", vaultName, rgName, vault.Location, storageAccountName, storageAccountResId);

            try
            {
                // list msas
                List<StorageAccountItem> msaList = new List<StorageAccountItem>();
                AzureOperationResponse<IPage<StorageAccountItem>> pageResponse;
                bool msaExists = false;

                // outer loop, retrieving storage accounts one page at a time
                for (pageResponse = await sample.DataClient.GetStorageAccountsWithHttpMessagesAsync(vault.Properties.VaultUri).ConfigureAwait(false);
                    ;
                    pageResponse = await sample.DataClient.GetStorageAccountsNextWithHttpMessagesAsync(pageResponse.Body.NextPageLink).ConfigureAwait(false))
                {
                    // inner loop, looking for a matching name
                    for (var it = pageResponse.Body.GetEnumerator(); it.MoveNext();)
                    {
                        msaExists = (it.Current.Identifier.Name == managedStorageName);
                        if (msaExists)
                            break;
                    }

                    // break if found, or reached the last page
                    if (msaExists
                        || null == pageResponse.Body.NextPageLink)
                        break;
                }

                AzureOperationResponse<StorageBundle> retrievedMsaResponse;
                string regenPeriodStr;
                if (msaExists)
                {
                    // get msa from vault
                    Console.Write("Retrieving managed storage account - first attempt...");
                    retrievedMsaResponse = await sample.DataClient.GetStorageAccountWithHttpMessagesAsync(vaultUri, managedStorageName).ConfigureAwait(false);
                    Console.WriteLine("done.");
                }
                else
                {
                    // create msa: set Key1 as active, enable auto-regeneratio with a period of 30 days.
                    Console.Write("Creating a managed storage account...");
                    regenPeriodStr = System.Xml.XmlConvert.ToString(TimeSpan.FromDays(30.0));
                    var createdMsaResponse = await sample.DataClient.SetStorageAccountWithHttpMessagesAsync(vaultUri, managedStorageName,
                        storageAccountResId, "key1", autoRegenerateKey: true, regenerationPeriod: regenPeriodStr)
                        .ConfigureAwait(false);
                    Console.WriteLine("done.");

                    // confirm creation, retrieve msa
                    Console.Write("Retrieving managed storage account - second attempt...");
                    retrievedMsaResponse = await sample.DataClient.GetStorageAccountWithHttpMessagesAsync(vaultUri, managedStorageName).ConfigureAwait(false);
                    Console.WriteLine("done.");
                }

                // list sas definitions
                List<SasDefinitionItem> sasList = new List<SasDefinitionItem>();
                AzureOperationResponse<IPage<SasDefinitionItem>> sasPageResponse;
                bool sasExists = false;

                // outer loop, retrieving sas definitions one page at a time
                for (sasPageResponse = await sample.DataClient.GetSasDefinitionsWithHttpMessagesAsync(vaultUri, managedStorageName).ConfigureAwait(false);
                    ;
                    sasPageResponse = await sample.DataClient.GetSasDefinitionsNextWithHttpMessagesAsync(sasPageResponse.Body.NextPageLink).ConfigureAwait(false))
                {
                    // inner loop, looking for a matching name
                    for (var it = sasPageResponse.Body.GetEnumerator(); it.MoveNext();)
                    {
                        sasExists = (it.Current.Identifier.Name == managedSasDefName);
                        if (sasExists)
                        {
                            // we may have found a match, but it might have expired.
                            // check the attributes, and reset the flag if the sas definition is disabled.
                            sasExists &= it.Current.Attributes.Enabled.HasValue && it.Current.Attributes.Enabled.Value;
                            break;
                        }
                    }

                    // break if found, or reached the last page
                    if (sasExists
                        || null == sasPageResponse.Body.NextPageLink)
                        break;
                }

                AzureOperationResponse<SasDefinitionBundle> retrievedSasResponse;
                if (sasExists)
                {
                    // get sas from vault
                    Console.Write("Retrieving existing sas definition...");
                    retrievedSasResponse = await sample.DataClient.GetSasDefinitionWithHttpMessagesAsync(vaultUri, managedStorageName, managedSasDefName).ConfigureAwait(false);
                    Console.WriteLine("done.");
                }
                else
                {
                    var validityPeriod = System.Xml.XmlConvert.ToString(TimeSpan.FromHours(24.0));

                    // create sas: use a predefined SAS template uri, 1 hour validity
                    Console.Write("Creating a SAS definition...");
                    var createdSasResponse = await sample.DataClient.SetSasDefinitionWithHttpMessagesAsync(vaultUri, managedStorageName,
                        managedSasDefName, SampleConstants.SasTemplateUri, SampleConstants.SasType.account.ToString(), validityPeriod)
                        .ConfigureAwait(false);
                    Console.WriteLine("done.");

                    // confirm creation, retrieve sas
                    Console.Write("Retrieving newly created sas definition...");
                    retrievedSasResponse = await sample.DataClient.GetSasDefinitionWithHttpMessagesAsync(vaultUri, managedStorageName, managedSasDefName).ConfigureAwait(false);
                    Console.WriteLine("done.");
                }

                // retrieve a token, via the secret corresponding to this sas definition
                Console.Write("Retrieving sas from corresponding secret...");
                var secretName = new SecretIdentifier(retrievedSasResponse.Body.SecretId).Name;
                var retrievedSecretResponse = await sample.DataClient.GetSecretWithHttpMessagesAsync(vaultUri, secretName, String.Empty).ConfigureAwait(false);
                Console.WriteLine("done.");

                // verify access to storage using the issued SAS
                await VerifyStorageAccessAsync(storageAccountName, retrievedSecretResponse.Body.Value).ConfigureAwait(false);

                // delete sas
                Console.Write("Deleting sas definition...");
                await sample.DataClient.DeleteSasDefinitionWithHttpMessagesAsync(vaultUri, managedStorageName, managedSasDefName).ConfigureAwait(false);
                Console.WriteLine("done.");

                // if s/d enabled
                if (vault.Properties.EnableSoftDelete.HasValue
                    && vault.Properties.EnableSoftDelete.Value)
                {
                    Console.Write("Retrieving deleted managed sas definition...");
                    AzureOperationResponse<DeletedSasDefinitionBundle> deletedSasResponse = null;
                    await RetryHttpRequestAsync(
                        async () => { return deletedSasResponse = await sample.DataClient.GetDeletedSasDefinitionWithHttpMessagesAsync(vaultUri, managedStorageName, managedSasDefName).ConfigureAwait(false); },
                        "get deleted managed sas definition",
                        SampleConstants.RetryPolicies.DefaultSoftDeleteRetryPolicy)
                        .ConfigureAwait(false);
                    Console.WriteLine("done.");

                    // recover sas
                    Console.Write("Recovering deleted managed sas definition...");
                    var recoveredSasResponse = await sample.DataClient.RecoverDeletedSasDefinitionWithHttpMessagesAsync(vaultUri, managedStorageName, managedSasDefName).ConfigureAwait(false);
                    Console.WriteLine("done.");

                    // confirm recovery
                    Console.Write("Retrieving recovered managed sas definition...");
                    await RetryHttpRequestAsync(
                        async () => { return retrievedSasResponse = await sample.DataClient.GetSasDefinitionWithHttpMessagesAsync(vaultUri, managedStorageName, managedSasDefName).ConfigureAwait(false); },
                        "retrieve deleted managed sas definition",
                        SampleConstants.RetryPolicies.DefaultSoftDeleteRetryPolicy)
                        .ConfigureAwait(false);
                    Console.WriteLine("done.");
                }
            }
            catch (KeyVaultErrorException kvee)
            {
                Console.WriteLine("Unexpected KeyVault exception encountered: {0} ({1})", kvee.Message, kvee.Response.Content);

                throw;
            }
            catch (CloudException ce)
            {
                Console.WriteLine("Unexpected ARM exception encountered: {0}", ce.Message);

                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected exception encountered: {0}", e.Message);

                throw;
            }
        }

        private static async Task VerifyStorageAccessAsync(string storageAccountName, string sas)
        {
            var storageBaseUriStr = "https://" + storageAccountName + ".blob.core.windows.net/";
            CloudBlobClient client = new CloudBlobClient(new Uri(storageBaseUriStr), new StorageCredentials(sas));

            try
            {
                var result = await client.ListContainersSegmentedAsync(null).ConfigureAwait(false);
            }
            catch (StorageException se)
            {
                Console.WriteLine("failed to enumerate blob containers using the specified storage account name and sas: {0}", se.Message);

                throw;
            }
        }
    }
}
