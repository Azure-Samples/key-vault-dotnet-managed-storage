using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Rest.Azure;

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

        internal static void DemonstrateSASManagementAsync()
        {
            throw new NotImplementedException();
        }

        internal static void DemonstrateSASConsumptionAsync()
        {
            throw new NotImplementedException();
        }
    }
}
